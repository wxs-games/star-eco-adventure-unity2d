using UnityEngine;
using UnityEngine.SceneManagement;
using StarEco.Framework;
using StarEco.Eco.Events;
using StarEco.EnemyAI;
using StarEco.EnemyAI.Events;

namespace StarEco.Level
{
    /// <summary>
    /// 污染度驱动的动态刷怪器（R5）：每条目独立计时、锚点轮询、对象池生成、名额随死亡回收。
    /// 间隔 = BaseInterval × Lerp(1.5, 0.5, 污染度)（污染越高乘数越小，刷得越快）。
    /// </summary>
    public class LevelSpawner : MonoBehaviour
    {
        [SerializeField] private LevelConfig _config;

        [Tooltip("锚点命名约定：关卡配置里 Anchors 为空时，运行时按此名前缀在场景中查找锚点（场景对象无法序列化进 asset）")]
        [SerializeField] private string _anchorNamePrefix = "SpawnAnchor";

        [Tooltip("每只敌人的重生次数上限（1 = 被打死后只重生一次，0 = 不重生）。每条目总生成数 = MaxAlive × (1 + 该值)")]
        [SerializeField] private int _respawnsPerEnemy = 1;

        private LevelObjectiveTracker _tracker;
        private EnemyControllerBase[] _prefabComponents;
        private Transform[][] _anchors;
        private float[] _timers;
        private int[] _aliveCounts;
        private int[] _spawnedTotals;   // 每条目累计生成数（含重生），用于限制重生次数
        private int[] _anchorCursor;
        private int[][] _waveRemaining;
        private float[][] _waveTimers;
        private Transform[][] _waveAnchors;
        private bool[] _waveTriggered;
        private bool _rightBankReached;
        private bool _rightBankTargetPurified;
        private float _rightBankPurifiedX;
        private bool _isLevel02;
        private StarEco.Player.PlayerController _player;
        private float _pollution = 1f;
        private bool _running;

        private void OnEnable()
        {
            EventCenter.Subscribe<PurityChangedEvent>(OnPurityChanged);
            EventCenter.Subscribe<EnemyDeathEvent>(OnEnemyDied);
            EventCenter.Subscribe<StarEco.Eco.Events.GarbagePurifiedEvent>(OnGarbagePurified);
            EventCenter.Subscribe<StarEco.Level.Events.ObjectiveCompletedEvent>(OnObjectiveCompleted);
        }

        private void OnDisable()
        {
            EventCenter.Unsubscribe<PurityChangedEvent>(OnPurityChanged);
            EventCenter.Unsubscribe<EnemyDeathEvent>(OnEnemyDied);
            EventCenter.Unsubscribe<StarEco.Eco.Events.GarbagePurifiedEvent>(OnGarbagePurified);
            EventCenter.Unsubscribe<StarEco.Level.Events.ObjectiveCompletedEvent>(OnObjectiveCompleted);
        }

        /// <summary>启动刷怪（LevelManager 进入 Playing 时调用）。</summary>
        public void Begin(LevelConfig config, LevelObjectiveTracker tracker)
        {
            _config = config;
            _tracker = tracker;
            _isLevel02 = _config != null && _config.LevelIndex == 2;
            _player = FindObjectOfType<StarEco.Player.PlayerController>();
            _pollution = 1f;

            int count = Entries?.Length ?? 0;
            _prefabComponents = new EnemyControllerBase[count];
            _anchors = new Transform[count][];
            _timers = new float[count];
            _aliveCounts = new int[count];
            _spawnedTotals = new int[count];
            _anchorCursor = new int[count];
            _rightBankReached = false;
            _rightBankTargetPurified = false;
            _rightBankPurifiedX = float.NegativeInfinity;

            var waves = _config != null ? _config.SpawnWaves : null;
            _waveRemaining = waves != null ? new int[waves.Length][] : null;
            _waveTimers = waves != null ? new float[waves.Length][] : null;
            _waveAnchors = waves != null ? new Transform[waves.Length][] : null;
            _waveTriggered = waves != null ? new bool[waves.Length] : null;
            if (waves != null)
            {
                for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
                {
                    _waveRemaining[waveIndex] = waves[waveIndex]?.Counts != null
                        ? (int[])waves[waveIndex].Counts.Clone()
                        : System.Array.Empty<int>();
                    _waveTimers[waveIndex] = new float[_waveRemaining[waveIndex].Length];
                    var wave = waves[waveIndex];
                    _waveAnchors[waveIndex] = wave != null && !string.IsNullOrEmpty(wave.AnchorNamePrefix)
                        ? ResolveAnchorPrefix(wave.AnchorNamePrefix)
                        : null;
                    if (_waveAnchors[waveIndex] != null && _waveAnchors[waveIndex].Length == 0)
                    {
                        _waveAnchors[waveIndex] = null;
                    }
                }
            }

            _running = count > 0;

            for (int i = 0; i < count; i++)
            {
                _prefabComponents[i] = ResolvePrefabComponent(i);
                ResolveAnchors(i);
            }

            if (_isLevel02 && waves != null && waves.Length > 0)
            {
                _running = count > 0;
                TriggerWaves(LevelConfig.SpawnWaveTrigger.GameStart, null, -1, 0f);
                return;
            }

            if (count == 0)
            {
                Debug.LogWarning("[LevelSpawner] 关卡配置没有刷怪表，本关不会有敌人出现。");
            }
        }

