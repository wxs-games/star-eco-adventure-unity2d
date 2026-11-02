using System;
using UnityEngine;
using StarEco.Framework;
using StarEco.Eco;
using StarEco.Eco.Events;
using StarEco.EnemyAI;
using StarEco.EnemyAI.Events;
using StarEco.Level.Events;

namespace StarEco.Level
{
    /// <summary>
    /// 目标链追踪器（R3）：按 LevelConfig.Objectives 顺序解锁，只有当前目标参与计数。
    /// 分类计数与 M5 解耦：塔 vs 污染源按 GarbagePurifiedEvent.TargetId 前缀判定（design §2.2 已知取舍）。
    /// </summary>
    public class LevelObjectiveTracker : MonoBehaviour
    {
        private const string TowerIdPrefix = "Tower";
        private const string WaterBlockPrefix = "WaterBlock";   // M9：河道堵塞点单独计数
        private const float RightBankMinX = 7f;

        [SerializeField] private LevelConfig _config;

        private PuritySystem _purity;
        private LevelStats _stats;
        private Action _onChainCompleted;

        private int _index;
        private bool _initialized;

        /// <summary>目标链完成回调的代次令牌：关卡重开时自增，用于作废上一局遗留的延迟回调。</summary>
        private int _chainVersion;

        /// <summary>并行模式：所有目标独立计数，全部达标后才通关。第三关启用。</summary>
        private bool _parallelMode;

        // 各类型累计计数（只由当前目标推进时写入）
        private int _purifiedTargets;
        private int _purifiedTowers;
        private int _defeatedEnemies;
        private int _rightBankPurified;
        private int _rightBankDefeated;
        private int _rescuedAnimals;   // M9 R2：AnimalRescuedEvent 计数
        private int _defeatedBoss;     // T-28：BossDefeatedEvent 计数（与"击杀任意敌人"严格区分）
        private int _waterCleared;     // M9 R3：WaterBlock 前缀 GarbagePurifiedEvent 计数
        private int _collectedKnowledgeCards;
        private readonly System.Collections.Generic.HashSet<string> _collectedKnowledgeCardIds =
            new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        private readonly System.Collections.Generic.HashSet<int> _rightBankPurifiedInstanceIds =
            new System.Collections.Generic.HashSet<int>();
        private readonly System.Collections.Generic.HashSet<string> _rightBankPurifiedFallbackIds =
            new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        private readonly System.Collections.Generic.HashSet<int> _rightBankEnemyInstanceIds =
            new System.Collections.Generic.HashSet<int>();

        // 运行时夹取后的 Required（不修改 LevelConfig 资产本身，避免 Editor 下资产被标脏持久化）
        private int[] _effectiveRequired;

        /// <summary>当前目标序号（-1 = 链已完成/未初始化）。</summary>
        public int CurrentIndex => _index;

        /// <summary>目标链是否全部完成。</summary>
        public bool ChainCompleted { get; private set; }

        /// <summary>当前目标配置（链完成时为 null）。</summary>
        public LevelConfig.LevelObjective CurrentObjective =>
            _config != null && _index >= 0 && _index < (_config.Objectives?.Length ?? 0)
                ? _config.Objectives[_index]
                : null;

        /// <summary>当前目标的运行时所需数量（已夹取）。</summary>
        public int CurrentRequired =>
            _effectiveRequired != null && _index >= 0 && _index < _effectiveRequired.Length
                ? _effectiveRequired[_index]
                : 0;

        /// <summary>
        /// 当前目标已完成的计数（M7 HUD 初始化时主动拉取用：
        /// 首帧的 LevelObjectiveChangedEvent 可能早于 HUD 订阅，需要能回头读一次）。
        /// </summary>
        public int CurrentProgress
        {
            get
            {
                var objective = CurrentObjective;
                return objective != null ? CurrentCountFor(objective.Type) : 0;
            }
        }

