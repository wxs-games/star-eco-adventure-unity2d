using UnityEngine;
using StarEco.EnemyAI.Events;
using StarEco.Framework;

namespace StarEco.Eco
{
    /// <summary>
    /// 掉落生成器（场景级）：订阅敌人死亡事件，按掉落表概率生成净化瓶。
    /// 掉落经对象池生成、±0.5u 随机偏移（y 微调防穿地）；表为空/缺失时安全跳过。
    /// </summary>
    public class DropSpawner : MonoBehaviour
    {
        [SerializeField] private DropTable _dropTable;

        [Tooltip("净化瓶预制体")]
        [SerializeField] private PurifyPickup _pickupPrefab;

        [Tooltip("回血瓶预制体")]
        [SerializeField] private HealingPickup _healingPickupPrefab;

        private void OnEnable()
        {
            EventCenter.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        private void OnDisable()
        {
            EventCenter.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            // 事件携带的表优先（敌人个体差异），否则用本组件默认表
            var table = evt.DropTable as DropTable ?? _dropTable;
            if (table == null || table.Entries == null || table.Entries.Count == 0)
            {
                return; // 空表安全跳过，不报错
            }

            foreach (var entry in table.Entries)
            {
                if (Random.value > entry.Probability)
                {
                    continue; // 每条目独立判定
                }

                for (int i = 0; i < entry.Count; i++)
                {
                    var offset = new Vector3(Random.Range(-0.5f, 0.5f), 0.15f, 0f);
                    Vector3 position = (Vector3)evt.Position + offset;
                    switch (entry.Kind)
                    {
                        case PickupKind.PurifyBottle:
                            PurifyPickup.Spawn(_pickupPrefab, position);
                            break;
                        case PickupKind.HealthBottle:
                            HealingPickup.Spawn(_healingPickupPrefab, position);
                            break;
                    }
                }
            }
        }
    }
}
