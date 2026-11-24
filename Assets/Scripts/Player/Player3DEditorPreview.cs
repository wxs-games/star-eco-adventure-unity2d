#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarEco.Player
{
    [InitializeOnLoad]
    internal static class Player3DEditorPreview
    {
        private sealed class Preview
        {
            internal readonly GameObject Source;
            internal readonly AnimationClip Idle;
            internal readonly GameObject Root;
            private readonly GameObject _model;
            private readonly SpriteRenderer _body;
            private readonly SpriteRenderer _tool;
            private readonly bool _bodyWasHidden, _toolWasHidden;
            private readonly SkinnedMeshRenderer[] _renderers;
            private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();

            internal Preview(Player3DGameplayVisual player, SpriteRenderer body, GameObject source, AnimationClip idle)
            {
                Source = source;
                Idle = idle;
                _body = body;
                _tool = player.GetComponentInChildren<PlayerToolAnchor>(true)?.ToolRenderer;
                _bodyWasHidden = body.forceRenderingOff;
                _toolWasHidden = _tool != null && _tool.forceRenderingOff;

                Root = EditorUtility.CreateGameObjectWithHideFlags("Player 3D Edit Preview", HideFlags.HideAndDontSave);
                if (Root.scene != player.gameObject.scene)
                    SceneManager.MoveGameObjectToScene(Root, player.gameObject.scene);
                _model = Object.Instantiate(source, Root.transform, false);
                if (_model.TryGetComponent<Animator>(out var animator)) animator.enabled = false;
                Idle.SampleAnimation(_model, 0f);
                _renderers = _model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var renderer in _renderers)
                    renderer.updateWhenOffscreen = true;
            }

            internal void Sync(Player3DGameplayVisual player)
            {
                Transform owner = player.transform;
                Root.transform.SetPositionAndRotation(owner.position, owner.rotation);
                Root.transform.localScale = owner.lossyScale;
                player.PlaceStandingModel(_model.transform);
                _model.transform.localRotation = Player3DGameplayVisual.ModelFacing(_body.flipX);

                _body.forceRenderingOff = true;
                if (_tool != null) _tool.forceRenderingOff = true;
                _properties.SetColor(Tint, _body.color);
                foreach (var renderer in _renderers)
                {
                    renderer.sortingLayerID = _body.sortingLayerID;
                    renderer.sortingOrder = _body.sortingOrder;
                    renderer.SetPropertyBlock(_properties);
                }
            }

            internal void Dispose()
            {
                if (_body != null) _body.forceRenderingOff = _bodyWasHidden;
                if (_tool != null) _tool.forceRenderingOff = _toolWasHidden;
                if (Root != null) Object.DestroyImmediate(Root);
            }
        }

        private static readonly int Tint = Shader.PropertyToID("_Color");
        private static readonly Dictionary<Player3DGameplayVisual, Preview> Previews =
            new Dictionary<Player3DGameplayVisual, Preview>();
        private static readonly HashSet<Player3DGameplayVisual> VisiblePlayers = new HashSet<Player3DGameplayVisual>();
        private static readonly List<Player3DGameplayVisual> RemovedPlayers = new List<Player3DGameplayVisual>();
        private static double _nextUpdate;

        static Player3DEditorPreview()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.quitting += Clear;
        }

        private static void Update()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || BuildPipeline.isBuildingPlayer || EditorApplication.timeSinceStartup < _nextUpdate)
                return;

            _nextUpdate = EditorApplication.timeSinceStartup + .1;
            VisiblePlayers.Clear();
            foreach (var player in StageUtility.GetCurrentStageHandle().FindComponentsOfType<Player3DGameplayVisual>())
            {
                if (!player.isActiveAndEnabled || EditorUtility.IsPersistent(player)
                    || SceneVisibilityManager.instance.IsHidden(player.gameObject)
                    || !player.TryGetComponent<SpriteRenderer>(out var body) || !body.enabled
                    || !player.TryGetPreviewAssets(out var prefab, out var idle))
                    continue;

                VisiblePlayers.Add(player);
                if (!Previews.TryGetValue(player, out var preview) || preview.Root == null
                    || preview.Source != prefab || preview.Idle != idle)
                {
                    preview?.Dispose();
                    Previews[player] = preview = new Preview(player, body, prefab, idle);
                    SceneView.RepaintAll();
                }
                preview.Sync(player);
            }

            RemovedPlayers.Clear();
            foreach (var pair in Previews)
            {
                if (!VisiblePlayers.Contains(pair.Key)) RemovedPlayers.Add(pair.Key);
            }
            foreach (var player in RemovedPlayers)
            {
                Previews[player].Dispose();
                Previews.Remove(player);
            }
            if (RemovedPlayers.Count > 0) SceneView.RepaintAll();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) Clear();
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                _nextUpdate = 0;
                Update();
                SceneView.RepaintAll();
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        private static void Clear()
        {
            foreach (var preview in Previews.Values) preview.Dispose();
            Previews.Clear();
        }
    }
}
#endif
