using System;
using UnityEngine;

namespace StarEco.Framework
{
    /// <summary>
    /// 音频管理器：BGM（循环+切歌淡入淡出）与 SFX（OneShot 叠加）双通道，
    /// 音量设置经 SaveSystem 持久化到 "settings" 槽。
    /// </summary>
    public class AudioManager : MonoSingleton<AudioManager>
    {
        private const string SettingsSlot = "settings";
        private const float FadeDuration = 0.8f;

        private AudioSource _bgmSource;
        private AudioSource _sfxSource;
        private AudioClip _currentBgm;
        private Coroutine _fadeCoroutine;

        public float BgmVolume { get; private set; } = 0.8f;
        public float SfxVolume { get; private set; } = 1f;

        protected override void OnSingletonAwake()
        {
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;

            LoadSettings();
        }

        #region BGM

        /// <summary>播放 BGM：同一曲目不重启；曲目变化时淡出旧曲淡入新曲。</summary>
        public void PlayBgm(AudioClip clip)
        {
            if (clip == null || clip == _currentBgm)
            {
                return;
            }

            _currentBgm = clip;
            StartFade(clip);
        }

        /// <summary>停止 BGM（淡出）。</summary>
        public void StopBgm()
        {
            _currentBgm = null;
            StartFade(null);
        }

        private void StartFade(AudioClip nextClip)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            _fadeCoroutine = StartCoroutine(FadeBgm(nextClip));
        }

        private System.Collections.IEnumerator FadeBgm(AudioClip nextClip)
        {
            // 淡出当前
            float startVolume = _bgmSource.volume;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / FadeDuration)
            {
                _bgmSource.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }
            _bgmSource.volume = 0f;
            _bgmSource.Stop();

            // 切歌并淡入
            if (nextClip != null)
            {
                _bgmSource.clip = nextClip;
                _bgmSource.Play();
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / FadeDuration)
                {
                    _bgmSource.volume = Mathf.Lerp(0f, BgmVolume, t);
                    yield return null;
                }
                _bgmSource.volume = BgmVolume;
            }

            _fadeCoroutine = null;
        }

        #endregion

        #region SFX

        /// <summary>播放一次性音效，可叠加不打断。</summary>
        public void PlaySfx(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            _sfxSource.PlayOneShot(clip, SfxVolume);
        }

        #endregion

        #region 音量设置

        public void SetBgmVolume(float volume)
        {
            BgmVolume = Mathf.Clamp01(volume);
            if (_fadeCoroutine == null)
            {
                _bgmSource.volume = BgmVolume;
            }

            PersistSettings();
        }

        public void SetSfxVolume(float volume)
        {
            SfxVolume = Mathf.Clamp01(volume);
            PersistSettings();
        }

        private void LoadSettings()
        {
            var data = SaveSystem.Load<GameSettingsData>(SettingsSlot);
            BgmVolume = Mathf.Clamp01(data.bgmVolume);
            SfxVolume = Mathf.Clamp01(data.sfxVolume);
            _bgmSource.volume = BgmVolume;
            _sfxSource.volume = SfxVolume;
        }

        private void PersistSettings()
        {
            SaveSystem.Save(SettingsSlot, new GameSettingsData
            {
                bgmVolume = BgmVolume,
                sfxVolume = SfxVolume
            });
        }

        #endregion
    }

    [Serializable]
    public class GameSettingsData
    {
        public float bgmVolume = 0.8f;
        public float sfxVolume = 1f;
    }
}
