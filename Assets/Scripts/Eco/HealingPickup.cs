using UnityEngine;
using StarEco.Combat;
using StarEco.Framework;

namespace StarEco.Eco
{
    /// <summary>治疗拾取物：只在实际回复生命值时才被消耗。</summary>
    public class HealingPickup : MonoBehaviour
    {
        [SerializeField] private int _healAmount = 2;

        private HealingPickup _sourcePrefab;

        public void Init(HealingPickup sourcePrefab)
        {
            _sourcePrefab = sourcePrefab;
        }

        public static void Spawn(HealingPickup prefab, Vector3 position)
        {
            if (prefab == null || PoolManager.Instance == null)
            {
                Debug.LogWarning("[HealingPickup] 预制体或 PoolManager 缺失，跳过生成。");
                return;
            }

            var pickup = PoolManager.Instance.Get<HealingPickup>(prefab);
            pickup.transform.position = position;
            pickup.Init(prefab);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var combat = other.GetComponentInParent<PlayerCombat>();
            if (combat == null)
            {
                return;
            }

            int applied = combat.Heal(_healAmount);
            if (applied <= 0)
            {
                return;
            }

            AudioManager.Instance?.PlaySfx(SfxId.Pickup);
            EventCenter.Publish(new Events.PickupCollectedEvent
            {
                Kind = PickupKind.HealthBottle,
                Amount = applied
            });
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (_sourcePrefab != null && PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(_sourcePrefab, this);
                return;
            }

            gameObject.SetActive(false);
        }
    }
}
