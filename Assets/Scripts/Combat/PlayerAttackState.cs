using System.Collections.Generic;
using UnityEngine;
using StarEco.Combat;
using StarEco.EnemyAI;
using StarEco.Framework;

namespace StarEco.Player
{
    /// <summary>
    /// 攻击状态（T4）：地面三段连击（Attack01→02→03）与空中单段（AttackAir）。
    /// 连段时序以 CombatConfig.SegmentEndTimes 为兜底；T7 接入 Animation Event 后以事件为准。
    ///
    /// 手感要点（2026-10-28 改）：
    /// 1) 边跑边打：攻击不再钉住水平速度，地面全速、空中沿用 AirControlRatio，
    ///    移动完全交给 MoveHorizontally（空洞骑士式）。也不再"新按方向就取消攻击"，
    ///    否则跑动中起手会被自己立刻打断。
    /// 2) 第三段改为投掷：动画播到挥臂最前处（ThrowReleaseTime）把手里的钳子甩出去，
    ///    命中判定全交给飞出去的钳子（AttackHitbox 在第三段不开窗）。
    /// </summary>
    public class PlayerAttackState : PlayerStateBase
    {
        /// <summary>第三段出手时刻 (s)：24fps 的 Attack03 第 5 帧附近，手臂挥到最前。</summary>
        internal const float ThrowReleaseTime = 0.19f;
        internal const float AirKickDuration = 31f / 60f;
        internal const float AirKickImpact = .22f;
        private const float AirKickContactGap = .64f;
        private const float AirKickApproachGap = .38f;
        private const float AirKickVerticalReach = .52f;
        private const float AirKickRange = 5.5f;
        private const float AirKickVerticalRange = 3.8f;
        private const float AirKickSpeed = 23f;
        private static readonly ContactFilter2D KickGround = new ContactFilter2D
            { useLayerMask = true, layerMask = LayerMask.GetMask("Ground") };
        private static readonly ContactFilter2D KickEnemies = new ContactFilter2D
            { useLayerMask = true, layerMask = LayerMask.GetMask("Enemy"), useTriggers = true };
        private readonly List<Collider2D> _kickCandidates = new List<Collider2D>(12);
        private readonly RaycastHit2D[] _kickWalls = new RaycastHit2D[4];
        private Collider2D _kickTarget;
        private EnemyControllerBase _kickEnemy;
        internal EnemyControllerBase KickEnemy => _kickEnemy;
        private float _kickFacing;
        private float _kickDuration;
        private float _kickImpactAt;

        private PlayerCombat _combat;
        private AttackHitbox _hitbox;

        private int _segment;             // 0=空中攻击 1/2/3=地面三段
        private float _segmentTimer;      // 本段动画剩余时长（兜底计时）
        private float _comboWindowTimer;  // 连段输入窗口剩余
        private bool _nextQueued;         // 窗口内缓冲的下一段输入
        private float _throwCountdown;    // 第三段的手部释放倒计时，>0 表示还没出手

        public PlayerAttackState(PlayerController player, PlayerStateMachine machine)
            : base(player, machine) { }

        public override void Enter()
        {
            _combat = Player.Combat;
            _nextQueued = false;
            _comboWindowTimer = 0f;
            _throwCountdown = 0f;
            Player.Anim?.ResetOneShot(PlayerAnimParams.AttackCancel);   // 清掉上次中断残留的取消触发器
            AudioManager.Instance?.PlaySfx(SfxId.Attack, 0.05f);   // M10：挥击音，轻微变调避免连击机械感

            if (!Player.IsGrounded)
            {
                _segment = 0;
                if (TrySelectKickTarget())
                {
                    _segmentTimer = _kickDuration;
                    Player.AirKickElapsed = 0f;
                    Player.Sprite.flipX = _kickFacing < 0f;
                }
                else
                {
                    _segmentTimer = EndTime(0);
                    Player.PlayOneShot("AttackAir");
                }
            }
            else
            {
                _segment = 1;
                _segmentTimer = EndTime(1);
                _comboWindowTimer = ComboWindow;
                Player.PlayOneShot("Attack");
            }

            _hitbox = Player.GetComponent<AttackHitbox>();
            if (_hitbox != null)
            {
                _hitbox.CurrentSegment = _segment;
            }
        }

