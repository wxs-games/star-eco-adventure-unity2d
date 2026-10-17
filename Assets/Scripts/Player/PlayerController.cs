using UnityEngine;

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
        public bool IsGrounded { get; private set; }
        public float VerticalVelocity => Body.velocity.y;
        public bool IsDead { get; private set; }
        public bool InputLocked { get; set; }

        private PlayerStateMachine _machine;

        // 手感计时器
        private float _coyoteCounter;
        private float _jumpBufferCounter;
        private bool _canJump = true;   // 单段跳：本次离地是否已消耗跳跃

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
            _machine.Register(new PlayerAttackState(this, _machine));
            _machine.Register(new PlayerSprayState(this, _machine));
            _machine.Register(new PlayerChargeState(this, _machine));
            _machine.Register(new PlayerHurtState(this, _machine));
            _machine.Register(new PlayerDeathState(this, _machine));
            _machine.SetInitial<PlayerIdleState>();
        }

        private void Update()
        {
            ReadInput();
            UpdateTimers();
            UpdateInvincibility();
            _machine.Tick();
        }

        private void FixedUpdate()
        {
            UpdateGrounded();
            UpdateGravity();
            _machine.FixedTick();
            ClampFallSpeed();
        }

        // ===== 输入 =====

        private void ReadInput()
        {
            if (InputLocked || IsDead)
            {
                HorizontalInput = 0f;
                JumpPressedThisFrame = false;
                JumpReleasedThisFrame = false;
                return;
            }

            HorizontalInput = Input.GetAxisRaw(AxisHorizontal);

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
        }

        private void UpdateTimers()
        {
            // 土狼时间
            if (IsGrounded)
            {
                _coyoteCounter = _config.CoyoteTime;
                _canJump = true;
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
        }

        // ===== 物理能力 =====

        /// <summary>能否起跳（土狼窗口内 + 未消耗本次跳跃）。成功后消耗窗口。</summary>
        public bool TryConsumeJump()
        {
            bool allowed = _coyoteCounter > 0f && _canJump;
            if (allowed)
            {
                _coyoteCounter = 0f;
                _canJump = false;
                _jumpBufferCounter = 0f;
                Body.velocity = new Vector2(Body.velocity.x, _config.JumpVelocity);
            }

            return allowed;
        }

        /// <summary>跳跃缓冲是否有效（触地瞬间自动起跳用）。</summary>
        public bool HasJumpBuffer => _jumpBufferCounter > 0f;

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
            Vector2 origin = (Vector2)transform.position + Vector2.down * (bounds.extents.y - 0.02f);
            IsGrounded = Physics2D.Raycast(origin, Vector2.down,
                _config.GroundCheckSkin + 0.02f, _config.GroundLayer);
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

        /// <summary>触发一次性动画（受击/死亡/攻击/互动），驱动器缺失时静默。</summary>
        public void TryFireAnim(int triggerHash)
        {
            Anim?.FireOneShot(triggerHash);
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
