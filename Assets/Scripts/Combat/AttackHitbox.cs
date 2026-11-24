using System.Collections.Generic;
using UnityEngine;
using StarEco.Eco;
using StarEco.Framework;
using StarEco.Player;

namespace StarEco.Combat
{
    /// <summary>
    /// 攻击命中盒（T6）：挂在 Player 根（Animation Event 直接调用本组件方法）。
    /// 开窗期启用 trigger 碰撞体，OnTriggerEnter2D 对 IHittable 去重命中；
    /// 同一目标在单次开窗内只结算一次（R3）。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class AttackHitbox : MonoBehaviour
    {
        [Tooltip("攻击 trigger 碰撞体（允许挂在 Player 子物体上）")]
        [SerializeField] private Collider2D _hitbox;
        [SerializeField, Min(0f)] private float _groundSwingReach = .22f;

        [Tooltip("命中火花特效（§AnimationVFXSpec §8.3：命中瞬间对准受击点）")]
        [SerializeField] private FxOneShot _fxHitPrefab;
        [SerializeField] private FxOneShot _fxAirKickImpact;

        internal bool HasAirKickImpact => _fxAirKickImpact != null && PoolManager.Instance != null;

        internal void PlayAirKickImpact(Vector2 point, float facing)
        {
            var fx = FxOneShot.Play(_fxAirKickImpact, point, FxFrontOrder);
            if (fx == null) return;
            fx.transform.rotation = Quaternion.identity;
            fx.transform.localScale = new Vector3(facing, 1f, 1f);
        }

        /// <summary>§5.2：斩击/命中属「特效（前）」= 21。</summary>
        private const int FxFrontOrder = 21;

        private readonly HashSet<IHittable> _hitThisWindow = new HashSet<IHittable>();
        private PlayerController _player;
        private PlayerCombat _combat;
        private Player3DGameplayVisual _visual;
        private bool _windowOpen;
        private bool _pendingFirstSwing;
        private Vector3 _hitboxLocalPosition;
        private Vector2 _originalBoxSize;

        /// <summary>当前攻击段号：由 PlayerAttackState 在 Enter/衔接/退出时写入（第三段命中附击退）。</summary>
        public int CurrentSegment { get; set; }

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _combat = GetComponent<PlayerCombat>();
            _visual = GetComponent<Player3DGameplayVisual>();
            if (_hitbox == null)
            {
                foreach (var col in GetComponentsInChildren<Collider2D>(true))
                {
                    if (col != null && col.isTrigger)
                    {
                        _hitbox = col;
                        break;
                    }
                }
            }

            if (_hitbox != null)
            {
                if (_hitbox is BoxCollider2D box) _originalBoxSize = box.size;
                _hitboxLocalPosition = _hitbox.transform.localPosition;
                _hitboxLocalPosition.x = Mathf.Abs(_hitboxLocalPosition.x);
                _hitbox.enabled = false;
                MirrorHitbox();
            }
        }

        private void LateUpdate()
        {
            if (_pendingFirstSwing && CurrentSegment == 1 && _visual.CanOpenFirstSwingHitbox())
                OpenWindow();
            MirrorHitbox();
        }

        // ===== Animation Event 入口（帧号见排期表「动画事件帧」列）=====

        /// <summary>开窗：HitOn 事件帧调用。</summary>
        public void OnAttackWindowOpen()
        {
            _pendingFirstSwing = false;
            // 第三段是投掷：近身判定整段不开窗，伤害全部由飞出去的钳子（ToolProjectile）结算。
            // 动画事件在进入第三段时仍会照常触发，这里按段号拦掉。
            if (CurrentSegment == 3)
            {
                _windowOpen = false;
                _hitThisWindow.Clear();
                if (_hitbox != null)
                {
                    _hitbox.enabled = false;
                }
                return;
            }

            if (CurrentSegment == 1 && _visual != null && !_visual.CanOpenFirstSwingHitbox())
            {
                _pendingFirstSwing = true;
                return;
            }
            OpenWindow();
        }

        private void OpenWindow()
        {
            _pendingFirstSwing = false;
            _hitThisWindow.Clear();
            _windowOpen = true;
            if (_hitbox != null)
            {
                MirrorHitbox();
                _hitbox.enabled = true;
            }

        }

        /// <summary>关窗：HitOff 事件帧调用。</summary>
        public void OnAttackWindowClose()
        {
            _pendingFirstSwing = false;
            _windowOpen = false;
            _hitThisWindow.Clear();
            if (_hitbox != null)
            {
                _hitbox.enabled = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_windowOpen)
            {
                return;
            }

            // 同一目标本窗只命中一次
            IHittable target = other.GetComponentInParent<IHittable>();
            if (target == null || _hitThisWindow.Contains(target))
            {
                return;
            }

            _hitThisWindow.Add(target);

            int dmg = _combat != null && _combat.Config != null ? _combat.Config.AttackDamage : 1;
            float thirdKnock = _combat != null && _combat.Config != null ? _combat.Config.ThirdHitKnockback : 0f;

            var info = new DamageInfo(dmg, FacingDirection(), gameObject)
            {
                KnockbackPower = CurrentSegment == 3 ? thirdKnock : 0f,
                IsSprayerSwing = CurrentSegment == 1 || CurrentSegment == 2
            }.WithPoint(HitPoint(other));

            target.TakeDamage(info);

            // 命中手感：短顿帧 + 轻震屏（第三段收尾更重一点）
            bool heavy = CurrentSegment == 3;
            HitFeel.Play(heavy ? 0.075f : 0.05f, heavy ? 0.10f : 0.055f);
        }

        private void MirrorHitbox()
        {
            if (_hitbox == null || _player == null || _player.Sprite == null)
            {
                return;
            }

            float facing = _player.Sprite.flipX ? -1f : 1f;
            var pos = _hitbox.transform.localPosition;
            bool groundSwing = _windowOpen && _player.IsGrounded && (CurrentSegment == 1 || CurrentSegment == 2);
            float reach = groundSwing ? _groundSwingReach : 0f;
            if (_hitbox is BoxCollider2D box)
            {
                float extension = groundSwing && _visual != null && _visual.HasSprayVisual
                    ? .235f * (Player3DGameplayVisual.SwingSprayerLengthMultiplier - 1f) : 0f;
                box.size = _originalBoxSize + Vector2.right * extension;
                reach += extension * .5f;
            }
            pos.x = facing * (_hitboxLocalPosition.x + reach);
            pos.y = _hitboxLocalPosition.y;
            _hitbox.transform.localPosition = pos;
        }

        /// <summary>伤害来源方向（受击者 → 来源）：与攻击者面朝方向相反，击退按其反方向施加即远离攻击者。</summary>
        private Vector2 FacingDirection()
        {
            return _player != null && _player.Sprite.flipX ? Vector2.right : Vector2.left;
        }

        /// <summary>
        /// 命中点：取被击中碰撞体上离命中盒最近的那个接触点（最贴近"真正被打中的位置"）。
        /// 取不到时回退到受击物体自身位置。
        /// </summary>
        private Vector2 HitPoint(Collider2D other)
        {
            if (other == null)
            {
                return Vector2.zero;
            }

            if (_hitbox != null)
            {
                Vector2 closest = _hitbox.ClosestPoint(other.bounds.center);
                // 接触点距离碰撞体中心过远时说明没真正相交，用碰撞体中心更稳
                if (Vector2.Distance(closest, other.bounds.center) <= other.bounds.extents.magnitude + 0.1f)
                {
                    return closest;
                }
            }

            return other.bounds.center;
        }
    }
}
