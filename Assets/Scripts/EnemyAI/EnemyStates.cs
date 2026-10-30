using UnityEngine;

namespace StarEco.EnemyAI
{
    /// <summary>待机状态：感知目标，有目标则转移动/追击或攻击；无目标时沿地面巡逻。</summary>
    public class EnemyIdleState : EnemyStateBase
    {
        public EnemyIdleState(EnemyControllerBase controller, EnemyStateMachine machine)
            : base(controller, machine) { }

        public override void Tick()
        {
            if (TryHandleCombatTransitions()) return;

            if (!Controller.HasTarget || Controller.IsStunned)
            {
                // 没看见玩家时自己巡逻，不再原地死等（未注册巡逻态的敌人保持待机）
                if (!Controller.HasTarget && Controller.CanPatrol && !DialogueActive)
                {
                    Machine.SwitchStateIfRegistered<EnemyPatrolState>();
                }
                return;
            }

            if (Controller.InAttackRange && Controller.AttackCooldownTimer <= 0f && !DialogueActive)
            {
                if (Config.AttackKind == EnemyConfig.AttackKindEnum.Melee)
                    Machine.SwitchState<EnemyMeleeAttackState>();
                else
                    Machine.SwitchState<EnemyRangedAttackState>();
            }
            else if (!Config.IsStatic && !Controller.InAttackRange)
            {
                Machine.SwitchState<EnemyChaseState>();
            }
        }
    }

    /// <summary>
    /// 巡逻状态：以出生点为圆心左右往返走动，到端点停一会儿再折返。
    /// 一旦感知到玩家（Target 由 Controller 的感知层写入），立即转入追击/攻击。
    /// 前方无地面则自动折返，避免走下平台。
    /// </summary>
    public class EnemyPatrolState : EnemyStateBase
    {
        private int _dir = 1;
        private float _pauseTimer;
        private float _stallTimer;
        private float _lastX;

        public EnemyPatrolState(EnemyControllerBase controller, EnemyStateMachine machine)
            : base(controller, machine) { }

        public override void Enter()
        {
            _pauseTimer = 0f;
            _stallTimer = 0f;
            _lastX = Controller.Body != null ? Controller.Body.position.x : 0f;
            Controller.Anim?.SetBool(EnemyAnimParams.IsMove, true);
        }

        public override void Exit()
        {
            Controller.Anim?.SetBool(EnemyAnimParams.IsMove, false);
            Controller.StopMoving();
        }

        public override void Tick()
        {
            if (TryHandleCombatTransitions()) return;

            // 看见玩家 → 交给追击/攻击接管
            if (Controller.HasTarget && !DialogueActive)
            {
                Machine.SwitchState<EnemyChaseState>();
                return;
            }

            if (!Controller.CanPatrol)
            {
                Machine.SwitchState<EnemyIdleState>();
                return;
            }

            if (_pauseTimer > 0f)
            {
                _pauseTimer -= Time.deltaTime;
                Controller.StopMoving();
                return;
            }
        }

        public override void FixedTick()
        {
            if (!Controller.CanPatrol)
            {
                return;
            }

            if (_pauseTimer > 0f)
            {
                return;
            }

            float targetX = _dir > 0 ? Controller.PatrolRightX : Controller.PatrolLeftX;
            float dist = Mathf.Abs(targetX - Controller.Body.position.x);

            // 到达端点，或前方没地面（防掉崖）→ 原地停一会儿再反向
            if (dist < 0.06f || !Controller.HasGroundAhead(_dir))
            {
                FlipDirection();
                return;
            }

            // 被垃圾堆/墙体卡住：位置几乎不动就当作到达端点，别一直顶着墙
            float x = Controller.Body.position.x;
            if (Mathf.Abs(x - _lastX) < 0.02f)
            {
                _stallTimer += Time.fixedDeltaTime;
                if (_stallTimer > 0.35f)
                {
                    FlipDirection();
                    return;
                }
            }
            else
            {
                _stallTimer = 0f;
                _lastX = x;
            }

            Controller.FaceDirection(_dir);
            Controller.MoveTowardsX(targetX);
        }

        private void FlipDirection()
        {
            _dir = -_dir;
            _pauseTimer = Config.PatrolPauseSeconds;
            _stallTimer = 0f;
            Controller.StopMoving();
        }
    }

    /// <summary>追击状态：水平移动追击目标。</summary>
    public class EnemyChaseState : EnemyStateBase
    {
        /// <summary>推不动目标的墙前停留时长阈值 (s)。</summary>
        private const float BlockedSeconds = 0.6f;
        /// <summary>受阻后暂不重新锁定目标的时长 (s)，避免"锁→撞墙→丢→再锁"来回抖动。</summary>
        private const float GiveUpSeconds = 2f;

        private float _blockedTimer;
        private float _lastX;

        public EnemyChaseState(EnemyControllerBase controller, EnemyStateMachine machine)
            : base(controller, machine) { }

        public override void Enter()
        {
            Controller.Anim?.SetBool(EnemyAnimParams.IsMove, true);
            _blockedTimer = 0f;
            _lastX = Controller.Body != null ? Controller.Body.position.x : 0f;
        }

        public override void Exit()
        {
            Controller.Anim?.SetBool(EnemyAnimParams.IsMove, false);
        }

