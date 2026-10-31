using UnityEngine;
using StarEco.Framework;
using StarEco.Player;
using StarEco.Combat;
using StarEco.Eco;
using StarEco.Eco.Events;
using StarEco.Level.Events;
using StarEco.UI;

namespace StarEco.Level
{
    /// <summary>
    /// 关卡管理器（M6 流程中枢，R2）：单例、状态机、终态结算与写档。
    /// 对 M1~M5 零改动，只调用公开接口与订阅事件。
    /// 依赖（PuritySystem / LevelStats / LevelObjectiveTracker / LevelSpawner）用惰性查找，
    /// 因为场景各组件 Awake 顺序不确定。
    /// </summary>
    public class LevelManager : MonoSingleton<LevelManager>
    {
        [SerializeField] private LevelConfig _config;

        [Tooltip("通关落幕对白 Id（仅最后一关播放；置空 = 关闭落幕对白）")]
        [SerializeField] private string _endingDialogueId = "ava_ending";

        private bool _endingPlayed;   // 落幕对白只播一次，防止重复进终态时二次弹出

        /// <summary>当前关卡状态。</summary>
        public LevelState State { get; private set; } = LevelState.NotStarted;

        /// <summary>当前使用的关卡配置。</summary>
        public LevelConfig Config => _config;

        private PlayerController _player;
        private PuritySystem _purity;
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

            _stats.Reset();
            _tracker.Initialize(_config, _purity, _stats, OnObjectiveChainCompleted);

            if (_player != null)
            {
                _player.InputLocked = false;
                // 出生点以场景里 Player 的摆放位置为准（2026-10-30 从"强制瞬移到
                // LevelConfig.SpawnPoint"改过来：编辑器里摆的位置才是唯一可见事实，
                // 运行时再按配置瞬移会让玩家被传送到一个场景视图看不到的地方）。
                // 想改出生点：直接在场景里拖动 Player，然后保存场景。
            }

            SetState(LevelState.Playing);

            // M10：进入关卡播放对应 BGM（素材缺失静默跳过）
            AudioManager.Instance?.PlayBgmForLevel(_config.LevelIndex);

            if (_spawner != null)
            {
                _spawner.Begin(_config, _tracker);
            }
        }

        /// <summary>
        /// 重开关卡（结算面板"重新开始"）：重载当前场景。
        /// 为什么重载而不是原地重置：已净化对象、熄火/永久关闭的敌人等运行时状态都是单向的，
        /// 原地重置无法真正回到初始态；重载场景让所有对象重新初始化，是唯一彻底的方案。
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
                // 死亡即结束本局：直接进入失败终态并弹出结算面板（重新开始 / 返回主菜单）。
                // 重开本关由结算面板的"重新开始"重载场景完成，不做原地续命。
                // 死因区分：坠落（掉出关卡地面）不能说成"被击败"，否则玩家会以为是被怪打死的。
                FinishLevel(false, _player.DiedFromFall ? "跌落山崖（掉出了关卡地面）" : "玩家被击败");
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

            // 收掉玩家可能正在看的对白。必须排在 _player.InputLocked = true 之前：
            // UIManager.ApplyInputLock 会按当前栈顶重算并写回 InputLocked / ExternalInputLock，
            // 在锁定之后再关面板会把锁清成 false，直到结算面板入栈才补回来。
            CloseResidualDialogue();

            if (_player != null)
            {
                _player.InputLocked = true;
            }

            // 通关落幕对白拦在结算之前：直接弹结算会冻结输入，玩家没有机会看到任何收尾文本。
            if (success && TryPlayEndingDialogue())
            {
                StartCoroutine(CommitFinishAfterEnding(reason));
                return;
            }

