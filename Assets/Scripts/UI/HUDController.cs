using UnityEngine;
using UnityEngine.UI;
using StarEco.Framework;
using StarEco.Eco;
using StarEco.Eco.Events;
using StarEco.Level;
using StarEco.Level.Events;
using StarEco.Player;

namespace StarEco.UI
{
    /// <summary>
    /// HUD（M7 R1）：常驻关卡界面。
    /// - HP 心形×5（实心=剩余 / 空心=损失）
    /// - 净化剂条 + 数值
    /// - 净化值进度条 + 百分比
    /// - 当前目标提示（Label + 进度 x/y）
    /// - 蓄力进度条（蓄力中显示）
    /// 非阻断面板（BlocksInput=false）；关卡终态自动隐藏。
    /// </summary>
    public class HUDController : UIPanel
    {
        public const string PanelId = "HUD";

        public override string PanelName => PanelId;
        public override bool BlocksInput => false;

        private Image[] _hearts;
        private Image _tankFill;
        private Text _tankText;
        private Image _purityFill;
        private Text _purityText;
        private Text _objectiveText;
        private Image _chargeFill;
        private GameObject _chargeRoot;

        private PlayerController _player;
        private PuritySystem _purity;
        private LevelObjectiveTracker _tracker;
        private LevelManager _level;
        private float _tankBlinkTimer;
        private bool _tankWarned;

        private static readonly Color HeartFull = new Color(1f, 0.35f, 0.42f);
        private static readonly Color HeartEmpty = new Color(0.30f, 0.30f, 0.34f, 0.85f);
        private static readonly Color TankFillColor = new Color(0.35f, 0.75f, 1f);
        private static readonly Color PurityFillColor = new Color(0.49f, 0.99f, 0.60f);
        private static readonly Color ChargeFillColor = new Color(1f, 0.85f, 0.35f);

        private void Awake()
        {
            Build();
            if (UIManager.HasInstance)
            {
                UIManager.Instance.Register(this);
            }
        }

        private void OnDestroy()
        {
            EventCenter.Unsubscribe<PurityChangedEvent>(OnPurityChanged);
            EventCenter.Unsubscribe<LevelObjectiveChangedEvent>(OnObjectiveChanged);
            EventCenter.Unsubscribe<ObjectiveCompletedEvent>(OnObjectiveCompleted);

            if (UIManager.HasInstance)
            {
                UIManager.Instance.Unregister(this);
            }
        }

        private void Start()
        {
            ResolveDependencies();

            EventCenter.Subscribe<PurityChangedEvent>(OnPurityChanged);
            EventCenter.Subscribe<LevelObjectiveChangedEvent>(OnObjectiveChanged);
            EventCenter.Subscribe<ObjectiveCompletedEvent>(OnObjectiveCompleted);

            // 主动拉取一次：首个 LevelObjectiveChangedEvent 由 LevelManager.Start 触发，
            // 可能早于本组件的 Start（同场景 Start 顺序不定），不补拉会停在"目标：—"。
            RefreshObjectiveFromTracker();
            RefreshPurity();

            // 注册可能因 Awake 时序丢失（HUD 的 Awake 早于 UIManager 的 Awake 时
            // HasInstance 为 false）。Start 阶段 UIManager 必已就绪，这里幂等补注册。
            if (UIManager.HasInstance)
            {
                UIManager.Instance.Register(this);
                UIManager.Instance.Open(PanelId);
            }
        }

        private void ResolveDependencies()
        {
            _player = FindObjectOfType<PlayerController>();
            _purity = FindObjectOfType<PuritySystem>();
            _tracker = FindObjectOfType<LevelObjectiveTracker>();
            _level = LevelManager.HasInstance ? LevelManager.Instance : null;

            // 无玩家数据源（如误挂主菜单场景）→ HUD 不显示
            if (_player == null)
            {
                Debug.LogWarning("[HUD] 未找到 PlayerController，HUD 停用。");
                if (Root != null)
                {
                    Root.SetActive(false);
                }
            }
        }