        private void OnEnable()
        {
            EventCenter.Subscribe<GarbagePurifiedEvent>(OnGarbagePurified);
            EventCenter.Subscribe<EnemyDeathEvent>(OnEnemyDied);
            EventCenter.Subscribe<PurityChangedEvent>(OnPurityChanged);
            EventCenter.Subscribe<AnimalRescuedEvent>(OnAnimalRescued);   // M9 R2
            EventCenter.Subscribe<KnowledgeCardCollectedEvent>(OnKnowledgeCardCollected);
            EventCenter.Subscribe<TowerDisableEvent>(OnTowerDestroyed);   // 炮塔改为攻击击毁后由此计数
            EventCenter.Subscribe<StarEco.Boss.Events.BossDefeatedEvent>(OnBossDefeated);   // T-28：击败 Boss 目标
        }

        private void OnDisable()
        {
            EventCenter.Unsubscribe<GarbagePurifiedEvent>(OnGarbagePurified);
            EventCenter.Unsubscribe<EnemyDeathEvent>(OnEnemyDied);
            EventCenter.Unsubscribe<PurityChangedEvent>(OnPurityChanged);
            EventCenter.Unsubscribe<AnimalRescuedEvent>(OnAnimalRescued);
            EventCenter.Unsubscribe<KnowledgeCardCollectedEvent>(OnKnowledgeCardCollected);
            EventCenter.Unsubscribe<TowerDisableEvent>(OnTowerDestroyed);
            EventCenter.Unsubscribe<StarEco.Boss.Events.BossDefeatedEvent>(OnBossDefeated);   // T-28：与 OnEnable 配对
        }

        /// <summary>
        /// 初始化目标链：夹取不合理 Required、激活首个目标。
        /// </summary>
        /// <param name="config">关卡配置（null 时回退默认配置）。</param>
        /// <param name="purity">净化系统（ReachPurity 目标需要）。</param>
        /// <param name="stats">统计数据（DefeatEnemies 计数展示）。</param>
        /// <param name="onChainCompleted">目标链全部完成回调（LevelManager 传，成功时用于通关判据）。</param>
        public void Initialize(LevelConfig config, PuritySystem purity, LevelStats stats, Action onChainCompleted)
        {
            _config = config != null ? config : LevelConfig.CreateDefault();
            _purity = purity;
            _stats = stats;
            _onChainCompleted = onChainCompleted;

            _index = 0;
            _purifiedTargets = 0;
            _purifiedTowers = 0;
            _defeatedEnemies = 0;
            _rightBankPurified = 0;
            _rightBankDefeated = 0;
            _rescuedAnimals = 0;
            _waterCleared = 0;
            _collectedKnowledgeCards = 0;
            _collectedKnowledgeCardIds.Clear();
            _defeatedBoss = 0;   // T-28
            _rightBankPurifiedInstanceIds.Clear();
            _rightBankPurifiedFallbackIds.Clear();
            _rightBankEnemyInstanceIds.Clear();
            ChainCompleted = false;
            _chainVersion++;   // 作废上一局遗留的延迟完成回调

            // 所有关卡：通关条件无先后顺序，全部达标即可
            _parallelMode = true;

            ClampRequired();

            _initialized = true;
            BroadcastCurrent();
        }

        /// <summary>重置为未开始（关卡重启）。</summary>
        public void ResetChain()
        {
            _index = 0;
            _purifiedTargets = 0;
            _purifiedTowers = 0;
            _defeatedEnemies = 0;
            _rightBankPurified = 0;
            _rightBankDefeated = 0;
            _rescuedAnimals = 0;
            _waterCleared = 0;
            _collectedKnowledgeCards = 0;
            _collectedKnowledgeCardIds.Clear();
            _defeatedBoss = 0;   // T-28
            _rightBankPurifiedInstanceIds.Clear();
            _rightBankPurifiedFallbackIds.Clear();
            _rightBankEnemyInstanceIds.Clear();
            ChainCompleted = false;
            _parallelMode = false;
            _initialized = false;
            _chainVersion++;   // 作废上一局遗留的延迟完成回调
        }

