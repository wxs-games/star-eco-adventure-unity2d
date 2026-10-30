using UnityEngine;
using StarEco.Combat;
using StarEco.Framework;
using StarEco.Eco.Events;

namespace StarEco.Level
{
    /// <summary>
    /// 地刺伤害区：踩进刺阵即受重伤（默认一击致命），与坠落区同构，
    /// 走既有 IHittable 伤害流水线，不另开死亡逻辑。死因不写成「跌落山崖」。
    /// 用 Stay 持续结算：受击无敌期结束后立即再次结算，无法靠无敌硬闯过去。
    ///
    /// 可伸缩：监听 GarbagePurifiedEvent，当 _retractOnTargetId 对应的污染源被净化后，
    /// 整组地刺平滑沉入地面（位移 = 自身高度）并关闭伤害判定，通路打开。
    /// 因此「先爬台阶 → 净化台阶上的污染草丛 → 地刺收回 → 从下方通行」的关卡节奏成立。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class SpikeHazard : MonoBehaviour
    {
        [Tooltip("每次结算伤害（半心点：2 点 = 1 颗心）")]
        [SerializeField] private int _damage = 2;

        [Tooltip("被净化后收起的触发源 TargetId（与 GarbagePile 的 _targetId 一致）；留空则永不收起")]
        [SerializeField] private string _retractOnTargetId = "";

        [Tooltip("收起动画时长（秒）")]
        [SerializeField] private float _retractDuration = 0.35f;

        [Tooltip("收起后沉入地面的额外深度（0 = 正好沉到看不见）")]
        [SerializeField] private float _retractExtraDepth = 0.2f;

        private Collider2D _collider;
        private SpriteRenderer _sprite;
        private Vector3 _raisedPosition;
        private bool _retracted;
        private bool _retracting;
        private float _retractT;

        public bool IsRetracted => _retracted;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            _sprite = GetComponent<SpriteRenderer>();
            _raisedPosition = transform.position;
        }

        private void OnEnable()
        {
            EventCenter.Subscribe<GarbagePurifiedEvent>(OnGarbagePurified);
        }

        private void OnDisable()
        {
            EventCenter.Unsubscribe<GarbagePurifiedEvent>(OnGarbagePurified);
        }

        private void OnGarbagePurified(GarbagePurifiedEvent e)
        {
            if (_retracted || _retracting || string.IsNullOrEmpty(_retractOnTargetId))
            {
                return;
            }
            if (e.TargetId != _retractOnTargetId)
            {
                return;
            }

            StartRetract();
        }

        /// <summary>开始收起（也可被关卡脚本/演出直接调用）。</summary>
        public void StartRetract()
        {
            if (_retracted || _retracting)
            {
                return;
            }
            _retracting = true;
            _retractT = 0f;
        }

        private void Update()
        {
            if (!_retracting)
            {
                return;
            }

            _retractT += Time.deltaTime;
            float t = _retractDuration > 0f ? Mathf.Clamp01(_retractT / _retractDuration) : 1f;

            float depth = ResolveRetractDepth() * t;
            transform.position = _raisedPosition + Vector3.down * depth;

            if (t >= 1f)
            {
                _retracting = false;
                _retracted = true;
                // 完全沉入后关闭伤害判定，避免"看不见但还在扎人"
                if (_collider != null)
                {
                    _collider.enabled = false;
                }
                if (_sprite != null)
                {
                    _sprite.enabled = false;
                }
            }
        }

        /// <summary>收起位移量：自身视觉高度 + 额外深度，保证完全没入地面。</summary>
        private float ResolveRetractDepth()
        {
            float height = 0.8f;
            if (_sprite != null && _sprite.sprite != null)
            {
                height = _sprite.bounds.size.y;
            }
            else if (_collider != null)
            {
                height = _collider.bounds.size.y;
            }
            return height + _retractExtraDepth;
        }

        private void Apply(Collider2D other)
        {
            if (_retracted)
            {
                return;
            }

            var hittable = other.GetComponentInParent<IHittable>();
            if (hittable == null)
            {
                return;
            }

            // 伤害来源方向取自"受击者 → 刺阵中心"，让击退把人往外弹出去而不是往下压。
            Vector2 px = other.bounds.center;
            Vector2 cx = _collider != null ? (Vector2)_collider.bounds.center : (Vector2)transform.position;
            float dx = cx.x - px.x;
            Vector2 dir = Mathf.Abs(dx) > 0.05f ? new Vector2(Mathf.Sign(dx), 0f) : Vector2.up;

            hittable.TakeDamage(new DamageInfo(_damage, dir, gameObject));
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Apply(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            Apply(other);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.35f);
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                Gizmos.DrawCube(transform.position, col.bounds.size);
            }
        }
    }
}
