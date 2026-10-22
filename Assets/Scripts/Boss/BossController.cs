using UnityEngine;
using StarEco.Combat;
using StarEco.EnemyAI;
using StarEco.Framework;
using StarEco.EnemyAI.Events;

namespace StarEco.Boss
{
    /// <summary>
    /// Boss01 控制器（M8 R2/R4）：继承 M4 `EnemyControllerBase`，复用 HP/受击硬直/死亡广播/感知/击退，
    /// 只新增"阶段判定 + 三套技能状态 + 净化倍率"三块差异逻辑。
    /// 配置槽位接受 `BossConfig`（配置继承 EnemyConfig，基类字段可直接读）。
    /// </summary>
    public class BossController : EnemyControllerBase
    {
        /// <summary>当前阶段（只升不降）。</summary>
        public BossPhase CurrentPhase { get; private set; } = BossPhase.Phase1;

        /// <summary>是否处于阶段切换过场（期间无敌、不响应伤害）。</summary>
        public bool IsPhaseTransition => _transitionTimer > 0f;

        /// <summary>当前使用的 Boss 配置（缺失时回落默认）。</summary>
        public BossConfig BossCfg { get; private set; }

        /// <summary>弹体预制体（技能 B / 特殊招共用，复用 M4 EnemyProjectile）。</summary>
        public StarEco.EnemyAI.EnemyProjectile ProjectilePrefab => _projectilePrefab;

        /// <summary>弹体出生点相对 Boss 的偏移距离（沿发射方向）。</summary>
        public float FirePointOffset => _firePointOffset;

        [Tooltip("弹体预制体（技能 B / 特殊招）")]
        [SerializeField] private StarEco.EnemyAI.EnemyProjectile _projectilePrefab;

        [Tooltip("弹体出生点相对 Boss 中心的前向偏移")]
        [SerializeField] private float _firePointOffset = 1.2f;

        /// <summary>当前 HP 百分比 [0,100]。</summary>
        public float HpPercent => Config.MaxHP > 0 ? (float)CurrentHP / Config.MaxHP * 100f : 0f;

        /// <summary>攻击间隔阶段系数（P2 更快、P3 最快）。</summary>
        public float IntervalScale
        {
            get
            {
                if (BossCfg == null)
                {
                    return 1f;
                }

                switch (CurrentPhase)
                {
                    case BossPhase.Phase2: return BossCfg.Phase2IntervalScale;
                    case BossPhase.Phase3: return BossCfg.Phase3IntervalScale;
                    default: return 1f;
                }
            }
        }

        private float _transitionTimer;
        private bool _triggeredPhase2;
        private bool _triggeredPhase3;
        private float _attackBCooldown;
        private float _specialCooldown;
        private int _levelIndex = 3;

        protected override void Awake()
        {
            base.Awake();

            BossCfg = _config as BossConfig;
            if (BossCfg == null)
            {
                Debug.LogWarning("[BossController] 配置不是 BossConfig，已回落到默认 Boss 配置（HP 80 半心点 = 40 心）。");
                BossCfg = BossConfig.CreateDefault();
                _config = BossCfg;
            }

            CurrentPhase = BossPhase.Phase1;
            _triggeredPhase2 = false;
            _triggeredPhase3 = false;
        }

        protected override void BuildStateMachine()
        {
            Machine = new EnemyStateMachine();
            Machine.Register(new BossIdleState(this, Machine));
            Machine.Register(new BossChaseState(this, Machine));
            Machine.Register(new BossAttackAState(this, Machine));
            Machine.Register(new BossAttackBState(this, Machine));
            Machine.Register(new BossSpecialState(this, Machine));
            Machine.Register(new BossEnrageState(this, Machine));
            Machine.Register(new EnemyHurtState(this, Machine));
            Machine.Register(new EnemyDieState(this, Machine));
        }

        protected override System.Type InitialStateType => typeof(BossIdleState);

        protected override void Update()
        {
            base.Update();

            if (IsDead)
            {
                return;
            }

            TickTransition();
            TickCooldowns();

            // 阶段判定放最后：本帧受击掉血后立刻反映到阶段
            EvaluatePhase();
        }

        // ===== 阶段 =====

        private void EvaluatePhase()
        {
            if (BossCfg == null || IsPhaseTransition)
            {
                return;
            }

            float percent = HpPercent;

            if (!_triggeredPhase3 && percent <= BossCfg.Phase3HpPercent)
            {
                _triggeredPhase3 = true;
                EnterPhase(BossPhase.Phase3, percent);
                return;
            }

            if (!_triggeredPhase2 && percent <= BossCfg.Phase2HpPercent)
            {
                _triggeredPhase2 = true;
                EnterPhase(BossPhase.Phase2, percent);
            }
        }

