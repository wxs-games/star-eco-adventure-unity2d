using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarEco.UI
{
    /// <summary>
    /// 提示牌阅读面板（M7 R9）：靠近按 E 打开，整段文本分页，E 翻页，末页关闭。
    /// 与 NPC 对话面板分离：不分句、无逐字、无头像；数据复用 DialogueData 单 Entry。
    ///
    /// 视觉：全屏压暗 + 居中卡片（标题 / 强调线 / 正文 / 页码 / 操作提示），
    /// 正文自动换行并留足内边距，避免文字直接压在游戏画面上。
    /// </summary>
    public class SignboardPanel : UIPanel
    {
        public const string PanelId = "Signboard";

        /// <summary>
        /// 单页字数上限。
        /// 卡片正文区约 1004×360 px，30 号字行高 1.5 → 每行约 33 个汉字、8 行，
        /// 取 200 留出换行余量，避免末行被裁掉。
        /// </summary>
        public const int PageCharCount = 200;

        public override string PanelName => PanelId;
        public override bool BlocksInput => true;

        /// <summary>单例入口，供 SignboardTrigger 请求打开。</summary>
        public static SignboardPanel Instance { get; private set; }

        /// <summary>确保单例存在（运行时自建，场景未挂载时兜底）。</summary>
        public static SignboardPanel EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            // 场景中可能已有实例（Awake 时序异常时 Instance 未设置），优先复用
            var existing = Object.FindObjectOfType<SignboardPanel>(true);
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("SignboardPanel");
            var panel = go.AddComponent<SignboardPanel>();
            return panel;
        }

        private Text _readerText;
        private Text _readerPageText;
        private Text _titleText;
        private CanvasGroup _group;

        private readonly List<string> _pages = new List<string>();
        private int _pageIndex;
        private bool _isReading;
        private bool _waitForInteractRelease;

        /// <summary>最近一次消费输入的帧号，用于 OnUpdate 与 Update 双入口去重。</summary>
        private int _lastHandledFrame = -1;

        /// <summary>
        /// 输入由 <see cref="Update"/> 单一驱动，不再与 UIManager 的 OnUpdate 双头处理。
        ///
        /// 教训（2026-11-01）：早先按「是否入 UIManager 栈」在两个入口间二选一，
        /// 结果 Open() 成功但面板没能成为栈顶时，OnUpdate 首行的栈顶校验直接 return，
        /// 而自身 Update 又因 _drivenByUIManager=true 整段跳过 —— 两条通道同时失效，
        /// 表现为「提示牌打开后按 E 永远关不掉」。单一入口可彻底消除这类互斥漏洞。
        /// </summary>
        private bool CanHandleInput =>
            _isReading &&
            (!UIManager.HasInstance || UIManager.Instance.TopPanelName == PanelId);

        /// <summary>是否正在阅读（关闭后为 false）。触发器据此让出 E 键。</summary>
        public bool IsReading => _isReading;

        /// <summary>
        /// 上次关闭阅读界面的 unscaledTime。
        /// 触发器用它避开"关闭用的那一次 E 立刻又把面板打开"的同帧竞态：
        /// 面板与触发器的 Update 执行顺序不确定，若触发器排在 UIManager 之后，
        /// 它看到的已经是"已关闭 + 本帧仍有 E 按下"，会立刻重开。
        /// </summary>
        public static float LastCloseUnscaledTime { get; private set; } = -999f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            Build();

            if (UIManager.HasInstance)
            {
                UIManager.Instance.Register(this);
            }
        }

        private void OnDestroy()
        {
            // 场景卸载时复位单例与阅读开关，避免残留导致新场景敌人永不攻击
            if (Instance == this)
            {
                Instance = null;
            }

            _isReading = false;
            _pages.Clear();
            _pageIndex = 0;

            if (UIManager.HasInstance)
            {
                UIManager.Instance.Unregister(this);
            }
        }

        protected override void OnOpen()
        {
            // 防御：运行时自建实例的 Build 若未跑完，此处补建，避免"面板开了但一片空白"
            if (_readerText == null)
            {
                Build();
            }

            _isReading = true;
            _pageIndex = 0;
            // 打开用的那一次 E 不能同时被当成翻页/关闭输入
            _waitForInteractRelease = true;
            RefreshPage();
            DialoguePanel.SetDialogueActive(true);
        }

        protected override void OnClose()
        {
            _isReading = false;
            DialoguePanel.SetDialogueActive(false);
        }
        /// <summary>UIManager 每帧转发的驱动入口，与 Update 共用同一个输入处理函数。</summary>
        protected override void OnUpdate(float dt)
        {
            HandleInput();
        }

        /// <summary>
        /// 唯一输入入口：翻页与关闭都在这里判定，E 只被消费一次。
        /// 不依赖 UIManager 是否存在，也不依赖面板是否恰好在栈顶——
        /// 只要它在阅读态、且没有更高优先级的阻断面板压着，就响应 E。
        /// </summary>
        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            if (!CanHandleInput)
            {
                return;
            }

            // OnUpdate（UIManager 转发）与自身 Update 同帧都会进来，
            // 用帧号去重，保证一次按键只翻一页。
            if (_lastHandledFrame == Time.frameCount)
            {
                return;
            }
            _lastHandledFrame = Time.frameCount;

            // 打开/关闭用的那一次 E 不能同时被当成翻页输入
            if (_waitForInteractRelease)
            {
                if (!Input.GetKey(KeyCode.E))
                {
                    _waitForInteractRelease = false;
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                _pageIndex++;
                if (_pageIndex >= _pages.Count)
                {
                    CloseReading();
                    return;
                }

                RefreshPage();
            }
        }

        /// <summary>设置卡片标题（默认「提示牌」，有说话人时用其名）。</summary>
        public void SetTitle(string title)
        {
            if (_titleText != null && !string.IsNullOrEmpty(title))
            {
                _titleText.text = title;
            }
        }

        /// <summary>由 SignboardTrigger 调用：请求打开并传入分页文本。</summary>
        public void StartReading(List<string> pages)
        {
            _pages.Clear();
            if (pages != null)
            {
                _pages.AddRange(pages);
            }

            if (_pages.Count == 0)
            {
                _pages.Add("（牌子上什么都没有）");
            }

            _pageIndex = 0;

            // 不依赖 UIManager 是否已存在：先确保面板自身可见并能接收 E。
            // UIManager 只负责入栈与玩家输入锁，缺了它界面也得能看、能翻页。
            if (_readerText == null)
            {
                Build();
            }

            if (UIManager.HasInstance)
            {
                UIManager.Instance.Register(this);
                UIManager.Instance.Open(PanelId);
            }

            // 面板可见性与输入不再依赖 UIManager 是否真正入栈：
            // 入栈只用于 Esc 路由与玩家输入锁，界面自身必须无论成败都能显示与关闭。
            if (!IsOpen)
            {
                Open();
            }
            else
            {
                _isReading = true;
                _pageIndex = 0;
                _waitForInteractRelease = true;
                RefreshPage();
                DialoguePanel.SetDialogueActive(true);
            }
        }

        private void CloseReading()
        {
            _isReading = false;
            _waitForInteractRelease = true;
            LastCloseUnscaledTime = Time.unscaledTime;

            // 关闭也走单一出口：无论面板是否在栈顶，都要真正隐藏。
            if (UIManager.HasInstance && UIManager.Instance.TopPanelName == PanelName)
            {
                UIManager.Instance.Close(PanelId);
            }

            if (IsOpen)
            {
                Close();
            }
        }

        private void RefreshPage()
        {
            if (_pageIndex >= _pages.Count)
            {
                _pageIndex = Mathf.Max(0, _pages.Count - 1);
            }

            if (_readerText != null)
            {
                _readerText.text = _pages[_pageIndex];
            }

            if (_readerPageText != null)
            {
                bool last = _pageIndex >= _pages.Count - 1;
                _readerPageText.text = _pages.Count <= 1
                    ? "按 E 关闭"
                    : $"第 {_pageIndex + 1} / {_pages.Count} 页 · 按 E {(last ? "关闭" : "继续")}";
            }
        }

        // ===== 构建 =====

        private void Build()
        {
            UIUtility.EnsureEventSystem();

            // 层级：压在 HUD(10) 与对话(70) 之上，低于结算(100)
            var canvas = UIUtility.CreateCanvas("SignboardCanvas", transform, 80);
            SetRoot(canvas.gameObject);

            var root = canvas.transform;

            // 全屏压暗，让文字从场景里"浮"出来
            var bg = UIUtility.CreateImage("Background", root, new Color(0f, 0f, 0f, 0.78f));
            UIUtility.Stretch(bg.GetComponent<RectTransform>());

            _group = bg.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;

            // 居中卡片
            var card = UIUtility.CreateObject("Card", root);
            UIUtility.CenterAnchor(card, new Vector2(1120f, 560f), Vector2.zero);

            var cardBg = UIUtility.CreateImage("Background", card.transform, UIUtility.PanelBg);
            UIUtility.Stretch(cardBg.GetComponent<RectTransform>());

            // 顶部强调线（替代纯文字标题的呆板感）
            var accent = UIUtility.CreateImage("AccentLine", card.transform, UIUtility.AccentGreen);
            var accentRt = accent.GetComponent<RectTransform>();
            accentRt.anchorMin = new Vector2(0f, 1f);
            accentRt.anchorMax = new Vector2(1f, 1f);
            accentRt.pivot = new Vector2(0.5f, 1f);
            accentRt.sizeDelta = new Vector2(-96f, 3f);
            accentRt.anchoredPosition = new Vector2(0f, -96f);

            // 标题
            _titleText = UIUtility.CreateText("Title", card.transform, "提示牌", 38,
                TextAnchor.MiddleLeft, UIUtility.AccentGreen);
            var titleRt = _titleText.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(-96f, 56f);
            titleRt.anchoredPosition = new Vector2(0f, -30f);
            _titleText.fontStyle = FontStyle.Bold;

            // 正文：左对齐、自动换行、行距 1.6、四周留白
            _readerText = UIUtility.CreateText("Body", card.transform, string.Empty, 30,
                TextAnchor.UpperLeft, UIUtility.TextBody);
            var bodyRt = _readerText.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 0.5f);
            bodyRt.offsetMin = new Vector2(48f, 84f);
            bodyRt.offsetMax = new Vector2(-48f, -116f);
            _readerText.lineSpacing = 1.6f;
            _readerText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _readerText.verticalOverflow = VerticalWrapMode.Truncate;
            _readerText.raycastTarget = false;

            // 页脚：页码 + 操作提示
            _readerPageText = UIUtility.CreateText("Footer", card.transform, string.Empty, 22,
                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.55f));
            var pageRt = _readerPageText.GetComponent<RectTransform>();
            pageRt.anchorMin = new Vector2(0.5f, 0f);
            pageRt.anchorMax = new Vector2(0.5f, 0f);
            pageRt.pivot = new Vector2(0.5f, 0f);
            pageRt.sizeDelta = new Vector2(-96f, 36f);
            pageRt.anchoredPosition = new Vector2(0f, 32f);
            _readerPageText.raycastTarget = false;

            Root.SetActive(false);
        }
    }
}
