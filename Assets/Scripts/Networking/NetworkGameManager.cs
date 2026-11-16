using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using FingerMonster.Core;
using FingerMonster.Gameplay;
using FingerMonster.Player;
using FingerMonster.UI;
using FingerMonster.Audio;
using FingerMonster.AI;
using FingerMonster.Data;

namespace FingerMonster.Networking
{
    /// <summary>
    /// FingerMonster LAN Multiplayer için yetkili sunucu (Server-Authoritative) oyun yöneticisi.
    /// Tuzakların yeri YALNIZCA sunucuda tutulur ve istemcilere asla sızdırılmaz.
    /// Tüm oyuncu eylemleri ServerRpc ile doğrulanır ve ClientRpc ile senkronize edilir.
    /// Bağlantısı kopan oyuncunun yerini yapay zekâ botu devralır.
    /// </summary>
    public class NetworkGameManager : NetworkBehaviour
    {
        public static NetworkGameManager Instance { get; private set; }

        [Header("Ağ Senkronize Değişkenleri")]
        public NetworkVariable<int> ActivePlayerIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> CurrentRound = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> RemainingSafeKeys = new NetworkVariable<int>(14, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<GameState> CurrentNetworkState = new NetworkVariable<GameState>(GameState.Lobby, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkList<NetworkPlayerSlot> PlayerSlots;

        [Header("Sunucu Özel Verileri (Hile Korumalı)")]
        private readonly HashSet<int> serverSecretTraps = new HashSet<int>();
        private readonly HashSet<int> serverPressedKeys = new HashSet<int>();
        private readonly Dictionary<ulong, int> clientToSeatMap = new Dictionary<ulong, int>();
        private readonly float[] serverLastEmoteTime = new float[4] { -10f, -10f, -10f, -10f };
        private int serverSafeCardIndex = -1;
        private bool serverCardSelectedThisTurn = false;
        private Coroutine serverBotCardCoroutine;

        private readonly List<AbilityCardType>[] serverPlayerHands = new List<AbilityCardType>[4]
        {
            new List<AbilityCardType>(2),
            new List<AbilityCardType>(2),
            new List<AbilityCardType>(2),
            new List<AbilityCardType>(2)
        };
        private readonly bool[] serverPlayerHasShield = new bool[4];
        private readonly bool[] serverPlayerIsFogged = new bool[4];
        private readonly bool[] serverUsedSkipLastTurn = new bool[4];
        private readonly bool[] serverUsedSkipThisTurn = new bool[4];
        private readonly bool[] serverUsedAbilityThisTurn = new bool[4];
        private bool serverTurnReversed = false;
        private GameSettings serverAbilitySettings;

        [Header("Sunucu Hayalet (Kasabın Yardımcısı) Verileri")]
        private static readonly Bounds GhostRoomBounds = new Bounds(
            new Vector3(0f, 2.20f, 0f),
            new Vector3(6.8f, 1.30f, 6.8f));
        private const float GhostScareCooldown = 6.0f;
        private const float GhostScareRange = 3.0f;
        private const float GhostScareImmunityDuration = 5.0f;
        private const float GhostThrowCooldown = 5.0f;
        private const float GhostThrowMaxRange = 12.0f;

        private readonly int[] serverSoulEnergy = new int[4];
        private readonly float[] serverLastGhostEffectTime = new float[4] { -10f, -10f, -10f, -10f };
        private readonly float[] serverLastGhostScareTime = new float[4] { -10f, -10f, -10f, -10f };
        private readonly float[] serverLastGhostThrowTime = new float[4] { -10f, -10f, -10f, -10f };
        private readonly float[] serverScareTargetImmunityUntil = new float[4];
        private readonly Vector3[] serverGhostPositions = new Vector3[4];
        private List<ThrowableItemData> cachedThrowableItems;
        private int serverCursedKeyIndex = -1;
        private bool serverCurseCreatedNewTrap = false;
        private int serverCursesUsedThisRound = 0;
        private int serverChosenWeaponIndex = -1;
        private readonly HashSet<int> serverXRayInspectedKeys = new HashSet<int>();
        private bool serverIsGhostWindowOpen = false;
        private Coroutine serverGhostWindowCoroutine;
        private Coroutine serverBotGhostCoroutine;

        [Header("Yerel İstemci Yetenek Kartı Önbelleği")]
        private readonly List<AbilityCardType> localAbilityHand = new List<AbilityCardType>(2);
        public IReadOnlyList<AbilityCardType> LocalAbilityHand => localAbilityHand;
        public bool IsWaitingForLocalXRayTarget { get; private set; } = false;
        private int pendingLocalXRaySlot = -1;
        public event Action<IReadOnlyList<AbilityCardType>> OnLocalAbilityHandUpdated;
        public event Action<int, AbilityCardType, int, string> OnNetworkAbilityCardPlayed;

        [Header("Yerel İstemci Hayalet API")]
        public bool IsLocalGhost => LocalSeatIndex >= 0 && LocalSeatIndex < PlayerSlots.Count && PlayerSlots[LocalSeatIndex].IsGhost;
        public int LocalSoulEnergy { get; private set; } = 0;
        public bool IsGhostWindowOpen { get; private set; } = false;
        public bool IsWaitingForGhostCurseTarget { get; private set; } = false;
        public int LocalCursedKeyIndex { get; private set; } = -1;
        public event Action<bool> OnLocalBecameGhost;
        public event Action<int> OnLocalSoulEnergyUpdated;
        public event Action<bool, float> OnGhostWindowUpdated;
        public event Action<int> OnGhostCurseConfirmedForGhosts;
        public event Action<GhostEffectType> OnNetworkGhostEffectTriggered;

        [Header("Yerel Sahne Referansları")]
        [SerializeField] private TypewriterMachine typewriter;
        [SerializeField] private HUDController hudController;
        [SerializeField] private CardSelectionController cardSelectionController;
        [SerializeField] private GameOverUIController gameOverController;
        [SerializeField] private RoomLightingController lightingController;
        [SerializeField] private ButcherPlaceholder butcher;
        [SerializeField] private List<PlayerSeat> playerSeats = new List<PlayerSeat>();
        [SerializeField] private GameObject[] characterPrefabs = new GameObject[4];
        [SerializeField] private AIBrain aiBrain;

        public int LocalSeatIndex { get; private set; } = 0;
        public bool IsLocalPlayerTurn => ActivePlayerIndex.Value == LocalSeatIndex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            PlayerSlots = new NetworkList<NetworkPlayerSlot>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

            if (serverAbilitySettings == null)
            {
                serverAbilitySettings = ScriptableObject.CreateInstance<GameSettings>();
            }

            EnsureLocalReferences();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            PlayerSlots.OnListChanged += HandlePlayerSlotsChanged;
            ActivePlayerIndex.OnValueChanged += HandleActivePlayerChanged;
            CurrentRound.OnValueChanged += HandleRoundChanged;
            RemainingSafeKeys.OnValueChanged += HandleSafeKeysChanged;
            PlayerInputHandler.OnKeyClickIntercept -= HandleKeyClickIntercept;
            PlayerInputHandler.OnKeyClickIntercept += HandleKeyClickIntercept;

            if (IsServer)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleServerClientDisconnect;
                if (PlayerSlots.Count == 0)
                {
                    if (NetworkRoomManager.Instance != null && NetworkRoomManager.Instance.PendingSlots != null && NetworkRoomManager.Instance.PendingSlots.Count > 0)
                    {
                        ServerInitializeMatch(NetworkRoomManager.Instance.PendingSlots, NetworkRoomManager.Instance.PendingTrapCount);
                    }
                    else
                    {
                        List<NetworkPlayerSlot> defaultSlots = new List<NetworkPlayerSlot>();
                        CharacterArchetype hostArch = GameSessionData.GetSeatArchetype(0);
                        var hostDef = CharacterDefinition.GetDefinition(hostArch);
                        defaultSlots.Add(new NetworkPlayerSlot(NetworkManager.Singleton.LocalClientId, 0, $"{hostDef.DisplayName} (Host - {hostDef.PersonalityTag})", false, hostDef.PrimaryColor, (int)hostArch));
                        for (int i = 1; i < 4; i++)
                        {
                            CharacterArchetype botArch = GameSessionData.GetSeatArchetype(i);
                            BotProfile b = BotProfile.ForCharacterSlot(i, botArch);
                            defaultSlots.Add(new NetworkPlayerSlot(ulong.MaxValue - (ulong)i, i, $"{b.BotName} [Bot]", true, b.AvatarColor, (int)botArch));
                        }
                        ServerInitializeMatch(defaultSlots, 6);
                    }
                }
            }

            DetermineLocalSeat();
            ConfigureLocalCameraForSeat(LocalSeatIndex);
            SyncSeatAvatars();
        }

        public override void OnNetworkDespawn()
        {
            if (PlayerSlots != null)
            {
                PlayerSlots.OnListChanged -= HandlePlayerSlotsChanged;
            }
            if (ActivePlayerIndex != null)
            {
                ActivePlayerIndex.OnValueChanged -= HandleActivePlayerChanged;
            }
            if (CurrentRound != null)
            {
                CurrentRound.OnValueChanged -= HandleRoundChanged;
            }
            if (RemainingSafeKeys != null)
            {
                RemainingSafeKeys.OnValueChanged -= HandleSafeKeysChanged;
            }

            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleServerClientDisconnect;
            }
            PlayerInputHandler.OnKeyClickIntercept -= HandleKeyClickIntercept;
            CancelLocalXRayKeyTarget();
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        private void EnsureLocalReferences()
        {
            if (typewriter == null) typewriter = UnityEngine.Object.FindFirstObjectByType<TypewriterMachine>();
            if (hudController == null) hudController = UnityEngine.Object.FindFirstObjectByType<HUDController>(FindObjectsInactive.Include);
            if (cardSelectionController == null) cardSelectionController = UnityEngine.Object.FindFirstObjectByType<CardSelectionController>(FindObjectsInactive.Include);
            if (gameOverController == null) gameOverController = UnityEngine.Object.FindFirstObjectByType<GameOverUIController>(FindObjectsInactive.Include);
            if (lightingController == null) lightingController = UnityEngine.Object.FindFirstObjectByType<RoomLightingController>();
            if (butcher == null) butcher = UnityEngine.Object.FindFirstObjectByType<ButcherPlaceholder>();
            if (aiBrain == null) aiBrain = GetComponent<AIBrain>() ?? gameObject.AddComponent<AIBrain>();

            if (playerSeats == null || playerSeats.Count == 0)
            {
                playerSeats = new List<PlayerSeat>(UnityEngine.Object.FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None));
                playerSeats.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
            }
        }

        /// <summary>
        /// İstemcinin kendi koltuğunu belirler ve kamerayı o koltuğun açısına kilitler.
        /// </summary>
        public void DetermineLocalSeat()
        {
            ulong myClientId = NetworkManager.Singleton.LocalClientId;
            LocalSeatIndex = 0;

            for (int i = 0; i < PlayerSlots.Count; i++)
            {
                if (PlayerSlots[i].ClientId == myClientId && !PlayerSlots[i].IsBot)
                {
                    LocalSeatIndex = PlayerSlots[i].SeatIndex;
                    break;
                }
            }

            ConfigureLocalCameraForSeat(LocalSeatIndex);
        }

        /// <summary>
        /// Her oyuncu kendi bilgisayarında masaya KENDİ koltuğundan bakar.
        /// </summary>
        public void ConfigureLocalCameraForSeat(int seatIndex)
        {
            PlayerCameraController cam = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>();
            if (cam == null) return;

            cam.SetLocalSeat(seatIndex, snapImmediately: true);
        }

