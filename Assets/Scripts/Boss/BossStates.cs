using UnityEngine;
using StarEco.EnemyAI;
using StarEco.Framework;

namespace StarEco.Boss
{
    /// <summary>
    /// Boss 状态公共基类（M8 R2）：在 M4 `EnemyStateBase` 之上补 Boss 强类型访问器。
    /// </summary>
    public abstract class BossStateBase : EnemyStateBase
    {
        protected readonly BossController Boss;
        protected new BossConfig Config => Boss != null ? Boss.BossCfg : null;

        protected BossStateBase(BossController boss, EnemyStateMachine machine)
            : base(boss, machine)
        {
            Boss = boss;
        }

        /// <summary>阶段过场优先级最高（高于死亡判定之后的受击检查）。</summary>
        protected bool InTransition => Boss != null && Boss.IsPhaseTransition;
    }

    /// <summary>待机：有目标则按距离与冷却决定追击 / 技能 A / 技能 B / 特殊招。</summary>
    public class BossIdleState : BossStateBase
    {
        public BossIdleState(BossController boss, EnemyStateMachine machine) : base(boss, machine) { }

        public override void Enter()
        {
            Boss.Anim?.SetBool(BossAnimParams.IsMove, false);
        }

        public override void Tick()
        {
            if (TryHandleCombatTransitions()) return;
            if (InTransition) return;

            if (!Boss.HasTarget)
            {
                return;
            }

            // 对话期间不发动技能（移动/追击照常）
            if (!DialogueActive && TryBeginSkill()) return;

            if (!Boss.InMeleeRange)
            {
                Machine.SwitchState<BossChaseState>();
            }
        }

        /// <summary>技能优先级：Special（P3）→ A（近身）→ B（远程）。返回是否已起手。</summary>
        protected bool TryBeginSkill()
        {
            if (Boss.IsSpecialReady)
            {
                Machine.SwitchState<BossSpecialState>();
                return true;
            }

            if (Boss.InMeleeRange && Boss.AttackCooldownTimer <= 0f)
            {
                Machine.SwitchState<BossAttackAState>();
                return true;
            }

            // 技能 B：P1 起可用，仅在超出近战范围时用来拉扯
            if (Boss.IsAttackBReady && !Boss.InMeleeRange)
            {
                Machine.SwitchState<BossAttackBState>();
                return true;
            }

            return false;
        }
    }

    /// <summary>追击：横向移动靠近目标（移速受狂暴倍率影响）。</summary>
    public class BossChaseState : BossStateBase
    {
        public BossChaseState(BossController boss, EnemyStateMachine machine) : base(boss, machine) { }

        public override void Enter()
        {
            Boss.Anim?.SetBool(BossAnimParams.IsMove, true);
        }

        public override void Exit()
        {
            Boss.Anim?.SetBool(BossAnimParams.IsMove, false);
        }

        public override void Tick()
        {
            if (TryHandleCombatTransitions()) return;
            if (InTransition) return;

            if (!Boss.HasTarget)
            {
                Machine.SwitchState<BossIdleState>();
                return;
            }

            // 对话期间不发动技能：保持追击（移动在 FixedTick）
            if (DialogueActive) return;

            if (Boss.IsSpecialReady)
            {
                Machine.SwitchState<BossSpecialState>();
                return;
            }

            if (Boss.InMeleeRange && Boss.AttackCooldownTimer <= 0f)
            {
                Machine.SwitchState<BossAttackAState>();
                return;
            }

            if (Boss.IsAttackBReady)
            {
                Machine.SwitchState<BossAttackBState>();
            }
        }

        public override void FixedTick()
        {
            if (InTransition)
            {
                Boss.StopMoving();
                return;
            }

            MoveTowards();
        }

        /// <summary>按 Boss 阶段移速移动（狂暴倍率手写，不依赖基类 EnemyConfig.MoveSpeed 缩放）。</summary>
        private void MoveTowards()
        {
            var body = Boss.Body;
            if (body == null || !Boss.HasTarget || Config == null || Config.IsStatic)
            {
                return;
            }

            Boss.FaceTarget();
            float dirX = Mathf.Sign(Boss.Target.position.x - Boss.transform.position.x);
            if (Mathf.Abs(Boss.Target.position.x - Boss.transform.position.x) < 0.05f)
            {
                return;
            }

            float speed = Config.MoveSpeed
                * (Boss.CurrentPhase == BossPhase.Phase1 ? 1f : Config.EnrageSpeedMultiplier);

            Vector2 next = body.position + new Vector2(dirX, 0f) * speed * Time.fixedDeltaTime;
            body.MovePosition(next);
        }
    }

