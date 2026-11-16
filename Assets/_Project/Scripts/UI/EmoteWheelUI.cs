using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Netcode;
using TMPro;
using FingerMonster.Core;
using FingerMonster.Player;
using FingerMonster.Audio;
using FingerMonster.Networking;

namespace FingerMonster.UI
{
    /// <summary>
    /// 5 slotlu Emote çubuğu, isteğe bağlı 5 dilimli Emote çarkı, hedef rakip seçimi (Q/E/Tab),
    /// hedef göstergesi, bekleme süresi (cooldown) ve gelen emote bildirimlerini yöneten kontrolcü.
    /// Oyuncunun sırası olmasa da kullanılabilir.
    /// </summary>
    public class EmoteWheelUI : MonoBehaviour
    {
        public static EmoteWheelUI Instance { get; private set; }

        [Header("Input System")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Karakter Emote Verileri (Feg, Kenan, Feyzi, MMG)")]
        [SerializeField] private CharacterEmoteData[] characterEmoteDatas = new CharacterEmoteData[4];

        [Header("Alt Emote Çubuğu (5 Slot + Hedef)")]
        [SerializeField] private GameObject emoteBarRoot;
        [SerializeField] private Button[] barSlotButtons = new Button[5];
        [SerializeField] private TextMeshProUGUI[] barSlotLabels = new TextMeshProUGUI[5];
        [SerializeField] private Image barCooldownOverlay;
        [SerializeField] private TextMeshProUGUI barCooldownText;
        [SerializeField] private TextMeshProUGUI targetLabelText;
        [SerializeField] private Button prevTargetButton;
        [SerializeField] private Button nextTargetButton;

        [Header("İsteğe Bağlı Emote Çarkı (Basılı Tut)")]
        [SerializeField] private GameObject wheelRoot;
        [SerializeField] private Image cooldownRing;
        [SerializeField] private TextMeshProUGUI centerStatusText;
        [SerializeField] private Image[] sliceHighlights = new Image[5];
        [SerializeField] private TextMeshProUGUI[] sliceLabels = new TextMeshProUGUI[5];

        [Header("Gelen Emote Bildirimi")]
        [SerializeField] private GameObject notificationRoot;
        [SerializeField] private TextMeshProUGUI notificationText;

        [Header("Ayarlar")]
        [SerializeField] private float cooldownDuration = 2.5f;
        [SerializeField] private CanvasGroup emoteBarCanvasGroup;

        private CharacterArchetype localArchetype = CharacterArchetype.Kabadayi;
        private int localSeatIndex = 0;
        private int selectedTargetSeatIndex = 1;
        private int selectedSliceIndex = -1;
        private float currentCooldown = 0f;
        private bool isWheelOpen = false;
        private Coroutine notificationRoutine;
        private float emoteBarVisibilityTimer = 0f;

        // Seat Lookup
        private readonly Dictionary<int, PlayerSeat> seatLookup = new Dictionary<int, PlayerSeat>();

        // Input Actions
        private InputActionMap emoteMap;
        private InputAction[] actEmotes = new InputAction[5];
        private InputAction actNextTarget;
        private InputAction actPrevTarget;
        private InputAction actHoldWheel;
        private InputAction actToggleGuide;

        public bool IsOnCooldown => currentCooldown > 0f;
        public int SelectedTargetSeatIndex => selectedTargetSeatIndex;

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

            if (wheelRoot != null) wheelRoot.SetActive(false);
            if (notificationRoot != null) notificationRoot.SetActive(false);

            SetupBarButtons();
            SetupInputActions();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            emoteMap?.Disable();
        }

        private void OnEnable()
        {
            emoteMap?.Enable();
        }

        private void OnDisable()
        {
            emoteMap?.Disable();
        }

        private void Start()
        {
            RefreshSeatLookup();
            ConfigureForArchetype(localArchetype);
            EnsureValidTargetSelected();
        }

