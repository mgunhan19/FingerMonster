using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FingerMonster.Audio;

namespace FingerMonster.Gameplay
{
    [System.Serializable]
    public class CardElement
    {
        public Button button;
        public Image cardBackImage;
        public GameObject cardFrontRoot;
        public Image frontIcon;
        public TextMeshProUGUI frontText;
        [HideInInspector] public bool isSafe;

        public RectTransform rectTransform => button != null ? button.GetComponent<RectTransform>() : null;
        public GameObject cardBackObject => cardBackImage != null ? cardBackImage.gameObject : null;
        public GameObject cardFrontObject => cardFrontRoot;
    }

    /// <summary>
    /// Sırası gelen oyuncu veya bot için 3 kart seçimini (Push-Your-Luck) yönetir.
    /// 3 karttan 1'i Güvenli (Pas), 2'si Canavar (Daktilo Tuş Cezası).
    /// </summary>
    public class CardSelectionController : MonoBehaviour
    {
        public static CardSelectionController Instance { get; private set; }

        [Header("UI Kökleri")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI promptText;

        [Header("3 Kart")]
        [SerializeField] private List<CardElement> cards = new List<CardElement>();

        [Header("Görsel Ayarlar")]
        [SerializeField] private Color safeColor = new Color(0.2f, 0.85f, 0.35f);
        [SerializeField] private Color monsterColor = new Color(0.95f, 0.25f, 0.25f);

        private Action<bool> currentCallback;
        private Action<int> networkCardClickIntercept;
        private bool isInitialized = false;
        private bool isShowingSelection = false;
        private bool isSelectionInProgress = false;
        private bool currentTurnIsBotOrRemote = false;
        private int activeSafeCardIndex = -1;
        private int hintedMonsterCardIndex = -1;
        private bool isCurrentSelectionFogged = false;
        private readonly Color[] defaultCardBackColors = new Color[3];
        private bool defaultBackColorsCaptured = false;

        public int ActiveSafeCardIndex => activeSafeCardIndex;
        public int HintedMonsterCardIndex => hintedMonsterCardIndex;
        public bool IsCurrentSelectionFogged => isCurrentSelectionFogged;
        public bool IsShowingSelection => isShowingSelection;
        public bool IsLocalCardPanelOpen => isShowingSelection && panelRoot != null && panelRoot.activeSelf;

        public void EnsureInitialized()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (!isInitialized)
            {
                isInitialized = true;
                for (int i = 0; i < cards.Count; i++)
                {
                    int index = i;
                    if (cards[i].button != null)
                    {
                        cards[i].button.onClick.RemoveAllListeners();
                        cards[i].button.onClick.AddListener(() => SelectCard(index));
                    }
                }
            }

            if (!defaultBackColorsCaptured)
            {
                for (int i = 0; i < cards.Count && i < defaultCardBackColors.Length; i++)
                {
                    defaultCardBackColors[i] = cards[i].cardBackImage != null ? cards[i].cardBackImage.color : Color.white;
                }
                defaultBackColorsCaptured = true;
            }
        }

        private void Awake()
        {
            EnsureInitialized();

            if (panelRoot != null && !isShowingSelection)
            {
                panelRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 3 karttan 1 tanesi doğru/güvenli (true), 2 tanesi yanlış/canavar (false) olacak şekilde
        /// Fisher-Yates algoritması ile her tur rastgele karıştırır.
        /// </summary>
        private void ShuffleCards()
        {
            if (cards == null || cards.Count == 0) return;

            bool[] deck = new bool[cards.Count];
            deck[0] = true;
            for (int i = 1; i < deck.Length; i++)
            {
                deck[i] = false;
            }

            // Fisher-Yates karıştırma
            for (int i = deck.Length - 1; i > 0; i--)
            {
                int r = UnityEngine.Random.Range(0, i + 1);
                bool tmp = deck[i];
                deck[i] = deck[r];
                deck[r] = tmp;
            }

            activeSafeCardIndex = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].isSafe = deck[i];
                if (deck[i])
                {
                    activeSafeCardIndex = i;
                }
            }
        }

        /// <summary>
        /// Sıradaki oyuncu için 3 kart seçim mini-oyununu başlatır.
        /// </summary>
        public void ShowCardSelection(string playerName, bool isBot, Action<bool> onResultCallback, string customTitle = null, string customInstruction = null)
        {
            ShowCardSelection(0, playerName, new Color(1f, 0.85f, 0.35f), isBot, onResultCallback, customTitle, customInstruction);
        }

