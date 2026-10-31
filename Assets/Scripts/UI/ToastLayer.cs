using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StarEco.Framework;

namespace StarEco.UI
{
    /// <summary>
    /// 轻量提示条（B2）：屏幕中下方一行短文字，淡入 → 停留 → 淡出。
    /// 与 <see cref="DamageNumberLayer"/> 同一套「运行时自举 + 单 Canvas」范式：
    /// 不依赖场景挂载、任何一关打开都能用，因此不需要场景侧做任何改动。
    /// 同屏只显示一条，多条进队列依次播放——拾取类反馈挤在一起就都看不清了。
    /// 仅做提示，不锁输入、不挡射线（raycastTarget=false）。
    /// </summary>
    public class ToastLayer : MonoBehaviour
    {
        [Header("时序")]
        [Tooltip("单条总时长（秒，含淡入淡出）")]
        [SerializeField] private float _displaySeconds = 1.7f;

        [Tooltip("淡入时长（秒）")]
        [SerializeField] private float _fadeIn = 0.15f;

        [Tooltip("淡出时长（秒）")]
        [SerializeField] private float _fadeOut = 0.4f;

        [Header("样式")]
        [SerializeField] private int _fontSize = 34;

        [Tooltip("距屏幕底部的高度（参考分辨率 1920x1080 下的像素）")]
        [SerializeField] private float _bottomOffset = 190f;

        [Tooltip("队列上限，超出则丢弃新消息（防极端情况积压）")]
        [SerializeField] private int _maxQueue = 4;

        [Header("拾取提示")]
        [Tooltip("净化剂拾取时的提示文案（{0} = 实际回复量）。文案属策划域，策划可在此调整")]
        [SerializeField] private string _pickupToastFormat = "净化剂 +{0}";

        /// <summary>
        /// 净化主题色：与 HUDController.TankFillColor 同值（复用项目既有配色，不新增色板决策）。
        /// </summary>
        private static readonly Color PurifyTint = new Color(0.35f, 0.75f, 1f);

        private Canvas _canvas;
        private CanvasGroup _group;
        private Text _label;
        private readonly Queue<Message> _queue = new Queue<Message>();
        private float _timer;
        private bool _showing;

        /// <summary>
        /// 结算期间抑制提示。2026-10-31 修：toast 在屏幕底部 190px 处，
        /// 而 ResultPanel 的「重新开始 / 返回主菜单」按钮正好压在这一带，
        /// 关卡失败/通关时弹出的提示文字会被按钮盖住、糊成一团（Play 截图可见）。
        /// 结算面板打开即清队列并收起当前条，场景重载（重开/下一关）后自动复位。
        /// </summary>
        private bool _suppressed;

        private struct Message
        {
            public string Text;
            public Color Color;
        }

        /// <summary>当前实例（调用方一般不需要，用静态 <see cref="Show(string)"/> 即可）。</summary>
        public static ToastLayer Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
            {
                return;
            }

            if (Object.FindObjectOfType<ToastLayer>(true) != null)
            {
                return;
            }

            var go = new GameObject("ToastLayer", typeof(ToastLayer));
            Object.DontDestroyOnLoad(go);
        }

        /// <summary>显示一条提示（默认正文字色）。UI 层未就绪时静默忽略，绝不抛异常。</summary>
        public static void Show(string text)
        {
            Show(text, UIUtility.TextBody);
        }

        /// <summary>显示一条带颜色的提示。</summary>
        public static void Show(string text, Color color)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (Instance == null)
            {
                Bootstrap();
            }

