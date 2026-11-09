using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using FingerMonster.Player;
using FingerMonster.Core;

namespace FingerMonster.UI
{
    /// <summary>
    /// Oyun esnasında üstteki tur/tuzak bilgisi, alttaki oyuncu kartları ve
    /// ekrandaki dramatik duyuru afişlerini yöneten ana HUD kontrolcüsü.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Üst Bilgi Barı")]
        [SerializeField] private TextMeshProUGUI roundText;
        [SerializeField] private TextMeshProUGUI remainingSafeKeysText;
        [SerializeField] private TextMeshProUGUI activePlayerPromptText;

        [Header("Duyuru Afişi (Banner)")]
        [SerializeField] private GameObject bannerRoot;
        [SerializeField] private TextMeshProUGUI bannerText;

        [Header("Oyuncu Kartları (P1 - P4)")]
        [SerializeField] private List<PlayerCardUI> playerCards = new List<PlayerCardUI>();

        private Coroutine bannerRoutine;

        public void SetupHUD(IReadOnlyList<PlayerData> players)
        {
            for (int i = 0; i < playerCards.Count && i < players.Count; i++)
            {
                playerCards[i].Initialize(players[i]);
            }

            if (bannerRoot != null)
            {
                bannerRoot.SetActive(false);
            }
        }

        public void UpdateRoundInfo(int roundNumber, int remainingSafeKeys)
        {
            if (roundText != null)
            {
                roundText.text = $"TUR {roundNumber}";
            }

            if (remainingSafeKeysText != null)
            {
                remainingSafeKeysText.text = $"Kalan Güvenli Tuş: {remainingSafeKeys}";
            }
        }

        public void UpdateTurn(PlayerData activePlayer, IReadOnlyList<PlayerData> players)
        {
            if (activePlayerPromptText != null && activePlayer != null)
            {
                activePlayerPromptText.text = $"Sıra: <color=#{ColorUtility.ToHtmlStringRGB(activePlayer.PlayerColor)}>{activePlayer.PlayerName}</color>";
            }

            for (int i = 0; i < playerCards.Count && i < players.Count; i++)
            {
                bool isThisPlayer = (players[i].PlayerId == activePlayer.PlayerId);
                playerCards[i].UpdateDisplay(players[i], isThisPlayer);
            }
        }

        public void RefreshAllCards(IReadOnlyList<PlayerData> players, int activePlayerId)
        {
            for (int i = 0; i < playerCards.Count && i < players.Count; i++)
            {
                bool isThisPlayer = (players[i].PlayerId == activePlayerId);
                playerCards[i].UpdateDisplay(players[i], isThisPlayer);
            }
        }

        /// <summary>
        /// Ekrana geçici dramatik bildirim mesajı basar (Tuzak, El kopması, vb.).
        /// </summary>
        public void ShowBanner(string message, Color color, float duration = 2.0f)
        {
            if (bannerRoot == null || bannerText == null) return;

            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerDisplayRoutine(message, color, duration));
        }

        private IEnumerator BannerDisplayRoutine(string message, Color color, float duration)
        {
            bannerText.text = message;
            bannerText.color = color;
            bannerRoot.SetActive(true);

            yield return new WaitForSeconds(duration);

            bannerRoot.SetActive(false);
        }
    }
}

