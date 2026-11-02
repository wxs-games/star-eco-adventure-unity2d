using UnityEngine;
using UnityEngine.UI;
using StarEco.Player;
using StarEco.Framework;
using StarEco.UI;

namespace StarEco.Level
{
    /// <summary>
    /// 需要钥匙的门（L2工厂关）：玩家持有钥匙（KeyDoor.HasKey=true）时按 E 打开门，
    /// 门体消失并开放通路；未持有钥匙时显示"需要钥匙"气泡提示。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class KeyDoor : MonoBehaviour
    {
        /// <summary>全局钥匙持有状态（KeyItem 拾取后设为 true）。</summary>
        public static bool HasKey { get; private set; }

        /// <summary>设置钥匙持有状态（供 KeyItem 调用）。</summary>
        public static void SetHasKey(bool value) => HasKey = value;

        [Tooltip("交互键，默认 E")]
        [SerializeField] private KeyCode _interactKey = KeyCode.E;

        [Tooltip("交互半径（u）")]
        [SerializeField] private float _radius = 2.5f;

        [Tooltip("气泡相对门体高度")]
        [SerializeField] private float _bubbleHeight = 1.8f;

        [Tooltip("门打开后是否销毁门对象（false 则仅隐藏渲染与碰撞体）")]
        [SerializeField] private bool _destroyOnOpen = false;

        [Tooltip("需要钥匙时的提示文本")]
        [SerializeField] private string _needKeyHint = "需要钥匙才能打开";

        [Tooltip("可打开时的提示文本")]
        [SerializeField] private string _canOpenHint = "按 E 打开门";

        private SpriteRenderer _renderer;
        private Collider2D _collider;
        private Transform _player;
        private bool _playerInRange;
        private GameObject _bubble;
        private Text _bubbleText;
        private bool _opened;

        private static int _lastHintFrame = -1;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<Collider2D>();
            BuildBubble();
        }

        private void OnEnable()
        {
            if (_bubble == null)
            {
                BuildBubble();
            }
        }

        private void OnDisable()
        {
            if (_bubble != null)
            {
                _bubble.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_bubble != null)
            {
                var canvasGo = _bubble.transform.parent != null
                    ? _bubble.transform.parent.gameObject
                    : _bubble.gameObject;
                if (canvasGo != gameObject)
                {
                    UnityEngine.Object.Destroy(canvasGo);
                }
            }
        }

        private void Update()
        {
            if (_opened)
            {
                return;
            }

            if (_player == null)
            {
                var pc = FindObjectOfType<PlayerController>();
                if (pc == null)
                {
                    return;
                }
                _player = pc.transform;
            }

            // 暂停/面板打开时不可互动
            if (UIManager.HasInstance && UIManager.Instance.IsBlocking)
            {
                return;
            }

            bool inRange = Vector2.Distance(transform.position, _player.position) <= _radius;
            if (inRange != _playerInRange)
            {
                _playerInRange = inRange;
                if (_bubble != null)
                {
                    _bubble.SetActive(inRange);
                }
                UpdateBubbleText();
            }

            if (!inRange)
            {
                return;
            }

            if (Input.GetKeyDown(_interactKey))
            {
                TryOpen();
            }
        }

        private void TryOpen()
        {
            if (HasKey)
            {
                OpenDoor();
            }
            else
            {
                ShowNeedKeyHint();
            }
        }

        private void OpenDoor()
        {
            _opened = true;
            if (_bubble != null)
            {
                _bubble.SetActive(false);
            }

            if (_destroyOnOpen)
            {
                Destroy(gameObject);
            }
            else
            {
                if (_renderer != null)
                {
                    _renderer.enabled = false;
                }
                if (_collider != null)
                {
                    _collider.enabled = false;
                }
            }

            AudioManager.Instance?.PlaySfx(SfxId.PuzzleSolved);
            Debug.Log("[KeyDoor] 门已打开，通路开放");
        }

        private void ShowNeedKeyHint()
        {
            // 同一帧内只显示一次提示，避免刷屏
            if (Time.frameCount == _lastHintFrame)
            {
                return;
            }
            _lastHintFrame = Time.frameCount;

            if (_bubbleText != null)
            {
                _bubbleText.text = _needKeyHint;
            }
            if (_bubble != null)
            {
                _bubble.SetActive(true);
            }

            // 1.5 秒后恢复正常提示
            Invoke(nameof(ResetBubbleText), 1.5f);
        }

        private void UpdateBubbleText()
        {
            if (_bubbleText == null)
            {
                return;
            }
            _bubbleText.text = HasKey ? _canOpenHint : _needKeyHint;
        }

        private void ResetBubbleText()
        {
            UpdateBubbleText();
        }

        // ===== 气泡 =====

        private void BuildBubble()
        {
            var canvasGo = new GameObject("KeyDoorBubbleCanvas",
                typeof(RectTransform), typeof(Canvas));
            UIUtility.CreateWorldCanvas(canvasGo, transform, new Vector3(0f, _bubbleHeight, 0f));

            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(400f, 120f);

            _bubble = UIUtility.CreateObject("Bubble", canvasGo.transform);
            UIUtility.CenterAnchor(_bubble, new Vector2(360f, 90f), Vector2.zero);
            var bubbleBg = UIUtility.CreateImage("Background", _bubble.transform, UIUtility.PanelBg);
            UIUtility.Stretch(bubbleBg.GetComponent<RectTransform>());

            _bubbleText = UIUtility.CreateText("Label", _bubble.transform, HasKey ? _canOpenHint : _needKeyHint, 34,
                TextAnchor.MiddleCenter, UIUtility.TextMain);
            UIUtility.Stretch(_bubbleText.GetComponent<RectTransform>());

            _bubble.SetActive(false);
        }
    }
}
