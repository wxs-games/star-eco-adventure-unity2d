using UnityEngine;
using UnityEngine.Video;

namespace StarEco.Level
{
    [RequireComponent(typeof(SpriteRenderer), typeof(VideoPlayer))]
    [DisallowMultipleComponent]
    public sealed class LevelBackgroundVideo : MonoBehaviour
    {
        [SerializeField] private Material _videoMaterial;

        private static readonly int VideoTexture = Shader.PropertyToID("_VideoTex");
        private static readonly int VideoBlend = Shader.PropertyToID("_VideoBlend");
        private static readonly int VideoUv = Shader.PropertyToID("_VideoUV");
        private SpriteRenderer _background;
        private VideoPlayer _player;
        private Material _stillMaterial;
        private MaterialPropertyBlock _stillProperties;
        private RenderTexture _target;

        private void OnEnable()
        {
            _background = GetComponent<SpriteRenderer>();
            _player = GetComponent<VideoPlayer>();
            if (_videoMaterial == null || _player.clip == null) return;

            _stillMaterial = _background.sharedMaterial;
            _stillProperties = new MaterialPropertyBlock();
            _background.GetPropertyBlock(_stillProperties);
            _target = new RenderTexture((int)_player.clip.width, (int)_player.clip.height, 0, RenderTextureFormat.ARGB32)
            {
                name = _player.clip.name + " render target",
                filterMode = FilterMode.Bilinear
            };
            _player.targetTexture = _target;
            _player.errorReceived += RestoreStill;
            _player.frameReady += ShowVideo;
            _player.sendFrameReadyEvents = true;
            _player.Play();
        }

        private void ShowVideo(VideoPlayer player, long frame)
        {
            var videoProperties = new MaterialPropertyBlock();
            _background.GetPropertyBlock(videoProperties);
            videoProperties.SetTexture(VideoTexture, _target);
            videoProperties.SetFloat(VideoBlend, 1f);
            videoProperties.SetVector(VideoUv, new Vector4(1f, 1f, 0f, 0f));
            _background.sharedMaterial = _videoMaterial;
            _background.SetPropertyBlock(videoProperties);
            player.sendFrameReadyEvents = false;
        }

        private void RestoreStill(VideoPlayer player, string message)
        {
            Debug.LogWarning($"Video playback unavailable for {name} in {gameObject.scene.name}: {message}", this);
            ReleaseVideo();
        }

        private void OnDisable() => ReleaseVideo();

        private void ReleaseVideo()
        {
            if (_target == null) return;

            _player.errorReceived -= RestoreStill;
            _player.frameReady -= ShowVideo;
            _player.sendFrameReadyEvents = false;
            _player.Stop();
            _player.targetTexture = null;
            _background.sharedMaterial = _stillMaterial;
            _background.SetPropertyBlock(_stillProperties);
            _target.Release();
            Destroy(_target);
            _target = null;
        }
    }
}
