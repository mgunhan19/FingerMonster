using UnityEngine;

namespace FingerMonster.Audio
{
    /// <summary>
    /// Oyun içi tüm ses efektleri, absürt kasap silahı sesleri, komik kıkırdamalar
    /// ve gerilimli çizgi film müziklerini tek merkezden yöneten singleton ses yöneticisi.
    /// Harici dosya gerektirmeden sıfırdan sentetik ses dalgaları üretir.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Ses Kaynakları")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioLowPassFilter lowPassFilter;

        [Header("Özel Ses Klipleri (Opsiyonel)")]
        [SerializeField] private AudioClip typewriterClickClip;
        [SerializeField] private AudioClip trapStingerClip;
        [SerializeField] private AudioClip bellDingClip;
        [SerializeField] private AudioClip butcherStingerClip;
        [SerializeField] private AudioClip uiClickClip;

        [Header("Absürt Kasap Silah Sesleri")]
        [SerializeField] private AudioClip malletBonkClip;
        [SerializeField] private AudioClip panDongClip;
        [SerializeField] private AudioClip whipSlapClip;
        [SerializeField] private AudioClip pieSplatClip;
        [SerializeField] private AudioClip butcherGiggleClip;
        [SerializeField] private AudioClip cartoonSuspenseClip;

        [Header("Ses Seviyeleri")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.65f;

        public float MasterVolume => masterVolume;
        public float SfxVolume => sfxVolume;
        public float MusicVolume => musicVolume;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (GetComponents<Component>().Length <= 2) Destroy(gameObject);
                else Destroy(this);
                return;
            }

            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);

            EnsureAudioSources();
            EnsureClipsGenerated();
            ApplyVolumes();
        }

        private void Start()
        {
            StartCartoonSuspenseLoop();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void EnsureAudioSources()
        {
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
            }

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
            }

            if (lowPassFilter == null)
            {
                lowPassFilter = GetComponent<AudioLowPassFilter>();
                if (lowPassFilter == null) lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
                lowPassFilter.enabled = false;
                lowPassFilter.cutoffFrequency = 900f;
            }
        }

        private void EnsureClipsGenerated()
        {
            if (typewriterClickClip == null) typewriterClickClip = SoundEffectsGenerator.CreateTypewriterClickClip();
            if (trapStingerClip == null) trapStingerClip = SoundEffectsGenerator.CreateTrapStingerClip();
            if (bellDingClip == null) bellDingClip = SoundEffectsGenerator.CreateBellDingClip();
            if (butcherStingerClip == null) butcherStingerClip = SoundEffectsGenerator.CreateButcherStingerClip();
            if (uiClickClip == null) uiClickClip = SoundEffectsGenerator.CreateUIClickClip();

            if (malletBonkClip == null) malletBonkClip = SoundEffectsGenerator.CreateMalletBonkClip();
            if (panDongClip == null) panDongClip = SoundEffectsGenerator.CreatePanDongClip();
            if (whipSlapClip == null) whipSlapClip = SoundEffectsGenerator.CreateWhipSlapClip();
            if (pieSplatClip == null) pieSplatClip = SoundEffectsGenerator.CreatePieSplatClip();
            if (butcherGiggleClip == null) butcherGiggleClip = SoundEffectsGenerator.CreateButcherGiggleClip();
            if (cartoonSuspenseClip == null) cartoonSuspenseClip = SoundEffectsGenerator.CreateCartoonSuspenseMusicLoop();
        }

        public void PlayTypewriterClick() => PlayClip(typewriterClickClip, 1.0f);
        public void PlayTrapTriggered() => PlayClip(trapStingerClip, 1.0f);
        public void PlayTrapSting() => PlayClip(trapStingerClip, 1.0f);
        public void PlayBell() => PlayClip(bellDingClip, 0.9f);
        public void PlayButcherStinger() => PlayClip(butcherStingerClip, 1.0f);
        public void PlayUIClick() => PlayClip(uiClickClip, 0.8f);

        // Absürt Silah Sesleri
        public void PlayMalletBonk() => PlayClip(malletBonkClip, 1.0f);
        public void PlayPanDong() => PlayClip(panDongClip, 1.0f);
        public void PlayWhipSlap() => PlayClip(whipSlapClip, 1.0f);
        public void PlayPieSplat() => PlayClip(pieSplatClip, 1.0f);
        public void PlayButcherGiggle() => PlayClip(butcherGiggleClip, 0.8f);

        public void StartCartoonSuspenseLoop()
        {
            if (musicSource != null && cartoonSuspenseClip != null)
            {
                musicSource.clip = cartoonSuspenseClip;
                musicSource.volume = musicVolume * masterVolume;
                musicSource.Play();
            }
        }

        public void StopCartoonSuspenseLoop()
        {
            if (musicSource != null) musicSource.Stop();
        }

        /// <summary>
        /// Diğer oyuncular için kasap sesini uzaktan/boğuk hissettiren alçak geçiren filtre.
        /// </summary>
        public void SetExecutionAudioFilter(bool muffled)
        {
            if (lowPassFilter != null)
            {
                lowPassFilter.enabled = muffled;
            }

            if (sfxSource != null)
            {
                sfxSource.pitch = muffled ? 0.85f : 1.0f;
            }
        }

        private void PlayClip(AudioClip clip, float pitchJitter = 1.0f)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.pitch = Random.Range(0.96f, 1.04f);
            sfxSource.PlayOneShot(clip, sfxVolume * masterVolume);
        }

        public void SetMasterVolume(float vol)
        {
            masterVolume = Mathf.Clamp01(vol);
            ApplyVolumes();
        }

        public void SetSfxVolume(float vol)
        {
            sfxVolume = Mathf.Clamp01(vol);
            ApplyVolumes();
        }

        public void SetMusicVolume(float vol)
        {
            musicVolume = Mathf.Clamp01(vol);
            ApplyVolumes();
        }

        private void ApplyVolumes()
        {
            AudioListener.volume = masterVolume;
            if (musicSource != null)
            {
                musicSource.volume = musicVolume * masterVolume;
            }
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume * masterVolume;
            }
        }
    }
}
