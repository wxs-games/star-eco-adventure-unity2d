using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using StarEco.Framework;
using StarEco.Eco.Events;
using StarEco.EnemyAI;
using StarEco.Level.Events;

namespace StarEco.Level
{
    /// <summary>
    /// 水流解谜控制器（M9 R3）：猴子救援 + 指示卡片 + 清堵塞全部完成后升桥。
    /// 监听动物救援、知识卡片拾取和 WaterBlock 净化事件；三类前置条件全部满足后：
    /// 水位 1.5s 平滑上升 → 激活限时木筏通路（RaftPath 组）→ 生成桥上敌人 → 广播 PuzzleSolvedEvent。
    /// 通路初始禁用（不过河够不着右侧区域），解谜完成后限时开放，计时结束整组关闭。
    /// 计数与 LevelObjectiveTracker 的 PuzzleSolved 目标同源（都走 WaterBlock 前缀事件），
    /// 但互不依赖：Tracker 管目标链，本组件管场景表现。
    /// </summary>
    public class WaterPuzzleController : MonoBehaviour
    {
        private const string BlockPrefix = "WaterBlock";
        private const string CardForestChain = "kc_food_chain";
        private const string CardForestOxygen = "kc_forest_oxygen";

        [Tooltip("堵塞点列表（为空时启动时自动扫描场景 WaterBlockage）")]
        [SerializeField] private WaterBlockageBridge[] _blockages = new WaterBlockageBridge[0];

        [Tooltip("水位视觉（整体上移的对象，如水域色块根）")]
        [SerializeField] private Transform _waterVisual;

        [Tooltip("水位低位 Y（初始）")]
        [SerializeField] private float _lowY = 0f;

        [Tooltip("水位高位 Y（上升后）")]
        [SerializeField] private float _highY = 1.2f;

        [Tooltip("上升时长（s）")]
        [SerializeField] private float _riseDuration = 1.5f;

        [Tooltip("木筏通路（初始禁用，解谜完成后激活）")]
        [SerializeField] private GameObject _raftPath;

        [Header("限时桥遭遇")]
        [Tooltip("桥架起后生成的敌人预制体；为空则不生成敌人")]
        [SerializeField] private EnemyControllerBase _bridgeEnemyPrefab;

        [Tooltip("桥上敌人的出生点；为空时使用桥通路中心")]
        [SerializeField] private Transform _bridgeEnemyAnchor;

        [Tooltip("桥架起后保持存在的时间（秒），到时整段桥通路碎掉")]
        [SerializeField] private float _bridgeLifetime = 3f;

        private readonly HashSet<string> _cleared = new HashSet<string>();
        private readonly HashSet<string> _rescued = new HashSet<string>();
        private readonly HashSet<string> _collectedCards = new HashSet<string>();
        private StarEco.Level.LevelStats _stats;
        private float _startTime;
        private bool _solved;
        private bool _rising;
        private float _riseT;
        private bool _bridgeActive;
        private float _bridgeTimer;
        private EnemyControllerBase _bridgeEnemy;
        private StarEco.Player.PlayerController _player;
        private Collider2D _playerCollider;

        /// <summary>解谜是否已完成。</summary>
        public bool IsSolved => _solved;

        /// <summary>已清理堵塞点数。</summary>
        public int ClearedCount => _cleared.Count;

        /// <summary>Bridge 序列化适配：让 Inspector/Setup 工具能挂场景对象引用。</summary>
        [System.Serializable]
        public class WaterBlockageBridge
        {
            public StarEco.Eco.WaterBlockage Blockage;
        }

        private void OnEnable()
        {
            EventCenter.Subscribe<GarbagePurifiedEvent>(OnGarbagePurified);
            EventCenter.Subscribe<AnimalRescuedEvent>(OnAnimalRescued);
            EventCenter.Subscribe<KnowledgeCardCollectedEvent>(OnKnowledgeCardCollected);
        }

        private void OnDisable()
        {
            EventCenter.Unsubscribe<GarbagePurifiedEvent>(OnGarbagePurified);
            EventCenter.Unsubscribe<AnimalRescuedEvent>(OnAnimalRescued);
            EventCenter.Unsubscribe<KnowledgeCardCollectedEvent>(OnKnowledgeCardCollected);
        }