        /// <summary>指定序号目标是否已完成（LevelSpawner 用它判断刷怪解锁）。</summary>
        public bool IsObjectiveCompleted(int objectiveIndex)
        {
            return objectiveIndex < 0 || objectiveIndex < _index;
        }

        // ===== 事件 =====

        private void OnGarbagePurified(GarbagePurifiedEvent evt)
        {
            if (IsLevel02RightBank(evt.Position.x) &&
                !string.IsNullOrEmpty(evt.TargetId) &&
                !evt.TargetId.StartsWith(WaterBlockPrefix, StringComparison.Ordinal) &&
                ((evt.InstanceId != 0 && _rightBankPurifiedInstanceIds.Add(evt.InstanceId)) ||
                 (evt.InstanceId == 0 && _rightBankPurifiedFallbackIds.Add(evt.TargetId))))
            {
                _rightBankPurified++;
            }

            // M9 R3：河道堵塞点单独计数（PuzzleSolved），不算普通污染源
            if (!string.IsNullOrEmpty(evt.TargetId) &&
                evt.TargetId.StartsWith(WaterBlockPrefix, StringComparison.Ordinal))
            {
                _waterCleared++;
                Evaluate();
                return;
            }

            if (IsTower(evt.TargetId))
            {
                _purifiedTowers++;
            }
            else
            {
                _purifiedTargets++;
            }

            Evaluate();
        }

        private void OnEnemyDied(EnemyDeathEvent evt)
        {
            _defeatedEnemies++;
            if (evt.CountsForLevelObjective &&
                (evt.InstanceId == 0 || _rightBankEnemyInstanceIds.Add(evt.InstanceId)) &&
                IsLevel02RightBank(evt.Position.x))
            {
                _rightBankDefeated++;
            }
            Evaluate();
        }

        /// <summary>炮塔被击毁（攻击击杀）后计入"击毁塔"目标。</summary>
        private void OnTowerDestroyed(TowerDisableEvent evt)
        {
            _purifiedTowers++;
            Evaluate();
        }

        private void OnPurityChanged(PurityChangedEvent evt)
        {
            Evaluate();
        }

        /// <summary>M9 R2：解救被困动物计数。</summary>
        private void OnAnimalRescued(AnimalRescuedEvent evt)
        {
            _rescuedAnimals++;
            Evaluate();
        }

        private void OnKnowledgeCardCollected(KnowledgeCardCollectedEvent evt)
        {
            if (_config == null || string.IsNullOrEmpty(evt.CardId))
            {
                return;
            }

            // 按本次进入关卡实际拾取事件计数，不受历史存档影响；第 2 关与第 3 关均适用。
            if (_collectedKnowledgeCardIds.Add(evt.CardId))
            {
                _collectedKnowledgeCards++;
                Evaluate();
            }
        }

        /// <summary>
        /// T-28：击败 Boss 计数。
        /// 走独立的 BossDefeatedEvent，而不是复用 EnemyDeathEvent 的"任意敌人"计数——
        /// 后者会让 L3 路上死一只小怪就通关，跳过 Boss 战。
        /// </summary>
        private void OnBossDefeated(StarEco.Boss.Events.BossDefeatedEvent evt)
        {
            _defeatedBoss++;
            Evaluate();
        }

        // ===== 推进 =====

