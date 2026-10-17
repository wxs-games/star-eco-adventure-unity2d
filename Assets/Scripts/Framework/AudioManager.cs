using System;
using UnityEngine;

namespace StarEco.Framework
{
    /// <summary>
    /// 音频管理器：BGM（循环+切歌淡入淡出）与 SFX（OneShot 叠加）双通道，
    /// 音量设置经 SaveSystem 持久化到 "settings" 槽。
    /// M10 新增：命名音效 API（PlaySfx(SfxId)）与分关 BGM（PlayTitleBgm/PlayBgmForLevel），
    /// 音频库资产缺失时静默降级，不报错不刷屏。
    /// </summary>
    public class AudioManager : MonoSingleton<AudioManager>
    {
        private const string SettingsSlot = "settings";
        private const float FadeDuration = 0.8f;

        /// <summary>音频库资产 Resources 路径（M10）。</summary>
        private const string LibraryResourcePath = "Audio/GameAudioLibrary";

        private AudioSource _bgmSource;
        private AudioSource _sfxSource;
        private AudioClip _currentBgm;
        private Coroutine _fadeCoroutine;

        // M10：音频库懒加载（解析过一次就不再重试，避免缺失时每帧报错）
        private GameAudioLibrary _library;
        private bool _libraryResolved;

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

        #region M10 命名音效 / 分关 BGM

        /// <summary>音频库懒加载：缺失时返回 null 并缓存结果（静默降级，R1.7）。</summary>
        private GameAudioLibrary Library
        {
            get
            {
                if (!_libraryResolved)
                {
                    _library = Resources.Load<GameAudioLibrary>(LibraryResourcePath);
                    _libraryResolved = true;
                    if (_library == null)
                    {
                        Debug.LogWarning("[AudioManager] 未找到音频库资产 Resources/Audio/GameAudioLibrary，音效将静默跳过（可运行菜单 StarEco/Setup/GameAudioLibrary 生成）。");
                    }
                }
                return _library;
            }
        }

        /// <summary>播放命名音效（M10）：库缺失或条目为空时静默跳过。</summary>
        public void PlaySfx(SfxId id)
        {
            var lib = Library;
            if (lib == null)
            {
                return;
            }

            PlaySfx(lib.GetSfx(id));
        }

        /// <summary>播放标题页 BGM（M10）。</summary>
        public void PlayTitleBgm()
        {
            var lib = Library;
            if (lib == null)
            {
                return;
            }

            PlayBgm(lib.TitleBgm);
        }

        /// <summary>播放指定关卡的 BGM（M10）：levelIndex 1/2/3，0 或越界回退标题曲。</summary>
        public void PlayBgmForLevel(int levelIndex)
        {
            var lib = Library;
            if (lib == null)
            {
                return;
            }

            PlayBgm(lib.GetBgm(levelIndex));
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

        /// <summary>画质档位 0/1/2（M7 设置面板新增字段，默认中档）。</summary>
        public int qualityLevel = 1;
    }
}