        /// <summary>
        /// Sıradaki oyuncu için 3 kart seçim mini-oyununu başlatır (Renk ve indeks destekli).
        /// </summary>
        public void ShowCardSelection(int playerIndex, string playerName, Color playerColor, bool isBot, Action<bool> onComplete, string customTitle = null, string customInstruction = null)
        {
            EnsureInitialized();
            StopAllCoroutines();

            currentTurnIsBotOrRemote = isBot;
            isSelectionInProgress = false;
            currentCallback = onComplete;
            hintedMonsterCardIndex = -1;

            ShuffleCards();

            if (isBot)
            {
                // Sıra botta veya başka bir oyuncuda ise yerel oyuncunun ekranında kart panelini KESİNLİKLE açma!
                // Böylece 3D sahne ve alt Emote çubuğu tamamen açık kalır, yerel oyuncu diğer oyunculara hareket/emote animasyonları yapabilir.
                isShowingSelection = false;
                if (panelRoot != null) panelRoot.SetActive(false);
                return;
            }

            isShowingSelection = true;

            if (titleText != null)
            {
                titleText.text = !string.IsNullOrEmpty(customTitle)
                    ? customTitle
                    : "3 KART SEÇİMİ - ŞANSINI DENE!";
                titleText.color = playerColor;
            }

            if (promptText != null)
            {
                if (!string.IsNullOrEmpty(customInstruction))
                {
                    promptText.text = customInstruction;
                }
                else
                {
                    promptText.text = $"<color=#FFD95A>{playerName}</color>, bir kart seç!\n<size=18>(1 GÜVENLİ PAS / 2 DAKTİLO CEZASI)</size>";
                }
            }

            for (int i = 0; i < cards.Count; i++)
            {
                CardElement c = cards[i];

                if (c.rectTransform != null) c.rectTransform.localScale = Vector3.one;
                if (c.cardBackImage != null && defaultBackColorsCaptured && i < defaultCardBackColors.Length)
                {
                    c.cardBackImage.color = defaultCardBackColors[i];
                }
                if (c.cardBackObject != null) c.cardBackObject.SetActive(true);
                if (c.cardFrontObject != null) c.cardFrontObject.SetActive(false);

                if (c.button != null)
                {
                    c.button.interactable = true;
                    c.button.transform.localScale = Vector3.one;
                }
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
                panelRoot.transform.SetAsLastSibling();
            }

            if (isCurrentSelectionFogged)
            {
                ApplyFogVisualsToOpenPanel();
            }
        }

        public void SetFoggedState(bool fogged)
        {
            isCurrentSelectionFogged = fogged;
            if (IsLocalCardPanelOpen && fogged)
            {
                ApplyFogVisualsToOpenPanel();
            }
        }

