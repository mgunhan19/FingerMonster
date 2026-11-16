using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Unity.Netcode;
using TMPro;
using FingerMonster.Core;
using FingerMonster.Gameplay;
using FingerMonster.Networking;
using FingerMonster.Player;

namespace FingerMonster.UI
{
    /// <summary>
    /// Elenen yerel oyuncu (Hayalet - Kasabın Yardımcısı) için prosedürel olarak
    /// oluşturulan kompakt sağ kenar HUD paneli: ruh enerjisi yuvaları, lanet,
    /// silah seçimi, atmosferik etkiler, izlenen karakter bilgisi ve çıkış butonu içerir.
    /// </summary>
    public class GhostHUD : MonoBehaviour
    {
        public static GhostHUD Instance { get; private set; }

        private const int MaxSoulSlots = 3;
        private const int CurseEnergyCost = 2;
        private const int EffectEnergyCost = 1;
        private const float DefaultEffectCooldown = 3.0f;
        private const int ScareEnergyCost = 1;
        private const float DefaultScareCooldown = 6.0f;
        private const int ThrowEnergyCost = 1;
        private const float DefaultThrowCooldown = 5.0f;

        public event Action OnScareRequested;
        public event Action<ThrowableItemType> OnThrowRequested;
        public ThrowableItemType SelectedThrowable => selectedThrowable;

        private static readonly PropertyInfo CameraGhostModeProperty =
            typeof(PlayerCameraController).GetProperty("CurrentGhostMovementMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? typeof(PlayerCameraController).GetProperty("GhostMovementMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private Canvas targetCanvas;
        private GameObject hudRoot;
        private RectTransform safeAreaRect;
        private RectTransform topLeftPanelRect;
        private RectTransform rightPanelRect;
        private RectTransform exitButtonRect;
        private TMP_FontAsset cachedFont;

        // Üst sol bilgi paneli
        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI spectatorLabel;
        private TextMeshProUGUI controlsHintLabel;

        // Ruh enerjisi göstergesi
        private readonly Image[] energySlots = new Image[MaxSoulSlots];
        private readonly Outline[] energyOutlines = new Outline[MaxSoulSlots];
        private TextMeshProUGUI energyValueLabel;
        private TextMeshProUGUI energyRegenLabel;
        private Coroutine energyAnimCoroutine;

        // Lanet bölümü
        private Button curseButton;
        private Image curseButtonBg;
        private TextMeshProUGUI curseButtonText;
        private TextMeshProUGUI curseStatusText;

        // Silah seçimi (4 buton)
        private readonly Button[] weaponButtons = new Button[4];
        private readonly Image[] weaponButtonBgs = new Image[4];
        private readonly Outline[] weaponOutlines = new Outline[4];
        private ExecutionWeaponType selectedWeapon = ExecutionWeaponType.GiantMeatMallet;

        // Atmosferik etkiler (6 buton)
        private readonly Button[] effectButtons = new Button[6];
        private readonly Image[] effectButtonBgs = new Image[6];
        private TextMeshProUGUI effectStatusText;

        // Korkutma ("Buuu!") ve Fırlatılabilir Eşyalar (4 buton)
        private RectTransform scareAndThrowSectionRect;
        private Button scareButton;
        private Image scareButtonBg;
        private TextMeshProUGUI scareButtonText;
        private TextMeshProUGUI scareStatusText;
        private readonly Button[] throwButtons = new Button[4];
        private readonly Image[] throwButtonBgs = new Image[4];
        private readonly Outline[] throwOutlines = new Outline[4];
        private TextMeshProUGUI throwStatusText;
        private ThrowableItemType selectedThrowable = ThrowableItemType.CreamPie;
        private float localLastScareTime = -20f;
        private float localLastThrowTime = -20f;
        private GhostMovementMode lastMovementMode = (GhostMovementMode)(-1);

        // Çıkış butonu
        private Button exitButton;

        private bool isInitialized = false;
        private bool subscribedGameManager = false;
        private bool subscribedNetworkManager = false;

        private int currentSoulEnergy = 0;
        private int previousSoulEnergy = -1;
        private float ghostWindowRemaining = 0f;
        private bool isGhostWindowOpen = false;
        private float localLastEffectTime = -10f;
        private int lastSpectatedSeat = -1;
        private string lastSpectatorDisplayText = string.Empty;
        private Rect lastSafeArea = Rect.zero;
        private Vector2Int lastScreenSize = Vector2Int.zero;

        // Renk paleti
        private static readonly Color PanelBgColor = new Color(0.06f, 0.07f, 0.12f, 0.90f);
        private static readonly Color SectionBgColor = new Color(0.10f, 0.11f, 0.18f, 0.85f);
        private static readonly Color ActiveEnergyColor = new Color(0.68f, 0.38f, 1.00f, 1.00f);
        private static readonly Color ActiveEnergyGlowColor = new Color(0.75f, 0.85f, 1.00f, 0.95f);
        private static readonly Color EmptyEnergyColor = new Color(0.18f, 0.18f, 0.24f, 0.90f);
        private static readonly Color EmptyEnergyOutlineColor = new Color(0.32f, 0.34f, 0.44f, 0.70f);
        private static readonly Color ButtonNormalColor = new Color(0.15f, 0.18f, 0.27f, 0.96f);
        private static readonly Color ButtonDisabledColor = new Color(0.12f, 0.13f, 0.17f, 0.65f);
        private static readonly Color WeaponSelectedColor = new Color(0.38f, 0.25f, 0.62f, 0.98f);
        private static readonly Color WeaponSelectedOutlineColor = new Color(1.00f, 0.82f, 0.32f, 1.00f);
        private static readonly Color CurseReadyColor = new Color(0.62f, 0.16f, 0.24f, 0.98f);
        private static readonly Color CurseWaitingColor = new Color(0.86f, 0.48f, 0.14f, 0.98f);

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
            SyncVisibilityAndState();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeEvents();

            if (energyAnimCoroutine != null)
            {
                StopCoroutine(energyAnimCoroutine);
                energyAnimCoroutine = null;
            }

            if (hudRoot != null)
            {
                Destroy(hudRoot);
                hudRoot = null;
            }

            isInitialized = false;
        }

        private void Update()
        {
            if (!isInitialized) return;

            ApplySafeAreaIfNeeded();
            TrySubscribeEvents();
            SyncVisibilityAndState();

            if (hudRoot == null || !hudRoot.activeSelf) return;

            if (isGhostWindowOpen && ghostWindowRemaining > 0f)
            {
                ghostWindowRemaining = Mathf.Max(0f, ghostWindowRemaining - Time.deltaTime);
            }

            HandleGhostActionInputs();
            RefreshDynamicUI();
        }

