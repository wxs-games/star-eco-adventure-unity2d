using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StarEco.Level;

namespace StarEco.UI
{
    /// <summary>
    /// 主菜单面板（M7 R3）：Title 场景根面板。
    /// 按钮：开始新游戏 / 继续游戏 / 关卡选择 / 图鉴 / 设置 / 退出游戏。
    /// </summary>
    public class MainMenuPanel : UIPanel
    {
        public const string PanelId = "MainMenu";

        public override string PanelName => PanelId;

        private Button _continueButton;
        private Text _versionText;

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
            if (UIManager.HasInstance)
            {
                UIManager.Instance.Unregister(this);
            }
        }

        private void Start()
        {
            // 与 HUD 同款时序兜底：Awake 早于 UIManager.Awake 时注册会丢；
            // Start 也可能早于 UIManager.Start 的兜底扫描，故这里先幂等注册再打开。
            if (UIManager.HasInstance)
            {
                UIManager.Instance.Register(this);
                UIManager.Instance.Open(PanelId);
            }

            PlayTitleBgm();
        }

        protected override void OnOpen()
        {
            RefreshContinueState();
        }

        // ===== 构建 =====

        private void Build()
        {
            UIUtility.EnsureEventSystem();

            var canvas = UIUtility.CreateCanvas("MainMenuCanvas", transform, 20);
            Root = canvas.gameObject;

            var root = canvas.transform;

            var bg = UIUtility.CreateImage("Background", root, new Color(0.06f, 0.10f, 0.09f, 1f));
            UIUtility.Stretch(bg.GetComponent<RectTransform>());

            // 顶部氛围色带（暗示污染→清新的主题）
            var band = UIUtility.CreateImage("Band", root,
                new Color(0.29f, 0.60f, 0.36f, 0.35f));
            var bandRt = band.GetComponent<RectTransform>();
            bandRt.anchorMin = new Vector2(0f, 1f);
            bandRt.anchorMax = new Vector2(1f, 1f);
            bandRt.pivot = new Vector2(0.5f, 1f);
            bandRt.sizeDelta = new Vector2(0f, 220f);
            bandRt.anchoredPosition = Vector2.zero;

            var title = UIUtility.CreateText("Title", root, "星际环保", 96,
                TextAnchor.MiddleCenter, UIUtility.TextMain);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(0f, 130f);
            titleRt.anchoredPosition = new Vector2(0f, -60f);

            var subtitle = UIUtility.CreateText("Subtitle", root, "—— 动作冒险 · 毕业设计 ——", 32,
                TextAnchor.MiddleCenter, UIUtility.AccentGreen);
            var subtitleRt = subtitle.GetComponent<RectTransform>();
            subtitleRt.anchorMin = new Vector2(0.5f, 1f);
            subtitleRt.anchorMax = new Vector2(0.5f, 1f);
            subtitleRt.pivot = new Vector2(0.5f, 1f);
            subtitleRt.sizeDelta = new Vector2(0f, 50f);
            subtitleRt.anchoredPosition = new Vector2(0f, -190f);

            // 按钮列
            var column = UIUtility.CreateObject("Buttons", root);
            var columnRt = column.GetComponent<RectTransform>();
            columnRt.anchorMin = new Vector2(0.5f, 0.5f);
            columnRt.anchorMax = new Vector2(0.5f, 0.5f);
            columnRt.pivot = new Vector2(0.5f, 0.5f);
            columnRt.sizeDelta = new Vector2(360f, 480f);
            columnRt.anchoredPosition = new Vector2(0f, -40f);

            var startButton = UIUtility.CreateButton("StartButton", column.transform, "开始新游戏",
                new Vector2(340f, 74f), new Vector2(0f, 200f));
            startButton.onClick.AddListener(OnStartClicked);

            _continueButton = UIUtility.CreateButton("ContinueButton", column.transform, "继续游戏",
                new Vector2(340f, 74f), new Vector2(0f, 110f));
            _continueButton.onClick.AddListener(OnContinueClicked);

            var levelSelectButton = UIUtility.CreateButton("LevelSelectButton", column.transform, "关卡选择",
                new Vector2(340f, 74f), new Vector2(0f, 20f));
            levelSelectButton.onClick.AddListener(() =>
            {
                UIUtility.PlayClickSfx();
                UIManager.SafeOpen(LevelSelectPanel.PanelId);
            });

            var codexButton = UIUtility.CreateButton("CodexButton", column.transform, "环保图鉴",
                new Vector2(340f, 74f), new Vector2(0f, -70f));
            codexButton.onClick.AddListener(() =>
            {
                UIUtility.PlayClickSfx();
                UIManager.SafeOpen(CodexPanel.PanelId);
            });

            var settingsButton = UIUtility.CreateButton("SettingsButton", column.transform, "设置",
                new Vector2(340f, 74f), new Vector2(0f, -160f));
            settingsButton.onClick.AddListener(() =>
            {
                UIUtility.PlayClickSfx();
                UIManager.SafeOpen(SettingsPanel.PanelId);
            });

            var quitButton = UIUtility.CreateButton("QuitButton", column.transform, "退出游戏",
                new Vector2(340f, 74f), new Vector2(0f, -250f));
            quitButton.onClick.AddListener(OnQuitClicked);

            _versionText = UIUtility.CreateText("Version", root,
                "Unity 2021.3 · M7 原型", 22, TextAnchor.LowerRight,
                new Color(1f, 1f, 1f, 0.35f));
            var versionRt = _versionText.GetComponent<RectTransform>();
            versionRt.anchorMin = new Vector2(1f, 0f);
            versionRt.anchorMax = new Vector2(1f, 0f);
            versionRt.pivot = new Vector2(1f, 0f);
            versionRt.sizeDelta = new Vector2(420f, 34f);
            versionRt.anchoredPosition = new Vector2(-16f, 10f);
        }