        /// <summary>停止刷怪（关卡进入终态）。已在场的敌人不处理。</summary>
        public void Stop()
        {
            _running = false;
        }

        private LevelConfig.SpawnEntry[] Entries => _config != null ? _config.SpawnEntries : null;

        private void Update()
        {
            if (!_running)
            {
                return;
            }

            var entries = Entries;
            if (entries == null)
            {
                return;
            }

            var waves = _config != null ? _config.SpawnWaves : null;
            if (_isLevel02 && waves != null && waves.Length > 0)
            {
                if (!_rightBankReached && _player != null && _player.IsGrounded)
                {
                    var rightBankWave = System.Array.Find(waves, wave =>
                        wave != null && wave.Trigger == LevelConfig.SpawnWaveTrigger.RightBankReached);
                    if (rightBankWave != null && _player.transform.position.x >= rightBankWave.TriggerX)
                    {
                        _rightBankReached = true;
                        TriggerWaves(LevelConfig.SpawnWaveTrigger.RightBankReached, null, -1, 0f);
                        TryTriggerRightBankPurifiedWave();
                    }
                }

                UpdateWaves();
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];

                if (entry.MaxAlive <= 0)
                {
                    continue;   // 上限 0 = 本条目禁用
                }

                if (_aliveCounts[i] >= entry.MaxAlive)
                {
                    continue;
                }

                // 重生次数上限：条目总生成数 = MaxAlive × (1 + 重生次数)，用完后不再补充
                int spawnLimit = entry.MaxAlive * (1 + Mathf.Max(0, _respawnsPerEnemy));
                if (_spawnedTotals[i] >= spawnLimit)
                {
                    continue;
                }

                // 目标链解锁检查（R5）
                if (entry.RequiredObjectiveIndex >= 0 &&
                    (_tracker == null || !_tracker.IsObjectiveCompleted(entry.RequiredObjectiveIndex)))
                {
                    continue;
                }

                _timers[i] -= Time.deltaTime;
                if (_timers[i] > 0f)
                {
                    continue;
                }

                _timers[i] = CurrentInterval(entry);

                if (TrySpawn(i, entry))
                {
                    _aliveCounts[i]++;
                    _spawnedTotals[i]++;
                }
            }
        }

        private float CurrentInterval(LevelConfig.SpawnEntry entry)
        {
            // 污染度 1（脏）→ 0.5 倍间隔（刷得快）；污染度 0（干净）→ 1.5 倍间隔（刷得慢）。
            return Mathf.Max(0.2f, entry.BaseInterval * Mathf.Lerp(1.5f, 0.5f, _pollution));
        }

        private bool TrySpawn(int index, LevelConfig.SpawnEntry entry)
        {
            var anchors = AnchorsFor(index);
            if (anchors == null || anchors.Length == 0)
            {
                Debug.LogError($"[LevelSpawner] 刷怪条目 {index} 没有锚点（配置未指定且场景无 {_anchorNamePrefix}* 对象），已跳过。");
                return false;
            }

            var anchor = anchors[_anchorCursor[index] % anchors.Length];
            _anchorCursor[index]++;
            return TrySpawnAt(index, anchor);
        }

        private bool TrySpawnAt(int index, Transform anchor)
        {
            var prefabComponent = _prefabComponents[index];
            if (prefabComponent == null || anchor == null || PoolManager.Instance == null)
            {
                return false;
            }

            var instance = PoolManager.Instance.Get(prefabComponent);
            instance.MarkPooled();

            // 池根节点在 DontDestroyOnLoad，下发出来的对象默认也留在该 Scene。
            // 运行时把敌人显式搬回关卡场景，避免层级混乱和跨场景残留副作用。
            var targetScene = anchor.gameObject.scene;
            if (targetScene.IsValid() && instance.gameObject.scene != targetScene)
            {
                SceneManager.MoveGameObjectToScene(instance.gameObject, targetScene);
            }

            instance.transform.position = anchor.position;

            // 巡逻原点必须跟着出生点走：池复用对象在 OnEnable 时记录的原点还在池里的位置。
            instance.BindHomeToCurrentPosition();

            // 不给出生即锁定玩家：目标交给敌人自己的感知，否则永远停在追击态、从不巡逻。
            return true;
        }

