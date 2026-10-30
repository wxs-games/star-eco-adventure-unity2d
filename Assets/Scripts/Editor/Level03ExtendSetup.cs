using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StarEco.Level;
using StarEco.UI;

namespace StarEco.EditorTools
{
    /// <summary>
    /// Level03 地图扩建（用户 2026-10-30 需求：地板太短，走路两下就到 Boss；中间要有不重复的内容）。
    ///
    /// 原则「只追加、不重建」：
    /// - 现有手摆内容（拉杆二层、NPC、招牌、装饰、垃圾堆、平台）一个都不删不改；
    /// - 只做三件事：把 Ground/KillZone 横向拉长、把 Boss 区整体右移、在右侧新区域补三段内容。
    ///
    /// 最终布局（96u）：
    ///   段 1 起点·厂区外环      x ∈ [-24, 24]      原有内容，未改动
    ///   段 2 跳台区             x ∈ [24, 40]       碎裂 ×2 + 移动平台 ×1 + 地面尖刺（纯跳跃挑战）
    ///   段 3 机关封锁区         x ∈ [40, 60]       高台 + 拉杆（强制扳动才收地刺）+ 高台补给 + 远程怪 + 可破坏障碍 + 新 NPC
    ///   段 4 Boss 前厅          x ∈ [60, 66]       炮塔 ×1 + 刷怪锚点
    ///   Boss 区                x ∈ [66, 72]       Boss01(68) + 右端封锁墙(71.5)
    ///
    /// 幂等：场景里已存在 Level03_Extended 根节点则直接跳过，不会重复叠加。
    /// 两份项目通用：右端封锁墙按名字查，毕设版是 GarbageWall_03R，求职展示版是 BossGateWall。
    /// 菜单：StarEco → Setup → Level03 Extend
    /// </summary>
    public static class Level03ExtendSetup
    {
        private const string ScenePath = "Assets/Scenes/Level03.unity";
        private const string RootName = "Level03_Extended";

        private const float GroundWidth = 96f;          // 原 48 → 96（约 2 倍）
        private const float GroundCenterX = 24f;        // 地面 x ∈ [-24, 72]
        private const float GroundTop = 0f;
        private const float LevelLeft = -24f;
        private const float LevelRight = 72f;
        private const float BossX = 68f;
        private const float EndWallX = 71.5f;

        [MenuItem("StarEco/Setup/Level03 Extend")]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.path.Replace('\\', '/').Equals(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var log = new StringBuilder();

            if (GameObject.Find(RootName) != null)
            {
                log.AppendLine($"{RootName} 已存在，本场景已扩建过，跳过（如需重建请先手动删除该节点）");
                Debug.Log("[Level03Extend] 跳过\n" + log);
                return;
            }

            var root = new GameObject(RootName);
            root.transform.position = Vector3.zero;

            ExtendGround(log);
            ExtendKillZone(log);
            ExtendCamera(log);
            MoveBossArea(log);

            BuildJumpSection(root.transform, log);
            BuildPuzzleSection(root.transform, log);
            BuildBossLobby(root.transform, log);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("[Level03Extend] 完成 ✓\n" + log);
        }

        // ===== 地面 / 边界 / 相机 =====

        /// <summary>
        /// 地面是单个 Tiled 精灵（sr.size 是世界单位的平铺尺寸，sprite 原生宽 16u）。
        /// 96 = 16×6，平铺正好整数块，不会出现半块接缝。
        /// 碰撞体沿用原来的 0.98 比例（原 48u 宽对应 47.04u 碰撞体）。
        /// </summary>
        private static void ExtendGround(StringBuilder log)
        {
            var ground = GameObject.Find("Ground");
            if (ground == null)
            {
                log.AppendLine("[warn] 未找到 Ground，地面未延长");
                return;
            }

            var sr = ground.GetComponent<SpriteRenderer>();
            var col = ground.GetComponent<BoxCollider2D>();

            float oldSizeX = sr != null ? sr.size.x : 48f;
            float colRatio = col != null ? col.size.x / oldSizeX : 0.98f;

            if (sr != null)
            {
                sr.size = new Vector2(GroundWidth, sr.size.y);
            }

            if (col != null)
            {
                col.size = new Vector2(GroundWidth * colRatio, col.size.y);
            }

            ground.transform.position = new Vector3(GroundCenterX, -4f, 0f);
            log.AppendLine($"地面 x∈[-24,72]（{GroundWidth}u，顶面 y={GroundTop}）");
        }

