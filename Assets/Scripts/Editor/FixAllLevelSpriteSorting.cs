using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FixAllLevelSpriteSorting
{
    // 仅调整 SpriteRenderer 的 sortingOrder；不动 transform。
    //
    // 层级口径（2026-10-31）：以 GraduationProject/docs/ArtStyleGuide.md §5.2
    // 「固化后的层级表」为唯一权威，规范表自述「本文档生效」。
    // 早前的 NameContainsOrder（按名字模糊猜）与兜底值 15 已废弃——
    // 兜底 15 会把新物件扔回玩家同层，正是 K-03「玩家被整棵树挡住」的根因。
    //
    // 匹配策略（两级 + 最长匹配）：
    //   ① ExactOrder   —— 显式指定，最高优先级（含预制体实例保护）
    //   ② 前缀规则      —— 名字以类别键开头，最长键优先
    //   ③ 子串规则      —— 前缀没命中时才用，同样最长键优先
    //   ④ 兜底 0       —— 地面层，比旧兜底 15 安全得多（不会盖住玩家）
    //
    // 为什么必须是「前缀优先」：NPC_Ava_LeverHint 既含 "npc"（应 4）
    // 又含 "lever"（应 8）。若只做子串匹配且不加优先级，它会被判成拉杆。
    // 前缀规则把它判成 NPC，子串规则只在「前缀未命中」时才生效，两边不打架。

    /// <summary>前缀规则：名字以该键开头即命中。键长者优先。</summary>
    private static readonly (string Key, int Order)[] PrefixRules =
    {
        // —— 远景 ——
        ("background", -100),          // 天空/远景：全屏背景图

        // —— 逻辑占位（不可见，"玩家绝不该看到"）——
        ("spawnanchor", -5),           // 刷怪锚点
        ("killzone", -5),              // 击杀区

        // —— 近景装饰（后）——
        ("decor_tree", -4),            // 玩家身后的树/枯树
        ("tree", -4),                  // 同上（L2 的树名为纯 "Tree"）

        // —— 地面 ——
        ("ground", 0),                 // 地面
        ("riverbed", 0),               // 河床
        ("raft", 0),                   // 木筏
        ("waterblock", 5),             // 水块（可站立）
        ("water", 0),                  // 水面（§5.2 明确含"水面"）

        // —— 地表装饰（无交互）——
        ("decor_sign", 2),             // 装饰牌
        ("jump_sign", 2),              // 跳跃关卡提示牌
        ("puzzle_sign", 2),            // 解谜关卡提示牌

        // —— NPC / 可读提示牌 ——
        ("npc", 4),                    // 艾娃 / 机器人（含 NPC_Ava_*、NPC_Robot_*）
        ("signboard", 4),              // 可读提示牌

        // —— 站立平台 ——
        ("platform", 5),               // 所有可站立平台
        ("secondfloorrightwall", 6),   // L3 土墙（危险物，键更长优先于 secondfloor）
        ("secondfloor", 5),            // 二层地板
        ("jump_crumble", 5),           // 可站立的碎裂块
        ("jump_mover", 5),             // 移动块
        ("jump_spikes", 6),            // 地刺
        ("puzzle_step", 5),            // 解谜台阶
        ("puzzle_floor", 5),           // 解谜地板
        ("puzzle_spikes", 6),          // 地刺

        // —— 道具 / 危险物 ——
        ("knowledgecard", 6),          // 知识卡
        ("spike", 6),                  // 地刺
        ("cage", 6),                   // 笼
        ("animal", 6),                 // 动物
        ("visual", 6),                 // 机械小件/环境装置薄横条

        // —— 可操作机关 ——
        ("puzzle_lever", 8),           // 拉杆（键更长优先于 lever）
        ("lever", 8),                  // 拉杆
        ("switch", 8),                 // 开关
    };

    /// <summary>子串规则：前缀没命中时用，键长者优先。</summary>
    private static readonly (string Key, int Order)[] SubstringRules =
    {
        ("background", -100),
        ("spawnanchor", -5),
        ("killzone", -5),
        ("decor_tree", -4),
        ("tree", -4),
        ("ground", 0),
        ("riverbed", 0),
        ("raft", 0),
        ("waterblock", 5),
        ("water", 0),
        ("decor_sign", 2),
        ("jump_sign", 2),
        ("puzzle_sign", 2),
        ("npc", 4),
        ("signboard", 4),
        ("platform", 5),
        ("secondfloorrightwall", 6),
        ("secondfloor", 5),
        ("jump_crumble", 5),
        ("jump_mover", 5),
        ("jump_spikes", 6),
        ("puzzle_step", 5),
        ("puzzle_floor", 5),
        ("puzzle_spikes", 6),
        ("knowledgecard", 6),
        ("spike", 6),
        ("cage", 6),
        ("animal", 6),
        ("visual", 6),
        ("puzzle_lever", 8),
        ("lever", 8),
        ("switch", 8),
    };

    /// <summary>
    /// 显式覆盖表：对象名 → order。用于名字不含任何稳定片段的特例，
    /// 以及**必须保护的预制体实例**。
    /// </summary>
    private static readonly Dictionary<string, int> ExactOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        // 三关共用的逻辑占位
        ["SpawnAnchorA"] = -5,
        ["SpawnAnchorB"] = -5,
        ["SpawnAnchorC"] = -5,
        ["SpawnAnchorD"] = -5,
        ["KillZone"] = -5,
        // L3 的两个机件薄横条（名字就是 "Visual"）
        ["Visual"] = 6,

        // —— 预制体实例保护 ——
        // FindObjectsOfType 会扫到场景里实例化的预制体。它们的 sortingOrder 存在
        // prefab 资产里（本脚本改不到，见 RiskRegister T-26），但**脚本仍会把
        // 实例上的值改掉**——而这些值一旦被兜底值 0 接管，后果是灾难性的：
        // Player 掉到 0 = 玩家被地面挡住直接消失；Boss 掉到 0 = Boss 被环境遮住。
        // 因此这里显式给出 §5.2 的目标值，让脚本「改对」而不是「改坏」。
        ["Player"] = 10,          // §5.2 玩家
        ["ToolAnchor"] = 11,      // §5.2 武器/工具 = 玩家 +1
        ["Boss01"] = 14,          // §5.2 Boss
        ["Tower01"] = 12,         // §5.2 敌人（塔）
        ["Garbage1"] = 6,         // §5.2 道具/危险物
        ["Garbage2"] = 6,
        ["Garbage3"] = 6,
        ["GarbageWall_03L"] = 6,
        ["GarbageWall_03R"] = 6,
        ["Puzzle_Pickup"] = 6,
        ["Puzzle_Rock1"] = 6,
        ["Puzzle_Rock2"] = 6,
        ["Puzzle_Reed"] = 6,

        // 可读提示牌的「发光边框」子对象（A 于 2026-10-31 挂的 sign_glow_*）：
        // §5.2 可读牌本体是 4，发光边框要压在其上才能看出「这块能读」，
        // 但不能高到盖住对话层（对话在 70 之上），取 7。
        ["SignGlow"] = 7,
    };

    /// <summary>兜底值：不再有"玩家同层 15"这个危险默认值，取中性的地面层。</summary>
    private const int FallbackOrder = 0;

    private static int GuessOrder(SpriteRenderer sr)
    {
        if (sr == null) return FallbackOrder;

        var n = sr.name ?? string.Empty;

        // ① 显式覆盖优先
        if (ExactOrder.TryGetValue(n, out int exact))
        {
            return exact;
        }

        // ② 前缀规则
        int bestOrder;
        if (TryMatch(n, PrefixRules, out bestOrder))
        {
            return bestOrder;
        }

        // ③ 子串规则
        if (TryMatch(n, SubstringRules, out bestOrder))
        {
            return bestOrder;
        }

        // ④ 兜底：按 sortingLayerName
        var ln = sr.sortingLayerName ?? string.Empty;
        if (ln.IndexOf("ui", StringComparison.OrdinalIgnoreCase) >= 0) return 100;
        if (ln.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0) return -100;

        return FallbackOrder;
    }

    /// <summary>在规则集里找最长命中键。命中返回 true 并输出 order。</summary>
    private static bool TryMatch(string name, (string Key, int Order)[] rules, out int order)
    {
        order = 0;
        int bestLen = 0;
        bool hit = false;

        foreach (var (key, ruleOrder) in rules)
        {
            if (name.IndexOf(key, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (key.Length <= bestLen) continue;
            bestLen = key.Length;
            order = ruleOrder;
            hit = true;
        }

        return hit;
    }

    public static void Execute(bool autoSave = false, params string[] scenePaths)
    {
        if (scenePaths == null || scenePaths.Length == 0)
        {
            scenePaths = new[]
            {
                "Assets/Scenes/Level01.unity",
                "Assets/Scenes/Level02.unity",
                "Assets/Scenes/Level03.unity"
            };
        }

        foreach (var path in scenePaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning($"[FixAllLevelSpriteSorting] Scene not found: {path}");
                continue;
            }

            var scene = EditorSceneManagerHelper.OpenScene(path);
            if (!scene.IsValid())
            {
                Debug.LogWarning($"[FixAllLevelSpriteSorting] Failed to open scene: {path}");
                continue;
            }

            var srs = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>();
            Undo.RecordObjects(srs, "Fix SpriteRenderer sortingOrder");

            int changed = 0;
            var report = new List<string>();
            var fallbackNames = new List<string>();

            foreach (var sr in srs)
            {
                int newOrder = GuessOrder(sr);
                if (sr.sortingOrder != newOrder)
                {
                    report.Add($"{sr.name}: {sr.sortingOrder} -> {newOrder}");
                    sr.sortingOrder = newOrder;
                    EditorUtility.SetDirty(sr);
                    changed++;
                }

                if (newOrder == FallbackOrder && !ExactOrder.ContainsKey(sr.name ?? string.Empty))
                {
                    var ln = sr.sortingLayerName ?? string.Empty;
                    if (ln.IndexOf("ui", StringComparison.OrdinalIgnoreCase) < 0 &&
                        ln.IndexOf("back", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        fallbackNames.Add(sr.name);
                    }
                }
            }

            Debug.Log($"[FixAllLevelSpriteSorting] {path} -> changed sortingOrder: {changed}, total SR: {srs.Length}");
            foreach (var line in report)
            {
                Debug.Log($"    {line}");
            }

            if (fallbackNames.Count > 0)
            {
                Debug.LogWarning($"[FixAllLevelSpriteSorting] {path} 有 {fallbackNames.Count} 个对象落到兜底值 {FallbackOrder}（分类未命中，请核对归类）：" +
                                 string.Join(", ", fallbackNames.ToArray()));
            }

            if (autoSave)
            {
                if (scene.IsValid())
                {
                    EditorSceneManagerHelper.SaveScene(scene);
                }
            }
        }

        AssetDatabase.Refresh();
    }

    // 为了避免直接引用 UnityEditor.SceneManagement 的内部差异，这里做一个轻量适配。
    private static class EditorSceneManagerHelper
    {
        public static Scene OpenScene(string scenePath)
        {
            // SaveCurrentModifiedScenesIfUserWantsTo 让用户在必要时处理保存。
            UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            return SceneManager.GetSceneByPath(scenePath);
        }

        public static void SaveScene(Scene scene)
        {
            if (!scene.IsValid()) return;
            var path = scene.path;
            if (string.IsNullOrEmpty(path)) return;
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"[FixAllLevelSpriteSorting] Saved scene: {path}");
        }
    }
}