        // ===== 交互 =====

        private void OnStartClicked()
        {
            UIUtility.PlayClickSfx();

            // 新游戏：清进度与图鉴收集、清结算，回到第一关
            LevelSaveService.ClearProgress();
            CodexService.ResetProgress();
            LevelSaveService.ClearResult();
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneNames.Level01);
        }

        private void OnContinueClicked()
        {
            UIUtility.PlayClickSfx();

            // M9：三关均建成，继续游戏 = 进入已解锁的最高关卡（通关即解锁下一关）
            var progress = LevelSaveService.LoadProgress();
            int target = Mathf.Clamp(progress.UnlockedLevelIndex, 1, 3);
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneNames.ForLevel(target));
            Debug.Log($"[MainMenu] 继续游戏：进入第 {target} 关（{SceneNames.ForLevel(target)}）。");
        }

        private void OnQuitClicked()
        {
            UIUtility.PlayClickSfx();
#if UNITY_EDITOR
            Debug.Log("[MainMenu] 退出游戏（编辑器模式下不真正退出）。");
#else
            Application.Quit();
#endif
        }

        private void RefreshContinueState()
        {
            if (_continueButton == null)
            {
                return;
            }

            bool hasProgress = LevelSaveService.HasProgress();
            _continueButton.interactable = hasProgress;
        }

        private void PlayTitleBgm()
        {
            // BGM 资源缺失时 AudioManager 内部静默跳过（M10 铺量）
            if (StarEco.Framework.AudioManager.HasInstance)
            {
                StarEco.Framework.AudioManager.Instance.PlayBgm(null);
            }
        }
    }

    /// <summary>场景名常量（避免散落字符串）。</summary>
    public static class SceneNames
    {
        public const string Title = "Title";
        public const string Level01 = "Level01";
        public const string Level02 = "Level02";
        public const string Level03 = "Level03";

        /// <summary>关卡序号（1 起）→ 场景名。LevelIndex 与 Build Settings 索引一致（Title=0）。</summary>
        public static string ForLevel(int levelIndex)
        {
            switch (Mathf.Clamp(levelIndex, 1, 3))
            {
                case 2: return Level02;
                case 3: return Level03;
                default: return Level01;
            }
        }
    }
}