    /// <summary>技能 A · 近战横扫：事件帧开窗判定（沿用 M4"事件帧为权威"）。</summary>
    public class BossAttackAState : BossStateBase
    {
        public BossAttackAState(BossController boss, EnemyStateMachine machine) : base(boss, machine) { }

        public override void Enter()
        {
            Boss.ResetAttackACooldown();
            Boss.StopMoving();
            Boss.FaceTarget();
            Boss.TryFireAnim(BossAnimParams.AttackA);
        }

        public override void Tick()
        {
            if (TryHandleCombatTransitions()) return;

            // 攻击动画时长用冷却近似（与 M4 做法一致）：冷却走完即回落
            if (Boss.AttackCooldownTimer <= 0f)
            {
                Machine.SwitchState(Boss.HasTarget ? typeof(BossChaseState) : typeof(BossIdleState));
            }
        }
    }

    /// <summary>技能 B · 远程弹幕：扇形发射，复用 M4 EnemyProjectile + 对象池。</summary>
    public class BossAttackBState : BossStateBase
    {
        public BossAttackBState(BossController boss, EnemyStateMachine machine) : base(boss, machine) { }

        public override void Enter()
        {
            Boss.ResetAttackBCooldown();
            Boss.StopMoving();
            Boss.FaceTarget();
            Boss.TryFireAnim(BossAnimParams.AttackB);
            DoFire();
        }

        public override void Tick()
        {
            if (TryHandleCombatTransitions()) return;

            if (Boss.AttackCooldownTimer <= 0f || Boss.IsAttackBReady)
            {
                Machine.SwitchState(Boss.HasTarget ? typeof(BossChaseState) : typeof(BossIdleState));
            }
        }

        /// <summary>实际弹幕内容（Special 覆写它，起手与冷却由各自 Enter 决定）。</summary>
        protected virtual void DoFire()
        {
            if (Config == null || Boss.ProjectilePrefab == null)
            {
                Debug.LogWarning("[BossAttackB] 未配置弹体预制体，技能 B 空放。");
                return;
            }

            int count = Mathf.Max(1, Config.ProjectileCount + (Boss.CurrentPhase == BossPhase.Phase1 ? 0 : 1));
            float spread = Config.ProjectileSpreadDegrees;
            Vector2 baseDir = Boss.FacingDirection;

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) - 0.5f : 0f;
                SpawnProjectile(Rotate(baseDir, t * spread));
            }
        }

        protected void SpawnProjectile(Vector2 dir)
        {
            var prefab = Boss.ProjectilePrefab;
            var projectile = PoolManager.Instance.Get(prefab, Config.ProjectilePrewarm);
            projectile.transform.position = (Vector2)Boss.transform.position + dir * Boss.FirePointOffset;

            projectile.Launch(new EnemyProjectile.ProjectileParams
            {
                Prefab = prefab,
                Direction = dir.normalized,
                Speed = Config.ProjectileSpeed,
                MaxRange = Config.ProjectileMaxRange,
                Damage = Config.AttackDamage,
                Knockback = Config.AttackKnockback,
                Source = Boss.gameObject,
                OwnerLayer = Boss.gameObject.layer
            });
        }

        protected static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }

    /// <summary>特殊招 · 环绕扇形弹幕（仅 P3）：弹数更多、张角更大、施法前先面朝一次。</summary>
    public class BossSpecialState : BossAttackBState
    {
        public BossSpecialState(BossController boss, EnemyStateMachine machine) : base(boss, machine) { }

        public override void Enter()
        {
            Boss.ResetSpecialCooldown();
            Boss.StopMoving();
            Boss.FaceTarget();
            Boss.TryFireAnim(BossAnimParams.Special);
            DoFire();
        }

        protected override void DoFire()
        {
            if (Config == null || Boss.ProjectilePrefab == null)
            {
                return;
            }

            int count = Mathf.Max(1, Config.SpecialProjectileCount);
            float spread = Config.SpecialSpreadDegrees;
            Vector2 baseDir = Boss.FacingDirection;

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) - 0.5f : 0f;
                SpawnProjectile(Rotate(baseDir, t * spread));
            }

            Debug.Log($"[BossSpecial] 释放特殊招：{count} 发 / 张角 {spread}°");
        }
    }

    /// <summary>阶段狂暴过场：站定播 Enrage，等 BossController 的过场计时结束。</summary>
    public class BossEnrageState : BossStateBase
    {
        public BossEnrageState(BossController boss, EnemyStateMachine machine) : base(boss, machine) { }

        public override void Enter()
        {
            Boss.StopMoving();
            Boss.Anim?.SetBool(BossAnimParams.IsMove, false);
        }

        public override void Tick()
        {
            // 过场结束由 BossController.TickTransition 统一切回 BossIdleState
            if (TryHandleDieTransition()) return;
        }
    }
}
