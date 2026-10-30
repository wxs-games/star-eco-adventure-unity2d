using UnityEditor;
using UnityEngine;
using StarEco.Combat;
using StarEco.Eco;
using StarEco.EnemyAI;
using StarEco.Player;

namespace StarEco.EditorTools
{
    /// <summary>
    /// 打击特效一键装配（RiskRegister T-07）。
    ///
    /// 背景：fx_hit / fx_slash / fx_explode / fx_gas / fx_dust_run 五个 prefab 早已生成、
    /// 各自带 .anim + .controller，但按 GUID 全库检索**引用数全为 0**——
    /// 「文件存在 ≠ 已接入」。这是同类问题的第三次（前两次：检查点、FX prefab 6/6）。
    ///
    /// 本菜单把五个 prefab 灌进对应组件的 [SerializeField] 字段，一次解决引用为 0 的问题，
    /// 免去手拖 91 处引用。字段与调用点见 AnimationVFXSpec.md §8.3。
    ///
    /// 幂等：可重复运行，不会产生重复引用。
    /// </summary>
    public static class FxSetup
    {
        private const string FxDir = "Assets/Prefabs/FX/";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";

        [MenuItem("StarEco/Setup/打击特效接入（T-07）")]
        public static void Run()
        {
            var fxSlash = LoadFx("fx_slash");
            var fxHit = LoadFx("fx_hit");
            var fxExplode = LoadFx("fx_explode");
            var fxGas = LoadFx("fx_gas");
            var fxDust = LoadFx("fx_dust_run");

            var log = new System.Text.StringBuilder();
            log.AppendLine("[FxSetup] ===== 打击特效接入 =====");

            // —— 1. Player 预制体：AttackHitbox（斩击/命中）、PlayerAnimatorDriver（扬尘）——
            AssignToPrefab(PlayerPrefabPath, root =>
            {
                var hitbox = root.GetComponent<AttackHitbox>();
                if (hitbox != null)
                {
                    SetIfPresent(hitbox, "_fxSlashPrefab", fxSlash);
                    SetIfPresent(hitbox, "_fxHitPrefab", fxHit);
                    log.AppendLine($"  AttackHitbox: slash={(fxSlash != null ? "OK" : "缺")} hit={(fxHit != null ? "OK" : "缺")}");
                }
                else
                {
                    log.AppendLine("  AttackHitbox: 组件不存在（跳过）");
                }

                var driver = root.GetComponent<PlayerAnimatorDriver>();
                if (driver != null)
                {
                    SetIfPresent(driver, "_fxDustRunPrefab", fxDust);
                    log.AppendLine($"  PlayerAnimatorDriver: dust={(fxDust != null ? "OK" : "缺")}");
                }
                else
                {
                    log.AppendLine("  PlayerAnimatorDriver: 组件不存在（跳过）");
                }
            });

            // —— 2. 敌人预制体：EnemyControllerBase（命中火花/死亡爆炸）——
            // Enemy02 额外挂 fx_gas（喷毒），字段声明在 Enemy02Controller 上。
            AssignEnemyFx("Assets/Prefabs/Enemies/Enemy01.prefab", fxHit, fxExplode, fxGas, log, isEnemy02: false);
            AssignEnemyFx("Assets/Prefabs/Enemies/Enemy02.prefab", fxHit, fxExplode, fxGas, log, isEnemy02: true);
            AssignEnemyFx("Assets/Prefabs/Enemies/Enemy03.prefab", fxHit, fxExplode, fxGas, log, isEnemy02: false);

            // —— 3. Boss 预制体：命中火花 + 死亡爆炸 ——
            AssignEnemyFx("Assets/Prefabs/Boss/Boss01.prefab", fxHit, fxExplode, fxGas, log, isEnemy02: false);

            // —— 4. 塔：命中火花 + 死亡爆炸（Tower01Controller 继承 EnemyControllerBase）——
            AssignEnemyFx("Assets/Prefabs/Towers/Tower01.prefab", fxHit, fxExplode, fxGas, log, isEnemy02: false);

            // —— 5. 场景内的敌人实例（场景里可能有直接摆的敌人，不是 prefab 实例）——
            AssignSceneInstances(fxHit, fxExplode, fxGas, log);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.AppendLine("[FxSetup] 完成。剩余：prefab 的 sortingOrder 与材质需 A 侧处理（属 Prefabs 车道）。");
            Debug.Log(log.ToString());
        }

