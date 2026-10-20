using UnityEngine;

namespace StarEco.Player
{
    /// <summary>
    /// 玩家状态公共基类：持有控制器与状态机引用，提供常用上下文方法。
    /// </summary>
    public abstract class PlayerStateBase : IPlayerState
    {
        protected readonly PlayerController Player;
        protected readonly PlayerStateMachine Machine;

        protected PlayerStateBase(PlayerController player, PlayerStateMachine machine)
        {
            Player = player;
            Machine = machine;
        }

        public virtual void Enter() { }
        public virtual void Exit() { }
        public virtual void Tick() { }
        public virtual void FixedTick() { }

        // ===== 常用转移快捷方式 =====

        /// <summary>死亡优先级最高。发生转移返回 true（调用方应立即 return）。</summary>
        protected bool TryHandleDieTransition()
        {
            if (Player.IsDead)
            {
                Machine.SwitchState<PlayerDeathState>();
                return true;
            }

            return false;
        }

        /// <summary>受击优先级次高。发生转移返回 true（调用方应立即 return）。</summary>
        protected bool TryHandleHurtTransition()
        {
            if (Player.ConsumePendingHurt())
            {
                Machine.SwitchState<PlayerHurtState>();
                return true;
            }

            return false;
        }

        /// <summary>死亡与受击统一检查：命中任一即转移。</summary>
        protected bool TryHandleCombatTransitions()
        {
            return TryHandleDieTransition() || TryHandleHurtTransition();
        }

        /// <summary>
        /// 闪避（翻滚）触发检查：可闪避且本帧按下左 Shift → 切入 Dodge 状态。
        /// 发生转移返回 true（调用方应立即 return）。
        /// </summary>
        protected bool TryStartDodge()
        {
            if (Player.CanStartDodge && Player.ConsumeDodgeInput())
            {
                Machine.SwitchState<PlayerDodgeState>();
                return true;
            }

            return false;
        }

        /// <summary>地面移动通用转移：Idle ⇄ Run。</summary>
        protected void SwitchGroundLocomotion()
        {
            if (Mathf.Abs(Player.HorizontalInput) > 0.01f)
            {
                Machine.SwitchState<PlayerRunState>();
            }
            else
            {
                Machine.SwitchState<PlayerIdleState>();
            }
        }
    }
}
