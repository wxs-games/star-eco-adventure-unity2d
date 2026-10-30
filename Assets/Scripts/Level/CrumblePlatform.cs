using UnityEngine;
using StarEco.Eco;
using StarEco.Framework;
using StarEco.Player;

namespace StarEco.Level
{
    /// <summary>碎裂平台的状态机：静置 → 预警抖动 → 碎裂 → 复原。</summary>
    public enum CrumbleState
    {
        Idle,
        Shaking,
        Broken,
        Reforming,
    }

    /// <summary>
    /// 一踩就碎的木板平台。
    ///
    /// 节奏取自同类 2D 平台跳跃的主流做法（Celeste 的 Crumble Block、马里奥的下坠平台）：
    /// 玩家踩上后先抖动预警，约 <b>0.5s</b> 后碎掉；这 0.5s 是留给玩家起跳的窗口，
    /// 计时一旦开始就<b>不会因为玩家离开而重置</b>——所以「及时走开就安全，犹豫就掉下去」。
    ///
    /// 碎裂表现不引入新美术：把平台自己的精灵按纹理矩形竖切成若干条，各自按抛物线抛出，
    /// 复用 <see cref="BreakableDebris"/> 的物理 + 淡出，再播一次重物断裂音。
    ///
    /// 复原：默认 3 秒后原地淡入还原（碰撞体先开，视觉淡入可容忍半透明站立），
    /// 设成 0 则本局永久消失。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class CrumblePlatform : MonoBehaviour
    {
        [Header("碎裂节奏")]
        [Tooltip("踩上后多久碎裂（秒）。社区主流 0.4~0.6s，取 0.5s")]
        [SerializeField] private float _breakDelay = 0.5f;

        [Tooltip("预警抖动幅度（世界单位）；平台宽 2u，0.045 肉眼明显但不夸张")]
        [SerializeField] private float _shakeAmplitude = 0.045f;

        [Tooltip("抖动频率（Hz）")]
        [SerializeField] private float _shakeFrequency = 26f;

        [Header("碎裂表现")]
        [Tooltip("竖切碎块数量（按平台精灵纹理会自动等分）")]
        [SerializeField] private int _chunkCount = 4;

        [Tooltip("碎块存活时长（含淡出）")]
        [SerializeField] private float _chunkLifetime = 0.95f;

        [Tooltip("碎块重力（与 BreakableDebris 同量级，17 接近道具碎裂手感）")]
        [SerializeField] private float _chunkGravity = 17f;

        [Tooltip("碎块横向抛散速度上限")]
        [SerializeField] private float _chunkSpreadX = 1.5f;

        [Tooltip("碎块起跳初速")]
        [SerializeField] private float _chunkUpward = 1.2f;

        [Tooltip("碎裂音（暂复用石头碎裂；有木板断裂音后改这里）")]
        [SerializeField] private SfxId _breakSfx = SfxId.BreakStone;

        [Header("复原")]
        [Tooltip("碎裂后多久长回来（秒）；<= 0 表示本局内永久消失")]
        [SerializeField] private float _respawnDelay = 3f;

        [Tooltip("复原淡入时长")]
        [SerializeField] private float _respawnFade = 0.18f;

        private Collider2D _collider;
        private SpriteRenderer _renderer;
        private Vector3 _basePosition;
        private Color _baseColor;
        private CrumbleState _state = CrumbleState.Idle;
        private float _timer;
        private float _shakePhase;

        /// <summary>当前状态，供调试与验收脚本读取。</summary>
        public CrumbleState State => _state;

        /// <summary>本平台从「踩上」到「碎裂」的窗口时长（秒）。</summary>
        public float BreakDelay => _breakDelay;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            _renderer = GetComponent<SpriteRenderer>();
            _basePosition = transform.position;
            _baseColor = _renderer != null ? _renderer.color : Color.white;
        }

        private void OnEnable()
        {
            // 关卡重开时对象可能被复用，回到初始形态
            if (_state == CrumbleState.Broken || _state == CrumbleState.Shaking)
            {
                RestoreVisual();
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryTrigger(collision.collider);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            // 单向平台靠 effector 生效，摩擦接触不一定每帧都报 Enter，
            // 用 Stay 兜底，避免「站上去却没触发」。
            TryTrigger(collision.collider);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            switch (_state)
            {
                case CrumbleState.Shaking:
                    TickShaking(dt);
                    break;
                case CrumbleState.Broken:
                    TickBroken(dt);
                    break;
                case CrumbleState.Reforming:
                    TickReforming(dt);
                    break;
            }
        }

        // ——————————————————————————— 触发 ———————————————————————————

        private void TryTrigger(Collider2D other)
        {
            if (_state != CrumbleState.Idle)
            {
                return;
            }

            if (!IsPlayerStandingOnTop(other))
            {
                return;
            }

            _state = CrumbleState.Shaking;
            _timer = 0f;
            _shakePhase = 0f;
        }

        /// <summary>
        /// 只认「玩家脚底踩在平台顶面上」这一种接触：
        /// 用包围盒几何判定，不依赖 ContactPoint2D.normal 的方向约定（2D 里各版本易踩坑）。
        /// </summary>
        private bool IsPlayerStandingOnTop(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.IsDead || !player.IsGrounded)
            {
                return false;
            }

            Bounds pb = other.bounds;
            Bounds mb = _collider.bounds;

            // 脚底必须落在顶面附近（容差 0.3u），且身体主体在顶面之上
            if (pb.min.y < mb.max.y - 0.3f)
            {
                return false;
            }

            // 水平方向必须有实质重叠，排除只是擦到边角
            return pb.max.x > mb.min.x + 0.05f && pb.min.x < mb.max.x - 0.05f;
        }