        private static void ExtendKillZone(StringBuilder log)
        {
            var kz = GameObject.Find("KillZone");
            if (kz == null)
            {
                log.AppendLine("[warn] 未找到 KillZone，坠落兜底未延长");
                return;
            }

            var col = kz.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.size = new Vector2((LevelRight - LevelLeft) + 8f, col.size.y);
            }

            kz.transform.position = new Vector3(GroundCenterX, -6f, 0f);
            log.AppendLine("KillZone 已覆盖全宽（掉落即判死，不会卡在关卡外）");
        }

        private static void ExtendCamera(StringBuilder log)
        {
            var camGo = GameObject.Find("Main Camera");
            var follow = camGo != null ? camGo.GetComponent<CameraFollow2D>() : null;
            if (follow == null)
            {
                log.AppendLine("[warn] 未找到 CameraFollow2D，相机钳制范围未更新");
                return;
            }

            var so = new SerializedObject(follow);
            var left = so.FindProperty("_levelLeft");
            var right = so.FindProperty("_levelRight");
            if (left != null) left.floatValue = LevelLeft;
            if (right != null) right.floatValue = LevelRight;
            so.ApplyModifiedPropertiesWithoutUndo();

            log.AppendLine($"相机跟随钳制 → [{LevelLeft}, {LevelRight}]");
        }

        /// <summary>
        /// Boss 与「右端封锁墙」一起右移到新终点。
        /// 墙按名字查：毕设版 GarbageWall_03R / 求职展示版 BossGateWall（后者挂着 BossGate 演出脚本，只移位不动逻辑）。
        /// </summary>
        private static void MoveBossArea(StringBuilder log)
        {
            var boss = GameObject.Find("Boss01");
            if (boss != null)
            {
                float y = boss.transform.position.y;
                boss.transform.position = new Vector3(BossX, y, 0f);
                log.AppendLine($"Boss01 → x={BossX}");
            }
            else
            {
                log.AppendLine("[warn] 未找到 Boss01");
            }

            GameObject wall = GameObject.Find("GarbageWall_03R");
            if (wall == null)
            {
                wall = GameObject.Find("BossGateWall");
            }

            if (wall != null)
            {
                float y = wall.transform.position.y;
                wall.transform.position = new Vector3(EndWallX, y, 0f);
                log.AppendLine($"{wall.name} → x={EndWallX}（终点封锁墙）");
            }
            else
            {
                log.AppendLine("[warn] 未找到右端封锁墙（GarbageWall_03R / BossGateWall）");
            }
        }

        // ===== 段 2：跳台区（24 → 40）=====

