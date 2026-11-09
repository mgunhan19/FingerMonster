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

            UpdateDisplay(player, isCurrentTurn: false);
        }

        public void UpdateDisplay(PlayerData player, bool isCurrentTurn)
        {
            if (scoreText != null)
            {
                scoreText.text = $"Skor: {player.Score}";
            }

            // Sol el durumu
            bool leftIntact = player.LeftHand == HandState.Intact;
            if (leftHandBadge != null) leftHandBadge.color = leftIntact ? handIntactColor : handLostColor;
            if (leftHandText != null) leftHandText.text = leftIntact ? "SOL: OK" : "SOL: KOPUK";

            // Sağ el durumu
            bool rightIntact = player.RightHand == HandState.Intact;
            if (rightHandBadge != null) rightHandBadge.color = rightIntact ? handIntactColor : handLostColor;
            if (rightHandText != null) rightHandText.text = rightIntact ? "SAĞ: OK" : "SAĞ: KOPUK";

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
        }
    }
}

