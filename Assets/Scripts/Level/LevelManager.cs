using UnityEngine;
using StarEco.Framework;
using StarEco.Player;
using StarEco.Combat;
using StarEco.Eco;
using StarEco.Eco.Events;
using StarEco.Level.Events;

namespace StarEco.Level
{
    /// <summary>
    /// 关卡管理器（M6 流程中枢，R2）：单例、状态机、检查点快照与恢复、终态结算与写档。
    /// 对 M1~M5 零改动，只调用公开接口与订阅事件。
    /// 依赖（PuritySystem / LevelStats / LevelObjectiveTracker / LevelSpawner）用惰性查找，
    /// 因为场景各组件 Awake 顺序不确定。
    /// </summary>
    public class LevelManager : MonoSingleton<LevelManager>
    {
        [SerializeField] private LevelConfig _config;

        /// <summary>当前关卡状态。</summary>
        public LevelState State { get; private set; } = LevelState.NotStarted;

        /// <summary>当前使用的关卡配置。</summary>
        public LevelConfig Config => _config;

        /// <summary>最近一次提交的检查点快照（单槽位，R4）。</summary>
        public LevelSnapshot CurrentSnapshot { get; private set; }

        /// <summary>无检查点恢复的次数（诊断用）。</summary>
        public int NoCheckpointRespawnCount { get; private set; }

        private PlayerController _player;
        private PuritySystem _purity;
        private PurifyTank _tank;
        private LevelStats _stats;
        private LevelObjectiveTracker _tracker;
        private LevelSpawner _spawner;

        private bool _finished;   // 终态幂等守卫

        protected override void OnSingletonAwake()
        {
            State = LevelState.NotStarted;
            EventCenter.Subscribe<LevelPurifiedEvent>(OnLevelPurified);
            EventCenter.Subscribe<RestartLevelEvent>(OnRestartRequested);
        }

        protected override void OnDestroy()
        {
            EventCenter.Unsubscribe<LevelPurifiedEvent>(OnLevelPurified);
            EventCenter.Unsubscribe<RestartLevelEvent>(OnRestartRequested);

            if (_spawner != null)
            {
                _spawner.Stop();
            }
        }

        private void Start()
        {
            ResolveDependencies();

            if (_config == null)
            {
                _config = Resources.Load<LevelConfig>("Level01Config");
            }

            if (!HasRequiredDependencies())
            {
                Debug.LogError("[LevelManager] 关卡依赖缺失（LevelConfig / PlayerController / PuritySystem），关卡无法开始。");
                SetState(LevelState.Failed);
                return;
            }

            StartLevel();
        }

        private void ResolveDependencies()
        {
            if (_player == null)
            {
                _player = FindObjectOfType<PlayerController>();
            }

            if (_purity == null)
            {
                _purity = FindObjectOfType<PuritySystem>();
            }

            if (_player != null)
            {
                if (_tank == null)
                {
                    _tank = _player.GetComponent<PurifyTank>();
                }
            }

            if (_stats == null)
            {
                _stats = FindObjectOfType<LevelStats>();
            }

            if (_tracker == null)
            {
                _tracker = FindObjectOfType<LevelObjectiveTracker>();
            }

            if (_spawner == null)
            {
                _spawner = FindObjectOfType<LevelSpawner>();
            }
        }

        private bool HasRequiredDependencies()
        {
            return _config != null && _player != null && _purity != null && _stats != null && _tracker != null;
        }

        // ===== 流程 =====

        /// <summary>开始关卡：进入 Playing 并启动目标链与刷怪（R2）。</summary>
        public void StartLevel()
        {
            ResolveDependencies();

            if (!HasRequiredDependencies())
            {
                Debug.LogError("[LevelManager] 关卡依赖缺失，无法开始。");
                SetState(LevelState.Failed);
                return;
            }

            if (_config == null)
            {
                _config = LevelConfig.CreateDefault();
            }

            _finished = false;
            NoCheckpointRespawnCount = 0;
            CurrentSnapshot = null;

            _stats.Reset();
            _tracker.Initialize(_config, _purity, _stats, OnObjectiveChainCompleted);

            if (_player != null)
            {
                _player.InputLocked = false;
                _player.transform.position = _config.SpawnPoint;
            }

            SetState(LevelState.Playing);

            // M10：进入关卡播放对应 BGM（素材缺失静默跳过）
            AudioManager.Instance?.PlayBgmForLevel(_config.LevelIndex);

            if (_spawner != null)
            {
                _spawner.Begin(_config, _tracker, _player != null ? _player.transform : null);
            }
        }

        /// <summary>
        /// 重开关卡（R7 面板"重新开始"）：重载当前场景。
        /// 为什么重载而不是原地重置：塔（Tower01Controller.RequestDisable）是 M4 的单向开关，
        /// 关掉后无法复活 —— 原地重置会让重开后的关卡少一个 10% 净化值目标（25/35 封顶），
        /// 无法达成 100% 通关。重载场景让所有对象回到初始态，是唯一彻底的方案。
        /// </summary>
        public void RestartLevel()
        {
            var scene = gameObject.scene;

            // LevelManager 挂在非根节点上（LevelSetup 保证），故不会被 MonoSingleton 标记为
            // DontDestroyOnLoad，随场景一起销毁；这里显式禁用以确保本帧不再 Update。
            enabled = false;
            gameObject.SetActive(false);

            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
        }