        /// <summary>
        /// Mevcut Canvas altında güvenli alan ve sağ kenar kompakt Hayalet HUD hiyerarşisini kurar; başlangıçta gizlidir.
        /// </summary>
        public void EnsureInitialized()
        {
            if (isInitialized && hudRoot != null) return;

            targetCanvas = GetComponentInParent<Canvas>();
            if (targetCanvas == null)
            {
                targetCanvas = FindFirstObjectByType<Canvas>();
            }
            if (targetCanvas == null) return;

            EnsureCanvasScaler();

            TMP_Text anyTmp = targetCanvas.GetComponentInChildren<TMP_Text>(true);
            if (anyTmp != null) cachedFont = anyTmp.font;

            CleanupDuplicateRoots();
            BuildHierarchy();
            isInitialized = true;
            previousSoulEnergy = -1;
            lastSpectatedSeat = -1;
            lastSpectatorDisplayText = string.Empty;
            hudRoot.SetActive(false);
            TrySubscribeEvents();
        }

        /// <summary>
        /// Yeni maç veya yeniden oyna durumunda Hayalet HUD durumunu sıfırlar ve eski görsel kalıntıları temizler.
        /// </summary>
        public void ResetForNewMatch()
        {
            if (energyAnimCoroutine != null)
            {
                StopCoroutine(energyAnimCoroutine);
                energyAnimCoroutine = null;
            }

            currentSoulEnergy = 0;
            previousSoulEnergy = -1;
            ghostWindowRemaining = 0f;
            isGhostWindowOpen = false;
            localLastEffectTime = -10f;
            localLastScareTime = -20f;
            localLastThrowTime = -20f;
            lastSpectatedSeat = -1;
            lastSpectatorDisplayText = string.Empty;
            lastMovementMode = (GhostMovementMode)(-1);
            selectedWeapon = ExecutionWeaponType.GiantMeatMallet;
            selectedThrowable = ThrowableItemType.CreamPie;

            for (int i = 0; i < MaxSoulSlots; i++)
            {
                if (energySlots[i] != null)
                {
                    energySlots[i].rectTransform.localScale = Vector3.one;
                    energySlots[i].color = EmptyEnergyColor;
                }
            }

            if (hudRoot != null)
            {
                hudRoot.SetActive(false);
            }
        }