        public override void Tick()
        {
            if (TryHandleCombatTransitions()) return;

            if (!Controller.HasTarget)
            {
                Machine.SwitchState<EnemyIdleState>();
                return;
            }

            if (Controller.InAttackRange && Controller.AttackCooldownTimer <= 0f && !DialogueActive)
            {
                if (Config.AttackKind == EnemyConfig.AttackKindEnum.Melee)
                    Machine.SwitchState<EnemyMeleeAttackState>();
                else
                    Machine.SwitchState<EnemyRangedAttackState>();
                return;
            }

            CheckBlockedByWall();
        }

        /// <summary>
        /// 目标在墙体/垃圾堆另一侧：位置长时间不变就说明推不动，
        /// 短暂放弃目标退回巡逻，而不是一直顶着墙站着（玩家走回来仍会重新发现）。
        /// </summary>
        private void CheckBlockedByWall()
        {
            if (Controller.Body == null || !Machine.HasState<EnemyPatrolState>())
            {
                return;
            }

            float x = Controller.Body.position.x;
            if (Mathf.Abs(x - _lastX) < 0.02f)
            {
                _blockedTimer += Time.deltaTime;
            }
            else
            {
                _blockedTimer = 0f;
                _lastX = x;
            }

            if (_blockedTimer > BlockedSeconds)
            {
                Controller.IgnoreTargetFor(GiveUpSeconds);
                Machine.SwitchState<EnemyPatrolState>();
            }
        }

        public override void FixedTick()
        {
            Controller.MoveTowardsTarget();
        }
    }

    /// <summary>近战攻击状态：播放 Attack Trigger，冷却后回落。</summary>
    public class EnemyMeleeAttackState : EnemyStateBase
    {
        public EnemyMeleeAttackState(EnemyControllerBase controller, EnemyStateMachine machine)
            : base(controller, machine) { }

        public override void Enter()
        {
            Controller.AttackCooldownTimer = Config.AttackInterval;
            Controller.StopMoving();
            Controller.FaceTarget();
            Controller.TryFireAnim(EnemyAnimParams.Attack);
        }

        public override void Tick()
        {
            if (TryHandleCombatTransitions()) return;

            if (Controller.AttackCooldownTimer <= 0f)
            {
                if (Controller.HasTarget && Controller.InAttackRange && !DialogueActive)
                {
                    Controller.AttackCooldownTimer = Config.AttackInterval;
                    Controller.TryFireAnim(EnemyAnimParams.Attack);
                }
                else if (Controller.HasTarget)
                {
                    Machine.SwitchState<EnemyChaseState>();
                }
                else
                {
                    Machine.SwitchState<EnemyIdleState>();
                }
            }
        }
    }

    /// <summary>远程攻击状态：播放 Attack Trigger，HitOn 事件帧生成弹体。</summary>
    public class EnemyRangedAttackState : EnemyStateBase
    {
        public EnemyRangedAttackState(EnemyControllerBase controller, EnemyStateMachine machine)
            : base(controller, machine) { }

        public override void Enter()
        {
            Controller.AttackCooldownTimer = Config.AttackInterval;
            Controller.StopMoving();
            Controller.FaceTarget();
            Controller.TryFireAnim(EnemyAnimParams.Attack);
        }

        public override void Tick()
        {
            if (TryHandleCombatTransitions()) return;

            if (Controller.AttackCooldownTimer <= 0f)
            {
                if (Controller.HasTarget && Controller.InAttackRange && !DialogueActive)
                {
                    Controller.AttackCooldownTimer = Config.AttackInterval;
                    Controller.TryFireAnim(EnemyAnimParams.Attack);
                }
                else if (Controller.HasTarget)
                {
                    Machine.SwitchState<EnemyChaseState>();
                }
                else
                {
                    Machine.SwitchState<EnemyIdleState>();
                }
            }
        }
    }

    /// <summary>受击状态：硬直+击退，结束后回战斗/Idle。</summary>
    public class EnemyHurtState : EnemyStateBase
    {
        public EnemyHurtState(EnemyControllerBase controller, EnemyStateMachine machine)
            : base(controller, machine) { }

        public override void Enter()
        {
            Controller.StopMoving();
            Controller.TryFireAnim(EnemyAnimParams.Hurt);
            var kb = Controller.ConsumeKnockback();
            if (Controller.Body != null && kb.sqrMagnitude > 0.0001f)
            {
                Controller.Body.velocity = new Vector2(kb.x, Controller.Body.velocity.y);
            }
        }

        public override void Tick()
        {
            if (TryHandleDieTransition()) return;

            if (!Controller.IsStunned)
            {
                Machine.SwitchState(Controller.DecidePostHurtState());
            }
        }
    }

    /// <summary>死亡状态：关碰撞/判定/停移动，等 DieEnd 事件帧回收。</summary>
    public class EnemyDieState : EnemyStateBase
    {
        public EnemyDieState(EnemyControllerBase controller, EnemyStateMachine machine)
            : base(controller, machine) { }

        public override void Enter()
        {
            Controller.StopMoving();
            var col = Controller.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            var hitbox = Controller.GetComponentInChildren<EnemyAttackHitbox>();
            if (hitbox != null) hitbox.gameObject.SetActive(false);
            Controller.TryFireAnim(EnemyAnimParams.Die);
        }

        public override void Tick()
        {
        }
    }
}
