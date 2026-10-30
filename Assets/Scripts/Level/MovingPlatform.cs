using UnityEngine;
using StarEco.Player;

namespace StarEco.Level
{
    /// <summary>
    /// 左右往返的移动平台（PlatformC 用）。
    ///
    /// 与 <see cref="CrumblePlatform"/> 的分工：这个是「渡船」，靠位移把玩家送过跳不过去的大空档；
    /// 那个是「陷阱」，踩上就碎。
    ///
    /// 三个实现要点：
    /// 1) <b>行程用世界 X 边界</b>（_minX / _maxX）而不是「振幅」——这一关的两端是有明确语义的
    ///    （左端接 PlatformL1 的起跳点，右端贴 SecondFloorRightWall 的落点），写死边界最不容易被误解。
    /// 2) <b>必须显式带人</b>。玩家脚底用的是无摩擦材质（PlayerFrictionless），靠摩擦带不动，
    ///    所以每步把位移量同样加到站在平台上的玩家身上，否则平台一动玩家就在原地滑走。
    /// 3) 在 FixedUpdate 里推进，位移量按「本步实际走了多少」传给乘客，速度快慢都不会漂移。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class MovingPlatform : MonoBehaviour
    {
        [Header("行程（世界坐标 X；两端按关卡结构定，不要随手改）")]
        [Tooltip("左端 X：PlatformC 取 -10.2，使左缘距 PlatformL1 右缘 0.6u，可从 L1 直接起跳上台")]
        [SerializeField] private float _minX = -10.2f;

        [Tooltip("右端 X：PlatformC 取 -1.7，使右缘距 SecondFloorRightWall（x=-0.5）留 0.2u 余量")]
        [SerializeField] private float _maxX = -1.7f;

        [Header("运动参数")]
        [Tooltip("移动速度（u/s）。玩家跑速 6，取 2 约为 1/3 跑速，站上去不会被甩")]
        [SerializeField] private float _speed = 2f;

        [Tooltip("到达端点后的停顿（秒），留给玩家上下平台")]
        [SerializeField] private float _pauseAtEnds = 0.35f;

        [Tooltip("开局是否直接贴到左端（玩家从左平台过来时正好接上）")]
        [SerializeField] private bool _startAtMin = true;

        [Tooltip("是否带动站在上面的玩家")]
        [SerializeField] private bool _carryRiders = true;

        private Collider2D _collider;
        private PlayerController _player;
        private int _direction = 1;         // +1 向右，-1 向左
        private float _pauseTimer;
        private float _progress;            // 0=左端 1=右端，便于调试与外部读取

        /// <summary>当前是否停在端点（含停顿）。</summary>
        public bool IsPaused => _pauseTimer > 0f;

        /// <summary>归一化位置：0 = 左端，1 = 右端。</summary>
        public float Progress => _progress;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();

            if (_minX > _maxX)
            {
                float t = _minX;
                _minX = _maxX;
                _maxX = t;
            }

            if (_startAtMin)
            {
                SetX(_minX);
                _direction = 1;
            }
            else
            {
                SetX(_maxX);
                _direction = -1;
            }

            _progress = Mathf.InverseLerp(_minX, _maxX, transform.position.x);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f)
            {
                return;
            }

            if (_pauseTimer > 0f)
            {
                _pauseTimer -= dt;
                return;
            }

            float targetX = _direction > 0 ? _maxX : _minX;
            float newX = Mathf.MoveTowards(transform.position.x, targetX, _speed * dt);
            float dx = newX - transform.position.x;

            if (Mathf.Abs(dx) > 0f)
            {
                var delta = new Vector3(dx, 0f, 0f);
                transform.position += delta;

                if (_carryRiders)
                {
                    CarryRiders(delta);
                }
            }

            _progress = Mathf.InverseLerp(_minX, _maxX, newX);

            // 到端点：反向 + 停顿
            if (Mathf.Abs(newX - targetX) <= 0.0001f)
            {
                _direction = -_direction;
                _pauseTimer = _pauseAtEnds;
            }
        }

        /// <summary>
        /// 把本步位移同步给站在平台顶面上的玩家。
        /// 判定用包围盒几何（脚底在顶面附近 + 水平有实质重叠），与 CrumblePlatform 同一套思路。
        /// </summary>
        private void CarryRiders(Vector3 delta)
        {
            if (_player == null)
            {
                _player = FindObjectOfType<PlayerController>();
            }

            if (_player == null || _player.IsDead || !_player.IsGrounded)
            {
                return;
            }

            var playerCollider = _player.GetComponent<Collider2D>();
            if (playerCollider == null || _collider == null)
            {
                return;
            }

            Bounds pb = playerCollider.bounds;
            Bounds mb = _collider.bounds;

            if (pb.min.y < mb.max.y - 0.3f)
            {
                return;
            }

            if (pb.max.x <= mb.min.x + 0.05f || pb.min.x >= mb.max.x - 0.05f)
            {
                return;
            }

            var body = _player.Body;
            if (body != null)
            {
                // 改 position 而不是 velocity：不干扰玩家自己的水平速度控制
                body.position += (Vector2)delta;
            }
            else
            {
                _player.transform.position += delta;
            }
        }

        private void SetX(float x)
        {
            var p = transform.position;
            p.x = x;
            transform.position = p;
        }

        private void OnDrawGizmosSelected()
        {
            float y = transform.position.y;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
            Gizmos.DrawLine(new Vector3(_minX, y, 0f), new Vector3(_maxX, y, 0f));
            Gizmos.DrawWireSphere(new Vector3(_minX, y, 0f), 0.12f);
            Gizmos.DrawWireSphere(new Vector3(_maxX, y, 0f), 0.12f);
        }
    }
}