        private void Evaluate()
        {
            if (!_initialized || ChainCompleted)
            {
                return;
            }

            // 第三关并行模式：所有目标独立计数，全部达标后才通关
            if (_parallelMode)
            {
                EvaluateParallel();
                return;
            }

            // M9 修复：循环推进到首个未满足目标。
            // 若新激活目标的计数已被先前事件满足（提前击杀 / 提前净化 / 提前解救），
            // 单次 if 会让链停滞到下一次同类事件才恢复，必须继续检查。
            while (!ChainCompleted)
            {
                var objective = CurrentObjective;
                if (objective == null)
                {
                    CompleteChain();
                    return;
                }

                int required = CurrentRequired;
                int current = CurrentCountFor(objective.Type);

                if (current < required)
                {
                    BroadcastCurrent();   // 计数变化即刷新目标文本
                    return;
                }

                EventCenter.Publish(new ObjectiveCompletedEvent { Index = _index, Label = objective.Label });

                _index++;
                if (_index >= (_config.Objectives?.Length ?? 0))
                {
                    CompleteChain();
                    return;
                }
            }
        }

        /// <summary>并行模式下的目标完成度评估：所有目标独立计数，全部达标后才触发 CompleteChain。</summary>
        private void EvaluateParallel()
        {
            if (_config.Objectives == null || _config.Objectives.Length == 0)
            {
                CompleteChain();
                return;
            }

            bool allCompleted = true;
            int firstIncompleteIndex = -1;
            for (int i = 0; i < _config.Objectives.Length; i++)
            {
                var obj = _config.Objectives[i];
                if (obj == null)
                {
                    continue;
                }
                int required = _effectiveRequired[i];
                int current = CurrentCountFor(obj.Type);
                if (current < required)
                {
                    allCompleted = false;
                    if (firstIncompleteIndex < 0)
                    {
                        firstIncompleteIndex = i;
                    }
                }
            }

            if (allCompleted)
            {
                CompleteChain();
                return;
            }

            // 广播第一个未完成目标的进度，供 HUD 显示
            if (firstIncompleteIndex >= 0)
            {
                _index = firstIncompleteIndex;
                BroadcastCurrent();
            }
        }

        private void CompleteChain()
        {
            if (ChainCompleted)
            {
                return;
            }

            ChainCompleted = true;
            _index = (_config.Objectives?.Length ?? 0);
            BroadcastCurrent();

            // 完成回调延后一帧再发（2026-10-31 修复「结算显示击杀：0」）：
            // 触发链完成的事件（EnemyDeathEvent / GarbagePurifiedEvent）与统计类订阅者
            // （LevelStats 记 KillCount）是同一次 EventCenter.Publish 的并列订阅者，
            // 而 Publish 是倒序遍历——若 LevelStats 排在本类之后被调用，
            // 本类同步回调 LevelManager.FinishLevel 时取到的 KillCount 快照还是旧值，
            // 结算面板就会显示「击杀：0」而运行时实际已计数。
            // 延后一帧后，同一事件的所有订阅者都已跑完，统计值必然是最新的。
            StartCoroutine(NotifyChainCompletedNextFrame(_chainVersion));
        }

        /// <summary>延后一帧发完成回调；version 与当前代次不符说明期间关卡重开，丢弃。</summary>
        private System.Collections.IEnumerator NotifyChainCompletedNextFrame(int version)
        {
            yield return null;   // 等同一次事件派发中的其他订阅者处理完

            if (version != _chainVersion)
            {
                yield break;     // 关卡已重开，丢弃这次回调
            }

            _onChainCompleted?.Invoke();
        }

        private int CurrentCountFor(LevelObjectiveType type)
        {
            switch (type)
            {
                case LevelObjectiveType.PurifyTargets:
                    return _purifiedTargets;
                case LevelObjectiveType.PurifyTowers:
                    return _purifiedTowers;
                case LevelObjectiveType.DefeatEnemies:
                    return _defeatedEnemies;
                case LevelObjectiveType.ClearRightBank:
                    return _rightBankPurified + _rightBankDefeated;
                case LevelObjectiveType.DefeatBoss:
                    return _defeatedBoss;   // T-28
                case LevelObjectiveType.RescueTargets:
                    return _rescuedAnimals;
                case LevelObjectiveType.PuzzleSolved:
                    return _waterCleared;
                case LevelObjectiveType.CollectKnowledgeCards:
                    return _collectedKnowledgeCards;
                case LevelObjectiveType.ReachPurity:
                    return _purity != null ? Mathf.FloorToInt(_purity.Percent * 100f) : 0;
                default:
                    return 0;
            }
        }