        private void Update()
        {
            if (State != LevelState.Playing)
            {
                return;
            }

            if (_player != null && _player.IsDead)
            {
                // 死亡处理：有检查点则从检查点恢复（横版动作游戏惯例，不打断关卡流程）；
                // 无任何检查点才判失败（R2 与 R4 的交界，requirements 已同步）
                if (CurrentSnapshot != null)
                {
                    RespawnFromCheckpoint();
                }
                else
                {
                    FinishLevel(false, "玩家死亡且无检查点可恢复");
                }
            }
        }

        private void SetState(LevelState next)
        {
            if (State == next)
            {
                return;
            }

            var old = State;
            State = next;
            EventCenter.Publish(new LevelStateChangedEvent { OldState = old, NewState = next });
        }

        /// <summary>进入终态：停表 → 冻结输入 → 生成结算数据 → 写档 → 广播（R2/R6）。</summary>
        private void FinishLevel(bool success, string reason)
        {
            if (_finished)
            {
                return;   // 终态幂等（R2）
            }

            _finished = true;

            _spawner?.Stop();

            if (_player != null)
            {
                _player.InputLocked = true;
            }

            var result = BuildResult(success);
            LevelSaveService.CommitResult(result);

            if (success)
            {
                AudioManager.Instance?.PlaySfx(SfxId.LevelComplete);   // M10
                SetState(LevelState.Completed);
                EventCenter.Publish(new LevelCompletedEvent { Result = result });
            }
            else
            {
                AudioManager.Instance?.PlaySfx(SfxId.LevelFail);   // M10
                SetState(LevelState.Failed);
                EventCenter.Publish(new LevelFailedEvent { Reason = reason });
            }
        }

        private LevelResultData BuildResult(bool success)
        {
            float percent = _purity != null ? _purity.Percent * 100f : 0f;

            return new LevelResultData
            {
                LevelIndex = _config != null ? _config.LevelIndex : 1,
                Cleared = success,
                PurityPercent = percent,
                ElapsedSeconds = _stats != null ? _stats.ElapsedSeconds : 0f,
                KillCount = _stats != null ? _stats.KillCount : 0,
                PickupCount = _stats != null ? _stats.PickupCount : 0,
                Stars = success && _config != null ? _config.EvaluateStars(percent) : 0,
                TimestampUtcTicks = System.DateTime.UtcNow.Ticks
            };
        }

        // ===== 事件 =====

        private void OnLevelPurified(LevelPurifiedEvent evt)
        {
            if (State == LevelState.Playing)
            {
                FinishLevel(true, "净化值达到 100%");
            }
        }

        private void OnObjectiveChainCompleted()
        {
            // 关卡完成判据：目标链全完成或净化值 100%，先到者为准（R3）
            if (State == LevelState.Playing)
            {
                FinishLevel(true, "全部关卡目标完成");
            }
        }

        private void OnRestartRequested(RestartLevelEvent evt)
        {
            RestartLevel();
        }

        // ===== 检查点 =====

        /// <summary>提交检查点快照（R4，单槽位覆盖）。</summary>
        public void CommitCheckpoint(Checkpoint checkpoint)
        {
            if (checkpoint == null)
            {
                return;
            }

            CurrentSnapshot = new LevelSnapshot
            {
                PlayerPosition = checkpoint.RespawnPosition,
                PlayerHP = _player != null && _player.Combat != null ? _player.Combat.CurrentHP : 0,
                PurifyTank = _tank != null ? _tank.Current : 0,
                KillCount = _stats != null ? _stats.KillCount : 0,
                PickupCount = _stats != null ? _stats.PickupCount : 0,
                ElapsedSeconds = _stats != null ? _stats.ElapsedSeconds : 0f,
                CheckpointId = checkpoint.CheckpointId
            };

            _stats?.CommitSnapshotElapsed();

            Debug.Log($"[LevelManager] 检查点已记录：{checkpoint.CheckpointId}");
        }

        /// <summary>从最近检查点恢复（R4）。无快照时回退配置出生点。</summary>
        public void RespawnFromCheckpoint()
        {
            if (_player == null)
            {
                return;
            }

            _player.Revive();

            if (CurrentSnapshot == null)
            {
                NoCheckpointRespawnCount++;
                _player.transform.position = _config != null ? (Vector3)_config.SpawnPoint : Vector3.zero;

                if (_player.Combat != null)
                {
                    _player.Combat.RestoreHP(_player.Combat.MaxHP);
                }

                Debug.LogWarning("[LevelManager] 没有任何检查点记录，已回退到关卡出生点。");
                return;
            }

            _player.transform.position = CurrentSnapshot.PlayerPosition;

            if (_player.Combat != null)
            {
                _player.Combat.RestoreHP(CurrentSnapshot.PlayerHP);
            }

            if (_tank != null)
            {
                // M5 只提供 Consume/Restore，用"扣到底再补到目标值"实现精确恢复（零改动 M5）
                _tank.Consume(_tank.Current);
                _tank.Restore(CurrentSnapshot.PurifyTank);
            }

            _stats?.RestoreFromSnapshot(CurrentSnapshot.KillCount, CurrentSnapshot.PickupCount, CurrentSnapshot.ElapsedSeconds);

            _player.InputLocked = false;
            Debug.Log($"[LevelManager] 已从检查点 {CurrentSnapshot.CheckpointId} 恢复。");
        }
    }
}