        /// <summary>
        /// CanvasScaler ayarlarını 1920x1080 referans çözünürlüğüne ve ScaleWithScreenSize moduna sabitler.
        /// </summary>
        public void EnsureCanvasScaler()
        {
            if (targetCanvas == null) return;

            CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = targetCanvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void CleanupDuplicateRoots()
        {
            if (targetCanvas == null) return;

            for (int i = targetCanvas.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = targetCanvas.transform.GetChild(i);
                if (child != null && child.name == "GhostHUD_Root" && (hudRoot == null || child.gameObject != hudRoot))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void BuildHierarchy()
        {
            hudRoot = new GameObject("GhostHUD_Root", typeof(RectTransform));
            hudRoot.transform.SetParent(targetCanvas.transform, false);
            RectTransform rootRect = hudRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            GameObject safeObj = new GameObject("SafeAreaContainer", typeof(RectTransform));
            safeObj.transform.SetParent(hudRoot.transform, false);
            safeAreaRect = safeObj.GetComponent<RectTransform>();
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;
            ApplySafeArea(force: true);

            BuildTopLeftPanel(safeAreaRect);
            BuildTopRightExitButton(safeAreaRect);
            BuildRightCompactPanel(safeAreaRect);
        }

        private void ApplySafeAreaIfNeeded()
        {
            Vector2Int currentScreen = new Vector2Int(Screen.width, Screen.height);
            Rect currentSafe = Screen.safeArea;
            if (currentScreen != lastScreenSize || currentSafe != lastSafeArea)
            {
                ApplySafeArea(force: false);
            }
        }

        private void ApplySafeArea(bool force)
        {
            if (safeAreaRect == null) return;

            int screenW = Screen.width;
            int screenH = Screen.height;
            Rect safe = Screen.safeArea;

            if (!force && screenW == lastScreenSize.x && screenH == lastScreenSize.y && safe == lastSafeArea)
            {
                return;
            }

            lastScreenSize = new Vector2Int(screenW, screenH);
            lastSafeArea = safe;

            if (screenW <= 0 || screenH <= 0 || safe.width <= 0f || safe.height <= 0f)
            {
                safeAreaRect.anchorMin = Vector2.zero;
                safeAreaRect.anchorMax = Vector2.one;
                safeAreaRect.offsetMin = Vector2.zero;
                safeAreaRect.offsetMax = Vector2.zero;
                return;
            }

            Vector2 anchorMin = new Vector2(
                Mathf.Clamp01(safe.xMin / screenW),
                Mathf.Clamp01(safe.yMin / screenH));
            Vector2 anchorMax = new Vector2(
                Mathf.Clamp01(safe.xMax / screenW),
                Mathf.Clamp01(safe.yMax / screenH));

            safeAreaRect.anchorMin = anchorMin;
            safeAreaRect.anchorMax = anchorMax;
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;
        }

        private void BuildTopLeftPanel(RectTransform parent)
        {
            GameObject topObj = new GameObject("GhostTopLeftPanel", typeof(RectTransform));
            topObj.transform.SetParent(parent, false);
            topLeftPanelRect = topObj.GetComponent<RectTransform>();
            topLeftPanelRect.anchorMin = new Vector2(0f, 1f);
            topLeftPanelRect.anchorMax = new Vector2(0f, 1f);
            topLeftPanelRect.pivot = new Vector2(0f, 1f);
            topLeftPanelRect.anchoredPosition = new Vector2(18f, -14f);
            topLeftPanelRect.sizeDelta = new Vector2(468f, 88f);

            Image topBg = topObj.AddComponent<Image>();
            topBg.color = PanelBgColor;
            Outline topOutline = topObj.AddComponent<Outline>();
            topOutline.effectColor = new Color(0.55f, 0.42f, 0.92f, 0.45f);
            topOutline.effectDistance = new Vector2(1f, -1f);

            titleLabel = CreateTMP(
                topObj.transform,
                "TitleLabel",
                "HAYALET - İZLEME [Tab]",
                15f,
                FontStyles.Bold,
                TextAlignmentOptions.Left,
                new Color(0.72f, 0.85f, 1.0f, 1f));
            RectTransform titleRect = titleLabel.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(12f, -8f);
            titleRect.sizeDelta = new Vector2(-24f, 22f);

            spectatorLabel = CreateTMP(
                topObj.transform,
                "SpectatorLabel",
                "[Q/E] İzlenen: -",
                14f,
                FontStyles.Normal,
                TextAlignmentOptions.Left,
                new Color(0.88f, 0.91f, 0.97f, 1f));
            RectTransform specRect = spectatorLabel.rectTransform;
            specRect.anchorMin = new Vector2(0f, 1f);
            specRect.anchorMax = new Vector2(1f, 1f);
            specRect.pivot = new Vector2(0f, 1f);
            specRect.anchoredPosition = new Vector2(12f, -33f);
            specRect.sizeDelta = new Vector2(-24f, 22f);

            controlsHintLabel = CreateTMP(
                topObj.transform,
                "ControlsHintLabel",
                "WASD Uç | Sağ Tık Bak | F Buuu | 1-4 Eşya | Sol Tık Fırlat",
                12f,
                FontStyles.Normal,
                TextAlignmentOptions.Left,
                new Color(0.68f, 0.80f, 0.94f, 0.95f));
            RectTransform hintRect = controlsHintLabel.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0f, 0f);
            hintRect.anchoredPosition = new Vector2(12f, 8f);
            hintRect.sizeDelta = new Vector2(-24f, 20f);
        }

        private void BuildTopRightExitButton(RectTransform parent)
        {
            exitButton = CreateButton(
                parent,
                "GhostExitButton",
                "Oyundan Çık",
                14f,
                new Color(0.36f, 0.14f, 0.18f, 0.95f),
                out _);
            exitButtonRect = exitButton.GetComponent<RectTransform>();
            exitButtonRect.anchorMin = new Vector2(1f, 1f);
            exitButtonRect.anchorMax = new Vector2(1f, 1f);
            exitButtonRect.pivot = new Vector2(1f, 1f);
            exitButtonRect.anchoredPosition = new Vector2(-62f, -12f);
            exitButtonRect.sizeDelta = new Vector2(128f, 34f);

            Outline exitOutline = exitButton.gameObject.AddComponent<Outline>();
            exitOutline.effectColor = new Color(0.85f, 0.35f, 0.42f, 0.55f);
            exitOutline.effectDistance = new Vector2(1f, -1f);

            exitButton.onClick.AddListener(OnExitClicked);
        }

        private void BuildRightCompactPanel(RectTransform parent)
        {
            GameObject panelObj = new GameObject("GhostRightPanel", typeof(RectTransform));
            panelObj.transform.SetParent(parent, false);
            rightPanelRect = panelObj.GetComponent<RectTransform>();
            rightPanelRect.anchorMin = new Vector2(0.835f, 0.14f);
            rightPanelRect.anchorMax = new Vector2(0.99f, 0.88f);
            rightPanelRect.offsetMin = Vector2.zero;
            rightPanelRect.offsetMax = Vector2.zero;

            Image panelBg = panelObj.AddComponent<Image>();
            panelBg.color = PanelBgColor;
            Outline panelOutline = panelObj.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.58f, 0.42f, 0.95f, 0.45f);
            panelOutline.effectDistance = new Vector2(1f, -1f);

            VerticalLayoutGroup rootLayout = panelObj.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(12, 12, 12, 12);
            rootLayout.spacing = 10f;
            rootLayout.childAlignment = TextAnchor.UpperCenter;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            BuildEnergySection(panelObj.transform);
            BuildCurseSection(panelObj.transform);
            BuildWeaponSection(panelObj.transform);
            BuildEffectSection(panelObj.transform);
            BuildScareAndThrowSection(panelObj.transform);
        }

        private void BuildEnergySection(Transform parent)
        {
            GameObject section = CreateSectionContainer(parent, "EnergySection", 8f);

            TextMeshProUGUI header = CreateTMP(
                section.transform,
                "GhostAbilitiesHeader",
                "HAYALET YETENEKLERİ",
                16f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Color(0.75f, 0.85f, 1.0f, 1f));
            AddLayoutElement(header.gameObject, minHeight: 24f);

            // 3 Yuvalı Enerji Göstergesi
            GameObject slotsRow = new GameObject("SoulEnergySlots", typeof(RectTransform));
            slotsRow.transform.SetParent(section.transform, false);
            AddLayoutElement(slotsRow, minHeight: 34f);

            HorizontalLayoutGroup slotsLayout = slotsRow.AddComponent<HorizontalLayoutGroup>();
            slotsLayout.spacing = 12f;
            slotsLayout.childAlignment = TextAnchor.MiddleCenter;
            slotsLayout.childControlWidth = false;
            slotsLayout.childControlHeight = false;

            for (int i = 0; i < MaxSoulSlots; i++)
            {
                GameObject slotObj = new GameObject($"EnergySlot_{i}", typeof(RectTransform));
                slotObj.transform.SetParent(slotsRow.transform, false);
                RectTransform slotRect = slotObj.GetComponent<RectTransform>();
                slotRect.sizeDelta = new Vector2(28f, 28f);

                Image slotImg = slotObj.AddComponent<Image>();
                slotImg.color = EmptyEnergyColor;

                Outline slotOutline = slotObj.AddComponent<Outline>();
                slotOutline.effectColor = EmptyEnergyOutlineColor;
                slotOutline.effectDistance = new Vector2(2f, -2f);

                energySlots[i] = slotImg;
                energyOutlines[i] = slotOutline;
            }

            // Enerji değeri ve sonraki tur bilgisi
            GameObject infoRow = new GameObject("EnergyInfoRow", typeof(RectTransform));
            infoRow.transform.SetParent(section.transform, false);
            AddLayoutElement(infoRow, minHeight: 22f);

            HorizontalLayoutGroup infoLayout = infoRow.AddComponent<HorizontalLayoutGroup>();
            infoLayout.spacing = 8f;
            infoLayout.childAlignment = TextAnchor.MiddleCenter;
            infoLayout.childControlWidth = true;
            infoLayout.childControlHeight = true;
            infoLayout.childForceExpandWidth = true;

            energyValueLabel = CreateTMP(
                infoRow.transform,
                "EnergyValueLabel",
                "Enerji: 0/3",
                15f,
                FontStyles.Bold,
                TextAlignmentOptions.Left,
                new Color(0.86f, 0.72f, 1.0f, 1f));

            energyRegenLabel = CreateTMP(
                infoRow.transform,
                "EnergyRegenLabel",
                "Sonraki tur: +1",
                14f,
                FontStyles.Normal,
                TextAlignmentOptions.Right,
                new Color(0.72f, 0.80f, 0.92f, 1f));
        }

        private void BuildCurseSection(Transform parent)
        {
            GameObject section = CreateSectionContainer(parent, "CurseSection", 6f);

            curseButton = CreateButton(section.transform, "CurseButton", "LANET AT (2 Enerji)", 14f, CurseReadyColor, out curseButtonText);
            curseButtonBg = curseButton.GetComponent<Image>();
            AddLayoutElement(curseButton.gameObject, minHeight: 44f);
            curseButton.onClick.AddListener(OnCurseClicked);

            curseStatusText = CreateTMP(
                section.transform,
                "CurseStatusText",
                "Pencere kapalı",
                14f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                new Color(0.84f, 0.88f, 0.94f, 1f));
            AddLayoutElement(curseStatusText.gameObject, minHeight: 22f);
        }

        private void BuildWeaponSection(Transform parent)
        {
            GameObject section = CreateSectionContainer(parent, "WeaponSection", 6f);

            TextMeshProUGUI header = CreateTMP(
                section.transform,
                "WeaponHeader",
                "SİLAH SEÇ (Ücretsiz)",
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Color(0.98f, 0.86f, 0.52f, 1f));
            AddLayoutElement(header.gameObject, minHeight: 20f);

            string[] weaponNames = { "Tokmak", "Tava", "Kırbaç", "Pasta" };
            ExecutionWeaponType[] weaponTypes =
            {
                ExecutionWeaponType.GiantMeatMallet,
                ExecutionWeaponType.FlyingCastIronPan,
                ExecutionWeaponType.SausageWhip,
                ExecutionWeaponType.CreamPieLauncher
            };

            // 2x2 düzenli buton gridi (satır bazlı responsive layout)
            for (int row = 0; row < 2; row++)
            {
                GameObject rowObj = CreateGridRow(section.transform, $"WeaponRow_{row}", 6f, 34f);
                for (int col = 0; col < 2; col++)
                {
                    int idx = row * 2 + col;
                    ExecutionWeaponType wType = weaponTypes[idx];
                    int capturedIdx = idx;

                    Button btn = CreateButton(rowObj.transform, $"Weapon_{idx}", weaponNames[idx], 14f, ButtonNormalColor, out _);
                    AddLayoutElement(btn.gameObject, minHeight: 34f);

                    Outline outline = btn.gameObject.AddComponent<Outline>();
                    outline.effectColor = Color.clear;
                    outline.effectDistance = new Vector2(2f, -2f);

                    weaponButtons[idx] = btn;
                    weaponButtonBgs[idx] = btn.GetComponent<Image>();
                    weaponOutlines[idx] = outline;
                    btn.onClick.AddListener(() => OnWeaponClicked(wType, capturedIdx));
                }
            }
        }

        private void BuildEffectSection(Transform parent)
        {
            GameObject section = CreateSectionContainer(parent, "EffectSection", 6f);

            TextMeshProUGUI header = CreateTMP(
                section.transform,
                "EffectHeader",
                "ETKİ (1 Enerji)",
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Color(0.65f, 0.90f, 1.0f, 1f));
            AddLayoutElement(header.gameObject, minHeight: 20f);

            string[] effectNames = { "Lamba", "Kahkaha", "Kâğıt", "Esinti", "Alkış", "Buuu" };
            GhostEffectType[] effectTypes =
            {
                GhostEffectType.FlickerLamp,
                GhostEffectType.EerieLaugh,
                GhostEffectType.FlyPaper,
                GhostEffectType.ColdBreeze,
                GhostEffectType.Applause,
                GhostEffectType.SadBoo
            };

            // 2x3 düzenli buton gridi (3 satır, her satırda 2 buton)
            for (int row = 0; row < 3; row++)
            {
                GameObject rowObj = CreateGridRow(section.transform, $"EffectRow_{row}", 6f, 32f);
                for (int col = 0; col < 2; col++)
                {
                    int idx = row * 2 + col;
                    GhostEffectType eType = effectTypes[idx];

                    Button btn = CreateButton(rowObj.transform, $"Effect_{idx}", effectNames[idx], 14f, ButtonNormalColor, out _);
                    AddLayoutElement(btn.gameObject, minHeight: 32f);

                    effectButtons[idx] = btn;
                    effectButtonBgs[idx] = btn.GetComponent<Image>();
                    btn.onClick.AddListener(() => OnEffectClicked(eType));
                }
            }

            effectStatusText = CreateTMP(
                section.transform,
                "EffectStatusText",
                "Hazır (1 Enerji)",
                14f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                new Color(0.82f, 0.88f, 0.95f, 1f));
            AddLayoutElement(effectStatusText.gameObject, minHeight: 20f);
        }

        private void BuildScareAndThrowSection(Transform parent)
        {
            GameObject section = CreateSectionContainer(parent, "ScareAndThrowSection", 5f);
            scareAndThrowSectionRect = section.GetComponent<RectTransform>();

            scareButton = CreateButton(section.transform, "ScareBuuuButton", "Buuu! [F] (1 Enerji)", 14f, new Color(0.32f, 0.22f, 0.56f, 0.96f), out scareButtonText);
            scareButtonBg = scareButton.GetComponent<Image>();
            AddLayoutElement(scareButton.gameObject, minHeight: 34f);
            scareButton.onClick.AddListener(TryRequestScare);

            scareStatusText = CreateTMP(
                section.transform,
                "ScareStatusText",
                "Hazır (1 Enerji | 6sn)",
                12f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                new Color(0.82f, 0.88f, 0.95f, 1f));
            AddLayoutElement(scareStatusText.gameObject, minHeight: 18f);

            TextMeshProUGUI throwHeader = CreateTMP(
                section.transform,
                "ThrowHeader",
                "FIRLAT (1 Enerji | 5sn)",
                13f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Color(0.98f, 0.82f, 0.45f, 1f));
            AddLayoutElement(throwHeader.gameObject, minHeight: 18f);

            string[] throwNames = { "1: Pasta", "2: Domates", "3: Kâğıt", "4: Sosis" };
            ThrowableItemType[] throwTypes =
            {
                ThrowableItemType.CreamPie,
                ThrowableItemType.Tomato,
                ThrowableItemType.PaperBall,
                ThrowableItemType.Sausage
            };

            for (int row = 0; row < 2; row++)
            {
                GameObject rowObj = CreateGridRow(section.transform, $"ThrowRow_{row}", 6f, 28f);
                for (int col = 0; col < 2; col++)
                {
                    int idx = row * 2 + col;
                    ThrowableItemType tType = throwTypes[idx];

                    Button btn = CreateButton(rowObj.transform, $"Throw_{idx}", throwNames[idx], 13f, ButtonNormalColor, out _);
                    AddLayoutElement(btn.gameObject, minHeight: 28f);

                    Outline outline = btn.gameObject.AddComponent<Outline>();
                    outline.effectColor = Color.clear;
                    outline.effectDistance = new Vector2(2f, -2f);

                    throwButtons[idx] = btn;
                    throwButtonBgs[idx] = btn.GetComponent<Image>();
                    throwOutlines[idx] = outline;
                    btn.onClick.AddListener(() => SelectThrowable(tType));
                }
            }

            throwStatusText = CreateTMP(
                section.transform,
                "ThrowStatusText",
                "Sol Tık: Fırlat (1 Enerji)",
                12f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                new Color(0.82f, 0.88f, 0.95f, 1f));
            AddLayoutElement(throwStatusText.gameObject, minHeight: 18f);
        }

        private GameObject CreateSectionContainer(Transform parent, string name, float spacing)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);

