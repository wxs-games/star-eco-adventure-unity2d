using UnityEngine;
using UnityEngine.UI;
using StarEco.Framework;
using StarEco.Player;
using StarEco.UI;

namespace StarEco.Level
{
    /// <summary>
    /// 工业风拉杆开关（M11）：E 键扳动，联动收起地刺。
    /// 与 RescueTarget / DialogueTrigger 同构（半径检测 + 世界空间气泡 + KeyCode.E），
    /// 差异在动作：扳动后换开态贴图并调用目标 SpikeHazard.StartRetract()。
    /// 幂等：扳动后本组件停用，不可重复触发。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class LeverSwitch : MonoBehaviour
    {
        [Tooltip("关态贴图（未扳动）")]
        [SerializeField] private Sprite _offSprite;

        [Tooltip("开态贴图（已扳动）")]
        [SerializeField] private Sprite _onSprite;

        [Tooltip("被本开关控制的地刺（扳动后调用它的 StartRetract）")]
        [SerializeField] private SpikeHazard _target;

        [Tooltip("交互键，默认 E（与 M7 对话一致）")]
        [SerializeField] private KeyCode _interactKey = KeyCode.E;

        [Tooltip("交互半径（u）")]
        [SerializeField] private float _radius = 2.2f;

        [Tooltip("气泡相对拉杆高度")]
        [SerializeField] private float _bubbleHeight = 1.6f;

        private SpriteRenderer _renderer;
        private Transform _player;
        private bool _playerInRange;
        private GameObject _bubble;
        private bool _done;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (_offSprite != null)
            {
                _renderer.sprite = _offSprite;
            }

            // 气泡只在 Play 模式创建：编辑器下建进场景会在 Scene 视图里留一片白色线框
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
                    Destroy(canvasGo);
                }
            }
        }

        private void Update()
        {
            if (_done)
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

            // 暂停/面板打开时不可互动（UIManager 输入冻结收口，M7 语义）
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
            }

            if (!inRange)
            {
                return;
            }

            if (Input.GetKeyDown(_interactKey))
            {
                Activate();
            }
        }

        /// <summary>扳动拉杆：换开态贴图 → 收起地刺。</summary>
        public void Activate()
        {
            if (_done)
            {
                return;
            }
            _done = true;

            if (_renderer != null && _onSprite != null)
            {
                _renderer.sprite = _onSprite;
            }

            if (_bubble != null)
            {
                _bubble.SetActive(false);
            }

            if (_target != null)
            {
                _target.StartRetract();
            }

            // 玩家 Interact 动画（空值保护：无 Animator 或缺动画不阻断流程）
            var pc = _player != null ? _player.GetComponent<PlayerController>() : null;
            pc?.TryFireAnim(StarEco.Player.PlayerAnimParams.Interact);

            AudioManager.Instance?.PlaySfx(SfxId.PuzzleSolved);

            Debug.Log("[LeverSwitch] 拉杆已扳动，地刺开始收起");
        }

        // ===== 气泡（复刻 RescueTarget 的世界空间 Canvas 模式）=====

        private void BuildBubble()
        {
            var canvasGo = new GameObject("LeverBubbleCanvas",
                typeof(RectTransform), typeof(Canvas));
            UIUtility.CreateWorldCanvas(canvasGo, transform, new Vector3(0f, _bubbleHeight, 0f));

            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(400f, 120f);

            _bubble = UIUtility.CreateObject("Bubble", canvasGo.transform);
            UIUtility.CenterAnchor(_bubble, new Vector2(360f, 90f), Vector2.zero);
            var bubbleBg = UIUtility.CreateImage("Background", _bubble.transform, UIUtility.PanelBg);
            UIUtility.Stretch(bubbleBg.GetComponent<RectTransform>());

            var label = UIUtility.CreateText("Label", _bubble.transform, "按 E 扳动", 34,
                TextAnchor.MiddleCenter, UIUtility.TextMain);
            UIUtility.Stretch(label.GetComponent<RectTransform>());

            _bubble.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
