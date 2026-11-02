using UnityEngine;
using StarEco.Framework;
using StarEco.UI;

namespace StarEco.Level
{
    /// <summary>
    /// 钥匙物品（L2工厂关）：玩家触碰后获得钥匙，可打开 KeyDoor。
    /// 触碰后显示 ItemGetUI 获得物品浮层，随后销毁自身。
    /// 玩家是否持有钥匙通过 KeyDoor.HasKey 静态属性查询。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class KeyItem : MonoBehaviour
    {
        [Tooltip("获得物品浮层显示时长（秒）")]
        [SerializeField] private float _showDuration = 5f;

        private bool _collected;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_collected)
            {
                return;
            }

            // 只有玩家本体可拾取
            if (!other.CompareTag("Player"))
            {
                return;
            }

            _collected = true;
            KeyDoor.SetHasKey(true);

            // 显示获得物品浮层（钥匙图标留空，用默认占位色块）
            ItemGetUI.Show(null, _showDuration, null);

            AudioManager.Instance?.PlaySfx(SfxId.Rescue);

            Debug.Log("[KeyItem] 玩家拾取了钥匙，KeyDoor.HasKey = true");

            // 隐藏自身（不立即销毁，避免 ItemGetUI 引用失效）
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.enabled = false;
            }
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.enabled = false;
            }

            Destroy(gameObject, _showDuration);
        }
    }
}