            Image bg = obj.AddComponent<Image>();
            bg.color = SectionBgColor;

            VerticalLayoutGroup layout = obj.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            return obj;
        }

        private GameObject CreateGridRow(Transform parent, string name, float spacing, float minHeight)
        {
            GameObject rowObj = new GameObject(name, typeof(RectTransform));
            rowObj.transform.SetParent(parent, false);
            AddLayoutElement(rowObj, minHeight: minHeight);

            HorizontalLayoutGroup rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = spacing;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            return rowObj;
        }

        private static LayoutElement AddLayoutElement(GameObject obj, float minHeight = -1f)
        {
            LayoutElement le = obj.AddComponent<LayoutElement>();
            if (minHeight >= 0f) le.minHeight = minHeight;
            return le;
        }

        private Button CreateButton(Transform parent, string name, string label, float fontSize, Color bgColor, out TextMeshProUGUI labelTMP)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            Image img = obj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;

            labelTMP = CreateTMP(obj.transform, "Label", label, fontSize, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            labelTMP.enableAutoSizing = false;
            RectTransform lRect = labelTMP.rectTransform;
            lRect.anchorMin = Vector2.zero;
            lRect.anchorMax = Vector2.one;
            lRect.offsetMin = new Vector2(4f, 2f);
            lRect.offsetMax = new Vector2(-4f, -2f);

            return btn;
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

        /// <summary>
        /// Verilen koltuk indeksindeki oyuncunun gerçek karakter adını (ve elenmişse "(Hayalet)" ekini) çözümler.
        /// </summary>
        public string GetSpectatedCharacterDisplayName(int seat)
        {
            if (seat < 0) return "Bilinmiyor";

            if (IsNetworkMatch() && NetworkGameManager.Instance.PlayerSlots != null)
            {
                for (int i = 0; i < NetworkGameManager.Instance.PlayerSlots.Count; i++)
                {
                    NetworkPlayerSlot slot = NetworkGameManager.Instance.PlayerSlots[i];
                    if (slot.SeatIndex == seat)
                    {
                        CharacterArchetype arch = (CharacterArchetype)(Mathf.Abs(slot.ArchetypeIndex) % 4);
                        CharacterDefinition def = CharacterDefinition.GetDefinition(arch);
                        string baseName = def != null && !string.IsNullOrEmpty(def.DisplayName)
                            ? def.DisplayName
                            : slot.PlayerName.ToString();

                        if (string.IsNullOrEmpty(baseName))
                        {
                            baseName = $"Koltuk {seat + 1}";
                        }

                        return (slot.IsGhost || !slot.IsAlive) ? $"{baseName} (Hayalet)" : baseName;
                    }
                }
            }

            if (GameManager.Instance != null && GameManager.Instance.Players != null &&
                seat >= 0 && seat < GameManager.Instance.Players.Count)
            {
                PlayerData p = GameManager.Instance.Players[seat];
                if (p != null)
                {
                    CharacterDefinition def = CharacterDefinition.GetDefinition(p.Archetype);
                    string baseName = def != null && !string.IsNullOrEmpty(def.DisplayName)
                        ? def.DisplayName
                        : p.PlayerName;

                    if (string.IsNullOrEmpty(baseName))
                    {
                        baseName = $"Koltuk {seat + 1}";
                    }

                    return !p.IsAlive ? $"{baseName} (Hayalet)" : baseName;
                }
            }

            return $"Koltuk {seat + 1}";
        }

        /// <summary>
        /// Hayalet HUD panellerinin sahnedeki ana HUD öğeleriyle (BottomBar, TopBar, Banner, ToggleGuideHudBtn)
        /// ekran uzayında çakışıp çakışmadığını kontrol eder. Çakışma yoksa true döner.
        /// </summary>
        public bool CheckPanelOverlaps(out string report)
        {
            StringBuilder sb = new StringBuilder();
            bool hasOverlap = false;

            var ghostPanels = new List<(string name, RectTransform rect)>
            {
                ("GhostRightPanel", rightPanelRect),
                ("GhostTopLeftPanel", topLeftPanelRect),
                ("GhostExitButton", exitButtonRect)
            };

            string[] targetNames = { "BottomBar", "TopBar", "Banner", "ToggleGuideHudBtn" };
            var sceneTargets = new List<(string name, RectTransform rect)>();

            if (targetCanvas != null)
            {
                RectTransform[] allRects = targetCanvas.GetComponentsInChildren<RectTransform>(true);
                foreach (RectTransform rt in allRects)
                {
                    if (rt == null) continue;
                    for (int i = 0; i < targetNames.Length; i++)
                    {
                        if (rt.name == targetNames[i])
                        {
                            sceneTargets.Add((rt.name, rt));
                        }
                    }
                }
            }

            foreach (var gp in ghostPanels)
            {
                if (gp.rect == null || !gp.rect.gameObject.activeInHierarchy) continue;
                Rect gRect = GetScreenRect(gp.rect);

                foreach (var st in sceneTargets)
                {
                    if (st.rect == null || !st.rect.gameObject.activeInHierarchy) continue;
                    Rect sRect = GetScreenRect(st.rect);

                    if (gRect.Overlaps(sRect, true))
                    {
                        hasOverlap = true;
                        sb.AppendLine($"ÇAKIŞMA: {gp.name} {gRect} <-> {st.name} {sRect}");
                    }
                }
            }

            if (rightPanelRect != null && rightPanelRect.gameObject.activeInHierarchy)
            {
                var panelSections = new List<(string name, RectTransform rect)>();
                for (int i = 0; i < rightPanelRect.childCount; i++)
                {
                    RectTransform childRt = rightPanelRect.GetChild(i) as RectTransform;
                    if (childRt != null && childRt.gameObject.activeInHierarchy)
                    {
                        panelSections.Add((childRt.name, childRt));
                    }
                }

                for (int i = 0; i < panelSections.Count; i++)
                {
                    Rect rA = GetScreenRect(panelSections[i].rect);
                    for (int j = i + 1; j < panelSections.Count; j++)
                    {
                        Rect rB = GetScreenRect(panelSections[j].rect);
                        if (rA.Overlaps(rB, false))
                        {
                            hasOverlap = true;
                            sb.AppendLine($"ÇAKIŞMA: {panelSections[i].name} <-> {panelSections[j].name}");
                        }
                    }
                }
            }

            report = hasOverlap ? sb.ToString().TrimEnd() : "Çakışma yok.";
            return !hasOverlap;
        }

        private static Rect GetScreenRect(RectTransform rt)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float xMin = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            float xMax = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            float yMin = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
            float yMax = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private bool IsNetworkMatch()
        {
            return NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.IsListening &&
                   NetworkGameManager.Instance != null;
        }

        private bool IsLocalPlayerGhost()
        {
            if (IsNetworkMatch())
            {
                return NetworkGameManager.Instance.IsLocalGhost;
            }

            return GameManager.Instance != null && GameManager.Instance.IsLocalHumanGhost();
        }

        private void SyncVisibilityAndState()
        {
            if (hudRoot == null) return;

            bool isGameOver = IsNetworkMatch()
                ? NetworkGameManager.Instance.CurrentNetworkState.Value == GameState.GameOver
                : (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.GameOver);

            bool shouldShow = IsLocalPlayerGhost() && !isGameOver;
            if (hudRoot.activeSelf != shouldShow)
            {
                hudRoot.SetActive(shouldShow);
                if (shouldShow)
                {
                    lastSpectatedSeat = -1;
                    lastSpectatorDisplayText = string.Empty;
                    RefreshDynamicUI();
                }
            }
        }

        private void RefreshDynamicUI()
        {
            bool isNetwork = IsNetworkMatch();

            if (isNetwork)
            {
                currentSoulEnergy = NetworkGameManager.Instance.LocalSoulEnergy;
                isGhostWindowOpen = NetworkGameManager.Instance.IsGhostWindowOpen;
            }
            else if (GameManager.Instance != null)
            {
                isGhostWindowOpen = GameManager.Instance.IsGhostWindowOpen;
                int localSeat = GameManager.Instance.GetLocalHumanSeatIndex();
                if (GameManager.Instance.Players != null && localSeat >= 0 && localSeat < GameManager.Instance.Players.Count)
                {
                    currentSoulEnergy = GameManager.Instance.Players[localSeat].SoulEnergy;
                }
            }

            UpdateEnergyDisplay();
            UpdateSpectatorLabel();
            UpdateCurseSection(isNetwork);
            UpdateWeaponSection();
            UpdateEffectSection(isNetwork);
            UpdateScareAndThrowSection();
        }

        private void UpdateEnergyDisplay()
        {
            int clampedEnergy = Mathf.Clamp(currentSoulEnergy, 0, MaxSoulSlots);

            if (previousSoulEnergy != clampedEnergy)
            {
                int oldVal = previousSoulEnergy;
                previousSoulEnergy = clampedEnergy;

                if (oldVal >= 0 && gameObject.activeInHierarchy && hudRoot != null && hudRoot.activeInHierarchy)
                {
                    if (energyAnimCoroutine != null) StopCoroutine(energyAnimCoroutine);
                    energyAnimCoroutine = StartCoroutine(AnimateEnergySlots(oldVal, clampedEnergy));
                }
                else
                {
                    ApplyEnergyVisualsImmediate(clampedEnergy);
                }
            }

            if (energyValueLabel != null)
            {
                energyValueLabel.text = $"Enerji: {clampedEnergy}/{MaxSoulSlots}";
            }
        }

        private void ApplyEnergyVisualsImmediate(int energy)
        {
            for (int i = 0; i < MaxSoulSlots; i++)
            {
                bool filled = i < energy;
                if (energySlots[i] != null)
                {
                    energySlots[i].color = filled ? ActiveEnergyColor : EmptyEnergyColor;
                    energySlots[i].rectTransform.localScale = Vector3.one;
                }
                if (energyOutlines[i] != null)
                {
                    energyOutlines[i].effectColor = filled ? ActiveEnergyGlowColor : EmptyEnergyOutlineColor;
                }
            }
        }

        private IEnumerator AnimateEnergySlots(int oldVal, int newVal)
        {
            ApplyEnergyVisualsImmediate(newVal);

            int minIdx = Mathf.Min(oldVal, newVal);
            int maxIdx = Mathf.Max(oldVal, newVal);
            bool gained = newVal > oldVal;
            float peakScale = gained ? 1.32f : 0.8f;

            const float duration = 0.28f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                float scale = Mathf.Lerp(1f, peakScale, pulse);

                for (int i = minIdx; i < maxIdx && i < MaxSoulSlots; i++)
                {
                    if (energySlots[i] != null)
                    {
                        energySlots[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
                        if (gained)
                        {
                            energySlots[i].color = Color.Lerp(ActiveEnergyColor, Color.white, pulse * 0.45f);
                        }
                    }
                }

                yield return null;
            }

            ApplyEnergyVisualsImmediate(newVal);
            energyAnimCoroutine = null;
        }

        private GhostMovementMode GetCurrentGhostMovementMode()
        {
            PlayerCameraController cam = PlayerCameraController.Instance;
            if (cam == null) return GhostMovementMode.SpectatorOrbit;

            if (CameraGhostModeProperty != null && CameraGhostModeProperty.PropertyType == typeof(GhostMovementMode))
            {
                return (GhostMovementMode)CameraGhostModeProperty.GetValue(cam);
            }

            return cam.IsGhostSpectatorMode ? GhostMovementMode.SpectatorOrbit : GhostMovementMode.FreeFlight;
        }

        private void UpdateSpectatorLabel()
        {
            if (PlayerCameraController.Instance == null || spectatorLabel == null) return;

            GhostMovementMode mode = GetCurrentGhostMovementMode();
            if (mode != lastMovementMode)
            {
                lastMovementMode = mode;
                if (titleLabel != null)
                {
                    titleLabel.text = mode == GhostMovementMode.FreeFlight
                        ? "HAYALET - SERBEST [Tab]"
                        : "HAYALET - İZLEME [Tab]";
                }
            }

            int seat = PlayerCameraController.Instance.GhostSpectatingSeatIndex;
            string displayName = GetSpectatedCharacterDisplayName(seat);
            string nextText = mode == GhostMovementMode.FreeFlight
                ? "Serbest Uçuş [Tab: İzle]"
                : $"[Q/E] İzlenen: {displayName}";

            if (seat != lastSpectatedSeat || nextText != lastSpectatorDisplayText)
            {
                lastSpectatedSeat = seat;
                lastSpectatorDisplayText = nextText;
                spectatorLabel.text = nextText;
            }
        }

        private void UpdateCurseSection(bool isNetwork)
        {
            bool isWaitingCurse = isNetwork
                ? NetworkGameManager.Instance.IsWaitingForGhostCurseTarget
                : (GameManager.Instance != null && GameManager.Instance.IsWaitingForGhostCurseTarget);

            bool usedThisRound = false;
            if (!isNetwork && GameManager.Instance != null && GameManager.Instance.Settings != null)
            {
                usedThisRound = GameManager.Instance.CursesUsedThisRound >= GameManager.Instance.Settings.maxCursesPerRound;
            }

            bool hasEnergy = currentSoulEnergy >= CurseEnergyCost;
            bool canCurse = isWaitingCurse || (isGhostWindowOpen && !usedThisRound && hasEnergy);

            if (curseButton != null)
            {
                curseButton.interactable = canCurse;
            }

            if (curseButtonBg != null)
            {
                if (isWaitingCurse)
                {
                    curseButtonBg.color = CurseWaitingColor;
                }
                else
                {
                    curseButtonBg.color = canCurse ? CurseReadyColor : ButtonDisabledColor;
                }
            }

            if (curseButtonText != null)
            {
                curseButtonText.text = isWaitingCurse ? "TUŞ SEÇİN... (İptal)" : "LANET AT (2 Enerji)";
            }

            if (curseStatusText != null)
            {
                if (isWaitingCurse)
                {
                    curseStatusText.text = "Daktilodan tuş seç...";
                }
                else if (usedThisRound)
                {
                    curseStatusText.text = "Bu turda kullanıldı";
                }
                else if (!isGhostWindowOpen)
                {
                    curseStatusText.text = "Pencere kapalı";
                }
                else if (!hasEnergy)
                {
                    curseStatusText.text = "Enerji yetersiz";
                }
                else
                {
                    curseStatusText.text = $"Pencere: {ghostWindowRemaining:0.0} sn";
                }
            }
        }

        private void UpdateWeaponSection()
        {
            for (int i = 0; i < weaponButtons.Length; i++)
            {
                bool isSelected = (int)selectedWeapon == i;
                if (weaponButtonBgs[i] != null)
                {
                    weaponButtonBgs[i].color = isSelected ? WeaponSelectedColor : ButtonNormalColor;
                }
                if (weaponOutlines[i] != null)
                {
                    weaponOutlines[i].effectColor = isSelected ? WeaponSelectedOutlineColor : Color.clear;
                }
            }
        }

        private void UpdateEffectSection(bool isNetwork)
        {
            float cooldownDuration = (GameManager.Instance != null && GameManager.Instance.Settings != null)
                ? GameManager.Instance.Settings.ghostEffectCooldown
                : DefaultEffectCooldown;

            float lastEffectTime = localLastEffectTime;
            if (!isNetwork && GameManager.Instance != null && GameManager.Instance.Players != null)
            {
                int localSeat = GameManager.Instance.GetLocalHumanSeatIndex();
                if (localSeat >= 0 && localSeat < GameManager.Instance.Players.Count)
                {
                    lastEffectTime = Mathf.Max(lastEffectTime, GameManager.Instance.Players[localSeat].LastGhostEffectTime);
                }
            }

            float remainingCooldown = Mathf.Max(0f, cooldownDuration - (Time.time - lastEffectTime));
            bool onCooldown = remainingCooldown > 0.01f;
            bool hasEnergy = currentSoulEnergy >= EffectEnergyCost;
            bool canTriggerEffect = hasEnergy && !onCooldown;

            for (int i = 0; i < effectButtons.Length; i++)
            {
                if (effectButtons[i] != null)
                {
                    effectButtons[i].interactable = canTriggerEffect;
                }
                if (effectButtonBgs[i] != null)
                {
                    effectButtonBgs[i].color = canTriggerEffect ? ButtonNormalColor : ButtonDisabledColor;
                }
            }

            if (effectStatusText != null)
            {
                if (onCooldown)
                {
                    effectStatusText.text = $"Bekleme: {remainingCooldown:0.0} sn";
                }
                else if (!hasEnergy)
                {
                    effectStatusText.text = "Enerji yetersiz";
                }
                else
                {
                    effectStatusText.text = "Hazır (1 Enerji)";
                }
            }
        }

        private void UpdateScareAndThrowSection()
        {
            float scareCooldown = (GameManager.Instance != null && GameManager.Instance.Settings != null)
                ? GameManager.Instance.Settings.ghostScareCooldown
                : DefaultScareCooldown;
            int scareCost = (GameManager.Instance != null && GameManager.Instance.Settings != null)
                ? GameManager.Instance.Settings.ghostScareCost
                : ScareEnergyCost;

            float scareRemaining = Mathf.Max(0f, scareCooldown - (Time.time - localLastScareTime));
            bool scareOnCd = scareRemaining > 0.01f;
            bool scareHasEnergy = currentSoulEnergy >= scareCost;
            bool canScare = scareHasEnergy && !scareOnCd;

            if (scareButton != null) scareButton.interactable = canScare;
            if (scareButtonBg != null)
            {
                scareButtonBg.color = canScare ? new Color(0.32f, 0.22f, 0.56f, 0.96f) : ButtonDisabledColor;
            }
            if (scareButtonText != null)
            {
                scareButtonText.text = scareOnCd ? $"Buuu! [F] ({scareRemaining:0.0}s)" : "Buuu! [F] (1 Enerji)";
            }
            if (scareStatusText != null)
            {
                if (scareOnCd) scareStatusText.text = $"Bekleme: {scareRemaining:0.0} sn";
                else if (!scareHasEnergy) scareStatusText.text = "Enerji yetersiz";
                else scareStatusText.text = "Hazır (1 Enerji | 6sn)";
            }

            float throwCooldown = (GameManager.Instance != null && GameManager.Instance.Settings != null)
                ? GameManager.Instance.Settings.ghostThrowCooldown
                : DefaultThrowCooldown;
            int throwCost = (GameManager.Instance != null && GameManager.Instance.Settings != null)
                ? GameManager.Instance.Settings.ghostThrowCost
                : ThrowEnergyCost;

            float throwRemaining = Mathf.Max(0f, throwCooldown - (Time.time - localLastThrowTime));
            bool throwOnCd = throwRemaining > 0.01f;
            bool throwHasEnergy = currentSoulEnergy >= throwCost;
            bool canThrow = throwHasEnergy && !throwOnCd;

            for (int i = 0; i < throwButtons.Length; i++)
            {
                bool isSelected = (int)selectedThrowable == i;
                if (throwButtons[i] != null) throwButtons[i].interactable = true;
                if (throwButtonBgs[i] != null)
                {
                    if (!canThrow)
                    {
                        throwButtonBgs[i].color = isSelected ? new Color(0.26f, 0.20f, 0.36f, 0.85f) : ButtonDisabledColor;
                    }
                    else
                    {
                        throwButtonBgs[i].color = isSelected ? WeaponSelectedColor : ButtonNormalColor;
                    }
                }
                if (throwOutlines[i] != null)
                {
                    throwOutlines[i].effectColor = isSelected ? WeaponSelectedOutlineColor : Color.clear;
                }
            }

            if (throwStatusText != null)
            {
                if (throwOnCd) throwStatusText.text = $"Bekleme: {throwRemaining:0.0} sn";
                else if (!throwHasEnergy) throwStatusText.text = "Enerji yetersiz";
                else throwStatusText.text = "Sol Tık: Fırlat (1 Enerji)";
            }
        }

        private void HandleGhostActionInputs()
        {
            if (!IsLocalPlayerGhost()) return;

            bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
                {
                    SelectThrowable(ThrowableItemType.CreamPie);
                }
                else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
                {
                    SelectThrowable(ThrowableItemType.Tomato);
                }
                else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
                {
                    SelectThrowable(ThrowableItemType.PaperBall);
                }
                else if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame)
                {
                    SelectThrowable(ThrowableItemType.Sausage);
                }

                if (Keyboard.current.fKey.wasPressedThisFrame)
                {
                    TryRequestScare();
                }
            }