        private void BroadcastCurrent()
        {
            var objective = CurrentObjective;
            EventCenter.Publish(new LevelObjectiveChangedEvent
            {
                Index = objective != null ? _index : -1,
                Label = objective?.Label ?? "关卡目标已完成",
                Current = objective != null ? CurrentCountFor(objective.Type) : 0,
                Required = CurrentRequired
            });
        }

        /// <summary>夹取不合理的 Required：净化类目标不得超过场景实际可净化目标数（R3 防死锁）。</summary>
        private void ClampRequired()
        {
            if (_config.Objectives == null || _config.Objectives.Length == 0)
            {
                _effectiveRequired = Array.Empty<int>();
                Debug.LogWarning("[LevelObjectiveTracker] 关卡配置没有目标，关卡将立即达成。");
                return;
            }

            _effectiveRequired = new int[_config.Objectives.Length];

            int available = 0;
            int towers = 0;
            int waterBlocks = 0;   // M9 R3：场景实际堵塞点数（PuzzleSolved 夹取用）
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                // 塔改为"攻击击毁"：按场景现存的 Tower01 数量统计可击毁塔数
                if (behaviours[i] is Tower01Controller tower && !tower.IsDisabled)
                {
                    towers++;
                }

                if (behaviours[i] is IPurifiable target && !target.IsPurified)
                {
                    if (behaviours[i] is WaterBlockage)
                    {
                        waterBlocks++;   // 堵塞点单独统计，不算普通污染源
                        continue;
                    }

                    available++;
                }
            }

            for (int i = 0; i < _config.Objectives.Length; i++)
            {
                var obj = _config.Objectives[i];
                if (obj == null)
                {
                    continue;
                }

                if (obj.Type == LevelObjectiveType.PurifyTargets && obj.Required > available)
                {
                    Debug.LogWarning($"[LevelObjectiveTracker] 目标 {i} 要求净化 {obj.Required} 个污染源，场景仅有 {available} 个，已按实际夹取。");
                    _effectiveRequired[i] = available;
                }
                else if (obj.Type == LevelObjectiveType.PurifyTowers && obj.Required > towers)
                {
                    Debug.LogWarning($"[LevelObjectiveTracker] 目标 {i} 要求净化 {obj.Required} 座塔，场景仅有 {towers} 座，已按实际夹取。");
                    _effectiveRequired[i] = towers;
                }
                else if (obj.Type == LevelObjectiveType.PuzzleSolved && obj.Required > waterBlocks)
                {
                    Debug.LogWarning($"[LevelObjectiveTracker] 目标 {i} 要求清理 {obj.Required} 处堵塞，场景仅有 {waterBlocks} 处，已按实际夹取。");
                    _effectiveRequired[i] = waterBlocks;
                }
                else if (obj.Type == LevelObjectiveType.CollectKnowledgeCards)
                {
                    var knowledgeCards = FindObjectsByType<KnowledgeCardPickup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
                    if (obj.Required > knowledgeCards)
                    {
                        Debug.LogWarning($"[LevelObjectiveTracker] 目标 {i} 要求收集 {obj.Required} 个知识卡片，场景仅有 {knowledgeCards} 个，已按实际夹取。");
                        _effectiveRequired[i] = knowledgeCards;
                    }
                    else
                    {
                        _effectiveRequired[i] = obj.Required;
                    }
                }
                else
                {
                    _effectiveRequired[i] = obj.Required;
                }
            }
        }

        private bool IsLevel02RightBank(float x)
        {
            return _config != null && _config.LevelIndex == 2 && x >= RightBankMinX;
        }

        private static bool IsTower(string targetId)
        {
            return !string.IsNullOrEmpty(targetId) && targetId.StartsWith(TowerIdPrefix, StringComparison.Ordinal);
        }
    }
}