        // ===== 构建 =====

        private void Build()
        {
            UIUtility.EnsureEventSystem();

            var canvas = UIUtility.CreateCanvas("HUDCanvas", transform, 10);
            Root = canvas.gameObject;

            var font = UIUtility.BuiltinFont;
            var root = canvas.transform;

            // 左上：HP 心形 + 净化剂条
            var statusGo = UIUtility.CreateObject("Status", root);
            UIUtility.TopLeftAnchor(statusGo, new Vector2(460f, 130f), new Vector2(30f, -30f));

            var heartsGo = UIUtility.CreateObject("Hearts", statusGo.transform);
            var heartsRt = heartsGo.GetComponent<RectTransform>();
            heartsRt.anchorMin = new Vector2(0f, 1f);
            heartsRt.anchorMax = new Vector2(0f, 1f);
            heartsRt.pivot = new Vector2(0f, 1f);
            heartsRt.sizeDelta = new Vector2(260f, 48f);
            heartsRt.anchoredPosition = Vector2.zero;

            _hearts = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                var heartGo = UIUtility.CreateObject("Heart" + i, heartsGo.transform);
                var rt = heartGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(44f, 44f);
                rt.anchoredPosition = new Vector2(24f + i * 50f, 0f);
                _hearts[i] = heartGo.AddComponent<Image>();
                _hearts[i].color = HeartFull;
            }

            var tankBar = UIUtility.CreateBar("TankBar", statusGo.transform,
                new Vector2(380f, 26f), new Vector2(190f, -78f),
                new Color(0f, 0f, 0f, 0.55f), TankFillColor, out _);
            tankBar.transform.SetParent(statusGo.transform, false);
            _tankFill = tankBar;

            var tankTextGo = UIUtility.CreateObject("TankText", statusGo.transform);
            var tankTextRt = tankTextGo.GetComponent<RectTransform>();
            tankTextRt.anchorMin = new Vector2(0f, 0f);
            tankTextRt.anchorMax = new Vector2(0f, 0f);
            tankTextRt.pivot = new Vector2(0f, 0f);
            tankTextRt.sizeDelta = new Vector2(380f, 30f);
            tankTextRt.anchoredPosition = new Vector2(0f, -108f);
            _tankText = tankTextGo.AddComponent<Text>();
            _tankText.font = font;
            _tankText.fontSize = 24;
            _tankText.alignment = TextAnchor.MiddleLeft;
            _tankText.color = TankFillColor;
            _tankText.text = "净化剂 0/0";

            // 蓄力条（净化剂条上方，默认隐藏）
            var chargeGo = UIUtility.CreateObject("ChargeBar", statusGo.transform);
            UIUtility.TopLeftAnchor(chargeGo, new Vector2(380f, 18f), new Vector2(0f, -112f));
            _chargeRoot = chargeGo;

            var chargeBg = UIUtility.CreateImage("Background", chargeGo.transform,
                new Color(0f, 0f, 0f, 0.55f));
            UIUtility.Stretch(chargeBg.GetComponent<RectTransform>());

            var chargeFillGo = UIUtility.CreateObject("Fill", chargeGo.transform);
            var chargeFillRt = chargeFillGo.GetComponent<RectTransform>();
            chargeFillRt.anchorMin = Vector2.zero;
            chargeFillRt.anchorMax = Vector2.one;
            chargeFillRt.offsetMin = Vector2.zero;
            chargeFillRt.offsetMax = Vector2.zero;
            _chargeFill = chargeFillGo.AddComponent<Image>();
            _chargeFill.color = ChargeFillColor;
            _chargeFill.type = Image.Type.Filled;
            _chargeFill.fillMethod = Image.FillMethod.Horizontal;
            _chargeFill.fillOrigin = (int)Image.OriginHorizontal.Left;

            chargeGo.SetActive(false);

