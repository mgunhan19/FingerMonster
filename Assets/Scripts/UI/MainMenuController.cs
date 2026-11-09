using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using FingerMonster.Audio;
using FingerMonster.Core;

namespace FingerMonster.UI
{
    /// <summary>
    /// Ana Menü sahnesindeki Başla, Oyun Modu (Solo vs Bot / Hotseat),
    /// Zorluk (Tuzak Sayısı), Ayarlar ve Çıkış akışlarını yöneten kontrolcü.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Menü Butonları")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button lanMultiplayerButton;
        [SerializeField] private Button modeButton;
        [SerializeField] private TextMeshProUGUI modeButtonText;
        [SerializeField] private Button difficultyButton;
        [SerializeField] private TextMeshProUGUI difficultyButtonText;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Lobi ve Ayarlar Panelleri")]
        [SerializeField] private NetworkLobbyUI networkLobbyUI;
        [SerializeField] private SettingsMenuController settingsController;

        private void Awake()
        {
            Application.runInBackground = true;

            if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
            if (lanMultiplayerButton != null) lanMultiplayerButton.onClick.AddListener(OnLanMultiplayerClicked);
            if (modeButton != null) modeButton.onClick.AddListener(OnModeToggled);
            if (difficultyButton != null) difficultyButton.onClick.AddListener(OnDifficultyToggled);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

            UpdateModeUI();
            UpdateDifficultyUI();
        }

        private void OnLanMultiplayerClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            if (networkLobbyUI != null)
            {
                networkLobbyUI.OpenLobby();
            }
        }

        private void OnPlayClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            SceneManager.LoadScene("Game");
        }

        private void OnModeToggled()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();

            if (GameSessionData.SelectedMode == GameSessionData.GameMode.SoloVsBots)
            {
                GameSessionData.SetMode(GameSessionData.GameMode.HotseatParty);
            }
            else
            {
                GameSessionData.SetMode(GameSessionData.GameMode.SoloVsBots);
            }

            UpdateModeUI();
        }

        private void UpdateModeUI()
        {
            if (modeButtonText != null)
            {
                if (GameSessionData.SelectedMode == GameSessionData.GameMode.SoloVsBots)
                {
                    modeButtonText.text = "MOD: 1 OYUNCU (VS 3 BOT)";
                }
                else
                {
                    modeButtonText.text = "MOD: 4 KİŞİLİK HOTSEAT";
                }
            }
        }

        private void OnDifficultyToggled()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();

            switch (GameSessionData.SelectedDifficulty)
            {
                case GameSessionData.Difficulty.Easy:
                    GameSessionData.SelectedDifficulty = GameSessionData.Difficulty.Normal;
                    break;
                case GameSessionData.Difficulty.Normal:
                    GameSessionData.SelectedDifficulty = GameSessionData.Difficulty.Nightmare;
                    break;
                case GameSessionData.Difficulty.Nightmare:
                    GameSessionData.SelectedDifficulty = GameSessionData.Difficulty.Easy;
                    break;
            }

            UpdateDifficultyUI();
        }

        private void UpdateDifficultyUI()
        {
            if (difficultyButtonText != null)
            {
                switch (GameSessionData.SelectedDifficulty)
                {
                    case GameSessionData.Difficulty.Easy:
                        difficultyButtonText.text = "ZORLUK: KOLAY (4 TUZAK)";
                        break;
                    case GameSessionData.Difficulty.Normal:
                        difficultyButtonText.text = "ZORLUK: NORMAL (6 TUZAK)";
                        break;
                    case GameSessionData.Difficulty.Nightmare:
                        difficultyButtonText.text = "ZORLUK: KÂBUS (8 TUZAK)";
                        break;
                }
            }
        }

        private void OnSettingsClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            if (settingsController != null)
            {
                settingsController.OpenSettings();
            }
        }

        private void OnQuitClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            Debug.Log("[MainMenu] Oyundan çıkılıyor...");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
