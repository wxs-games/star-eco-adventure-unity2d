using UnityEngine;

namespace StarEco.Player
{
    /// <summary>待机：地面静止。</summary>
    public class PlayerIdleState : PlayerStateBase
    {
        public PlayerIdleState(PlayerController player, PlayerStateMachine machine)
            : base(player, machine) { }

        public override void FixedTick()
        {
            Player.StopHorizontal();
        }

        public override void Tick()
        {
            if (TryHandleCombatTransitions())
            {
                return;
            }

            if (Player.Combat != null && Player.Combat.ConsumeAttackPressed())
            {
                Machine.SwitchState<PlayerAttackState>();
                return;
            }

            // 跳跃缓冲（含土狼时间内的起跳）
            if (Player.HasJumpBuffer && Player.TryConsumeJump())
            {
                Machine.SwitchState<PlayerAirState>();
                return;
            }

            SwitchGroundLocomotion();

            // 地面静止但角色离地（走下平台）→ 空中
            if (!Player.IsGrounded && Machine.CurrentType == typeof(PlayerIdleState))
            {
                Machine.SwitchState<PlayerAirState>();
            }
        }
    }

    /// <summary>奔跑：地面水平移动。</summary>
    public class PlayerRunState : PlayerStateBase
    {
        public PlayerRunState(PlayerController player, PlayerStateMachine machine)
            : base(player, machine) { }

        public override void FixedTick()
        {
            Player.MoveHorizontally(false);
        }

        public override void Tick()
        {
            if (TryHandleCombatTransitions())
            {
                return;
            }

            if (Player.Combat != null && Player.Combat.ConsumeAttackPressed())
            {
                Machine.SwitchState<PlayerAttackState>();
                return;
            }

            if (Player.HasJumpBuffer && Player.TryConsumeJump())
            {
                Machine.SwitchState<PlayerAirState>();
                return;
            }

            SwitchGroundLocomotion();

            if (!Player.IsGrounded && Machine.CurrentType == typeof(PlayerRunState))
            {
                Machine.SwitchState<PlayerAirState>();
            }
        }
    }

    /// <summary>
    /// 空中：上升(Jump)与下落(Fall)合并为一个状态，
    /// 动画由 Animator 依据 VelocityY 参数分支（设计说明见 spec design.md）。
    /// </summary>
    public class PlayerAirState : PlayerStateBase
    {
        public PlayerAirState(PlayerController player, PlayerStateMachine machine)
            : base(player, machine) { }

        public override void FixedTick()
        {
            Player.MoveHorizontally(true);
        }

        public override void Tick()
        {
            if (TryHandleCombatTransitions())
            {
                return;
            }

            if (Player.Combat != null && Player.Combat.ConsumeAttackPressed())
            {
                Machine.SwitchState<PlayerAttackState>();
                return;
            }

            // 空中不允许二段跳；触地 → Land
            if (Player.IsGrounded && Player.VerticalVelocity <= 0.01f)
            {
                Machine.SwitchState<PlayerLandState>();
            }
        }
    }

    /// <summary>落地：短暂锁定移动并播放 Land 动画，随后回到地面移动状态。</summary>
    public class PlayerLandState : PlayerStateBase
    {
        private float _lockTimer;

        public PlayerLandState(PlayerController player, PlayerStateMachine machine)
            : base(player, machine) { }

        public override void Enter()
        {
            _lockTimer = Player.Config.LandLockTime;
        }

        public override void FixedTick()
        {
            if (_lockTimer > 0f)
            {
                Player.StopHorizontal();
            }
            else
            {
                Player.MoveHorizontally(false);
            }
        }

        public override void Tick()
        {
            if (TryHandleCombatTransitions())
            {
                return;
            }

            _lockTimer -= Time.deltaTime;

            // 落地瞬间恰好有跳跃缓冲 → 立即再跳（连贯跳跃手感）
            if (_lockTimer <= 0f && Player.HasJumpBuffer && Player.TryConsumeJump())
            {
                Machine.SwitchState<PlayerAirState>();
                return;
            }

            if (_lockTimer <= 0f)
            {
                SwitchGroundLocomotion();

                if (!Player.IsGrounded && Machine.CurrentType == typeof(PlayerLandState))
                {
                    Machine.SwitchState<PlayerAirState>();
                }
            }
        }
    }

    /// <summary>受击：击退 + 无敌闪烁，结束后回到地面/空中状态。</summary>
    public class PlayerHurtState : PlayerStateBase
    {
        private float _recoverTimer;

        public PlayerHurtState(PlayerController player, PlayerStateMachine machine)
            : base(player, machine) { }

        public override void Enter()
        {
            // 击退：水平反向 + 小幅上抛
            var knockback = Player.TakeKnockback();
            Player.SetKnockback(knockback);
            Player.TryFireAnim(PlayerAnimParams.Hurt);
            // 硬直时长来自 CombatConfig（T5：GDD 初值 0.6s）
            _recoverTimer = Player.Combat != null && Player.Combat.Config != null
                ? Player.Combat.Config.StunDuration
                : 0.6f;
            Player.InputLocked = true;
        }

        public override void Exit()
        {
            Player.Sprite.color = Color.white;
            Player.InputLocked = false;
        }

        public override void FixedTick()
        {
            // 击退期间不接管水平速度（保留 SetKnockback 的冲量）
        }

        public override void Tick()
        {
            if (TryHandleDieTransition())
            {
                return;
            }

            _recoverTimer -= Time.deltaTime;

            // 无敌闪烁
            float alpha = Player.InvincibleTimer > 0f
                ? (Mathf.FloorToInt(Player.InvincibleTimer * 12f) % 2 == 0 ? 1f : 0.4f)
                : 1f;
            Player.Sprite.color = new Color(1f, 1f, 1f, alpha);

            if (_recoverTimer <= 0f)
            {
                if (Player.IsGrounded)
                {
                    SwitchGroundLocomotion();
                }
                else
                {
                    Machine.SwitchState<PlayerAirState>();
                }
            }
        }
    }

    /// <summary>死亡：停输入停碰撞，播放 Death 动画后由关卡流程接管重生。</summary>
    public class PlayerDeathState : PlayerStateBase
    {
        public PlayerDeathState(PlayerController player, PlayerStateMachine machine)
            : base(player, machine) { }

        public override void Enter()
        {
            Player.StopHorizontal();
            Player.TryFireAnim(PlayerAnimParams.Death);

            var collider = Player.GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            Player.Body.isKinematic = true;
            Player.Body.velocity = Vector2.zero;
        }

        public override void Tick()
        {
            // 死亡是终态；重生逻辑由 LevelFlow（M6）通过重载场景/回检查点实现
        }
    }
}