        private void SetupBarButtons()
        {
            if (barSlotButtons != null)
            {
                for (int i = 0; i < barSlotButtons.Length; i++)
                {
                    int slot1Based = i + 1;
                    if (barSlotButtons[i] != null)
                    {
                        barSlotButtons[i].onClick.RemoveAllListeners();
                        barSlotButtons[i].onClick.AddListener(() => TryExecuteEmoteSlot(slot1Based));
                    }
                }
            }

            if (prevTargetButton != null)
            {
                prevTargetButton.onClick.RemoveAllListeners();
                prevTargetButton.onClick.AddListener(() => CycleTarget(-1));
            }
            if (nextTargetButton != null)
            {
                nextTargetButton.onClick.RemoveAllListeners();
                nextTargetButton.onClick.AddListener(() => CycleTarget(1));
            }
        }

        private void SetupInputActions()
        {
            if (inputActions == null) return;
            emoteMap = inputActions.FindActionMap("Emote", false);
            if (emoteMap == null) return;

            for (int i = 0; i < 5; i++)
            {
                actEmotes[i] = emoteMap.FindAction($"Emote{i + 1}", false);
            }
            actNextTarget = emoteMap.FindAction("NextTarget", false);
            actPrevTarget = emoteMap.FindAction("PrevTarget", false);
            actHoldWheel = emoteMap.FindAction("HoldWheel", false);
            actToggleGuide = emoteMap.FindAction("ToggleGuide", false);
            emoteMap.Enable();
        }

        public void SetCharacterEmoteDatas(CharacterEmoteData[] datas)
        {
            characterEmoteDatas = datas;
            ConfigureForArchetype(localArchetype);
        }

        public CharacterEmoteData GetEmoteData(CharacterArchetype archetype)
        {
            int idx = Mathf.Clamp((int)archetype, 0, 3);
            if (characterEmoteDatas != null && idx < characterEmoteDatas.Length && characterEmoteDatas[idx] != null)
            {
                return characterEmoteDatas[idx];
            }
            var fallback = CharacterEmoteData.CreateRuntimeFallback(archetype);
            if (characterEmoteDatas == null || characterEmoteDatas.Length < 4)
            {
                characterEmoteDatas = new CharacterEmoteData[4];
            }
            characterEmoteDatas[idx] = fallback;
            return fallback;
        }

        public void ConfigureForSeat(int seatIndex, CharacterArchetype archetype)
        {
            localSeatIndex = seatIndex;
            ConfigureForArchetype(archetype);
            EnsureValidTargetSelected();
        }

        public void ConfigureForArchetype(CharacterArchetype archetype)
        {
            localArchetype = archetype;
            CharacterEmoteData data = GetEmoteData(archetype);

            for (int i = 0; i < 5; i++)
            {
                EmoteSlotInfo slot = data.GetSlot(i + 1);
                string label = slot != null ? slot.DisplayName : $"Emote {i + 1}";

                if (barSlotLabels != null && i < barSlotLabels.Length && barSlotLabels[i] != null)
                {
                    barSlotLabels[i].textWrappingMode = TextWrappingModes.NoWrap;
                    barSlotLabels[i].overflowMode = TextOverflowModes.Ellipsis;
                    barSlotLabels[i].text = $"[{i + 1}] {label}";
                }
                if (sliceLabels != null && i < sliceLabels.Length && sliceLabels[i] != null)
                {
                    sliceLabels[i].text = $"[{i + 1}]\n{label}";
                }
            }

            if (centerStatusText != null)
            {
                var def = CharacterDefinition.GetDefinition(archetype);
                centerStatusText.text = $"{def.DisplayName}\n<size=14>[1-5] Emote</size>";
            }
        }

        private void Update()
        {
            if (!IsSeatEliminated(localSeatIndex) && emoteMap != null && !emoteMap.enabled)
            {
                emoteMap.Enable();
            }

            UpdateCooldownUI();
            EnsureValidTargetSelected();
            HandleInput();
            UpdateEmoteBarVisibility();
        }

