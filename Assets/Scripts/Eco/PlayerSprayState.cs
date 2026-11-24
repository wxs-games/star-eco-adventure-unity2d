using UnityEngine;
using StarEco.Eco;

namespace StarEco.Player
{
    /// <summary>
    /// 喷洒状态（M5）：按住 K 期间持续结算判定盒内目标，减速不锁死；
    /// 松开、净化剂耗尽、离地、受击或死亡时退出。
    /// </summary>
    public class PlayerSprayState : PlayerStateBase
    {
        private static readonly int InteractStateHash = Animator.StringToHash("Interact");

        private SprayController _spray;
        private PurifyTank _tank;
        private Animator _animator;
        private Player3DGameplayVisual _visual;

        public PlayerSprayState(PlayerController player, PlayerStateMachine machine)
            : base(player, machine) { }

        public override void Enter()
        {
            _spray = Player.GetComponent<SprayController>();
            _tank = Player.GetComponent<PurifyTank>();
            _animator = Player.GetComponent<Animator>();
            _visual = Player.GetComponent<Player3DGameplayVisual>();
            if (_visual == null || !_visual.HasSprayVisual)
                Player.TryFireAnim(PlayerAnimParams.Interact);
            _spray?.BeginSpray();
        }

        public override void Exit()
        {
            _spray?.EndSpray();
        }

        public override void FixedTick()
        {
            // 减速不锁死（R2）：移速因子由 SprayController 提供
            Player.MoveHorizontally(false, _spray != null ? _spray.SpraySpeedFactor : 1f);
        }

        public override void Tick()
        {
            if (TryHandleCombatTransitions())
            {
                return;
            }

            // 喷洒仅地面：走下平台立即转空中
            if (!Player.IsGrounded)
            {
                Machine.SwitchState<PlayerAirState>();
                return;
            }

            // 松开 / 净化剂耗尽 → 自动退出（R2，不报错不产生负值）
            bool held = _spray != null && _spray.SprayHeld;
            bool hasTank = _tank == null || !_tank.IsEmpty;
            if (!held || !hasTank)
            {
                SwitchGroundLocomotion();
                return;
            }

            // 2D 角色使用 Interact；3D 喷洒独立采样，保持 Animator 的移动状态可直接衔接。
            if ((_visual == null || !_visual.HasSprayVisual) && _animator != null
                && !_animator.IsInTransition(0)
                && _animator.GetCurrentAnimatorStateInfo(0).shortNameHash != InteractStateHash)
            {
                Player.TryFireAnim(PlayerAnimParams.Interact);
            }
        }
    }
}
