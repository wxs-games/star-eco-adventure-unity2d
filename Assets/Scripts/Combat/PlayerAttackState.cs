using UnityEngine;
using StarEco.Combat;
using StarEco.Framework;

namespace StarEco.Player
{
    /// <summary>
    /// 攻击状态（T4）：地面三段连击（Attack01→02→03）与空中单段（AttackAir）。
    /// 连段时序以 CombatConfig.SegmentEndTimes 为兜底；T7 接入 Animation Event 后以事件为准。
    /// 原地攻击：锁水平位移、不转身（R1）。
    /// </summary>
    public class PlayerAttackState : PlayerStateBase
    {
        private PlayerCombat _combat;
        private AttackHitbox _hitbox;

        private int _segment;             // 0=空中攻击 1/2/3=地面三段
        private float _segmentTimer;      // 本段动画剩余时长（兜底计时）
        private float _comboWindowTimer;  // 连段输入窗口剩余
        private bool _nextQueued;         // 窗口内缓冲的下一段输入

        public PlayerAttackState(PlayerController player, PlayerStateMachine machine)
            : base(player, machine) { }

        public override void Enter()
        {
            _combat = Player.Combat;
            _nextQueued = false;
            _comboWindowTimer = 0f;
            AudioManager.Instance?.PlaySfx(SfxId.Attack);   // M10

            if (!Player.IsGrounded)
            {
                _segment = 0;
                _segmentTimer = EndTime(0);
                Player.TryFireAnim(PlayerAnimParams.AttackAir);
            }
            else
            {
                _segment = 1;
                _segmentTimer = EndTime(1);
                _comboWindowTimer = ComboWindow;
                Player.TryFireAnim(PlayerAnimParams.Attack);
            }

            _hitbox = Player.GetComponent<AttackHitbox>();
            if (_hitbox != null)
            {
                _hitbox.CurrentSegment = _segment;
            }
        }

        public override void FixedTick()
        {
            Player.StopHorizontal();   // 原地攻击（R1：锁移动；不调用 MoveHorizontally 即不翻转朝向）
        }

        public override void Tick()
        {
            if (TryHandleCombatTransitions())
            {
                return;   // 受击/死亡打断攻击（R4，优先级高于攻击）
            }

            _segmentTimer -= Time.deltaTime;

            // ===== 空中单段 =====
            if (_segment == 0)
            {
                // 触地优先 → Land（R2）
                if (Player.IsGrounded && Player.VerticalVelocity <= 0.01f)
                {
                    Machine.SwitchState<PlayerLandState>();
                    return;
                }

                if (_segmentTimer <= 0f)
                {
                    Machine.SwitchState<PlayerAirState>();
                }

                return;
            }

            // ===== 地面三段链 =====
            if (_comboWindowTimer > 0f)
            {
                _comboWindowTimer -= Time.deltaTime;
                if (_combat != null && _combat.ConsumeAttackPressed())
                {
                    _nextQueued = true;
                }
            }

            if (_segmentTimer > 0f)
            {
                return;   // 本段动画未播完
            }

            if (_nextQueued && _segment < 3)
            {
                AdvanceCombo();   // 窗口内有输入 → 播完即衔接下一段（R1）
            }
            else
            {
                ExitToLocomotion();
            }
        }

        private void AdvanceCombo()
        {
            _segment++;
            _nextQueued = false;
            _segmentTimer = EndTime(_segment);
            _comboWindowTimer = _segment < 3 ? ComboWindow : 0f;   // 第三段后不再缓冲

            if (_hitbox != null)
            {
                _hitbox.CurrentSegment = _segment;
            }

            Player.TryFireAnim(_segment == 2 ? PlayerAnimParams.Attack2 : PlayerAnimParams.Attack3);
        }

        private void ExitToLocomotion()
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

        private float EndTime(int segment)
        {
            if (_combat == null || _combat.Config == null || _combat.Config.SegmentEndTimes == null)
            {
                return 0.3f;
            }

            int idx = Mathf.Clamp(segment, 0, _combat.Config.SegmentEndTimes.Length - 1);
            return Mathf.Max(0.05f, _combat.Config.SegmentEndTimes[idx]);
        }

        private float ComboWindow =>
            _combat != null && _combat.Config != null ? _combat.Config.ComboWindowSeconds : 0.15f;
    }
}
