using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FingerMonster.Player;
using FingerMonster.Core;

namespace FingerMonster.UI
{
    /// <summary>
    /// HUD üzerinde tek bir oyuncunun skorunu, sıra durumunu ve iki elinin sağlamlık durumunu gösteren kart bileşeni.
    /// </summary>
    public class PlayerCardUI : MonoBehaviour
    {
        [Header("Metin ve Kart Alanları")]
        [SerializeField] private Image cardBackground;
        [SerializeField] private Image turnIndicatorBorder;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI scoreText;

        [Header("El Göstergeleri")]
        [SerializeField] private Image leftHandBadge;
        [SerializeField] private TextMeshProUGUI leftHandText;
        [SerializeField] private Image rightHandBadge;
        [SerializeField] private TextMeshProUGUI rightHandText;

        [Header("Elenme Göstergesi")]
        [SerializeField] private GameObject eliminatedOverlay;

        [Header("Yetenek Kartı Rozeti")]
        [SerializeField] private GameObject abilityBadgeObj;
        [SerializeField] private TextMeshProUGUI abilityCountText;

        [Header("Renk Paleti")]
        [SerializeField] private Color handIntactColor = new Color(0.2f, 0.75f, 0.3f);
        [SerializeField] private Color handLostColor = new Color(0.35f, 0.35f, 0.35f);

        private int playerId;

        public void Initialize(PlayerData player)
        {
            playerId = player.PlayerId;

            if (playerNameText != null)
            {
                playerNameText.text = player.PlayerName;
                playerNameText.color = player.PlayerColor;
            }

            if (cardBackground != null)
            {
                cardBackground.color = new Color(player.PlayerColor.r * 0.15f, player.PlayerColor.g * 0.15f, player.PlayerColor.b * 0.15f, 0.9f);
            }

            EnsureAbilityBadge();
            UpdateDisplay(player, isCurrentTurn: false);
        }

        private void EnsureAbilityBadge()
        {
            if (abilityCountText != null)
            {
                if (abilityBadgeObj == null)
                {
                    abilityBadgeObj = abilityCountText.gameObject;
                }
                return;
            }

            GameObject badgeObj = new GameObject("AbilityBadge", typeof(RectTransform));
            badgeObj.transform.SetParent(transform, false);

            RectTransform rect = badgeObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-8f, -6f);
            rect.sizeDelta = new Vector2(132f, 20f);

            TextMeshProUGUI tmp = badgeObj.AddComponent<TextMeshProUGUI>();
            if (scoreText != null && scoreText.font != null) tmp.font = scoreText.font;
            else if (playerNameText != null && playerNameText.font != null) tmp.font = playerNameText.font;
            tmp.fontSize = 11f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.TopRight;
            tmp.color = new Color(0.82f, 0.90f, 1f, 0.95f);
            tmp.raycastTarget = false;

            abilityBadgeObj = badgeObj;
            abilityCountText = tmp;
        }

        public void UpdateDisplay(PlayerData player, bool isCurrentTurn)
        {
            if (scoreText != null)
            {
                scoreText.text = $"Skor: {player.Score}";
            }

            // Sol el durumu (Kompakt ikon: ● / ✕)
            bool leftIntact = player.LeftHand == HandState.Intact;
            if (leftHandBadge != null) leftHandBadge.color = leftIntact ? handIntactColor : handLostColor;
            if (leftHandText != null) leftHandText.text = leftIntact ? "L: ●" : "L: ✕";

            // Sağ el durumu (Kompakt ikon: ● / ✕)
            bool rightIntact = player.RightHand == HandState.Intact;
            if (rightHandBadge != null) rightHandBadge.color = rightIntact ? handIntactColor : handLostColor;
            if (rightHandText != null) rightHandText.text = rightIntact ? "R: ●" : "R: ✕";

            // Sıra göstergesi
            if (turnIndicatorBorder != null)
            {
                turnIndicatorBorder.enabled = isCurrentTurn && player.IsAlive;
            }

            // Elendi paneli
            if (eliminatedOverlay != null)
            {
                eliminatedOverlay.SetActive(!player.IsAlive);
            }

            // Yetenek kartı rozeti
            EnsureAbilityBadge();
            if (abilityBadgeObj != null)
            {
                abilityBadgeObj.SetActive(player.IsAlive);
            }
            if (abilityCountText != null && player.IsAlive)
            {
                int cardCount = player.AbilityCards != null ? player.AbilityCards.Count : 0;
                string badgeText = $"Kart: {cardCount}";
                if (player.HasActiveShield) badgeText += " [KALKAN]";
                if (player.IsFogged) badgeText += " [SİS]";
                abilityCountText.text = badgeText;
            }
        }
    }
}