            if (Instance != null)
            {
                Instance.Enqueue(text, color);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Build();
        }

        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                return;
            }

            EventCenter.Subscribe<StarEco.Level.Events.LevelFailedEvent>(OnLevelEnded);
            EventCenter.Subscribe<StarEco.Level.Events.LevelCompletedEvent>(OnLevelCompleted);
            EventCenter.Subscribe<StarEco.Eco.Events.PickupCollectedEvent>(OnPickupCollected);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            EventCenter.Unsubscribe<StarEco.Level.Events.LevelFailedEvent>(OnLevelEnded);
            EventCenter.Unsubscribe<StarEco.Level.Events.LevelCompletedEvent>(OnLevelCompleted);
            EventCenter.Unsubscribe<StarEco.Eco.Events.PickupCollectedEvent>(OnPickupCollected);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>关卡失败/通关：结算面板要占屏幕底部，提示条让位。</summary>
        private void OnLevelEnded(StarEco.Level.Events.LevelFailedEvent evt)
        {
            Suppress();
        }

        private void OnLevelCompleted(StarEco.Level.Events.LevelCompletedEvent evt)
        {
            Suppress();
        }

        /// <summary>重开本关 / 进入下一关：结算面板已关，提示恢复。</summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _suppressed = false;
        }

        /// <summary>
        /// 净化剂拾取反馈（U-03）：瓶子被捡起时原本只有 HUD 的净化剂条在变，
        /// 罐子接近满时数值看不出变化，玩家不知道刚才那瓶到底有没有喝上——补一条短提示。
        /// </summary>
        private void OnPickupCollected(StarEco.Eco.Events.PickupCollectedEvent evt)
        {
            if (evt.Kind != StarEco.Eco.PickupKind.PurifyBottle)
            {
                return;
            }

            // evt.Amount 是「实际回复量」：罐子已满时为 0，此时不弹 "+0"，免得误导玩家以为加了量。
            if (evt.Amount <= 0)
            {
                return;
            }

            // 文案可能缺 {0} 占位（策划改文案时），此时直接原样显示，不抛格式化异常。
            string text = !string.IsNullOrEmpty(_pickupToastFormat) && _pickupToastFormat.Contains("{0}")
                ? string.Format(_pickupToastFormat, evt.Amount)
                : _pickupToastFormat;

            Show(text, PurifyTint);
        }

        /// <summary>清空待播队列并立刻收起当前条（不等待淡出，避免残影留在结算面板上）。</summary>
        private void Suppress()
        {
            if (_suppressed)
            {
                return;
            }

            _suppressed = true;
            _queue.Clear();
            _showing = false;
            _timer = 0f;

            if (_group != null)
            {
                _group.alpha = 0f;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (_suppressed)
            {
                return;
            }

            if (!_showing)
            {
                if (_queue.Count > 0)
                {
                    BeginNext();
                }
                else
                {
                    return;
                }
            }

            _timer += Time.unscaledDeltaTime;

            float total = Mathf.Max(_displaySeconds, _fadeIn + _fadeOut + 0.05f);
            float alpha;
            if (_timer < _fadeIn)
            {
                alpha = _timer / _fadeIn;
            }
            else if (_timer > total - _fadeOut)
            {
                alpha = (total - _timer) / _fadeOut;
            }
            else
            {
                alpha = 1f;
            }

            if (_group != null)
            {
                _group.alpha = Mathf.Clamp01(alpha);
            }

            if (_timer >= total)
            {
                _showing = false;
                if (_group != null)
                {
                    _group.alpha = 0f;
                }
            }
        }

        private void Enqueue(string text, Color color)
        {
            // 结算期间直接丢弃，而不是排队等结算后补播——过时的拾取提示晚到更莫名其妙
            if (_suppressed || _queue.Count >= _maxQueue)
            {
                return;
            }

            _queue.Enqueue(new Message { Text = text, Color = color });
        }

        private void BeginNext()
        {
            var msg = _queue.Dequeue();

            if (_label != null)
            {
                _label.text = msg.Text;
                _label.color = msg.Color;
            }

            if (_group != null)
            {
                _group.alpha = 0f;
            }

            _timer = 0f;
            _showing = true;
        }

        // ===== 构建 =====

        private void Build()
        {
            _canvas = UIUtility.CreateCanvas("ToastCanvas", transform, 45);

            var go = UIUtility.CreateObject("Toast", _canvas.transform);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(1200f, 72f);
            rt.anchoredPosition = new Vector2(0f, _bottomOffset);

            _group = go.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _label = go.AddComponent<Text>();
            _label.font = UIUtility.BuiltinFont;
            _label.fontSize = _fontSize;
            _label.fontStyle = FontStyle.Bold;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = UIUtility.TextBody;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;
            _label.text = string.Empty;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.05f, 0.08f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }
    }
}
