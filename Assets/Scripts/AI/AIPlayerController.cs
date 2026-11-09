using System.Collections.Generic;
using UnityEngine;
using FingerMonster.Core;
using FingerMonster.Player;

namespace FingerMonster.AI
{
    /// <summary>
    /// Hangi koltukların botlar tarafından yönetildiğini takip eden ve
    /// sırası gelen bot için AIBrain karar döngüsünü tetikleyen kontrolcü.
    /// </summary>
    [RequireComponent(typeof(AIBrain))]
    public class AIPlayerController : MonoBehaviour
    {
        public static AIPlayerController Instance { get; private set; }

        [Header("Bot Yapılandırması")]
        [Tooltip("True ise ilgili oyuncu indeksi bilgisayar (bot) tarafından oynanır")]
        [SerializeField] private bool[] isBotPlayer = new bool[4] { false, false, false, false };

        private AIBrain brain;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            brain = GetComponent<AIBrain>();
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnTurnChanged += HandleTurnChanged;
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnTurnChanged -= HandleTurnChanged;
            }
        }

        public void SetPlayerAsBot(int playerIndex, bool isBot)
        {
            if (playerIndex >= 0 && playerIndex < isBotPlayer.Length)
            {
                isBotPlayer[playerIndex] = isBot;
            }
        }

        public bool IsBot(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < isBotPlayer.Length)
            {
                return isBotPlayer[playerIndex];
            }
            return false;
        }

        private void HandleTurnChanged(PlayerData activePlayer)
        {
            if (activePlayer == null) return;

            if (IsBot(activePlayer.PlayerId) && brain != null)
            {
                brain.DecideAndExecuteTurn(activePlayer.PlayerId, GameManager.Instance);
            }
        }
    }
}

