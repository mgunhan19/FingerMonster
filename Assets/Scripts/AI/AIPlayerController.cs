using System.Collections.Generic;
using UnityEngine;
using FingerMonster.Core;
using FingerMonster.Gameplay;
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
                if (Instance.gameObject != gameObject)
                {
                    Destroy(Instance.gameObject);
                }
                else
                {
                    Destroy(this);
                    return;
                }
            }
            Instance = this;

            brain = GetComponent<AIBrain>();
        }

        private void OnEnable()
        {
            SubscribeToGameManager();
        }

        private void Start()
        {
            SubscribeToGameManager();
        }

        private void SubscribeToGameManager()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.OnTurnChanged -= HandleTurnChanged;
            GameManager.Instance.OnTurnChanged += HandleTurnChanged;
            GameManager.Instance.OnCardSelectionStarted -= HandleCardSelectionStarted;
            GameManager.Instance.OnCardSelectionStarted += HandleCardSelectionStarted;
        }

        private void UnsubscribeFromGameManager()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.OnTurnChanged -= HandleTurnChanged;
            GameManager.Instance.OnCardSelectionStarted -= HandleCardSelectionStarted;
        }

        private void OnDisable()
        {
            UnsubscribeFromGameManager();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeFromGameManager();
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

        private void HandleCardSelectionStarted(PlayerData activePlayer)
        {
            if (activePlayer == null) return;
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.CardPlay) return;
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.CardSelection) return;

            if (IsBot(activePlayer.PlayerId) && brain != null)
            {
                CardSelectionController cardCtrl = CardSelectionController.Instance != null
                    ? CardSelectionController.Instance
                    : (GameManager.Instance != null && GameManager.Instance.CardController != null
                        ? GameManager.Instance.CardController
                        : UnityEngine.Object.FindFirstObjectByType<CardSelectionController>(FindObjectsInactive.Include));

                if (cardCtrl != null)
                {
                    brain.DecideAndExecuteCard(activePlayer.PlayerId, cardCtrl);
                }
            }
        }

        private void HandleTurnChanged(PlayerData activePlayer)
        {
            if (activePlayer == null) return;
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.CardPlay) return;
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.PlayerTurn) return;

            if (IsBot(activePlayer.PlayerId) && brain != null)
            {
                brain.DecideAndExecuteTurn(activePlayer.PlayerId, GameManager.Instance);
            }
        }
    }
}