        /// <summary>
        /// 解析锚点：条目已配锚点则直接用；否则按命名约定在场景里找 SpawnAnchor*，
        /// 并按条目序号分配偶数/奇数锚点（两条条目各占一半位置，交替生成）。
        /// </summary>
        private void ResolveAnchors(int index)
        {
            var entries = Entries;
            if (entries == null || index >= entries.Length)
            {
                return;
            }

            var entry = entries[index];
            if (entry.Anchors != null && entry.Anchors.Length > 0)
            {
                return;   // 配置里已指定
            }

            string prefix = !string.IsNullOrEmpty(entry.AnchorNamePrefix)
                ? entry.AnchorNamePrefix
                : _anchorNamePrefix;
            var found = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            System.Collections.Generic.List<Transform> matched = null;
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i].name.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    matched ??= new System.Collections.Generic.List<Transform>();
                    matched.Add(found[i]);
                }
            }

            if (matched == null || matched.Count == 0)
            {
                return;   // 交给 TrySpawn 报错跳过
            }

            // FindObjectsByType 返回顺序不保证稳定，先按名字排序：
            // 否则条目 0/1 每次会话可能取到不同锚点，出生位置随机漂移。
            matched.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            // 未指定专属前缀时沿用旧的奇偶分配；专属前缀则使用全部匹配锚点。
            // 存运行时数组，绝不回写 LevelConfig 资产（避免 Editor 下资产被标脏持久化场景对象引用）。
            if (!string.IsNullOrEmpty(entry.AnchorNamePrefix))
            {
                _anchors[index] = matched.ToArray();
                return;
            }

            var subset = new System.Collections.Generic.List<Transform>();
            for (int i = index % 2; i < matched.Count; i += 2)
            {
                subset.Add(matched[i]);
            }

            _anchors[index] = subset.ToArray();
        }

        private Transform[] ResolveAnchorPrefix(string prefix)
        {
            var found = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            System.Array.Sort(found, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            var matches = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i].name.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    matches.Add(found[i]);
                }
            }

            if (matches.Count == 0)
            {
                Debug.LogError($"[LevelSpawner] 波次锚点前缀 {prefix} 未匹配到场景对象。");
            }

            return matches.ToArray();
        }

        /// <summary>该条目的运行时锚点（配置优先，其次运行时解析）。</summary>
        private Transform[] AnchorsFor(int index)
        {
            if (_anchors != null && index < _anchors.Length && _anchors[index] != null && _anchors[index].Length > 0)
            {
                return _anchors[index];
            }

            var entries = Entries;
            return entries != null && index < entries.Length ? entries[index].Anchors : null;
        }

        private EnemyControllerBase ResolvePrefabComponent(int index)        {
            var entries = Entries;
            if (entries == null || index >= entries.Length)
            {
                return null;
            }

            var entry = entries[index];
            if (entry.Prefab == null)
            {
                Debug.LogError($"[LevelSpawner] 刷怪条目 {index} 未配置敌人预制体，已跳过。");
                return null;
            }

            var comp = entry.Prefab.GetComponent<EnemyControllerBase>();
            if (comp == null)
            {
                Debug.LogError($"[LevelSpawner] 预制体 {entry.Prefab.name} 上没有 EnemyControllerBase 派生组件，已跳过。");
            }

            return comp;
        }

        private void OnPurityChanged(PurityChangedEvent evt)
        {
            _pollution = evt.Pollution;
        }

        private void OnGarbagePurified(StarEco.Eco.Events.GarbagePurifiedEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.TargetId))
            {
                TriggerWaves(LevelConfig.SpawnWaveTrigger.TargetPurified, evt.TargetId, -1, evt.Position.x);
            }

            var waves = _config != null ? _config.SpawnWaves : null;
            if (waves == null)
            {
                return;
            }

            for (int i = 0; i < waves.Length; i++)
            {
                var wave = waves[i];
                if (wave != null && wave.Trigger == LevelConfig.SpawnWaveTrigger.PurifiedInRegion &&
                    evt.Position.x >= wave.MinTargetX)
                {
                    _rightBankTargetPurified = true;
                    _rightBankPurifiedX = Mathf.Max(_rightBankPurifiedX, evt.Position.x);
                    break;
                }
            }

            TryTriggerRightBankPurifiedWave();
        }

        private void TryTriggerRightBankPurifiedWave()
        {
            if (_rightBankReached && _rightBankTargetPurified)
            {
                TriggerWaves(LevelConfig.SpawnWaveTrigger.PurifiedInRegion, null, -1, _rightBankPurifiedX);
            }
        }

        private void OnObjectiveCompleted(StarEco.Level.Events.ObjectiveCompletedEvent evt)
        {
            TriggerWaves(LevelConfig.SpawnWaveTrigger.ObjectiveCompleted, null, evt.Index, 0f);
        }

        private void TriggerWaves(LevelConfig.SpawnWaveTrigger trigger, string targetId, int objectiveIndex, float targetX)
        {
            var waves = _config != null ? _config.SpawnWaves : null;
            if (!_isLevel02 || waves == null || _waveTriggered == null)
            {
                return;
            }

            for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
            {
                var wave = waves[waveIndex];
                if (_waveTriggered[waveIndex] || wave == null || wave.Trigger != trigger)
                {
                    continue;
                }

                if (trigger == LevelConfig.SpawnWaveTrigger.TargetPurified && wave.TargetId != targetId)
                {
                    continue;
                }

                if (trigger == LevelConfig.SpawnWaveTrigger.PurifiedInRegion && targetX < wave.MinTargetX)
                {
                    continue;
                }

                if (trigger == LevelConfig.SpawnWaveTrigger.RightBankReached &&
                    (_player == null || _player.transform.position.x < wave.TriggerX))
                {
                    continue;
                }

                if (trigger == LevelConfig.SpawnWaveTrigger.ObjectiveCompleted &&
                    wave.ObjectiveIndex != objectiveIndex)
                {
                    continue;
                }

                _waveTriggered[waveIndex] = true;
                var indices = wave.EntryIndices ?? System.Array.Empty<int>();
                var counts = _waveRemaining[waveIndex];
                if (indices.Length != counts.Length)
                {
                    Debug.LogError($"[LevelSpawner] 波次 {waveIndex} 的 EntryIndices/Counts 长度不一致。");
                    continue;
                }

                for (int i = 0; i < indices.Length; i++)
                {
                    int entryIndex = indices[i];
                    if (entryIndex < 0 || entryIndex >= (Entries?.Length ?? 0) || counts[i] < 0)
                    {
                        Debug.LogError($"[LevelSpawner] 波次 {waveIndex} 配置了无效敌人条目或数量。");
                        counts[i] = 0;
                    }
                }
            }
        }

        private void UpdateWaves()
        {
            if (_waveRemaining == null || Entries == null)
            {
                return;
            }

            for (int waveIndex = 0; waveIndex < _waveRemaining.Length; waveIndex++)
            {
                if (!_waveTriggered[waveIndex])
                {
                    continue;
                }

                var wave = _config.SpawnWaves[waveIndex];
                var indices = wave.EntryIndices;
                var counts = _waveRemaining[waveIndex];
                for (int i = 0; i < indices.Length; i++)
                {
                    int entryIndex = indices[i];
                    var entry = Entries[entryIndex];
                    if (counts[i] <= 0 || _aliveCounts[entryIndex] >= entry.MaxAlive)
                    {
                        continue;
                    }

                    _waveTimers[waveIndex][i] -= Time.deltaTime;
                    if (_waveTimers[waveIndex][i] > 0f)
                    {
                        continue;
                    }

                    var waveAnchors = _waveAnchors[waveIndex];
                    if (waveAnchors != null && waveAnchors.Length == 0)
                    {
                        continue;
                    }

                    bool spawned = waveAnchors != null
                        ? TrySpawnAt(entryIndex, waveAnchors[_anchorCursor[entryIndex]++ % waveAnchors.Length])
                        : TrySpawn(entryIndex, entry);
                    if (spawned)
                    {
                        _aliveCounts[entryIndex]++;
                        _spawnedTotals[entryIndex]++;
                        counts[i]--;
                        _waveTimers[waveIndex][i] = Mathf.Max(0.2f, entry.BaseInterval);
                    }
                }
            }
        }

        private void OnEnemyDied(EnemyDeathEvent evt)
        {
            var entries = Entries;
            if (entries == null || string.IsNullOrEmpty(evt.EnemyTypeId))
            {
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                var comp = _prefabComponents[i];
                if (comp == null || comp.Config == null)
                {
                    continue;
                }

                if (comp.Config.EnemyTypeId == evt.EnemyTypeId)
                {
                    _aliveCounts[i] = Mathf.Max(0, _aliveCounts[i] - 1);
                }
            }
        }
    }
}