        public override void Exit()
        {
            // 攻击被打断（受击 / 死亡 / 跑出平台）时立即关命中窗，避免"人已走开、判定还在"
            if (_hitbox != null)
            {
                _hitbox.OnAttackWindowClose();
            }

            _throwCountdown = 0f;
            if (Player.AirKickElapsed >= 0f)
            {
                Player.AirKickElapsed = -1f;
                StopKickTravel();
                _kickTarget = null;
                _kickEnemy = null;
            }
        }

        public override void FixedTick()
        {
            if (Player.AirKickElapsed >= 0f)
            {
                UpdateKickTravel();
                return;
            }

            // 边跑边打：不再 StopHorizontal。地面按正常移速、空中按 AirControlRatio，
            // 攻击只负责播动画与判定，位移交给移动逻辑（起手时已有的速度也自然延续）。
            Player.MoveHorizontally(!Player.IsGrounded);
        }

        public override void Tick()
        {
            if (TryHandleCombatTransitions())
            {
                return;   // 受击/死亡打断攻击（R4，优先级高于攻击）
            }

            // 攻击期间可被翻滚取消后摇（与 Idle/Run 保持一致的输入优先级）
            if (TryStartDodge())
            {
                return;
            }

            _segmentTimer -= Time.deltaTime;

            // ===== 空中单段 =====
            if (_segment == 0)
            {
                if (Player.AirKickElapsed >= 0f)
                {
                    float elapsed = _kickDuration - _segmentTimer;
                    // Keep the extension frame aligned with contact at both short and long distances.
                    Player.AirKickElapsed = elapsed < _kickImpactAt
                        ? Mathf.Lerp(0f, AirKickImpact, elapsed / _kickImpactAt)
                        : Mathf.Min(AirKickDuration, AirKickImpact + elapsed - _kickImpactAt);
                }
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

            // 地面段跑到平台外：交给空中状态，避免"人在天上、播着地面攻击"
            if (!Player.IsGrounded && Player.VerticalVelocity < -0.1f)
            {
                ExitToLocomotion();
                return;
            }

            // 第三段出手：到点把钳子甩出去（不在手上/已在空中时 TryThrow 返回 false，本下空挥）
            if (_throwCountdown > 0f)
            {
                _throwCountdown -= Time.deltaTime;
                if (_throwCountdown <= 0f)
                {
                    Player.ToolThrow?.TryThrow();
                }
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

        private bool TrySelectKickTarget()
        {
            Vector2 origin = Player.Body.worldCenterOfMass;
            Physics2D.OverlapCircle(origin, AirKickRange, KickEnemies, _kickCandidates);
            float nearest = AirKickRange * AirKickRange;
            _kickTarget = null;
            _kickEnemy = null;
            foreach (var collider in _kickCandidates)
            {
                var enemy = collider.GetComponentInParent<EnemyControllerBase>();
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsDead) continue;
                Vector2 targetPoint = collider.ClosestPoint(origin);
                Vector2 delta = targetPoint - origin;
                if (delta.sqrMagnitude >= nearest || Mathf.Abs(delta.y) > AirKickVerticalRange
                    || Physics2D.Linecast(origin, targetPoint, KickGround, _kickWalls) > 0)
                    continue;

                nearest = delta.sqrMagnitude;
                _kickTarget = collider;
                _kickEnemy = enemy;
                float horizontal = collider.bounds.center.x - origin.x;
                _kickFacing = Mathf.Abs(horizontal) > .05f ? Mathf.Sign(horizontal) : Player.FacingDir;
            }

            _kickImpactAt = Mathf.Clamp(Mathf.Sqrt(nearest) / 16f, AirKickImpact, .36f);
            _kickDuration = AirKickDuration + _kickImpactAt - AirKickImpact;
            return _kickTarget != null;
        }

        private void UpdateKickTravel()
        {
            if (ReferenceEquals(_kickTarget, null)) return;
            if (_kickTarget == null || !_kickTarget.enabled || !_kickEnemy || !_kickEnemy.isActiveAndEnabled || _kickEnemy.IsDead
                || Player.AirKickElapsed > AirKickImpact + .10f)
            {
                _kickTarget = null;
                _kickEnemy = null;
                StopKickTravel();
                return;
            }

            Vector2 origin = Player.Body.worldCenterOfMass;
            Vector2 point = _kickTarget.ClosestPoint(origin);
            Vector2 delta = point - origin;
            if (Physics2D.Linecast(origin, point, KickGround, _kickWalls) > 0)
            {
                _kickTarget = null;
                _kickEnemy = null;
                StopKickTravel();
                return;
            }

            float forwardGap = delta.x * _kickFacing;
            if (forwardGap >= -.12f && forwardGap <= AirKickContactGap
                && Mathf.Abs(delta.y) <= AirKickVerticalReach && Player.AirKickElapsed >= AirKickImpact)
            {
                int damage = _combat != null && _combat.Config != null ? _combat.Config.AttackDamage : 1;
                float push = _combat != null && _combat.Config != null ? _combat.Config.ThirdHitKnockback : 6f;
                _kickTarget = null;
                StopKickTravel();
                int previousHp = _kickEnemy.CurrentHP;
                bool useKickEffect = _hitbox != null && _hitbox.HasAirKickImpact;
                _kickEnemy.TakeDamage(new DamageInfo(damage, new Vector2(-_kickFacing, 0f), Player.gameObject)
                    { KnockbackPower = push, SuppressDefaultHitFx = useKickEffect }.WithPoint(point));
                if (useKickEffect && _kickEnemy != null && _kickEnemy.CurrentHP < previousHp)
                {
                    Vector2 effectPoint = Player.TryGetComponent<Player3DGameplayVisual>(out var visual)
                        ? visual.AirKickImpactPosition(point) : point;
                    _hitbox.PlayAirKickImpact(effectPoint, _kickFacing);
                }
                HitFeel.Play(.075f, .10f);
                return;
            }

            // Aim at the enemy's upper body so an attack from above visibly descends into the target.
            var bounds = _kickTarget.bounds;
            float aimHeight = bounds.center.y + bounds.extents.y * .45f;
            var approach = new Vector2(_kickFacing * Mathf.Max(0f, forwardGap - AirKickApproachGap),
                Mathf.Clamp(aimHeight - origin.y, delta.y - .4f, delta.y + .4f));
            float time = Mathf.Max(Time.fixedDeltaTime,
                _segmentTimer - (AirKickDuration - AirKickImpact) + Time.fixedDeltaTime);
            Vector2 velocity = Vector2.ClampMagnitude(approach / time, AirKickSpeed);
            if (velocity.y < 0f)
            {
                // Keep the player just above a platform while closing the remaining horizontal gap.
                float step = Time.fixedDeltaTime;
                const float floorClearance = .15f;
                int groundHits = Player.Body.Cast(Vector2.down, KickGround, _kickWalls,
                    -velocity.y * step + floorClearance);
                for (int i = 0; i < groundHits; i++)
                {
                    var hit = _kickWalls[i];
                    if (hit.normal.y > .5f)
                        velocity.y = Mathf.Max(velocity.y, -Mathf.Max(0f, hit.distance - floorClearance) / step);
                }
            }

            if (velocity.sqrMagnitude > .001f)
            {
                int obstacles = Player.Body.Cast(velocity.normalized, KickGround, _kickWalls,
                    velocity.magnitude * Time.fixedDeltaTime + .03f);
                for (int i = 0; i < obstacles; i++)
                {
                    if (_kickWalls[i].normal.y > .5f) continue;
                    _kickTarget = null;
                    _kickEnemy = null;
                    StopKickTravel();
                    return;
                }
            }

            Player.Body.velocity = velocity;
        }

        private void StopKickTravel()
        {
            Vector2 velocity = Player.Body.velocity;
            velocity.x = 0f;
            Player.Body.velocity = velocity;
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

            Player.PlayOneShot(_segment == 2 ? "Attack02" : "Attack03");

            // 第三段：记下出手倒计时（动画播到挥臂最前时由 Tick 释放投掷）
            if (_segment == 3)
            {
                _throwCountdown = ThrowReleaseTime;
            }
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
