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
    /// 2. Assets/Scenes/Level01.unity 灰盒（地面/玩家/污染源/塔/刷怪锚点/坠落区/各系统）
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

            // 目标链（v1.1 重做）：
            // ① 3 个垃圾堆净化（主线，必经）
            // ② 净化度 100%（主线，必经）
            // 污染监控塔改为可选战斗支线，不再作为净化目标。击毁塔可解除射线、提升评级，
            // 但不作为通关判据，避免与净化度 100% 形成并联通关。
            config.Objectives = new[]
            {
                new StarEco.Level.LevelConfig.LevelObjective(
                    StarEco.Level.LevelObjectiveType.PurifyTargets, 3, "清理散落的垃圾堆"),
                new StarEco.Level.LevelConfig.LevelObjective(
                    StarEco.Level.LevelObjectiveType.ReachPurity, 100, "把关卡净化值推到 100%")
            };

            // 刷怪表（v1.1 重做）：敌人不再全程无限循环刷。
            // 第一条：缓慢近战压制，贯穿全关但数量少，保持教学关前段低压力。
            // 第二条：垃圾堆清理完成（目标①达成）后才开始刷新，对应塔区收尾阶段的轻度战斗压力。
            // 塔为可选支线，不与刷怪阶段绑定，避免玩家被迫击塔才能推进。
            config.SpawnEntries = new[]
            {
                new StarEco.Level.LevelConfig.SpawnEntry
                {
                    Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Enemy01Prefab),
                    Anchors = new Transform[0],
                    MaxAlive = 2,
                    BaseInterval = 12f,
                    RequiredObjectiveIndex = -1
                },
                new StarEco.Level.LevelConfig.SpawnEntry
                {
                    Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Enemy01Prefab),
                    Anchors = new Transform[0],
                    MaxAlive = 2,
                    BaseInterval = 10f,
                    RequiredObjectiveIndex = 0
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

            // —— 坠落死亡区 ——
            // 地面只有 40u 宽，玩家从两侧走出后会无限下坠；在下方铺一层触发区兜底。
            var killZone = new GameObject("KillZone");
            killZone.transform.position = new Vector3(0f, -6f, 0f);
            var killCol = killZone.AddComponent<BoxCollider2D>();
            killCol.isTrigger = true;
            killCol.size = new Vector2(60f, 4f);
            killZone.AddComponent<StarEco.Level.FallHazard>();
            log.AppendLine("已创建 KillZone（y=-6，60x4 触发区，坠落即死）");

            // —— 跳跃平台（与第二关同款单向木板，左右各两级）——
            // 地面顶面 y=0，跳跃上限约 2.45u；L1 可站立顶面 1.40 从地面可稳定跳上，
            // L2 顶面 2.60 相对 L1 再升 1.2u，可连跳。同一侧两级横向错开 2.8u，
            // 避免上下叠在同一条竖线上（角色高 1.98u，同 x 叠放会让站在下层的人头部穿进上层）。
            // 放在两侧空地（x=±12.8 / ±15.6），避开中段 -7~11 的污染源 / 牌板 / 知识卡 / NPC。
            int groundLayer = LayerMask.NameToLayer("Ground");
            // L1 是碎裂机关：踩上约 0.5s 后碎掉，犹豫就掉下去；L2/R1/R2 保持普通单向木板
            // PlatformL1 左台阶为独占知识卡 kc_plastic_bag 的落点（补齐图鉴缺失卡），
            // 跳上去有明确收益，失败落回地面不直接死亡。
            MakePlatform("PlatformL1", new Vector3(-12.8f, 1.40f, 0f), 2f, groundLayer, crumble: true);
            MakePlatform("PlatformL2", new Vector3(-15.6f, 2.60f, 0f), 2f, groundLayer);
            MakePlatform("PlatformR1", new Vector3(12.8f, 1.40f, 0f), 2f, groundLayer);
            MakePlatform("PlatformR2", new Vector3(15.6f, 2.60f, 0f), 2f, groundLayer);
            log.AppendLine("已放置 4 个跳跃平台（左右各两级，单向平台；L1 为碎裂平台）");

            // —— 相机 ——
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = 5.5f;
            cam.backgroundColor = new Color(0.45f, 0.40f, 0.52f, 1f);
            camGo.transform.position = new Vector3(0f, 2f, -10f);

            // 横向跟随：Level01 宽 40 单位，静态相机会让玩家走出视口。
            // 与 M9 的 CameraFollow2D 同构，按关卡边界 ±20 钳制。
            var follow = camGo.AddComponent<StarEco.Level.CameraFollow2D>();
            var fso = new SerializedObject(follow);
            fso.FindProperty("_levelLeft").floatValue = -20f;
            fso.FindProperty("_levelRight").floatValue = 20f;
            fso.FindProperty("_fixedY").floatValue = 2f;
            fso.ApplyModifiedPropertiesWithoutUndo();

            // —— 背景层（挂相机子物体，随相机移动必定铺满）——
            //尺寸走 localScale：实测本工程 Simple 模式下 sr.size 不改变 bounds。
            // Center pivot：localPosition 置 0 即居中（旧 TopLeft pivot 需偏移半屏）。
            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/Art/Environment/Background/bg_polluted_planet.png");
            if (bgSprite != null)
            {
                var bgGo = new GameObject("Background");
                var bgSr = bgGo.AddComponent<SpriteRenderer>();
                bgSr.sprite = bgSprite;
                bgSr.drawMode = SpriteDrawMode.Simple;
                bgSr.sortingOrder = -100;
                float bgViewW = cam.orthographicSize * 2f * cam.aspect * 1.05f;
                float bgViewH = cam.orthographicSize * 2f * 1.05f;
                var bgNative = bgSprite.bounds.size;
                bgGo.transform.localScale = new Vector3(bgViewW / bgNative.x, bgViewH / bgNative.y, 1f);
                bgGo.transform.SetParent(camGo.transform, false);
                bgGo.transform.localPosition = new Vector3(0f, 0f, 10f);
                log.AppendLine("已创建背景层（相机子物体，Center pivot 居中）");
            }

            // —— 玩家 ——
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            GameObject player = null;
            if (playerPrefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                // 出生点唯一事实来源是 LevelConfig.SpawnPoint（LevelManager 运行时也按它放置玩家），
                // 编辑器建场景时直接复用，避免两处硬编码各写一个值后逐渐漂移。
                player.transform.position = new Vector3(config.SpawnPoint.x, config.SpawnPoint.y, 0f);
                player.name = "Player";
                log.AppendLine("已放置 Player（含 M2/M3/M5 组件）");
            }
            else
            {
                Debug.LogError($"[LevelSetup] 找不到 {PlayerPrefab}，玩家未放置。");
            }

            // —— 污染源：3 垃圾堆+ 1 挡路墙 ——
            // 注意：prefab 根节点位于物件**中心**，碰撞体 offset=0。
            // 垃圾堆精灵 1.60×0.72u，垃圾墙精灵 1.40×1.66u（地面顶面 y=0）。
            // 但两张精灵底部是向外散开的破布/碎屑、向下收窄成锥形，
            // 套用「半高」使包围盒底边贴地时，主体实心底边仍悬空约 0.09u，
            // 故在半高基础上再下移 0.09u 补偿：垃圾堆 y=0.36-0.09=0.27；垃圾墙 y=0.83-0.09=0.74。
            PlacePrefab(GarbagePilePrefab, "Garbage1", new Vector3(-4.5f, 0.27f, 0f), log);
            PlacePrefab(GarbagePilePrefab, "Garbage2", "Garbage2", new Vector3(-1f, 0.27f, 0f), log);
            PlacePrefab(GarbagePilePrefab, "Garbage3", new Vector3(3f, 0.27f, 0f), log);
            PlacePrefab(GarbageWallPrefab, "GarbageWall1", "GarbageWall1", new Vector3(6.5f, 0.74f, 0f), log);
            log.AppendLine("已放置 Garbage1~3(y=0.27) + GarbageWall1(y=0.74)，含底部锥形 0.09u 落地下移补偿");

            // —— 塔（攻击击毁，不挂净化适配器）——
            var towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TowerPrefab);
            if (towerPrefab != null)
            {
                var tower = (GameObject)PrefabUtility.InstantiatePrefab(towerPrefab);
                // 塔精灵高 2.5899（半高 1.295）、碰撞体底边已对齐精灵脚底，
                // 故塔根节点置于半高处即脚底贴地（旧值 1.5 会让塔悬空 0.205u）。
                tower.transform.position = new Vector3(10f, 1.295f, 0f);
                tower.name = "Tower01";
                log.AppendLine("已放置 Tower01（攻击击毁，不加 TowerPurifiable）");
            }

            // —— 刷怪锚点 ——
            var anchorA = MakeAnchor("SpawnAnchorA", new Vector3(-3f, 0.2f, 0f));
            var anchorB = MakeAnchor("SpawnAnchorB", new Vector3(8f, 0.2f, 0f));
            log.AppendLine("已创建 SpawnAnchorA/B");

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
            log.AppendLine("已放置 LevelSystems（Manager/Tracker/Spawner/Stats）");

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

        // platform_flat.png：1200×245、PPU 400 → 原生 3.00×0.6125u，Pivot=Center。
        // 木面水平线（真正可站立的顶面）在精灵局部坐标 y=+0.1735，其上还有约 0.09u 的草叶。
        private const string PlatformSpritePath = "Assets/Art/Art/Environment/Props/platform_flat.png";
        private const float PlatformWoodLineY = 0.1735f;

        /// <param name="pos">pos.y 传「可站立的木面顶面高度」，不是物件中心。</param>
        /// <param name="crumble">
        /// 是否挂「一踩就碎」的机关（目前只有 PlatformL1 用）。
        /// 放在生成函数里而不是事后手动添加，否则重跑本菜单重建场景时组件会丢。
        /// </param>
        private static void MakePlatform(string name, Vector3 pos, float width, int groundLayer, bool crumble = false)
        {
            var white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            var go = MakeWorldBox(name, white, width, 0.5f, pos,
                new Color(0.38f, 0.46f, 0.34f, 1f), -4);
            go.layer = groundLayer;

            var spr = AssetDatabase.LoadAssetAtPath<Sprite>(PlatformSpritePath);
            if (spr == null)
            {
                Debug.LogWarning($"[LevelSetup] 找不到平台素材 {PlatformSpritePath}，{name} 保留灰盒");
                return;
            }

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = spr;
            sr.color = Color.white;
            sr.drawMode = SpriteDrawMode.Simple;
            sr.sortingOrder = 5;

            float s = width / 3f;                       // 精灵原生宽 3.0u，按目标宽度等比缩放
            go.transform.localScale = new Vector3(s, s, 1f);

            // 碰撞体世界厚度固定 0.56u（薄平台要留厚度防高速下落穿透），顶面对齐木面水平线
            var col = go.GetComponent<BoxCollider2D>();
            col.size = new Vector2(3f, 0.56f / s);
            col.offset = new Vector2(0f, PlatformWoodLineY - col.size.y / 2f);

            // 单向平台：从下方/侧面可穿过，只从上方落下时踩住（与第二关同参数）
            var eff = go.AddComponent<PlatformEffector2D>();
            eff.useOneWay = true;
            eff.surfaceArc = 150f;
            col.usedByEffector = true;

            // 碎裂机关：踩上约 0.5s 后碎掉，没跳走就跟着掉下去，3s 后原地长回来
            if (crumble)
            {
                go.AddComponent<StarEco.Level.CrumblePlatform>();
            }

            go.transform.position = new Vector3(pos.x, pos.y - PlatformWoodLineY * s, pos.z);
        }

        private static Transform MakeAnchor(string name, Vector3 pos)
        {
            var white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            var go = MakeWorldBox(name, white, 0.3f, 0.3f, pos, new Color(1f, 0.4f, 0.4f, 0.5f), -3);
            var col = go.GetComponent<BoxCollider2D>();
            UnityEngine.Object.DestroyImmediate(col);   // 锚点只需位置，不需要碰撞

            // 刷怪锚点是纯逻辑标记, 视觉留个句柄即可, 不进成品画面
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.enabled = false;
            }

            return go.transform;
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
