using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StarEco.UI;

namespace StarEco.EditorTools
{
    /// <summary>
    /// 第一关 UI/对话系统一键挂载（M7 T22）：打开 Level01.unity，补齐 M7 组件。
    /// - 替换 M6 的 LevelResultUI → ResultPanel
    /// - 新增 UIManager/HUDController/PausePanel/DialoguePanel（挂 LevelSystems 子节点）
    /// - 艾娃 NPC 对话触发器 + 提示牌×2 + 知识卡片×2
    /// 菜单：StarEco → Setup → Level01UI（幂等：已挂载则跳过对应项）
    /// </summary>
    public static class LevelUISetup
    {
        private const string ScenePath = "Assets/Scenes/Level01.unity";
        private const string WhiteSquarePath = "Assets/Settings/WhiteSquare.png";

        [MenuItem("StarEco/Setup/Level01UI")]
        public static void Run()
        {
            var log = new StringBuilder();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // —— 1. 移除 M6 旧结算 UI（ResultPanel 取代之） ——
            int removed = 0;
            foreach (var legacy in Object.FindObjectsOfType<StarEco.Level.LevelResultUI>())
            {
                Object.DestroyImmediate(legacy);
                removed++;
            }

            if (removed > 0)
            {
                log.AppendLine($"已移除旧 LevelResultUI ×{removed}（由 ResultPanel 取代）");
            }

            // —— 2. UIManager（关卡场景不新建：Title 场景的实例跨场景常驻；
            //        若单独打开 Level01 调试（未经过 Title），则补挂一个根节点实例） ——
            var existingManager = Object.FindObjectOfType<UIManager>();
            if (existingManager == null)
            {
                var go = new GameObject("UIManager");
                go.AddComponent<UIManager>();
                log.AppendLine("已补挂 UIManager（单独打开本场景调试用；经 Title 进入时常驻实例已存在）");
            }
            else
            {
                log.AppendLine("UIManager 已存在，跳过");
            }

            // —— 3. LevelSystems 下挂 UI 面板 ——
            var levelSystems = GameObject.Find("LevelSystems");
            if (levelSystems == null)
            {
                levelSystems = new GameObject("LevelSystems");
                log.AppendLine("未找到 LevelSystems，已新建");
            }

            AddOnce<HUDController>(levelSystems, "HUD", log);
            AddOnce<PausePanel>(levelSystems, "Pause", log);
            AddOnce<ResultPanel>(levelSystems, "Result", log);
            AddOnce<DialoguePanel>(levelSystems, "Dialogue", log);
            // 关卡内从暂停菜单可进设置；图鉴/关卡选择一并注册（入口只在主菜单，但 UIManager 需要注册表完整）
            AddOnce<SettingsPanel>(levelSystems, "Settings", log);
            AddOnce<CodexPanel>(levelSystems, "Codex", log);
            AddOnce<LevelSelectPanel>(levelSystems, "LevelSelect", log);

            // —— 4. 艾娃 NPC 触发器（出生点右侧的教学对话） ——
            if (Object.FindObjectOfType<DialogueTrigger>() == null)
            {
                var npc = CreateTriggerVisual("NPC_Ava", new Vector3(-6f, 1.6f, 0f),
                    new Color(0.49f, 0.99f, 0.60f, 0.9f));
                var trigger = npc.AddComponent<DialogueTrigger>();
                var so = new SerializedObject(trigger);
                so.FindProperty("_dialogueId").stringValue = "ava_intro";
                so.FindProperty("_radius").floatValue = 2.6f;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine("已放置 NPC_Ava（ava_intro 教学对话，半径 2.6u）");
            }

            // —— 5. 提示牌×2（挡路墙前后各一） ——
            if (Object.FindObjectsOfType<SignboardTrigger>().Length == 0)
            {
                CreateSignboard("Signboard_Plastic", new Vector3(2f, 1.4f, 0f), "signboard_plastic");
                CreateSignboard("Signboard_Recycle", new Vector3(9f, 1.4f, 0f), "signboard_recycle");
                log.AppendLine("已放置 Signboard_Plastic / Signboard_Recycle");
            }

            // —— 6. 知识卡片×2（出生点附近 + 塔前） ——
            if (Object.FindObjectsOfType<StarEco.Eco.KnowledgeCardPickup>().Length == 0)
            {
                CreateKnowledgeCard("KnowledgeCard1", new Vector3(-2.5f, 1.0f, 0f), "kc_plastic_bottle");
                CreateKnowledgeCard("KnowledgeCard2", new Vector3(7.5f, 1.0f, 0f), "kc_recycle_symbol");
                log.AppendLine("已放置 KnowledgeCard1（kc_plastic_bottle）/ KnowledgeCard2（kc_recycle_symbol）");
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[LevelUISetup] 完成 ✓\n" + log);
        }

        // ===== 辅助 =====

        private static void AddOnce<T>(GameObject parent, string name, StringBuilder log) where T : Component
        {
            if (Object.FindObjectOfType<T>() != null)
            {
                log.AppendLine($"{typeof(T).Name} 已存在，跳过");
                return;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<T>();
            log.AppendLine($"已挂载 {name}（{typeof(T).Name}）");
        }

        private static GameObject CreateTriggerVisual(string name, Vector3 pos, Color color)
        {
            var white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            var go = new GameObject(name);
            go.transform.position = pos;

            if (white != null)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = white;
                sr.color = color;
                sr.sortingOrder = 4;
                go.transform.localScale = new Vector3(
                    0.8f / white.bounds.size.x,
                    1.2f / white.bounds.size.y, 1f);
            }

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 1.3f;

            return go;
        }

        private static void CreateSignboard(string name, Vector3 pos, string dialogueId)
        {
            var white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            var go = new GameObject(name);
            go.transform.position = pos;

            if (white != null)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = white;
                sr.color = new Color(0.56f, 0.81f, 1f, 0.9f);
                sr.sortingOrder = 4;
                go.transform.localScale = new Vector3(
                    0.7f / white.bounds.size.x,
                    1.0f / white.bounds.size.y, 1f);
            }

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 1.2f;

            var trigger = go.AddComponent<SignboardTrigger>();
            var so = new SerializedObject(trigger);
            so.FindProperty("_dialogueId").stringValue = dialogueId;
            so.FindProperty("_radius").floatValue = 2.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateKnowledgeCard(string name, Vector3 pos, string cardId)
        {
            var white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            var go = new GameObject(name);
            go.transform.position = pos;

            if (white != null)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = white;
                sr.color = new Color(1f, 0.92f, 0.35f, 0.95f);
                sr.sortingOrder = 6;
                go.transform.localScale = new Vector3(
                    0.5f / white.bounds.size.x,
                    0.5f / white.bounds.size.y, 1f);
            }

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.6f;

            var pickup = go.AddComponent<StarEco.Eco.KnowledgeCardPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("_cardId").stringValue = cardId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
