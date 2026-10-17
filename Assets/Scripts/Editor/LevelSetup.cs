using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarEco.EditorTools
{
    /// <summary>
    /// 第一关一键搭建（T17）：
    /// 1. Level01.asset（目标链 / 刷怪表 / 星级阈值）
    /// 2. Assets/Scenes/Level01.unity 灰盒（地面/玩家/污染源/塔/刷怪锚点/检查点/各系统）
    /// 菜单：StarEco → Setup → Level01（幂等：场景已存在则跳过创建，可重复运行）
    /// </summary>
    public static class LevelSetup
    {
        private const string ConfigPath = "Assets/Settings/Levels/Level01.asset";
        private const string ScenePath = "Assets/Scenes/Level01.unity";

        private const string PlayerPrefab = "Assets/Prefabs/Player/Player.prefab";
        private const string GarbagePilePrefab = "Assets/Prefabs/Eco/GarbagePile.prefab";
        private const string GarbageWallPrefab = "Assets/Prefabs/Eco/GarbageWall.prefab";
        private const string TowerPrefab = "Assets/Prefabs/Towers/Tower01.prefab";
        private const string Enemy01Prefab = "Assets/Prefabs/Enemies/Enemy01.prefab";
        private const string WhiteSquarePath = "Assets/Settings/WhiteSquare.png";

        [MenuItem("StarEco/Setup/Level01")]
        public static void Run()
        {
            var log = new StringBuilder();

            EnsureFolder("Assets/Settings/Levels");
            EnsureFolder("Assets/Scenes");

            var config = BuildConfig(log);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                log.AppendLine("Level01.unity 已存在，跳过场景创建（需要重建请先删除该场景）");
                AssetDatabase.SaveAssets();
                Debug.Log("[LevelSetup] 完成 ✓\n" + log);
                return;
            }

            BuildScene(config, log);

            AssetDatabase.SaveAssets();
            Debug.Log("[LevelSetup] 完成 ✓\n" + log);
        }

        // ===== LevelConfig 资产 =====

        private static StarEco.Level.LevelConfig BuildConfig(StringBuilder log)
        {
            var config = AssetDatabase.LoadAssetAtPath<StarEco.Level.LevelConfig>(ConfigPath);
            if (config != null)
            {
                log.AppendLine("Level01.asset 已存在，跳过（需改配置请在 Inspector 编辑）");
                return config;
            }

            config = ScriptableObject.CreateInstance<StarEco.Level.LevelConfig>();
            config.LevelIndex = 1;
            config.LevelName = "第一关：污染初现的星球";
            config.SpawnPoint = new Vector2(-8f, 2f);
            config.Star3Purity = 100;
            config.Star2Purity = 60;
            config.Star1Purity = 30;
            config.TimeLimit = 0f;
            config.IntroText = "这颗星球已经被垃圾污染了。用喷壶把污染源清理干净，让天空重新变绿。";

            // 目标链：3 个污染源 → 1 座塔 → 净化度 100%
            config.Objectives = new[]
            {
                new StarEco.Level.LevelConfig.LevelObjective(
                    StarEco.Level.LevelObjectiveType.PurifyTargets, 3, "清理散落的垃圾堆"),
                new StarEco.Level.LevelConfig.LevelObjective(
                    StarEco.Level.LevelObjectiveType.PurifyTowers, 1, "净化污染监控塔"),
                new StarEco.Level.LevelConfig.LevelObjective(
                    StarEco.Level.LevelObjectiveType.ReachPurity, 100, "把关卡净化值推到 100%")
            };

            // 刷怪表：两条 Enemy01 条目，锚点在 LevelSetup 场景构建后由 FixAnchors 回填
            config.SpawnEntries = new[]
            {
                new StarEco.Level.LevelConfig.SpawnEntry
                {
                    Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Enemy01Prefab),
                    Anchors = new Transform[0],
                    MaxAlive = 2,
                    BaseInterval = 5f,
                    RequiredObjectiveIndex = -1
                },
                new StarEco.Level.LevelConfig.SpawnEntry
                {
                    Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Enemy01Prefab),
                    Anchors = new Transform[0],
                    MaxAlive = 3,
                    BaseInterval = 6f,
                    RequiredObjectiveIndex = 1
                }
            };

            AssetDatabase.CreateAsset(config, ConfigPath);
            log.AppendLine("已创建 Level01.asset（目标链 3 项 + 刷怪表 2 条）");
            return config;
        }

        // ===== 场景 =====

        private static void BuildScene(StarEco.Level.LevelConfig config, StringBuilder log)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            if (white == null)
            {
                Debug.LogError("[LevelSetup] WhiteSquare.png 缺失，灰盒无法构建。");
                return;
            }

            // —— 地面 ——
            // 注意：WhiteSquare 的 sprite bounds 仅 0.04u。做法是 scale = 目标世界尺寸 / sprite 尺寸，
            // 同时把 Collider2D.size 设为 sprite 原始尺寸（collider.size 是局部单位，会被 scale 放大），
            // 两者相乘才等于目标世界尺寸。早期版本 collider.size=1 且 scale≈0，导致玩家穿地掉落。
            var ground = MakeWorldBox("Ground", white, 40f, 1f, new Vector3(0f, -0.5f, 0f),
                new Color(0.32f, 0.30f, 0.34f, 1f), -5);
            ground.layer = LayerMask.NameToLayer("Ground");
            log.AppendLine("已创建 Ground（40x1u，顶面 y=0，layer=Ground）");

            // —— 相机 ——
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.5f;
            cam.backgroundColor = new Color(0.45f, 0.40f, 0.52f, 1f);
            camGo.transform.position = new Vector3(0f, 2f, -10f);

            // —— 玩家 ——
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            GameObject player = null;
            if (playerPrefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.position = new Vector3(-8f, 1.5f, 0f);
                player.name = "Player";
                log.AppendLine("已放置 Player（含 M2/M3/M5 组件）");
            }
            else
            {
                Debug.LogError($"[LevelSetup] 找不到 {PlayerPrefab}，玩家未放置。");
            }

            // —— 污染源：3 垃圾堆 + 1 挡路墙 ——
            PlacePrefab(GarbagePilePrefab, "Garbage1", new Vector3(-4.5f, 0.9f, 0f), log);
            PlacePrefab(GarbagePilePrefab, "Garbage2", "Garbage2", new Vector3(-1f, 0.9f, 0f), log);
            PlacePrefab(GarbagePilePrefab, "Garbage3", new Vector3(3f, 0.9f, 0f), log);
            PlacePrefab(GarbageWallPrefab, "GarbageWall1", "GarbageWall1", new Vector3(6.5f, 1.4f, 0f), log);
            log.AppendLine("已放置 Garbage1~3 + GarbageWall1");

            // —— 塔（挂 TowerPurifiable 适配器）——
            var towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TowerPrefab);
            if (towerPrefab != null)
            {
                var tower = (GameObject)PrefabUtility.InstantiatePrefab(towerPrefab);
                tower.transform.position = new Vector3(10f, 1.5f, 0f);
                tower.name = "Tower01";
                var adapter = tower.GetComponent<StarEco.Eco.TowerPurifiable>() ?? tower.AddComponent<StarEco.Eco.TowerPurifiable>();
                var so = new SerializedObject(adapter);
                so.FindProperty("_targetId").stringValue = "Tower01";
                so.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine("已放置 Tower01（增挂 TowerPurifiable）");
            }

            // —— 刷怪锚点 ——
            var anchorA = MakeAnchor("SpawnAnchorA", new Vector3(-3f, 0.2f, 0f));
            var anchorB = MakeAnchor("SpawnAnchorB", new Vector3(8f, 0.2f, 0f));
            log.AppendLine("已创建 SpawnAnchorA/B");

            // —— 检查点 ——
            MakeCheckpoint("Checkpoint1", "CP_1", new Vector3(-6f, 0.5f, 0f), log);
            MakeCheckpoint("Checkpoint2", "CP_2", new Vector3(4.5f, 0.5f, 0f), log);

            // —— 环保系统 ——
            var eco = new GameObject("EcoSystems");
            var purity = eco.AddComponent<StarEco.Eco.PuritySystem>();
            eco.AddComponent<StarEco.Eco.AtmosphereController>();
            eco.AddComponent<StarEco.Eco.DropSpawner>();

            // —— 关卡系统 ——
            var levelGo = new GameObject("LevelSystems");
            // LevelManager 必须挂在非根节点上：MonoSingleton 只对根节点做 DontDestroyOnLoad，
            // 若它持久化，"重新开始"重载场景后新场景会再生成一个实例 → 单例冲突。
            var mgrGo = new GameObject("LevelManager");
            mgrGo.transform.SetParent(levelGo.transform, false);
            var manager = mgrGo.AddComponent<StarEco.Level.LevelManager>();

            var tracker = levelGo.AddComponent<StarEco.Level.LevelObjectiveTracker>();
            var spawner = levelGo.AddComponent<StarEco.Level.LevelSpawner>();
            var stats = levelGo.AddComponent<StarEco.Level.LevelStats>();
            levelGo.AddComponent<StarEco.Level.LevelResultUI>();

            // 注入配置（SerializedObject 私有字段）
            var mgrSo = new SerializedObject(manager);
            mgrSo.FindProperty("_config").objectReferenceValue = config;
            mgrSo.ApplyModifiedPropertiesWithoutUndo();

            var trkSo = new SerializedObject(tracker);
            trkSo.FindProperty("_config").objectReferenceValue = config;
            trkSo.ApplyModifiedPropertiesWithoutUndo();

            var spnSo = new SerializedObject(spawner);
            spnSo.FindProperty("_config").objectReferenceValue = config;
            spnSo.ApplyModifiedPropertiesWithoutUndo();

            // 回填刷怪锚点到配置资产（锚点是场景对象，无法序列化进 asset，故运行时由 LevelSpawner 用场景锚点）
            log.AppendLine("已放置 LevelSystems（Manager/Tracker/Spawner/Stats/ResultUI）");

            EditorSceneManager.SaveScene(scene, ScenePath);
            log.AppendLine($"已创建场景 {ScenePath}");

            EnsureInBuildSettings(ScenePath, log);

            log.AppendLine($"锚点引用：{anchorA.name} / {anchorB.name}；PuritySystem={purity != null}");
        }

        /// <summary>把关卡场景加入 Build Settings（RestartLevel 用 buildIndex 重载，必须在列表里）。</summary>
        private static void EnsureInBuildSettings(string scenePath, StringBuilder log)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    log.AppendLine("Build Settings 已含 Level01，跳过");
                    return;
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            log.AppendLine("已把 Level01 加入 Build Settings（index 0，RestartLevel 重载依赖）");
        }

        private static void PlacePrefab(string prefabPath, string name, Vector3 pos, StringBuilder log)
        {
            PlacePrefab(prefabPath, name, name, pos, log);
        }

        private static void PlacePrefab(string prefabPath, string prefabName, string instanceName, Vector3 pos, StringBuilder log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[LevelSetup] 缺少预制体 {prefabPath}");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = pos;
            go.name = instanceName;
        }

        /// <summary>
        /// 按"目标世界尺寸"创建 WhiteSquare 色块（SpriteRenderer + 2D 碰撞体）。
        /// scale = 目标世界尺寸 / sprite 原始尺寸，Collider2D.size 保持 sprite 原始尺寸（两者相乘还原世界尺寸）。
        /// </summary>
        private static GameObject MakeWorldBox(string name, Sprite white, float worldW, float worldH,
            Vector3 pos, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.color = color;
            sr.sortingOrder = sortingOrder;

            go.transform.localScale = new Vector3(
                worldW / white.bounds.size.x,
                worldH / white.bounds.size.y, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(white.bounds.size.x, white.bounds.size.y);

            return go;
        }

        private static Transform MakeAnchor(string name, Vector3 pos)
        {
            var white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            var go = MakeWorldBox(name, white, 0.3f, 0.3f, pos, new Color(1f, 0.4f, 0.4f, 0.5f), -3);
            var col = go.GetComponent<BoxCollider2D>();
            UnityEngine.Object.DestroyImmediate(col);   // 锚点只需位置，不需要碰撞
            return go.transform;
        }

        private static void MakeCheckpoint(string name, string id, Vector3 pos, StringBuilder log)
        {
            const float cpWorldSize = 1.6f;

            var white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            var go = MakeWorldBox(name, white, cpWorldSize, cpWorldSize, pos,
                new Color(0.4f, 0.8f, 1f, 0.35f), 6);

            var col = go.GetComponent<BoxCollider2D>();
            col.isTrigger = true;

            var cp = go.AddComponent<StarEco.Level.Checkpoint>();
            var so = new SerializedObject(cp);
            so.FindProperty("_checkpointId").stringValue = id;
            so.FindProperty("_respawnOffset").vector2Value = new Vector2(0f, 1.2f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