            CommitFinish(success, reason);
        }

        /// <summary>
        /// 关闭仍在播放的对话面板（T-19）。幂等：没有残留对白时什么都不做。
        ///
        /// 为什么必须关：玩家可能在读艾娃对白/提示牌的半途完成关卡（或在对话中被 Boss 打死）。
        /// 此时 DialoguePanel 仍在 UIManager 栈里，DialogueCanvas(sortingOrder 70) 位于
        /// ResultCanvas(100) 之下，而结算遮罩 UIUtility.Overlay 只有 72% 不透明
        /// → 对白框从遮罩下透出来，结算按钮白字直接压在艾娃台词上，文字糊成一团。
        /// 这违反 UXDesign §2.3「结算面板 ← 最高（一出现就压住一切）」。
        ///
        /// 顺带修掉一个同源问题：同一个 DialoguePanel 不能同时播两段，
        /// 所以原先若正好有对白在播，L3 的落幕对白 ava_ending 会被整段跳过
        /// （违反 GDD §40「最后一关先播落幕对白再弹结算」）。
        /// 先关残留对白、再播落幕对白，收尾顺序就回到策划规定的轨道。
        /// </summary>
        private void CloseResidualDialogue()
        {
            if (!DialoguePanel.IsDialogueActive)
            {
                return;
            }

            UIManager.SafeClose(DialoguePanel.PanelId);

            // 兜底：面板未注册进 UIManager 时 SafeClose 找不到它，OnClose 不会触发，
            // 静态开关 IsDialogueActive 会残留 true（敌人被永久冻结，见 T-20）。
            if (DialoguePanel.IsDialogueActive)
            {
                var panel = FindObjectOfType<DialoguePanel>();
                if (panel != null)
                {
                    panel.Close();
                }
                else
                {
                    Debug.LogWarning("[LevelManager] 残留对白无法关闭（面板已不存在），继续结算。");
                }
            }
        }

        /// <summary>
        /// 播放通关落幕对白（仅最后一关）。返回 true 表示已交给协程异步收尾，
        /// 调用方不得再走同步结算路径。
        /// </summary>
        private bool TryPlayEndingDialogue()
        {
            if (_endingPlayed || string.IsNullOrEmpty(_endingDialogueId))
            {
                return false;
            }

            // 只给最后一关：这段台词讲的是三颗星球的数据互通，放在第一/二关语义不通
            if (_config == null || _config.LevelIndex != 3)
            {
                return false;
            }

            // 防御性检查：残留对白正常已在 FinishLevel 里被 CloseResidualDialogue 收掉，
            // 走到这里说明是别的路径直接调的。同一时刻只能播一段，不能抢。
            if (DialoguePanel.IsDialogueActive)
            {
                return false;
            }

            var data = DialogueRepository.Find(_endingDialogueId);
            if (data == null)
            {
                Debug.LogWarning($"[LevelManager] 落幕对白缺失：{_endingDialogueId}，直接进入结算。");
                return false;
            }

            UIManager.SafeOpen(DialoguePanel.PanelId);
            var panel = FindObjectOfType<DialoguePanel>();
            if (panel == null || !panel.IsOpen)
            {
                UIManager.SafeClose(DialoguePanel.PanelId);
                Debug.LogWarning("[LevelManager] 对话面板不可用，跳过落幕对白。");
                return false;
            }

            panel.Play(data);
            _endingPlayed = true;
            return true;
        }

        /// <summary>
        /// 等落幕对白播完再结算。不直接把结算挂在对话回调上，是因为面板栈没有
        /// "关闭回调"；轮询 IsDialogueActive 最省事且对 Esc 中断也成立。
        /// </summary>
        private System.Collections.IEnumerator CommitFinishAfterEnding(string reason)
        {
            yield return null;   // SafeOpen 当帧只入栈，等一帧让 OnOpen 生效

            while (DialoguePanel.IsDialogueActive)
            {
                yield return null;
            }

            // 最后一句是玩家按 E 关掉的；不等这一帧，结算面板会被同一个 E 立刻按掉
            yield return null;

            CommitFinish(true, reason);
        }

        /// <summary>终态后半段：生成结算数据 → 写档 → 播提示音 → 切状态 → 广播（结算面板据此弹出）。</summary>
        private void CommitFinish(bool success, string reason)
        {
            // 结算面板要独占屏幕（UXDesign §2.3）。这里再兜一次，
            // 保证无论从哪条路径进来，结算弹出那一瞬对白都不在栈里。
            CloseResidualDialogue();

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
    }
}
