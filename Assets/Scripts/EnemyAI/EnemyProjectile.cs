using UnityEngine;
using StarEco.Combat;
using StarEco.Framework;

namespace StarEco.EnemyAI
{
    /// <summary>
    /// 毒气弹体：经对象池复用，命中玩家结算一次伤害 + fx_gas，触地/超程归还。
    /// 位移只在 FixedUpdate（性能红线）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyProjectile : MonoBehaviour
    {
        private Rigidbody2D _body;
        private SpriteRenderer _sprite;

        private EnemyProjectile _sourcePrefab;
        private Vector2 _direction;
        private float _speed;
        private float _maxRange;
        private int _damage;
        private float _knockback;
        private GameObject _source;
        private int _ownerLayer;

        private float _traveledDistance;
        private bool _resolved;
        private bool _launched;

        /// <summary>弹体发射参数。</summary>
        public struct ProjectileParams
        {
            public EnemyProjectile Prefab;
            public Vector2 Direction;
            public float Speed;
            public float MaxRange;
            public int Damage;
            public float Knockback;
            public GameObject Source;
            public int OwnerLayer;
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _sprite = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            _traveledDistance = 0f;
            _resolved = false;
            _launched = false;
        }

        /// <summary>发射弹体（池取出后调用）。</summary>
        public void Launch(ProjectileParams p)
        {
            _sourcePrefab = p.Prefab;
            _direction = p.Direction.sqrMagnitude > 0.0001f ? p.Direction.normalized : Vector2.right;
            _speed = p.Speed;
            _maxRange = p.MaxRange;
            _damage = p.Damage;
            _knockback = p.Knockback;
            _source = p.Source;
            _ownerLayer = p.OwnerLayer;
            _traveledDistance = 0f;
            _resolved = false;
            _launched = true;

            if (_sprite != null)
            {
                _sprite.flipX = _direction.x < 0f;
            }
        }

        private void FixedUpdate()
        {
            if (!_launched || _resolved)
            {
                return;
            }

            Vector2 next = _body.position + _direction * _speed * Time.fixedDeltaTime;
            _body.MovePosition(next);
            _traveledDistance += _speed * Time.fixedDeltaTime;

            if (_traveledDistance >= _maxRange)
            {
                ResolveWithoutHit();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_launched || _resolved)
            {
                return;
            }

            if (_source != null && other.transform.root == _source.transform.root)
            {
                return;
            }
            if (other.gameObject.layer == _ownerLayer)
            {
                return;
            }

            var hittable = other.GetComponentInParent<IHittable>();
            if (hittable != null)
            {
                ResolveWithHit(hittable);
                return;
            }

            ResolveWithoutHit();
        }

        private void ResolveWithHit(IHittable target)
        {
            _resolved = true;
            var dmg = new DamageInfo(_damage, -_direction, _source)
            {
                KnockbackPower = _knockback
            };
            target.TakeDamage(dmg);
            ReturnToPool();
        }

        private void ResolveWithoutHit()
        {
            _resolved = true;
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (_sourcePrefab != null && PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(_sourcePrefab, this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
