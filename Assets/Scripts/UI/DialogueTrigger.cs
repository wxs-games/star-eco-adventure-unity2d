using UnityEngine;
using UnityEngine.UI;
using StarEco.Player;

namespace StarEco.UI
{
    /// <summary>
    /// NPC 对话触发器（M7 R9）：玩家进入范围显示"按 E 交谈"气泡，按 E 打开对话面板。
    /// 与 DialoguePanel 的生命周期：触发器只负责"发起"，播放/推进全在面板内。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DialogueTrigger : MonoBehaviour
    {
        [Tooltip("对话数据 Id（Resources/Data/dialogues.json）")]
        [SerializeField] private string _dialogueId = "ava_intro";

        [Tooltip("交互键，默认 E")]
        [SerializeField] private KeyCode _interactKey = KeyCode.E;

        [Tooltip("触发区半径（单位 u）")]
        [SerializeField] private float _radius = 2.5f;

        [Tooltip("气泡相对触发器高度")]
        [SerializeField] private float _bubbleHeight = 1.8f;

        private Transform _player;
        private bool _playerInRange;
        private GameObject _bubble;
        private Text _bubbleText;
        private bool _playing;

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

            float dist = Vector2.Distance(transform.position, _player.position);
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

            // 对话播放中不重复触发（面板自己处理推进）
            var panel = FindObjectOfType<DialoguePanel>();
            bool panelOpen = panel != null && panel.IsOpen;
            if (!panelOpen && Input.GetKeyDown(_interactKey))
            {
                var data = DialogueRepository.Find(_dialogueId);
                if (data == null)
                {
                    Debug.LogError($"[DialogueTrigger] 未找到对话：{_dialogueId}");
                    return;
                }

                if (_bubble != null)
                {
                    _bubble.SetActive(false);
                }

                UIManager.SafeOpen(DialoguePanel.PanelId);
                panel = FindObjectOfType<DialoguePanel>();
                panel?.Play(data);
            }
        }

        private void BuildBubble()
        {
            UIUtility.EnsureEventSystem();

            var canvasGo = new GameObject("DialogueBubbleCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 0.01f;
            scaler.dynamicPixelsPerUnit = 10f;

            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(400f, 120f);
            canvasRt.localPosition = new Vector3(0f, _bubbleHeight, 0f);

            _bubble = UIUtility.CreateObject("Bubble", canvasGo.transform);
            UIUtility.CenterAnchor(_bubble, new Vector2(360f, 90f), Vector2.zero);
            var bubbleBg = UIUtility.CreateImage("Background", _bubble.transform, UIUtility.PanelBg);
            UIUtility.Stretch(bubbleBg.GetComponent<RectTransform>());

            _bubbleText = UIUtility.CreateText("Label", _bubble.transform, "按 E 交谈", 34,
                TextAnchor.MiddleCenter, UIUtility.TextMain);
            UIUtility.Stretch(_bubbleText.GetComponent<RectTransform>());

            _bubble.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 1f, 0.6f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
