using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FingerMonster.Player;
using FingerMonster.Gameplay;
using FingerMonster.UI;
using FingerMonster.Audio;
using FingerMonster.Data;

namespace FingerMonster.Core
{
    /// <summary>
    /// FingerMonster oyununun merkezi durum makinesi ve hakem sınıfı.
    /// Tüm oyuncu verileri, tuzak konumları, tur sırası ve durum geçişleri burada denetlenir.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Ayarlar")]
        [SerializeField] private GameSettings settings;
        public GameSettings Settings => settings;

        [Header("Sahne Referansları")]
        [SerializeField] private TypewriterMachine typewriter;
        [SerializeField] private TurnController turnController;
        [SerializeField] private PlayerCameraController cameraController;
        [SerializeField] private RoomLightingController lightingController;
        [SerializeField] private ButcherPlaceholder butcher;
        [SerializeField] private List<PlayerSeat> playerSeats = new List<PlayerSeat>();
        [SerializeField] private GameObject[] characterPrefabs = new GameObject[4];

        [Header("UI Referansları")]
        [SerializeField] private HUDController hudController;
        [SerializeField] private GameOverUIController gameOverController;
        [SerializeField] private PauseMenuController pauseMenuController;
        [SerializeField] private FingerMonster.Gameplay.CardSelectionController cardSelectionController;
        public FingerMonster.Gameplay.CardSelectionController CardController => cardSelectionController;

        // Oyun Durumu
        public GameState CurrentState { get; private set; } = GameState.Lobby;
        public int CurrentRound { get; private set; } = 1;
        public int ActivePlayerIndex => turnController != null ? turnController.ActivePlayerIndex : 0;
        public TypewriterMachine Typewriter => typewriter;

        public List<PlayerData> Players { get; private set; } = new List<PlayerData>();
        public HashSet<int> TrapIndices { get; private set; } = new HashSet<int>();
        public HashSet<int> PressedIndices { get; private set; } = new HashSet<int>();

        public int RemainingSafeKeysCount
        {
            get
            {
                int totalSafe = (settings != null ? settings.totalKeys : 20) - TrapIndices.Count;
                int pressedSafe = 0;
                foreach (int idx in PressedIndices)
                {
                    if (!TrapIndices.Contains(idx)) pressedSafe++;
                }
                return Mathf.Max(0, totalSafe - pressedSafe);
            }
        }

        // Olaylar (Events)
        public event Action<GameState> OnStateChanged;
        public event Action<PlayerData> OnTurnChanged;
        public event Action<PlayerData> OnCardSelectionStarted;
        public event Action<int, AbilityCardType, int, string> OnAbilityCardPlayed;
        public event Action<IReadOnlyList<PlayerData>> OnAbilityHandsChanged;

        public bool IsWaitingForXRayKeyTarget { get; private set; } = false;
        private int pendingXRayHandSlot = -1;

        // Hayalet (Ghost) Durumu ve Olayları
        public bool IsGhostWindowOpen { get; private set; } = false;
        public int CursedKeyIndex { get; private set; } = -1;
        public int CursesUsedThisRound { get; private set; } = 0;
        public int ChosenNextButcherWeapon { get; private set; } = -1;
        public bool IsWaitingForGhostCurseTarget { get; private set; } = false;
        public HashSet<int> XRayInspectedKeysThisRound { get; private set; } = new HashSet<int>();
        private bool curseCreatedNewTrap = false;
        private Coroutine ghostWindowCoroutine;
        private readonly Dictionary<int, float> ghostLastScareTime = new Dictionary<int, float>();
        private readonly Dictionary<int, float> ghostLastThrowTime = new Dictionary<int, float>();
        private readonly Dictionary<int, float> scareTargetImmunityUntil = new Dictionary<int, float>();
        private GhostHUD boundGhostHud;
        private float lastGhostTransformSendTime = -10f;

        private bool IsNetworkSessionActive =>
            Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening &&
            FingerMonster.Networking.NetworkGameManager.Instance != null;

        public event Action<int> OnPlayerBecameGhost;
        public event Action<bool, float> OnGhostWindowChanged;
        public event Action<int, int> OnGhostSoulEnergyChanged;
        public event Action<int> OnGhostCurseApplied;
        public event Action<int, GhostEffectType> OnGhostEffectTriggered;

        private void Awake()
        {
            Application.runInBackground = true;
            Time.timeScale = 1f;

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureSettings();
        }

