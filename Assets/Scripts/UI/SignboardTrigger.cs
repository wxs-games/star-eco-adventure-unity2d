using System.Collections.Generic;
using UnityEngine;
using StarEco.Player;

namespace StarEco.UI
{
    /// <summary>
    /// 提示牌触发器（M7 R9）：靠近按 E 打开阅读界面，整段文本分页，E 翻页，末页关闭。
    /// 与 NPC 对话的区别：不分句、无逐字、无头像；数据复用 DialogueData 单 Entry。
    /// 读牌 UI 走 SignboardPanel（UIPanel 派生，入栈），触发器只负责「请求打开 + 传数据」。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class SignboardTrigger : MonoBehaviour
    {

        [Tooltip("提示牌数据 Id（Resources/Data/dialogues.json，Entry 只有一条长文本）")]
        [SerializeField] private string _dialogueId = "signboard_plastic";

        [Tooltip("交互键，默认 E")]
        [SerializeField] private KeyCode _interactKey = KeyCode.E;

        [Tooltip("触发区半径（单位 u）")]
        [SerializeField] private float _radius = 5.0f;

        [Tooltip("气泡相对牌体高度")]
        [SerializeField] private float _bubbleHeight = 1.6f;

        private Transform _player;
        private bool _playerInRange;
        private GameObject _bubble;

        private void OnEnable()
        {
            BuildBubble();
        }

        private void OnDisable()
        {
            if (_bubble != null)
            {
                _bubble.SetActive(false);
            }
        }

        private void Update()
        {
            if (_player == null)
            {
                var pc = FindObjectOfType<PlayerController>();
                if (pc == null)
                {
                    return;
                }

                _player = pc.transform;
            }

            var signCollider = GetComponent<Collider2D>();
            float dist = signCollider != null
                ? Vector2.Distance(signCollider.ClosestPoint(_player.position), _player.position)
                : Vector2.Distance(transform.position, _player.position);
            bool inRange = dist <= _radius;

            if (inRange != _playerInRange)
            {
                _playerInRange = inRange;
                if (_bubble != null)
                {
                    _bubble.SetActive(inRange);
                }
            }

            if (!inRange)
            {
                return;
            }

            // 阅读中不再抢 E：这个键归 SignboardPanel 翻页/关闭，
            // 否则同一次按键会既翻页又重开面板。
            if (IsPanelReading())
            {
                return;
            }

            // 关闭用的那一次 E 不能立刻又把面板打开：
            // 面板与触发器的 Update 顺序不确定，关闭后本帧的 E 仍是按下状态。
            if (Time.unscaledTime - SignboardPanel.LastCloseUnscaledTime < 0.2f)
            {
                return;
            }

            // 另有面板在栈顶 → 读牌界面不吃 E
            // （对话/暂停/设置面板开着时，避免误触发读牌）
            if (UIManager.HasInstance && UIManager.Instance.IsBlocking)
            {
                return;
            }

            if (Input.GetKeyDown(_interactKey))
            {
                StartReading();
            }
        }

        /// <summary>提示牌面板当前是否正在阅读（决定本触发器是否让出 E 键）。</summary>
        private static bool IsPanelReading()
        {
            var panel = SignboardPanel.Instance;
            return panel != null && panel.IsReading;
        }

        private void StartReading()
        {
            var data = DialogueRepository.Find(_dialogueId);
            if (data == null || data.Entries == null || data.Entries.Count == 0)
            {
                Debug.LogError($"[SignboardTrigger] 未找到提示牌数据：{_dialogueId}");
                return;
            }

            var text = data.Entries[0].Text ?? string.Empty;

            // 走专用阅读面板：自带压暗遮罩 + 卡片底板 + 标题/正文/页脚，
            // 长文自动分页，避免文字直接叠在游戏画面上。
            var pages = Paginate(text, SignboardPanel.PageCharCount);
            var panel = SignboardPanel.EnsureInstance();
            if (panel == null)
            {
                Debug.LogError("[SignboardTrigger] SignboardPanel 创建失败，无法显示提示牌内容。");
                return;
            }

            panel.SetTitle(string.IsNullOrEmpty(data.Entries[0].Speaker) ? "提示牌" : data.Entries[0].Speaker);
            panel.StartReading(pages);

            if (_bubble != null)
            {
                _bubble.SetActive(false);
            }
        }

        private static List<string> Paginate(string text, int perPage)
        {
            var pages = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                pages.Add("（牌子上什么都没有）");
                return pages;
            }

            for (int i = 0; i < text.Length; i += perPage)
            {
                int len = Mathf.Min(perPage, text.Length - i);
                pages.Add(text.Substring(i, len));
            }

            return pages;
        }

        // ===== 气泡 =====

        private void BuildBubble()
        {
            var canvasGo = new GameObject("SignBubbleCanvas",
                typeof(RectTransform), typeof(Canvas));
            UIUtility.CreateWorldCanvas(canvasGo, transform, new Vector3(0f, _bubbleHeight, 0f));

            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(240f, 70f);    // 像素；已由 localScale 换算为 2.4×0.7 世界单位

            _bubble = UIUtility.CreateObject("Bubble", canvasGo.transform);
            UIUtility.CenterAnchor(_bubble, new Vector2(200f, 50f), Vector2.zero);
            var bubbleBg = UIUtility.CreateImage("Background", _bubble.transform, UIUtility.PanelBg);
            UIUtility.Stretch(bubbleBg.GetComponent<RectTransform>());

            var text = UIUtility.CreateText("Label", _bubble.transform, "按 E 查看", 24,
                TextAnchor.MiddleCenter, UIUtility.TextMain);
            UIUtility.Stretch(text.GetComponent<RectTransform>());

            _bubble.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.85f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
