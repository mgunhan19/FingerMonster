using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.Netcode;
using TMPro;
using FingerMonster.Core;
using FingerMonster.Gameplay;
using FingerMonster.Networking;
using FingerMonster.Player;

namespace FingerMonster.UI
{
    /// <summary>
    /// Yerel oyuncunun elindeki stratejik yetenek kartlarını (maksimum 2 yuva) gösteren,
    /// [6]/[7] kısayolları ve tıklama ile oynanabilen, hover tooltip ve kısa duyuru rozeti içeren
    /// kendi kendine yeten prosedürel HUD bileşeni.
    /// </summary>
    public class AbilityCardHUD : MonoBehaviour
    {
        public static AbilityCardHUD Instance { get; private set; }

        private const int SlotCount = 2;
        private const float AnnouncementDuration = 1.8f;

        private class SlotUI
        {
            public GameObject root;
            public CanvasGroup canvasGroup;
            public Button button;
            public Image backgroundImage;
            public Image borderImage;
            public Image iconImage;
            public TextMeshProUGUI keyBadgeText;
            public TextMeshProUGUI titleText;
            public TextMeshProUGUI tagText;
            public TextMeshProUGUI emptyText;
            public AbilityCardType currentType = AbilityCardType.None;
        }

        private class SlotHoverBridge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public AbilityCardHUD owner;
            public int slotIndex;

            public void OnPointerEnter(PointerEventData eventData)
            {
                owner?.OnSlotPointerEnter(slotIndex);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                owner?.OnSlotPointerExit(slotIndex);
            }
        }

        private static readonly Dictionary<AbilityCardType, Sprite> IconCache = new Dictionary<AbilityCardType, Sprite>();

        private Canvas targetCanvas;
        private RectTransform hudRoot;
        private readonly SlotUI[] slots = new SlotUI[SlotCount];

        // Hover Tooltip
        private RectTransform tooltipRoot;
        private TextMeshProUGUI tooltipTitleText;
        private TextMeshProUGUI tooltipDescText;
        private TextMeshProUGUI tooltipHintText;
        private int hoveredSlotIndex = -1;

        // Kısa Duyuru Rozeti
        private RectTransform announcementRoot;
        private Image announcementBg;
        private TextMeshProUGUI announcementText;
        private Coroutine announcementRoutine;

        private bool isInitialized = false;
        private bool subscribedGameManager = false;
        private bool subscribedNetworkManager = false;

