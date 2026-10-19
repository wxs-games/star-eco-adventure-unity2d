using System.Text;
using UnityEditor;
using UnityEngine;

namespace StarEco.EditorTools
{
    /// <summary>
    /// Player 预制体战斗/环保组件固化（T17 依赖）。
    /// 把战斗、喷雾、近战判定等组件统一固化到 Player.prefab。
    /// </summary>
    public static class PlayerPrefabSetup
    {
        private const string PrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string SprayHitboxName = "SprayHitbox";
        private const string AttackHitboxName = "AttackHitbox";

        [MenuItem("StarEco/Setup/Player Prefab")]
        public static void Run()
        {
            var log = new StringBuilder();

            var combatConfig = AssetDatabase.LoadAssetAtPath<StarEco.Combat.CombatConfig>("Assets/Settings/CombatConfig.asset");
            var ecoConfig = AssetDatabase.LoadAssetAtPath<StarEco.Eco.EcoConfig>("Assets/Settings/EcoConfig.asset");
            var fxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FX/fx_purify.prefab");
            var fxOneShot = fxPrefab != null ? fxPrefab.GetComponent<StarEco.Eco.FxOneShot>() : null;

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var combat = root.GetComponent<StarEco.Combat.PlayerCombat>();
                if (combat == null)
                {
                    combat = root.AddComponent<StarEco.Combat.PlayerCombat>();
                    log.AppendLine("已添加 PlayerCombat");
                }

                var tank = root.GetComponent<StarEco.Eco.PurifyTank>();
                if (tank == null)
                {
                    tank = root.AddComponent<StarEco.Eco.PurifyTank>();
                    log.AppendLine("已添加 PurifyTank");
                }

                var bodyCol = root.GetComponent<BoxCollider2D>();
                float halfW = bodyCol != null ? bodyCol.size.x * 0.5f : 0.8f;

                // 近战判定盒：盒体从身体右缘向前铺开，而非从身体中心外推。
                // 半宽 1.2，故中心置于 halfW + 0.35，盒左缘正好贴住身体右缘（无空隙），
                // 盒右缘伸到 halfW + 1.55 ≈ 2.4 单位处，覆盖一段挥击距离。
                const float AttackHalfW = 1.2f;
                float attackCenterX = halfW + AttackHalfW * 0.3f;

                var sprayHitboxGo = root.transform.Find(SprayHitboxName);
                if (sprayHitboxGo == null)
                {
                    var child = new GameObject(SprayHitboxName);
                    child.transform.SetParent(root.transform, false);
                    child.transform.localPosition = new Vector3(halfW + 0.55f, 0f, 0f);
                    child.layer = root.layer;

                    var col = child.AddComponent<BoxCollider2D>();
                    col.isTrigger = true;
                    col.size = new Vector2(1.4f, 1.2f);
                    child.AddComponent<StarEco.Eco.SprayHitbox>();

                    sprayHitboxGo = child.transform;
                    log.AppendLine("已创建 SprayHitbox 子物体（size 1.4x1.2）");
                }
                else
                {
                    log.AppendLine("SprayHitbox 子物体已存在");
                }

                var attackHitboxGo = root.transform.Find(AttackHitboxName);
                if (attackHitboxGo == null)
                {
                    var child = new GameObject(AttackHitboxName);
                    child.transform.SetParent(root.transform, false);
                    child.transform.localPosition = new Vector3(attackCenterX, 0.05f, 0f);
                    child.layer = root.layer;

                    var col = child.AddComponent<BoxCollider2D>();
                    col.isTrigger = true;
                    col.size = new Vector2(AttackHalfW * 2f, 1.5f);
                    col.enabled = false;

                    attackHitboxGo = child.transform;
                    log.AppendLine($"已创建 AttackHitbox 子物体（size {AttackHalfW * 2f}x1.5，中心 x={attackCenterX:F2}）");
                }
                else
                {
                    attackHitboxGo.localPosition = new Vector3(attackCenterX, 0.05f, 0f);
                    attackHitboxGo.gameObject.layer = root.layer;
                    var col = attackHitboxGo.GetComponent<BoxCollider2D>() ?? attackHitboxGo.gameObject.AddComponent<BoxCollider2D>();
                    col.isTrigger = true;
                    col.size = new Vector2(AttackHalfW * 2f, 1.5f);
                    col.enabled = false;
                    log.AppendLine($"AttackHitbox 子物体已校正（中心 x={attackCenterX:F2}）");
                }

                var attackHitbox = root.GetComponent<StarEco.Combat.AttackHitbox>();
                if (attackHitbox == null)
                {
                    attackHitbox = root.AddComponent<StarEco.Combat.AttackHitbox>();
                    log.AppendLine("已添加 AttackHitbox 组件");
                }

                var spray = root.GetComponent<StarEco.Eco.SprayController>();
                if (spray == null)
                {
                    spray = root.AddComponent<StarEco.Eco.SprayController>();
                    log.AppendLine("已添加 SprayController");
                }

                Inject(combat, "_config", combatConfig);
                Inject(tank, "_config", ecoConfig);
                Inject(attackHitbox, "_hitbox", attackHitboxGo.GetComponent<Collider2D>());

                var spraySo = new SerializedObject(spray);
                SetIfPresent(spraySo, "_config", ecoConfig);
                SetIfPresent(spraySo, "_fxPurifyPrefab", fxOneShot);
                SetIfPresent(spraySo, "_hitbox", sprayHitboxGo.GetComponent<StarEco.Eco.SprayHitbox>());
                spraySo.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[PlayerPrefabSetup] Player 预制体组件固化完成 ✓\n" + log);
        }

        private static void Inject(Component target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SetIfPresent(so, field, value);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetIfPresent(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
            }
            else
            {
                Debug.LogWarning($"[PlayerPrefabSetup] 字段 {field} 不存在（脚本已变更？）");
            }
        }
    }
}
