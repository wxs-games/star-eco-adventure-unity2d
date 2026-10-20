using UnityEngine;
using StarEco.Framework;

namespace StarEco.Player
{
    /// <summary>
    /// 玩家控制器：物理与手感核心。
    /// 职责：输入采集（Update）、物理运动（FixedUpdate）、土狼时间/跳跃缓冲/可变跳高、朝向、地面检测、受击与死亡事件入口。
    /// 状态决策交给状态机，本类只提供"能力与事实"（能否跳、是否着地等）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerConfig _config;

        public PlayerConfig Config => _config;
        public Rigidbody2D Body { get; private set; }
        public SpriteRenderer Sprite { get; private set; }
        public PlayerAnimatorDriver Anim { get; private set; }
        public StarEco.Combat.PlayerCombat Combat { get; private set; }
        public StarEco.Eco.SprayController Spray { get; private set; }

        // ===== 运行时事实（供状态机读取） =====
        public float HorizontalInput { get; private set; }
        public bool JumpPressedThisFrame { get; private set; }
        public bool JumpReleasedThisFrame { get; private set; }
        public bool DodgePressedThisFrame { get; private set; }
        public bool IsGrounded { get; private set; }
        public float VerticalVelocity => Body.velocity.y;
        public bool IsDead { get; private set; }
        public bool InputLocked { get; set; }

        private PlayerStateMachine _machine;

        // 手感计时器
        private float _coyoteCounter;
        private float _jumpBufferCounter;
        private bool _canJump = true;   // 起跳窗口（地面/土狼时间）是否可用
        private int _remainingAirJumps; // 额外空中跳次数：当前需求为二段跳，所以这里是“剩余 1 次二段跳”

        // 闪避（翻滚）
        private float _dodgeCooldownTimer;
        private int _airDodgesUsed;

        // 受击
        private bool _pendingHurt;
        private float _invincibleTimer;
        private Vector2 _knockbackVelocity;

        // 常量输入轴（旧版 Input Manager）
        private const string AxisHorizontal = "Horizontal";
        private const string ButtonJump = "Jump";

        private void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            Sprite = GetComponent<SpriteRenderer>();
            Anim = GetComponent<PlayerAnimatorDriver>();
            Combat = GetComponent<StarEco.Combat.PlayerCombat>();
            Spray = GetComponent<StarEco.Eco.SprayController>();
            Body.freezeRotation = true;

            if (_config == null)
            {
                _config = Resources.Load<PlayerConfig>("PlayerConfig")
                          ?? PlayerConfig.CreateDefault();
            }

