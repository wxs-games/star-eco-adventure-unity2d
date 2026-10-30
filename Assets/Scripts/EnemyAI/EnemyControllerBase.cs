using UnityEngine;
using StarEco.Combat;
using StarEco.Eco;
using StarEco.Framework;
using StarEco.EnemyAI.Events;
using StarEco.UI;

namespace StarEco.EnemyAI
{
    /// <summary>
    /// 敌人控制器基类：感知/HP/受击/死亡回收/事件广播。
    /// 实现 IHittable 接入 M3 命中流水线，不引用 StarEco.Player 具体类型。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public abstract class EnemyControllerBase : MonoBehaviour, IHittable
    {
        [SerializeField] protected EnemyConfig _config;
        [SerializeField] protected EnemyControllerBase _sourcePrefab;

        [Tooltip("命中火花特效（§AnimationVFXSpec §8.3：命中瞬间对准受击点，order 21）")]
        [SerializeField] protected FxOneShot _fxHitPrefab;

        [Tooltip("死亡爆炸特效（§AnimationVFXSpec §8.3：OnDieEnd 时播在尸体坐标，order 21）")]
        [SerializeField] protected FxOneShot _fxExplodePrefab;

        public EnemyConfig Config => _config;
        public SpriteRenderer Sprite { get; private set; }
        public EnemyAnimatorDriver Anim { get; private set; }
        public Rigidbody2D Body { get; private set; }
        private EnemyAttackHitbox _attackHitbox;
        protected EnemyStateMachine Machine;

        public int CurrentHP { get; protected set; }
        public bool IsDead { get; protected set; }
        public bool IsSpawnImmune { get; set; }
        public Transform Target { get; private set; }
        public bool HasTarget => Target != null;
        public float AttackCooldownTimer { get; set; }

        protected Vector2 LastHitDirection;
        protected bool _pendingHurt;
        protected float _hurtStunTimer;
        protected Vector2 _knockbackVelocity;
        private float _loseTargetTimer;
        private bool _recycleGuard;
        private bool _pooled;
        private bool _awaken;

        private static readonly Collider2D[] _senseBuffer = new Collider2D[8];

        /// <summary>巡逻原点（出生/复用时的位置），巡逻左右端点以它为圆心。</summary>
        private Vector2 _homePosition;

        /// <summary>放弃目标的截止时间：追击被墙体阻断时短暂不重新锁定，避免来回抖动。</summary>
        private float _ignoreTargetUntil;

        /// <summary>巡逻是否可用（配置开启 + 非固定单位 + 有刚体）。</summary>
        public bool CanPatrol => _config != null && _config.PatrolEnabled && !_config.IsStatic && Body != null;

        public float PatrolLeftX => _homePosition.x - _config.PatrolRadius;
        public float PatrolRightX => _homePosition.x + _config.PatrolRadius;

        /// <summary>初始状态类型（子类重写，如 Enemy03 重写为 Spawn）。</summary>
        protected virtual System.Type InitialStateType => typeof(EnemyIdleState);

        protected virtual void Awake()
        {
            Sprite = GetComponent<SpriteRenderer>();
            Anim = GetComponent<EnemyAnimatorDriver>();
            Body = GetComponent<Rigidbody2D>();
            _attackHitbox = GetComponentInChildren<EnemyAttackHitbox>(true);
            if (_config == null)
            {
                _config = EnemyConfig.CreateDefault();
            }
            CurrentHP = _config.MaxHP;
            _homePosition = transform.position;
            BuildStateMachine();
            _awaken = true;
        }

        protected abstract void BuildStateMachine();

        /// <summary>
        /// 保证状态机可用。域重载（Play 中途改脚本触发）会把所有实例字段清零、
        /// 但 Awake 不会重跑，导致 Machine 为 null 而 Update/FixedUpdate 照常被调用 → 每帧 NRE。
        /// 这里做懒加载自愈：缺失时按子类实现重建并复位初始态。
        /// BuildStateMachine 幂等（每次 new 新实例），可安全重复调用。
        /// </summary>
        protected void EnsureMachine()
        {
            if (Machine != null)
            {
                return;
            }
            BuildStateMachine();
            Machine.SetInitial(InitialStateType);
        }

        protected virtual void OnEnable()
        {
            if (_awaken)
            {
                ResetForReuse();
            }
        }

        protected virtual void Update()
        {
            EnsureMachine();

            // 对话期间敌人整体定住：停移动、停状态机、停感知与计时，
            // 动画由 EnemyAnimatorDriver 按 Body 速度自动切回 Idle。
            if (DialoguePanel.IsDialogueActive)
            {
                StopMoving();
                return;
            }

            if (IsDead)
            {
                Machine.Tick();
                return;
            }
            SenseTarget();
            UpdateTimers();
            Machine.Tick();
        }

        protected virtual void FixedUpdate()
        {
            EnsureMachine();

            if (DialoguePanel.IsDialogueActive)
            {
                Body.velocity = new Vector2(0f, Body.velocity.y);
                return;
            }

            if (IsDead)
            {
                return;
            }
            ApplyKnockback();
            Machine.FixedTick();
        }

        // ===== 感知 =====

        protected virtual void SenseTarget()
        {
            if (IsDead || IsSpawnImmune || IsIgnoringTarget)
            {
                return;
            }

            float radius = HasTarget
                ? _config.PerceptionRadius + _config.LoseTargetBuffer
                : _config.PerceptionRadius;

            int count = Physics2D.OverlapCircleNonAlloc(
                transform.position, radius, _senseBuffer, _config.PlayerLayer);

            Transform best = null;
            float bestDy = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_senseBuffer[i] == null) continue;
                var t = _senseBuffer[i].transform;
                float dy = Mathf.Abs(t.position.y - transform.position.y);
                if (dy <= _config.SameGroundToleranceY && dy < bestDy)
                {
                    best = t;
                    bestDy = dy;
                }
            }

