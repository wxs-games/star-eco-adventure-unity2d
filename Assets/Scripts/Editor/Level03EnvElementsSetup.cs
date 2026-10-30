using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StarEco.Level;

namespace StarEco.EditorTools
{
    /// <summary>
    /// Level03 新环境元素验证区（2026-10-30）。
    ///
    /// 只做两件事：把「传送管道」和「污染喷口」各摆一个到段 4 的空地（Boss 前厅前），
    /// 供手感验证；美术一律复用现有贴图并染色，属占位素材，正式版需要新画。
    ///
    /// 位置：传送带 x∈[61.8, 64.2]（向右推 3.5u/s）→ 玩家会被送进喷口 x∈[64.4, 66.0]，
    /// 正好构成「加速冲刺 + 卡节奏越过喷口」的组合验证。
    ///
    /// 幂等：已存在 Section5_EnvTest 则跳过。
    /// 菜单：StarEco → Setup → Level03 Env Elements
    /// </summary>
    public static class Level03EnvElementsSetup
    {
        private const string ScenePath = "Assets/Scenes/Level03.unity";
        private const string RootName = "Level03_Extended";
        private const string GroupName = "Section5_EnvTest";

        private const string SoilSpritePath = "Assets/Art/Art/Environment/Props/wall_soil_block.png";

        [MenuItem("StarEco/Setup/Level03 Env Elements")]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.path.Replace('\\', '/').Equals(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            if (GameObject.Find(GroupName) != null)
            {
                Debug.Log($"[Level03Env] {GroupName} 已存在，跳过");
                return;
            }

            var host = GameObject.Find(RootName);
            if (host == null)
            {
                Debug.LogWarning($"[Level03Env] 未找到 {RootName}，请先执行 StarEco/Setup/Level03 Extend");
                return;
            }

            var log = new StringBuilder();
            var group = new GameObject(GroupName);
            group.transform.SetParent(host.transform, false);
            group.transform.position = Vector3.zero;

            BuildConveyor(group.transform, log);
            BuildVent(group.transform, log);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("[Level03Env] 完成 ✓\n" + log);
        }

        private static void BuildConveyor(Transform parent, StringBuilder log)
        {
            var go = new GameObject("Env_Conveyor");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(63f, 0.14f, 0f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2.4f, 1.2f);
            col.offset = new Vector2(0f, 0.56f);

            // 视觉单独挂子物体：如果直接把拉伸缩放放在根节点上，会把碰撞体一起放大
            var sr = CreateVisual(go.transform, 2.4f, 0.28f);
            sr.sortingOrder = 3;
            sr.color = new Color(0.35f, 0.62f, 0.90f);   // 占位：蓝色=向右推

            var zone = go.AddComponent<ConveyorZone>();
            var so = new SerializedObject(zone);
            so.FindProperty("_speed").floatValue = 3.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            log.AppendLine("传送管道 x∈[61.8,64.2] 推力 +3.5u/s（占位蓝块，正式版需新美术）");
        }

        private static void BuildVent(Transform parent, StringBuilder log)
        {
            var go = new GameObject("Env_Vent");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(65.2f, 0.22f, 0f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.6f, 3.4f);
            col.offset = new Vector2(0f, 1.70f);

            var sr = CreateVisual(go.transform, 1.6f, 0.44f);
            sr.sortingOrder = 3;

            go.AddComponent<VentHazard>();

            log.AppendLine("污染喷口 x∈[64.4,66.0] 预警1.2s / 喷射1.8s / 冷却2.6s，顶飞 7u/s、每 0.6s 扣 0.5 心");
        }

        private static Sprite LoadSprite()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SoilSpritePath);
            if (sprite == null)
            {
                Debug.LogWarning($"[Level03Env] 找不到占位精灵 {SoilSpritePath}，元素将不可见");
            }
            return sprite;
        }

        /// <summary>
        /// 生成占位视觉：单独的子物体挂 SpriteRenderer 并按目标世界尺寸缩放。
        /// 缩放放在子物体上，根节点的碰撞体尺寸才不会被一起放大。
        /// </summary>
        private static SpriteRenderer CreateVisual(Transform parent, float width, float height)
        {
            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(parent, false);
            visualGo.transform.localPosition = Vector3.zero;

            var sr = visualGo.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite();

            if (sr.sprite != null)
            {
                var native = sr.sprite.bounds.size;
                float sx = native.x > 0.0001f ? width / native.x : 1f;
                float sy = native.y > 0.0001f ? height / native.y : 1f;
                visualGo.transform.localScale = new Vector3(sx, sy, 1f);
            }

            return sr;
        }
    }
}