        private void EnterPhase(BossPhase next, float percent)
        {
            CurrentPhase = next;
            _transitionTimer = BossCfg != null ? BossCfg.PhaseTransitionSeconds : 0.8f;

            // 过场期间不进入受击态，避免打断狂暴动画
            _pendingHurt = false;
            _hurtStunTimer = 0f;

            Machine.SwitchState<BossEnrageState>();
            TryFireAnim(BossAnimParams.Enrage);

            EventCenter.Publish(new Events.BossPhaseChangedEvent
            {
                Phase = (int)next,
                PhaseName = next.DisplayName(),
                HpPercent = percent
            });

            Debug.Log($"[BossController] 进入 {next.DisplayName()}（HP {percent:F0}%，过场无敌 {_transitionTimer:F2}s）");
        }

        private void TickTransition()
        {
            if (_transitionTimer <= 0f)
            {
                return;
            }

            _transitionTimer -= Time.deltaTime;
            if (_transitionTimer > 0f)
            {
                return;
            }

            _transitionTimer = 0f;
            Debug.Log($"[BossController] {CurrentPhase.DisplayName()} 过场结束，恢复战斗。");
            Machine.SwitchState<BossIdleState>();
        }

        // ===== 冷却（B / Special 独立计时，A 复用基类 AttackCooldownTimer） =====

        private void TickCooldowns()
        {
            if (_attackBCooldown > 0f)
            {
                _attackBCooldown -= Time.deltaTime;
            }

            if (_specialCooldown > 0f)
            {
                _specialCooldown -= Time.deltaTime;
            }
        }

        /// <summary>技能 B 是否冷却就绪。</summary>
        public bool IsAttackBReady => _attackBCooldown <= 0f;

        /// <summary>设置技能 B 冷却。</summary>
        public void ResetAttackBCooldown()
        {
            _attackBCooldown = BossCfg != null
                ? BossCfg.AttackBInterval * IntervalScale
                : 3f;
        }

        /// <summary>特殊招是否冷却就绪（仅 P3）。</summary>
        public bool IsSpecialReady => CurrentPhase == BossPhase.Phase3 && _specialCooldown <= 0f;

        /// <summary>设置特殊招冷却。</summary>
        public void ResetSpecialCooldown()
        {
            _specialCooldown = BossCfg != null
                ? BossCfg.SpecialInterval * IntervalScale
                : 6f;
        }

        /// <summary>设置技能 A 冷却（走基类 AttackCooldownTimer）。</summary>
        public void ResetAttackACooldown()
        {
            AttackCooldownTimer = BossCfg != null
                ? BossCfg.AttackAInterval * IntervalScale
                : 2f;
        }

        /// <summary>近战范围判定（用 BossConfig.AttackARange）。</summary>
        public bool InMeleeRange =>
            HasTarget && BossCfg != null
            && Mathf.Abs(Target.position.x - transform.position.x) <= BossCfg.AttackARange;

        // ===== 伤害（R4：净化 ×1.5，过场无敌） =====

        /// <summary>M10：Boss 受击音效（基类默认 EnemyHit）。</summary>
        protected override SfxId HitSfxId => StarEco.Framework.SfxId.BossHit;

        public override void TakeDamage(DamageInfo info)
        {
            if (IsDead)
            {
                return;
            }

            // 阶段过场：忽略一切伤害（不扣血、不进受击态、不播 Hurt）
            if (IsPhaseTransition)
            {
                Debug.Log("[BossController] 阶段过场无敌，伤害被忽略。");
                return;
            }

            if (info.IsPurify && BossCfg != null && BossCfg.PurifyDamageMultiplier > 0f)
            {
                int boosted = Mathf.Max(1, Mathf.FloorToInt(info.Amount * BossCfg.PurifyDamageMultiplier));
                Debug.Log($"[BossController] 净化伤害 {info.Amount} ×{BossCfg.PurifyDamageMultiplier} → {boosted}");
                info.Amount = boosted;
            }

            base.TakeDamage(info);
        }

        // ===== 死亡：额外广播 BossDefeatedEvent =====

        public override void Recycle()
        {
            bool alreadyDead = IsDead;

            // Boss 是场景实例（非池借出），走基类的 SetActive(false) 分支
            base.Recycle();

            if (!alreadyDead)
            {
                return;
            }

            var stats = FindObjectOfType<StarEco.Level.LevelStats>();
            float elapsed = stats != null ? stats.ElapsedSeconds : 0f;

            EventCenter.Publish(new Events.BossDefeatedEvent
            {
                BossId = Config != null ? Config.EnemyTypeId : "Boss01",
                LevelIndex = _levelIndex,
                ElapsedSeconds = elapsed
            });

            Debug.Log($"[BossController] Boss 已击败（耗时 {elapsed:F1}s），已广播 BossDefeatedEvent。");
        }

        /// <summary>设置所属关卡序号（BossDefeatedEvent 携带；默认第三关）。</summary>
        public void SetLevelIndex(int index)
        {
            _levelIndex = index;
        }

        /// <summary>受击硬直后回落状态（Boss 优先追击）。</summary>
        public override System.Type DecidePostHurtState()
        {
            return HasTarget ? typeof(BossChaseState) : typeof(BossIdleState);
        }
    }
}
