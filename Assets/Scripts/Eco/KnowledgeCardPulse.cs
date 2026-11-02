using UnityEngine;

namespace StarEco.Eco
{
    /// <summary>
    /// 悬空知识卡的呼吸提示：透明度在 [minAlpha, maxAlpha] 间循环，并带一点上下浮动，
    /// 让「这张卡是要跳上去拿的」在静止画面里也能被注意到。
    ///
    /// 刻意**不做缩放**：卡片下挂着 <see cref="KnowledgeCardBeacon"/> 光柱，
    /// 缩放父物体会把光柱一起拉变形；浮动只影响整体位置，光柱会跟着走，观感正常。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class KnowledgeCardPulse : MonoBehaviour
    {
        [Header("透明度呼吸")]
        [SerializeField] private float _pulseHz = 0.75f;
        [Range(0f, 1f)][SerializeField] private float _minAlpha = 0.65f;
        [Range(0f, 1f)][SerializeField] private float _maxAlpha = 1f;

        [Header("上下浮动（世界单位）")]
        [SerializeField] private float _floatAmplitude = 0.04f;

        private SpriteRenderer _sr;
        private Vector3 _baseLocalPos;
        private float _phase;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _baseLocalPos = transform.localPosition;

            // 错开相位，避免同屏几张卡整齐划一地闪
            _phase = Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            if (_sr == null)
            {
                return;
            }

            // 被拾取后 SpriteRenderer 会被关掉，此时停止呼吸，避免留下半透明的空物体
            if (!_sr.enabled)
            {
                return;
            }

            _phase += Time.deltaTime * _pulseHz * Mathf.PI * 2f;

            float k = (Mathf.Sin(_phase) + 1f) * 0.5f;   // 0~1
            Color c = _sr.color;
            c.a = Mathf.Lerp(_minAlpha, _maxAlpha, k);
            _sr.color = c;

            Vector3 p = _baseLocalPos;
            p.y = _baseLocalPos.y + Mathf.Sin(_phase) * _floatAmplitude;
            transform.localPosition = p;
        }
    }
}