            if (!pointerOverUI && Mouse.current != null)
            {
                float scrollY = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.01f)
                {
                    int dir = scrollY > 0f ? -1 : 1;
                    int next = (((int)selectedThrowable + dir) % 4 + 4) % 4;
                    SelectThrowable((ThrowableItemType)next);
                }

                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    TryRequestThrow();
                }
            }
        }

        public void SelectThrowable(ThrowableItemType itemType)
        {
            if (selectedThrowable == itemType) return;
            selectedThrowable = itemType;
            RefreshDynamicUI();
        }

        public void NotifyScareCooldownStarted()
        {
            localLastScareTime = Time.time;
            RefreshDynamicUI();
        }

        public void NotifyThrowCooldownStarted()
        {
            localLastThrowTime = Time.time;
            RefreshDynamicUI();
        }

        private void TryRequestScare()
        {
            float scareCooldown = (GameManager.Instance != null && GameManager.Instance.Settings != null)
                ? GameManager.Instance.Settings.ghostScareCooldown
                : DefaultScareCooldown;
            int scareCost = (GameManager.Instance != null && GameManager.Instance.Settings != null)
                ? GameManager.Instance.Settings.ghostScareCost
                : ScareEnergyCost;

            if (currentSoulEnergy < scareCost) return;
            if (Time.time - localLastScareTime < scareCooldown) return;

            OnScareRequested?.Invoke();
        }

        private void TryRequestThrow()
        {
            float throwCooldown = (GameManager.Instance != null && GameManager.Instance.Settings != null)
                ? GameManager.Instance.Settings.ghostThrowCooldown
                : DefaultThrowCooldown;
            int throwCost = (GameManager.Instance != null && GameManager.Instance.Settings != null)
                ? GameManager.Instance.Settings.ghostThrowCost
                : ThrowEnergyCost;

            if (currentSoulEnergy < throwCost) return;
            if (Time.time - localLastThrowTime < throwCooldown) return;

            OnThrowRequested?.Invoke(selectedThrowable);
        }

        private void OnCurseClicked()
        {
            if (IsNetworkMatch())
            {
                if (NetworkGameManager.Instance.IsWaitingForGhostCurseTarget)
                {
                    NetworkGameManager.Instance.LocalCancelGhostCurseSelection();
                }
                else
                {
                    NetworkGameManager.Instance.LocalStartGhostCurseSelection();
                }
            }
            else if (GameManager.Instance != null)
            {
                if (GameManager.Instance.IsWaitingForGhostCurseTarget)
                {
                    GameManager.Instance.CancelLocalGhostCurseSelection();
                }
                else
                {
                    GameManager.Instance.StartLocalGhostCurseSelection();
                }
            }

            RefreshDynamicUI();
        }

        private void OnWeaponClicked(ExecutionWeaponType weapon, int index)
        {
            selectedWeapon = weapon;

            if (IsNetworkMatch())
            {
                NetworkGameManager.Instance.LocalRequestGhostSelectWeapon((int)weapon);
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.TryGhostSelectWeapon(GameManager.Instance.GetLocalHumanSeatIndex(), (int)weapon);
            }

            RefreshDynamicUI();
        }

        private void OnEffectClicked(GhostEffectType effect)
        {
            bool triggered = false;
            if (IsNetworkMatch())
            {
                if (currentSoulEnergy >= EffectEnergyCost)
                {
                    NetworkGameManager.Instance.LocalRequestGhostEffect(effect);
                    localLastEffectTime = Time.time;
                    triggered = true;
                }
            }
            else if (GameManager.Instance != null)
            {
                triggered = GameManager.Instance.TryGhostTriggerEffect(GameManager.Instance.GetLocalHumanSeatIndex(), effect);
                if (triggered)
                {
                    localLastEffectTime = Time.time;
                }
            }

            if (triggered)
            {
                RefreshDynamicUI();
            }
        }

        private void OnExitClicked()
        {
            if (IsNetworkMatch())
            {
                NetworkGameManager.Instance.LocalGhostReturnToMainMenu();
            }
            else
            {
                SceneManager.LoadScene("MainMenu");
            }
        }

        private void TrySubscribeEvents()
        {
            if (!subscribedGameManager && GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerBecameGhost += HandleGamePlayerBecameGhost;
                GameManager.Instance.OnGhostWindowChanged += HandleGhostWindowChanged;
                GameManager.Instance.OnGhostSoulEnergyChanged += HandleGameSoulEnergyChanged;
                subscribedGameManager = true;
            }

            if (!subscribedNetworkManager && NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnLocalBecameGhost += HandleNetworkLocalBecameGhost;
                NetworkGameManager.Instance.OnLocalSoulEnergyUpdated += HandleNetworkSoulEnergyUpdated;
                NetworkGameManager.Instance.OnGhostWindowUpdated += HandleGhostWindowChanged;
                subscribedNetworkManager = true;
            }
        }

        private void UnsubscribeEvents()
        {
            if (subscribedGameManager && GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerBecameGhost -= HandleGamePlayerBecameGhost;
                GameManager.Instance.OnGhostWindowChanged -= HandleGhostWindowChanged;
                GameManager.Instance.OnGhostSoulEnergyChanged -= HandleGameSoulEnergyChanged;
            }
            subscribedGameManager = false;

            if (subscribedNetworkManager && NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnLocalBecameGhost -= HandleNetworkLocalBecameGhost;
                NetworkGameManager.Instance.OnLocalSoulEnergyUpdated -= HandleNetworkSoulEnergyUpdated;
                NetworkGameManager.Instance.OnGhostWindowUpdated -= HandleGhostWindowChanged;
            }
            subscribedNetworkManager = false;
        }

        private void HandleGamePlayerBecameGhost(int seatIndex)
        {
            if (GameManager.Instance != null && seatIndex == GameManager.Instance.GetLocalHumanSeatIndex())
            {
                SyncVisibilityAndState();
            }
        }

        private void HandleNetworkLocalBecameGhost(bool becameGhost)
        {
            SyncVisibilityAndState();
        }

        private void HandleGhostWindowChanged(bool isOpen, float duration)
        {
            isGhostWindowOpen = isOpen;
            ghostWindowRemaining = isOpen ? duration : 0f;
            if (hudRoot != null && hudRoot.activeSelf)
            {
                RefreshDynamicUI();
            }
        }

        private void HandleGameSoulEnergyChanged(int seatIndex, int energy)
        {
            if (GameManager.Instance != null && seatIndex == GameManager.Instance.GetLocalHumanSeatIndex())
            {
                currentSoulEnergy = energy;
                if (hudRoot != null && hudRoot.activeSelf)
                {
                    RefreshDynamicUI();
                }
            }
        }

        private void HandleNetworkSoulEnergyUpdated(int energy)
        {
            currentSoulEnergy = energy;
            if (hudRoot != null && hudRoot.activeSelf)
            {
                RefreshDynamicUI();
            }
        }
    }
}
