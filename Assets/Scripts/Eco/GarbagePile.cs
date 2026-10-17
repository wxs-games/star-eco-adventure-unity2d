using UnityEngine;
using StarEco.Framework;
using StarEco.Eco.Events;

namespace StarEco.Eco
{
    /// <summary>
    /// 垃圾堆/挡路重垃圾（静态可净化目标）：实现 IPurifiable。
    /// 净化进度驱动占位 Sprite 颜色插值（污染色 → 净化色，即时反馈）；
    /// 完成后播 fx_purify、广播事件；挡路型关闭阻挡 Collider 打开通路。
    /// 进度中断不衰减（design §2.1）。美术后补仅换 Sprite，逻辑不变。
    /// </summary>
    public class GarbagePile : MonoBehaviour, IPurifiable
    {
        [SerializeField] private EcoConfig _config;
        [SerializeField] private FxOneShot _fxPurifyPrefab;

        [Tooltip("净化阈值：-1 = 跟随 EcoConfig（按 BlocksPath 分流），≥0 显式覆盖")]
        [SerializeField] private int _purifyRequired = -1;

        [Tooltip("净化值贡献：-1 = 跟随 EcoConfig（按 BlocksPath 分流），≥0 显式覆盖")]
        [SerializeField] private int _purityValue = -1;

        [Tooltip("是否挡路型（净化完成后关闭阻挡 Collider 打开通路）")]
        [SerializeField] private bool _blocksPath = false;

        [Tooltip("目标标识（净化完成事件携带）")]
        [SerializeField] private string _targetId = "GarbagePile";

        private SpriteRenderer _sprite;
        private BoxCollider2D _collider;
        private int _progress;
        private bool _purified;

        public bool IsPurified => _purified;

        public int PurityValue => _purityValue >= 0
            ? _purityValue
            : (_blocksPath ? ConfigValue(c => c.GarbageWallPurityValue, 10) : ConfigValue(c => c.GarbagePurityValue, 5));

        private void Awake()
        {
            _sprite = GetComponent<SpriteRenderer>();
            _collider = GetComponent<BoxCollider2D>();

            if (_config == null)
            {
                _config = Resources.Load<EcoConfig>("EcoConfig") ?? EcoConfig.CreateDefault();
            }

            // 初始态：污染色
            if (_sprite != null)
            {
                _sprite.color = _config.GarbagePollutedTint;
            }
        }

        // ===== IPurifiable =====

        public void ReceivePurify(int amount, GameObject source)
        {
            if (_purified || amount <= 0)
            {
                return;   // 幂等（R1）
            }

            _progress += amount;

            int required = ResolveRequired();
            float t = required > 0 ? Mathf.Clamp01((float)_progress / required) : 1f;

            // 净化中：颜色随进度插值（R5 即时反馈）
            if (_sprite != null)
            {
                _sprite.color = Color.Lerp(_config.GarbagePollutedTint, _config.GarbageCleanTint, t);
            }

            if (t >= 1f)
            {
                CompletePurify();
            }
        }

        private void CompletePurify()
        {
            _purified = true;

            if (_sprite != null)
            {
                _sprite.color = _config.GarbageCleanTint;
            }

            FxOneShot.Play(_fxPurifyPrefab, transform.position);

            EventCenter.Publish(new GarbagePurifiedEvent
            {
                PurityValue = PurityValue,
                Position = transform.position,
                TargetId = _targetId
            });

            // 挡路型：开通路 + 视觉变淡（R5）
            if (_blocksPath)
            {
                if (_collider != null)
                {
                    _collider.enabled = false;
                }

                if (_sprite != null)
                {
                    var c = _sprite.color;
                    c.a = 0.3f;
                    _sprite.color = c;
                }
            }
        }

        private int ResolveRequired()
        {
            if (_purifyRequired >= 0)
            {
                return _purifyRequired;
            }

            return _blocksPath
                ? ConfigValue(c => c.GarbageWallPurifyRequired, 60)
                : ConfigValue(c => c.GarbagePurifyRequired, 4);
        }

        private int ConfigValue(System.Func<EcoConfig, int> selector, int fallback)
        {
            return _config != null ? selector(_config) : fallback;
        }
    }
}