        // ——————————————————————————— 各状态 ———————————————————————————

        private void TickShaking(float dt)
        {
            _timer += dt;

            float t = Mathf.Clamp01(_timer / Mathf.Max(0.01f, _breakDelay));
            float amp = Mathf.Lerp(_shakeAmplitude * 0.35f, _shakeAmplitude, t);

            _shakePhase += dt * _shakeFrequency * Mathf.PI * 2f;
            float ox = Mathf.Sin(_shakePhase) * amp;
            float oy = -Mathf.Abs(Mathf.Sin(_shakePhase * 1.7f)) * amp * 0.65f;

            transform.position = _basePosition + new Vector3(ox, oy, 0f);

            if (_timer >= _breakDelay)
            {
                Break();
            }
        }

        private void TickBroken(float dt)
        {
            if (_respawnDelay <= 0f)
            {
                return;
            }

            _timer += dt;
            if (_timer >= _respawnDelay)
            {
                BeginReform();
            }
        }

        private void TickReforming(float dt)
        {
            _timer += dt;

            float t = Mathf.Clamp01(_timer / Mathf.Max(0.01f, _respawnFade));
            var c = _baseColor;
            c.a = Mathf.Lerp(0f, _baseColor.a, t);
            if (_renderer != null)
            {
                _renderer.color = c;
            }

            if (t >= 1f)
            {
                _state = CrumbleState.Idle;
                RestoreVisual();
            }
        }

        // ——————————————————————————— 碎裂 / 复原 ———————————————————————————

        private void Break()
        {
            // 先把精灵与包围盒取出来，再关渲染
            var sprite = _renderer != null ? _renderer.sprite : null;
            Bounds bounds = _renderer != null
                ? _renderer.bounds
                : new Bounds(_basePosition, Vector3.one);

            transform.position = _basePosition;
            _collider.enabled = false;
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }

            SpawnChunks(sprite, bounds);

            if (AudioManager.Instance != null)
            {
                // 第二参数是音高随机量：连续踩碎时不至于完全同一声
                AudioManager.Instance.PlaySfx(_breakSfx, 0.12f);
            }

            _state = CrumbleState.Broken;
            _timer = 0f;
        }

        /// <summary>
        /// 按纹理矩形把平台精灵竖切成 N 条，各自抛出。
        /// Sprite.Create 只引用纹理与矩形，不需要纹理开启 Read/Write。
        /// </summary>
        private void SpawnChunks(Sprite sprite, Bounds bounds)
        {
            if (sprite == null || sprite.texture == null || _chunkCount < 2)
            {
                return;
            }

            float floorY = FindFloorY(bounds);
            Rect tr = sprite.textureRect;
            float ppu = sprite.pixelsPerUnit;
            int n = Mathf.Clamp(_chunkCount, 2, 8);
            float chunkWidth = tr.width / n;

            for (int i = 0; i < n; i++)
            {
                var rect = new Rect(tr.x + chunkWidth * i, tr.y, chunkWidth, tr.height);
                var chunkSprite = Sprite.Create(sprite.texture, rect, new Vector2(0.5f, 0.5f), ppu);
                chunkSprite.name = $"{sprite.name}_chunk{i:00}";

                var go = new GameObject($"CrumbleChunk_{name}_{i:00}");
                go.transform.SetParent(transform.parent, false);
                go.transform.position = new Vector3(
                    bounds.min.x + bounds.size.x * (i + 0.5f) / n,
                    bounds.center.y,
                    _basePosition.z);
                go.transform.localScale = transform.lossyScale;
                go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-6f, 6f));

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = chunkSprite;
                if (_renderer != null)
                {
                    sr.sortingLayerID = _renderer.sortingLayerID;
                    sr.sortingOrder = _renderer.sortingOrder + 3;
                }
                sr.color = _baseColor;

                // 从中心向两侧散开：i 居中时横向速度≈0
                float dir = n > 1 ? (i - (n - 1) * 0.5f) / ((n - 1) * 0.5f) : 0f;
                var velocity = new Vector2(
                    dir * _chunkSpreadX * Random.Range(0.55f, 1f),
                    Random.Range(0.5f, 1f) * _chunkUpward);

                var debris = go.AddComponent<BreakableDebris>();
                debris.Launch(velocity, Random.Range(-240f, 240f), _chunkGravity, _chunkLifetime, floorY);
            }
        }

        /// <summary>向下探到真正的地面，碎块才不会停在半空。平台自身碰撞体此时已关，不会打到自己。</summary>
        private float FindFloorY(Bounds bounds)
        {
            int mask = LayerMask.GetMask("Ground");
            if (mask != 0)
            {
                var hit = Physics2D.Raycast(
                    new Vector2(bounds.center.x, bounds.min.y - 0.05f), Vector2.down, 12f, mask);
                if (hit.collider != null && hit.collider.gameObject != gameObject)
                {
                    return hit.point.y + 0.02f;
                }
            }

            return bounds.min.y - 3f;   // 探不到地面就让碎块自然落出画面
        }

        private void BeginReform()
        {
            _state = CrumbleState.Reforming;
            _timer = 0f;

            transform.position = _basePosition;
            if (_collider != null)
            {
                _collider.enabled = true;
            }

            if (_renderer != null)
            {
                _renderer.enabled = true;
                var c = _baseColor;
                c.a = 0f;
                _renderer.color = c;
            }
        }

        private void RestoreVisual()
        {
            transform.position = _basePosition;
            if (_collider != null)
            {
                _collider.enabled = true;
            }
            if (_renderer != null)
            {
                _renderer.enabled = true;
                _renderer.color = _baseColor;
            }
        }
    }
}
