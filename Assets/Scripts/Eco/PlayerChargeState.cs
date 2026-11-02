using UnityEngine;
using StarEco.Eco;

namespace StarEco.Player
{
    /// <summary>
    /// 蓄力状态（M5）：长按 L 站定蓄力；未满松开取消（零消耗），蓄满松开同帧释放净化波
    /// 并播放非攻击表现（Interact），恢复硬直后回地面状态。与喷洒互斥（本状态不检查 SprayHeld）。
    /// 净化波本身无攻击性，不伤害敌人，不触发 AttackHitbox。
    /// </summary>
    public class PlayerChargeState : PlayerStateBase
    {
        private SprayController _spray;
        private float _chargeTimer;
        private bool _released;
        private float _recoverTimer;

        /// <summary>蓄力进度比例 [0,1]（M7 HUD 蓄力条数据源；蓄满保持 1）。</summary>
        public float ChargeRatio
        {
            get
            {
                float full = _spray != null && _spray.Config != null
                    ? _spray.Config.HeavyChargeSeconds
                    : 0f;
                if (full <= 0f)
                {
                    return _released ? 0f : 0f;
                }

                return Mathf.Clamp01(_chargeTimer / full);
            }
        }

        public PlayerChargeState(PlayerController player, PlayerStateMachine machine)
            : base(player, machine) { }

        public override void Enter()
        {
            _spray = Player.GetComponent<SprayController>();
            _chargeTimer = 0f;
            _released = false;
            Player.StopHorizontal();
        }

        public override void FixedTick()
        {
            // 站定：蓄力与释放恢复期均锁水平位移（不调 MoveHorizontally 即不翻转朝向）
            Player.StopHorizontal();
        }

        public override void Tick()
        {
            if (TryHandleCombatTransitions())
            {
                return;
            }

            // 释放后恢复硬直
            if (_released)
            {
                _recoverTimer -= Time.deltaTime;
                if (_recoverTimer <= 0f)
                {
                    SwitchGroundLocomotion();
                }

                return;
            }

            // 离地取消（不打断已蓄满的判定，走下平台的边界）
            if (!Player.IsGrounded)
            {
                Machine.SwitchState<PlayerAirState>();
                return;
            }

            bool held = _spray != null && _spray.ChargeHeld;

            // 松开：蓄满释放 / 未满取消（R3）
            if (!held)
            {
                if (IsChargeFull)
                {
                    ReleaseWave();
                }
                else
                {
                    SwitchGroundLocomotion();
                }

                return;
            }

            // 持续按住：累加蓄力，蓄满后保持（不重复消耗）
            _chargeTimer += Time.deltaTime;
        }

        private bool IsChargeFull =>
            _spray != null && _spray.Config != null
            && _chargeTimer >= _spray.Config.HeavyChargeSeconds;

        private void ReleaseWave()
        {
            _spray?.ReleasePurifyWave();   // 同帧结算（含扣净化剂）—— 仅净化，不伤害敌人
            // 不再播放 Player_HeavyAttack：净化波不是攻击，不应触发 AttackHitbox 或播攻击动作。
            // 改用 Interact 表现（与喷洒同款非攻击动画），或由策划替换为专门的净化波动画。
            Player.TryFireAnim(PlayerAnimParams.Interact);
            _released = true;
            _recoverTimer = _spray != null && _spray.Config != null
                ? _spray.Config.HeavyRecoverSeconds
                : 0.5f;
        }
    }
}
