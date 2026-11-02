using UnityEngine;
using UnityEngine.SceneManagement;
using StarEco.Framework;
using StarEco.Level;
using StarEco.Eco.Events;

namespace StarEco.Eco
{
    /// <summary>
    /// 知识卡片拾取物（M7 R8）：玩家碰到即收集，写图鉴进度并隐藏自身（幂等）。
    /// 场景中每张卡片一个实例；CardId 对应 knowledge_cards.json 的 Id。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class KnowledgeCardPickup : MonoBehaviour
    {
        [Tooltip("卡片 Id（对应 Resources/Data/knowledge_cards.json 的 Id）")]
        [SerializeField] private string _cardId = "kc_plastic_bottle";

        [Tooltip("是否已在本局被拾取（场景保存用，避免重载前重复）")]
        [SerializeField] private bool _taken;

        private bool _collected;
        private SpriteRenderer _sprite;

        // 进入第 N 关时，第 N 关及后续关卡重新收集；前置关卡沿用历史状态。
        private int CurrentLevelIndex
        {
            get
            {
                switch (SceneManager.GetActiveScene().name)
                {
                    case "Level01": return 1;
                    case "Level02": return 2;
                    case "Level03": return 3;
                    default: return 1;
                }
            }
        }

        private bool ResetsForCurrentRun
        {
            get
            {
                var card = UI.CodexService.Find(_cardId);
                return card != null && card.LevelIndex >= CurrentLevelIndex;
            }
        }

        private void Awake()
        {
            _sprite = GetComponent<SpriteRenderer>();
            NormalizeTriggerRadius();
        }

        /// <summary>
        /// 把触发半径归一到目标世界尺寸（默认 0.6u）。
        /// 场景搭建用 WhiteSquare 放大色块（bounds 仅 0.04u，scale≈12.5），
        /// 不归一的话 radius=0.6 会变成 7.5u 世界半径，把大片区域算进触发区。
        /// </summary>
        private void NormalizeTriggerRadius()
        {
            var col = GetComponent<CircleCollider2D>();
            if (col == null)
            {
                return;
            }

            float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
            if (scale > 0.0001f)
            {
                col.radius = 0.6f / scale;
            }
        }

        private void Start()
        {
            // 当前关及后续关卡不读取历史状态；前置关卡按永久图鉴状态隐藏。
            _collected = _taken || (!ResetsForCurrentRun && UI.CodexService.IsCollected(_cardId));
            if (_collected && _sprite != null)
            {
                _sprite.enabled = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_collected)
            {
                return;
            }

            var player = other.GetComponent<StarEco.Player.PlayerController>();
            if (player == null)
            {
                return;
            }

            var card = UI.CodexService.Find(_cardId);
            if (card == null)
            {
                Debug.LogError($"[KnowledgeCardPickup] 未知卡片 Id：{_cardId}");
                return;
            }

            bool isNew = UI.CodexService.CollectCard(_cardId);
            bool shouldShowPickupFeedback = isNew || ResetsForCurrentRun;

            // 关卡机关需要区分“本次进入是否实际拾取”，不能直接读取永久图鉴存档。
            EventCenter.Publish(new KnowledgeCardCollectedEvent
            {
                CardId = _cardId,
                Position = transform.position
            });
            _collected = true;
            _taken = true;

            // 先取精灵引用：下面会关掉渲染器，但 sprite 资源本身仍可复用
            var cardSprite = _sprite != null ? _sprite.sprite : null;

            if (_sprite != null)
            {
                _sprite.enabled = false;
            }

            // 拾取反馈（B2）：拾取音 + 一行短提示（ToastLayer 运行时自举，无需场景挂载）
            AudioManager.Instance?.PlaySfx(SfxId.Pickup);
            UI.ToastLayer.Show($"获得知识卡：{card.Title}", UI.UIUtility.AccentGreen);

            // 塞尔达风格：角色欢呼 + 头顶物品升起 → 稍后再弹物品获得面板
            // （面板带全屏遮罩且居中，会盖住角色和升起特效，所以必须等演出放映完再弹）
            if (shouldShowPickupFeedback)
            {
                player.PlayItemGetAnim(2f);
                ItemRaiseFx.Play(cardSprite, player.Sprite);
                StartCoroutine(ShowPanelDelayed(card, cardSprite, 1.6f));
                Debug.Log($"[KnowledgeCardPickup] 获得知识卡片：{card.Title}");
            }
        }

        /// <summary>让「物品升起」先被看到，再弹出信息面板。</summary>
        private System.Collections.IEnumerator ShowPanelDelayed(
            UI.KnowledgeCardData card, Sprite icon, float delay)
        {
            yield return new WaitForSeconds(delay);
            UI.ItemGetUI.Show(card, 5f, icon);
        }

        private void OnValidate()
        {
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }
    }
}
