using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using FingerMonster.Audio;
using FingerMonster.Core;

namespace FingerMonster.UI
{
    /// <summary>
    /// ESC veya Gamepad Start ile açılan duraklatma menüsü kontrolcüsü.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Panel Kök Nesnesi")]
        [SerializeField] private GameObject pausePanelRoot;

        [Header("Butonlar")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;

        public bool IsPaused { get; private set; } = false;

        private void Awake()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(Resume);
            }
            if (restartButton != null)
            {
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(RestartGame);
            }
            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveAllListeners();
                mainMenuButton.onClick.AddListener(GoToMainMenu);
            }
        }

        public void TogglePause()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            IsPaused = true;
            Time.timeScale = 0f;
            if (pausePanelRoot != null) pausePanelRoot.SetActive(true);
        }

        public void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            if (pausePanelRoot != null) pausePanelRoot.SetActive(false);
        }

        private void RestartGame()
        {
            Time.timeScale = 1f;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.name);
        }

        private void GoToMainMenu()
        {
            Time.timeScale = 1f;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            SceneManager.LoadScene("MainMenu");
        }
    }
}

