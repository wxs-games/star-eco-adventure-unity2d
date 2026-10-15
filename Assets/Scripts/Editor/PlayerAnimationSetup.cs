using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarEco.EditorTools
{
    /// <summary>
    /// 一键搭建玩家动画管线：
    /// 1. 把 Art/Art/Player/Frames 下的 png 统一设为 Sprite 导入
    /// 2. 按目录生成 AnimationClip（12fps）
    /// 3. 生成 AnimatorController（状态机 + 转移条件）
    /// 4. 生成 PlayerConfig 资产与 Player 预制体
    /// 菜单：StarEco → Setup → Player Animation（幂等，可重复运行）
    /// </summary>
    public static class PlayerAnimationSetup
    {
        private const string FramesRoot = "Assets/Art/Art/Player/Frames";
        private const string OutputRoot = "Assets/Animations/Player";
        private const string ConfigPath = "Assets/Settings/PlayerConfig.asset";
        private const string ControllerPath = "Assets/Animations/Player/Player.controller";
        private const string PrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const float FrameRate = 12f;

        // 目录名 → 是否循环播放
        private static readonly Dictionary<string, bool> AnimLoopMap = new Dictionary<string, bool>
        {
            { "Player_Idle", true },
            { "Player_Run", true },
            { "Player_Jump", false },
            { "Player_Fall", false },
            { "Player_Land", false },
            { "Player_Hurt", false },
            { "Player_Die", false },
            { "Player_Attack01", false },
            { "Player_Interact", false },
        };

        [MenuItem("StarEco/Setup/Player Animation")]
        public static void Run()
        {
            EnsureFolder(OutputRoot);
            EnsureFolder("Assets/Settings");
            EnsureFolder("Assets/Prefabs/Player");

            var clips = BuildAllClips();
            var controller = BuildController(clips);
            var config = EnsureConfig();
            BuildPrefab(controller, config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PlayerAnimationSetup] 玩家动画管线搭建完成 ✓");
        }

        // ===== 1. Sprite 导入 =====

        private static void EnsureSpriteImport(string pngPath)
        {
            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer == null || (importer.textureType == TextureImporterType.Sprite &&
                                     importer.spriteImportMode == SpriteImportMode.Single))
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        // ===== 2. AnimationClip 生成 =====

        private static Dictionary<string, AnimationClip> BuildAllClips()
        {
            var clips = new Dictionary<string, AnimationClip>();

            foreach (var animName in AnimLoopMap.Keys)
            {
                var folder = $"{FramesRoot}/{animName}";
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.LogWarning($"[PlayerAnimationSetup] 缺少动画目录：{folder}，跳过。");
                    continue;
                }

                var clip = BuildClip(animName, folder, AnimLoopMap[animName]);
                if (clip != null)
                {
                    clips[animName] = clip;
                }
            }

            return clips;
        }

        private static AnimationClip BuildClip(string animName, string folder, bool loop)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            var pngPaths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".png"))
                .OrderBy(NaturalOrder)
                .ToList();

            if (pngPaths.Count == 0)
            {
                Debug.LogWarning($"[PlayerAnimationSetup] 目录 {folder} 内没有 png 帧，跳过。");
                return null;
            }

            foreach (var path in pngPaths)
            {
                EnsureSpriteImport(path);
            }
            AssetDatabase.Refresh();

            var keyframes = new ObjectReferenceKeyframe[pngPaths.Count];
            for (int i = 0; i < pngPaths.Count; i++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPaths[i]);
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i / FrameRate,
                    value = sprite
                };
            }

            var clip = new AnimationClip { frameRate = FrameRate };
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var clipPath = $"{OutputRoot}/{animName}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                Object.DestroyImmediate(clip);
                clip = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            Debug.Log($"[PlayerAnimationSetup] 生成 {animName}（{pngPaths.Count} 帧, {(loop ? "循环" : "单次")}）");
            return clip;
        }

        /// <summary>自然排序：Player_Run_02 排在 Player_Run_10 之前。</summary>
        private static string NaturalOrder(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            return Regex.Replace(name, @"\d+", m => m.Value.PadLeft(6, '0'));
        }

        // ===== 3. AnimatorController 生成 =====

        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            // 参数
            controller.parameters = new[]
            {
                new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float, defaultFloat = 0f },
                new AnimatorControllerParameter { name = "IsGrounded", type = AnimatorControllerParameterType.Bool, defaultBool = true },
                new AnimatorControllerParameter { name = "VelocityY", type = AnimatorControllerParameterType.Float, defaultFloat = 0f },
                new AnimatorControllerParameter { name = "Hurt", type = AnimatorControllerParameterType.Trigger },
                new AnimatorControllerParameter { name = "Die", type = AnimatorControllerParameterType.Trigger },
            };

            var root = controller.layers[0].stateMachine;
            root.states = new ChildAnimatorState[0];
            root.anyStateTransitions = new AnimatorStateTransition[0];
            root.entryTransitions = new AnimatorTransition[0];

            var idle = AddState(root, "Idle", GetClip(clips, "Player_Idle"), new Vector2(200, 0));
            var run = AddState(root, "Run", GetClip(clips, "Player_Run"), new Vector2(400, 0));
            var jump = AddState(root, "Jump", GetClip(clips, "Player_Jump"), new Vector2(400, 160));
            var fall = AddState(root, "Fall", GetClip(clips, "Player_Fall"), new Vector2(600, 160));
            var land = AddState(root, "Land", GetClip(clips, "Player_Land"), new Vector2(400, 320));
            var hurt = AddState(root, "Hurt", GetClip(clips, "Player_Hurt"), new Vector2(200, 480));
            var die = AddState(root, "Die", GetClip(clips, "Player_Die"), new Vector2(400, 480));
            root.defaultState = idle;

            // ---- 地面移动循环 ----
            Link(idle, run, t => t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"));
            Link(run, idle, t => t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"));

            // ---- 起跳 / 离地 ----
            Link(idle, jump, t => t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded"));
            Link(run, jump, t => t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded"));
            Link(jump, fall, t => t.AddCondition(AnimatorConditionMode.Less, -0.1f, "VelocityY"));

            // ---- 落地 ----
            Link(jump, land, t => t.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded"));
            Link(fall, land, t => t.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded"));

            // ---- Land 动画播完回待机；中途离地回空中 ----
            Link(land, idle, t =>
            {
                t.hasExitTime = true;
                t.exitTime = 0.9f;
            });
            Link(land, jump, t => t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded"));

            // ---- Hurt：任意状态进入，播放完回 Idle ----
            foreach (var state in new[] { idle, run, jump, fall, land })
            {
                var t = root.AddAnyStateTransition(hurt);
                t.hasExitTime = false;
                t.duration = 0.02f;
                t.AddCondition(AnimatorConditionMode.If, 0, "Hurt");
            }

            Link(hurt, idle, t =>
            {
                t.hasExitTime = true;
                t.exitTime = 0.95f;
            });

            // ---- Die：任意状态进入，不退出（由重生逻辑复位） ----
            foreach (var state in new[] { idle, run, jump, fall, land, hurt })
            {
                var t = root.AddAnyStateTransition(die);
                t.hasExitTime = false;
                t.duration = 0.02f;
                t.AddCondition(AnimatorConditionMode.If, 0, "Die");
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState AddState(AnimatorStateMachine root, string name, AnimationClip clip, Vector2 position)
        {
            var state = root.AddState(name, position);
            state.motion = clip;
            return state;
        }

        private static void Link(AnimatorState from, AnimatorState to, System.Action<AnimatorStateTransition> setup)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.05f;
            t.interruptionSource = TransitionInterruptionSource.Source;
            setup?.Invoke(t);
        }

        private static AnimationClip GetClip(Dictionary<string, AnimationClip> clips, string key)
        {
            if (clips.TryGetValue(key, out var clip))
            {
                return clip;
            }

            Debug.LogError($"[PlayerAnimationSetup] 缺少 clip {key}，动画状态将为空。");
            return null;
        }

        // ===== 4. PlayerConfig 资产 =====

        private static StarEco.Player.PlayerConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StarEco.Player.PlayerConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<StarEco.Player.PlayerConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                Debug.Log($"[PlayerAnimationSetup] 已创建默认配置 {ConfigPath}");
            }

            return config;
        }

        // ===== 5. Player 预制体 =====

        private static void BuildPrefab(AnimatorController controller, StarEco.Player.PlayerConfig config)
        {
            // 用 Idle 第一帧做占位显示（外观由美术帧决定，尺寸由碰撞体决定）
            var idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{OutputRoot}/Player_Idle.anim");
            Sprite displaySprite = null;
            if (idleClip != null)
            {
                var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
                var keys = AnimationUtility.GetObjectReferenceCurve(idleClip, binding);
                if (keys != null && keys.Length > 0)
                {
                    displaySprite = keys[0].value as Sprite;
                }
            }

            var go = new GameObject("Player");
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = displaySprite;

            var body = go.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = go.AddComponent<BoxCollider2D>();
            if (displaySprite != null)
            {
                var size = displaySprite.bounds.size * 0.9f; // 碰撞体略小于显示，宽容手感
                collider.size = size;
                collider.offset = new Vector2(0f, -displaySprite.bounds.size.y * 0.05f);
            }

            go.AddComponent<Animator>().runtimeAnimatorController = controller;

            var player = go.AddComponent<StarEco.Player.PlayerController>();

            // 私有字段序列化注入（无需 public setter）
            var so = new SerializedObject(player);
            so.FindProperty("_config").objectReferenceValue = config;
            so.ApplyModifiedProperties();

            go.AddComponent<StarEco.Player.PlayerAnimatorDriver>();

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                // 覆盖：保留已有预制体资产身份
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Object.DestroyImmediate(go);
            }
            else
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabPath, InteractionMode.AutomatedAction);
            }

            Debug.Log($"[PlayerAnimationSetup] 预制体已生成 {PrefabPath}");
        }

        // ===== 工具 =====

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
