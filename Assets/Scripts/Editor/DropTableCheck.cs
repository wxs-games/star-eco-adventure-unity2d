using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StarEco.Eco;

namespace StarEco.EditorTools
{
    /// <summary>
    /// 掉落表配置自检：扫描三关场景，检查每个 DropSpawner 的掉落表及所需拾取预制体。
    /// 此工具用于
    /// 回归防漏：任何一关缺表会在 Console 逐条告警，全齐则输出 PASS。
    /// 只读：Additive 打开场景检查后立即关闭，不保存、不改场景。
    /// 菜单：StarEco → Tools → 检查掉落表配置
    /// </summary>
    public static class DropTableCheck
    {
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/Level01.unity",
            "Assets/Scenes/Level02.unity",
            "Assets/Scenes/Level03.unity"
        };

        [MenuItem("StarEco/Tools/检查掉落表配置")]
        public static void Run()
        {
            var log = new StringBuilder();
            int missing = 0;
            int total = 0;

            foreach (var path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var sp in root.GetComponentsInChildren<DropSpawner>(true))
                        {
                            total++;
                            var so = new SerializedObject(sp);
                            var table = so.FindProperty("_dropTable");
                            var prefab = so.FindProperty("_pickupPrefab");

                            if (table == null || prefab == null)
                            {
                                missing++;
                                log.AppendLine($"[MISS] {path} {sp.name}：字段缺失（脚本版本不符？）");
                                continue;
                            }

                            var configuredTable = table.objectReferenceValue as DropTable;
                            bool needsPurify = configuredTable != null && configuredTable.Entries.Exists(e => e.Kind == PickupKind.PurifyBottle);
                            bool missingPurify = needsPurify && prefab.objectReferenceValue == null;
                            if (table.objectReferenceValue == null || missingPurify)
                            {
                                missing++;
                                log.AppendLine($"[MISS] {path} {sp.name}：" +
                                    $"_dropTable={(table.objectReferenceValue == null ? "null" : "ok")} " +
                                    $"_pickupPrefab={(missingPurify ? "null" : "ok")}");
                            }
                        }
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            if (missing == 0)
            {
                Debug.Log($"[DropTableCheck] PASS —— {total} 个 DropSpawner 全部已配表。");
            }
            else
            {
                Debug.LogWarning($"[DropTableCheck] {missing}/{total} 个 DropSpawner 配置缺失：\n{log}");
            }
        }
    }
}