        private static void BuildJumpSection(Transform root, StringBuilder log)
        {
            var section = NewGroup("Section2_Jump", root);

            // J1 碎裂平台：顶面 2.0，可站立的木面在精灵局部 y=+0.1735，clone 时按碰撞体顶面反推位置
            ClonePlatform("Jump_Crumble1", "PlatformL1", section, 27.0f, 2.0f);
            ClonePlatform("Jump_Crumble2", "PlatformL1", section, 35.4f, 2.4f);

            // 移动平台：行程 28.8 → 33.2，顶面 3.3（比 J1 高 1.3u，可从 J1 直接起跳）
            var mover = ClonePlatform("Jump_Mover", "PlatformC", section, 28.8f, 3.3f);
            if (mover != null)
            {
                var mp = mover.GetComponent<MovingPlatform>();
                if (mp != null)
                {
                    var so = new SerializedObject(mp);
                    so.FindProperty("_minX").floatValue = 28.8f;
                    so.FindProperty("_maxX").floatValue = 33.2f;
                    so.FindProperty("_startAtMin").boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // 地面尖刺：x∈[28,39]，纯跳跃挑战（不可收起，不挂 targetId）
            CloneSpike("Jump_Spikes", "SpikeField", section, 33.5f, 11f);

            CloneSign("Jump_Sign", section, 25.0f);
            log.AppendLine("段2 跳台区：碎裂平台×2 + 移动平台×1（28.8~33.2）+ 地面尖刺×1（11u）");
        }

        // ===== 段 3：机关封锁区（40 → 60）=====

        private static void BuildPuzzleSection(Transform root, StringBuilder log)
        {
            var section = NewGroup("Section3_Puzzle", root);

            // 踏步台：地面 → 高台过渡（顶面 1.2，相对地面 1.2u 一跳可上）
            ClonePlatform("Puzzle_Step", "PlatformL2", section, 39.4f, 1.2f);

            // 高台：顶面 2.8，x∈[41,48.7]（SecondFloor 原生 7.7u 宽，直接 clone 不改宽度）
            ClonePlatform("Puzzle_Floor", "SecondFloor", section, 44.85f, 2.8f);

            // 地面尖刺：x∈[40.5,52.5]，12u 远超跳跃距离 → 必须上高台扳拉杆才会收起
            var spike = CloneSpike("Puzzle_Spikes", "SpikeField", section, 46.5f, 12f);

            // 拉杆（高台顶面 2.8 上，精灵高 0.8 → 中心 3.2）
            var lever = CloneObject("Puzzle_Lever", "LeverSwitch_03", section);
            if (lever != null)
            {
                lever.transform.position = new Vector3(47f, 2.8f + 0.4f, 0f);
                var lv = lever.GetComponent<LeverSwitch>();
                var target = spike != null ? spike.GetComponent<SpikeHazard>() : null;
                if (lv != null && target != null)
                {
                    var so = new SerializedObject(lv);
                    var prop = so.FindProperty("_target");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = target;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            // 高台补给（净化拾取），放在高台顶面 2.8 上
            PlacePrefab("Assets/Prefabs/Eco/PurifyPickup.prefab", "Puzzle_Pickup", 44.0f, section, 2.8f);

            // 段 3 的敌人走 Level03.asset 的第二条刷怪条目（Enemy02 远程毒植物），
            // 不摆静态实例：实测 Enemy02 预制体在场景里会成为 inactive（依赖对象池下发的运行态），
            // 静态摆放会变成一个看不见的哑巴怪。

            // 可破坏障碍：岩石×2 + 芦苇×1（长按 L 蓄力波 / J 三连击可破）
            PlacePrefab("Assets/Prefabs/Eco/BreakableRock.prefab", "Puzzle_Rock1", 56.0f, section, GroundTop);
            PlacePrefab("Assets/Prefabs/Eco/BreakableRock.prefab", "Puzzle_Rock2", 57.4f, section, GroundTop);
            PlacePrefab("Assets/Prefabs/Eco/BreakableReed.prefab", "Puzzle_Reed", 58.6f, section, GroundTop);

            // 新 NPC：艾娃（新对话 ava_factory_gate），站在 x=60.8
            var npc = CloneObject("Puzzle_NPC", "NPC_Ava_LeverHint", section);
            if (npc != null)
            {
                npc.transform.position = new Vector3(60.8f, 1.10f, 0f);
                var trigger = npc.GetComponent<DialogueTrigger>();
                if (trigger != null)
                {
                    var so = new SerializedObject(trigger);
                    so.FindProperty("_dialogueId").stringValue = "ava_factory_gate";
                    so.FindProperty("_repeatDialogueId").stringValue = "ava_factory_gate_repeat";
                    so.FindProperty("_autoTalkOnFirstApproach").boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            CloneSign("Puzzle_Sign", section, 53.5f);
            log.AppendLine("段3 机关封锁区：踏步台 + 高台 + 拉杆（强制）+ 12u 地刺 + 补给 + Enemy02 + 可破坏障碍×3 + 新 NPC");
        }

        // ===== 段 4：Boss 前厅（60 → 66）=====

        private static void BuildBossLobby(Transform root, StringBuilder log)
        {
            var section = NewGroup("Section4_BossLobby", root);

            // 刷怪锚点：命名必须以 SpawnAnchor 开头，LevelSpawner 按前缀在场景里查找
            CloneAnchor("SpawnAnchorC", section, 54.0f);
            CloneAnchor("SpawnAnchorD", section, 64.0f);

            // 固定炮塔不放：Tower01 是实心的（trigger=false、Kinematic），摆在 62~65 会把
            // 通往 Boss 的地面走廊整条堵住，而它是需要打掉才能过的硬障碍，属于本关不该有的路径阻断。
            log.AppendLine("段4 Boss 前厅：刷怪锚点 C/D（不摆炮塔，避免堵住走廊）");
        }

        // ===== 构件 =====

        private static Transform NewGroup(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            return go.transform;
        }

        /// <summary>
        /// 克隆场景内已有构件：位置按「碰撞体顶面 = topY」反推。
        ///
        /// 平台的 pivot/碰撞体 offset 各不相同（PlatformL1 顶面比原点高 0.12，SecondFloor 高 0.17），
        /// 写死偏移量会在不同模板上错位，所以统一用 clone 后的碰撞体包围盒实测。
        /// </summary>
        private static GameObject ClonePlatform(string name, string templateName, Transform parent, float x, float topY)
        {
            var go = CloneObject(name, templateName, parent);
            if (go == null)
            {
                return null;
            }

            var col = go.GetComponent<Collider2D>();
            float offset = col != null ? col.bounds.max.y - go.transform.position.y : 0f;
            go.transform.position = new Vector3(x, topY - offset, 0f);
            return go;
        }

        /// <summary>克隆尖刺并按目标世界宽度横向缩放（spike_strip 原生宽 10.6u）。</summary>
        private static GameObject CloneSpike(string name, string templateName, Transform parent, float centerX, float width)
        {
            var go = CloneObject(name, templateName, parent);
            if (go == null)
            {
                return null;
            }

            var sr = go.GetComponent<SpriteRenderer>();
            float nativeW = sr != null && sr.sprite != null ? sr.bounds.size.x : 10.6f;
            float sx = nativeW > 0.0001f ? width / nativeW : 1f;
            go.transform.localScale = new Vector3(sx, 1f, 1f);
            go.transform.position = new Vector3(centerX, GroundTop, 0f);
            return go;
        }

        private static void CloneSign(string name, Transform parent, float x)
        {
            var go = CloneObject(name, "Decor_Sign_03WM", parent);
            if (go != null)
            {
                go.transform.position = new Vector3(x, 0.58f, 0f);
            }
        }

        private static void CloneAnchor(string name, Transform parent, float x)
        {
            var go = CloneObject(name, "SpawnAnchorA", parent);
            if (go != null)
            {
                go.transform.position = new Vector3(x, 0.20f, 0f);
            }
        }

        private static GameObject CloneObject(string name, string templateName, Transform parent)
        {
            var template = GameObject.Find(templateName);
            if (template == null)
            {
                Debug.LogWarning($"[Level03Extend] 模板对象 {templateName} 不存在，跳过 {name}");
                return null;
            }

            var go = Object.Instantiate(template, template.transform.position, template.transform.rotation, parent);
            go.name = name;
            return go;
        }

        /// <summary>
        /// 实例化预制体并使其「碰撞体底边」贴到 groundY 上。
        ///
        /// 不能按「高度 ÷ 2」摆放：敌人/炮塔的碰撞体中心相对 transform 原点有偏移
        /// （实测 Enemy02 偏移 -0.825、Tower01 偏移 -0.665），按半高摆会让它们半个身子陷进地面。
        /// </summary>
        private static GameObject PlacePrefab(string path, string name, float x, Transform parent, float groundY)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[Level03Extend] 缺少预制体 {path}，跳过 {name}");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(parent, true);

            Physics2D.SyncTransforms();   // 编辑模式下包围盒不会自动刷新，先同步一次再读底边

            var col = go.GetComponent<Collider2D>();
            var sr = go.GetComponent<SpriteRenderer>();
            float bottomOffset = 0f;
            if (col != null && col.bounds.size.y > 0.0001f)
            {
                bottomOffset = col.bounds.min.y - go.transform.position.y;
            }
            else if (sr != null && sr.sprite != null)
            {
                bottomOffset = sr.bounds.min.y - go.transform.position.y;
            }

            go.transform.position = new Vector3(x, groundY - bottomOffset, 0f);
            return go;
        }
    }
}