        public void EnsureSettings()
        {
            if (Instance == null) Instance = this;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GameSettings>();
            }
            EnsureReferences();
        }

        public void EnsureReferences()
        {
            if (hudController == null) hudController = UnityEngine.Object.FindFirstObjectByType<HUDController>(FindObjectsInactive.Include);
            if (gameOverController == null) gameOverController = UnityEngine.Object.FindFirstObjectByType<GameOverUIController>(FindObjectsInactive.Include);
            if (pauseMenuController == null) pauseMenuController = UnityEngine.Object.FindFirstObjectByType<PauseMenuController>(FindObjectsInactive.Include);
            if (turnController == null) turnController = GetComponent<TurnController>() ?? gameObject.AddComponent<TurnController>();
            if (cameraController == null) cameraController = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>();
            if (lightingController == null) lightingController = UnityEngine.Object.FindFirstObjectByType<RoomLightingController>();
            if (butcher == null) butcher = UnityEngine.Object.FindFirstObjectByType<ButcherPlaceholder>();
            if (typewriter == null) typewriter = UnityEngine.Object.FindFirstObjectByType<TypewriterMachine>();
            if (cardSelectionController == null) cardSelectionController = UnityEngine.Object.FindFirstObjectByType<FingerMonster.Gameplay.CardSelectionController>(FindObjectsInactive.Include);

            // Sahnedeki koltuk referanslarını her zaman taze topla (eski sahneden yok edilmiş referans kalmasın)
            playerSeats = new List<PlayerSeat>(UnityEngine.Object.FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None));
            playerSeats.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));

            if (characterPrefabs == null || characterPrefabs.Length < 4)
            {
                characterPrefabs = new GameObject[4];
            }
            for (int p = 0; p < 4; p++)
            {
                if (characterPrefabs[p] == null)
                {
                    CharacterDefinition pDef = CharacterDefinition.GetDefinition((CharacterArchetype)p);
#if UNITY_EDITOR
                    characterPrefabs[p] = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(pDef.PrefabAssetPath);
#endif
                }
            }
        }

        private void Start()
        {
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                Debug.Log("[GameManager] LAN Multiplayer oturumu aktif - Yerel GameManager devre dışı bırakılıyor.");
                enabled = false;
                return;
            }

            ApplySessionSettings();
            InitializeGame();
            BindGhostHUDEvents();
        }

        private void OnDisable()
        {
            UnbindGhostHUDEvents();
        }

        private void OnDestroy()
        {
            UnbindGhostHUDEvents();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            BindGhostHUDEvents();

            if (Players == null || Players.Count == 0 || settings == null) return;

            Vector3 tableCenter = typewriter != null ? typewriter.transform.position : Vector3.zero;
            int localSeat = GetLocalHumanSeatIndex();
            bool localIsGhost = IsLocalHumanGhost();

            if (localIsGhost && cameraController != null &&
                cameraController.IsGhostSpectatorMode &&
                cameraController.CurrentGhostMode == GhostMovementMode.FreeFlight)
            {
                Vector3 ghostPos = cameraController.CurrentGhostWorldPosition;
                Quaternion ghostRot = cameraController.CurrentGhostWorldRotation;

                if (localSeat >= 0 && localSeat < playerSeats.Count && playerSeats[localSeat]?.Avatar != null)
                {
                    playerSeats[localSeat].Avatar.SetGhostFreeFlightTransform(ghostPos, ghostRot);
                }

                if (IsNetworkSessionActive && Time.time - lastGhostTransformSendTime >= 0.1f)
                {
                    lastGhostTransformSendTime = Time.time;
                    FingerMonster.Networking.NetworkGameManager.Instance.SubmitLocalGhostTransform(
                        ghostPos,
                        ghostRot.eulerAngles.y);
                }
            }

            Vector3? localLivingCamPos = null;
            if (!localIsGhost)
            {
                if (cameraController != null)
                {
                    localLivingCamPos = cameraController.transform.position;
                }
                else if (Camera.main != null)
                {
                    localLivingCamPos = Camera.main.transform.position;
                }
            }

            for (int i = 0; i < Players.Count && i < playerSeats.Count; i++)
            {
                if (!Players[i].IsAlive && playerSeats[i] != null && playerSeats[i].Avatar != null)
                {
                    playerSeats[i].Avatar.UpdateGhostProximityFade(
                        localLivingCamPos,
                        tableCenter,
                        settings.ghostFadeDistance,
                        settings.ghostMinAlphaNearLiving);
                }
            }
        }

        /// <summary>
        /// Ana Menü'de seçilen zorluk (tuzak sayısı) ve bot/insan yapılandırmasını uygular.
        /// Asıl ScriptableObject asset'ini değiştirmemek için çalışma zamanı kopyası kullanılır.
        /// </summary>
        private void ApplySessionSettings()
        {
            settings = Instantiate(settings);
            settings.trapCount = GameSessionData.GetTrapCount();

            var ai = FingerMonster.AI.AIPlayerController.Instance;
            if (ai != null)
            {
                for (int i = 0; i < settings.playerCount; i++)
                {
                    ai.SetPlayerAsBot(i, GameSessionData.IsSlotBot(i));
                }
            }
        }

        /// <summary>
        /// Belirtilen oyuncu bilgisayar (bot) tarafından mı kontrol ediliyor?
        /// </summary>
        public bool IsBotPlayer(int playerIndex)
        {
            var ai = FingerMonster.AI.AIPlayerController.Instance;
            return ai != null && ai.IsBot(playerIndex);
        }

        public int GetLocalHumanSeatIndex()
        {
            int count = Players != null && Players.Count > 0
                ? Players.Count
                : (settings != null ? settings.playerCount : 4);
            for (int i = 0; i < count; i++)
            {
                if (!IsBotPlayer(i)) return i;
            }
            return 0;
        }

        public bool IsLocalHumanGhost()
        {
            int localSeat = GetLocalHumanSeatIndex();
            return Players != null && localSeat >= 0 && localSeat < Players.Count && !Players[localSeat].IsAlive;
        }

        /// <summary>
        /// Oyunu başlatır, oyuncuları tanımlar ve ilk turu hazırlar.
        /// </summary>
        public void InitializeGame()
        {
            EnsureSettings();
            ResetTableForNewGame();
            ChangeState(GameState.Lobby);

            // 4 Oyuncuyu yapılandır (Feg, Kenan, Feyzi, MMG + seçili karaktere göre yerleşim)
            Players.Clear();
            var aiBrain = UnityEngine.Object.FindFirstObjectByType<FingerMonster.AI.AIBrain>();
            for (int i = 0; i < settings.playerCount; i++)
            {
                CharacterArchetype archetype = GameSessionData.GetSeatArchetype(i);
                CharacterDefinition def = CharacterDefinition.GetDefinition(archetype);
                bool isBot = IsBotPlayer(i);

                if (i < playerSeats.Count && playerSeats[i] != null)
                {
                    if (playerSeats[i].Avatar == null || playerSeats[i].Avatar.Archetype != archetype)
                    {
                        int prefabIdx = (int)archetype;
                        GameObject prefab = (characterPrefabs != null && prefabIdx >= 0 && prefabIdx < characterPrefabs.Length)
                            ? characterPrefabs[prefabIdx]
                            : null;
                        if (prefab != null)
                        {
                            playerSeats[i].AssignCharacterPrefab(prefab, def.PrimaryColor);
                        }
                    }
                    playerSeats[i].SetupSeat(i, def.PrimaryColor);
                }

                if (isBot && aiBrain != null)
                {
                    var botProfile = FingerMonster.AI.BotProfile.ForCharacterSlot(i, archetype);
                    aiBrain.RegisterBotProfile(i, botProfile);
                }

                string displayName = isBot ? $"{def.DisplayName} (Bot - {def.PersonalityTag})" : $"{def.DisplayName} ({def.PersonalityTag})";
                PlayerData p = new PlayerData(i, displayName, def.PrimaryColor, archetype, def.PersonalityTag);
                Players.Add(p);
            }

            // Yerel insan oyuncunun koltuğunu bul (Varsayılan: Koltuk 0 / İnsan oyuncu)
            int humanSeat = 0;
            for (int i = 0; i < settings.playerCount; i++)
            {
                if (!IsBotPlayer(i)) { humanSeat = i; break; }
            }

            if (cameraController != null)
            {
                cameraController.SetLocalSeat(humanSeat, snapImmediately: true);
            }

            // Emote çubuğu ve hedef seçici YALNIZCA YEREL OYUNCUNUN koltuğu için bir kez yapılandırılır
            if (EmoteWheelUI.Instance != null && humanSeat >= 0 && humanSeat < Players.Count)
            {
                EmoteWheelUI.Instance.ConfigureForSeat(humanSeat, Players[humanSeat].Archetype);
            }

            if (turnController != null)
            {
                turnController.ResetTurn(0);
            }

            if (hudController != null)
            {
                hudController.SetupHUD(Players);
            }

            StartRound();
        }

        public void ResetTableForNewGame()
        {
            StopAllCoroutines();
            CurrentRound = 1;
            IsWaitingForXRayKeyTarget = false;
            pendingXRayHandSlot = -1;
            IsGhostWindowOpen = false;
            CursedKeyIndex = -1;
            CursesUsedThisRound = 0;
            ChosenNextButcherWeapon = -1;
            IsWaitingForGhostCurseTarget = false;
            curseCreatedNewTrap = false;
            XRayInspectedKeysThisRound.Clear();
            if (ghostWindowCoroutine != null)
            {
                StopCoroutine(ghostWindowCoroutine);
                ghostWindowCoroutine = null;
            }
            if (cameraController != null)
            {
                cameraController.SetGhostSpectatorMode(false, 0);
            }
            var refl = UnityEngine.Object.FindFirstObjectByType<ExecutionReflectionSystem>();
            if (refl != null)
            {
                refl.IsLocalViewerGhost = false;
            }
            if (typewriter != null)
            {
                typewriter.ClearAllCurseVisuals();
            }
            PressedIndices.Clear();
            TrapIndices.Clear();
            if (turnController != null)
            {
                turnController.ResetTurn(0);
            }
            if (Players != null)
            {
                foreach (var p in Players)
                {
                    p.ClearAbilityCardsAndEffects();
                }
            }
            if (cardSelectionController != null)
            {
                cardSelectionController.HideImmediate();
            }
            if (butcher != null)
            {
                butcher.ResumePatrol();
            }
            if (lightingController != null)
            {
                lightingController.SetNormalState();
            }
            ClearGhostActiveGameplayState();
        }

        /// <summary>
        /// Yeni bir tur başlatır: Tuşları ve tuzakları yeniden dağıtır.
        /// </summary>
        public void StartRound()
        {
            ChangeState(GameState.RoundStart);
            PressedIndices.Clear();
            TrapIndices.Clear();
            CursedKeyIndex = -1;
            CursesUsedThisRound = 0;
            curseCreatedNewTrap = false;
            XRayInspectedKeysThisRound.Clear();
            if (typewriter != null)
            {
                typewriter.ClearAllCurseVisuals();
            }
            if (CurrentRound <= 1)
            {
                ClearGhostActiveGameplayState();
            }

            if (cardSelectionController != null)
            {
                cardSelectionController.HideImmediate();
            }

            // Daktiloyu sıfırla
            if (typewriter != null)
            {
                typewriter.ResetAllKeys();
                typewriter.SetupKeys(settings.alphabetLetters);
            }

            // Yetenek kartı ve el yenileme kuralları
            if (CurrentRound == 1 || !settings.keepAbilityCardsBetweenRounds)
            {
                foreach (var p in Players)
                {
                    p.AbilityCards.Clear();
                }
            }

            if (settings.restoreHandsOnNewRound && CurrentRound > 1)
            {
                foreach (var p in Players)
                {
                    if (p.IsAlive)
                    {
                        p.RestoreHands();
                    }
                }
            }

            if (turnController != null)
            {
                turnController.SetReversed(false);
            }

            // Oyuncu kukla avatarlarını sıfırla (sadece hayatta olanlar)
            for (int i = 0; i < playerSeats.Count; i++)
            {
                if (playerSeats[i] != null && playerSeats[i].Avatar != null)
                {
                    if (i < Players.Count && Players[i].IsAlive)
                    {
                        playerSeats[i].Avatar.ResetAvatar();
                    }
                }
            }

            // Tur başında hayattaki oyunculara yetenek kartı dağıt, hayaletlere ruh enerjisi ekle
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].IsAlive)
                {
                    DrawAbilityCardsForPlayer(i, settings.cardsDrawnPerRoundStart);
                }
                else
                {
                    Players[i].AddSoulEnergy(settings.ghostEnergyPerRound, settings.ghostMaxSoulEnergy);
                    OnGhostSoulEnergyChanged?.Invoke(i, Players[i].SoulEnergy);
                }
            }
            OnAbilityHandsChanged?.Invoke(Players);
            if (hudController != null)
            {
                hudController.RefreshAllCards(Players, ActivePlayerIndex);
            }

            // Rastgele 6 gizli tuzak seç
            List<int> allIndices = new List<int>();
            for (int i = 0; i < settings.totalKeys; i++) allIndices.Add(i);

            // Karıştır (Fisher-Yates)
            for (int i = 0; i < allIndices.Count; i++)
            {
                int r = UnityEngine.Random.Range(i, allIndices.Count);
                int tmp = allIndices[i];
                allIndices[i] = allIndices[r];
                allIndices[r] = tmp;
            }

            int trapsToPlace = Mathf.Min(settings.trapCount, settings.totalKeys - 1);
            for (int i = 0; i < trapsToPlace; i++)
            {
                TrapIndices.Add(allIndices[i]);
            }

            Debug.Log($"[GameManager] Tur {CurrentRound} Başladı! Tuzak Tuş İndeksleri: {string.Join(", ", TrapIndices)}");

            if (hudController != null)
            {
                hudController.UpdateRoundInfo(CurrentRound, RemainingSafeKeysCount);
                hudController.ShowBanner($"TUR {CurrentRound} BAŞLADI!", Color.white, 1.8f);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBell();
            }

            StartCoroutine(DelayToFirstTurn());
        }

        private IEnumerator DelayToFirstTurn()
        {
            yield return new WaitForSeconds(settings.turnTransitionDelay);
            BeginPlayerTurn(turnController.ActivePlayerIndex);
        }

        /// <summary>
        /// Sıradaki oyuncunun turunu açar. Önce 3 Kart mini-oyununu başlatır;
        /// kart doğru bilinirse sırayı devreder, yanlış bilinirse daktilo tuşlarına basma aşamasını açar.
        /// </summary>
        private void BeginPlayerTurn(int playerIndex)
        {
            PlayerData activePlayer = Players[playerIndex];
            Debug.Log($"[GameManager] Sıra: {activePlayer.PlayerName} (Kart Seçimi Aşaması)");

            if (typewriter != null)
            {
                typewriter.ClearAllXRayVisuals();
            }
            IsWaitingForXRayKeyTarget = false;
            pendingXRayHandSlot = -1;
            activePlayer.BeginTurnAbilityReset();
            if (cardSelectionController != null)
            {
                cardSelectionController.SetFoggedState(activePlayer.IsFogged);
            }

            // Koltuk ve ışık vurgusu
            for (int i = 0; i < playerSeats.Count; i++)
            {
                if (playerSeats[i] != null)
                {
                    playerSeats[i].SetHighlight(i == playerIndex);
                }
            }

            if (lightingController != null)
            {
                lightingController.SetTurnState(activePlayer.PlayerColor);
            }

            if (hudController != null)
            {
                hudController.UpdateTurn(activePlayer, Players);
            }

            // Kart mini-oyununu başlat
            if (cardSelectionController == null)
            {
                cardSelectionController = UnityEngine.Object.FindFirstObjectByType<FingerMonster.Gameplay.CardSelectionController>(FindObjectsInactive.Include);
            }

            ChangeState(GameState.CardPlay);
            OnAbilityHandsChanged?.Invoke(Players);

            if (HasAnyGhostPlayer())
            {
                OpenGhostWindow();
            }

            if (cardSelectionController != null)
            {
                cardSelectionController.SetNetworkClickInterceptor(null);

                bool isBot = IsBotPlayer(playerIndex);
                if (isBot && hudController != null)
                {
                    hudController.SetPromptText($"Sıra: <color=#{ColorUtility.ToHtmlStringRGB(activePlayer.PlayerColor)}>{activePlayer.PlayerName}</color> — Kader kartı seçiyor...");
                }

                cardSelectionController.ShowCardSelection(
                    playerIndex,
                    activePlayer.PlayerName,
                    activePlayer.PlayerColor,
                    isBot,
                    (isSafe) => HandleCardSelectionResult(playerIndex, isSafe)
                );

                ChangeState(GameState.CardSelection);
                OnCardSelectionStarted?.Invoke(activePlayer);
            }
            else
            {
                // Fallback: Kart kontrolcüsü yoksa doğrudan daktilo turuna geç
                ActivateTypewriterTurn(playerIndex);
            }
        }

        private void HandleCardSelectionResult(int playerIndex, bool isSafe)
        {
            if (CurrentState != GameState.CardSelection || ActivePlayerIndex != playerIndex) return;
            if (playerIndex < 0 || playerIndex >= Players.Count) return;

            PlayerData player = Players[playerIndex];
            bool isBot = IsBotPlayer(playerIndex);

            player.IsFogged = false;
            if (cardSelectionController != null)
            {
                cardSelectionController.SetFoggedState(false);
            }

            if (isSafe)
            {
                // DOĞRU BİLDİ - DAKTİLOYA BASMAKTAN KURTULDU!
                Debug.Log($"[GameManager] {player.PlayerName} GÜVENLİ kartı buldu ve pas geçti!");

                DrawAbilityCardsForPlayer(playerIndex, settings.cardsDrawnOnSafeCardPass);
                player.EndTurnAbilityUpdate();

                if (hudController != null)
                {
                    if (isBot)
                    {
                        hudController.ShowBanner($"{player.PlayerName} DOĞRU KARTI BULDU!\nDaktiloya basmadan sırayı pas geçiyor!", new Color(0.2f, 0.85f, 0.3f), 1.4f);
                    }
                    else
                    {
                        hudController.ShowBanner($"{player.PlayerName} Güvenli Kartı Buldu ve Pas Geçti!", Color.green, 1.8f);
                    }
                }

                if (playerIndex < playerSeats.Count && playerSeats[playerIndex] != null && playerSeats[playerIndex].Avatar != null)
                {
                    playerSeats[playerIndex].Avatar.PlayJoyRelief();
                }

                // Sıra hemen bir sonraki hayatta olan oyuncuya geçer
                int nextPlayer = turnController.AdvanceToNextAlivePlayer(Players);
                if (nextPlayer != -1)
                {
                    StartCoroutine(TransitionToNextTurn(nextPlayer));
                }
                else
                {
                    EndGame();
                }
            }
            else
            {
                // YANLIŞ BİLDİ - CANAVAR! DAKTİLO TUŞLARINA BASMA AŞAMASINA GEÇİLİR
                Debug.Log($"[GameManager] {player.PlayerName} CANAVAR kart çekti! Daktilo tuşuna basmak zorunda!");

                if (hudController != null)
                {
                    if (isBot)
                    {
                        hudController.ShowBanner($"{player.PlayerName} YANLIŞ KARTI SEÇTİ!\nŞimdi daktiloda bir tuşa basacak!", new Color(0.9f, 0.25f, 0.2f), 1.4f);
                    }
                    else
                    {
                        hudController.ShowBanner($"{player.PlayerName} Canavara Yakalandı! Daktiloda tuşa basmak zorunda!", Color.red, 1.8f);
                    }
                }

                ActivateTypewriterTurn(playerIndex);
            }
        }

        private void ActivateTypewriterTurn(int playerIndex)
        {
            ChangeState(GameState.PlayerTurn);

            PlayerData activePlayer = Players[playerIndex];

            if (playerIndex < playerSeats.Count && playerSeats[playerIndex] != null && playerSeats[playerIndex].Avatar != null)
            {
                playerSeats[playerIndex].Avatar.PlayThinking(1.2f);
            }

            OnTurnChanged?.Invoke(activePlayer);
        }

        /// <summary>
        /// Gelen ICommand eylemini doğrular ve yürütür.
        /// </summary>
        public bool ProcessCommand(ICommand command)
        {
            if (command == null) return false;

            if (command.Validate(this))
            {
                command.Execute(this);
                return true;
            }

            return false;
        }

        public bool IsKeyPressed(int keyIndex)
        {
            return PressedIndices.Contains(keyIndex);
        }

        #region YETENEK KARTLARI (ABILITY CARDS)

        /// <summary>
        /// Belirtilen oyuncuya ağırlıklı rastgele yetenek kart(lar)ı çektirir.
        /// </summary>
        public bool DrawAbilityCardsForPlayer(int playerIndex, int count = 1)
        {
            if (settings == null || Players == null || playerIndex < 0 || playerIndex >= Players.Count) return false;
            PlayerData player = Players[playerIndex];
            if (!player.IsAlive || count <= 0) return false;

            bool anyAdded = false;
            for (int i = 0; i < count; i++)
            {
                AbilityCardType drawn = settings.DrawWeightedRandomAbilityCard();
                if (player.TryAddAbilityCard(drawn, settings.maxAbilityCardsInHand))
                {
                    anyAdded = true;
                }
            }

            if (anyAdded)
            {
                OnAbilityHandsChanged?.Invoke(Players);
                if (hudController != null)
                {
                    hudController.RefreshAllCards(Players, ActivePlayerIndex);
                }
            }

            return anyAdded;
        }

        /// <summary>
        /// Belirtilen oyuncunun şu an ilgili yetenek kartını oynayıp oynayamayacağını kurallara göre denetler.
        /// </summary>
        public bool CanPlayAbilityCard(int playerIndex, AbilityCardType cardType, out string reason)
        {
            reason = string.Empty;
            if (Players == null || playerIndex < 0 || playerIndex >= Players.Count)
            {
                reason = "Geçersiz oyuncu!";
                return false;
            }

            if (ActivePlayerIndex != playerIndex)
            {
                reason = "Sıra sende değil!";
                return false;
            }

            PlayerData player = Players[playerIndex];
            if (!player.IsAlive)
            {
                reason = "Elendin!";
                return false;
            }

            if (CurrentState != GameState.CardPlay && CurrentState != GameState.CardSelection && CurrentState != GameState.PlayerTurn)
            {
                reason = "Şu an yetenek kartı kullanılamaz!";
                return false;
            }

            if (player.UsedAbilityThisTurn)
            {
                reason = "Bu tur zaten bir yetenek kartı kullandın!";
                return false;
            }

            CardData def = CardData.GetDefinition(cardType);
            if (def != null)
            {
                if ((CurrentState == GameState.CardPlay || CurrentState == GameState.CardSelection) && !def.canUseDuringCardSelection)
                {
                    reason = $"{def.displayName} kart seçimi aşamasında kullanılamaz!";
                    return false;
                }
                if (CurrentState == GameState.PlayerTurn && !def.canUseDuringTypewriterTurn)
                {
                    reason = $"{def.displayName} daktilo aşamasında kullanılamaz!";
                    return false;
                }
            }

            switch (cardType)
            {
                case AbilityCardType.Shield:
                    if (player.HasActiveShield)
                    {
                        reason = "Kalkan zaten aktif!";
                        return false;
                    }
                    break;

                case AbilityCardType.Possess:
                    if (CurrentState != GameState.CardSelection)
                    {
                        reason = "Ele Geçir sadece 3 kart seçimi sırasında kullanılabilir!";
                        return false;
                    }
                    if (player.IsFogged || (cardSelectionController != null && cardSelectionController.IsCurrentSelectionFogged))
                    {
                        reason = "Sis altındayken Ele Geçir kullanılamaz!";
                        return false;
                    }
                    if (cardSelectionController != null && cardSelectionController.HintedMonsterCardIndex >= 0)
                    {
                        reason = "Bu seçimde zaten ipucu alındı!";
                        return false;
                    }
                    break;

                case AbilityCardType.XRay:
                    if (GetRandomUnpressedKeyIndex() < 0)
                    {
                        reason = "Taranacak basılmamış tuş kalmadı!";
                        return false;
                    }
                    break;

                case AbilityCardType.SkipTurn:
                    if (player.UsedSkipLastTurn)
                    {
                        reason = "Sıra Atlat üst üste 2 tur kullanılamaz!";
                        return false;
                    }
                    break;

                case AbilityCardType.ReverseDirection:
                    if (CurrentState != GameState.CardSelection && CurrentState != GameState.CardPlay)
                    {
                        reason = "Ters Yön sadece kart seçimi aşamasında kullanılabilir!";
                        return false;
                    }
                    break;

                case AbilityCardType.Fog:
                    if (FindRandomLivingRival(playerIndex) < 0)
                    {
                        reason = "Hedeflenecek hayatta rakip yok!";
                        return false;
                    }
                    break;

                default:
                    reason = "Geçersiz kart türü!";
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Oyuncunun elindeki bir yetenek kartını oynar ve etkisini uygular.
        /// </summary>
        public bool TryPlayAbilityCard(int playerIndex, int handSlotIndex, int targetIndex = -1)
        {
            if (Players == null || playerIndex < 0 || playerIndex >= Players.Count) return false;
            PlayerData player = Players[playerIndex];
            if (handSlotIndex < 0 || handSlotIndex >= player.AbilityCards.Count) return false;

            AbilityCardType cardType = player.AbilityCards[handSlotIndex];
            if (!CanPlayAbilityCard(playerIndex, cardType, out string reason))
            {
                Debug.LogWarning($"[GameManager] Yetenek kartı oynanamadı ({cardType}): {reason}");
                return false;
            }

            bool isLocalHuman = !IsBotPlayer(playerIndex);
            string summaryMsg = string.Empty;
            int resolvedTarget = targetIndex;

            switch (cardType)
            {
                case AbilityCardType.Shield:
                    player.HasActiveShield = true;
                    summaryMsg = $"{player.PlayerName} KALKAN açtı!";
                    ConsumeAbilityCard(player, handSlotIndex, playerIndex, cardType, -1, summaryMsg);
                    return true;

                case AbilityCardType.Possess:
                    int revealed = cardSelectionController != null
                        ? cardSelectionController.RevealOneMonsterHint(applyVisualsForLocalHuman: isLocalHuman)
                        : -1;
                    if (revealed < 0) return false;
                    summaryMsg = $"{player.PlayerName} ELE GEÇİR oynadı (Canavar kartı ifşa edildi)!";
                    ConsumeAbilityCard(player, handSlotIndex, playerIndex, cardType, revealed, summaryMsg);
                    return true;

                case AbilityCardType.XRay:
                    if (targetIndex >= 0)
                    {
                        if (!InspectKeyWithXRay(targetIndex, isLocalHuman)) return false;
                        summaryMsg = $"{player.PlayerName} RÖNTGEN ile tuş #{targetIndex} taradı!";
                        ConsumeAbilityCard(player, handSlotIndex, playerIndex, cardType, targetIndex, summaryMsg);
                        return true;
                    }
                    else if (CurrentState == GameState.PlayerTurn && isLocalHuman)
                    {
                        pendingXRayHandSlot = handSlotIndex;
                        IsWaitingForXRayKeyTarget = true;
                        if (hudController != null)
                        {
                            hudController.ShowBanner("RÖNTGEN AKTİF: Taramak istediğin daktilo tuşuna tıkla!", new Color(0.25f, 0.92f, 0.55f), 2.5f);
                        }
                        return true;
                    }
                    else
                    {
                        int randomKey = GetRandomUnpressedKeyIndex();
                        if (randomKey < 0 || !InspectKeyWithXRay(randomKey, isLocalHuman)) return false;
                        summaryMsg = $"{player.PlayerName} RÖNTGEN ile rastgele bir tuş taradı!";
                        ConsumeAbilityCard(player, handSlotIndex, playerIndex, cardType, randomKey, summaryMsg);
                        return true;
                    }

                case AbilityCardType.SkipTurn:
                    player.UsedSkipThisTurn = true;
                    player.IsFogged = false;
                    if (cardSelectionController != null)
                    {
                        cardSelectionController.SetFoggedState(false);
                        cardSelectionController.HideImmediate();
                    }
                    summaryMsg = $"{player.PlayerName} SIRA ATLAT oynadı ve sırasını devretti!";
                    ConsumeAbilityCard(player, handSlotIndex, playerIndex, cardType, -1, summaryMsg);
                    player.EndTurnAbilityUpdate();
                    AdvanceTurnAfterAbilitySkip();
                    return true;

                case AbilityCardType.ReverseDirection:
                    bool isNowReversed = turnController != null && turnController.ToggleDirection();
                    player.IsFogged = false;
                    if (cardSelectionController != null)
                    {
                        cardSelectionController.SetFoggedState(false);
                        cardSelectionController.HideImmediate();
                    }
                    summaryMsg = $"{player.PlayerName} TERS YÖN oynadı! Dönüş yönü: {(isNowReversed ? "TERS" : "NORMAL")}";
                    ConsumeAbilityCard(player, handSlotIndex, playerIndex, cardType, isNowReversed ? 1 : 0, summaryMsg);
                    player.EndTurnAbilityUpdate();
                    AdvanceTurnAfterAbilitySkip();
                    return true;

                case AbilityCardType.Fog:
                    if (resolvedTarget < 0 || resolvedTarget >= Players.Count || resolvedTarget == playerIndex || !Players[resolvedTarget].IsAlive)
                    {
                        resolvedTarget = FindRandomLivingRival(playerIndex);
                    }
                    if (resolvedTarget < 0) return false;
                    Players[resolvedTarget].IsFogged = true;
                    summaryMsg = $"{player.PlayerName}, {Players[resolvedTarget].PlayerName} üzerine SİS attı!";
                    ConsumeAbilityCard(player, handSlotIndex, playerIndex, cardType, resolvedTarget, summaryMsg);
                    return true;
            }

            return false;
        }

        private void ConsumeAbilityCard(PlayerData player, int handSlotIndex, int playerIndex, AbilityCardType cardType, int targetIndex, string message)
        {
            player.RemoveAbilityCardAt(handSlotIndex);
            player.UsedAbilityThisTurn = true;
            OnAbilityCardPlayed?.Invoke(playerIndex, cardType, targetIndex, message);
            OnAbilityHandsChanged?.Invoke(Players);
            if (hudController != null)
            {
                hudController.RefreshAllCards(Players, ActivePlayerIndex);
                if (!string.IsNullOrEmpty(message))
                {
                    hudController.ShowBanner(message, CardData.GetDefinition(cardType)?.accentColor ?? Color.cyan, 1.8f);
                }
            }
        }

        private void AdvanceTurnAfterAbilitySkip()
        {
            int nextPlayer = turnController.AdvanceToNextAlivePlayer(Players);
            if (nextPlayer != -1)
            {
                StartCoroutine(TransitionToNextTurn(nextPlayer));
            }
            else
            {
                EndGame();
            }
        }

        /// <summary>
        /// Röntgen kartı hedef bekleme modundayken seçilen tuşu onaylar ve tarar.
        /// </summary>
        public bool ConfirmXRayKeyTarget(int keyIndex)
        {
            if (!IsWaitingForXRayKeyTarget) return false;

            int playerIndex = ActivePlayerIndex;
            if (Players == null || playerIndex < 0 || playerIndex >= Players.Count)
            {
                CancelXRayKeyTarget();
                return false;
            }

            int totalKeys = settings != null ? settings.totalKeys : 20;
            if (keyIndex < 0 || keyIndex >= totalKeys || IsKeyPressed(keyIndex))
            {
                return false;
            }

            int slot = pendingXRayHandSlot;
            IsWaitingForXRayKeyTarget = false;
            pendingXRayHandSlot = -1;

            PlayerData player = Players[playerIndex];
            if (slot < 0 || slot >= player.AbilityCards.Count || player.AbilityCards[slot] != AbilityCardType.XRay)
            {
                return false;
            }

            if (!InspectKeyWithXRay(keyIndex, !IsBotPlayer(playerIndex)))
            {
                return false;
            }

            string summaryMsg = $"{player.PlayerName} RÖNTGEN ile tuş #{keyIndex} taradı!";
            ConsumeAbilityCard(player, slot, playerIndex, AbilityCardType.XRay, keyIndex, summaryMsg);
            return true;
        }

        /// <summary>
        /// Röntgen hedef seçimini iptal eder.
        /// </summary>
        public void CancelXRayKeyTarget()
        {
            IsWaitingForXRayKeyTarget = false;
            pendingXRayHandSlot = -1;
        }

        /// <summary>
        /// Belirtilen tuşun tuzak olup olmadığını Röntgen ile tarar ve yerel insan için görselleştirir.
        /// </summary>
        public bool InspectKeyWithXRay(int keyIndex, bool showVisualForLocalHuman)
        {
            int totalKeys = settings != null ? settings.totalKeys : 20;
            if (keyIndex < 0 || keyIndex >= totalKeys || IsKeyPressed(keyIndex)) return false;

            XRayInspectedKeysThisRound.Add(keyIndex);

            bool isTrap = TrapIndices.Contains(keyIndex);
            if (showVisualForLocalHuman && typewriter != null)
            {
                TypewriterKey key = typewriter.GetKey(keyIndex);
                if (key != null)
                {
                    key.ApplyXRayVisual(isTrap);
                }
            }
            return true;
        }

        private int GetRandomUnpressedKeyIndex()
        {
            int totalKeys = settings != null ? settings.totalKeys : 20;
            List<int> candidates = new List<int>();
            for (int i = 0; i < totalKeys; i++)
            {
                if (!PressedIndices.Contains(i))
                {
                    candidates.Add(i);
                }
            }
            return candidates.Count > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Count)] : -1;
        }

        #endregion

        /// <summary>
        /// PressKeyCommand tarafından tetiklenir: Tuşun güvenli mi yoksa tuzak mı olduğunu çözer.
        /// </summary>
        public void ResolveKey(int playerIndex, int keyIndex)
        {
            CloseGhostWindow();
            ChangeState(GameState.KeyResolve);
            PressedIndices.Add(keyIndex);

            bool isTrap = TrapIndices.Contains(keyIndex);
            PlayerData player = Players[playerIndex];

            player.EndTurnAbilityUpdate();
            if (typewriter != null)
            {
                typewriter.ClearAllXRayVisuals();
            }
            IsWaitingForXRayKeyTarget = false;
            pendingXRayHandSlot = -1;

            // Daktilo tuş animasyonu
            if (typewriter != null)
            {
                typewriter.PressKey(keyIndex, isTrap);
            }

            if (hudController != null)
            {
                hudController.UpdateRoundInfo(CurrentRound, RemainingSafeKeysCount);
            }

            Vector3 keyWorldPos = (typewriter != null && typewriter.GetKey(keyIndex) != null) 
                ? typewriter.GetKey(keyIndex).transform.position 
                : Vector3.zero;

            if (keyIndex == CursedKeyIndex)
            {
                CursedKeyIndex = -1;
                if (curseCreatedNewTrap)
                {
                    AudioManager.Instance?.PlayGhostCurseExplosion();
                    CartoonFXManager.Instance?.PlayTrapSnapFX(keyWorldPos, new Color(0.75f, 0.2f, 0.95f));
                }
                curseCreatedNewTrap = false;
            }

            PlayerVisualAvatar activeAvatar = (playerIndex < playerSeats.Count && playerSeats[playerIndex] != null)
                ? playerSeats[playerIndex].Avatar
                : null;

            if (activeAvatar != null)
            {
                activeAvatar.PlayPressKey(player.RemainingHandsCount, null, keyWorldPos);
            }

            if (!isTrap)
            {
                // GÜVENLİ TUŞ
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayTypewriterClick();
                }

                if (CartoonFXManager.Instance != null)
                {
                    CartoonFXManager.Instance.PlaySafeKeyFX(keyWorldPos);
                }

                if (activeAvatar != null)
                {
                    activeAvatar.PlayJoyRelief();
                }

                // Bot güvenli tuşa bastıysa kişiliğine göre sevinç emote'u yapabilir
                var aiBrain = UnityEngine.Object.FindFirstObjectByType<FingerMonster.AI.AIBrain>();
                if (IsBotPlayer(playerIndex) && aiBrain != null && activeAvatar != null)
                {
                    int target = FindRandomLivingRival(playerIndex);
                    Vector3? tPos = (target >= 0 && target < playerSeats.Count && playerSeats[target] != null) ? playerSeats[target].transform.position : (Vector3?)null;
                    CharacterEmoteData data = UI.EmoteWheelUI.Instance != null ? UI.EmoteWheelUI.Instance.GetEmoteData(activeAvatar.Archetype) : null;
                    int usedSlot = aiBrain.TriggerBotReactionEmote(playerIndex, activeAvatar, true, false, tPos, data);
                    if (usedSlot > 0 && UI.EmoteWheelUI.Instance != null)
                    {
                        UI.EmoteWheelUI.Instance.ShowIncomingEmoteNotification(playerIndex, target, usedSlot, data);
                    }
                }

                player.Score += settings.pointsPerSafeKey;
                Debug.Log($"[GameManager] {player.PlayerName} güvenli tuşa bastı! Skor: {player.Score}");

                if (hudController != null)
                {
                    hudController.RefreshAllCards(Players, playerIndex);
                    hudController.ShowBanner($"{player.PlayerName} harfi yazdı! (+{settings.pointsPerSafeKey} Puan)", Color.green, 1.0f);
                }

                StartCoroutine(PostKeyRoutine(playerIndex, isTrap: false));
            }
            else
            {
                // TUZAK TUŞ! Kalkan koruması kontrolü (aktif veya elde pasif)
                if (player.HasActiveShield || player.HasAbilityCard(AbilityCardType.Shield))
                {
                    if (player.HasActiveShield)
                    {
                        player.HasActiveShield = false;
                    }
                    else
                    {
                        player.RemoveFirstAbilityCardOfType(AbilityCardType.Shield);
                    }

                    Debug.Log($"[GameManager] KALKAN! {player.PlayerName} tuzak tuştan korundu!");
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayBell();
                    }
                    if (CartoonFXManager.Instance != null)
                    {
                        CartoonFXManager.Instance.PlaySafeKeyFX(keyWorldPos);
                    }

                    OnAbilityHandsChanged?.Invoke(Players);
                    if (hudController != null)
                    {
                        hudController.RefreshAllCards(Players, playerIndex);
                        hudController.ShowBanner($"KALKAN KORUDU! {player.PlayerName} elini kaybetmekten kurtuldu!", new Color(0.18f, 0.78f, 0.96f), 1.8f);
                    }

                    StartCoroutine(PostKeyRoutine(playerIndex, isTrap: false));
                    return;
                }

                ChangeState(GameState.HandLost);

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayTrapTriggered();
                }

                PlayerHand lostHand = player.SeverRandomHand();
                string handName = (lostHand == PlayerHand.Left) ? "SOL" : "SAĞ";
                Debug.Log($"[GameManager] TUZAK! {player.PlayerName} {handName} elini kaybetti!");

                // Çizgi film patlama efekti (el kopması Kasap vurduğunda gerçekleşecek)
                if (CartoonFXManager.Instance != null)
                {
                    CartoonFXManager.Instance.PlayTrapSnapFX(keyWorldPos, player.PlayerColor);
                }

                TriggerBotRivalReaction(playerIndex);

                StartCoroutine(PostKeyRoutine(playerIndex, isTrap: true, lostHand));
            }
        }

        private IEnumerator PostKeyRoutine(int playerIndex, bool isTrap, PlayerHand lostHand = PlayerHand.None)
        {
            yield return new WaitForSeconds(settings.keyResolveDelay);

            PlayerData player = Players[playerIndex];

            if (isTrap)
            {
                // Hem 1. el kaybında hem 2. el kaybında/elenmede Kasap yanına gelip eli keser!
                yield return StartCoroutine(HandleButcherSequence(player, lostHand));
            }

            // Tur ve oyun sonu kontrolleri
            CheckGameProgress();
        }

        private int FindRandomLivingRival(int selfSeat)
        {
            List<int> candidates = new List<int>();
            for (int i = 0; i < Players.Count; i++)
            {
                if (i != selfSeat && Players[i].IsAlive) candidates.Add(i);
            }
            return candidates.Count > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Count)] : -1;
        }

        private void TriggerBotRivalReaction(int victimSeat)
        {
            var aiBrain = UnityEngine.Object.FindFirstObjectByType<FingerMonster.AI.AIBrain>();
            if (aiBrain == null || victimSeat < 0 || victimSeat >= playerSeats.Count) return;
            Vector3 victimPos = playerSeats[victimSeat].transform.position;

            for (int i = 0; i < Players.Count; i++)
            {
                if (i == victimSeat || !Players[i].IsAlive || !IsBotPlayer(i)) continue;
                if (i >= playerSeats.Count || playerSeats[i] == null || playerSeats[i].Avatar == null) continue;

                var avatar = playerSeats[i].Avatar;
                CharacterEmoteData data = UI.EmoteWheelUI.Instance != null ? UI.EmoteWheelUI.Instance.GetEmoteData(avatar.Archetype) : null;
                int usedSlot = aiBrain.TriggerBotReactionEmote(i, avatar, false, true, victimPos, data);
                if (usedSlot > 0)
                {
                    if (UI.EmoteWheelUI.Instance != null)
                    {
                        UI.EmoteWheelUI.Instance.ShowIncomingEmoteNotification(i, victimSeat, usedSlot, data);
                    }
                    break; // Aynı anda tek bir bot tepki versin, karmaşa olmasın
                }
            }
        }

        /// <summary>
        /// Yanlış tuşa basan oyuncunun yanına kasabın gelip elini kestiği sekans.
        /// </summary>
        private IEnumerator HandleButcherSequence(PlayerData targetPlayer, PlayerHand lostHand = PlayerHand.None)
        {
            ChangeState(GameState.ButcherSequence);
            EnsureButcherReference();
            butcher?.DestroyGhostPreview();
            Debug.Log($"[GameManager] KASAP SAHNESİ: {targetPlayer.PlayerName} için kasap geldi!");

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayButcherStinger();
            }

            bool localIsGhost = IsLocalHumanGhost();
            var refl = UnityEngine.Object.FindFirstObjectByType<ExecutionReflectionSystem>();
            if (refl != null)
            {
                refl.IsLocalViewerGhost = localIsGhost;
            }

            if (lightingController != null && !localIsGhost)
            {
                lightingController.SetButcherState();
            }

            // Kasabı oyuncunun koltuğunun yanına yürüt ve elini kes
            int idx = targetPlayer.PlayerId;
            if (butcher != null && idx >= 0 && idx < playerSeats.Count && playerSeats[idx] != null)
            {
                PlayerSeat seat = playerSeats[idx];
                Transform spawnAnchor = seat.ButcherSpawnPoint;
                int weaponIdx = (ChosenNextButcherWeapon >= 0 && ChosenNextButcherWeapon < 4)
                    ? ChosenNextButcherWeapon
                    : UnityEngine.Random.Range(0, 4);
                ChosenNextButcherWeapon = -1;
                butcher.AppearAt(spawnAnchor, idx, weaponIdx, () =>
                {
                    if (lostHand != PlayerHand.None && seat.Avatar != null)
                    {
                        seat.Avatar.SeverHand(lostHand);
                    }
                    if (!targetPlayer.IsAlive && seat.Avatar != null)
                    {
                        seat.Avatar.PlayEliminatedByWeapon(weaponIdx);
                    }
                    hudController?.RefreshAllCards(Players, ActivePlayerIndex);
                });
            }

            yield return new WaitForSeconds(settings.butcherSequenceDuration);

            if (butcher != null)
            {
                butcher.ResumePatrol();
            }

            if (lightingController != null)
            {
                lightingController.SetNormalState();
            }

            if (!targetPlayer.IsAlive && turnController.GetAlivePlayersCount(Players) > 1)
            {
                if (idx >= 0 && idx < playerSeats.Count && playerSeats[idx] != null)
                {
                    playerSeats[idx].Avatar?.SetGhostMode(true);
                }

                targetPlayer.InitializeGhostEnergy(settings.ghostStartSoulEnergy, settings.ghostMaxSoulEnergy);
                OnGhostSoulEnergyChanged?.Invoke(targetPlayer.PlayerId, targetPlayer.SoulEnergy);

                if (targetPlayer.PlayerId == GetLocalHumanSeatIndex() && !IsBotPlayer(targetPlayer.PlayerId))
                {
                    cameraController?.SetGhostSpectatorMode(true, targetPlayer.PlayerId, settings.ghostOrbitSpeed);
                    if (refl != null)
                    {
                        refl.IsLocalViewerGhost = true;
                    }
                }

                OnPlayerBecameGhost?.Invoke(targetPlayer.PlayerId);
            }
        }

        #region HAYALET YETENEKLERİ (GHOST ABILITIES)

        private bool HasAnyGhostPlayer()
        {
            if (Players == null) return false;
            for (int i = 0; i < Players.Count; i++)
            {
                if (!Players[i].IsAlive) return true;
            }
            return false;
        }

        private void OpenGhostWindow()
        {
            if (ghostWindowCoroutine != null)
            {
                StopCoroutine(ghostWindowCoroutine);
            }
            IsGhostWindowOpen = true;
            OnGhostWindowChanged?.Invoke(true, settings != null ? settings.ghostWindowDuration : 4.0f);
            ghostWindowCoroutine = StartCoroutine(GhostWindowTimerRoutine());
        }

        private void CloseGhostWindow()
        {
            if (ghostWindowCoroutine != null)
            {
                StopCoroutine(ghostWindowCoroutine);
                ghostWindowCoroutine = null;
            }
            if (!IsGhostWindowOpen) return;
            IsGhostWindowOpen = false;
            IsWaitingForGhostCurseTarget = false;
            OnGhostWindowChanged?.Invoke(false, 0f);
        }

        private IEnumerator GhostWindowTimerRoutine()
        {
            float duration = settings != null ? settings.ghostWindowDuration : 4.0f;
            yield return new WaitForSeconds(duration);
            ghostWindowCoroutine = null;
            CloseGhostWindow();
        }

        public void StartLocalGhostCurseSelection()
        {
            IsWaitingForGhostCurseTarget = true;
        }

        public void CancelLocalGhostCurseSelection()
        {
            IsWaitingForGhostCurseTarget = false;
        }

        public bool TryGhostCurseKey(int ghostPlayerIndex, int keyIndex)
        {
            if (Players == null || ghostPlayerIndex < 0 || ghostPlayerIndex >= Players.Count) return false;
            if (Players[ghostPlayerIndex].IsAlive) return false;
            if (!IsGhostWindowOpen || settings == null) return false;
            if (CursesUsedThisRound >= settings.maxCursesPerRound) return false;
            if (keyIndex < 0 || keyIndex >= settings.totalKeys) return false;
            if (IsKeyPressed(keyIndex) || XRayInspectedKeysThisRound.Contains(keyIndex)) return false;
            if (!Players[ghostPlayerIndex].TrySpendSoulEnergy(settings.ghostCurseCost)) return false;

            bool alreadyTrap = TrapIndices.Contains(keyIndex);
            if (!alreadyTrap)
            {
                List<int> trapCandidates = new List<int>();
                foreach (int t in TrapIndices)
                {
                    if (!PressedIndices.Contains(t)) trapCandidates.Add(t);
                }
                if (trapCandidates.Count == 0 && TrapIndices.Count > 0)
                {
                    trapCandidates.AddRange(TrapIndices);
                }
                if (trapCandidates.Count > 0)
                {
                    int oldTrap = trapCandidates[UnityEngine.Random.Range(0, trapCandidates.Count)];
                    TrapIndices.Remove(oldTrap);
                }
                TrapIndices.Add(keyIndex);
                curseCreatedNewTrap = true;
            }
            else
            {
                curseCreatedNewTrap = false;
            }

            CursedKeyIndex = keyIndex;
            CursesUsedThisRound++;
            IsWaitingForGhostCurseTarget = false;

            if (typewriter != null)
            {
                typewriter.GetKey(keyIndex)?.SetCursedVisual(true, IsLocalHumanGhost());
                typewriter.ShakeMachine(0.35f, 0.015f);
            }

            AudioManager.Instance?.PlayGhostCurse();
            OnGhostSoulEnergyChanged?.Invoke(ghostPlayerIndex, Players[ghostPlayerIndex].SoulEnergy);
            OnGhostCurseApplied?.Invoke(keyIndex);
            return true;
        }

        public bool TryGhostSelectWeapon(int ghostPlayerIndex, int weaponIndex)
        {
            if (Players == null || ghostPlayerIndex < 0 || ghostPlayerIndex >= Players.Count) return false;
            if (Players[ghostPlayerIndex].IsAlive) return false;
            if (weaponIndex < 0 || weaponIndex >= 4) return false;

            ChosenNextButcherWeapon = weaponIndex;

            if (IsLocalHumanGhost())
            {
                EnsureButcherReference();
                butcher?.SpawnOrUpdateGhostPreview(
                    (ExecutionWeaponType)weaponIndex,
                    GetGhostPreviewCornerPos(),
                    GetGhostPreviewCornerRot());
            }

            return true;
        }

        private void EnsureButcherReference()
        {
            if (butcher == null)
            {
                butcher = UnityEngine.Object.FindFirstObjectByType<ButcherPlaceholder>();
            }
        }

        private Vector3 GetGhostPreviewCornerPos()
        {
            Vector3 tableCenter = typewriter != null ? typewriter.transform.position : Vector3.zero;
            return new Vector3(tableCenter.x + 2.35f, 0f, tableCenter.z + 2.35f);
        }

        private Quaternion GetGhostPreviewCornerRot()
        {
            Vector3 cornerPos = GetGhostPreviewCornerPos();
            Vector3 tableCenter = typewriter != null ? typewriter.transform.position : Vector3.zero;
            Vector3 lookDir = tableCenter - cornerPos;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude < 0.0001f)
            {
                return Quaternion.identity;
            }
            return Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        }

        private void BindGhostHUDEvents()
        {
            GhostHUD hud = GhostHUD.Instance != null
                ? GhostHUD.Instance
                : UnityEngine.Object.FindFirstObjectByType<GhostHUD>(FindObjectsInactive.Include);
            if (hud == null || hud == boundGhostHud) return;

            UnbindGhostHUDEvents();
            boundGhostHud = hud;
            boundGhostHud.OnScareRequested += OnLocalGhostScareRequested;
            boundGhostHud.OnThrowRequested += OnLocalGhostThrowRequested;
        }

        private void UnbindGhostHUDEvents()
        {
            if (boundGhostHud == null) return;
            boundGhostHud.OnScareRequested -= OnLocalGhostScareRequested;
            boundGhostHud.OnThrowRequested -= OnLocalGhostThrowRequested;
            boundGhostHud = null;
        }

        private void OnLocalGhostScareRequested()
        {
            if (IsNetworkSessionActive)
            {
                FingerMonster.Networking.NetworkGameManager.Instance?.RequestGhostScareClient();
                return;
            }

            TryGhostScare(GetLocalHumanSeatIndex());
        }

        private void OnLocalGhostThrowRequested(ThrowableItemType item)
        {
            Transform camT = cameraController != null
                ? cameraController.transform
                : (Camera.main != null ? Camera.main.transform : null);
            Vector3 origin = camT != null ? camT.position + camT.forward * 0.45f : Vector3.up * 1.4f;
            Vector3 direction = camT != null ? camT.forward : Vector3.forward;

            if (IsNetworkSessionActive)
            {
                FingerMonster.Networking.NetworkGameManager.Instance?.RequestGhostThrowClient(item, origin, direction);
                return;
            }

            TryGhostThrow(GetLocalHumanSeatIndex(), item, origin, direction);
        }

        public bool TryGhostScare(int actorSeat)
        {
            if (Players == null || actorSeat < 0 || actorSeat >= Players.Count) return false;
            if (Players[actorSeat].IsAlive || settings == null) return false;

            settings.EnsureRuntimeDefaults();

            if (ghostLastScareTime.TryGetValue(actorSeat, out float lastScare) &&
                Time.time - lastScare < settings.ghostScareCooldown)
            {
                return false;
            }

            Vector3 ghostPos = Vector3.zero;
            int localSeat = GetLocalHumanSeatIndex();
            if (actorSeat == localSeat && cameraController != null && cameraController.IsGhostSpectatorMode)
            {
                ghostPos = cameraController.CurrentGhostWorldPosition;
            }
            else if (actorSeat < playerSeats.Count && playerSeats[actorSeat]?.Avatar != null)
            {
                ghostPos = playerSeats[actorSeat].Avatar.transform.position;
            }

            int bestTargetSeat = -1;
            PlayerVisualAvatar bestTargetAvatar = null;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < Players.Count && i < playerSeats.Count; i++)
            {
                if (!Players[i].IsAlive || playerSeats[i] == null) continue;

                if (scareTargetImmunityUntil.TryGetValue(i, out float immuneUntil) && Time.time < immuneUntil)
                {
                    continue;
                }

                Transform targetTransform = playerSeats[i].Avatar != null
                    ? playerSeats[i].Avatar.transform
                    : playerSeats[i].transform;

                if (!GhostScareAndThrowSystem.CanScareTarget(ghostPos, targetTransform, settings.ghostScareRange))
                {
                    continue;
                }

                float distSq = (targetTransform.position - ghostPos).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestTargetSeat = i;
                    bestTargetAvatar = playerSeats[i].Avatar;
                }
            }

            if (bestTargetSeat < 0) return false;
            if (!Players[actorSeat].TrySpendSoulEnergy(settings.ghostScareCost)) return false;

            ghostLastScareTime[actorSeat] = Time.time;
            scareTargetImmunityUntil[bestTargetSeat] = Time.time + settings.ghostScareTargetImmunity;
            OnGhostSoulEnergyChanged?.Invoke(actorSeat, Players[actorSeat].SoulEnergy);

            bool isLocalTarget = bestTargetSeat == localSeat && !IsBotPlayer(bestTargetSeat);
            GhostScareAndThrowSystem.Instance?.ExecuteScareVisuals(
                bestTargetSeat,
                ghostPos,
                bestTargetAvatar,
                cameraController,
                isLocalTarget);

            if (actorSeat == localSeat)
            {
                GhostHUD.Instance?.NotifyScareCooldownStarted();
            }

            return true;
        }

        public bool TryGhostThrow(int actorSeat, ThrowableItemType item, Vector3 origin, Vector3 direction)
        {
            if (Players == null || actorSeat < 0 || actorSeat >= Players.Count) return false;
            if (Players[actorSeat].IsAlive || settings == null) return false;

            settings.EnsureRuntimeDefaults();

            if (ghostLastThrowTime.TryGetValue(actorSeat, out float lastThrow) &&
                Time.time - lastThrow < settings.ghostThrowCooldown)
            {
                return false;
            }

            ThrowableItemData itemData = FindThrowableItemData(item);
            if (itemData == null) return false;
            if (!Players[actorSeat].TrySpendSoulEnergy(settings.ghostThrowCost)) return false;

            ghostLastThrowTime[actorSeat] = Time.time;
            OnGhostSoulEnergyChanged?.Invoke(actorSeat, Players[actorSeat].SoulEnergy);

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            float maxRange = settings.ghostThrowMaxRange;
            Vector3 endPos = origin + dir * maxRange;
            int hitSeat = -1;
            PlayerVisualAvatar hitAvatar = null;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxRange, ~0, QueryTriggerInteraction.Ignore))
            {
                endPos = hit.point;
                PlayerVisualAvatar avatar = hit.collider.GetComponentInParent<PlayerVisualAvatar>();
                int resolvedSeat = ResolveAvatarSeatIndex(avatar);
                if (resolvedSeat >= 0 && resolvedSeat < Players.Count && Players[resolvedSeat].IsAlive)
                {
                    hitSeat = resolvedSeat;
                    hitAvatar = avatar;
                }
            }

            int localSeat = GetLocalHumanSeatIndex();
            bool isLocalTarget = hitSeat >= 0 && hitSeat == localSeat && !IsBotPlayer(hitSeat);
            GhostScareAndThrowSystem.Instance?.LaunchThrowableVisual(
                itemData,
                origin,
                endPos,
                hitSeat,
                hitAvatar,
                isLocalTarget);

            if (actorSeat == localSeat)
            {
                GhostHUD.Instance?.NotifyThrowCooldownStarted();
            }

            return true;
        }

        private ThrowableItemData FindThrowableItemData(ThrowableItemType item)
        {
            if (settings == null || settings.throwableItems == null || settings.throwableItems.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < settings.throwableItems.Count; i++)
            {
                if (settings.throwableItems[i] != null && settings.throwableItems[i].itemType == item)
                {
                    return settings.throwableItems[i];
                }
            }

            return settings.throwableItems[0];
        }

        private int ResolveAvatarSeatIndex(PlayerVisualAvatar avatar)
        {
            if (avatar == null) return -1;
            for (int i = 0; i < playerSeats.Count; i++)
            {
                if (playerSeats[i] != null && playerSeats[i].Avatar == avatar)
                {
                    return i;
                }
            }
            return -1;
        }

        public void ClearGhostActiveGameplayState()
        {
            GhostScareAndThrowSystem.Instance?.ClearAllActiveEffects();
            EnsureButcherReference();
            butcher?.DestroyGhostPreview();
            ghostLastScareTime.Clear();
            ghostLastThrowTime.Clear();
            scareTargetImmunityUntil.Clear();
        }

        public bool TryGhostTriggerEffect(int ghostPlayerIndex, GhostEffectType effectType)
        {
            if (Players == null || ghostPlayerIndex < 0 || ghostPlayerIndex >= Players.Count) return false;
            if (Players[ghostPlayerIndex].IsAlive || settings == null) return false;
            if (Time.time - Players[ghostPlayerIndex].LastGhostEffectTime < settings.ghostEffectCooldown) return false;
            if (!Players[ghostPlayerIndex].TrySpendSoulEnergy(settings.ghostEffectCost)) return false;

            Players[ghostPlayerIndex].LastGhostEffectTime = Time.time;
            OnGhostSoulEnergyChanged?.Invoke(ghostPlayerIndex, Players[ghostPlayerIndex].SoulEnergy);
            ExecuteGhostEffectVisuals(effectType);
            OnGhostEffectTriggered?.Invoke(ghostPlayerIndex, effectType);
            return true;
        }

        private void ExecuteGhostEffectVisuals(GhostEffectType effectType)
        {
            if (!UI.SettingsMenuController.AreGhostEffectsEnabled()) return;

            AudioManager.Instance?.PlayGhostEffect(effectType);

            switch (effectType)
            {
                case GhostEffectType.FlickerLamp:
                    StartCoroutine(GhostFlickerBulbRoutine());
                    break;
                case GhostEffectType.FlyPaper:
                    typewriter?.Paper?.PlayPaperFlyAway();
                    break;
                case GhostEffectType.ColdBreeze:
                    typewriter?.ShakeMachine(0.25f, 0.008f);
                    break;
            }
        }

        private IEnumerator GhostFlickerBulbRoutine()
        {
            var bulb = UnityEngine.Object.FindFirstObjectByType<FlickeringBulb>();
            if (bulb == null) yield break;

            for (int i = 0; i < 3; i++)
            {
                bulb.SetMuted(true);
                yield return new WaitForSeconds(0.07f);
                bulb.SetMuted(false);
                yield return new WaitForSeconds(0.09f);
            }
        }

        #endregion

        /// <summary>
        /// Turun veya oyunun bitip bitmediğini denetler, bitmediyse sırayı sonraki oyuncuya aktarır.
        /// </summary>
        private void CheckGameProgress()
        {
            int aliveCount = turnController.GetAlivePlayersCount(Players);

            // Tek kişi kaldıysa oyun biter
            if (aliveCount <= 1)
            {
                EndGame();
                return;
            }

            // Kalan güvenli tuş kalmadıysa veya tüm tuzaklar tetiklendiyse tur biter
            if (RemainingSafeKeysCount <= 0 || PressedIndices.Count >= settings.totalKeys)
            {
                StartCoroutine(RoundEndRoutine());
                return;
            }

            // Sıradaki hayatta olan oyuncuya geç
            int nextPlayer = turnController.AdvanceToNextAlivePlayer(Players);
            if (nextPlayer != -1)
            {
                StartCoroutine(TransitionToNextTurn(nextPlayer));
            }
            else
            {
                EndGame();
            }
        }

        private IEnumerator TransitionToNextTurn(int nextPlayer)
        {
            yield return new WaitForSeconds(settings.turnTransitionDelay);
            BeginPlayerTurn(nextPlayer);
        }

        private IEnumerator RoundEndRoutine()
        {
            ChangeState(GameState.RoundEnd);
            Debug.Log("[GameManager] Tur sona erdi!");

            if (hudController != null)
            {
                hudController.ShowBanner("TUR TAMAMLANDI!\nYeni tura hazırlanılıyor...", Color.cyan, 2.5f);
            }

            yield return new WaitForSeconds(2.5f);

            CurrentRound++;
            StartRound();
        }

        /// <summary>
        /// Oyunu sonlandırır, kazananı belirler ve GameOver ekranını açar.
        /// </summary>
        public void EndGame()
        {
            ChangeState(GameState.GameOver);
            Debug.Log("[GameManager] Oyun Bitti!");

            PlayerData winner = null;
            int aliveCount = turnController.GetAlivePlayersCount(Players);

            if (aliveCount == 1)
            {
                winner = turnController.GetSoleSurvivor(Players);
            }
            else
            {
                // Birden fazla hayatta kalan varsa veya herkes elendiyse puan (skor) belirler
                winner = turnController.GetHighestScorePlayer(Players);
            }

            if (gameOverController == null)
            {
                gameOverController = UnityEngine.Object.FindFirstObjectByType<GameOverUIController>(FindObjectsInactive.Include);
            }

            if (gameOverController != null)
            {
                gameOverController.ShowGameOver(winner, Players);
            }
            else
            {
                Debug.LogWarning("[GameManager] GameOverUIController referansı bulunamadı!");
            }
        }

        public void TogglePause()
        {
            if (pauseMenuController != null)
            {
                pauseMenuController.TogglePause();
            }
        }

        public void ForceSetState(GameState newState)
        {
            ChangeState(newState);
        }

        private void ChangeState(GameState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }
    }
}