        private void Start()
        {
            _stats = FindObjectOfType<StarEco.Level.LevelStats>();
            _startTime = _stats != null ? _stats.ElapsedSeconds : Time.time;
            _player = FindObjectOfType<StarEco.Player.PlayerController>();
            _playerCollider = _player != null ? _player.GetComponent<Collider2D>() : null;

            // 未在 Inspector 挂堵塞点则自动扫描（Setup 工具会显式回填，这里是兜底）
            if (_blockages == null || _blockages.Length == 0)
            {
                var found = FindObjectsOfType<StarEco.Eco.WaterBlockage>();
                var list = new List<WaterBlockageBridge>();
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] != null && !found[i].IsPurified)
                    {
                        list.Add(new WaterBlockageBridge { Blockage = found[i] });
                    }
                }
                _blockages = list.ToArray();
                if (_blockages.Length == 0)
                {
                    Debug.LogWarning("[WaterPuzzle] 场景中没有堵塞点，解谜将立即完成。");
                }
            }

            if (_waterVisual != null)
            {
                _waterVisual.position = new Vector3(_waterVisual.position.x, _lowY, _waterVisual.position.z);
            }

            if (_raftPath != null)
            {
                _raftPath.SetActive(false);
            }
        }

        private void Update()
        {
            if (_bridgeActive)
            {
                if (IsPlayerStandingOnBridge())
                {
                    _bridgeTimer += Time.deltaTime;
                    if (_bridgeLifetime > 0f && _bridgeTimer >= _bridgeLifetime)
                    {
                        BreakBridge();
                    }
                }
                else if (_bridgeTimer > 0f)
                {
                    _bridgeTimer = 0f;
                    Debug.Log("[WaterPuzzle] 玩家已离开木筏，桥梁计时已重置。");
                }
            }

            if (!_rising)
            {
                return;
            }

            _riseT += Time.deltaTime;
            float t = _riseDuration > 0f ? Mathf.Clamp01(_riseT / _riseDuration) : 1f;

            if (_waterVisual != null)
            {
                _waterVisual.position = new Vector3(
                    _waterVisual.position.x, Mathf.Lerp(_lowY, _highY, t), _waterVisual.position.z);
            }

            if (t >= 1f)
            {
                _rising = false;
                if (_raftPath != null)
                {
                    _raftPath.SetActive(true);
                    ActivateBridgeEncounter();
                    Debug.Log("[WaterPuzzle] 水位上升完成，木筏通路已激活，限时桥遭遇已开始。");
                }
            }
        }

        private void ActivateBridgeEncounter()
        {
            if (_bridgeActive)
            {
                return;
            }

            _bridgeActive = true;
            _bridgeTimer = 0f;

            if (_player == null)
            {
                _player = FindObjectOfType<StarEco.Player.PlayerController>();
            }
            if (_playerCollider == null && _player != null)
            {
                _playerCollider = _player.GetComponent<Collider2D>();
            }

            if (_bridgeEnemyPrefab == null || PoolManager.Instance == null)
            {
                return;
            }

            Transform anchor = _bridgeEnemyAnchor != null
                ? _bridgeEnemyAnchor
                : _raftPath != null ? _raftPath.transform : transform;
            _bridgeEnemy = PoolManager.Instance.Get(_bridgeEnemyPrefab);
            _bridgeEnemy.MarkPooled();
            _bridgeEnemy.SetCountsForLevelObjective(false);
            Scene targetScene = anchor.gameObject.scene;
            if (targetScene.IsValid() && _bridgeEnemy.gameObject.scene != targetScene)
            {
                SceneManager.MoveGameObjectToScene(_bridgeEnemy.gameObject, targetScene);
            }

            // 锚点语义是「桥上落脚点」（脚底高度）。敌人原点在精灵中心，碰撞体还另有 offset
            // （Enemy02 为 -0.824，高 1.65 → 底面比原点低 1.65u），
            // 直接按原点摆放会让碰撞体扎进河底溺水区，敌人一出生就被 DrownHazard 秒掉。
            // 先用「碰撞体底面相对原点的偏移」换算目标原点，再一次性落位（不依赖物理同步时机）。
            var enemyCollider = _bridgeEnemy.GetComponent<Collider2D>();
            float bottomOffset = enemyCollider != null
                ? enemyCollider.bounds.min.y - _bridgeEnemy.transform.position.y
                : 0f;
            _bridgeEnemy.transform.position = new Vector3(
                anchor.position.x, anchor.position.y - bottomOffset, anchor.position.z);

            _bridgeEnemy.BindHomeToCurrentPosition();
            Debug.Log($"[WaterPuzzle] 桥上敌人已生成，玩家站上木筏后开始 {_bridgeLifetime:F1}s 计时。");
        }

        private bool IsPlayerStandingOnBridge()
        {
            if (_player == null || _player.IsDead || !_player.IsGrounded || _playerCollider == null ||
                _raftPath == null || !_raftPath.activeInHierarchy)
            {
                return false;
            }

            Bounds playerBounds = _playerCollider.bounds;
            var bridgeColliders = _raftPath.GetComponentsInChildren<Collider2D>(false);
            for (int i = 0; i < bridgeColliders.Length; i++)
            {
                var bridgeCollider = bridgeColliders[i];
                if (bridgeCollider == null || !bridgeCollider.enabled || bridgeCollider.isTrigger)
                {
                    continue;
                }

                Bounds bridgeBounds = bridgeCollider.bounds;
                bool overlapsHorizontally = playerBounds.max.x > bridgeBounds.min.x &&
                    playerBounds.min.x < bridgeBounds.max.x;
                bool feetOnTop = playerBounds.min.y >= bridgeBounds.max.y - 0.12f &&
                    playerBounds.min.y <= bridgeBounds.max.y + 0.22f;
                if (overlapsHorizontally && feetOnTop)
                {
                    return true;
                }
            }

            return false;
        }

        private void BreakBridge()
        {
            if (!_bridgeActive)
            {
                return;
            }

            _bridgeActive = false;

            // 这只敌人是本次桥遭遇专属实例。桥消失前显式归还到本次使用的池，避免它掉进河道后
            // 走 EnemyControllerBase 的旧回收路径，把 Enemy02(Clone) 当成池键而产生警告。
            if (_bridgeEnemy != null)
            {
                if (_bridgeEnemyPrefab != null && PoolManager.Instance != null)
                {
                    PoolManager.Instance.Release(_bridgeEnemyPrefab, _bridgeEnemy);
                }
                else
                {
                    _bridgeEnemy.gameObject.SetActive(false);
                }
                _bridgeEnemy = null;
            }

            if (_raftPath != null)
            {
                _raftPath.SetActive(false);
            }

            Debug.Log("[WaterPuzzle] 木筏计时结束，桥已碎裂，木筏通路关闭。");
        }

        // ===== 事件 =====

        private void OnGarbagePurified(GarbagePurifiedEvent evt)
        {
            if (_solved)
            {
                return;
            }

            if (string.IsNullOrEmpty(evt.TargetId) || !evt.TargetId.StartsWith(BlockPrefix, System.StringComparison.Ordinal))
            {
                return;   // 普通污染源与解谜无关
            }

            _cleared.Add(evt.TargetId);
            Debug.Log($"[WaterPuzzle] 堵塞点 {evt.TargetId} 已清理（{_cleared.Count}/{ExpectedTotal()}）");

            TrySolveAfterPrerequisites();
        }

        private void OnAnimalRescued(AnimalRescuedEvent evt)
        {
            if (_solved || string.IsNullOrEmpty(evt.TargetId))
            {
                return;
            }

            _rescued.Add(evt.TargetId);
            Debug.Log($"[WaterPuzzle] 已解救 {evt.TargetId}（{_rescued.Count}/2）");
            TrySolveAfterPrerequisites();
        }

        private void OnKnowledgeCardCollected(KnowledgeCardCollectedEvent evt)
        {
            if (_solved || string.IsNullOrEmpty(evt.CardId) || !IsRequiredCard(evt.CardId))
            {
                return;
            }

            _collectedCards.Add(evt.CardId);
            Debug.Log($"[WaterPuzzle] 已收集指示卡片 {evt.CardId}（{_collectedCards.Count}/2）");
            TrySolveAfterPrerequisites();
        }

        private void TrySolveAfterPrerequisites()
        {
            if (!_solved && _cleared.Count >= ExpectedTotal() &&
                _rescued.Count >= 2 && _collectedCards.Count >= 2)
            {
                Solve();
            }
        }

        private static bool IsRequiredCard(string cardId)
        {
            return cardId == CardForestChain || cardId == CardForestOxygen;
        }

        private int ExpectedTotal()
        {
            if (_blockages == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < _blockages.Length; i++)
            {
                if (_blockages[i] != null && _blockages[i].Blockage != null)
                {
                    count++;
                }
            }
            return count;
        }

        private void Solve()
        {
            if (_solved)
            {
                return;
            }

            _solved = true;
            _rising = true;
            _riseT = 0f;

            AudioManager.Instance?.PlaySfx(SfxId.PuzzleSolved);   // M10

            float elapsed = _stats != null
                ? _stats.ElapsedSeconds - _startTime
                : Time.time - _startTime;

            EventCenter.Publish(new PuzzleSolvedEvent
            {
                Position = _waterVisual != null ? (Vector2)_waterVisual.position : transform.position,
                ElapsedInPuzzle = elapsed
            });

            Debug.Log($"[WaterPuzzle] 解谜完成：全部堵塞点已清理，水位上升中（耗时 {elapsed:F1}s）。");
        }
    }
}