        private TMP_FontAsset cachedFont;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            EnsureInitialized();
            TrySubscribeEvents();
            RefreshDisplay();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeEvents();
        }

        private void Update()
        {
            if (!isInitialized) return;

            TrySubscribeEvents();
            HandleKeyboardInput();
            RefreshDisplay();
        }

        private void LateUpdate()
        {
            if (!isInitialized || hudRoot == null || hudRoot.parent == null) return;

            if (IsLocalPlayerGhost())
            {
                if (hudRoot.gameObject.activeSelf)
                {
                    hudRoot.gameObject.SetActive(false);
                }
                return;
            }

            // CardSelectionController açıldığında SetAsLastSibling() çağırır;
            // yetenek kartı yuvalarının ve tooltip'in tıklanabilir/görünür kalması için en önde tut.
            int lastIndex = hudRoot.parent.childCount - 1;
            if (hudRoot.GetSiblingIndex() < lastIndex)
            {
                hudRoot.SetAsLastSibling();
            }
        }

        /// <summary>
        /// Sahnede hazır prefab gerektirmeden mevcut Canvas altında prosedürel UI hiyerarşisini kurar.
        /// </summary>
        public void EnsureInitialized()
        {
            if (isInitialized) return;

            targetCanvas = GetComponentInParent<Canvas>();
            if (targetCanvas == null)
            {
                targetCanvas = FindFirstObjectByType<Canvas>();
            }
            if (targetCanvas == null) return;

            TMP_Text anyTmp = targetCanvas.GetComponentInChildren<TMP_Text>(true);
            if (anyTmp != null) cachedFont = anyTmp.font;

            BuildHierarchy();
            isInitialized = true;
            TrySubscribeEvents();
            RefreshDisplay();
        }

        private void BuildHierarchy()
        {
            GameObject rootObj = new GameObject("AbilityCardHUD_Root", typeof(RectTransform));
            rootObj.transform.SetParent(targetCanvas.transform, false);
            hudRoot = rootObj.GetComponent<RectTransform>();
            hudRoot.anchorMin = Vector2.zero;
            hudRoot.anchorMax = Vector2.one;
            hudRoot.offsetMin = Vector2.zero;
            hudRoot.offsetMax = Vector2.zero;

            // Alt kısımda Emote çubuğunun hemen üstünde konumlanan 2 kart yuvası konteyneri
            GameObject barObj = new GameObject("AbilityCardBar", typeof(RectTransform));
            barObj.transform.SetParent(hudRoot, false);
            RectTransform barRect = barObj.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 108f);
            barRect.sizeDelta = new Vector2(300f, 92f);

            HorizontalLayoutGroup layout = barObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (int i = 0; i < SlotCount; i++)
            {
                slots[i] = CreateSlotUI(barRect, i);
            }

            BuildTooltip(hudRoot);
            BuildAnnouncementBadge(hudRoot);
            hudRoot.SetAsLastSibling();
        }

        private SlotUI CreateSlotUI(Transform parent, int index)
        {
            SlotUI slot = new SlotUI();

            GameObject slotObj = new GameObject($"AbilitySlot_{index}", typeof(RectTransform));
            slotObj.transform.SetParent(parent, false);
            RectTransform rect = slotObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(138f, 88f);

            slot.root = slotObj;
            slot.canvasGroup = slotObj.AddComponent<CanvasGroup>();
            slot.canvasGroup.alpha = 0.55f;

            slot.backgroundImage = slotObj.AddComponent<Image>();
            slot.backgroundImage.color = new Color(0.08f, 0.10f, 0.14f, 0.90f);

            slot.button = slotObj.AddComponent<Button>();
            slot.button.targetGraphic = slot.backgroundImage;
            int capturedIndex = index;
            slot.button.onClick.AddListener(() => TryPlaySlot(capturedIndex));

            SlotHoverBridge hover = slotObj.AddComponent<SlotHoverBridge>();
            hover.owner = this;
            hover.slotIndex = index;

            // Çerçeve kenarlığı
            GameObject borderObj = new GameObject("Border", typeof(RectTransform));
            borderObj.transform.SetParent(slotObj.transform, false);
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            slot.borderImage = borderObj.AddComponent<Image>();
            slot.borderImage.color = new Color(0.35f, 0.40f, 0.48f, 0.45f);
            slot.borderImage.raycastTarget = false;
            Outline outline = borderObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            outline.effectDistance = new Vector2(1f, -1f);

            // İç arka plan (kenarlık efekti için 2px içeride)
            GameObject innerObj = new GameObject("InnerBg", typeof(RectTransform));
            innerObj.transform.SetParent(slotObj.transform, false);
            RectTransform innerRect = innerObj.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(2f, 2f);
            innerRect.offsetMax = new Vector2(-2f, -2f);
            Image innerBg = innerObj.AddComponent<Image>();
            innerBg.color = new Color(0.09f, 0.11f, 0.16f, 0.94f);
            innerBg.raycastTarget = false;

            // Sol üst kısayol etiketi [6] / [7]
            GameObject keyBadgeObj = new GameObject("KeyBadge", typeof(RectTransform));
            keyBadgeObj.transform.SetParent(slotObj.transform, false);
            RectTransform keyBadgeRect = keyBadgeObj.GetComponent<RectTransform>();
            keyBadgeRect.anchorMin = new Vector2(0f, 1f);
            keyBadgeRect.anchorMax = new Vector2(0f, 1f);
            keyBadgeRect.pivot = new Vector2(0f, 1f);
            keyBadgeRect.anchoredPosition = new Vector2(6f, -5f);
            keyBadgeRect.sizeDelta = new Vector2(22f, 18f);
            Image keyBg = keyBadgeObj.AddComponent<Image>();
            keyBg.color = new Color(0.18f, 0.22f, 0.30f, 0.95f);
            keyBg.raycastTarget = false;

            slot.keyBadgeText = CreateTMP(keyBadgeObj.transform, "KeyText", $"{6 + index}", 11f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            StretchFull(slot.keyBadgeText.rectTransform);

            // Sağ üst küçük etiket (shortTag)
            slot.tagText = CreateTMP(slotObj.transform, "ShortTag", "", 9f, FontStyles.Bold, TextAlignmentOptions.TopRight, new Color(0.75f, 0.85f, 0.95f));
            RectTransform tagRect = slot.tagText.rectTransform;
            tagRect.anchorMin = new Vector2(0.3f, 1f);
            tagRect.anchorMax = new Vector2(1f, 1f);
            tagRect.pivot = new Vector2(1f, 1f);
            tagRect.anchoredPosition = new Vector2(-6f, -6f);
            tagRect.sizeDelta = new Vector2(0f, 16f);

            // Ortada prosedürel vektörel ikon
            GameObject iconObj = new GameObject("CardIcon", typeof(RectTransform));
            iconObj.transform.SetParent(slotObj.transform, false);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.55f);
            iconRect.anchorMax = new Vector2(0.5f, 0.55f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(36f, 36f);
            slot.iconImage = iconObj.AddComponent<Image>();
            slot.iconImage.preserveAspect = true;
            slot.iconImage.raycastTarget = false;

            // Altta kart adı
            slot.titleText = CreateTMP(slotObj.transform, "CardTitle", "", 11f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            RectTransform titleRect = slot.titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(1f, 0f);
            titleRect.pivot = new Vector2(0.5f, 0f);
            titleRect.anchoredPosition = new Vector2(0f, 5f);
            titleRect.sizeDelta = new Vector2(-8f, 18f);

            // Boş yuva yazısı
            slot.emptyText = CreateTMP(slotObj.transform, "EmptyText", "BOŞ YUVA", 11f, FontStyles.Italic, TextAlignmentOptions.Center, new Color(0.65f, 0.68f, 0.74f, 0.65f));
            StretchFull(slot.emptyText.rectTransform);

            return slot;
        }

        private void BuildTooltip(Transform parent)
        {
            GameObject ttObj = new GameObject("AbilityTooltip", typeof(RectTransform));
            ttObj.transform.SetParent(parent, false);
            tooltipRoot = ttObj.GetComponent<RectTransform>();
            tooltipRoot.anchorMin = new Vector2(0.5f, 0f);
            tooltipRoot.anchorMax = new Vector2(0.5f, 0f);
            tooltipRoot.pivot = new Vector2(0.5f, 0f);
            tooltipRoot.anchoredPosition = new Vector2(0f, 206f);
            tooltipRoot.sizeDelta = new Vector2(310f, 96f);

            Image bg = ttObj.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.07f, 0.11f, 0.94f);
            bg.raycastTarget = false;

            VerticalLayoutGroup vlg = ttObj.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 8, 8);
            vlg.spacing = 3f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = ttObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            tooltipTitleText = CreateTMP(ttObj.transform, "TooltipTitle", "", 13f, FontStyles.Bold, TextAlignmentOptions.Left, Color.white);
            tooltipDescText = CreateTMP(ttObj.transform, "TooltipDesc", "", 11f, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.88f, 0.90f, 0.94f));
            tooltipDescText.textWrappingMode = TextWrappingModes.Normal;
            tooltipHintText = CreateTMP(ttObj.transform, "TooltipHint", "", 10f, FontStyles.Italic, TextAlignmentOptions.Left, new Color(0.65f, 0.85f, 0.95f));

            ttObj.SetActive(false);
        }

        private void BuildAnnouncementBadge(Transform parent)
        {
            GameObject annObj = new GameObject("AbilityAnnouncementBadge", typeof(RectTransform));
            annObj.transform.SetParent(parent, false);
            announcementRoot = annObj.GetComponent<RectTransform>();
            announcementRoot.anchorMin = new Vector2(0.5f, 1f);
            announcementRoot.anchorMax = new Vector2(0.5f, 1f);
            announcementRoot.pivot = new Vector2(0.5f, 1f);
            announcementRoot.anchoredPosition = new Vector2(0f, -92f);
            announcementRoot.sizeDelta = new Vector2(380f, 38f);

            announcementBg = annObj.AddComponent<Image>();
            announcementBg.color = new Color(0.08f, 0.10f, 0.15f, 0.88f);
            announcementBg.raycastTarget = false;

            announcementText = CreateTMP(annObj.transform, "AnnouncementText", "", 14f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            StretchFull(announcementText.rectTransform, 10f);

            annObj.SetActive(false);
        }

        private TextMeshProUGUI CreateTMP(Transform parent, string name, string text, float fontSize, FontStyles style, TextAlignmentOptions align, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            if (cachedFont != null) tmp.font = cachedFont;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void StretchFull(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        private void TrySubscribeEvents()
        {
            if (!subscribedGameManager && GameManager.Instance != null)
            {
                GameManager.Instance.OnAbilityCardPlayed += HandleAbilityCardPlayed;
                GameManager.Instance.OnAbilityHandsChanged += HandleAbilityHandsChanged;
                subscribedGameManager = true;
            }

            if (!subscribedNetworkManager && NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnLocalAbilityHandUpdated += HandleNetworkHandUpdated;
                NetworkGameManager.Instance.OnNetworkAbilityCardPlayed += HandleAbilityCardPlayed;
                subscribedNetworkManager = true;
            }
        }

        private void UnsubscribeEvents()
        {
            if (subscribedGameManager && GameManager.Instance != null)
            {
                GameManager.Instance.OnAbilityCardPlayed -= HandleAbilityCardPlayed;
                GameManager.Instance.OnAbilityHandsChanged -= HandleAbilityHandsChanged;
            }
            subscribedGameManager = false;

            if (subscribedNetworkManager && NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnLocalAbilityHandUpdated -= HandleNetworkHandUpdated;
                NetworkGameManager.Instance.OnNetworkAbilityCardPlayed -= HandleAbilityCardPlayed;
            }
            subscribedNetworkManager = false;
        }

        private void HandleAbilityHandsChanged(IReadOnlyList<PlayerData> _)
        {
            RefreshDisplay();
        }

        private void HandleNetworkHandUpdated(IReadOnlyList<AbilityCardType> _)
        {
            RefreshDisplay();
        }

        private void HandleAbilityCardPlayed(int playerIndex, AbilityCardType cardType, int targetIndex, string message)
        {
            if (!isInitialized || cardType == AbilityCardType.None) return;

            CardData def = CardData.GetDefinition(cardType);
            string actorName = ResolvePlayerName(playerIndex);
            string displayCardName = def != null ? def.displayName : cardType.ToString();

            // Ele Geçir ve Röntgen kartlarının gizli sonuçlarını genel duyuruda ifşa etme
            string publicMessage;
            if (cardType == AbilityCardType.Possess || cardType == AbilityCardType.XRay || string.IsNullOrEmpty(message))
            {
                publicMessage = $"{actorName} {displayCardName} oynadı!";
            }
            else
            {
                publicMessage = message;
            }

            Color accent = def != null ? def.accentColor : Color.cyan;
            ShowAnnouncement(publicMessage, accent);
            RefreshDisplay();
        }

        private string ResolvePlayerName(int playerIndex)
        {
            if (GameManager.Instance != null && GameManager.Instance.Players != null &&
                playerIndex >= 0 && playerIndex < GameManager.Instance.Players.Count)
            {
                return GameManager.Instance.Players[playerIndex].PlayerName;
            }
            return $"Oyuncu {playerIndex + 1}";
        }

        private void ShowAnnouncement(string text, Color accent)
        {
            if (announcementRoot == null || announcementText == null) return;

            if (announcementRoutine != null) StopCoroutine(announcementRoutine);
            announcementRoutine = StartCoroutine(AnnouncementRoutine(text, accent));
        }

        private IEnumerator AnnouncementRoutine(string text, Color accent)
        {
            announcementText.text = text;
            announcementText.color = Color.Lerp(Color.white, accent, 0.35f);
            if (announcementBg != null)
            {
                announcementBg.color = new Color(accent.r * 0.16f, accent.g * 0.16f, accent.b * 0.16f, 0.90f);
            }

            announcementRoot.gameObject.SetActive(true);
            yield return new WaitForSeconds(AnnouncementDuration);
            announcementRoot.gameObject.SetActive(false);
        }

        private void HandleKeyboardInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame)
            {
                TryPlaySlot(0);
            }
            else if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame)
            {
                TryPlaySlot(1);
            }
        }

        private bool IsNetworkMatch()
        {
            return NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.IsListening &&
                   NetworkGameManager.Instance != null;
        }

        private IReadOnlyList<AbilityCardType> GetLocalHand()
        {
            if (IsNetworkMatch())
            {
                return NetworkGameManager.Instance.LocalAbilityHand;
            }

            if (GameManager.Instance != null && GameManager.Instance.Players != null && GameManager.Instance.Players.Count > 0)
            {
                return GameManager.Instance.Players[0].AbilityCards;
            }

            return null;
        }

        private bool IsLocalTurnPlayable()
        {
            if (IsNetworkMatch())
            {
                NetworkGameManager ngm = NetworkGameManager.Instance;
                GameState state = ngm.CurrentNetworkState.Value;
                return ngm.IsLocalPlayerTurn &&
                       !ngm.IsWaitingForLocalXRayTarget &&
                       (state == GameState.CardSelection || state == GameState.PlayerTurn);
            }

            if (GameManager.Instance != null)
            {
                GameManager gm = GameManager.Instance;
                return gm.ActivePlayerIndex == 0 &&
                       !gm.IsBotPlayer(0) &&
                       !gm.IsWaitingForXRayKeyTarget &&
                       (gm.CurrentState == GameState.CardSelection || gm.CurrentState == GameState.PlayerTurn);
            }

            return false;
        }

        public void TryPlaySlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return;

            IReadOnlyList<AbilityCardType> hand = GetLocalHand();
            if (hand == null || slotIndex >= hand.Count) return;

            AbilityCardType cardType = hand[slotIndex];
            if (cardType == AbilityCardType.None) return;

            CardData def = CardData.GetDefinition(cardType);
            int targetIndex = -1;
            if (def != null && def.requiresTargetOpponent)
            {
                targetIndex = EmoteWheelUI.Instance != null ? EmoteWheelUI.Instance.SelectedTargetSeatIndex : -1;
            }

            if (IsNetworkMatch())
            {
                NetworkGameManager.Instance.LocalRequestPlayAbilityCard(slotIndex, targetIndex);
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.TryPlayAbilityCard(0, slotIndex, targetIndex);
            }

            RefreshDisplay();
        }

        private bool IsLocalPlayerGhost()
        {
            if (IsNetworkMatch())
            {
                return NetworkGameManager.Instance.IsLocalGhost;
            }

            return GameManager.Instance != null && GameManager.Instance.IsLocalHumanGhost();
        }

        public void RefreshDisplay()
        {
            if (!isInitialized) return;

            if (IsLocalPlayerGhost())
            {
                if (hudRoot != null && hudRoot.gameObject.activeSelf)
                {
                    hudRoot.gameObject.SetActive(false);
                }
                return;
            }
            else if (hudRoot != null && !hudRoot.gameObject.activeSelf)
            {
                hudRoot.gameObject.SetActive(true);
            }

            IReadOnlyList<AbilityCardType> hand = GetLocalHand();
            bool canPlayTurn = IsLocalTurnPlayable();

            for (int i = 0; i < SlotCount; i++)
            {
                SlotUI slot = slots[i];
                if (slot == null) continue;

                AbilityCardType type = (hand != null && i < hand.Count) ? hand[i] : AbilityCardType.None;
                slot.currentType = type;

                bool hasCard = type != AbilityCardType.None;
                CardData def = hasCard ? CardData.GetDefinition(type) : null;

                if (hasCard && def != null)
                {
                    slot.emptyText.gameObject.SetActive(false);
                    slot.iconImage.gameObject.SetActive(true);
                    slot.titleText.gameObject.SetActive(true);
                    slot.tagText.gameObject.SetActive(true);

                    slot.iconImage.sprite = GetOrCreateIconSprite(type);
                    slot.iconImage.color = def.accentColor;
                    slot.titleText.text = def.displayName;
                    slot.titleText.color = Color.white;
                    slot.tagText.text = def.shortTag;
                    slot.tagText.color = def.accentColor;
                    slot.borderImage.color = canPlayTurn
                        ? new Color(def.accentColor.r, def.accentColor.g, def.accentColor.b, 0.85f)
                        : new Color(def.accentColor.r, def.accentColor.g, def.accentColor.b, 0.35f);

                    slot.canvasGroup.alpha = canPlayTurn ? 0.95f : 0.55f;
                    slot.button.interactable = canPlayTurn;
                }
                else
                {
                    slot.emptyText.gameObject.SetActive(true);
                    slot.iconImage.gameObject.SetActive(false);
                    slot.titleText.gameObject.SetActive(false);
                    slot.tagText.gameObject.SetActive(false);
                    slot.borderImage.color = new Color(0.35f, 0.40f, 0.48f, 0.30f);
                    slot.canvasGroup.alpha = 0.55f;
                    slot.button.interactable = false;
                }
            }

            if (hoveredSlotIndex >= 0)
            {
                UpdateTooltipContent(hoveredSlotIndex);
            }
        }

        private void OnSlotPointerEnter(int slotIndex)
        {
            hoveredSlotIndex = slotIndex;
            UpdateTooltipContent(slotIndex);
        }

        private void OnSlotPointerExit(int slotIndex)
        {
            if (hoveredSlotIndex == slotIndex)
            {
                hoveredSlotIndex = -1;
                if (tooltipRoot != null) tooltipRoot.gameObject.SetActive(false);
            }
        }

        private void UpdateTooltipContent(int slotIndex)
        {
            if (tooltipRoot == null || slotIndex < 0 || slotIndex >= SlotCount) return;

            AbilityCardType type = slots[slotIndex].currentType;
            if (type == AbilityCardType.None)
            {
                tooltipRoot.gameObject.SetActive(false);
                return;
            }

            CardData def = CardData.GetDefinition(type);
            if (def == null)
            {
                tooltipRoot.gameObject.SetActive(false);
                return;
            }

            tooltipTitleText.text = $"{def.displayName}  <size=10><color=#9AB0C8>[{def.shortTag}]</color></size>";
            tooltipTitleText.color = def.accentColor;
            tooltipDescText.text = def.description;

            if (def.requiresTargetOpponent)
            {
                tooltipHintText.text = "Hedef: Q/E ile seçili rakip";
            }
            else if (def.requiresTargetKey)
            {
                tooltipHintText.text = "Hedef: Oynadıktan sonra daktilodan bir tuş seç";
            }
            else
            {
                tooltipHintText.text = $"Kullanım: Tuş [{6 + slotIndex}] veya Tıkla";
            }

            float xOffset = slotIndex == 0 ? -75f : 75f;
            tooltipRoot.anchoredPosition = new Vector2(xOffset, 204f);
            tooltipRoot.gameObject.SetActive(true);
        }

        #region Prosedürel Vektörel İkon Üreticisi (MIT / In-House Texture2D)

        private static Sprite GetOrCreateIconSprite(AbilityCardType type)
        {
            if (IconCache.TryGetValue(type, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Texture2D tex = GenerateIconTexture(type);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = $"ProceduralIcon_{type}";
            IconCache[type] = sprite;
            return sprite;
        }

        private static Texture2D GenerateIconTexture(AbilityCardType type)
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size * 2f - 1f; // -1 .. 1
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f; // -1 .. 1
                    float alpha = SampleIconAlpha(type, nx, ny);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static float SampleIconAlpha(AbilityCardType type, float x, float y)
        {
            switch (type)
            {
                case AbilityCardType.Shield:
                    return SampleShield(x, y);
                case AbilityCardType.Possess:
                    return SampleEye(x, y);
                case AbilityCardType.XRay:
                    return SampleXRayLens(x, y);
                case AbilityCardType.SkipTurn:
                    return SampleSkipArrow(x, y);
                case AbilityCardType.ReverseDirection:
                    return SampleReverseArrows(x, y);
                case AbilityCardType.Fog:
                    return SampleFogCloud(x, y);
                default:
                    return 0f;
            }
        }

        private static float SmoothEdge(float dist, float edge = 0.04f)
        {
            return 1f - Mathf.SmoothStep(-edge, edge, dist);
        }

        private static float SegmentDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a;
            Vector2 ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        private static float SampleShield(float x, float y)
        {
            float ax = Mathf.Abs(x);
            bool inTop = y >= 0.05f && y <= 0.72f && ax <= 0.62f;
            float bottomLimit = 0.62f * Mathf.Clamp01((y + 0.78f) / 0.83f);
            bool inBottom = y >= -0.78f && y < 0.05f && ax <= bottomLimit;

            if (!inTop && !inBottom) return 0f;

            // Dış çerçeve ve iç çapraz/kalkan detayı
            float borderDist = y >= 0.05f
                ? Mathf.Max(ax - 0.62f, y - 0.72f)
                : Mathf.Max(ax - bottomLimit, -0.78f - y);

            float outer = SmoothEdge(borderDist);
            float innerMask = (ax < 0.45f && y < 0.56f && y > -0.58f && (y >= 0.05f || ax < 0.45f * ((y + 0.58f) / 0.63f))) ? 0.35f : 1f;
            float cross = Mathf.Min(Mathf.Abs(x), Mathf.Abs(y - 0.05f)) < 0.08f ? 1f : 0f;
            return Mathf.Max(outer * innerMask, outer * cross);
        }

        private static float SampleEye(float x, float y)
        {
            float eyeHeight = 0.42f * Mathf.Max(0f, 1f - (x * x) / 0.68f);
            float dOuter = Mathf.Abs(y) - eyeHeight;
            float dInner = Mathf.Abs(y) - Mathf.Max(0f, eyeHeight - 0.12f);

            float outline = SmoothEdge(dOuter) - SmoothEdge(dInner);
            float r = Mathf.Sqrt(x * x + y * y);
            float irisRing = SmoothEdge(Mathf.Abs(r - 0.25f) - 0.055f);
            float pupil = SmoothEdge(r - 0.12f);

            return Mathf.Clamp01(Mathf.Max(outline, Mathf.Max(irisRing, pupil)));
        }

        private static float SampleXRayLens(float x, float y)
        {
            Vector2 p = new Vector2(x, y);
            Vector2 c = new Vector2(-0.14f, 0.14f);
            float r = (p - c).magnitude;

            float ring = SmoothEdge(Mathf.Abs(r - 0.42f) - 0.075f);
            float handle = SmoothEdge(SegmentDist(p, new Vector2(0.16f, -0.16f), new Vector2(0.68f, -0.68f)) - 0.085f);

            // Mercek içi tarama artısı
            float crossH = (r < 0.30f) ? SmoothEdge(Mathf.Abs(p.y - c.y) - 0.045f) : 0f;
            float crossV = (r < 0.30f) ? SmoothEdge(Mathf.Abs(p.x - c.x) - 0.045f) : 0f;

            return Mathf.Clamp01(Mathf.Max(Mathf.Max(ring, handle), Mathf.Max(crossH, crossV)));
        }

        private static float SampleSkipArrow(float x, float y)
        {
            Vector2 p = new Vector2(x, y);
            // Sağ ok gövdesi ve başı
            float shaft = SmoothEdge(SegmentDist(p, new Vector2(-0.66f, 0f), new Vector2(0.22f, 0f)) - 0.09f);
            float headTop = SmoothEdge(SegmentDist(p, new Vector2(-0.08f, 0.36f), new Vector2(0.28f, 0f)) - 0.085f);
            float headBot = SmoothEdge(SegmentDist(p, new Vector2(-0.08f, -0.36f), new Vector2(0.28f, 0f)) - 0.085f);
            // Dikey atlama çubuğu
            float bar = SmoothEdge(SegmentDist(p, new Vector2(0.50f, -0.44f), new Vector2(0.50f, 0.44f)) - 0.09f);

            return Mathf.Clamp01(Mathf.Max(Mathf.Max(shaft, bar), Mathf.Max(headTop, headBot)));
        }

        private static float SampleReverseArrows(float x, float y)
        {
            Vector2 p = new Vector2(x, y);
            // Üst ok (sağa)
            float topShaft = SmoothEdge(SegmentDist(p, new Vector2(-0.56f, 0.28f), new Vector2(0.52f, 0.28f)) - 0.08f);
            float topH1 = SmoothEdge(SegmentDist(p, new Vector2(0.24f, 0.54f), new Vector2(0.56f, 0.28f)) - 0.075f);
            float topH2 = SmoothEdge(SegmentDist(p, new Vector2(0.24f, 0.02f), new Vector2(0.56f, 0.28f)) - 0.075f);

            // Alt ok (sola)
            float botShaft = SmoothEdge(SegmentDist(p, new Vector2(-0.52f, -0.28f), new Vector2(0.56f, -0.28f)) - 0.08f);
            float botH1 = SmoothEdge(SegmentDist(p, new Vector2(-0.24f, -0.02f), new Vector2(-0.56f, -0.28f)) - 0.075f);
            float botH2 = SmoothEdge(SegmentDist(p, new Vector2(-0.24f, -0.54f), new Vector2(-0.56f, -0.28f)) - 0.075f);

            float top = Mathf.Max(topShaft, Mathf.Max(topH1, topH2));
            float bot = Mathf.Max(botShaft, Mathf.Max(botH1, botH2));
            return Mathf.Clamp01(Mathf.Max(top, bot));
        }

        private static float SampleFogCloud(float x, float y)
        {
            Vector2 p = new Vector2(x, y);
            float c1 = SmoothEdge((p - new Vector2(-0.30f, 0.08f)).magnitude - 0.26f);
            float c2 = SmoothEdge((p - new Vector2(0.02f, 0.24f)).magnitude - 0.32f);
            float c3 = SmoothEdge((p - new Vector2(0.32f, 0.06f)).magnitude - 0.24f);
            float baseRect = (x >= -0.45f && x <= 0.45f && y >= -0.14f && y <= 0.14f) ? 1f : 0f;

            // Altta iki dalgalı sis çizgisi
            float line1 = SmoothEdge(SegmentDist(p, new Vector2(-0.55f, -0.34f), new Vector2(0.45f, -0.34f)) - 0.055f);
            float line2 = SmoothEdge(SegmentDist(p, new Vector2(-0.38f, -0.54f), new Vector2(0.55f, -0.54f)) - 0.055f);

            float cloud = Mathf.Max(Mathf.Max(c1, c2), Mathf.Max(c3, baseRect));
            return Mathf.Clamp01(Mathf.Max(cloud, Mathf.Max(line1, line2)));
        }

        #endregion
    }
}