            // 右上：净化值进度条
            var purityGo = UIUtility.CreateObject("PurityPanel", root);
            UIUtility.TopLeftAnchor(purityGo, new Vector2(420f, 96f), new Vector2(-450f, -30f));
            // TopLeftAnchor 的 pos 是相对左上；这里用 anchorMax 方案改为右上更直观
            var purityRt = purityGo.GetComponent<RectTransform>();
            purityRt.anchorMin = new Vector2(1f, 1f);
            purityRt.anchorMax = new Vector2(1f, 1f);
            purityRt.pivot = new Vector2(1f, 1f);
            purityRt.sizeDelta = new Vector2(420f, 96f);
            purityRt.anchoredPosition = new Vector2(-30f, -30f);

            var purityLabel = UIUtility.CreateText("Label", purityGo.transform, "星球净化值", 26,
                TextAnchor.MiddleLeft, PurityFillColor);
            var purityLabelRt = purityLabel.GetComponent<RectTransform>();
            purityLabelRt.anchorMin = new Vector2(0f, 1f);
            purityLabelRt.anchorMax = new Vector2(1f, 1f);
            purityLabelRt.pivot = new Vector2(0f, 1f);
            purityLabelRt.sizeDelta = new Vector2(0f, 34f);
            purityLabelRt.anchoredPosition = Vector2.zero;

            _purityFill = UIUtility.CreateBar("Bar", purityGo.transform,
                new Vector2(420f, 34f), new Vector2(0f, -56f),
                new Color(0f, 0f, 0f, 0.55f), PurityFillColor, out _);
            _purityFill.fillAmount = 0f;

            _purityText = UIUtility.CreateText("Percent", purityGo.transform, "0%", 26,
                TextAnchor.MiddleRight, TextMainColor());
            var purityTextRt = _purityText.GetComponent<RectTransform>();
            purityTextRt.anchorMin = new Vector2(0f, 1f);
            purityTextRt.anchorMax = new Vector2(1f, 1f);
            purityTextRt.pivot = new Vector2(1f, 1f);
            purityTextRt.sizeDelta = new Vector2(0f, 34f);
            purityTextRt.anchoredPosition = Vector2.zero;

            // 顶部居中：当前目标
            var objGo = UIUtility.CreateObject("Objective", root);
            var objRt = objGo.GetComponent<RectTransform>();
            objRt.anchorMin = new Vector2(0.5f, 1f);
            objRt.anchorMax = new Vector2(0.5f, 1f);
            objRt.pivot = new Vector2(0.5f, 1f);
            objRt.sizeDelta = new Vector2(760f, 60f);
            objRt.anchoredPosition = new Vector2(0f, -24f);

            var objBg = UIUtility.CreateImage("Background", objGo.transform,
                new Color(0.05f, 0.08f, 0.12f, 0.65f));
            UIUtility.Stretch(objBg.GetComponent<RectTransform>());

            _objectiveText = UIUtility.CreateText("Label", objGo.transform, "目标：—", 28,
                TextAnchor.MiddleCenter, UIUtility.TextBody);
            UIUtility.Stretch(_objectiveText.GetComponent<RectTransform>());
        }

        private static Color TextMainColor() => UIUtility.TextMain;

        // ===== 事件 =====

        private void OnPurityChanged(PurityChangedEvent evt)
        {
            RefreshPurity();
        }

        private void OnObjectiveChanged(LevelObjectiveChangedEvent evt)
        {
            if (_objectiveText == null)
            {
                return;
            }

            if (evt.Index < 0 || string.IsNullOrEmpty(evt.Label))
            {
                _objectiveText.text = "目标：关卡目标已完成";
            }
            else if (evt.Required > 0)
            {
                _objectiveText.text = $"目标：{evt.Label}（{evt.Current}/{evt.Required}）";
            }
            else
            {
                _objectiveText.text = $"目标：{evt.Label}";
            }
        }

