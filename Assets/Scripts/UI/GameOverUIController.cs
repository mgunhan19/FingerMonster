using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using FingerMonster.Player;
using FingerMonster.Audio;

namespace FingerMonster.UI
{
    /// <summary>
    /// Oyun bittiğinde kazananı duyuran, skor tablosunu sunan ve yeniden başlatma/menüye dönme
    /// aksiyonlarını yöneten panel kontrolcüsü.
    /// </summary>
    public class GameOverUIController : MonoBehaviour
    {
        [Header("Panel Kök Nesnesi")]
        [SerializeField] private GameObject panelRoot;

        [Header("Kazanan Bilgisi")]
        [SerializeField] private TextMeshProUGUI winnerTitleText;
        [SerializeField] private TextMeshProUGUI winnerDetailText;

        [Header("Skor Özeti")]
        [SerializeField] private TextMeshProUGUI scoreBoardText;

        [Header("Butonlar")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;

        private void Awake()
        {
            if (restartButton != null)
            {
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(OnRestartClicked);
            }
            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveAllListeners();
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            }
        }

        public void ShowGameOver(PlayerData winner, IReadOnlyList<PlayerData> allPlayers)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
                panelRoot.transform.SetAsLastSibling();
            }
            else
            {
                gameObject.SetActive(true);
                transform.SetAsLastSibling();
            }

            if (winner != null)
            {
                if (winnerTitleText != null)
                {
                    winnerTitleText.text = $"KAZANAN: {winner.PlayerName}!";
                    winnerTitleText.color = winner.PlayerColor;
                }

                if (winnerDetailText != null)
                {
                    winnerDetailText.text = $"Skor: {winner.Score} Puan | Kalan El: {winner.RemainingHandsCount}/2";
                }

                // Kazanan karakterin sevinç animasyonu ve konfeti patlaması
                var seats = Object.FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None);
                for (int s = 0; s < seats.Length; s++)
                {
                    if (seats[s].PlayerIndex == winner.PlayerId && seats[s].Avatar != null)
                    {
                        seats[s].Avatar.PlayJoyRelief();
                        if (Gameplay.CartoonFXManager.Instance != null)
                        {
                            Gameplay.CartoonFXManager.Instance.PlayConfetti(seats[s].transform.position + Vector3.up * 1.5f, winner.PlayerColor);
                        }
                    }
                }
            }
            else
            {
                if (winnerTitleText != null)
                {
                    winnerTitleText.text = "BERABERE! (HERKES ELENDİ)";
                    winnerTitleText.color = Color.gray;
                }
            }

            if (scoreBoardText != null && allPlayers != null)
            {
                string recap = "--- OYUN SKOR TABLOSU ---\n";
                for (int i = 0; i < allPlayers.Count; i++)
                {
                    var p = allPlayers[i];
                    string status = p.IsAlive ? $"({p.RemainingHandsCount} El Kaldı)" : "(Kasap Tarafından Elendi)";
                    recap += $"{p.PlayerName}: {p.Score} Puan {status}\n";
                }
                scoreBoardText.text = recap;
            }
        }

        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnRestartClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.name);
        }

        private void OnMainMenuClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            SceneManager.LoadScene("MainMenu");
        }
    }
}