        private void ApplyFogVisualsToOpenPanel()
        {
            Color fogColor = new Color(0.42f, 0.50f, 0.58f, 0.92f);
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].cardBackImage != null)
                {
                    cards[i].cardBackImage.color = fogColor;
                }
            }

            if (promptText != null && !promptText.text.Contains("SİS ETKİSİ"))
            {
                promptText.text += "\n<color=#90CAF9>SİS ETKİSİ: Kartlar sis altında! 'Ele Geçir' ipucu çalışmaz!</color>";
            }
        }

        /// <summary>
        /// Ele Geçir yeteneği için 2 canavar kartından 1 tanesini ifşa eder. Sis altındaysa -1 döner.
        /// </summary>
        public int RevealOneMonsterHint(bool applyVisualsForLocalHuman = true, int forcedMonsterIndex = -1)
        {
            if (isCurrentSelectionFogged) return -1;

            if (forcedMonsterIndex >= 0 && forcedMonsterIndex < cards.Count)
            {
                hintedMonsterCardIndex = forcedMonsterIndex;
            }
            else
            {
                List<int> monsterIndices = new List<int>();
                for (int i = 0; i < cards.Count; i++)
                {
                    if (!cards[i].isSafe)
                    {
                        monsterIndices.Add(i);
                    }
                }

                if (monsterIndices.Count == 0) return -1;
                hintedMonsterCardIndex = monsterIndices[UnityEngine.Random.Range(0, monsterIndices.Count)];
            }

            if (applyVisualsForLocalHuman && IsLocalCardPanelOpen && hintedMonsterCardIndex >= 0 && hintedMonsterCardIndex < cards.Count)
            {
                CardElement c = cards[hintedMonsterCardIndex];
                if (c.cardBackImage != null) c.cardBackImage.color = new Color(0.85f, 0.18f, 0.22f, 1f);
                if (c.cardBackObject != null) c.cardBackObject.SetActive(false);
                if (c.cardFrontObject != null) c.cardFrontObject.SetActive(true);
                if (c.frontText != null)
                {
                    c.frontText.text = "İPUCU:\nCANAVAR!";
                    c.frontText.color = monsterColor;
                }
                if (c.button != null) c.button.interactable = false;
                if (promptText != null)
                {
                    promptText.text = "<color=#CE93D8>ELE GEÇİR: Canavar kartlardan biri ifşa edildi! Kalan 2 karttan birini seç (%50 Güvenli)!</color>";
                }
            }

            return hintedMonsterCardIndex;
        }

        public void SetNetworkClickInterceptor(Action<int> interceptor)
        {
            networkCardClickIntercept = interceptor;
        }

        /// <summary>
        /// Kart seçimini yürütür (İnsan tıklaması veya Bot yapay zekası çağırır).
        /// </summary>
        public void SelectCard(int index)
        {
            if (isSelectionInProgress || index < 0 || index >= cards.Count) return;

            isSelectionInProgress = true;

            // Tüm butonları hemen kilitle (çift tıklamayı engelle)
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].button != null) cards[i].button.interactable = false;
            }

            if (networkCardClickIntercept != null)
            {
                var intercept = networkCardClickIntercept;
                intercept.Invoke(index);
                return;
            }

            if (currentTurnIsBotOrRemote || panelRoot == null || !panelRoot.activeSelf)
            {
                bool pickedSafe = cards[index].isSafe;
                isShowingSelection = false;
                isSelectionInProgress = false;
                var cb = currentCallback;
                currentCallback = null;
                cb?.Invoke(pickedSafe);
                return;
            }

            StartCoroutine(RevealAndProceedRoutine(index));
        }

        /// <summary>
        /// LAN Multiplayer modunda sunucudan gelen kart sonucunu tüm istemcilerde senkronize açar.
        /// </summary>
        public void RevealNetworkCardSelection(int chosenIndex, int safeCardIndex, bool isSafe, Action onRevealComplete = null)
        {
            EnsureInitialized();
            StopAllCoroutines();

            if (currentTurnIsBotOrRemote || panelRoot == null || !panelRoot.activeSelf)
            {
                HideImmediate();
                onRevealComplete?.Invoke();
                return;
            }

            isShowingSelection = true;
            isSelectionInProgress = true;
            activeSafeCardIndex = Mathf.Clamp(safeCardIndex, 0, Mathf.Max(0, cards.Count - 1));

            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].isSafe = (i == activeSafeCardIndex);
                if (cards[i].button != null) cards[i].button.interactable = false;
            }

            if (chosenIndex >= 0 && chosenIndex < cards.Count)
            {
                cards[chosenIndex].isSafe = isSafe;
            }

            currentCallback = (_) => onRevealComplete?.Invoke();
            StartCoroutine(RevealAndProceedRoutine(Mathf.Clamp(chosenIndex, 0, Mathf.Max(0, cards.Count - 1))));
        }

        public void HideImmediate()
        {
            StopAllCoroutines();
            isShowingSelection = false;
            isSelectionInProgress = false;
            isCurrentSelectionFogged = false;
            hintedMonsterCardIndex = -1;
            networkCardClickIntercept = null;
            currentCallback = null;
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private IEnumerator RevealAndProceedRoutine(int chosenIndex)
        {
            CardElement chosen = cards[chosenIndex];
            bool pickedSafe = chosen.isSafe;

            // Seçilen kartı aç
            if (chosen.cardBackObject != null) chosen.cardBackObject.SetActive(false);
            if (chosen.cardFrontObject != null) chosen.cardFrontObject.SetActive(true);

            if (chosen.frontText != null)
            {
                chosen.frontText.text = pickedSafe ? "GÜVENLİ!\n(PAS)" : "CANAVAR!\n(TUŞA BAS)";
                chosen.frontText.color = pickedSafe ? safeColor : monsterColor;
            }

            if (chosen.button != null)
            {
                chosen.button.transform.localScale = Vector3.one * 1.12f; // Vurgu
            }

            // Ses Efekti
            if (AudioManager.Instance != null)
            {
                if (pickedSafe) AudioManager.Instance.PlayBell();
                else AudioManager.Instance.PlayUIClick();
            }

            if (promptText != null)
            {
                promptText.text = pickedSafe
                    ? "<color=#32FF64>DOĞRU KART! Daktilodan kurtuldun, sıra devrediliyor!</color>"
                    : "<color=#FF3232>YANLIŞ KART! Cezalısın, daktiloda bir tuşa basacaksın!</color>";
            }

            // Oyuncunun sonucu okuması için 1.2 sn bekle
            yield return new WaitForSeconds(1.2f);

            // Diğer kartları da merak gidermek için göster
            for (int i = 0; i < cards.Count; i++)
            {
                if (i == chosenIndex) continue;
                if (cards[i].cardBackObject != null) cards[i].cardBackObject.SetActive(false);
                if (cards[i].cardFrontObject != null) cards[i].cardFrontObject.SetActive(true);
                if (cards[i].frontText != null)
                {
                    cards[i].frontText.text = cards[i].isSafe ? "GÜVENLİ" : "CANAVAR";
                    cards[i].frontText.color = cards[i].isSafe ? safeColor : monsterColor;
                }
            }

            yield return new WaitForSeconds(0.8f);

            isShowingSelection = false;
            isSelectionInProgress = false;
            if (panelRoot != null) panelRoot.SetActive(false);

            var cb = currentCallback;
            currentCallback = null;
            cb?.Invoke(pickedSafe);
        }
    }
}
