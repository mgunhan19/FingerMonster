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

        private const string PrefMaster = "FingerMonster_MasterVol";
        private const string PrefSfx = "FingerMonster_SfxVol";
        private const string PrefMusic = "FingerMonster_MusicVol";

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
        }

        private void SavePreferences()
        {
            PlayerPrefs.Save();
        }
    }
}