        /// <summary>从目标链直接读当前目标文本（初始化与事件丢失时的兜底）。</summary>
        private void RefreshObjectiveFromTracker()
        {
            if (_objectiveText == null || _tracker == null)
            {
                return;
            }

            var objective = _tracker.CurrentObjective;
            int required = _tracker.CurrentRequired;

            if (objective == null)
            {
                _objectiveText.text = "目标：关卡目标已完成";
            }
            else if (required > 0)
            {
                _objectiveText.text = $"目标：{objective.Label}（{_tracker.CurrentProgress}/{required}）";
            }
            else
            {
                _objectiveText.text = $"目标：{objective.Label}";
            }
        }

        private void OnObjectiveCompleted(ObjectiveCompletedEvent evt)
        {
            // 计数刷新由 LevelObjectiveChangedEvent 统一驱动；这里只做日志
            Debug.Log($"[HUD] 目标完成：{evt.Label}");
        }

        // ===== 刷新 =====

        protected override void OnUpdate(float dt)
        {
            if (Root == null || !Root.activeSelf)
            {
                return;
            }

            // 终态隐藏
            if (_level != null && _level.State != LevelState.Playing)
            {
                Root.SetActive(false);
                return;
            }

            if (_player == null || _player.Combat == null)
            {
                _player = FindObjectOfType<PlayerController>();
                if (_player == null)
                {
                    return;
                }
            }

            RefreshHearts();
            RefreshTank(dt);
            RefreshCharge();
        }

        private void RefreshHearts()
        {
            int hp = _player.Combat.CurrentHP;
            int max = Mathf.Max(1, _player.Combat.MaxHP);

            for (int i = 0; i < _hearts.Length; i++)
            {
                if (_hearts[i] == null)
                {
                    continue;
                }

                bool full = i < hp;
                _hearts[i].color = full ? HeartFull : HeartEmpty;

                // HP 上限超过 5 时超出部分隐藏，避免越界（M3 MaxHP=5）
                _hearts[i].gameObject.SetActive(i < max);
            }
        }

        private void RefreshTank(float dt)
        {
            var tank = _player.Spray != null ? _player.Spray.Tank : null;
            if (tank == null || _tankFill == null)
            {
                return;
            }

            float ratio = tank.Capacity > 0 ? Mathf.Clamp01((float)tank.Current / tank.Capacity) : 0f;
            _tankFill.fillAmount = ratio;

            if (_tankText != null)
            {
                _tankText.text = $"净化剂 {tank.Current}/{tank.Capacity}";
            }

            // 不足提示：低于蓄力消耗时条体闪烁
            var spray = _player.Spray;
            int heavyCost = spray != null && spray.Config != null ? spray.Config.HeavyCost : 0;
            bool low = tank.Current < heavyCost;

            if (low)
            {
                _tankBlinkTimer += dt;
                float blink = Mathf.PingPong(_tankBlinkTimer * 4f, 1f);
                _tankFill.color = Color.Lerp(TankFillColor, UIUtility.WarnRed, blink);

                if (!_tankWarned)
                {
                    _tankWarned = true;
                    Debug.Log("[HUD] 净化剂不足，无法蓄力。");
                }
            }
            else
            {
                _tankBlinkTimer = 0f;
                _tankWarned = false;
                _tankFill.color = TankFillColor;
            }
        }

        private void RefreshCharge()
        {
            if (_chargeRoot == null)
            {
                return;
            }

            float ratio = _player.ChargeRatio;
            bool charging = ratio > 0.001f;

            if (_chargeRoot.activeSelf != charging)
            {
                _chargeRoot.SetActive(charging);
            }

            if (charging && _chargeFill != null)
            {
                _chargeFill.fillAmount = Mathf.Clamp01(ratio);
            }
        }

        private void RefreshPurity()
        {
            if (_purity == null || _purityFill == null)
            {
                return;
            }

            float percent = Mathf.Clamp01(_purity.Percent);
            _purityFill.fillAmount = percent;

            if (_purityText != null)
            {
                _purityText.text = $"{percent * 100f:F0}%";
            }
        }
    }
}