        private void UpdateEmoteBarVisibility()
        {
            if (emoteBarCanvasGroup == null) return;

            bool isAimingOrHolding = (Keyboard.current != null && (Keyboard.current.tabKey.isPressed || Keyboard.current.qKey.isPressed || Keyboard.current.eKey.isPressed || Keyboard.current.cKey.isPressed))
                || (Gamepad.current != null && (Gamepad.current.leftShoulder.isPressed || Gamepad.current.rightShoulder.isPressed))
                || isWheelOpen || emoteBarVisibilityTimer > 0f;

            if (emoteBarVisibilityTimer > 0f)
            {
                emoteBarVisibilityTimer -= Time.deltaTime;
            }

            // Normalde çok sade ve yarı saydam (%25), tuşa basıldığında tam (%100)
            float targetAlpha = isAimingOrHolding ? 1.0f : 0.25f;
            emoteBarCanvasGroup.alpha = Mathf.MoveTowards(emoteBarCanvasGroup.alpha, targetAlpha, Time.deltaTime * 6.0f);
        }

        private void UpdateCooldownUI()
        {
            if (currentCooldown > 0f)
            {
                currentCooldown = Mathf.Max(0f, currentCooldown - Time.deltaTime);
                float ratio = currentCooldown / Mathf.Max(0.01f, cooldownDuration);

                if (cooldownRing != null) cooldownRing.fillAmount = ratio;
                if (barCooldownOverlay != null)
                {
                    barCooldownOverlay.gameObject.SetActive(true);
                    barCooldownOverlay.fillAmount = ratio;
                }
                if (barCooldownText != null)
                {
                    barCooldownText.text = currentCooldown > 0.05f ? $"{currentCooldown:F1}s" : "";
                }
            }
            else
            {
                if (cooldownRing != null) cooldownRing.fillAmount = 0f;
                if (barCooldownOverlay != null) barCooldownOverlay.gameObject.SetActive(false);
                if (barCooldownText != null) barCooldownText.text = "";
            }
        }

