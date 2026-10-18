using System.Text;
using UnityEditor;
using UnityEngine;

namespace StarEco.EditorTools
{
    /// <summary>
    /// Player 预制体战斗/环保组件固化（T17 依赖）：
    /// M3/M5 开发时这些组件是直接挂在 SampleScene 的 Player 实例上的，导致 Player.prefab 本身
    /// 只有 M2 的输入/物理组件 —— 任何新场景实例化该预制体都会缺少战斗与环保功能。
    /// 本工具把它们补进预制体并注入配置资产，使预制体成为唯一事实来源。
    /// 实现走 PrefabUtility.LoadPrefabContents 隔离上下文（预制体资产禁止直接增删子物体）。
    /// 菜单：StarEco → Setup → Player Prefab（幂等，可重复运行）
    /// </summary>
    public static class PlayerPrefabSetup
    {
        private const string PrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string HitboxName = "SprayHitbox";

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
                // —— PlayerCombat（M3）——
                var combat = root.GetComponent<StarEco.Combat.PlayerCombat>();
                if (combat == null)
                {
                    combat = root.AddComponent<StarEco.Combat.PlayerCombat>();
                    log.AppendLine("已添加 PlayerCombat");
                }

                // —— PurifyTank（M5）——
                var tank = root.GetComponent<StarEco.Eco.PurifyTank>();
                if (tank == null)
                {
                    tank = root.AddComponent<StarEco.Eco.PurifyTank>();
                    log.AppendLine("已添加 PurifyTank");
                }

                // —— SprayHitbox 子物体（M5 判定盒）——
                var hitboxGo = root.transform.Find(HitboxName);
                if (hitboxGo == null)
                {
                    var child = new GameObject(HitboxName);
                    child.transform.SetParent(root.transform, false);
                    // Bottom Center pivot 下 y=0 是脚底；喷射源自手持净化器，
                    // 抬到身体中下部（碰撞体高约 2，喷射口约在 45% 高度）。
                    // size(1.4,1.2) 是与 PPU 无关的手写世界单位，无需随 PPU 缩放。
                    child.transform.localPosition = new Vector3(0.55f, 0.85f, 0f);
                    child.layer = root.layer;

                    var col = child.AddComponent<BoxCollider2D>();
                    col.isTrigger = true;
                    col.size = new Vector2(1.4f, 1.2f);
                    child.AddComponent<StarEco.Eco.SprayHitbox>();

                    hitboxGo = child.transform;
                    log.AppendLine("已创建 SprayHitbox 子物体（localPos 0.55,0.85，size 1.4x1.2，Trigger）");
                }
                else
                {
                    log.AppendLine("SprayHitbox 子物体已存在");
                }

                // —— SprayController（M5）——
                var spray = root.GetComponent<StarEco.Eco.SprayController>();
                if (spray == null)
                {
                    spray = root.AddComponent<StarEco.Eco.SprayController>();
                    log.AppendLine("已添加 SprayController");
                }

                // —— 注入配置与引用（私有字段）——
                Inject(combat, "_config", combatConfig);
                Inject(tank, "_config", ecoConfig);

                var spraySo = new SerializedObject(spray);
                SetIfPresent(spraySo, "_config", ecoConfig);
                SetIfPresent(spraySo, "_fxPurifyPrefab", fxOneShot);
                SetIfPresent(spraySo, "_hitbox", hitboxGo.GetComponent<StarEco.Eco.SprayHitbox>());
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