        private static FxOneShot LoadFx(string name)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(FxDir + name + ".prefab");
            if (go == null)
            {
                Debug.LogWarning($"[FxSetup] 找不到特效 prefab {FxDir}{name}.prefab");
                return null;
            }
            return go.GetComponent<FxOneShot>();
        }

        private static void AssignEnemyFx(string prefabPath, FxOneShot fxHit, FxOneShot fxExplode,
            FxOneShot fxGas, System.Text.StringBuilder log, bool isEnemy02)
        {
            AssignToPrefab(prefabPath, root =>
            {
                // EnemyControllerBase 的字段是 protected，用 SerializedObject 写更直接
                foreach (var controller in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (controller == null) continue;

                    if (controller is Enemy02Controller)
                    {
                        SetIfPresent(controller, "_fxGasPrefab", fxGas);
                    }

                    if (controller is EnemyControllerBase)
                    {
                        SetIfPresent(controller, "_fxHitPrefab", fxHit);
                        SetIfPresent(controller, "_fxExplodePrefab", fxExplode);
                    }
                }

                log.AppendLine($"  {System.IO.Path.GetFileName(prefabPath)}: " +
                               $"hit={(fxHit != null ? "OK" : "缺")} explode={(fxExplode != null ? "OK" : "缺")}" +
                               (isEnemy02 ? $" gas={(fxGas != null ? "OK" : "缺")}" : ""));
            });
        }

        /// <summary>场景里直接摆的敌人（不是 prefab 实例）也要接上。</summary>
        private static void AssignSceneInstances(FxOneShot fxHit, FxOneShot fxExplode,
            FxOneShot fxGas, System.Text.StringBuilder log)
        {
            var scenePaths = new[]
            {
                "Assets/Scenes/Level01.unity",
                "Assets/Scenes/Level02.unity",
                "Assets/Scenes/Level03.unity"
            };

            int touched = 0;
            foreach (var path in scenePaths)
            {
                if (!System.IO.File.Exists(path)) continue;

                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                if (!scene.IsValid()) continue;

                bool dirty = false;
                foreach (var controller in Object.FindObjectsOfType<MonoBehaviour>(true))
                {
                    if (controller == null) continue;

                    bool isEnemy02 = controller is Enemy02Controller;
                    if (!isEnemy02 && !(controller is EnemyControllerBase)) continue;

                    var so = new SerializedObject(controller);

                    if (isEnemy02)
                    {
                        if (SetIfPresentQuiet(so, "_fxGasPrefab", fxGas)) dirty = true;
                    }

                    if (controller is EnemyControllerBase)
                    {
                        if (SetIfPresentQuiet(so, "_fxHitPrefab", fxHit)) dirty = true;
                        if (SetIfPresentQuiet(so, "_fxExplodePrefab", fxExplode)) dirty = true;
                    }
                }

                if (dirty)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                    touched++;
                }
            }

            log.AppendLine($"  场景内敌人实例：{touched} 个场景有改动");
        }

        private static void AssignToPrefab(string prefabPath, System.Action<GameObject> assign)
        {
            if (!System.IO.File.Exists(prefabPath))
            {
                Debug.LogWarning($"[FxSetup] 找不到预制体 {prefabPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogWarning($"[FxSetup] 打开预制体失败 {prefabPath}");
                return;
            }

            try
            {
                assign(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetIfPresent(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            if (SetIfPresentQuiet(so, field, value))
            {
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning($"[FxSetup] 字段 {field} 不存在于 {target.GetType().Name}（脚本已变更？）");
            }
        }

        /// <summary>字段存在则赋值并返回 true；不存在只警告不报错。</summary>
        private static bool SetIfPresentQuiet(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                return false;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
