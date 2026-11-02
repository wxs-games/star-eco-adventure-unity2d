using UnityEngine;
using UnityEngine.UI;

namespace StarEco.UI
{
    /// <summary>
    /// 退出游戏确认弹窗。由主菜单退出按钮或 Title 场景的 Esc 打开。
    /// </summary>
    public class QuitConfirmPanel : UIPanel
    {
        public const string PanelId = "QuitConfirm";

        public override string PanelName => PanelId;

        private Button _confirmButton;
        private bool _confirmRequested;

        private void Awake()
        {
            Build();
            if (UIManager.HasInstance)
            {
                UIManager.Instance.Register(this);
            }
        }

        private void Start()
        {
            // UIManager 的 Start 会再次扫描注册，这里保留幂等注册以覆盖初始化顺序差异。
            if (UIManager.HasInstance)
            {
                UIManager.Instance.Register(this);
            }
        }

        private void OnDestroy()
        {
            if (UIManager.HasInstance)
            {
                UIManager.Instance.Unregister(this);
            }
        }

        protected override void OnOpen()
        {
            _confirmRequested = false;
            if (_confirmButton != null)
            {
                _confirmButton.Select();
            }
        }

        protected override void OnUpdate(float dt)
        {
            if (_confirmRequested)
            {
                return;
            }

            // 鼠标点击确认按钮之外，回车也执行当前确认动作。
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                _confirmRequested = true;
                ConfirmQuit();
            }
        }

        private void Build()
        {
            UIUtility.EnsureEventSystem();

            var canvas = UIUtility.CreateCanvas("QuitConfirmCanvas", transform, 100);
            SetRoot(canvas.gameObject);

            var root = canvas.transform;
            var overlay = UIUtility.CreateImage("Overlay", root, UIUtility.Overlay);
            UIUtility.Stretch(overlay.GetComponent<RectTransform>());

            var panel = MenuArt.CreatePanel(root, "QuitConfirmBox", new Vector2(760f, 390f), Vector2.zero,
                MenuArt.QuitConfirmFrame);

            var title = UIUtility.CreateText("Title", panel, "退出游戏", 48,
                TextAnchor.MiddleCenter, UIUtility.TextMain);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(-80f, 80f);
            titleRt.anchoredPosition = new Vector2(0f, -42f);

            var message = UIUtility.CreateText("Message", panel,
                "确定要退出游戏吗？", 34, TextAnchor.MiddleCenter, UIUtility.TextBody);
            var messageRt = message.GetComponent<RectTransform>();
            messageRt.anchorMin = new Vector2(0f, 1f);
            messageRt.anchorMax = new Vector2(1f, 1f);
            messageRt.pivot = new Vector2(0.5f, 1f);
            messageRt.sizeDelta = new Vector2(-80f, 70f);
            messageRt.anchoredPosition = new Vector2(0f, -135f);

            _confirmButton = UIUtility.CreateButton("ConfirmButton", panel,
                "确定", new Vector2(250f, 72f), new Vector2(-145f, -245f), 30);
            _confirmButton.onClick.AddListener(ConfirmQuit);

            var cancelButton = UIUtility.CreateButton("CancelButton", panel,
                "取消", new Vector2(250f, 72f), new Vector2(145f, -245f), 30);
            cancelButton.onClick.AddListener(Cancel);

            MenuArt.ApplyButtonSkin(_confirmButton);
            MenuArt.ApplyButtonSkin(cancelButton);
        }

        private void ConfirmQuit()
        {
            UIUtility.PlayClickSfx();
#if UNITY_EDITOR
            Debug.Log("[QuitConfirm] 已确认退出游戏（编辑器模式下不真正退出）。");
#else
            Application.Quit();
#endif
        }

        private void Cancel()
        {
            UIUtility.PlayClickSfx();
            if (UIManager.HasInstance)
            {
                UIManager.Instance.Close(PanelId);
            }
        }
    }
}
