using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using FingerMonster.Core;
using FingerMonster.Gameplay;

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

        [Header("Audio Mixer Grupları")]
        [SerializeField] private AudioMixerGroup ghostMixerGroup;
        [SerializeField] private AudioMixerGroup sfxMixerGroup;
        [SerializeField] private AudioMixerGroup musicMixerGroup;

        [Header("3D Spatial Havuz Ayarları")]
        [SerializeField] private int spatialPoolSize = 10;
        private readonly List<AudioSource> _spatialPool = new List<AudioSource>();

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

        [Header("Hayalet Ses Efektleri")]
        [SerializeField] private AudioClip ghostCurseClip;
        [SerializeField] private AudioClip ghostCurseExplosionClip;
        [SerializeField] private AudioClip ghostFlickerLampClip;
        [SerializeField] private AudioClip ghostEerieLaughClip;
        [SerializeField] private AudioClip ghostFlyPaperClip;
        [SerializeField] private AudioClip ghostColdBreezeClip;
        [SerializeField] private AudioClip ghostApplauseClip;
        [SerializeField] private AudioClip ghostSadBooClip;
        [SerializeField] private AudioClip ghostBuuuScareClip;
        [SerializeField] private AudioClip ghostThrowWhooshClip;
        [SerializeField] private AudioClip ghostPieSplatClip;
        [SerializeField] private AudioClip ghostTomatoSplatClip;
        [SerializeField] private AudioClip ghostPaperHitClip;
        [SerializeField] private AudioClip ghostSausageBonkClip;

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
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

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
            if (sfxMixerGroup != null) sfxSource.outputAudioMixerGroup = sfxMixerGroup;

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
            }
            if (musicMixerGroup != null) musicSource.outputAudioMixerGroup = musicMixerGroup;

            if (lowPassFilter == null)
            {
                lowPassFilter = GetComponent<AudioLowPassFilter>();
                if (lowPassFilter == null) lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
                lowPassFilter.enabled = false;
                lowPassFilter.cutoffFrequency = 900f;
            }

            EnsureSpatialPool();
        }

        private void EnsureSpatialPool()
        {
            if (_spatialPool.Count > 0) return;
            int count = Mathf.Max(4, spatialPoolSize);
            for (int i = 0; i < count; i++)
            {
                _spatialPool.Add(CreateSpatialAudioSource(i));
            }
        }

        private AudioSource CreateSpatialAudioSource(int index)
        {
            GameObject go = new GameObject($"SpatialAudio_{index}");
            go.transform.SetParent(transform, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            return src;
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

            if (ghostCurseClip == null) ghostCurseClip = CreateGhostCurseClip();
            if (ghostCurseExplosionClip == null) ghostCurseExplosionClip = CreateGhostCurseExplosionClip();
            if (ghostFlickerLampClip == null) ghostFlickerLampClip = CreateGhostFlickerLampClip();
            if (ghostEerieLaughClip == null) ghostEerieLaughClip = CreateGhostEerieLaughClip();
            if (ghostFlyPaperClip == null) ghostFlyPaperClip = CreateGhostFlyPaperClip();
            if (ghostColdBreezeClip == null) ghostColdBreezeClip = CreateGhostColdBreezeClip();
            if (ghostApplauseClip == null) ghostApplauseClip = CreateGhostApplauseClip();
            if (ghostSadBooClip == null) ghostSadBooClip = CreateGhostSadBooClip();
            if (ghostBuuuScareClip == null) ghostBuuuScareClip = CreateGhostBuuuScareClip();
            if (ghostThrowWhooshClip == null) ghostThrowWhooshClip = CreateGhostThrowWhooshClip();
            if (ghostPieSplatClip == null) ghostPieSplatClip = SoundEffectsGenerator.CreatePieSplatClip();
            if (ghostTomatoSplatClip == null) ghostTomatoSplatClip = CreateGhostTomatoSplatClip();
            if (ghostPaperHitClip == null) ghostPaperHitClip = CreateGhostPaperHitClip();
            if (ghostSausageBonkClip == null) ghostSausageBonkClip = CreateGhostSausageBonkClip();
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

        // Hayalet Ses Efektleri
        public void PlayGhostCurse(Vector3? worldPos = null) => PlayClipOrSpatial(ghostCurseClip, worldPos, 1.0f);
        public void PlayGhostCurseExplosion(Vector3? worldPos = null) => PlayClipOrSpatial(ghostCurseExplosionClip, worldPos, 1.0f);

        public void PlayGhostEffect(GhostEffectType effectType, Vector3? worldPos = null)
        {
            AudioClip clip = effectType switch
            {
                GhostEffectType.FlickerLamp => ghostFlickerLampClip,
                GhostEffectType.EerieLaugh => ghostEerieLaughClip,
                GhostEffectType.FlyPaper => ghostFlyPaperClip,
                GhostEffectType.ColdBreeze => ghostColdBreezeClip,
                GhostEffectType.Applause => ghostApplauseClip,
                GhostEffectType.SadBoo => ghostSadBooClip,
                _ => null
            };
            PlayClipOrSpatial(clip, worldPos, 1.0f);
        }

        public void PlayGhostScare(Vector3 worldPos) => PlayGhostSpatial(ghostBuuuScareClip, worldPos, 1.0f);
        public void PlayGhostThrow(Vector3 worldPos) => PlayGhostSpatial(ghostThrowWhooshClip, worldPos, 0.9f);

        /// <summary>
        /// Fırlatılabilir hayalet nesnesi çarpma sesi.
        /// Indeksler ThrowableItemType ile eşleşir: 0=CreamPie, 1=Tomato, 2=PaperBall, 3=Sausage.
        /// </summary>
        public void PlayGhostSplat(int itemTypeIndex, Vector3 worldPos)
        {
            AudioClip clip = itemTypeIndex switch
            {
                0 => ghostPieSplatClip,
                1 => ghostTomatoSplatClip,
                2 => ghostPaperHitClip,
                3 => ghostSausageBonkClip,
                _ => ghostPieSplatClip
            };
            PlayGhostSpatial(clip, worldPos, 1.0f);
        }

        public void PlayGhostSplat(ThrowableItemType item, Vector3 worldPos) => PlayGhostSplat((int)item, worldPos);

        public void PlayButcherWeaponHit(ExecutionWeaponType weapon, Vector3 worldPos)
        {
            AudioClip clip = weapon switch
            {
                ExecutionWeaponType.GiantMeatMallet => malletBonkClip,
                ExecutionWeaponType.FlyingCastIronPan => panDongClip,
                ExecutionWeaponType.SausageWhip => whipSlapClip,
                ExecutionWeaponType.CreamPieLauncher => pieSplatClip,
                _ => malletBonkClip
            };
            PlaySpatialOnGroup(clip, worldPos, sfxMixerGroup, 1.0f, 1.5f, 18f);
        }

        public void PlayGhostSpatial(AudioClip clip, Vector3 worldPos, float volumeScale = 1f, float minDist = 1.2f, float maxDist = 14f)
        {
            PlaySpatialOnGroup(clip, worldPos, ghostMixerGroup, volumeScale, minDist, maxDist);
        }

        private void PlayClipOrSpatial(AudioClip clip, Vector3? worldPos, float volumeScale)
        {
            if (worldPos.HasValue)
            {
                PlayGhostSpatial(clip, worldPos.Value, volumeScale);
            }
            else
            {
                PlayClip(clip, volumeScale);
            }
        }

        private void PlaySpatialOnGroup(AudioClip clip, Vector3 worldPos, AudioMixerGroup mixerGroup, float volumeScale, float minDist, float maxDist)
        {
            if (clip == null) return;
            EnsureSpatialPool();

            AudioSource src = GetAvailableSpatialSource();
            src.transform.position = worldPos;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = Mathf.Max(0.1f, minDist);
            src.maxDistance = Mathf.Max(src.minDistance + 0.5f, maxDist);
            src.outputAudioMixerGroup = mixerGroup;
            src.pitch = Random.Range(0.95f, 1.05f);
            src.volume = 1f;
            src.PlayOneShot(clip, Mathf.Clamp01(volumeScale) * sfxVolume * masterVolume);
        }

        private AudioSource GetAvailableSpatialSource()
        {
            for (int i = 0; i < _spatialPool.Count; i++)
            {
                if (_spatialPool[i] != null && !_spatialPool[i].isPlaying)
                {
                    return _spatialPool[i];
                }
            }

            AudioSource extra = CreateSpatialAudioSource(_spatialPool.Count);
            _spatialPool.Add(extra);
            return extra;
        }

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

        #region Prosedürel Hayalet Sesleri

        private static AudioClip CreateProceduralClip(string clipName, float duration, System.Func<float, float> sampleFunc)
        {
            const int sampleRate = 44100;
            int length = Mathf.Max(1, (int)(sampleRate * duration));
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                data[i] = Mathf.Clamp(sampleFunc(t), -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(clipName, length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateGhostCurseClip()
        {
            return CreateProceduralClip("GhostCurse_Proc", 0.55f, t =>
            {
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.55f)) * Mathf.Exp(-t * 2.5f);
                float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(110f, 65f, t / 0.55f) * t) * 0.55f;
                float whisper = (Random.value * 2f - 1f) * 0.25f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 6f * t));
                return (sub + whisper) * env;
            });
        }

        private static AudioClip CreateGhostCurseExplosionClip()
        {
            return CreateProceduralClip("GhostCurseExplosion_Proc", 0.65f, t =>
            {
                float env = Mathf.Exp(-t * 5.5f);
                float boom = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(260f, 55f, t / 0.4f) * t) * 0.55f;
                float cackle = Mathf.Sin(2f * Mathf.PI * (520f + 160f * Mathf.Sin(2f * Mathf.PI * 14f * t)) * t) * 0.35f;
                float burst = (Random.value * 2f - 1f) * Mathf.Exp(-t * 18f) * 0.4f;
                return (boom + cackle + burst) * env;
            });
        }

        private static AudioClip CreateGhostFlickerLampClip()
        {
            return CreateProceduralClip("GhostFlickerLamp_Proc", 0.35f, t =>
            {
                float buzz = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.2f;
                float gate = Mathf.Sin(2f * Mathf.PI * 18f * t) > 0f ? 1f : 0.15f;
                float crackle = (Random.value * 2f - 1f) * 0.25f;
                return (buzz + crackle) * gate * Mathf.Exp(-t * 4f);
            });
        }

        private static AudioClip CreateGhostEerieLaughClip()
        {
            return CreateProceduralClip("GhostEerieLaugh_Proc", 0.7f, t =>
            {
                float pulse = Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 5.5f * t));
                float freq = Mathf.Lerp(620f, 340f, t / 0.7f) + 90f * Mathf.Sin(2f * Mathf.PI * 11f * t);
                float voice = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * freq * 1.5f * t) * 0.2f;
                return voice * pulse * Mathf.Exp(-t * 1.8f);
            });
        }

        private static AudioClip CreateGhostFlyPaperClip()
        {
            return CreateProceduralClip("GhostFlyPaper_Proc", 0.4f, t =>
            {
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.4f));
                float noise = (Random.value * 2f - 1f) * 0.45f;
                float flutter = Mathf.Sin(2f * Mathf.PI * 24f * t) * 0.5f + 0.5f;
                return noise * flutter * env;
            });
        }

        private static AudioClip CreateGhostColdBreezeClip()
        {
            return CreateProceduralClip("GhostColdBreeze_Proc", 0.8f, t =>
            {
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.8f));
                float wind = (Random.value * 2f - 1f) * 0.28f;
                float howl = Mathf.Sin(2f * Mathf.PI * (320f + 80f * Mathf.Sin(2f * Mathf.PI * 2.5f * t)) * t) * 0.3f;
                return (wind + howl) * env;
            });
        }

        private static AudioClip CreateGhostApplauseClip()
        {
            return CreateProceduralClip("GhostApplause_Proc", 0.75f, t =>
            {
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.75f));
                float clapGate = Mathf.Pow(Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 9f * t)), 4f);
                float clap = (Random.value * 2f - 1f) * 0.65f * (0.35f + 0.65f * clapGate);
                return clap * env;
            });
        }

        private static AudioClip CreateGhostSadBooClip()
        {
            return CreateProceduralClip("GhostSadBoo_Proc", 0.65f, t =>
            {
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.65f));
                float freq = Mathf.Lerp(260f, 130f, t / 0.65f);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.55f + Mathf.Sin(2f * Mathf.PI * freq * 0.5f * t) * 0.25f;
                return tone * env;
            });
        }

        private static AudioClip CreateNormalizedProceduralClip(string clipName, float duration, System.Func<float, float> sampleFunc, float targetPeak = 0.88f)
        {
            const int sampleRate = 44100;
            int length = Mathf.Max(1, (int)(sampleRate * duration));
            float[] data = new float[length];
            float maxAbs = 0.0001f;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float val = sampleFunc(t);
                data[i] = val;
                float absVal = Mathf.Abs(val);
                if (absVal > maxAbs) maxAbs = absVal;
            }
            float scale = targetPeak / maxAbs;
            for (int i = 0; i < length; i++)
            {
                data[i] = Mathf.Clamp(data[i] * scale, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(clipName, length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateGhostBuuuScareClip()
        {
            return CreateNormalizedProceduralClip("GhostBuuuScare_Proc", 0.85f, t =>
            {
                float u = Mathf.Clamp01(t / 0.85f);
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.7f);
                float freq = Mathf.Lerp(310f, 155f, Mathf.Pow(u, 0.8f)) + 18f * Mathf.Sin(2f * Mathf.PI * 6.5f * t);
                float tone = 0.65f * Mathf.Sin(2f * Mathf.PI * freq * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * freq * t);
                float breath = (Random.value * 2f - 1f) * 0.18f;
                return (tone + breath) * env;
            });
        }

        private static AudioClip CreateGhostThrowWhooshClip()
        {
            return CreateNormalizedProceduralClip("GhostThrowWhoosh_Proc", 0.30f, t =>
            {
                float u = Mathf.Clamp01(t / 0.30f);
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 1.2f);
                float noise = (Random.value * 2f - 1f) * 0.55f;
                float sweep = 0.45f * Mathf.Sin(2f * Mathf.PI * (280f + 520f * Mathf.Sin(Mathf.PI * u)) * t);
                return (noise + sweep) * env;
            });
        }

        private static AudioClip CreateGhostTomatoSplatClip()
        {
            return CreateNormalizedProceduralClip("GhostTomatoSplat_Proc", 0.32f, t =>
            {
                float pop = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(380f, 120f, Mathf.Clamp01(t / 0.08f)) * t) * Mathf.Exp(-t * 28f);
                float juice = (Random.value * 2f - 1f) * Mathf.Exp(-t * 12f) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 55f * t));
                return 0.6f * pop + 0.7f * juice;
            });
        }

        private static AudioClip CreateGhostPaperHitClip()
        {
            return CreateNormalizedProceduralClip("GhostPaperHit_Proc", 0.22f, t =>
            {
                float thump = Mathf.Sin(2f * Mathf.PI * 160f * t) * Mathf.Exp(-t * 35f) * 0.4f;
                float crinkle = (Random.value * 2f - 1f) * Mathf.Exp(-t * 18f) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 75f * t));
                return thump + crinkle * 0.8f;
            });
        }

        private static AudioClip CreateGhostSausageBonkClip()
        {
            return CreateNormalizedProceduralClip("GhostSausageBonk_Proc", 0.28f, t =>
            {
                float freq = 190f + 180f * Mathf.Exp(-t * 18f) + 45f * Mathf.Sin(2f * Mathf.PI * 22f * t);
                float env = Mathf.Exp(-t * 10.5f);
                return (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.75f + Mathf.Sin(4f * Mathf.PI * freq * t) * 0.2f) * env;
            });
        }

        #endregion
    }
}