            BuildStateMachine();
        }

        private void BuildStateMachine()
        {
            _machine = new PlayerStateMachine();
            _machine.Register(new PlayerIdleState(this, _machine));
            _machine.Register(new PlayerRunState(this, _machine));
            _machine.Register(new PlayerAirState(this, _machine));
            _machine.Register(new PlayerLandState(this, _machine));
            _machine.Register(new PlayerDodgeState(this, _machine));
            _machine.Register(new PlayerAttackState(this, _machine));
            _machine.Register(new PlayerSprayState(this, _machine));
            _machine.Register(new PlayerChargeState(this, _machine));
            _machine.Register(new PlayerHurtState(this, _machine));
            _machine.Register(new PlayerDeathState(this, _machine));
            _machine.SetInitial<PlayerIdleState>();
        }

        private void Update()
        {
            EnsureMachine();
            ReadInput();
            UpdateTimers();
            UpdateInvincibility();
            UpdateStuckEscape();
            _machine.Tick();
        }

        private void FixedUpdate()
        {
            EnsureMachine();
            UpdateGrounded();
            UpdateGravity();
            _machine.FixedTick();
            ClampFallSpeed();
        }

        /// <summary>
        /// 保证状态机可用。域重载（Play 中途改脚本触发）会把实例字段清零但 Awake 不重跑，
        /// 导致 _machine 为 null 而 Update/FixedUpdate 照常被调用 → 每帧 NRE。
        /// 懒加载自愈：缺失时重建并复位初始态。BuildStateMachine 幂等，可重复调用。
        /// </summary>
        private void EnsureMachine()
        {
            if (_machine != null)
            {
                return;
            }
            BuildStateMachine();
        }

        // ===== 输入 =====

        private void ReadInput()
        {
            if (InputLocked || IsDead)
            {
                HorizontalInput = 0f;
                JumpPressedThisFrame = false;
                JumpReleasedThisFrame = false;
                DodgePressedThisFrame = false;
                return;
            }

            float axis = Input.GetAxisRaw(AxisHorizontal);

            // 卡住自救期间临时接管水平输入，把玩家推离支撑物（见 UpdateStuckEscape/TryUnstick）
            if (_escapeInputTimer > 0f)
            {
                _escapeInputTimer -= Time.deltaTime;
                axis = _escapeDir;
            }

            HorizontalInput = axis;

            JumpPressedThisFrame = Input.GetButtonDown(ButtonJump);
            if (JumpPressedThisFrame)
            {
                _jumpBufferCounter = _config.JumpBufferTime;
            }

            JumpReleasedThisFrame = Input.GetButtonUp(ButtonJump);
            if (JumpReleasedThisFrame && Body.velocity.y > 0f)
            {
                // 可变跳高：提前松键 = 短跳
                Body.velocity = new Vector2(Body.velocity.x, Body.velocity.y * _config.JumpCutMultiplier);
            }

            // 闪避（翻滚）：左 Shift
            DodgePressedThisFrame = Input.GetKeyDown(KeyCode.LeftShift);
        }

        private void UpdateTimers()
        {
            // 土狼时间
            if (IsGrounded)
            {
                _coyoteCounter = _config.CoyoteTime;
                _canJump = true;
                _remainingAirJumps = 1;
                _airDodgesUsed = 0;   // 落地重置空中闪避次数（R1）
            }
            else
            {
                _coyoteCounter -= Time.deltaTime;
            }

            // 跳跃缓冲
            if (_jumpBufferCounter > 0f)
            {
                _jumpBufferCounter -= Time.deltaTime;
            }

            // 闪避冷却
            if (_dodgeCooldownTimer > 0f)
            {
                _dodgeCooldownTimer -= Time.deltaTime;
            }
        }

        // ===== 物理能力 =====

        /// <summary>
        /// 尝试消耗一次跳跃：
        /// - 地面/土狼时间内：执行第一段跳；
        /// - 已离地且额外次数>0：执行第二段跳（当前先复用第一段跳动画逻辑，占位）。
        /// </summary>
        public bool TryConsumeJump()
        {
            bool allowGroundJump = _coyoteCounter > 0f && _canJump;
            bool allowAirJump = !IsGrounded && _remainingAirJumps > 0;
            bool allowed = allowGroundJump || allowAirJump;
            if (allowed)
            {
                if (allowGroundJump)
                {
                    _coyoteCounter = 0f;
                    _canJump = false;
                }
                else
                {
                    _remainingAirJumps--;
                }

                _jumpBufferCounter = 0f;
                Body.velocity = new Vector2(Body.velocity.x, _config.JumpVelocity);
                AudioManager.Instance?.PlaySfx(SfxId.Jump);   // M10
            }

            return allowed;
        }

        /// <summary>跳跃缓冲是否有效（触地瞬间自动起跳用）。</summary>
        public bool HasJumpBuffer => _jumpBufferCounter > 0f;

        // ===== 闪避（翻滚）=====

        /// <summary>当前是否可以起翻滚：冷却已结束，且（在地面，或空中次数未用尽）。</summary>
        public bool CanStartDodge =>
            _config != null && _dodgeCooldownTimer <= 0f
            && (IsGrounded || _airDodgesUsed < _config.AirDodgeCount);

        /// <summary>状态机消费闪避输入：仅当前帧有效，消费即清。</summary>
        public bool ConsumeDodgeInput()
        {
            if (!DodgePressedThisFrame)
            {
                return false;
            }

            DodgePressedThisFrame = false;
            return true;
        }

        /// <summary>进入翻滚：起冷却、开无敌窗、空中计数 +1，并按方向翻转朝向。</summary>
        public void BeginDodge(float dir)
        {
            _dodgeCooldownTimer = _config.DodgeCooldown;
            _invincibleTimer = Mathf.Max(_invincibleTimer, _config.DodgeInvincibleTime);
            if (!IsGrounded)
            {
                _airDodgesUsed++;
            }

            Sprite.flipX = dir < 0f;
        }

        /// <summary>水平移动（状态机在 FixedTick 调用；air 参数控制空中控制力；speedFactor 为移速因子，喷洒减速用）。</summary>
        public void MoveHorizontally(bool inAir, float speedFactor = 1f)
        {
            float targetSpeed = HorizontalInput * _config.MoveSpeed * (inAir ? _config.AirControlRatio : 1f) * speedFactor;
            float newSpeed = Mathf.MoveTowards(Body.velocity.x, targetSpeed, _config.Acceleration * Time.fixedDeltaTime);
            Body.velocity = new Vector2(newSpeed, Body.velocity.y);

            if (Mathf.Abs(HorizontalInput) > 0.01f)
            {
                Sprite.flipX = HorizontalInput < 0f;
            }
        }

        public void StopHorizontal()
        {
            Body.velocity = new Vector2(0f, Body.velocity.y);
        }

        public void SetKnockback(Vector2 velocity)
        {
            Body.velocity = velocity;
        }

        private void UpdateGravity()
        {
            float targetScale = Body.velocity.y < 0f
                ? _config.GravityScale * _config.FallGravityMultiplier
                : _config.GravityScale;

            Body.gravityScale = Mathf.MoveTowards(Body.gravityScale, targetScale, Time.fixedDeltaTime * 10f);
        }

        private void ClampFallSpeed()
        {
            if (Body.velocity.y < -_config.MaxFallSpeed)
            {
                Body.velocity = new Vector2(Body.velocity.x, -_config.MaxFallSpeed);
            }
        }

        private void UpdateGrounded()
        {
            var bounds = Sprite.bounds;
            float castDistance = _config.GroundCheckSkin + 0.02f;

            // 旧逻辑只有“脚底中心”一根射线：站在窄平台/方块边缘时，
            // 身体还压在平台上，但中心点已经探出边缘，导致误判成未落地。
            // 改为脚底左/中/右三点探测，只要任一点命中 GroundLayer 就算落地。
            float inset = Mathf.Min(0.05f, bounds.extents.x * 0.25f);
            float leftX = bounds.min.x + inset;
            float centerX = bounds.center.x;
            float rightX = bounds.max.x - inset;
            float originY = bounds.min.y + 0.02f;

            bool hitLeft = Physics2D.Raycast(new Vector2(leftX, originY), Vector2.down,
                castDistance, _config.GroundLayer);
            bool hitCenter = Physics2D.Raycast(new Vector2(centerX, originY), Vector2.down,
                castDistance, _config.GroundLayer);
            bool hitRight = Physics2D.Raycast(new Vector2(rightX, originY), Vector2.down,
                castDistance, _config.GroundLayer);

            IsGrounded = hitLeft || hitCenter || hitRight;
        }

        // ===== 卡住自救 =====

        // 判定阈值：StuckEscapeSeconds 时间窗内总位移小于 StuckMoveEpsilon 视为卡住。
        private const float StuckEscapeSeconds = 0.5f;
        private const float StuckMoveEpsilon = 0.05f;
        private const float EscapeInputSeconds = 0.45f;   // 接管水平输入的持续时间
        private float _stuckProbeTime;
        private Vector2 _stuckProbePos;
        private float _escapeInputTimer;
        private float _escapeDir;

        /// <summary>
        /// 卡住自救（2026-10-19 实测补漏）：地面检测只认 <see cref="PlayerConfig.GroundLayer"/>，
        /// 而垃圾堆等可净化目标是 Default 层实体碰撞体——玩家跳/走上堆顶时，
        /// <see cref="IsGrounded"/> 恒为 false，状态机卡在 PlayerAirState 无法移动。
        /// 判定用"时间窗内总位移"而非逐帧速度/位移：物理求解器的接触抖动（逐帧亚像素起伏）
        /// 不会误触发，只有真正静止才累计。触发后 <see cref="TryUnstick"/> 临时接管水平输入，
        /// 让玩家沿既有移动逻辑滑下堆顶回到真地面（不传送、无掉落风险）。
        /// 脚下探测不到非地面实体时不动作（交由坠落/溺水/复活逻辑处理）。
        /// </summary>
        private void UpdateStuckEscape()
        {
            // 死亡/输入锁/已落地都不需要自救；同时刷新探针，避免离地瞬间误判
            if (IsDead || InputLocked || IsGrounded || _config == null)
            {
                ResetStuckProbe();
                return;
            }

            if (Time.time - _stuckProbeTime < StuckEscapeSeconds)
            {
                return;
            }

            // 窗口内几乎没动 → 判定卡住，尝试推离支撑物；之后重置窗口（失败则 0.5s 后重试）
            if ((Body.position - _stuckProbePos).magnitude < StuckMoveEpsilon)
            {
                TryUnstick();
            }

            ResetStuckProbe();
        }

        private void ResetStuckProbe()
        {
            _stuckProbePos = Body.position;
            _stuckProbeTime = Time.time;
        }

        /// <summary>
        /// 探测脚底下压着的"非 GroundLayer 实体"（典型：Default 层的垃圾堆），
        /// 找到就临时接管水平输入、把玩家推离其中心，使其滑下堆顶落回真地面。
        /// 返回是否找到支撑物。
        /// </summary>
        private bool TryUnstick()
        {
            var col = GetComponent<Collider2D>();
            if (col == null)
            {
                return false;
            }

            Vector2 size = col.bounds.size;
            // 身体碰撞体带圆角时，物理外形比 bounds 外扩 r（bounds 仍按 size 返回）；
            // 又因碰撞体 offset 已下移对齐精灵脚底，真实脚底 = col.bounds.min.y - r，
            // 不能再假设碰撞体与 transform 同心（2026-10-20）。
            float edgeRadius = col is BoxCollider2D box ? box.edgeRadius : 0f;
            // 探测盒放在脚底正下方：下探 0.25u 覆盖堆顶
            Vector2 probeCenter = new Vector2(Body.position.x, col.bounds.min.y - edgeRadius - 0.02f);
            Vector2 probeSize = new Vector2(size.x * 0.9f, 0.25f);

            foreach (var c in Physics2D.OverlapBoxAll(probeCenter, probeSize, 0f))
            {
                if (c == null || c == col || !c.enabled || c.isTrigger)
                {
                    continue;
                }
                if (c.transform.IsChildOf(transform))
                {
                    continue;
                }
                // 真地面（GroundLayer）不算卡人来源
                if (((1 << c.gameObject.layer) & _config.GroundLayer.value) != 0)
                {
                    continue;
                }

                float dir = Mathf.Sign(Body.position.x - c.bounds.center.x);
                if (Mathf.Approximately(dir, 0f))
                {
                    dir = -1f;   // 正压在堆心：默认往左滑
                }

                _escapeDir = dir;
                _escapeInputTimer = EscapeInputSeconds;
                return true;
            }

            return false;
        }

        // ===== 受击 / 死亡（M3 起由伤害系统调用） =====

        public void ApplyHit(Vector2 fromDirection, int currentHp, int maxHp)
        {
            if (_invincibleTimer > 0f || IsDead)
            {
                return;
            }

            _pendingHurt = true;
            _invincibleTimer = _config.HurtInvincibleTime;
            _knockbackVelocity = new Vector2(Mathf.Sign(fromDirection.x) * -_config.HurtKnockback, _config.HurtKnockback * 0.6f);

            if (currentHp <= 0)
            {
                IsDead = true;
                InputLocked = true;
            }
        }

        /// <summary>状态机轮询：有无未处理的受击事件。</summary>
        public bool ConsumePendingHurt()
        {
            if (_pendingHurt)
            {
                _pendingHurt = false;
                return true;
            }

            return false;
        }

        public Vector2 TakeKnockback()
        {
            return _knockbackVelocity;
        }

        /// <summary>无敌期剩余时间（闪烁效果用）。</summary>
        public float InvincibleTimer => _invincibleTimer;

        /// <summary>
        /// 复活（M6 检查点恢复）：清死亡/输入锁、零化速度与击退、复位动画参数。
        /// 刻意不直接改 HP —— HP 由 PlayerCombat.RestoreHP 负责。
        /// </summary>
        public void Revive()
        {
            IsDead = false;
            InputLocked = false;
            _pendingHurt = false;
            _knockbackVelocity = Vector2.zero;
            _dodgeCooldownTimer = 0f;
            _airDodgesUsed = 0;

            if (Body != null)
            {
                Body.isKinematic = false;
                Body.bodyType = RigidbodyType2D.Dynamic;
                Body.simulated = true;
                Body.velocity = Vector2.zero;
            }

            var collider = GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.enabled = true;
            }

            // 复位 Animator 一次性触发器并切回 Idle（否则死亡状态触发器残留、状态机停在死亡态）
            if (Anim != null)
            {
                var animator = Anim.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.ResetTrigger(PlayerAnimParams.Hurt);
                    animator.ResetTrigger(PlayerAnimParams.Death);
                    animator.SetBool(PlayerAnimParams.IsCrouching, false);
                }
            }

            _machine?.SwitchState<PlayerIdleState>();
        }

        /// <summary>
        /// 蓄力进度比例 [0,1]（M7 HUD 蓄力条数据源）：非蓄力状态返回 0。
        /// 唯一改动点是本只读属性（M7 design §2.7 论证的必要最小增量）。
        /// </summary>
        public float ChargeRatio =>
            _machine != null && _machine.Current is PlayerChargeState charge
                ? charge.ChargeRatio
                : 0f;

        /// <summary>触发一次性动画（受击/死亡/攻击/互动），驱动器缺失时静默。</summary>
        public void TryFireAnim(int triggerHash)
        {
            Anim?.FireOneShot(triggerHash);
        }

        /// <summary>
        /// Animation Event 兜底接收器：攻击动画事件会发到 Animator 所在的 Player 根物体。
        /// 真正的命中逻辑仍由 AttackHitbox 负责；这里避免缺组件/引用时刷“has no receiver”。
        /// </summary>
        public void OnAttackWindowOpen()
        {
            GetComponent<StarEco.Combat.AttackHitbox>()?.OnAttackWindowOpen();
        }

        /// <summary>Animation Event 兜底接收器，见 OnAttackWindowOpen。</summary>
        public void OnAttackWindowClose()
        {
            GetComponent<StarEco.Combat.AttackHitbox>()?.OnAttackWindowClose();
        }

        private void UpdateInvincibility()
        {
            if (_invincibleTimer > 0f)
            {
                _invincibleTimer -= Time.deltaTime;
            }
        }
    }
}
