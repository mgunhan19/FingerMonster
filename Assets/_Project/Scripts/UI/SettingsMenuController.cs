using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FingerMonster.Audio;

namespace FingerMonster.UI
{
    /// <summary>
    /// Ses ayarlarını (Master, SFX, Müzik) düzenleyen, kalıcı (PlayerPrefs) hale getiren
    /// ve hem fareyle sürükleme hem de +/- butonlarıyla kolayca ayarlanabilen arayüz kontrolcüsü.
    /// </summary>
    public class SettingsMenuController : MonoBehaviour
    {
        [Header("Panel Kök")]
        [SerializeField] private GameObject settingsPanelRoot;

        [Header("Slider Kontrolleri")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;

        [Header("Yüzde Metinleri (Opsiyonel)")]
        [SerializeField] private TextMeshProUGUI masterValueText;
        [SerializeField] private TextMeshProUGUI sfxValueText;
        [SerializeField] private TextMeshProUGUI musicValueText;

        [Header("Kapat Butonu")]
        [SerializeField] private Button backButton;

        [Header("Hayalet Ayarları")]
        [SerializeField] private Toggle ghostEffectsToggle;
        [SerializeField] private Toggle reduceGhostEffectsToggle;

        private const string PrefMaster = "FingerMonster_MasterVol";
        private const string PrefSfx = "FingerMonster_SfxVol";
        private const string PrefMusic = "FingerMonster_MusicVol";
        private const string PrefGhostEffects = "FingerMonster_GhostEffects";
        private const string PrefReduceGhostEffects = "FingerMonster_ReduceGhostEffects";

        /// <summary>
        /// Hayalet atmosferik etkilerinin ayarlar menüsünden etkin olup olmadığını döner.
        /// </summary>
        public static bool AreGhostEffectsEnabled() => PlayerPrefs.GetInt(PrefGhostEffects, 1) == 1;

        /// <summary>
        /// Hayalet etkilerinin azaltılmış (erişilebilirlik/sadeleştirilmiş) modda olup olmadığını döner.
        /// </summary>
        public static bool AreGhostEffectsReduced() => PlayerPrefs.GetInt(PrefReduceGhostEffects, 0) == 1;

        private void Awake()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(CloseSettings);
            }

            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.RemoveAllListeners();
                masterVolumeSlider.onValueChanged.AddListener(OnMasterChanged);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.RemoveAllListeners();
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxChanged);
            }

            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.onValueChanged.RemoveAllListeners();
                musicVolumeSlider.onValueChanged.AddListener(OnMusicChanged);
            }

            EnsureGhostEffectsToggle();
            BindGhostEffectsToggle();

            LoadSavedPreferences();
        }

        private void OnEnable()
        {
            SyncSlidersFromAudio();
        }

        private void Start()
        {
            SyncSlidersFromAudio();
        }

        public void OpenSettings()
        {
            if (settingsPanelRoot != null)
            {
                settingsPanelRoot.SetActive(true);
            }
            else
            {
                gameObject.SetActive(true);
            }
            SyncSlidersFromAudio();
        }

        public void CloseSettings()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();

            SavePreferences();

            if (settingsPanelRoot != null)
            {
                settingsPanelRoot.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void SyncSlidersFromAudio()
        {
            float m = PlayerPrefs.GetFloat(PrefMaster, 1.0f);
            float s = PlayerPrefs.GetFloat(PrefSfx, 1.0f);
            float bgm = PlayerPrefs.GetFloat(PrefMusic, 0.65f);

            if (AudioManager.Instance != null)
            {
                m = AudioManager.Instance.MasterVolume;
                s = AudioManager.Instance.SfxVolume;
                bgm = AudioManager.Instance.MusicVolume;
            }

            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.SetValueWithoutNotify(m);
            }
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.SetValueWithoutNotify(s);
            }
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.SetValueWithoutNotify(bgm);
            }

            EnsureGhostEffectsToggle();
            BindGhostEffectsToggle();

            UpdateLabels(m, s, bgm);
        }

        private void UpdateLabels(float m, float s, float bgm)
        {
            if (masterValueText != null) masterValueText.text = $"%{(int)(m * 100f)}";
            if (sfxValueText != null) sfxValueText.text = $"%{(int)(s * 100f)}";
            if (musicValueText != null) musicValueText.text = $"%{(int)(bgm * 100f)}";
        }

        public void AdjustMasterStep(float delta)
        {
            if (masterVolumeSlider == null) return;
            masterVolumeSlider.value = Mathf.Clamp01(masterVolumeSlider.value + delta);
        }

        public void AdjustSfxStep(float delta)
        {
            if (sfxVolumeSlider == null) return;
            sfxVolumeSlider.value = Mathf.Clamp01(sfxVolumeSlider.value + delta);
        }

        public void AdjustMusicStep(float delta)
        {
            if (musicVolumeSlider == null) return;
            musicVolumeSlider.value = Mathf.Clamp01(musicVolumeSlider.value + delta);
        }

        private void OnMasterChanged(float val)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetMasterVolume(val);
            if (masterValueText != null) masterValueText.text = $"%{(int)(val * 100f)}";
            PlayerPrefs.SetFloat(PrefMaster, val);
        }

        private void OnSfxChanged(float val)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetSfxVolume(val);
            if (sfxValueText != null) sfxValueText.text = $"%{(int)(val * 100f)}";
            PlayerPrefs.SetFloat(PrefSfx, val);
        }

        private void OnMusicChanged(float val)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(val);
            if (musicValueText != null) musicValueText.text = $"%{(int)(val * 100f)}";
            PlayerPrefs.SetFloat(PrefMusic, val);
        }

        private void LoadSavedPreferences()
        {
            float m = PlayerPrefs.GetFloat(PrefMaster, 1.0f);
            float s = PlayerPrefs.GetFloat(PrefSfx, 1.0f);
            float bgm = PlayerPrefs.GetFloat(PrefMusic, 0.65f);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMasterVolume(m);
                AudioManager.Instance.SetSfxVolume(s);
                AudioManager.Instance.SetMusicVolume(bgm);
            }

            BindGhostEffectsToggle();
        }

        private void BindGhostEffectsToggle()
        {
            if (ghostEffectsToggle != null)
            {
                ghostEffectsToggle.onValueChanged.RemoveListener(OnGhostEffectsChanged);
                ghostEffectsToggle.onValueChanged.AddListener(OnGhostEffectsChanged);
                ghostEffectsToggle.SetIsOnWithoutNotify(AreGhostEffectsEnabled());
            }

            if (reduceGhostEffectsToggle != null)
            {
                reduceGhostEffectsToggle.onValueChanged.RemoveListener(OnReduceGhostEffectsChanged);
                reduceGhostEffectsToggle.onValueChanged.AddListener(OnReduceGhostEffectsChanged);
                reduceGhostEffectsToggle.SetIsOnWithoutNotify(AreGhostEffectsReduced());
            }
        }

        private void OnGhostEffectsChanged(bool val)
        {
            PlayerPrefs.SetInt(PrefGhostEffects, val ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void OnReduceGhostEffectsChanged(bool val)
        {
            PlayerPrefs.SetInt(PrefReduceGhostEffects, val ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void EnsureGhostEffectsToggle()
        {
            if (settingsPanelRoot == null) return;

            if (ghostEffectsToggle == null)
            {
                Vector2 baseAnchorMin = new Vector2(0.5f, 0.5f);
                Vector2 baseAnchorMax = new Vector2(0.5f, 0.5f);
                Vector2 basePivot = new Vector2(0.5f, 0.5f);
                Vector2 basePos = new Vector2(0f, -120f);

                if (musicVolumeSlider != null)
                {
                    RectTransform sliderRect = musicVolumeSlider.GetComponent<RectTransform>();
                    if (sliderRect != null)
                    {
                        baseAnchorMin = sliderRect.anchorMin;
                        baseAnchorMax = sliderRect.anchorMax;
                        basePivot = sliderRect.pivot;
                        basePos = sliderRect.anchoredPosition + new Vector2(0f, -55f);
                    }
                }

                ghostEffectsToggle = CreateProceduralToggle(
                    "GhostEffectsToggle",
                    "Hayalet Etkileri",
                    baseAnchorMin,
                    baseAnchorMax,
                    basePivot,
                    basePos);
            }

            if (reduceGhostEffectsToggle == null)
            {
                RectTransform ghostRect = ghostEffectsToggle != null
                    ? ghostEffectsToggle.GetComponent<RectTransform>()
                    : null;

                Vector2 anchorMin = ghostRect != null ? ghostRect.anchorMin : new Vector2(0.5f, 0.5f);
                Vector2 anchorMax = ghostRect != null ? ghostRect.anchorMax : new Vector2(0.5f, 0.5f);
                Vector2 pivot = ghostRect != null ? ghostRect.pivot : new Vector2(0.5f, 0.5f);
                Vector2 pos = ghostRect != null
                    ? ghostRect.anchoredPosition + new Vector2(0f, -44f)
                    : new Vector2(0f, -164f);

                reduceGhostEffectsToggle = CreateProceduralToggle(
                    "ReduceGhostEffectsToggle",
                    "Hayalet etkilerini azalt",
                    anchorMin,
                    anchorMax,
                    pivot,
                    pos);
            }
        }

        private Toggle CreateProceduralToggle(
            string objectName,
            string labelText,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition)
        {
            Transform container = settingsPanelRoot.transform;
            GameObject toggleGo = new GameObject(objectName, typeof(RectTransform), typeof(Toggle));
            toggleGo.transform.SetParent(container, false);

            RectTransform rootRect = toggleGo.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(280f, 36f);
            rootRect.anchorMin = anchorMin;
            rootRect.anchorMax = anchorMax;
            rootRect.pivot = pivot;
            rootRect.anchoredPosition = anchoredPosition;

            GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(toggleGo.transform, false);
            RectTransform bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(0f, 0.5f);
            bgRect.pivot = new Vector2(0f, 0.5f);
            bgRect.anchoredPosition = new Vector2(8f, 0f);
            bgRect.sizeDelta = new Vector2(24f, 24f);
            Image bgImage = bgGo.GetComponent<Image>();
            bgImage.color = new Color(0.18f, 0.18f, 0.22f, 0.95f);

            GameObject checkGo = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkGo.transform.SetParent(bgGo.transform, false);
            RectTransform checkRect = checkGo.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.15f, 0.15f);
            checkRect.anchorMax = new Vector2(0.85f, 0.85f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            Image checkImage = checkGo.GetComponent<Image>();
            checkImage.color = new Color(0.35f, 0.85f, 0.55f, 1f);

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(toggleGo.transform, false);
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(42f, 0f);
            labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = labelText;
            label.fontSize = 20f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            if (musicValueText != null && musicValueText.font != null)
            {
                label.font = musicValueText.font;
            }

            Toggle toggle = toggleGo.GetComponent<Toggle>();
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            return toggle;
        }

        private void SavePreferences()
        {
            PlayerPrefs.Save();
        }
    }
}
