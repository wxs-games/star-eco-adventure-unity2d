using System.Collections.Generic;
using StarEco.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Video;

namespace StarEco.EditorTools
{
    [InitializeOnLoad]
    internal static class LevelBackgroundVideoPreview
    {
        private const string PreviewFolder = "Assets/Editor/BackgroundPreviews/";
        private const int Columns = 8;
        private const int TileWidth = 320;
        private const int TileHeight = 180;
        private const int RowStride = 184;
        private const int FrameRate = 10;
        private static readonly int VideoTexture = Shader.PropertyToID("_VideoTex");
        private static readonly int VideoBlend = Shader.PropertyToID("_VideoBlend");
        private static readonly int VideoUv = Shader.PropertyToID("_VideoUV");
        private static readonly int EnvironmentPreviewClock = Shader.PropertyToID("_EnvironmentPreviewClock");
        private static readonly List<Preview> Previews = new List<Preview>();
        private static readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
        private static double _nextScan;
        private static int _lastFrame = -1;

        private sealed class Preview
        {
            internal SpriteRenderer Renderer;
            internal Texture2D Frames;
            internal int FrameCount;
            internal readonly MaterialPropertyBlock OriginalProperties = new MaterialPropertyBlock();
        }

        static LevelBackgroundVideoPreview()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
        }

        private static void Update()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || BuildPipeline.isBuildingPlayer)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now >= _nextScan)
            {
                _nextScan = now + 1;
                Refresh();
            }

            int timeFrame = (int)(now * FrameRate);
            if (Previews.Count == 0 || _lastFrame == timeFrame) return;
            _lastFrame = timeFrame;
            Shader.SetGlobalVector(EnvironmentPreviewClock, new Vector4(1f, (float)now, 0f, 0f));

            foreach (var preview in Previews)
            {
                if (preview.Renderer == null) continue;
                int frame = timeFrame % preview.FrameCount;
                int column = frame % Columns;
                int row = frame / Columns;
                float width = preview.Frames.width;
                float height = preview.Frames.height;
                // Inset to texel centers so bilinear filtering cannot bleed between frames.
                var uv = new Vector4((TileWidth - 1f) / width, (TileHeight - 1f) / height,
                    (column * TileWidth + .5f) / width, 1f - (row * RowStride + TileHeight - .5f) / height);
                preview.Renderer.GetPropertyBlock(Properties);
                Properties.SetTexture(VideoTexture, preview.Frames);
                Properties.SetFloat(VideoBlend, 1f);
                Properties.SetVector(VideoUv, uv);
                preview.Renderer.SetPropertyBlock(Properties);
            }

            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void Refresh()
        {
            Clear();
            var stage = StageUtility.GetCurrentStageHandle();
            foreach (var background in stage.FindComponentsOfType<LevelBackgroundVideo>())
            {
                if (!background.isActiveAndEnabled) continue;
                var video = background.GetComponent<VideoPlayer>();
                if (video.clip == null) continue;
                var frames = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    PreviewFolder + video.clip.name + "_preview.jpg");
                if (frames == null) continue;

                var preview = new Preview
                {
                    Renderer = background.GetComponent<SpriteRenderer>(),
                    Frames = frames,
                    FrameCount = Mathf.RoundToInt((float)video.clip.length * FrameRate)
                };
                preview.Renderer.GetPropertyBlock(preview.OriginalProperties);
                Previews.Add(preview);
            }
        }

        private static void OnModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) Clear();
            if (state == PlayModeStateChange.EnteredEditMode) _nextScan = 0;
        }

        private static void Clear()
        {
            Shader.SetGlobalVector(EnvironmentPreviewClock, Vector4.zero);
            foreach (var preview in Previews)
                if (preview.Renderer != null)
                    preview.Renderer.SetPropertyBlock(preview.OriginalProperties);
            Previews.Clear();
            _lastFrame = -1;
        }
    }
}