        #region SERVER TARAFI OYUN AKIŞI
        /// <summary>
        /// Sunucu tarafından 4 kişilik odayı başlatır (İnsanlar + Boş koltuklara atanan Botlar).
        /// </summary>
        public void ServerInitializeMatch(List<NetworkPlayerSlot> initialSlots, int trapCount = 6)
        {
            if (!IsServer) return;

            PlayerSlots.Clear();
            clientToSeatMap.Clear();

            serverTurnReversed = false;
            serverCursedKeyIndex = -1;
            serverCurseCreatedNewTrap = false;
            serverCursesUsedThisRound = 0;
            serverChosenWeaponIndex = -1;
            serverXRayInspectedKeys.Clear();
            serverIsGhostWindowOpen = false;
            if (serverGhostWindowCoroutine != null)
            {
                StopCoroutine(serverGhostWindowCoroutine);
                serverGhostWindowCoroutine = null;
            }
            if (serverBotGhostCoroutine != null)
            {
                StopCoroutine(serverBotGhostCoroutine);
                serverBotGhostCoroutine = null;
            }
            for (int i = 0; i < 4; i++)
            {
                serverPlayerHands[i].Clear();
                serverPlayerHasShield[i] = false;
                serverPlayerIsFogged[i] = false;
                serverUsedSkipLastTurn[i] = false;
                serverUsedSkipThisTurn[i] = false;
                serverUsedAbilityThisTurn[i] = false;
                serverSoulEnergy[i] = 0;
                serverLastGhostEffectTime[i] = -10f;
                serverLastGhostScareTime[i] = -10f;
                serverLastGhostThrowTime[i] = -10f;
                serverScareTargetImmunityUntil[i] = 0f;
                serverGhostPositions[i] = Vector3.zero;
            }

            for (int i = 0; i < initialSlots.Count; i++)
            {
                PlayerSlots.Add(initialSlots[i]);
                if (initialSlots[i].IsBot && aiBrain != null)
                {
                    aiBrain.RegisterBotProfile(initialSlots[i].SeatIndex, BotProfile.ForCharacterSlot(initialSlots[i].SeatIndex, (CharacterArchetype)initialSlots[i].ArchetypeIndex));
                }
                if (!initialSlots[i].IsBot)
                {
                    clientToSeatMap[initialSlots[i].ClientId] = initialSlots[i].SeatIndex;
                }
            }

            ServerStartNewRound(trapCount);
        }

        public void ServerStartNewRound(int trapCount = 6)
        {
            if (!IsServer) return;

            serverSecretTraps.Clear();
            serverPressedKeys.Clear();
            serverTurnReversed = false;
            serverCursedKeyIndex = -1;
            serverCurseCreatedNewTrap = false;
            serverCursesUsedThisRound = 0;
            serverXRayInspectedKeys.Clear();
            serverIsGhostWindowOpen = false;
            if (serverGhostWindowCoroutine != null)
            {
                StopCoroutine(serverGhostWindowCoroutine);
                serverGhostWindowCoroutine = null;
            }
            if (serverBotGhostCoroutine != null)
            {
                StopCoroutine(serverBotGhostCoroutine);
                serverBotGhostCoroutine = null;
            }

            if (serverAbilitySettings == null)
            {
                serverAbilitySettings = ScriptableObject.CreateInstance<GameSettings>();
            }

            if (CurrentRound.Value <= 1)
            {
                for (int i = 0; i < 4; i++)
                {
                    serverPlayerHands[i].Clear();
                    serverPlayerHasShield[i] = false;
                    serverPlayerIsFogged[i] = false;
                    serverUsedSkipLastTurn[i] = false;
                    serverUsedSkipThisTurn[i] = false;
                    serverUsedAbilityThisTurn[i] = false;
                    serverSoulEnergy[i] = 0;
                    serverLastGhostEffectTime[i] = -10f;
                    serverLastGhostScareTime[i] = -10f;
                    serverLastGhostThrowTime[i] = -10f;
                    serverScareTargetImmunityUntil[i] = 0f;
                    serverGhostPositions[i] = Vector3.zero;
                }
            }

            // Hayatta olan her koltuğa tur başı 1 yetenek kartı ver (maksimum 2 kart)
            for (int i = 0; i < PlayerSlots.Count && i < 4; i++)
            {
                NetworkPlayerSlot slot = PlayerSlots[i];
                if (slot.IsAlive && serverPlayerHands[i].Count < 2)
                {
                    serverPlayerHands[i].Add(serverAbilitySettings.DrawWeightedRandomAbilityCard());
                }

                slot.AbilityCardCount = serverPlayerHands[i].Count;
                slot.HasActiveShield = serverPlayerHasShield[i];
                slot.IsFogged = serverPlayerIsFogged[i];
                PlayerSlots[i] = slot;

                ServerSyncHandToOwner(i);

                if (slot.IsGhost)
                {
                    serverSoulEnergy[i] = Mathf.Min(3, serverSoulEnergy[i] + 1);
                    if (!slot.IsBot)
                    {
                        SyncGhostSoulEnergyTargetClientRpc(serverSoulEnergy[i], CreateTargetParams(slot.ClientId));
                    }
                }
            }

            // 20 tuştan rastgele 6 tuzak seç (Yalnızca sunucu hafızasında kalır!)
            List<int> keysPool = new List<int>();
            for (int i = 0; i < 20; i++) keysPool.Add(i);

            for (int i = 0; i < keysPool.Count; i++)
            {
                int r = UnityEngine.Random.Range(i, keysPool.Count);
                int tmp = keysPool[i];
                keysPool[i] = keysPool[r];
                keysPool[r] = tmp;
            }

            int trapsToPlace = Mathf.Clamp(trapCount, 1, 19);
            for (int i = 0; i < trapsToPlace; i++)
            {
                serverSecretTraps.Add(keysPool[i]);
            }

            RemainingSafeKeys.Value = 20 - serverSecretTraps.Count;
            ActivePlayerIndex.Value = 0;
            CurrentNetworkState.Value = GameState.RoundStart;

            Debug.Log($"[NetworkGameManager-SERVER] Tur {CurrentRound.Value} Başladı! Gizli Tuzaklar: {string.Join(",", serverSecretTraps)}");

            OnRoundStartedClientRpc(CurrentRound.Value, RemainingSafeKeys.Value);
            StartCoroutine(ServerDelayToFirstTurn());
        }

        private IEnumerator ServerDelayToFirstTurn()
        {
            yield return new WaitForSeconds(1.5f);
            ServerBeginTurn(0);
        }

        private void ServerBeginTurn(int seatIndex)
        {
            if (!IsServer) return;

            if (seatIndex >= 0 && seatIndex < 4)
            {
                serverUsedAbilityThisTurn[seatIndex] = false;
                serverUsedSkipThisTurn[seatIndex] = false;
            }

            ActivePlayerIndex.Value = seatIndex;
            CurrentNetworkState.Value = GameState.CardSelection;
            serverSafeCardIndex = UnityEngine.Random.Range(0, 3);
            serverCardSelectedThisTurn = false;

            OnCardSelectionStartedClientRpc(seatIndex);

            if (serverBotCardCoroutine != null)
            {
                StopCoroutine(serverBotCardCoroutine);
                serverBotCardCoroutine = null;
            }

            if (seatIndex >= 0 && seatIndex < PlayerSlots.Count && PlayerSlots[seatIndex].IsBot && PlayerSlots[seatIndex].IsAlive)
            {
                serverBotCardCoroutine = StartCoroutine(ServerTriggerBotCardSelection(seatIndex));
            }

            bool hasAnyGhost = false;
            bool hasBotGhost = false;
            for (int i = 0; i < PlayerSlots.Count; i++)
            {
                if (PlayerSlots[i].IsGhost)
                {
                    hasAnyGhost = true;
                    if (PlayerSlots[i].IsBot) hasBotGhost = true;
                }
            }

            if (serverGhostWindowCoroutine != null)
            {
                StopCoroutine(serverGhostWindowCoroutine);
                serverGhostWindowCoroutine = null;
            }
            if (serverBotGhostCoroutine != null)
            {
                StopCoroutine(serverBotGhostCoroutine);
                serverBotGhostCoroutine = null;
            }

            if (hasAnyGhost)
            {
                serverIsGhostWindowOpen = true;
                OnGhostWindowChangedClientRpc(true, 4f);
                serverGhostWindowCoroutine = StartCoroutine(ServerCloseGhostWindowAfterDelay(4f));

                if (hasBotGhost)
                {
                    serverBotGhostCoroutine = StartCoroutine(ServerTriggerBotGhostActions());
                }
            }
            else
            {
                serverIsGhostWindowOpen = false;
            }
        }

        private IEnumerator ServerCloseGhostWindowAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            serverGhostWindowCoroutine = null;
            if (IsServer && serverIsGhostWindowOpen)
            {
                serverIsGhostWindowOpen = false;
                OnGhostWindowChangedClientRpc(false, 0f);
            }
        }

        private IEnumerator ServerTriggerBotGhostActions()
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(1.0f, 2.2f));
            serverBotGhostCoroutine = null;
            if (!IsServer || !serverIsGhostWindowOpen) yield break;

            EnsureLocalReferences();
            Vector3[] seatPositions = new Vector3[playerSeats.Count];
            for (int s = 0; s < playerSeats.Count; s++)
            {
                seatPositions[s] = playerSeats[s] != null ? playerSeats[s].transform.position : Vector3.zero;
            }

            List<int> aliveSeats = new List<int>(4);
            float[] immunityRemaining = new float[4];
            for (int s = 0; s < PlayerSlots.Count && s < 4; s++)
            {
                if (PlayerSlots[s].IsAlive)
                {
                    aliveSeats.Add(s);
                }
                immunityRemaining[s] = Mathf.Max(0f, serverScareTargetImmunityUntil[s] - Time.time);
            }

            Vector3 tableCenter = typewriter != null ? typewriter.transform.position : Vector3.zero;

            for (int i = 0; i < PlayerSlots.Count && i < 4; i++)
            {
                NetworkPlayerSlot slot = PlayerSlots[i];
                if (!slot.IsBot || !slot.IsGhost) continue;

                BotPersonality personality = aiBrain != null
                    ? aiBrain.GetProfile(i).Personality
                    : BotPersonality.Cesur;

                Vector3 roamTarget = AIBrain.ComputeBotGhostRoamTarget(
                    personality,
                    i,
                    seatPositions,
                    GhostRoomBounds,
                    Time.time);
                roamTarget = ClampToGhostRoomBounds(roamTarget);
                Vector3 lookDir = tableCenter - roamTarget;
                lookDir.y = 0f;
                float yaw = lookDir.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(lookDir.normalized, Vector3.up).eulerAngles.y
                    : 0f;
                serverGhostPositions[i] = roamTarget;
                BroadcastGhostTransformClientRpc(i, roamTarget, yaw);

                bool canScare = (Time.time - serverLastGhostScareTime[i]) >= GhostScareCooldown;
                bool canThrow = (Time.time - serverLastGhostThrowTime[i]) >= GhostThrowCooldown;

                BotGhostDecision decision = AIBrain.DecideBotGhostActiveAction(
                    personality,
                    serverSoulEnergy[i],
                    aliveSeats,
                    immunityRemaining,
                    canScare,
                    canThrow);

                switch (decision.Choice)
                {
                    case BotGhostActiveChoice.CurseKey:
                    {
                        if (serverSoulEnergy[i] >= 2 && serverCursesUsedThisRound < 1)
                        {
                            List<int> validKeys = new List<int>();
                            for (int k = 0; k < 20; k++)
                            {
                                if (!serverPressedKeys.Contains(k) && !serverXRayInspectedKeys.Contains(k))
                                {
                                    validKeys.Add(k);
                                }
                            }

                            if (validKeys.Count > 0)
                            {
                                int chosenKey = validKeys[UnityEngine.Random.Range(0, validKeys.Count)];
                                ServerExecuteGhostCurse(i, chosenKey);
                            }
                        }
                        break;
                    }

                    case BotGhostActiveChoice.SelectWeapon:
                    {
                        serverChosenWeaponIndex = Mathf.Clamp(decision.WeaponIndex, 0, 3);
                        ServerBroadcastGhostWeaponPreview(serverChosenWeaponIndex);
                        break;
                    }

                    case BotGhostActiveChoice.TriggerEffect:
                    {
                        if (serverSoulEnergy[i] >= 1 && (Time.time - serverLastGhostEffectTime[i] >= 3.0f))
                        {
                            serverLastGhostEffectTime[i] = Time.time;
                            serverSoulEnergy[i] -= 1;
                            OnGhostEffectExecutedClientRpc(i, (int)decision.EffectType);
                        }
                        break;
                    }

                    case BotGhostActiveChoice.ScareTarget:
                    {
                        if (ServerTryExecuteGhostScare(i))
                        {
                            for (int s = 0; s < PlayerSlots.Count && s < 4; s++)
                            {
                                immunityRemaining[s] = Mathf.Max(0f, serverScareTargetImmunityUntil[s] - Time.time);
                            }
                        }
                        break;
                    }

                    case BotGhostActiveChoice.ThrowItem:
                    {
                        int targetSeat = decision.TargetSeat;
                        Vector3 targetPos = (targetSeat >= 0 && targetSeat < playerSeats.Count && playerSeats[targetSeat] != null)
                            ? (playerSeats[targetSeat].Avatar != null ? playerSeats[targetSeat].Avatar.transform.position : playerSeats[targetSeat].transform.position) + Vector3.up * 1.1f
                            : tableCenter + Vector3.up * 0.9f;
                        Vector3 dir = targetPos - roamTarget;
                        ServerTryExecuteGhostThrow(i, decision.ThrowItem, roamTarget, dir);
                        break;
                    }
                }
            }
        }

        private void ServerActivateTypewriterTurn(int seatIndex)
        {
            if (!IsServer) return;

            ActivePlayerIndex.Value = seatIndex;
            CurrentNetworkState.Value = GameState.PlayerTurn;

            OnTurnStartedClientRpc(seatIndex);

            // Eğer bu koltuk bir BOT ise, sunucu AIBrain'i devreye sokar
            if (seatIndex >= 0 && seatIndex < PlayerSlots.Count && PlayerSlots[seatIndex].IsBot && PlayerSlots[seatIndex].IsAlive)
            {
                StartCoroutine(ServerTriggerBotTurn(seatIndex));
            }
        }

        private void ServerAdvanceToNextAlivePlayer(int currentSeatIndex)
        {
            if (!IsServer || PlayerSlots.Count == 0) return;

            int step = serverTurnReversed ? -1 : 1;
            int next = (currentSeatIndex + step + PlayerSlots.Count) % PlayerSlots.Count;
            int guard = 0;
            while (!PlayerSlots[next].IsAlive && guard < PlayerSlots.Count)
            {
                next = (next + step + PlayerSlots.Count) % PlayerSlots.Count;
                guard++;
            }

            ServerBeginTurn(next);
        }

        private IEnumerator ServerTriggerBotCardSelection(int seatIndex)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(1.0f, 1.5f));
            serverBotCardCoroutine = null;

            if (!IsServer || CurrentNetworkState.Value != GameState.CardSelection || ActivePlayerIndex.Value != seatIndex || serverCardSelectedThisTurn)
            {
                yield break;
            }

            int revealedMonsterForBot = -1;

            // Bot elindeki yetenek kartlarını adil kurallarla değerlendirir
            if (!serverUsedAbilityThisTurn[seatIndex] && serverPlayerHands[seatIndex].Count > 0)
            {
                int skipIdx = serverPlayerHands[seatIndex].IndexOf(AbilityCardType.SkipTurn);
                if (skipIdx >= 0 && !serverUsedSkipLastTurn[seatIndex] &&
                    (PlayerSlots[seatIndex].RemainingHands == 1 || UnityEngine.Random.value < 0.35f))
                {
                    if (ServerExecuteAbilityCard(seatIndex, skipIdx, -1, out _))
                    {
                        yield break;
                    }
                }

                int possessIdx = serverPlayerHands[seatIndex].IndexOf(AbilityCardType.Possess);
                if (!serverUsedAbilityThisTurn[seatIndex] && possessIdx >= 0 && !serverPlayerIsFogged[seatIndex])
                {
                    if (ServerExecuteAbilityCard(seatIndex, possessIdx, -1, out revealedMonsterForBot))
                    {
                        yield return new WaitForSeconds(0.6f);
                    }
                }

                if (!serverUsedAbilityThisTurn[seatIndex])
                {
                    int fogIdx = serverPlayerHands[seatIndex].IndexOf(AbilityCardType.Fog);
                    if (fogIdx >= 0 && UnityEngine.Random.value < 0.45f)
                    {
                        List<int> fogTargets = new List<int>();
                        for (int i = 0; i < PlayerSlots.Count; i++)
                        {
                            if (i != seatIndex && PlayerSlots[i].IsAlive && !serverPlayerIsFogged[i])
                            {
                                fogTargets.Add(i);
                            }
                        }
                        if (fogTargets.Count > 0)
                        {
                            int targetSeat = fogTargets[UnityEngine.Random.Range(0, fogTargets.Count)];
                            ServerExecuteAbilityCard(seatIndex, fogIdx, targetSeat, out _);
                            yield return new WaitForSeconds(0.4f);
                        }
                    }
                }

                if (!serverUsedAbilityThisTurn[seatIndex])
                {
                    int revIdx = serverPlayerHands[seatIndex].IndexOf(AbilityCardType.ReverseDirection);
                    if (revIdx >= 0 && UnityEngine.Random.value < 0.35f)
                    {
                        ServerExecuteAbilityCard(seatIndex, revIdx, -1, out _);
                        yield return new WaitForSeconds(0.4f);
                    }
                }
            }

            if (!IsServer || CurrentNetworkState.Value != GameState.CardSelection || ActivePlayerIndex.Value != seatIndex || serverCardSelectedThisTurn)
            {
                yield break;
            }

            List<int> cardChoices = new List<int>(3);
            for (int c = 0; c < 3; c++)
            {
                if (c != revealedMonsterForBot) cardChoices.Add(c);
            }

            int chosenCard = cardChoices[UnityEngine.Random.Range(0, cardChoices.Count)];
            ServerExecuteCardSelection(seatIndex, chosenCard);
        }

        private IEnumerator ServerTriggerBotTurn(int seatIndex)
        {
            yield return new WaitForSeconds(0.4f);

            List<int> available = new List<int>();
            for (int i = 0; i < 20; i++)
            {
                if (!serverPressedKeys.Contains(i)) available.Add(i);
            }

            if (available.Count == 0) yield break;

            // Bot adil karar verir (Tuzakları bilmez!)
            BotProfile profile = aiBrain != null ? aiBrain.GetProfile(seatIndex) : BotProfile.GenerateRandom(seatIndex);
            float thinkTime = UnityEngine.Random.Range(1.2f, 2.5f);
            yield return new WaitForSeconds(thinkTime);

            int chosenKey = available[UnityEngine.Random.Range(0, available.Count)];

            // Bot imleç simülasyonunu tüm istemcilere bildir
            OnBotAimingClientRpc(seatIndex, chosenKey);
            yield return new WaitForSeconds(0.45f);

            if (CurrentNetworkState.Value != GameState.PlayerTurn || ActivePlayerIndex.Value != seatIndex)
            {
                yield break;
            }

            ServerExecuteKeyPress(seatIndex, chosenKey);
        }
        #endregion

        #region RPC İLETİŞİMİ
        /// <summary>
        /// İstemci 3 karttan birini seçtiğinde sunucuya komut gönderir (Hile kontrolü yapılır).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestSelectCardServerRpc(int seatIndex, int cardIndex, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            if (CurrentNetworkState.Value != GameState.CardSelection || ActivePlayerIndex.Value != seatIndex || serverCardSelectedThisTurn)
            {
                return;
            }

            if (cardIndex < 0 || cardIndex >= 3)
            {
                return;
            }

            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (clientToSeatMap.TryGetValue(senderClientId, out int assignedSeat) && assignedSeat != seatIndex)
            {
                Debug.LogWarning($"[SERVER] Yetkisiz kart seçimi! Client:{senderClientId} Seat:{seatIndex}'e kart seçemez!");
                return;
            }

            ServerExecuteCardSelection(seatIndex, cardIndex);
        }

        private void ServerExecuteCardSelection(int seatIndex, int cardIndex)
        {
            serverCardSelectedThisTurn = true;
            bool isSafe = (cardIndex == serverSafeCardIndex);

            if (seatIndex >= 0 && seatIndex < 4)
            {
                serverPlayerIsFogged[seatIndex] = false;
                if (seatIndex < PlayerSlots.Count)
                {
                    NetworkPlayerSlot slot = PlayerSlots[seatIndex];
                    slot.IsFogged = false;

                    if (isSafe)
                    {
                        serverUsedSkipLastTurn[seatIndex] = false;
                        if (serverPlayerHands[seatIndex].Count < 2)
                        {
                            if (serverAbilitySettings == null)
                            {
                                serverAbilitySettings = ScriptableObject.CreateInstance<GameSettings>();
                            }
                            serverPlayerHands[seatIndex].Add(serverAbilitySettings.DrawWeightedRandomAbilityCard());
                            slot.AbilityCardCount = serverPlayerHands[seatIndex].Count;
                            PlayerSlots[seatIndex] = slot;
                            ServerSyncHandToOwner(seatIndex);
                        }
                        else
                        {
                            PlayerSlots[seatIndex] = slot;
                        }
                    }
                    else
                    {
                        PlayerSlots[seatIndex] = slot;
                    }
                }
            }

            OnCardRevealedClientRpc(seatIndex, cardIndex, serverSafeCardIndex, isSafe);
            StartCoroutine(ServerPostCardRevealSequence(seatIndex, isSafe));
        }

        private IEnumerator ServerPostCardRevealSequence(int seatIndex, bool isSafe)
        {
            yield return new WaitForSeconds(2.1f);

            if (isSafe)
            {
                ServerAdvanceToNextAlivePlayer(seatIndex);
            }
            else
            {
                ServerActivateTypewriterTurn(seatIndex);
            }
        }

        /// <summary>
        /// İstemci elindeki bir yetenek kartını oynamak istediğinde sunucuya komut gönderir.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestPlayAbilityCardServerRpc(int seatIndex, int handSlotIndex, int targetIndex, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (seatIndex < 0 || seatIndex >= PlayerSlots.Count || seatIndex >= 4) return;
            if (PlayerSlots[seatIndex].IsBot || !PlayerSlots[seatIndex].IsAlive) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (clientToSeatMap.TryGetValue(senderClientId, out int assignedSeat) && assignedSeat != seatIndex)
            {
                Debug.LogWarning($"[SERVER] Yetkisiz yetenek kartı! Client:{senderClientId} Seat:{seatIndex} adına oynayamaz!");
                return;
            }

            if (ActivePlayerIndex.Value != seatIndex)
            {
                return;
            }

            if (CurrentNetworkState.Value != GameState.CardSelection && CurrentNetworkState.Value != GameState.PlayerTurn)
            {
                return;
            }

            if (serverUsedAbilityThisTurn[seatIndex])
            {
                return;
            }

            if (handSlotIndex < 0 || handSlotIndex >= serverPlayerHands[seatIndex].Count)
            {
                return;
            }

            ServerExecuteAbilityCard(seatIndex, handSlotIndex, targetIndex, out _);
        }

        private bool ServerExecuteAbilityCard(int seatIndex, int handSlotIndex, int targetIndex, out int revealedMonsterIdx)
        {
            revealedMonsterIdx = -1;
            if (!IsServer || seatIndex < 0 || seatIndex >= PlayerSlots.Count || seatIndex >= 4) return false;
            if (serverUsedAbilityThisTurn[seatIndex]) return false;
            if (handSlotIndex < 0 || handSlotIndex >= serverPlayerHands[seatIndex].Count) return false;

            AbilityCardType cardType = serverPlayerHands[seatIndex][handSlotIndex];

            // Ön koşul doğrulamaları (kart tüketilmeden önce)
            switch (cardType)
            {
                case AbilityCardType.Possess:
                    if (CurrentNetworkState.Value != GameState.CardSelection || serverPlayerIsFogged[seatIndex])
                    {
                        return false;
                    }
                    break;

                case AbilityCardType.XRay:
                    if (targetIndex < 0)
                    {
                        List<int> availableKeys = new List<int>();
                        for (int k = 0; k < 20; k++)
                        {
                            if (!serverPressedKeys.Contains(k)) availableKeys.Add(k);
                        }
                        if (availableKeys.Count == 0) return false;
                        targetIndex = availableKeys[UnityEngine.Random.Range(0, availableKeys.Count)];
                    }
                    if (targetIndex < 0 || targetIndex >= 20 || serverPressedKeys.Contains(targetIndex))
                    {
                        return false;
                    }
                    break;

                case AbilityCardType.SkipTurn:
                    if (serverUsedSkipLastTurn[seatIndex])
                    {
                        return false;
                    }
                    break;

                case AbilityCardType.Fog:
                    if (targetIndex < 0 || targetIndex >= PlayerSlots.Count || targetIndex == seatIndex || !PlayerSlots[targetIndex].IsAlive)
                    {
                        return false;
                    }
                    break;
            }

            // Kart etkisini sunucuda uygula
            switch (cardType)
            {
                case AbilityCardType.Shield:
                    serverPlayerHasShield[seatIndex] = true;
                    break;

                case AbilityCardType.Possess:
                {
                    List<int> monsters = new List<int>(2);
                    for (int c = 0; c < 3; c++)
                    {
                        if (c != serverSafeCardIndex) monsters.Add(c);
                    }
                    revealedMonsterIdx = monsters[UnityEngine.Random.Range(0, monsters.Count)];
                    if (!PlayerSlots[seatIndex].IsBot)
                    {
                        OnPossessResultTargetClientRpc(revealedMonsterIdx, CreateTargetParams(PlayerSlots[seatIndex].ClientId));
                    }
                    break;
                }

                case AbilityCardType.XRay:
                {
                    serverXRayInspectedKeys.Add(targetIndex);
                    bool isTrap = serverSecretTraps.Contains(targetIndex);
                    if (!PlayerSlots[seatIndex].IsBot)
                    {
                        OnXRayResultTargetClientRpc(targetIndex, isTrap, CreateTargetParams(PlayerSlots[seatIndex].ClientId));
                    }
                    break;
                }

                case AbilityCardType.SkipTurn:
                    serverUsedSkipThisTurn[seatIndex] = true;
                    serverUsedSkipLastTurn[seatIndex] = true;
                    serverPlayerIsFogged[seatIndex] = false;
                    serverCardSelectedThisTurn = true;
                    if (serverBotCardCoroutine != null)
                    {
                        StopCoroutine(serverBotCardCoroutine);
                        serverBotCardCoroutine = null;
                    }
                    break;

                case AbilityCardType.ReverseDirection:
                    serverTurnReversed = !serverTurnReversed;
                    break;

                case AbilityCardType.Fog:
                {
                    serverPlayerIsFogged[targetIndex] = true;
                    NetworkPlayerSlot targetSlot = PlayerSlots[targetIndex];
                    targetSlot.IsFogged = true;
                    PlayerSlots[targetIndex] = targetSlot;
                    break;
                }
            }

            serverUsedAbilityThisTurn[seatIndex] = true;
            serverPlayerHands[seatIndex].RemoveAt(handSlotIndex);

            NetworkPlayerSlot actorSlot = PlayerSlots[seatIndex];
            actorSlot.AbilityCardCount = serverPlayerHands[seatIndex].Count;
            actorSlot.HasActiveShield = serverPlayerHasShield[seatIndex];
            actorSlot.IsFogged = serverPlayerIsFogged[seatIndex];
            PlayerSlots[seatIndex] = actorSlot;

            ServerSyncHandToOwner(seatIndex);

            CardData def = CardData.GetDefinition(cardType);
            string cardName = def != null ? def.displayName : cardType.ToString();
            string actorName = actorSlot.PlayerName.ToString();
            string announcement = cardType == AbilityCardType.Fog && targetIndex >= 0 && targetIndex < PlayerSlots.Count
                ? $"{actorName}, {PlayerSlots[targetIndex].PlayerName} oyuncusuna {cardName} oynadı!"
                : $"{actorName} [{cardName}] kartını oynadı!";

            int publicTarget = cardType == AbilityCardType.Possess ? -1 : targetIndex;
            OnAbilityCardPlayedClientRpc(seatIndex, (int)cardType, publicTarget, announcement);

            if (cardType == AbilityCardType.SkipTurn)
            {
                ServerAdvanceToNextAlivePlayer(seatIndex);
            }

            return true;
        }

        private ClientRpcParams CreateTargetParams(ulong clientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };
        }

        private ClientRpcParams CreateTargetParams(ulong[] clientIds)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = clientIds
                }
            };
        }

        private void ServerSyncHandToOwner(int seatIndex)
        {
            if (!IsServer || seatIndex < 0 || seatIndex >= PlayerSlots.Count || seatIndex >= 4) return;
            NetworkPlayerSlot slot = PlayerSlots[seatIndex];
            if (slot.IsBot) return;

            int count = serverPlayerHands[seatIndex].Count;
            int[] cardInts = new int[count];
            for (int i = 0; i < count; i++)
            {
                cardInts[i] = (int)serverPlayerHands[seatIndex][i];
            }

            SyncLocalHandTargetClientRpc(cardInts, CreateTargetParams(slot.ClientId));
        }

        /// <summary>
        /// İstemci tuşa bastığında sunucuya komut gönderir (Hile kontrolü yapılır).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestPressKeyServerRpc(int seatIndex, int keyIndex, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (seatIndex < 0 || seatIndex >= PlayerSlots.Count || !PlayerSlots[seatIndex].IsAlive) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;

            // Doğrulama 1: Oyun sırası bu oyuncuda mı?
            if (CurrentNetworkState.Value != GameState.PlayerTurn || ActivePlayerIndex.Value != seatIndex)
            {
                Debug.LogWarning($"[SERVER] Geçersiz sıra! Sıra:{ActivePlayerIndex.Value}, İsteyen:{seatIndex}");
                return;
            }

            // Doğrulama 2: Bu istemci bu koltuğun gerçek sahibi mi?
            if (clientToSeatMap.TryGetValue(senderClientId, out int assignedSeat))
            {
                if (assignedSeat != seatIndex)
                {
                    Debug.LogWarning($"[SERVER] Yetkisiz hamle! Client:{senderClientId} Seat:{seatIndex}'e basamaz!");
                    return;
                }
            }

            // Doğrulama 3: Tuş daha önce basılmış mı?
            if (serverPressedKeys.Contains(keyIndex))
            {
                Debug.LogWarning($"[SERVER] Tuş {keyIndex} zaten basılmış!");
                return;
            }

            ServerExecuteKeyPress(seatIndex, keyIndex);
        }

        private void ServerExecuteKeyPress(int seatIndex, int keyIndex)
        {
            if (serverIsGhostWindowOpen)
            {
                serverIsGhostWindowOpen = false;
                if (serverGhostWindowCoroutine != null)
                {
                    StopCoroutine(serverGhostWindowCoroutine);
                    serverGhostWindowCoroutine = null;
                }
                OnGhostWindowChangedClientRpc(false, 0f);
            }

            serverPressedKeys.Add(keyIndex);
            CurrentNetworkState.Value = GameState.KeyResolve;

            bool cursedTrapTriggered = (keyIndex == serverCursedKeyIndex && serverCurseCreatedNewTrap);
            if (keyIndex == serverCursedKeyIndex)
            {
                serverCursedKeyIndex = -1;
                serverCurseCreatedNewTrap = false;
            }
            if (cursedTrapTriggered)
            {
                OnGhostCurseExplodedClientRpc(keyIndex);
            }

            if (seatIndex >= 0 && seatIndex < 4)
            {
                serverUsedSkipLastTurn[seatIndex] = serverUsedSkipThisTurn[seatIndex];
                serverUsedSkipThisTurn[seatIndex] = false;
            }

            bool isTrap = serverSecretTraps.Contains(keyIndex);
            NetworkPlayerSlot slot = PlayerSlots[seatIndex];
            PlayerHand lostHand = PlayerHand.None;

            if (isTrap)
            {
                // Kalkan koruması kontrolü (aktif veya elde pasif)
                if (seatIndex >= 0 && seatIndex < 4 &&
                    (serverPlayerHasShield[seatIndex] || serverPlayerHands[seatIndex].Contains(AbilityCardType.Shield)))
                {
                    if (serverPlayerHasShield[seatIndex])
                    {
                        serverPlayerHasShield[seatIndex] = false;
                    }
                    else
                    {
                        serverPlayerHands[seatIndex].Remove(AbilityCardType.Shield);
                    }

                    slot.HasActiveShield = serverPlayerHasShield[seatIndex];
                    slot.AbilityCardCount = serverPlayerHands[seatIndex].Count;
                    PlayerSlots[seatIndex] = slot;
                    ServerSyncHandToOwner(seatIndex);

                    OnShieldBlockedTrapClientRpc(seatIndex, keyIndex);
                    StartCoroutine(ServerPostKeySequence(seatIndex, false, PlayerHand.None, slot.IsAlive));
                    return;
                }

                // Sağlam ellerden birini seçerek kopar (aynı eli iki kez kesme)
                if (slot.LeftHandIntact && slot.RightHandIntact)
                {
                    lostHand = (UnityEngine.Random.value > 0.5f) ? PlayerHand.Left : PlayerHand.Right;
                }
                else if (slot.LeftHandIntact)
                {
                    lostHand = PlayerHand.Left;
                }
                else if (slot.RightHandIntact)
                {
                    lostHand = PlayerHand.Right;
                }

                if (lostHand == PlayerHand.Left) slot.LeftHandIntact = false;
                else if (lostHand == PlayerHand.Right) slot.RightHandIntact = false;

                slot.RemainingHands = (slot.LeftHandIntact ? 1 : 0) + (slot.RightHandIntact ? 1 : 0);
                if (slot.RemainingHands == 0)
                {
                    slot.IsAlive = false;
                    if (seatIndex >= 0 && seatIndex < 4)
                    {
                        serverPlayerHands[seatIndex].Clear();
                        serverPlayerHasShield[seatIndex] = false;
                        serverPlayerIsFogged[seatIndex] = false;
                        slot.AbilityCardCount = 0;
                        slot.HasActiveShield = false;
                        slot.IsFogged = false;
                        ServerSyncHandToOwner(seatIndex);
                    }

                    int aliveCountAfterElimination = 0;
                    for (int i = 0; i < PlayerSlots.Count; i++)
                    {
                        if (i == seatIndex) continue;
                        if (PlayerSlots[i].IsAlive) aliveCountAfterElimination++;
                    }

                    if (aliveCountAfterElimination > 1)
                    {
                        slot.IsGhost = true;
                        if (seatIndex >= 0 && seatIndex < 4)
                        {
                            serverSoulEnergy[seatIndex] = 1;
                        }
                        if (!slot.IsBot)
                        {
                            SyncGhostSoulEnergyTargetClientRpc(1, CreateTargetParams(slot.ClientId));
                        }
                    }
                }
            }
            else
            {
                slot.Score += 10;
                RemainingSafeKeys.Value = Mathf.Max(0, RemainingSafeKeys.Value - 1);
            }

            PlayerSlots[seatIndex] = slot;

            // Sonucu tüm istemcilere yay
            OnKeyResolvedClientRpc(seatIndex, keyIndex, isTrap, lostHand, slot.Score, slot.RemainingHands, slot.IsAlive);

            ServerTriggerBotEmoteReaction(seatIndex, isTrap);

            StartCoroutine(ServerPostKeySequence(seatIndex, isTrap, lostHand, slot.IsAlive));
        }

        private void ServerTriggerBotEmoteReaction(int actorSeat, bool isTrap)
        {
            if (!IsServer || aiBrain == null) return;

            if (isTrap)
            {
                for (int i = 0; i < PlayerSlots.Count; i++)
                {
                    var s = PlayerSlots[i];
                    if (s.SeatIndex == actorSeat || !s.IsAlive || !s.IsBot) continue;
                    var prof = aiBrain.GetProfile(s.SeatIndex);
                    if (UnityEngine.Random.value <= aiBrain.GetEmoteChanceForDifficulty(prof.Difficulty))
                    {
                        int chosenSlot = aiBrain.PickReactionSlot(prof.Personality, true);
                        OnEmoteSlotExecutedClientRpc(s.SeatIndex, chosenSlot, actorSeat);
                        break;
                    }
                }
            }
            else if (actorSeat >= 0 && actorSeat < PlayerSlots.Count && PlayerSlots[actorSeat].IsBot && PlayerSlots[actorSeat].IsAlive)
            {
                var prof = aiBrain.GetProfile(actorSeat);
                if (UnityEngine.Random.value <= aiBrain.GetEmoteChanceForDifficulty(prof.Difficulty))
                {
                    int chosenSlot = aiBrain.PickReactionSlot(prof.Personality, false);
                    int target = -1;
                    for (int i = 0; i < PlayerSlots.Count; i++)
                    {
                        if (PlayerSlots[i].SeatIndex != actorSeat && PlayerSlots[i].IsAlive)
                        {
                            target = PlayerSlots[i].SeatIndex;
                            break;
                        }
                    }
                    OnEmoteSlotExecutedClientRpc(actorSeat, chosenSlot, target);
                }
            }
        }

        private IEnumerator ServerPostKeySequence(int seatIndex, bool isTrap, PlayerHand lostHand, bool isAlive)
        {
            yield return new WaitForSeconds(1.2f);

            // Her yanlış tuşta (hem 1. el hem 2. el kaybında) Kasap Sekansı
            if (isTrap)
            {
                CurrentNetworkState.Value = GameState.ButcherSequence;
                int weaponIdx = (serverChosenWeaponIndex >= 0 && serverChosenWeaponIndex < 4)
                    ? serverChosenWeaponIndex
                    : UnityEngine.Random.Range(0, 4);
                serverChosenWeaponIndex = -1;
                DestroyGhostPreviewClientRpc();
                OnButcherSequenceClientRpc(seatIndex, weaponIdx, lostHand, !isAlive);
                yield return new WaitForSeconds(2.2f);
            }

            // Tur veya Oyun Bitiş Kontrolü
            int aliveCount = 0;
            int soleSurvivor = -1;
            for (int i = 0; i < PlayerSlots.Count; i++)
            {
                if (PlayerSlots[i].IsAlive)
                {
                    aliveCount++;
                    soleSurvivor = i;
                }
            }

            // Oyun bitti: Tek kişi kaldı
            if (aliveCount <= 1)
            {
                ServerEndGame(soleSurvivor);
                yield break;
            }

            // Tur bitti: Güvenli tuş kalmadı
            if (RemainingSafeKeys.Value <= 0 || serverPressedKeys.Count >= 20)
            {
                CurrentNetworkState.Value = GameState.RoundEnd;
                OnRoundEndedClientRpc();
                yield return new WaitForSeconds(2.5f);
                CurrentRound.Value++;
                ServerStartNewRound();
                yield break;
            }

            // Sıradaki hayatta olan oyuncuya geç
            ServerAdvanceToNextAlivePlayer(seatIndex);
        }

        private void ServerEndGame(int winnerSeatIndex)
        {
            CurrentNetworkState.Value = GameState.GameOver;

            string winnerName = "Herkes Elendi (Berabere)";
            Color winnerCol = Color.gray;
            int winnerScore = 0;
            int winnerArchetype = 0;
            int winnerHands = 0;

            if (winnerSeatIndex >= 0 && winnerSeatIndex < PlayerSlots.Count)
            {
                winnerName = PlayerSlots[winnerSeatIndex].PlayerName.ToString();
                winnerCol = PlayerSlots[winnerSeatIndex].AvatarColor;
                winnerScore = PlayerSlots[winnerSeatIndex].Score;
                winnerArchetype = PlayerSlots[winnerSeatIndex].ArchetypeIndex;
                winnerHands = PlayerSlots[winnerSeatIndex].RemainingHands;
            }

            string recap = "--- LAN OYUN SKOR TABLOSU ---\n";
            for (int i = 0; i < PlayerSlots.Count; i++)
            {
                var s = PlayerSlots[i];
                var def = CharacterDefinition.GetDefinition((CharacterArchetype)(s.ArchetypeIndex % 4));
                string st = s.IsAlive ? $"({s.RemainingHands} El)" : "(Kasap Tarafından Elendi)";
                recap += $"{s.PlayerName} [{def.PersonalityTag}]: {s.Score} Puan {st}\n";
            }

            OnGameOverClientRpc(winnerName, winnerCol, winnerScore, winnerArchetype, winnerHands, recap);
        }

        private void HandleServerClientDisconnect(ulong clientId)
        {
            if (!IsServer) return;

            if (clientToSeatMap.TryGetValue(clientId, out int seatIndex))
            {
                clientToSeatMap.Remove(clientId);

                // Boşalan koltuğu anında Yapay Zekâ Botu devralsın
                if (seatIndex < PlayerSlots.Count)
                {
                    NetworkPlayerSlot slot = PlayerSlots[seatIndex];
                    slot.IsBot = true;
                    slot.PlayerName = new FixedString64Bytes($"Bot {seatIndex + 1} (Devraldı)");
                    PlayerSlots[seatIndex] = slot;

                    Debug.Log($"[SERVER] Client {clientId} ayrıldı! Koltuk {seatIndex + 1} bot tarafından devralındı.");
                    if (slot.IsAlive)
                    {
                        OnSeatTakenOverClientRpc(seatIndex, slot.PlayerName.ToString());
                    }

                    // Sıra bu oyuncudaydı ise bot hamlesini tetikle
                    if (ActivePlayerIndex.Value == seatIndex)
                    {
                        if (CurrentNetworkState.Value == GameState.CardSelection && !serverCardSelectedThisTurn)
                        {
                            if (serverBotCardCoroutine != null)
                            {
                                StopCoroutine(serverBotCardCoroutine);
                                serverBotCardCoroutine = null;
                            }
                            serverBotCardCoroutine = StartCoroutine(ServerTriggerBotCardSelection(seatIndex));
                        }
                        else if (CurrentNetworkState.Value == GameState.PlayerTurn)
                        {
                            StartCoroutine(ServerTriggerBotTurn(seatIndex));
                        }
                    }
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestGhostCurseKeyServerRpc(int seatIndex, int keyIndex, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (seatIndex < 0 || seatIndex >= PlayerSlots.Count || seatIndex >= 4) return;
            if (!serverIsGhostWindowOpen || serverCursesUsedThisRound >= 1) return;
            if (keyIndex < 0 || keyIndex >= 20 || serverPressedKeys.Contains(keyIndex) || serverXRayInspectedKeys.Contains(keyIndex)) return;

            NetworkPlayerSlot slot = PlayerSlots[seatIndex];
            if (!slot.IsGhost || slot.IsBot) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (!clientToSeatMap.TryGetValue(senderClientId, out int assignedSeat) || assignedSeat != seatIndex)
            {
                return;
            }

            if (serverSoulEnergy[seatIndex] < 2) return;

            ServerExecuteGhostCurse(seatIndex, keyIndex);
        }

        private void ServerExecuteGhostCurse(int seatIndex, int keyIndex)
        {
            if (!IsServer || seatIndex < 0 || seatIndex >= PlayerSlots.Count || seatIndex >= 4) return;

            if (!serverSecretTraps.Contains(keyIndex))
            {
                List<int> swappableTraps = new List<int>();
                foreach (int t in serverSecretTraps)
                {
                    if (!serverPressedKeys.Contains(t)) swappableTraps.Add(t);
                }

                if (swappableTraps.Count == 0)
                {
                    foreach (int t in serverSecretTraps) swappableTraps.Add(t);
                }

                if (swappableTraps.Count > 0)
                {
                    int oldTrap = swappableTraps[UnityEngine.Random.Range(0, swappableTraps.Count)];
                    serverSecretTraps.Remove(oldTrap);
                    serverSecretTraps.Add(keyIndex);
                    serverCurseCreatedNewTrap = true;
                }
                else
                {
                    serverCurseCreatedNewTrap = false;
                }
            }
            else
            {
                serverCurseCreatedNewTrap = false;
            }

            serverCursedKeyIndex = keyIndex;
            serverCursesUsedThisRound++;
            serverSoulEnergy[seatIndex] -= 2;

            NetworkPlayerSlot actorSlot = PlayerSlots[seatIndex];
            if (!actorSlot.IsBot)
            {
                SyncGhostSoulEnergyTargetClientRpc(serverSoulEnergy[seatIndex], CreateTargetParams(actorSlot.ClientId));
            }

            List<ulong> ghostClientIds = new List<ulong>();
            for (int i = 0; i < PlayerSlots.Count; i++)
            {
                if (PlayerSlots[i].IsGhost && !PlayerSlots[i].IsBot)
                {
                    ghostClientIds.Add(PlayerSlots[i].ClientId);
                }
            }

            if (ghostClientIds.Count > 0)
            {
                OnCursedKeySyncedToGhostsClientRpc(keyIndex, CreateTargetParams(ghostClientIds.ToArray()));
            }

            OnMachineCurseShakeClientRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestGhostSelectWeaponServerRpc(int seatIndex, int weaponIndex, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (seatIndex < 0 || seatIndex >= PlayerSlots.Count || seatIndex >= 4) return;
            if (!serverIsGhostWindowOpen) return;
            if (weaponIndex < 0 || weaponIndex >= 4) return;

            NetworkPlayerSlot slot = PlayerSlots[seatIndex];
            if (!slot.IsGhost || slot.IsBot) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (!clientToSeatMap.TryGetValue(senderClientId, out int assignedSeat) || assignedSeat != seatIndex)
            {
                return;
            }

            serverChosenWeaponIndex = weaponIndex;
            ServerBroadcastGhostWeaponPreview(weaponIndex);
        }

        private void ServerBroadcastGhostWeaponPreview(int weaponIndex)
        {
            if (!IsServer) return;

            List<ulong> ghostClientIds = new List<ulong>();
            for (int i = 0; i < PlayerSlots.Count; i++)
            {
                if (PlayerSlots[i].IsGhost && !PlayerSlots[i].IsBot)
                {
                    ghostClientIds.Add(PlayerSlots[i].ClientId);
                }
            }

            if (ghostClientIds.Count > 0)
            {
                OnGhostWeaponPreviewSyncedClientRpc(weaponIndex, CreateTargetParams(ghostClientIds.ToArray()));
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitGhostTransformServerRpc(Vector3 pos, float yaw, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (!clientToSeatMap.TryGetValue(senderClientId, out int seatIndex)) return;
            if (seatIndex < 0 || seatIndex >= PlayerSlots.Count || seatIndex >= 4) return;

            NetworkPlayerSlot slot = PlayerSlots[seatIndex];
            if (!slot.IsGhost || slot.IsBot) return;

            Vector3 clampedPos = ClampToGhostRoomBounds(pos);
            serverGhostPositions[seatIndex] = clampedPos;
            BroadcastGhostTransformClientRpc(seatIndex, clampedPos, yaw);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestGhostScareServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (!clientToSeatMap.TryGetValue(senderClientId, out int actorSeat)) return;
            if (actorSeat < 0 || actorSeat >= PlayerSlots.Count || actorSeat >= 4) return;

            NetworkPlayerSlot slot = PlayerSlots[actorSeat];
            if (!slot.IsGhost || slot.IsBot) return;

            ServerTryExecuteGhostScare(actorSeat);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestGhostThrowServerRpc(int itemTypeIndex, Vector3 origin, Vector3 direction, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (!Enum.IsDefined(typeof(ThrowableItemType), itemTypeIndex)) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (!clientToSeatMap.TryGetValue(senderClientId, out int actorSeat)) return;
            if (actorSeat < 0 || actorSeat >= PlayerSlots.Count || actorSeat >= 4) return;

            NetworkPlayerSlot slot = PlayerSlots[actorSeat];
            if (!slot.IsGhost || slot.IsBot) return;

            ServerTryExecuteGhostThrow(actorSeat, (ThrowableItemType)itemTypeIndex, origin, direction);
        }

        private bool ServerTryExecuteGhostScare(int actorSeat)
        {
            if (!IsServer || actorSeat < 0 || actorSeat >= PlayerSlots.Count || actorSeat >= 4) return false;
            if (!PlayerSlots[actorSeat].IsGhost) return false;
            if (serverSoulEnergy[actorSeat] < 1) return false;
            if (Time.time - serverLastGhostScareTime[actorSeat] < GhostScareCooldown) return false;

            EnsureLocalReferences();

            Vector3 ghostPos = serverGhostPositions[actorSeat];
            if (ghostPos.sqrMagnitude < 0.0001f)
            {
                if (actorSeat < playerSeats.Count && playerSeats[actorSeat]?.Avatar != null)
                {
                    ghostPos = playerSeats[actorSeat].Avatar.transform.position;
                }
                else
                {
                    ghostPos = GhostRoomBounds.center;
                }
            }

            int bestTargetSeat = -1;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < PlayerSlots.Count && i < playerSeats.Count && i < 4; i++)
            {
                if (!PlayerSlots[i].IsAlive || playerSeats[i] == null) continue;
                if (Time.time < serverScareTargetImmunityUntil[i]) continue;

                Transform targetTransform = playerSeats[i].Avatar != null
                    ? playerSeats[i].Avatar.transform
                    : playerSeats[i].transform;

                if (!GhostScareAndThrowSystem.CanScareTarget(ghostPos, targetTransform, GhostScareRange))
                {
                    continue;
                }

                float distSq = (targetTransform.position - ghostPos).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestTargetSeat = i;
                }
            }

            if (bestTargetSeat < 0) return false;

            serverSoulEnergy[actorSeat] -= 1;
            serverLastGhostScareTime[actorSeat] = Time.time;
            serverScareTargetImmunityUntil[bestTargetSeat] = Time.time + GhostScareImmunityDuration;

            NetworkPlayerSlot actorSlot = PlayerSlots[actorSeat];
            if (!actorSlot.IsBot)
            {
                OnSoulEnergySyncedClientRpc(serverSoulEnergy[actorSeat], CreateTargetParams(actorSlot.ClientId));
            }

            OnGhostScareExecutedClientRpc(actorSeat, bestTargetSeat, ghostPos);
            return true;
        }

        private bool ServerTryExecuteGhostThrow(int actorSeat, ThrowableItemType itemType, Vector3 origin, Vector3 direction)
        {
            if (!IsServer || actorSeat < 0 || actorSeat >= PlayerSlots.Count || actorSeat >= 4) return false;
            if (!PlayerSlots[actorSeat].IsGhost) return false;
            if (serverSoulEnergy[actorSeat] < 1) return false;
            if (Time.time - serverLastGhostThrowTime[actorSeat] < GhostThrowCooldown) return false;

            EnsureLocalReferences();

            Vector3 startPos = ClampToGhostRoomBounds(origin);
            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            Vector3 endPos = startPos + dir * GhostThrowMaxRange;
            int hitSeat = -1;

            if (Physics.Raycast(startPos, dir, out RaycastHit hit, GhostThrowMaxRange, ~0, QueryTriggerInteraction.Ignore))
            {
                endPos = hit.point;
                PlayerVisualAvatar avatar = hit.collider.GetComponentInParent<PlayerVisualAvatar>();
                if (avatar != null)
                {
                    for (int i = 0; i < playerSeats.Count && i < PlayerSlots.Count; i++)
                    {
                        if (playerSeats[i] != null && playerSeats[i].Avatar == avatar && PlayerSlots[i].IsAlive)
                        {
                            hitSeat = i;
                            break;
                        }
                    }
                }
            }

            serverSoulEnergy[actorSeat] -= 1;
            serverLastGhostThrowTime[actorSeat] = Time.time;

            NetworkPlayerSlot actorSlot = PlayerSlots[actorSeat];
            if (!actorSlot.IsBot)
            {
                OnSoulEnergySyncedClientRpc(serverSoulEnergy[actorSeat], CreateTargetParams(actorSlot.ClientId));
            }

            OnGhostThrowExecutedClientRpc(actorSeat, (int)itemType, startPos, endPos, hitSeat);
            return true;
        }

        private static Vector3 ClampToGhostRoomBounds(Vector3 pos)
        {
            Vector3 min = GhostRoomBounds.min;
            Vector3 max = GhostRoomBounds.max;
            return new Vector3(
                Mathf.Clamp(pos.x, min.x, max.x),
                Mathf.Clamp(pos.y, min.y, max.y),
                Mathf.Clamp(pos.z, min.z, max.z));
        }

        private ThrowableItemData ResolveThrowableItemData(ThrowableItemType itemType)
        {
            List<ThrowableItemData> items = GameManager.Instance != null &&
                                            GameManager.Instance.Settings != null &&
                                            GameManager.Instance.Settings.throwableItems != null &&
                                            GameManager.Instance.Settings.throwableItems.Count > 0
                ? GameManager.Instance.Settings.throwableItems
                : null;

            if (items == null)
            {
                if (cachedThrowableItems == null || cachedThrowableItems.Count == 0)
                {
                    cachedThrowableItems = ThrowableItemData.CreateRuntimeDefaultList();
                }
                items = cachedThrowableItems;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].itemType == itemType)
                {
                    return items[i];
                }
            }

            return items.Count > 0 ? items[0] : null;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestGhostEffectServerRpc(int seatIndex, int effectType, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (seatIndex < 0 || seatIndex >= PlayerSlots.Count || seatIndex >= 4) return;
            if (!Enum.IsDefined(typeof(GhostEffectType), effectType)) return;

            NetworkPlayerSlot slot = PlayerSlots[seatIndex];
            if (!slot.IsGhost || slot.IsBot) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (!clientToSeatMap.TryGetValue(senderClientId, out int assignedSeat) || assignedSeat != seatIndex)
            {
                return;
            }

            if (Time.time - serverLastGhostEffectTime[seatIndex] < 3.0f) return;
            if (serverSoulEnergy[seatIndex] < 1) return;

            serverLastGhostEffectTime[seatIndex] = Time.time;
            serverSoulEnergy[seatIndex] -= 1;

            SyncGhostSoulEnergyTargetClientRpc(serverSoulEnergy[seatIndex], CreateTargetParams(slot.ClientId));
            OnGhostEffectExecutedClientRpc(seatIndex, effectType);
        }
        #endregion

        #region CLIENT RPC METOTLARI (Tüm Oyuncular Görür)
        [ClientRpc]
        private void OnRoundStartedClientRpc(int round, int safeKeys)
        {
            EnsureLocalReferences();
            CancelLocalXRayKeyTarget();
            IsWaitingForGhostCurseTarget = false;
            IsGhostWindowOpen = false;
            LocalCursedKeyIndex = -1;
            if (cardSelectionController != null)
            {
                cardSelectionController.SetFoggedState(false);
                cardSelectionController.HideImmediate();
            }

            SyncSeatAvatars();
            if (round <= 1)
            {
                LocalSoulEnergy = 0;
                GameManager.Instance?.ClearGhostActiveGameplayState();
                butcher?.DestroyGhostPreview();
                if (ExecutionReflectionSystem.Instance != null)
                {
                    ExecutionReflectionSystem.Instance.IsLocalViewerGhost = false;
                }
                if (PlayerCameraController.Instance != null)
                {
                    PlayerCameraController.Instance.SetGhostSpectatorMode(false);
                }
                for (int i = 0; i < playerSeats.Count; i++)
                {
                    if (playerSeats[i] != null && playerSeats[i].Avatar != null)
                    {
                        playerSeats[i].Avatar.ResetAvatar();
                    }
                }
            }

            if (typewriter != null)
            {
                typewriter.ClearAllXRayVisuals();
                typewriter.ClearAllCurseVisuals();
                typewriter.ResetAllKeys();
            }
            if (hudController != null)
            {
                hudController.UpdateRoundInfo(round, safeKeys);
                hudController.ShowBanner($"TUR {round} BAŞLADI!", Color.white, 2.0f);
            }
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBell();
        }

        [ClientRpc]
        private void OnCardSelectionStartedClientRpc(int seatIndex)
        {
            EnsureLocalReferences();

            for (int i = 0; i < playerSeats.Count; i++)
            {
                if (playerSeats[i] != null) playerSeats[i].SetHighlight(i == seatIndex);
            }

            if (seatIndex >= 0 && seatIndex < playerSeats.Count && playerSeats[seatIndex] != null && playerSeats[seatIndex].Avatar != null)
            {
                playerSeats[seatIndex].Avatar.PlayThinking(1.2f);
            }

            if (lightingController != null && seatIndex < PlayerSlots.Count)
            {
                lightingController.SetTurnState(PlayerSlots[seatIndex].AvatarColor);
            }

            bool isLocalHumanTurn = (seatIndex == LocalSeatIndex) && (seatIndex < PlayerSlots.Count && !PlayerSlots[seatIndex].IsBot);
            string pName = (seatIndex < PlayerSlots.Count) ? PlayerSlots[seatIndex].PlayerName.ToString() : $"Oyuncu {seatIndex + 1}";
            Color col = (seatIndex < PlayerSlots.Count) ? (Color)PlayerSlots[seatIndex].AvatarColor : Color.white;

            if (hudController != null)
            {
                if (isLocalHumanTurn)
                {
                    hudController.SetPromptText($"Kader Kartı: <color=#{ColorUtility.ToHtmlStringRGB(col)}>{pName}</color>");
                }
                else
                {
                    hudController.SetPromptText($"Sıra: <color=#{ColorUtility.ToHtmlStringRGB(col)}>{pName}</color> — Kader kartı seçiyor...");
                }
            }

            if (cardSelectionController != null)
            {
                if (isLocalHumanTurn)
                {
                    bool isFogged = seatIndex < PlayerSlots.Count && PlayerSlots[seatIndex].IsFogged;
                    cardSelectionController.SetFoggedState(isFogged);
                    cardSelectionController.SetNetworkClickInterceptor((chosenIdx) => RequestSelectCardServerRpc(seatIndex, chosenIdx));
                    cardSelectionController.ShowCardSelection(
                        seatIndex,
                        pName,
                        col,
                        false,
                        null,
                        $"KADER KARTINI SEÇ: {pName}",
                        "Doğru kartı bulursan daktiloya basmadan sırayı pas geçersin!\nYanlış kartı seçersen daktiloda tuşa basmak zorundasın!"
                    );
                }
                else
                {
                    cardSelectionController.SetFoggedState(false);
                    cardSelectionController.SetNetworkClickInterceptor(null);
                    cardSelectionController.HideImmediate();
                }
            }
        }

        [ClientRpc]
        private void OnCardRevealedClientRpc(int seatIndex, int chosenCardIndex, int safeCardIndex, bool isSafe)
        {
            EnsureLocalReferences();

            bool isLocalHumanTurn = (seatIndex == LocalSeatIndex) && (seatIndex < PlayerSlots.Count && !PlayerSlots[seatIndex].IsBot);

            if (cardSelectionController != null)
            {
                if (isLocalHumanTurn)
                {
                    cardSelectionController.RevealNetworkCardSelection(chosenCardIndex, safeCardIndex, isSafe, null);
                }
                else
                {
                    cardSelectionController.HideImmediate();
                }
            }

            if (hudController != null && seatIndex < PlayerSlots.Count)
            {
                string pName = PlayerSlots[seatIndex].PlayerName.ToString();
                if (isSafe)
                {
                    hudController.ShowBanner($"{pName} DOĞRU KARTI BULDU!\nDaktiloya basmadan sırayı pas geçiyor!", new Color(0.2f, 0.85f, 0.35f), 1.8f);
                }
                else
                {
                    hudController.ShowBanner($"{pName} YANLIŞ KARTI SEÇTİ!\nŞimdi daktiloda bir tuşa basmak zorunda!", new Color(0.9f, 0.25f, 0.2f), 1.8f);
                }
            }
        }

        [ClientRpc]
        private void OnTurnStartedClientRpc(int seatIndex)
        {
            EnsureLocalReferences();
            if (cardSelectionController != null) cardSelectionController.HideImmediate();

            for (int i = 0; i < playerSeats.Count; i++)
            {
                if (playerSeats[i] != null) playerSeats[i].SetHighlight(i == seatIndex);
            }

            if (seatIndex >= 0 && seatIndex < playerSeats.Count && playerSeats[seatIndex] != null && playerSeats[seatIndex].Avatar != null)
            {
                playerSeats[seatIndex].Avatar.PlayThinking(1.2f);
            }

            if (lightingController != null && seatIndex < PlayerSlots.Count)
            {
                lightingController.SetTurnState(PlayerSlots[seatIndex].AvatarColor);
            }

            if (hudController != null && seatIndex < PlayerSlots.Count)
            {
                string pName = PlayerSlots[seatIndex].PlayerName.ToString();
                Color col = PlayerSlots[seatIndex].AvatarColor;
                hudController.SetPromptText($"Sıra: <color=#{ColorUtility.ToHtmlStringRGB(col)}>{pName}</color>");
            }
        }

        [ClientRpc]
        private void OnBotAimingClientRpc(int botSeatIndex, int keyIndex)
        {
            if (typewriter != null)
            {
                TypewriterKey key = typewriter.GetKey(keyIndex);
                if (key != null)
                {
                    StartCoroutine(FlashKeyAimRoutine(key));
                }
            }
        }

        private IEnumerator FlashKeyAimRoutine(TypewriterKey key)
        {
            key.SetHovered(true);
            yield return new WaitForSeconds(0.4f);
            key.SetHovered(false);
        }

        [ClientRpc]
        private void OnKeyResolvedClientRpc(int seatIndex, int keyIndex, bool isTrap, PlayerHand lostHand, int score, int handsRemaining, bool isAlive)
        {
            // 1. Daktilo tuşuna bas
            if (typewriter != null)
            {
                typewriter.PressKey(keyIndex, isTrap);
            }

            Vector3 keyPos = (typewriter != null && typewriter.GetKey(keyIndex) != null)
                ? typewriter.GetKey(keyIndex).transform.position
                : Vector3.zero;

            PlayerVisualAvatar avatar = (seatIndex >= 0 && seatIndex < playerSeats.Count && playerSeats[seatIndex] != null)
                ? playerSeats[seatIndex].Avatar
                : null;

            if (avatar != null)
            {
                int prevHands = isTrap ? Mathf.Min(2, handsRemaining + 1) : handsRemaining;
                avatar.PlayPressKey(prevHands, null, keyPos);
            }

            // 2. Ses ve Efekt
            if (isTrap)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayTrapTriggered();

                Color pCol = (seatIndex < PlayerSlots.Count) ? (Color)PlayerSlots[seatIndex].AvatarColor : Color.red;

                if (CartoonFXManager.Instance != null)
                {
                    CartoonFXManager.Instance.PlayTrapSnapFX(keyPos, pCol);
                }
            }
            else
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayTypewriterClick();

                if (CartoonFXManager.Instance != null)
                {
                    CartoonFXManager.Instance.PlaySafeKeyFX(keyPos);
                }

                if (avatar != null)
                {
                    avatar.PlayJoyRelief();
                }

                UpdateHUDCards();
            }
        }

        [ClientRpc]
        private void OnButcherSequenceClientRpc(int seatIndex, int weaponIdx, PlayerHand lostHand, bool isEliminated)
        {
            EnsureLocalReferences();
            butcher?.DestroyGhostPreview();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButcherStinger();

            if (ExecutionReflectionSystem.Instance != null)
            {
                ExecutionReflectionSystem.Instance.IsLocalViewerGhost = IsLocalGhost;
            }

            if (lightingController != null)
            {
                if (IsLocalGhost)
                {
                    lightingController.SetNormalState();
                }
                else
                {
                    lightingController.SetButcherState();
                }
            }

            if (butcher != null && seatIndex < playerSeats.Count && playerSeats[seatIndex] != null)
            {
                PlayerSeat seat = playerSeats[seatIndex];
                Transform spawn = seat.ButcherSpawnPoint;
                butcher.AppearAt(spawn, seatIndex, weaponIdx, () =>
                {
                    if (seat.Avatar != null)
                    {
                        if (lostHand != PlayerHand.None)
                        {
                            seat.Avatar.SeverHand(lostHand);
                        }
                        if (isEliminated)
                        {
                            seat.Avatar.PlayEliminatedByWeapon(weaponIdx);
                            seat.Avatar.SetGhostMode(true);
                        }
                    }

                    if (isEliminated && seatIndex == LocalSeatIndex)
                    {
                        if (PlayerCameraController.Instance != null)
                        {
                            PlayerCameraController.Instance.SetGhostSpectatorMode(true, LocalSeatIndex, 12f);
                        }
                        OnLocalBecameGhost?.Invoke(true);
                    }

                    UpdateHUDCards();
                });
            }

            StartCoroutine(RestoreLightingAfterDelay(2.2f));
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestEmoteServerRpc(int seatIndex, int emoteIndex)
        {
            if (!IsServer) return;
            OnEmoteExecutedClientRpc(seatIndex, emoteIndex);
        }

        [ClientRpc]
        private void OnEmoteExecutedClientRpc(int seatIndex, int emoteIndex)
        {
            if (seatIndex >= 0 && seatIndex < playerSeats.Count && playerSeats[seatIndex] != null)
            {
                var avatar = playerSeats[seatIndex].Avatar;
                if (avatar != null)
                {
                    avatar.TriggerEmote((EmoteType)emoteIndex);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestEmoteSlotServerRpc(int senderSeat, int slotIndex, int targetSeat)
        {
            if (!IsServer) return;
            if (senderSeat < 0 || senderSeat >= PlayerSlots.Count) return;
            if (!PlayerSlots[senderSeat].IsAlive) return;
            if (slotIndex < 1 || slotIndex > 5) return;
            if (Time.time - serverLastEmoteTime[senderSeat] < 2.0f) return;

            serverLastEmoteTime[senderSeat] = Time.time;

            int validTarget = -1;
            if (targetSeat >= 0 && targetSeat < PlayerSlots.Count && targetSeat != senderSeat && PlayerSlots[targetSeat].IsAlive)
            {
                validTarget = targetSeat;
            }

            OnEmoteSlotExecutedClientRpc(senderSeat, slotIndex, validTarget);
        }

        [ClientRpc]
        private void OnEmoteSlotExecutedClientRpc(int senderSeat, int slotIndex, int targetSeat)
        {
            if (UI.EmoteWheelUI.Instance != null)
            {
                UI.EmoteWheelUI.Instance.ExecuteLocalEmoteSlot(senderSeat, slotIndex, targetSeat);
                return;
            }

            if (senderSeat >= 0 && senderSeat < playerSeats.Count && playerSeats[senderSeat] != null && playerSeats[senderSeat].Avatar != null)
            {
                Vector3? targetPos = (targetSeat >= 0 && targetSeat < playerSeats.Count && playerSeats[targetSeat] != null)
                    ? playerSeats[targetSeat].transform.position
                    : (Vector3?)null;
                playerSeats[senderSeat].Avatar.TriggerEmoteSlot(slotIndex, targetPos);
            }
        }

        private IEnumerator RestoreLightingAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (butcher != null) butcher.ResumePatrol();
            if (lightingController != null) lightingController.SetNormalState();
        }

        [ClientRpc]
        private void OnRoundEndedClientRpc()
        {
            EnsureLocalReferences();
            CancelLocalXRayKeyTarget();
            if (typewriter != null) typewriter.ClearAllXRayVisuals();
            if (cardSelectionController != null)
            {
                cardSelectionController.SetFoggedState(false);
                cardSelectionController.HideImmediate();
            }

            if (hudController != null)
            {
                hudController.ShowBanner("TUR TAMAMLANDI!\nYeni tura geçiliyor...", Color.cyan, 2.5f);
            }
        }

        [ClientRpc]
        private void OnGameOverClientRpc(string winnerName, Color winnerColor, int score, int winnerArchetype, int winnerHands, string scoreRecap)
        {
            EnsureLocalReferences();
            CancelLocalXRayKeyTarget();
            IsWaitingForGhostCurseTarget = false;
            IsGhostWindowOpen = false;
            GameManager.Instance?.ClearGhostActiveGameplayState();
            butcher?.DestroyGhostPreview();
            if (ExecutionReflectionSystem.Instance != null)
            {
                ExecutionReflectionSystem.Instance.IsLocalViewerGhost = false;
            }
            if (PlayerCameraController.Instance != null)
            {
                PlayerCameraController.Instance.SetGhostSpectatorMode(false);
            }
            if (typewriter != null)
            {
                typewriter.ClearAllXRayVisuals();
                typewriter.ClearAllCurseVisuals();
            }
            if (cardSelectionController != null)
            {
                cardSelectionController.SetFoggedState(false);
                cardSelectionController.HideImmediate();
            }

            if (gameOverController != null)
            {
                CharacterArchetype arch = (CharacterArchetype)(Mathf.Clamp(winnerArchetype, 0, 3));
                PlayerData dummyWinner = new PlayerData(0, winnerName, winnerColor, arch) { Score = score };
                if (winnerHands == 1) dummyWinner.SeverRandomHand();
                else if (winnerHands == 0) { dummyWinner.SeverRandomHand(); dummyWinner.SeverRandomHand(); }
                gameOverController.ShowGameOver(dummyWinner, null, scoreRecap);
            }
        }

        [ClientRpc]
        private void OnSeatTakenOverClientRpc(int seatIndex, string newBotName)
        {
            if (hudController != null)
            {
                hudController.ShowBanner($"Oyuncu ayrıldı! Koltuk {seatIndex + 1} bot tarafından devralındı.", Color.yellow, 2.0f);
            }
            UpdateHUDCards();
        }

        [ClientRpc]
        private void SyncGhostSoulEnergyTargetClientRpc(int energy, ClientRpcParams rpcParams = default)
        {
            LocalSoulEnergy = energy;
            OnLocalSoulEnergyUpdated?.Invoke(energy);
        }

        [ClientRpc]
        private void OnSoulEnergySyncedClientRpc(int energy, ClientRpcParams rpcParams = default)
        {
            LocalSoulEnergy = energy;
            OnLocalSoulEnergyUpdated?.Invoke(energy);
        }

        [ClientRpc]
        private void BroadcastGhostTransformClientRpc(int seatIndex, Vector3 pos, float yaw)
        {
            if (seatIndex == LocalSeatIndex) return;
            EnsureLocalReferences();

            if (seatIndex >= 0 && seatIndex < playerSeats.Count && playerSeats[seatIndex]?.Avatar != null)
            {
                playerSeats[seatIndex].Avatar.SetGhostFreeFlightTransform(pos, Quaternion.Euler(0f, yaw, 0f));
            }
        }

        [ClientRpc]
        private void OnGhostScareExecutedClientRpc(int actorSeat, int targetSeat, Vector3 ghostPos)
        {
            EnsureLocalReferences();

            PlayerVisualAvatar targetAvatar = (targetSeat >= 0 && targetSeat < playerSeats.Count && playerSeats[targetSeat] != null)
                ? playerSeats[targetSeat].Avatar
                : null;
            bool isLocalTarget = targetSeat >= 0 &&
                                 targetSeat == LocalSeatIndex &&
                                 targetSeat < PlayerSlots.Count &&
                                 !PlayerSlots[targetSeat].IsBot;

            GhostScareAndThrowSystem.Instance?.ExecuteScareVisuals(
                targetSeat,
                ghostPos,
                targetAvatar,
                PlayerCameraController.Instance,
                isLocalTarget);

            if (actorSeat == LocalSeatIndex)
            {
                GhostHUD.Instance?.NotifyScareCooldownStarted();
            }
        }

        [ClientRpc]
        private void OnGhostThrowExecutedClientRpc(int actorSeat, int itemTypeIndex, Vector3 startPos, Vector3 endPos, int hitSeat)
        {
            EnsureLocalReferences();

            ThrowableItemData itemData = ResolveThrowableItemData((ThrowableItemType)itemTypeIndex);
            if (itemData == null) return;

            PlayerVisualAvatar hitAvatar = (hitSeat >= 0 && hitSeat < playerSeats.Count && playerSeats[hitSeat] != null)
                ? playerSeats[hitSeat].Avatar
                : null;
            bool isLocalTarget = hitSeat >= 0 &&
                                 hitSeat == LocalSeatIndex &&
                                 hitSeat < PlayerSlots.Count &&
                                 !PlayerSlots[hitSeat].IsBot;

            GhostScareAndThrowSystem.Instance?.LaunchThrowableVisual(
                itemData,
                startPos,
                endPos,
                hitSeat,
                hitAvatar,
                isLocalTarget);

            if (actorSeat == LocalSeatIndex)
            {
                GhostHUD.Instance?.NotifyThrowCooldownStarted();
            }
        }

        [ClientRpc]
        private void OnGhostWeaponPreviewSyncedClientRpc(int weaponIndex, ClientRpcParams rpcParams = default)
        {
            if (!IsLocalGhost) return;
            EnsureLocalReferences();

            Vector3 tableCenter = typewriter != null ? typewriter.transform.position : Vector3.zero;
            Vector3 cornerPos = new Vector3(tableCenter.x + 2.35f, 0f, tableCenter.z + 2.35f);
            Vector3 lookDir = tableCenter - cornerPos;
            lookDir.y = 0f;
            Quaternion cornerRot = lookDir.sqrMagnitude >= 0.0001f
                ? Quaternion.LookRotation(lookDir.normalized, Vector3.up)
                : Quaternion.identity;

            butcher?.SpawnOrUpdateGhostPreview((ExecutionWeaponType)weaponIndex, cornerPos, cornerRot);
        }

        [ClientRpc]
        private void DestroyGhostPreviewClientRpc()
        {
            EnsureLocalReferences();
            butcher?.DestroyGhostPreview();
        }

        [ClientRpc]
        private void OnGhostWindowChangedClientRpc(bool isOpen, float duration)
        {
            IsGhostWindowOpen = isOpen;
            if (!isOpen)
            {
                IsWaitingForGhostCurseTarget = false;
            }
            OnGhostWindowUpdated?.Invoke(isOpen, duration);
        }

        [ClientRpc]
        private void OnCursedKeySyncedToGhostsClientRpc(int keyIndex, ClientRpcParams rpcParams = default)
        {
            LocalCursedKeyIndex = keyIndex;
            IsWaitingForGhostCurseTarget = false;

            if (typewriter != null)
            {
                TypewriterKey key = typewriter.GetKey(keyIndex);
                if (key != null)
                {
                    key.SetCursedVisual(true, IsLocalGhost);
                }
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayGhostCurse();
            }

            OnGhostCurseConfirmedForGhosts?.Invoke(keyIndex);
        }

        [ClientRpc]
        private void OnMachineCurseShakeClientRpc()
        {
            if (typewriter != null)
            {
                typewriter.ShakeMachine(0.45f, 0.02f);
            }
        }

        [ClientRpc]
        private void OnGhostCurseExplodedClientRpc(int keyIndex)
        {
            if (LocalCursedKeyIndex == keyIndex)
            {
                LocalCursedKeyIndex = -1;
            }

            if (typewriter != null)
            {
                TypewriterKey key = typewriter.GetKey(keyIndex);
                if (key != null)
                {
                    key.SetCursedVisual(false, false);
                }
                typewriter.ShakeMachine(0.55f, 0.028f);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayGhostCurseExplosion();
            }

            if (hudController != null)
            {
                hudController.ShowBanner("LANETLİ TUŞ PATLADI! Hayalet gülüyor!", new Color(0.75f, 0.35f, 0.95f), 2.0f);
            }
        }

        [ClientRpc]
        private void OnGhostEffectExecutedClientRpc(int seatIndex, int effectType)
        {
            if (!SettingsMenuController.AreGhostEffectsEnabled()) return;

            GhostEffectType effect = (GhostEffectType)effectType;
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayGhostEffect(effect);
            }

            if (effect == GhostEffectType.FlyPaper && typewriter != null && typewriter.Paper != null)
            {
                typewriter.Paper.PlayPaperFlyAway();
            }
            else if (effect == GhostEffectType.FlickerLamp && lightingController != null)
            {
                lightingController.SetButcherState();
                StartCoroutine(RestoreLightingAfterDelay(0.45f));
            }

            OnNetworkGhostEffectTriggered?.Invoke(effect);
        }

        [ClientRpc]
        private void SyncLocalHandTargetClientRpc(int[] cardInts, ClientRpcParams rpcParams = default)
        {
            localAbilityHand.Clear();
            if (cardInts != null)
            {
                for (int i = 0; i < cardInts.Length; i++)
                {
                    localAbilityHand.Add((AbilityCardType)cardInts[i]);
                }
            }
            OnLocalAbilityHandUpdated?.Invoke(localAbilityHand);
        }

        [ClientRpc]
        private void OnPossessResultTargetClientRpc(int revealedMonsterIndex, ClientRpcParams rpcParams = default)
        {
            EnsureLocalReferences();
            if (cardSelectionController != null)
            {
                cardSelectionController.RevealOneMonsterHint(true, revealedMonsterIndex);
            }
            if (hudController != null)
            {
                hudController.ShowBanner("ELE GEÇİR: Canavar kartlarından biri ifşa edildi!", new Color(0.7f, 0.35f, 0.95f), 2.0f);
            }
        }

        [ClientRpc]
        private void OnXRayResultTargetClientRpc(int keyIndex, bool isTrap, ClientRpcParams rpcParams = default)
        {
            EnsureLocalReferences();
            if (typewriter != null)
            {
                TypewriterKey key = typewriter.GetKey(keyIndex);
                if (key != null)
                {
                    key.ApplyXRayVisual(isTrap);
                }
            }
            if (hudController != null)
            {
                string resultText = isTrap ? "TUZAK!" : "GÜVENLİ!";
                Color col = isTrap ? new Color(1.0f, 0.25f, 0.25f) : new Color(0.2f, 0.95f, 0.55f);
                hudController.ShowBanner($"RÖNTGEN: {keyIndex + 1}. tuş {resultText}", col, 2.2f);
            }
        }

        [ClientRpc]
        private void OnAbilityCardPlayedClientRpc(int seatIndex, int cardTypeInt, int targetIndex, string announcement)
        {
            EnsureLocalReferences();
            AbilityCardType cardType = (AbilityCardType)cardTypeInt;

            if (cardType == AbilityCardType.SkipTurn && cardSelectionController != null)
            {
                cardSelectionController.SetFoggedState(false);
                cardSelectionController.HideImmediate();
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBell();
            }

            if (hudController != null && !string.IsNullOrEmpty(announcement))
            {
                hudController.ShowBanner(announcement, new Color(0.35f, 0.8f, 1.0f), 2.0f);
            }

            OnNetworkAbilityCardPlayed?.Invoke(seatIndex, cardType, targetIndex, announcement);
            UpdateHUDCards();
        }

        [ClientRpc]
        private void OnShieldBlockedTrapClientRpc(int seatIndex, int keyIndex)
        {
            EnsureLocalReferences();
            if (typewriter != null)
            {
                typewriter.PressKey(keyIndex, false);
            }

            Vector3 keyPos = (typewriter != null && typewriter.GetKey(keyIndex) != null)
                ? typewriter.GetKey(keyIndex).transform.position
                : Vector3.zero;

            PlayerVisualAvatar avatar = (seatIndex >= 0 && seatIndex < playerSeats.Count && playerSeats[seatIndex] != null)
                ? playerSeats[seatIndex].Avatar
                : null;

            if (avatar != null && seatIndex < PlayerSlots.Count)
            {
                avatar.PlayPressKey(PlayerSlots[seatIndex].RemainingHands, null, keyPos);
                avatar.PlayJoyRelief();
            }

            if (AudioManager.Instance != null) AudioManager.Instance.PlayBell();
            if (CartoonFXManager.Instance != null) CartoonFXManager.Instance.PlaySafeKeyFX(keyPos);

            if (hudController != null && seatIndex < PlayerSlots.Count)
            {
                string pName = PlayerSlots[seatIndex].PlayerName.ToString();
                hudController.ShowBanner($"KALKAN KORUDU! {pName} elini kaybetmekten kurtuldu!", new Color(0.18f, 0.78f, 0.96f), 2.0f);
            }

            UpdateHUDCards();
        }
        #endregion

        private void SyncSeatAvatars()
        {
            for (int i = 0; i < PlayerSlots.Count && i < playerSeats.Count; i++)
            {
                var slot = PlayerSlots[i];
                var seat = playerSeats[slot.SeatIndex];
                if (seat == null) continue;

                CharacterArchetype targetArch = (CharacterArchetype)(Mathf.Clamp(slot.ArchetypeIndex, 0, 3));
                if (seat.Avatar == null || seat.Avatar.Archetype != targetArch)
                {
                    int idx = (int)targetArch;
                    if (characterPrefabs != null && idx >= 0 && idx < characterPrefabs.Length && characterPrefabs[idx] != null)
                    {
                        seat.AssignCharacterPrefab(characterPrefabs[idx], slot.AvatarColor);
                    }
                }
                seat.SetupSeat(slot.SeatIndex, slot.AvatarColor);

                if (slot.SeatIndex == LocalSeatIndex && UI.EmoteWheelUI.Instance != null)
                {
                    UI.EmoteWheelUI.Instance.ConfigureForSeat(LocalSeatIndex, targetArch);
                }
            }
        }

        private void HandlePlayerSlotsChanged(NetworkListEvent<NetworkPlayerSlot> changeEvent)
        {
            DetermineLocalSeat();
            ConfigureLocalCameraForSeat(LocalSeatIndex);
            SyncSeatAvatars();
            UpdateHUDCards();
        }

        private void HandleActivePlayerChanged(int oldVal, int newVal)
        {
            UpdateHUDInfo();
        }

        private void HandleRoundChanged(int oldVal, int newVal)
        {
            UpdateHUDInfo();
        }

        private void HandleSafeKeysChanged(int oldVal, int newVal)
        {
            UpdateHUDInfo();
        }

        private void UpdateHUDInfo()
        {
            if (hudController != null)
            {
                hudController.UpdateRoundInfo(CurrentRound.Value, RemainingSafeKeys.Value);
            }
        }

        private void UpdateHUDCards()
        {
            if (hudController == null) return;

            List<PlayerData> dummyList = new List<PlayerData>();
            for (int i = 0; i < PlayerSlots.Count; i++)
            {
                var slot = PlayerSlots[i];
                PlayerData p = new PlayerData(slot.SeatIndex, slot.PlayerName.ToString(), slot.AvatarColor, (CharacterArchetype)(slot.ArchetypeIndex % 4))
                {
                    Score = slot.Score,
                    LeftHand = slot.LeftHandIntact ? HandState.Intact : HandState.Lost,
                    RightHand = slot.RightHandIntact ? HandState.Intact : HandState.Lost,
                    HasActiveShield = slot.HasActiveShield,
                    IsFogged = slot.IsFogged
                };
                for (int c = 0; c < slot.AbilityCardCount; c++)
                {
                    p.AbilityCards.Add(AbilityCardType.None);
                }
                dummyList.Add(p);
            }

            hudController.RefreshAllCards(dummyList, ActivePlayerIndex.Value);
        }

        /// <summary>
        /// Yerel oyuncunun tıklama isteği gönderdiği arabirim.
        /// </summary>
        public void LocalRequestPressKey(int keyIndex)
        {
            if (IsWaitingForLocalXRayTarget)
            {
                ConfirmLocalXRayKeyTarget(keyIndex);
                return;
            }

            if (!IsLocalPlayerTurn || CurrentNetworkState.Value != GameState.PlayerTurn)
            {
                return;
            }

            RequestPressKeyServerRpc(LocalSeatIndex, keyIndex);
        }

        public void LocalRequestPlayAbilityCard(int handSlotIndex, int targetIndex = -1)
        {
            if (handSlotIndex < 0 || handSlotIndex >= localAbilityHand.Count) return;

            AbilityCardType cardType = localAbilityHand[handSlotIndex];
            if (cardType == AbilityCardType.XRay && targetIndex < 0 && CurrentNetworkState.Value == GameState.PlayerTurn)
            {
                IsWaitingForLocalXRayTarget = true;
                pendingLocalXRaySlot = handSlotIndex;
                if (hudController != null)
                {
                    hudController.ShowBanner("RÖNTGEN: Taramak istediğin tuşa tıkla!", new Color(0.2f, 0.9f, 0.95f), 2.5f);
                }
                return;
            }

            RequestPlayAbilityCardServerRpc(LocalSeatIndex, handSlotIndex, targetIndex);
        }

        public bool ConfirmLocalXRayKeyTarget(int keyIndex)
        {
            if (!IsWaitingForLocalXRayTarget) return false;
            IsWaitingForLocalXRayTarget = false;
            RequestPlayAbilityCardServerRpc(LocalSeatIndex, pendingLocalXRaySlot, keyIndex);
            pendingLocalXRaySlot = -1;
            return true;
        }

        public void CancelLocalXRayKeyTarget()
        {
            IsWaitingForLocalXRayTarget = false;
            pendingLocalXRaySlot = -1;
        }

        public void LocalStartGhostCurseSelection()
        {
            if (!IsLocalGhost || !IsGhostWindowOpen || LocalSoulEnergy < 2) return;
            IsWaitingForGhostCurseTarget = true;
            if (hudController != null)
            {
                hudController.ShowBanner("HAYALET LANETİ: Lanetlemek istediğin tuşa tıkla!", new Color(0.65f, 0.3f, 0.95f), 2.5f);
            }
        }

        public void LocalCancelGhostCurseSelection()
        {
            IsWaitingForGhostCurseTarget = false;
        }

        public void LocalRequestGhostCurseKey(int keyIndex)
        {
            if (!IsLocalGhost || !IsGhostWindowOpen) return;
            IsWaitingForGhostCurseTarget = false;
            RequestGhostCurseKeyServerRpc(LocalSeatIndex, keyIndex);
        }

        public void LocalRequestGhostSelectWeapon(int weaponIndex)
        {
            if (!IsLocalGhost || !IsGhostWindowOpen) return;
            RequestGhostSelectWeaponServerRpc(LocalSeatIndex, weaponIndex);
        }

        public void LocalRequestGhostEffect(GhostEffectType effectType)
        {
            if (!IsLocalGhost) return;
            RequestGhostEffectServerRpc(LocalSeatIndex, (int)effectType);
        }

        public void RequestGhostScareClient()
        {
            if (!IsLocalGhost) return;
            RequestGhostScareServerRpc();
        }

        public void RequestGhostThrowClient(ThrowableItemType item, Vector3 origin, Vector3 direction)
        {
            if (!IsLocalGhost) return;
            RequestGhostThrowServerRpc((int)item, origin, direction);
        }

        public void SubmitLocalGhostTransform(Vector3 pos, float yaw)
        {
            if (!IsLocalGhost) return;
            SubmitGhostTransformServerRpc(pos, yaw);
        }

        public void LocalGhostReturnToMainMenu()
        {
            Time.timeScale = 1f;
            GameManager.Instance?.ClearGhostActiveGameplayState();
            butcher?.DestroyGhostPreview();
            if (PlayerCameraController.Instance != null)
            {
                PlayerCameraController.Instance.SetGhostSpectatorMode(false);
            }
            if (ExecutionReflectionSystem.Instance != null)
            {
                ExecutionReflectionSystem.Instance.IsLocalViewerGhost = false;
            }
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        private bool HandleKeyClickIntercept(int keyIndex)
        {
            if (IsWaitingForLocalXRayTarget)
            {
                return ConfirmLocalXRayKeyTarget(keyIndex);
            }
            if (IsWaitingForGhostCurseTarget)
            {
                LocalRequestGhostCurseKey(keyIndex);
                return true;
            }
            return false;
        }
    }
}