        private bool CanUseEmotesNow()
        {
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.CurrentState == GameState.GameOver) return false;
            }
            if (Gameplay.CardSelectionController.Instance != null && Gameplay.CardSelectionController.Instance.IsLocalCardPanelOpen)
            {
                return false;
            }
            if (IsSeatEliminated(localSeatIndex)) return false;
            return true;
        }

        private void HandleInput()
        {
            if (IsSeatEliminated(localSeatIndex))
            {
                if (isWheelOpen) CloseWheelWithoutExecute();
                if (emoteBarRoot != null && emoteBarRoot.activeSelf) emoteBarRoot.SetActive(false);
                if (emoteMap != null && emoteMap.enabled) emoteMap.Disable();
                return;
            }

            // Rehber paneli [H] ile her zaman açılıp kapatılabilir
            bool toggleGuide = (actToggleGuide != null && actToggleGuide.WasPressedThisFrame())
                || (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame);

            if (toggleGuide && EmoteGuidePanelUI.Instance != null)
            {
                EmoteGuidePanelUI.Instance.TogglePanel();
            }

            // Koltuk verisi güncel değilse her zaman taze topla
            if (seatLookup.Count < 4) RefreshSeatLookup();

            // Hedef değiştirme (Q / E / Tab / LB / RB) - Sıra kimde olursa olsun, tur boyunca bağımsız çalışır
            bool nextTarget = (actNextTarget != null && actNextTarget.WasPressedThisFrame())
                || (Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame))
                || (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame);

            bool prevTarget = (actPrevTarget != null && actPrevTarget.WasPressedThisFrame())
                || (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame);

            if (nextTarget)
            {
                CycleTarget(1);
                emoteBarVisibilityTimer = 3.0f;
            }
            else if (prevTarget)
            {
                CycleTarget(-1);
                emoteBarVisibilityTimer = 3.0f;
            }

            if (!CanUseEmotesNow())
            {
                if (isWheelOpen) CloseWheelWithoutExecute();
                return;
            }

            // 1..5 Tuşları ve Gamepad D-Pad / Y ile direkt Emote
            for (int i = 0; i < 5; i++)
            {
                if (WasEmoteKeyPressed(i))
                {
                    emoteBarVisibilityTimer = 3.0f;
                    TryExecuteEmoteSlot(i + 1);
                    break;
                }
            }

            // İsteğe bağlı Emote Çarkı (C veya Sağ Analog Basma)
            bool holdWheel = (actHoldWheel != null && actHoldWheel.IsPressed())
                || (Keyboard.current != null && Keyboard.current.cKey.isPressed)
                || (Gamepad.current != null && Gamepad.current.rightStickButton.isPressed);

            if (holdWheel && !isWheelOpen)
            {
                OpenWheel();
            }
            else if (!holdWheel && isWheelOpen)
            {
                CloseAndExecuteWheel();
            }

            if (isWheelOpen)
            {
                UpdateSliceSelection();
            }
        }

        private bool WasEmoteKeyPressed(int zeroBasedIndex)
        {
            if (actEmotes[zeroBasedIndex] != null && actEmotes[zeroBasedIndex].WasPressedThisFrame())
            {
                return true;
            }

            if (Keyboard.current != null)
            {
                switch (zeroBasedIndex)
                {
                    case 0: if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) return true; break;
                    case 1: if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) return true; break;
                    case 2: if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) return true; break;
                    case 3: if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame) return true; break;
                    case 4: if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame) return true; break;
                }
            }

            if (Gamepad.current != null)
            {
                switch (zeroBasedIndex)
                {
                    case 0: if (Gamepad.current.dpad.up.wasPressedThisFrame) return true; break;
                    case 1: if (Gamepad.current.dpad.right.wasPressedThisFrame) return true; break;
                    case 2: if (Gamepad.current.dpad.down.wasPressedThisFrame) return true; break;
                    case 3: if (Gamepad.current.dpad.left.wasPressedThisFrame) return true; break;
                    case 4: if (Gamepad.current.buttonNorth.wasPressedThisFrame) return true; break;
                }
            }

            return false;
        }

        #region HEDEF SEÇİMİ VE 3D GÖSTERGE
        private void RefreshSeatLookup()
        {
            seatLookup.Clear();
            var seats = FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None);
            foreach (var s in seats)
            {
                if (s != null) seatLookup[s.PlayerIndex] = s;
            }
        }

        private bool IsSeatEliminated(int seatIndex)
        {
            if (NetworkGameManager.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                foreach (var slot in NetworkGameManager.Instance.PlayerSlots)
                {
                    if (slot.SeatIndex == seatIndex) return !slot.IsAlive;
                }
            }
            else if (GameManager.Instance != null && GameManager.Instance.Players != null)
            {
                if (seatIndex >= 0 && seatIndex < GameManager.Instance.Players.Count)
                {
                    return !GameManager.Instance.Players[seatIndex].IsAlive;
                }
            }
            return false;
        }

        private string GetSeatDisplayName(int seatIndex)
        {
            if (NetworkGameManager.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                foreach (var slot in NetworkGameManager.Instance.PlayerSlots)
                {
                    if (slot.SeatIndex == seatIndex)
                    {
                        string sName = slot.PlayerName.ToString();
                        return string.IsNullOrEmpty(sName) ? CharacterDefinition.GetDefinition((CharacterArchetype)slot.ArchetypeIndex).DisplayName : sName;
                    }
                }
            }
            else if (GameManager.Instance != null && GameManager.Instance.Players != null)
            {
                if (seatIndex >= 0 && seatIndex < GameManager.Instance.Players.Count)
                {
                    return CharacterDefinition.GetDefinition(GameManager.Instance.Players[seatIndex].Archetype).DisplayName;
                }
            }

            if (seatLookup.TryGetValue(seatIndex, out PlayerSeat seat) && seat != null && seat.Avatar != null)
            {
                return CharacterDefinition.GetDefinition(seat.Avatar.Archetype).DisplayName;
            }
            return $"Koltuk {seatIndex + 1}";
        }

        public void CycleTarget(int step)
        {
            List<int> livingRivals = GetLivingRivalSeats();
            if (livingRivals.Count == 0)
            {
                selectedTargetSeatIndex = -1;
                UpdateTargetLabel();
                Debug.Log("[EmoteTarget] Hayatta rakip yok! Hedef: Yok");
                return;
            }

            int prevTarget = selectedTargetSeatIndex;
            int curIdx = livingRivals.IndexOf(selectedTargetSeatIndex);
            if (curIdx < 0)
            {
                selectedTargetSeatIndex = livingRivals[0];
            }
            else
            {
                int nextIdx = (curIdx + step + livingRivals.Count) % livingRivals.Count;
                selectedTargetSeatIndex = livingRivals[nextIdx];
            }

            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            UpdateTargetLabel();

            // Geri bildirim: Tek rakip varsa veya hedef değişmediyse kullanıcıya kilitlenmediğini bildir
            if (livingRivals.Count == 1 || prevTarget == selectedTargetSeatIndex)
            {
                if (targetLabelText != null)
                {
                    targetLabelText.text = $"<color=#FFD95A>{GetSeatDisplayName(selectedTargetSeatIndex)} (Tek Rakip)</color>";
                }
            }

            Debug.Log($"[EmoteTarget] Hedef Değişti -> Koltuk: {selectedTargetSeatIndex} ({GetSeatDisplayName(selectedTargetSeatIndex)}) | Adım: {step} | Sıra: {(GameManager.Instance != null ? GameManager.Instance.ActivePlayerIndex.ToString() : "Ağ")} | Tur: {(GameManager.Instance != null ? GameManager.Instance.CurrentRound.ToString() : "Ağ")}");
        }

        private void EnsureValidTargetSelected()
        {
            List<int> livingRivals = GetLivingRivalSeats();
            if (livingRivals.Count == 0)
            {
                if (selectedTargetSeatIndex != -1)
                {
                    selectedTargetSeatIndex = -1;
                    UpdateTargetLabel();
                }
                return;
            }

            if (!livingRivals.Contains(selectedTargetSeatIndex))
            {
                selectedTargetSeatIndex = livingRivals[0];
                UpdateTargetLabel();
                Debug.Log($"[EmoteTarget] Mevcut hedef artık geçerli değil, otomatik aktarıldı -> Koltuk {selectedTargetSeatIndex} ({GetSeatDisplayName(selectedTargetSeatIndex)})");
            }
        }

        public List<int> GetLivingRivalSeats()
        {
            List<int> result = new List<int>(3);
            for (int offset = 1; offset <= 3; offset++)
            {
                int candidate = (localSeatIndex + offset) % 4;
                if (!IsSeatEliminated(candidate))
                {
                    result.Add(candidate);
                }
            }
            return result;
        }

        private void UpdateTargetLabel()
        {
            if (targetLabelText == null) return;
            if (selectedTargetSeatIndex < 0)
            {
                targetLabelText.text = "Hedef: Yok";
            }
            else
            {
                targetLabelText.text = $"Hedef: <color=#FFD95A>{GetSeatDisplayName(selectedTargetSeatIndex)}</color> <size=13>[Q/E]</size>";
            }
        }
        #endregion

        #region EMOTE ÇARKI VE ÇUBUK TETİKLEME
        private void OpenWheel()
        {
            isWheelOpen = true;
            if (wheelRoot != null) wheelRoot.SetActive(true);
            selectedSliceIndex = -1;
            ClearHighlights();
        }

        private void CloseWheelWithoutExecute()
        {
            isWheelOpen = false;
            if (wheelRoot != null) wheelRoot.SetActive(false);
            selectedSliceIndex = -1;
        }

        private void UpdateSliceSelection()
        {
            Vector2 dir = Vector2.zero;

            if (Gamepad.current != null)
            {
                dir = Gamepad.current.rightStick.ReadValue();
                if (dir.sqrMagnitude < 0.2f) dir = Gamepad.current.leftStick.ReadValue();
            }

            if (dir.sqrMagnitude < 0.2f && Mouse.current != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                dir = (mousePos - screenCenter);
                if (dir.magnitude < 25f) dir = Vector2.zero;
                else dir.Normalize();
            }

            if (dir.sqrMagnitude > 0.25f)
            {
                // 5 dilimli çark (üstten başlayarak saat yönünde 72° dilimler)
                float angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; // 0 = Yukarı, 90 = Sağ
                if (angle < 0f) angle += 360f;
                int slice = Mathf.FloorToInt(((angle + 36f) % 360f) / 72f);
                selectedSliceIndex = Mathf.Clamp(slice, 0, 4);
                HighlightSlice(selectedSliceIndex);
            }
            else
            {
                selectedSliceIndex = -1;
                ClearHighlights();
            }
        }

        private void HighlightSlice(int index)
        {
            if (sliceHighlights == null) return;
            for (int i = 0; i < sliceHighlights.Length; i++)
            {
                if (sliceHighlights[i] != null)
                {
                    sliceHighlights[i].color = (i == index) ? new Color(1f, 0.85f, 0.2f, 0.88f) : new Color(0.2f, 0.2f, 0.25f, 0.65f);
                }
            }
        }

        private void ClearHighlights()
        {
            if (sliceHighlights == null) return;
            for (int i = 0; i < sliceHighlights.Length; i++)
            {
                if (sliceHighlights[i] != null)
                {
                    sliceHighlights[i].color = new Color(0.2f, 0.2f, 0.25f, 0.65f);
                }
            }
        }

        private void CloseAndExecuteWheel()
        {
            isWheelOpen = false;
            if (wheelRoot != null) wheelRoot.SetActive(false);

            if (selectedSliceIndex >= 0 && selectedSliceIndex < 5)
            {
                TryExecuteEmoteSlot(selectedSliceIndex + 1);
            }
        }

        public void TryExecuteEmoteSlot(int slotIndex1Based)
        {
            if (!CanUseEmotesNow()) return;

            if (currentCooldown > 0f)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
                return;
            }

            EnsureValidTargetSelected();
            currentCooldown = cooldownDuration;

            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();

            // Yerel oyuncu emote yaptıysa kamerayı hedefin yönüne yumuşakça çevir
            if (PlayerCameraController.Instance != null)
            {
                PlayerCameraController.Instance.TriggerEmotePan(selectedTargetSeatIndex, 1.8f);
            }

            if (NetworkGameManager.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                int mySeat = NetworkGameManager.Instance.LocalSeatIndex;
                NetworkGameManager.Instance.RequestEmoteSlotServerRpc(mySeat, slotIndex1Based, selectedTargetSeatIndex);
            }
            else
            {
                ExecuteLocalEmoteSlot(localSeatIndex, slotIndex1Based, selectedTargetSeatIndex);
            }
        }

        public void ExecuteLocalEmoteSlot(int senderSeat, int slotIndex1Based, int targetSeat)
        {
            if (seatLookup.Count == 0) RefreshSeatLookup();

            Vector3? targetPos = null;
            if (targetSeat >= 0 && seatLookup.TryGetValue(targetSeat, out PlayerSeat tSeat) && tSeat != null)
            {
                targetPos = tSeat.transform.position;
            }

            if (seatLookup.TryGetValue(senderSeat, out PlayerSeat sSeat) && sSeat != null && sSeat.Avatar != null)
            {
                CharacterEmoteData data = GetEmoteData(sSeat.Avatar.Archetype);
                sSeat.Avatar.TriggerEmoteSlot(slotIndex1Based, targetPos, data);
                ShowIncomingEmoteNotification(senderSeat, targetSeat, slotIndex1Based, data);
            }
        }

        public void ShowIncomingEmoteNotification(int senderSeat, int targetSeat, int slotIndex1Based, CharacterEmoteData senderData = null)
        {
            if (notificationRoot == null || notificationText == null) return;
            if (targetSeat < 0 || senderSeat == targetSeat) return;

            string senderName = GetSeatDisplayName(senderSeat);
            string targetName = GetSeatDisplayName(targetSeat);
            EmoteSlotInfo info = senderData != null ? senderData.GetSlot(slotIndex1Based) : null;
            string emoteTitle = info != null ? info.DisplayName : $"Hareket {slotIndex1Based}";

            bool isToMe = (targetSeat == localSeatIndex);
            notificationText.text = isToMe
                ? $"<b>{senderName}</b> sana alay etti: <color=#FFD95A>{emoteTitle}</color>!"
                : $"<b>{senderName}</b> → <b>{targetName}</b>: <color=#FFD95A>{emoteTitle}</color>";

            if (AudioManager.Instance != null && isToMe)
            {
                AudioManager.Instance.PlayUIClick();
            }

            if (notificationRoutine != null) StopCoroutine(notificationRoutine);
            notificationRoutine = StartCoroutine(HideNotificationAfterDelay(2.4f));
        }

        private IEnumerator HideNotificationAfterDelay(float delay)
        {
            notificationRoot.SetActive(true);
            yield return new WaitForSeconds(delay);
            notificationRoot.SetActive(false);
            notificationRoutine = null;
        }
        #endregion
    }
}