            if (best != null)
            {
                SetTarget(best);
                _loseTargetTimer = 0f;
            }
            else
            {
                _loseTargetTimer += Time.deltaTime;
                if (_loseTargetTimer > _config.LoseTargetSeconds)
                {
                    ClearTarget();
                }
            }
        }

        public void SetTarget(Transform t) { Target = t; }
        public void ClearTarget() { Target = null; _loseTargetTimer = 0f; }

        /// <summary>放弃目标期间是否不重新锁定（见 EnemyChaseState 的受阻回退）。</summary>
        public bool IsIgnoringTarget => Time.time < _ignoreTargetUntil;

        /// <summary>丢目标并在一段时间内不再重新锁定（追击被墙体阻断时调用）。</summary>
        public void IgnoreTargetFor(float seconds)
        {
            ClearTarget();
            if (seconds > 0f)
            {
                _ignoreTargetUntil = Time.time + seconds;
            }
        }

        /// <summary>把巡逻原点重绑到当前站位（池取出后被摆到出生点时由 Spawner 调用）。</summary>
        public void BindHomeToCurrentPosition()
        {
            _homePosition = transform.position;
        }

        // ===== 朝向 =====

        /// <summary>
        /// 美术默认朝向约定：true = 原图正面/炮口朝左（如 Tower01），此时 flipX 语义与常规相反。
        /// 默认 false（原图朝右），子类按素材实际朝向覆写。
        /// </summary>
        protected virtual bool SpriteFacesLeftByDefault => false;

        public void FaceTarget()
        {
            if (Target != null && Sprite != null)
            {
                bool targetOnLeft = Target.position.x < transform.position.x;
                Sprite.flipX = SpriteFacesLeftByDefault ? !targetOnLeft : targetOnLeft;
            }
        }

        public bool IsFacingLeft => Sprite != null
            && (SpriteFacesLeftByDefault ? !Sprite.flipX : Sprite.flipX);
        public Vector2 FacingDirection => IsFacingLeft ? Vector2.left : Vector2.right;

        // ===== 移动 =====

        public virtual void MoveTowardsTarget()
        {
            if (Body == null || !HasTarget || _config.IsStatic)
            {
                return;
            }
            FaceTarget();
            Vector2 target = Target.position;
            Vector2 pos = Body.position;
            float dirX = Mathf.Sign(target.x - pos.x);
            if (Mathf.Abs(target.x - pos.x) < 0.01f) return;
            Vector2 next = pos + new Vector2(dirX, 0f) * _config.MoveSpeed * Time.fixedDeltaTime;
            Body.MovePosition(next);
        }

        public virtual void StopMoving()
        {
            if (Body != null)
            {
                Body.velocity = new Vector2(0f, Body.velocity.y);
            }
        }

        /// <summary>沿地面水平移动到指定 x（巡逻用，不依赖目标）。用速度而非 MovePosition，交给物理处理碰撞。</summary>
        public virtual void MoveTowardsX(float worldX)
        {
            if (Body == null || _config.IsStatic)
            {
                return;
            }
            float dx = worldX - Body.position.x;
            if (Mathf.Abs(dx) < 0.01f)
            {
                Body.velocity = new Vector2(0f, Body.velocity.y);
                return;
            }
            Body.velocity = new Vector2(Mathf.Sign(dx) * _config.MoveSpeed, Body.velocity.y);
        }

        /// <summary>按方向设置朝向（巡逻用，不依赖目标）。</summary>
        public void FaceDirection(int dir)
        {
            if (Sprite == null || dir == 0)
            {
                return;
            }
            bool onLeft = dir < 0;
            Sprite.flipX = SpriteFacesLeftByDefault ? !onLeft : onLeft;
        }

        /// <summary>
        /// 前方是否有地面（巡逻防掉崖）：从身体斜前方投一条向下的射线探地面层。
        /// 跳过自身碰撞体（贴图/受击盒会挡住自己的射线）与触发体。
        /// </summary>
        public bool HasGroundAhead(int dir)
        {
            if (Body == null)
            {
                return false;
            }
            Vector3 origin = transform.position + new Vector3(dir * 0.5f, 0.2f, 0f);
            int mask = _config.GroundLayer.value;
            var hits = Physics2D.RaycastAll(origin, Vector2.down, 2.5f, mask);
            for (int i = 0; i < hits.Length; i++)
            {
                var c = hits[i].collider;
                if (c == null || c.isTrigger)
                {
                    continue;
                }
                if (c.transform == transform || c.transform.IsChildOf(transform))
                {
                    continue;
                }
                return true;
            }
            return false;
        }

        // ===== 受击 (IHittable) =====

        /// <summary>受击音效标识（M10）：Boss 覆写为 BossHit，小怪统一 EnemyHit。</summary>
        protected virtual SfxId HitSfxId => SfxId.EnemyHit;

        /// <summary>受击是否产生硬直中断：Boss 在技能前摇/命中阶段覆写为 false（霸体）。</summary>
        protected virtual bool CanBeHurtInterrupted => true;

        public virtual void TakeDamage(DamageInfo info)
        {
            if (IsDead || IsSpawnImmune)
            {
                return;
            }
            CurrentHP = Mathf.Max(0, CurrentHP - info.Amount);
            LastHitDirection = info.SourceDirection;

            // 霸体期间只吃伤害，不进受击态、不产生硬直
            bool interrupted = CanBeHurtInterrupted;
            _pendingHurt = interrupted;
            _hurtStunTimer = interrupted ? _config.HurtStunSeconds : 0f;
            AudioManager.Instance?.PlaySfx(HitSfxId, 0.05f);   // M10：受击音效（Boss 覆写为 BossHit）

            if (_config.KnockbackResponse > 0f && info.KnockbackPower > 0f && Body != null)
            {
                Vector2 dir = info.SourceDirection.sqrMagnitude > 0.0001f
                    ? -info.SourceDirection.normalized
                    : FacingDirection;
                _knockbackVelocity = dir * info.KnockbackPower * _config.KnockbackResponse;
            }

            if (CurrentHP <= 0)
            {
                IsDead = true;
            }

            // 表现层（小怪血条等）据此刷新，扣完血再广播，订阅者读到的是结算后的数值
            EventCenter.Publish(new EnemyDamagedEvent
            {
                Enemy = this,
                CurrentHP = CurrentHP,
                MaxHP = _config != null ? _config.MaxHP : CurrentHP,
                Amount = info.Amount
            });

            // 受击闪白：与血条同一帧触发，命中反馈更"跟手"
            HitFlash.Play(Sprite);

            // 命中火花：§AnimationVFXSpec §8.3「IHittable.TakeDamage 成功后」播在受击点。
            // 玩家受击同样走这条链路（PlayerCombat 也实现 IHittable），此处只管敌人侧。
            PlayHitFx(info);
        }

        /// <summary>§5.2：命中火花属「特效（前）」= 21。</summary>
        private const int FxFrontOrder = 21;

        /// <summary>命中火花：播在受击点（未提供命中点时用自身位置）。</summary>
        private void PlayHitFx(DamageInfo info)
        {
            if (_fxHitPrefab == null)
            {
                return;
            }

            Vector3 pos = info.HasPoint
                ? new Vector3(info.Point.x, info.Point.y, transform.position.z)
                : transform.position;

            FxOneShot.Play(_fxHitPrefab, pos, FxFrontOrder);
        }

        public bool ConsumePendingHurt()
        {
            if (_pendingHurt)
            {
                _pendingHurt = false;
                return true;
            }
            return false;
        }

        public Vector2 ConsumeKnockback()
        {
            Vector2 v = _knockbackVelocity;
            _knockbackVelocity = Vector2.zero;
            return v;
        }

        // ===== 计时器 =====

        private void UpdateTimers()
        {
            if (_hurtStunTimer > 0f)
            {
                _hurtStunTimer -= Time.deltaTime;
            }
            if (AttackCooldownTimer > 0f)
            {
                AttackCooldownTimer -= Time.deltaTime;
            }
        }

        public bool IsStunned => _hurtStunTimer > 0f;
        public float HurtStunTimer => _hurtStunTimer;

        private void ApplyKnockback()
        {
            if (Body == null || _knockbackVelocity.sqrMagnitude < 0.0001f)
            {
                return;
            }
            Body.velocity = new Vector2(_knockbackVelocity.x, Body.velocity.y);
            _knockbackVelocity = Vector2.zero;
        }

        // ===== 距离工具 =====

        public float HorizontalDistanceToTarget =>
            HasTarget ? Mathf.Abs(Target.position.x - transform.position.x) : float.MaxValue;

        public bool InAttackRange => HorizontalDistanceToTarget <= _config.AttackRange;

        // ===== 死亡 / 回收 / 广播 =====

        /// <summary>Animation Event: HitOn — 转发到子 AttackHitbox（动画事件仅达 Animator 所在物体的组件）。</summary>
        public void OnAttackWindowOpen()
        {
            _attackHitbox?.OnAttackWindowOpen();
        }

        /// <summary>Animation Event: HitOff — 转发到子 AttackHitbox。</summary>
        public void OnAttackWindowClose()
        {
            _attackHitbox?.OnAttackWindowClose();
        }

        /// <summary>Animation Event: DieEnd — 死亡回收 + 事件广播（Boss 覆写以插入击败演出）。</summary>
        public virtual void OnDieEnd()
        {
            // 死亡爆炸：§AnimationVFXSpec §8.3「Enemy03.Die / BossController.Die 的 OnDieEnd」播在尸体坐标。
            // 必须在 Recycle 之前播——回收后 transform 会被池重置。
            if (_fxExplodePrefab != null)
            {
                FxOneShot.Play(_fxExplodePrefab, transform.position, FxFrontOrder);
            }

            BroadcastDeath();
            Recycle();
        }

        public void BroadcastDeath()
        {
            if (_recycleGuard)
            {
                return;
            }
            EventCenter.Publish(new EnemyDeathEvent
            {
                EnemyTypeId = _config.EnemyTypeId,
                Position = transform.position,
                DropTable = _config.DropTable
            });
        }

        public virtual void Recycle()
        {
            if (_recycleGuard)
            {
                return;
            }
            _recycleGuard = true;

            // 仅池借出的实例走 Release（M3 PoolManager 对未建池 prefab 会 Destroy、
            // 对非本池借出对象会忽略，两种都不符合"场景实例隐藏保留"语义）。
            if (_pooled && _sourcePrefab != null && PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(_sourcePrefab, this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>标记为池借出实例（Spawner 取出后调用，归还走 PoolManager）。</summary>
        public void MarkPooled()
        {
            _pooled = true;
        }

        /// <summary>池取出后复位（不复用上一生命周期状态）。</summary>
        public virtual void ResetForReuse()
        {
            CurrentHP = _config.MaxHP;
            IsDead = false;
            IsSpawnImmune = false;
            _pendingHurt = false;
            _hurtStunTimer = 0f;
            _knockbackVelocity = Vector2.zero;
            AttackCooldownTimer = 0f;
            _loseTargetTimer = 0f;
            _recycleGuard = false;
            _ignoreTargetUntil = 0f;
            Target = null;
            _homePosition = transform.position;   // 池复用对象可能被摆到别处，巡逻原点随之更新

            if (Body != null)
            {
                Body.velocity = Vector2.zero;
            }
            if (Sprite != null)
            {
                Sprite.flipX = false;
            }
            Anim?.ResetParams();
            EnsureMachine();
            Machine.SetInitial(InitialStateType);
        }

        public void TryFireAnim(int triggerHash)
        {
            Anim?.FireOneShot(triggerHash);
        }

        /// <summary>受击硬直结束后决定回哪个状态。</summary>
        public virtual System.Type DecidePostHurtState()
        {
            if (HasTarget && !_config.IsStatic)
            {
                return typeof(EnemyChaseState);
            }
            return typeof(EnemyIdleState);
        }
    }
}
