using UnityEngine;

namespace StarEco.Eco
{
    /// <summary>
    /// 悬浮知识卡的「下落指示光柱」：在卡片下方生成一条上亮下淡的绿色光柱，
    /// 提示玩家「这个要跳上去拿」。
    ///
    /// 为什么运行时生成：项目里没有现成的柔光/光柱素材，而这条光柱只是简单渐变，
    /// 用代码生成 1 张 8×64 的渐变贴图即可，不需要新增美术资源、也不占图集。
    /// 组件挂在卡片上即可，长度按「卡片底 → 支撑面顶」配置。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class KnowledgeCardBeacon : MonoBehaviour
    {
        [Header("光柱尺寸")]
        [Tooltip("光柱长度（世界单位）：从卡片精灵底部往下延伸多少")]
        [SerializeField] private float _beamLength = 0.9f;
        [Tooltip("光柱宽度（世界单位）")]
        [SerializeField] private float _beamWidth = 0.18f;

        [Header("配色与呼吸")]
        [SerializeField] private Color _color = new Color(0.49f, 0.99f, 0.60f, 1f);
        [SerializeField] private float _minAlpha = 0.10f;
        [SerializeField] private float _maxAlpha = 0.34f;
        [SerializeField] private float _pulseHz = 0.9f;

        [Header("渲染层")]
        [Tooltip("与卡片同为 6（道具层），保证在平台(5)之上、玩家(10)之下")]
        [SerializeField] private int _sortingOrder = 6;

        private SpriteRenderer _beam;
        private float _phase;

        private void Start()
        {
            BuildBeam();
        }

        private void BuildBeam()
        {
            var card = GetComponent<SpriteRenderer>();
            if (card == null || card.sprite == null)
            {
                return;
            }

            var go = new GameObject("Beacon");
            go.transform.SetParent(transform, false);

            // 卡片精灵底部在世界空间的偏移
            float spriteHalfHeight = card.sprite.bounds.extents.y;
            float pivotToBottom = card.sprite.bounds.extents.y + card.sprite.bounds.center.y;
            float bottomLocalY = -pivotToBottom;

            go.transform.localPosition = new Vector3(0f, bottomLocalY - _beamLength * 0.5f, 0f);

            _beam = go.AddComponent<SpriteRenderer>();
            _beam.sprite = CreateGradientSprite();
            _beam.sortingOrder = _sortingOrder;
            _beam.color = new Color(_color.r, _color.g, _color.b, _maxAlpha);

            // 贴图是按世界单位归一化的：宽 8px、PPU 64 → 精灵宽 0.125u、高 1.0u。
            // 所以宽度要除以精灵自身宽度，长度直接等于目标长度（这样 _beamWidth/_beamLength 才真是世界单位）。
            var size = _beam.sprite.bounds.size;
            float sx = size.x > 0.0001f ? _beamWidth / size.x : _beamWidth;
            go.transform.localScale = new Vector3(sx, _beamLength / Mathf.Max(size.y, 0.0001f), 1f);
        }

        /// <summary>生成上亮下淡的竖直渐变（8×64），白色 + alpha 渐变，靠 color 上色。</summary>
        private static Sprite CreateGradientSprite()
        {
            const int w = 8;
            const int h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "CardBeaconGradient"
            };

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                // y=0 是贴图底部 → 越靠下越透明
                float t = (float)y / (h - 1);
                float a = Mathf.Pow(t, 1.7f);
                byte alpha = (byte)(Mathf.Clamp01(a) * 255f);
                for (int x = 0; x < w; x++)
                {
                    pixels[y * w + x] = new Color32(255, 255, 255, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), h);
        }

        private void Update()
        {
            if (_beam == null)
            {
                return;
            }

            _phase += Time.deltaTime * _pulseHz * Mathf.PI * 2f;
            float k = (Mathf.Sin(_phase) + 1f) * 0.5f;   // 0~1
            float a = Mathf.Lerp(_minAlpha, _maxAlpha, k);
            _beam.color = new Color(_color.r, _color.g, _color.b, a);
        }

        private void OnDestroy()
        {
            if (_beam != null && _beam.sprite != null && _beam.sprite.texture != null)
            {
                Destroy(_beam.sprite.texture);
            }
        }
    }
}
