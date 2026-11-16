> ← [Ana Mimari İndeksine Dön](../../UYGULAMA_KAPSAMLI_REHBERI_VE_GUVENLIK_RAPORU.md) | **Modül 3/5:** Çekirdek Veri, Kartlar, Kasap, Hayalet, Ağ, Yapay Zekâ, Ses, Kamera ve Masaüstü Oynanış Envanteri (Bölüm 4.1–4.4)

# Modül 3: Çekirdek Servisler ve Oynanış Sistemleri Kod Envanteri (Bölüm 4.1–4.4)

Bu belge, `Assets/_Project/Scripts/` altındaki çekirdek veri modelleri, kart/kasap/hayalet/emote sistemleri, ağ altyapısı, yapay zekâ, ses, kamera ve masaüstü oynanış bileşenlerinin dosya bazlı teknik envanterini içerir.

---

## 4.1. Çekirdek Veri Modelleri, Enumlar ve Komutlar (`Core/`)

| Dosya | Satır | Namespace | Ana Tip(ler) | Sorumluluk Özeti |
| :--- | :---: | :--- | :--- | :--- |
| [GameEnums.cs](../../Assets/_Project/Scripts/Core/GameEnums.cs) | 108 | `FingerMonster.Core` | `GameState`, `AbilityCardType`, `PlayerHand`, `HandState`, `KeyResult`, `GhostEffectType`, `GhostActionType`, `ThrowableItemType`, `GhostMovementMode` | Tüm oyun durumları, yetenek kartları, el/tuş sonuçları ve hayalet aksiyon enumları |
| [GameSessionData.cs](../../Assets/_Project/Scripts/Core/GameSessionData.cs) | 97 | `FingerMonster.Core` | `GameSessionData` (`static class`), `GameMode`, `Difficulty` | Ana menü ile `Game` sahnesi arası mod, zorluk ve koltuk/karakter seçimi taşıyıcısı |
| [GameSettings.cs](../../Assets/_Project/Scripts/Core/GameSettings.cs) | 188 | `FingerMonster.Core` | `GameSettings` (`ScriptableObject`) | Oyun kuralları, zamanlamalar, kart ağırlıkları, hayalet ve kasap silahı ayarları |
| [ICommand.cs](../../Assets/_Project/Scripts/Core/ICommand.cs) | 20 | `FingerMonster.Core` | `ICommand` (`interface`) | Yerel/ağ oyuncu eylemleri için Command Pattern sözleşmesi |
| [PressKeyCommand.cs](../../Assets/_Project/Scripts/Core/PressKeyCommand.cs) | 65 | `FingerMonster.Core` | `PressKeyCommand : ICommand` | Daktilo tuşuna basma komutunu doğrular ve `GameManager.ResolveKey` ile icra eder |
| [GameManager.cs](../../Assets/_Project/Scripts/Core/GameManager.cs) | 1944 | `FingerMonster.Core` | `GameManager : MonoBehaviour` | Tek oyunculu / hotseat oyun döngüsü, FSM, tuzak dağılımı, kartlar ve hayalet akışı |

### 4.1.1. `GameEnums.cs` (`Assets/_Project/Scripts/Core/GameEnums.cs`)
- **Namespace:** `FingerMonster.Core`
- **Enumlar:**
  - `GameState`: `Lobby`, `RoundStart`, `CardPlay`, `CardSelection`, `PlayerTurn`, `KeyResolve`, `HandLost`, `ButcherSequence`, `RoundEnd`, `GameOver`
  - `AbilityCardType`: `None = -1`, `Shield = 0`, `Possess = 1`, `XRay = 2`, `SkipTurn = 3`, `ReverseDirection = 4`, `Fog = 5`
  - `PlayerHand`: `None`, `Left`, `Right`
  - `HandState`: `Intact`, `Lost`
  - `KeyResult`: `Invalid`, `AlreadyPressed`, `Safe`, `Trap`
  - `GhostEffectType`: `FlickerLamp = 0`, `EerieLaugh = 1`, `FlyPaper = 2`, `ColdBreeze = 3`, `Applause = 4`, `SadBoo = 5`
  - `GhostActionType`: `None = 0`, `CurseKey = 1`, `ChooseWeapon = 2`, `GhostEffect = 3`
  - `ThrowableItemType`: `CreamPie = 0`, `Tomato = 1`, `PaperBall = 2`, `Sausage = 3`
  - `GhostMovementMode`: `FreeFlight = 0`, `SpectatorOrbit = 1`

### 4.1.2. `GameSessionData.cs` (`Assets/_Project/Scripts/Core/GameSessionData.cs`)
- **Namespace:** `FingerMonster.Core`
- **Tip:** `public static class GameSessionData`
  - **İç Enumlar:** `GameMode` (`SoloVsBots`, `HotseatParty`), `Difficulty` (`Easy = 4`, `Normal = 6`, `Nightmare = 8`)
  - **Statik Alanlar:** `SelectedMode`, `SelectedDifficulty`, `SelectedCharacterIndex`
  - **Public Metotlar:**
    - `public static void SetMode(GameMode mode)`
    - `public static void SetSelectedCharacter(int index)`
    - `public static CharacterArchetype GetSeatArchetype(int seatIndex)`
    - `public static bool IsSlotBot(int index)`
    - `public static int GetTrapCount()`

### 4.1.3. `GameSettings.cs` (`Assets/_Project/Scripts/Core/GameSettings.cs`)
- **Namespace:** `FingerMonster.Core`
- **Tip:** `public class GameSettings : ScriptableObject` (`[CreateAssetMenu(fileName = "GameSettings", menuName = "FingerMonster/Game Settings")]`)
- **Önemli Alanlar:**
  - Oyuncu/Tuş: `playerCount` (4), `totalKeys` (20), `trapCount` (6), `pointsPerSafeKey` (10), `alphabetLetters` (`"QWERTYUIOPASDFGHJKLZ"`), `playerColors`
  - Zamanlamalar: `turnTransitionDelay`, `keyResolveDelay`, `handLostDelay`, `butcherSequenceDuration`, `cameraLerpSpeed`
  - Yetenek Kartları: `maxAbilityCardsInHand` (2), `cardsDrawnPerRoundStart` (1), `cardsDrawnOnSafeCardPass` (1), `keepAbilityCardsBetweenRounds`, `restoreHandsOnNewRound`, `weightShield`, `weightPossess`, `weightXRay`, `weightSkipTurn`, `weightReverseDirection`, `weightFog`
  - Hayalet Ayarları: `ghostStartSoulEnergy`, `ghostEnergyPerRound`, `ghostMaxSoulEnergy`, `ghostCurseCost`, `ghostEffectCost`, `ghostWindowDuration`, `ghostEffectCooldown`, `maxCursesPerRound`, `ghostOrbitSpeed`, `ghostMoveSpeed`, `ghostSprintMultiplier`, `ghostLookSensitivity`, `ghostFadeDistance`, `ghostMinAlphaNearLiving`, `ghostScareCost`, `ghostScareCooldown`, `ghostScareRange`, `ghostScareTargetImmunity`, `ghostScareEffectDuration`, `ghostThrowCost`, `ghostThrowCooldown`, `ghostThrowMaxRange`, `ghostSplatDuration`, `ghostDecalDuration`
  - Koleksiyonlar: `List<ThrowableItemData> throwableItems`, `List<WeaponData> butcherWeapons`
- **Public Metotlar:**
  - `public void EnsureRuntimeDefaults()`
  - `public AbilityCardType DrawWeightedRandomAbilityCard()`

### 4.1.4. `ICommand.cs` & `PressKeyCommand.cs`
- **`ICommand`** (`FingerMonster.Core`):
  - `bool Validate(GameManager gameManager);`
  - `void Execute(GameManager gameManager);`
- **`PressKeyCommand : ICommand`** (`FingerMonster.Core`):
  - Property'ler: `public int PlayerIndex { get; }`, `public int KeyIndex { get; }`
  - Kurucu: `public PressKeyCommand(int playerIndex, int keyIndex)`
  - Metotlar: `public bool Validate(GameManager gameManager)`, `public void Execute(GameManager gameManager)`

### 4.1.5. `GameManager.cs` (`Assets/_Project/Scripts/Core/GameManager.cs`)
- **Namespace:** `FingerMonster.Core`
- **Tip:** `public class GameManager : MonoBehaviour` (`Instance` Singleton)
- **Alanlar & Property'ler:**
  - Referanslar (`[SerializeField]`): `settings`, `typewriter`, `turnController`, `cameraController`, `lightingController`, `butcher`, `playerSeats`, `characterPrefabs`, `hudController`, `gameOverController`, `pauseMenuController`, `cardSelectionController`
  - Durum Property'leri: `CurrentState`, `CurrentRound`, `ActivePlayerIndex`, `Typewriter`, `Players`, `TrapIndices`, `PressedIndices`, `RemainingSafeKeysCount`, `IsWaitingForXRayKeyTarget`, `IsGhostWindowOpen`, `CursedKeyIndex`, `CursesUsedThisRound`, `ChosenNextButcherWeapon`, `IsWaitingForGhostCurseTarget`, `XRayInspectedKeysThisRound`
- **Event'ler:** `OnStateChanged`, `OnTurnChanged`, `OnCardSelectionStarted`, `OnAbilityCardPlayed`, `OnAbilityHandsChanged`, `OnPlayerBecameGhost`, `OnGhostWindowChanged`, `OnGhostSoulEnergyChanged`, `OnGhostCurseApplied`, `OnGhostEffectTriggered`
- **Public Metotlar:**
  - Kurulum/Oyun Akışı: `EnsureSettings()`, `EnsureReferences()`, `IsBotPlayer(int playerIndex)`, `GetLocalHumanSeatIndex()`, `IsLocalHumanGhost()`, `InitializeGame()`, `ResetTableForNewGame()`, `StartRound()`, `EndGame()`, `TogglePause()`, `ForceSetState(GameState newState)`
  - Komut/Tuş: `ProcessCommand(ICommand command)`, `IsKeyPressed(int keyIndex)`, `ResolveKey(int playerIndex, int keyIndex)`
  - Yetenek Kartları: `DrawAbilityCardsForPlayer(int playerIndex, int count = 1)`, `CanPlayAbilityCard(int playerIndex, AbilityCardType cardType, out string reason)`, `TryPlayAbilityCard(int playerIndex, int handSlotIndex, int targetIndex = -1)`, `ConfirmXRayKeyTarget(int keyIndex)`, `CancelXRayKeyTarget()`, `InspectKeyWithXRay(int keyIndex, bool showVisualForLocalHuman)`
  - Hayalet Etkileşimi: `StartLocalGhostCurseSelection()`, `CancelLocalGhostCurseSelection()`, `TryGhostCurseKey(int ghostPlayerIndex, int keyIndex)`, `TryGhostSelectWeapon(int ghostPlayerIndex, int weaponIndex)`, `TryGhostScare(int actorSeat)`, `TryGhostThrow(int actorSeat, ThrowableItemType item, Vector3 origin, Vector3 direction)`, `ClearGhostActiveGameplayState()`, `TryGhostTriggerEffect(int ghostPlayerIndex, GhostEffectType effectType)`

---

## 4.2. Kart, Kasap, Hayalet ve Emote Sistemleri (`Cards/`, `Butcher/`, `Ghost/`, `Emotes/`)

| Dosya | Satır | Namespace | Ana Tip(ler) | Sorumluluk Özeti |
| :--- | :---: | :--- | :--- | :--- |
| [CardData.cs](../../Assets/_Project/Scripts/Cards/CardData.cs) | 188 | `FingerMonster.Gameplay` | `CardData` (`ScriptableObject`), `CardDataCatalog` | 6 stratejik yetenek kartının metadata ve katalog tanımları |
| [CardSelectionController.cs](../../Assets/_Project/Scripts/Cards/CardSelectionController.cs) | 456 | `FingerMonster.Gameplay` | `CardElement`, `CardSelectionController` | 3 kartlı "Push-Your-Luck" (1 güvenli, 2 canavar) seçimi, Sis ve Ele Geçir görselleri |
| [ButcherPlaceholder.cs](../../Assets/_Project/Scripts/Butcher/ButcherPlaceholder.cs) | 2191 | `FingerMonster.Gameplay` | `ButcherPlaceholder : MonoBehaviour` | Kasap 3D modeli (`kasap.fbx`), devriye hareketi, silah soketi, infaz animasyon ve FX |
| [ExecutionReflectionSystem.cs](../../Assets/_Project/Scripts/Butcher/ExecutionReflectionSystem.cs) | 247 | `FingerMonster.Gameplay` | `ExecutionReflectionSystem : MonoBehaviour` | İnfaz sırasında odayı karartıp kasabı daktilo silindirinde yansıtan `RenderTexture` sistemi |
| [ExecutionWeapons.cs](../../Assets/_Project/Scripts/Butcher/ExecutionWeapons.cs) | 117 | `FingerMonster.Gameplay` | `ExecutionWeaponType`, `ExecutionWeaponData` | 4 absürt infaz silahının enum ve statik veri tanımları |
| [WeaponData.cs](../../Assets/_Project/Scripts/Butcher/WeaponData.cs) | 93 | `FingerMonster.Data` | `WeaponData : ScriptableObject` | Silah prefab, el yerleşim ofsetleri, saldırı animasyon ve ses verisi |
| [GhostScareAndThrowSystem.cs](../../Assets/_Project/Scripts/Ghost/GhostScareAndThrowSystem.cs) | 569 | `FingerMonster.Gameplay` | `GhostScareAndThrowSystem : MonoBehaviour` | Hayaletin "Buuu!" korkutma vinyeti ve balistik nesne fırlatma görselleri |
| [ThrowableItemData.cs](../../Assets/_Project/Scripts/Ghost/ThrowableItemData.cs) | 103 | `FingerMonster.Data` | `ThrowableItemData : ScriptableObject` | Fırlatılabilir çizgi film nesnelerinin (pasta, domates, kağıt, sosis) balistik verisi |
| [CharacterEmoteData.cs](../../Assets/_Project/Scripts/Emotes/CharacterEmoteData.cs) | 98 | `FingerMonster.Player` | `EmoteSlotInfo`, `CharacterEmoteData : ScriptableObject` | 4 karakterin 5'er slotluk emote tanımları ve çalışma zamanı fallback'leri |

### 4.2.1. `Cards/`
- **`CardData`** (`FingerMonster.Gameplay`, `[CreateAssetMenu(fileName = "NewAbilityCard", menuName = "FingerMonster/Ability Card Data")]`):
  - Alanlar: `cardType`, `cardId`, `displayName`, `description`, `shortTag`, `accentColor`, `defaultWeight`, `canUseDuringCardSelection`, `canUseDuringTypewriterTurn`, `requiresTargetKey`, `requiresTargetOpponent`
  - Statik Metotlar: `GetDefinition(AbilityCardType type)`, `GetAllDefinitions()` (`CardDataCatalog` bu metotlara yönlendirir).
- **`CardSelectionController`** (`FingerMonster.Gameplay`, `Instance` Singleton):
  - Property'ler: `ActiveSafeCardIndex`, `HintedMonsterCardIndex`, `IsCurrentSelectionFogged`, `IsShowingSelection`, `IsLocalCardPanelOpen`
  - Public Metotlar: `EnsureInitialized()`, `ShowCardSelection(...)` (2 overload), `SetFoggedState(bool fogged)`, `RevealOneMonsterHint(bool applyVisualsForLocalHuman = true, int forcedMonsterIndex = -1)`, `SetNetworkClickInterceptor(Action<int> interceptor)`, `SelectCard(int index)`, `RevealNetworkCardSelection(int chosenIndex, int safeCardIndex, bool isSafe, Action onRevealComplete = null)`, `HideImmediate()`

### 4.2.2. `Butcher/`
- **`ButcherPlaceholder`** (`FingerMonster.Gameplay`):
  - Önemli Alanlar: `visualRoot`, `weaponArmPivot`, `customButcherModelPrefab`, `targetButcherHeight` (1.34f), `maxButcherWidth` (0.48f), `weaponModels`, `mixamoPoseDriverPrefab`, `idleClip`, `walkClip`, `taunt1Clip`, `taunt2Clip`, `attackClip`, `butcherAnimatorController`, `patrolRadius` (2.65f), `patrolAngularSpeed` (0.52f)
  - Sabitler (`ClampWalkablePosition` içi): `minTableRadius = 1.58f`, `seatKeepOutRadius = 0.86f`
  - Public Metotlar: `CurrentWeaponIndex` (property), `SetCustomButcherModelPrefab(GameObject modelPrefab)`, `SetButcherAnimationAssets(...)`, `EnsureKasapModelSetup()`, `CalibrateScaleToSeatedPlayers(Transform root)`, `ResumePatrol()`, `ClampWalkablePosition(Vector3 flatPos)`, `AppearAt(Transform spawnAnchor, int victimSeatIndex = 0, int weaponIndex = -1, Action onStrikeCallback = null)`, `GetOrCreateWeaponSocket()`, `EquipWeaponData(WeaponData data)`, `SpawnOrUpdateGhostPreview(ExecutionWeaponType weapon, Vector3 cornerPos, Quaternion cornerRot)`, `DestroyGhostPreview()`, `PlayWeaponExecutionEffects(ExecutionWeaponType weapon, Vector3 targetPos)`, `Hide()`, `SetupWeapons(Transform pivot, GameObject[] weapons)`
- **`ExecutionReflectionSystem`** (`FingerMonster.Gameplay`, `Instance` Singleton):
  - Property'ler: `IsExecutionActive`, `IsLocalViewerGhost`
  - Public Metotlar: `StartExecutionSequence(int victimSeatIndex, bool isLocalPlayerVictim)`, `ConfigureVisibility(bool isLocalPlayerVictim)`, `EndExecutionSequence()`, `SetupComponents(Camera reflCam, Renderer reflSurface, Light spot, Light rim)`
- **`ExecutionWeapons.cs`** (`FingerMonster.Gameplay`):
  - Enum `ExecutionWeaponType`: `GiantMeatMallet = 0`, `FlyingCastIronPan = 1`, `SausageWhip = 2`, `CreamPieLauncher = 3`
  - Sınıf `ExecutionWeaponData`: `GetData(ExecutionWeaponType type)`, `GetWeaponData(ExecutionWeaponType type)`
- **`WeaponData`** (`FingerMonster.Data`, `[CreateAssetMenu(fileName = "WeaponData", menuName = "FingerMonster/Weapon Data")]`):
  - Alanlar: `weaponType`, `displayName`, `weaponPrefab`, `localPositionOffset`, `localEulerOffset`, `localScale`, `attackTriggerName`, `attackSpeedMultiplier`, `hitSfx`, `vfxColor`, `victimReactionType`, `funnyHitMessage`
  - Statik Metot: `CreateRuntimeDefault(ExecutionWeaponType type)`

### 4.2.3. `Ghost/` ve `Emotes/`
- **`GhostScareAndThrowSystem`** (`FingerMonster.Gameplay`, `Instance` Singleton):
  - Sabitler: `DefaultScareDuration = 0.6f`, `DefaultDecalDuration = 8.0f`, `DefaultSplatDuration = 3.0f`, `NormalVignetteAlpha = 0.65f`, `ReducedVignetteAlpha = 0.35f`, `NormalScreenSplatAlpha = 0.45f`, `ReducedScreenSplatAlpha = 0.25f`
  - Public Metotlar: `CanScareTarget(Vector3 ghostPos, Transform livingTarget, float maxRange = 3.0f)` (`static`), `ExecuteScareVisuals(...)`, `LaunchThrowableVisual(...)`, `ClearAllActiveEffects()`
- **`ThrowableItemData`** (`FingerMonster.Data`, `[CreateAssetMenu(fileName = "ThrowableItemData", menuName = "FingerMonster/Throwable Item Data")]`):
  - Alanlar: `itemType`, `displayName`, `shortLabel`, `primaryColor`, `splatColor`, `arcHeight`, `flightDuration`, `splatDuration`, `hitRadius`
  - Statik Metot: `CreateRuntimeDefaultList()`
- **`CharacterEmoteData`** (`FingerMonster.Player`, `[CreateAssetMenu(fileName = "EmoteData_Character", menuName = "FingerMonster/Character Emote Data")]`):
  - `EmoteSlotInfo`: `DisplayName`, `PopupText`, `PopupColor`, `ExpectedMixamoFile`, `RecommendedMixamoSearch`
  - `CharacterEmoteData`: `Archetype`, `CharacterName`, `PersonalityTag`, `Slots` (5 eleman), `GetSlot(int slotIndex1Based)`, `CreateRuntimeFallback(CharacterArchetype archetype)` (`static`), `GetDefaultSlotsForArchetype(CharacterArchetype archetype)` (`static`)

---

## 4.3. Ağ Altyapısı ve Yapay Zekâ (`Networking/`, `AI/`)

| Dosya | Satır | Namespace | Ana Tip(ler) | Sorumluluk Özeti |
| :--- | :---: | :--- | :--- | :--- |
| [LANDiscovery.cs](../../Assets/_Project/Scripts/Networking/LANDiscovery.cs) | 321 | `FingerMonster.Networking` | `DiscoveredRoom`, `LANDiscovery : MonoBehaviour` | UDP port `47777` üzerinden yerel ağ oda yayını ve dinleme |
| [NetworkGameManager.cs](../../Assets/_Project/Scripts/Networking/NetworkGameManager.cs) | 2739 | `FingerMonster.Networking` | `NetworkGameManager : NetworkBehaviour` | Server-Authoritative çok oyunculu oyun durumu, RPC'ler ve senkronizasyon |
| [NetworkPlayerSlot.cs](../../Assets/_Project/Scripts/Networking/NetworkPlayerSlot.cs) | 121 | `FingerMonster.Networking` | `NetworkPlayerSlot` (`struct`, `INetworkSerializable`) | Ağ üzerinden senkronize edilen oyuncu koltuk verisi |
| [NetworkRoomManager.cs](../../Assets/_Project/Scripts/Networking/NetworkRoomManager.cs) | 251 | `FingerMonster.Networking` | `NetworkRoomManager : MonoBehaviour` | Host/Client başlatma, bağlantı koparma ve lobi koltuk yönetimi |
| [AIBrain.cs](../../Assets/_Project/Scripts/AI/AIBrain.cs) | 995 | `FingerMonster.AI` | `BotGhostActiveChoice`, `BotGhostDecision`, `AIBrain` | Botların kart seçimi, tuş tahmini, yetenek kartı, hayalet ve emote kararları |
| [AIPlayerController.cs](../../Assets/_Project/Scripts/AI/AIPlayerController.cs) | 132 | `FingerMonster.AI` | `AIPlayerController : MonoBehaviour` | Koltuk bazlı bot durumu takibi (`isBotPlayer[4]`) |
| [BotData.cs](../../Assets/_Project/Scripts/AI/BotData.cs) | 86 | `FingerMonster.AI` | `BotPersonality`, `BotDifficulty`, `BotProfile` | Bot kişilik/zorluk enumları ve profil oluşturucu |

### 4.3.1. `LANDiscovery.cs` & `NetworkRoomManager.cs` & `NetworkPlayerSlot.cs`
- **`LANDiscovery`** (`Instance` Singleton):
  - Alanlar: `discoveryPort` (47777), `broadcastInterval` (1.0f), `roomTimeout` (3.5f), `DiscoveredRooms` (property), `OnRoomsUpdated` (event)
  - Public Metotlar: `StartBroadcasting(string roomName, ushort gamePort, int currentPlayersCount, int maxPlayersCount = 4)`, `UpdatePlayerCount(int count)`, `StopBroadcasting()`, `StartListening()`, `StopListening()`, `StopDiscovery()`, `GetDiscoveredRooms()`
- **`NetworkRoomManager`** (`Instance` Singleton):
  - Alanlar/Property'ler: `defaultPort` (7777), `fillEmptySeatsWithBots` (true), `FillEmptySeatsWithBots`, `LocalPlayerName`, `CurrentRoomName`, `PendingSlots`, `PendingTrapCount`, `OnJoinedLobby`, `OnLeftLobby`
  - Public Metotlar: `StartHost(string roomName, ushort port = 0)`, `StartClient(string ipAddress, ushort port = 0)`, `Disconnect()`, `StartGameMatch(int trapCount = 6)`
- **`NetworkPlayerSlot`** (`INetworkSerializable, IEquatable<NetworkPlayerSlot>`):
  - Alanlar: `ClientId`, `SeatIndex`, `ArchetypeIndex`, `PlayerName` (`FixedString64Bytes`), `IsBot`, `IsReady`, `IsAlive`, `LeftHandIntact`, `RightHandIntact`, `RemainingHands`, `Score`, `AvatarColor`, `AbilityCardCount`, `HasActiveShield`, `IsFogged`, `IsGhost`
  - Metotlar: `NetworkSerialize<T>(BufferSerializer<T> serializer)`, `Equals(NetworkPlayerSlot other)`, `Equals(object obj)`, `GetHashCode()`

### 4.3.2. `NetworkGameManager.cs` (`Assets/_Project/Scripts/Networking/NetworkGameManager.cs`)
- **Namespace:** `FingerMonster.Networking`
- **Tip:** `public class NetworkGameManager : NetworkBehaviour` (`Instance` Singleton)
- **Ağ Değişkenleri (`NetworkVariable` / `NetworkList`):**
  - `ActivePlayerIndex` (`NetworkVariable<int>`)
  - `CurrentRound` (`NetworkVariable<int>`)
  - `RemainingSafeKeys` (`NetworkVariable<int>`)
  - `CurrentNetworkState` (`NetworkVariable<GameState>`)
  - `PlayerSlots` (`NetworkList<NetworkPlayerSlot>`)
- **Sunucu Sabitleri:** `GhostScareCooldown = 6.0f`, `GhostScareRange = 3.0f`, `GhostScareImmunityDuration = 5.0f`, `GhostThrowCooldown = 5.0f`, `GhostThrowMaxRange = 12.0f`
- **Yerel İstemci Property & Event'leri:** `LocalAbilityHand`, `IsWaitingForLocalXRayTarget`, `IsLocalGhost`, `LocalSoulEnergy`, `IsGhostWindowOpen`, `IsWaitingForGhostCurseTarget`, `LocalCursedKeyIndex`, `LocalSeatIndex`, `IsLocalPlayerTurn`; Event'ler: `OnLocalAbilityHandUpdated`, `OnNetworkAbilityCardPlayed`, `OnLocalBecameGhost`, `OnLocalSoulEnergyUpdated`, `OnGhostWindowUpdated`, `OnGhostCurseConfirmedForGhosts`, `OnNetworkGhostEffectTriggered`
- **Yaşam Döngüsü ve Public API:** `OnNetworkSpawn()`, `OnNetworkDespawn()`, `OnDestroy()`, `DetermineLocalSeat()`, `ConfigureLocalCameraForSeat(int seatIndex)`, `ServerInitializeMatch(List<NetworkPlayerSlot> initialSlots, int trapCount = 6)`, `ServerStartNewRound(int trapCount = 6)`, `LocalRequestPressKey(int keyIndex)`, `LocalRequestPlayAbilityCard(int handSlotIndex, int targetIndex = -1)`, `ConfirmLocalXRayKeyTarget(int keyIndex)`, `CancelLocalXRayKeyTarget()`, `LocalStartGhostCurseSelection()`, `LocalCancelGhostCurseSelection()`, `LocalRequestGhostCurseKey(int keyIndex)`, `LocalRequestGhostSelectWeapon(int weaponIndex)`, `LocalRequestGhostEffect(GhostEffectType effectType)`, `RequestGhostScareClient()`, `RequestGhostThrowClient(ThrowableItemType item, Vector3 origin, Vector3 direction)`, `SubmitLocalGhostTransform(Vector3 pos, float yaw)`, `LocalGhostReturnToMainMenu()`
- **ServerRpc Metotları (`[ServerRpc(RequireOwnership = false)]`):**
  - `RequestSelectCardServerRpc(int seatIndex, int cardIndex, ServerRpcParams rpcParams = default)`
  - `RequestPlayAbilityCardServerRpc(int seatIndex, int handSlotIndex, int targetIndex, ServerRpcParams rpcParams = default)`
  - `RequestPressKeyServerRpc(int seatIndex, int keyIndex, ServerRpcParams rpcParams = default)`
  - `RequestGhostCurseKeyServerRpc(int seatIndex, int keyIndex, ServerRpcParams rpcParams = default)`
  - `RequestGhostSelectWeaponServerRpc(int seatIndex, int weaponIndex, ServerRpcParams rpcParams = default)`
  - `SubmitGhostTransformServerRpc(Vector3 pos, float yaw, ServerRpcParams rpcParams = default)`
  - `RequestGhostScareServerRpc(ServerRpcParams rpcParams = default)`
  - `RequestGhostThrowServerRpc(int itemTypeIndex, Vector3 origin, Vector3 direction, ServerRpcParams rpcParams = default)`
  - `RequestGhostEffectServerRpc(int seatIndex, int effectType, ServerRpcParams rpcParams = default)`
  - `RequestEmoteServerRpc(int seatIndex, int emoteIndex)`
  - `RequestEmoteSlotServerRpc(int senderSeat, int slotIndex, int targetSeat)`
- **ClientRpc Metotları (`[ClientRpc]`):**
  - `OnRoundStartedClientRpc`, `OnCardSelectionStartedClientRpc`, `OnCardRevealedClientRpc`, `OnTurnStartedClientRpc`, `OnBotAimingClientRpc`, `OnKeyResolvedClientRpc`, `OnButcherSequenceClientRpc`, `OnEmoteExecutedClientRpc`, `OnEmoteSlotExecutedClientRpc`, `OnRoundEndedClientRpc`, `OnGameOverClientRpc`, `OnSeatTakenOverClientRpc`, `SyncGhostSoulEnergyTargetClientRpc`, `OnSoulEnergySyncedClientRpc`, `BroadcastGhostTransformClientRpc`, `OnGhostScareExecutedClientRpc`, `OnGhostThrowExecutedClientRpc`, `OnGhostWeaponPreviewSyncedClientRpc`, `DestroyGhostPreviewClientRpc`, `OnGhostWindowChangedClientRpc`, `OnCursedKeySyncedToGhostsClientRpc`, `OnMachineCurseShakeClientRpc`, `OnGhostCurseExplodedClientRpc`, `OnGhostEffectExecutedClientRpc`, `SyncLocalHandTargetClientRpc`, `OnPossessResultTargetClientRpc`, `OnXRayResultTargetClientRpc`, `OnAbilityCardPlayedClientRpc`, `OnShieldBlockedTrapClientRpc`

### 4.3.3. `AI/`
- **`AIBrain`** (`FingerMonster.AI`):
  - Yardımcı Tipler: `BotGhostActiveChoice` (enum), `BotGhostDecision` (struct: `Choice`, `TargetSeat`, `WeaponIndex`, `EffectType`, `ThrowItem`)
  - Public Metotlar: `RegisterBotProfile(int slotIndex, BotProfile profile)`, `GetProfile(int slotIndex)`, `GetEmoteChanceForDifficulty(BotDifficulty difficulty)`, `PickReactionSlot(BotPersonality personality, bool isRivalFailure)`, `TriggerBotReactionEmote(...)`, `DecideAndExecuteCard(int botPlayerIndex, CardSelectionController cardController)`, `DecideAndExecuteTurn(int botPlayerIndex, GameManager gameManager, Action<int, int> onKeyChosenCallback = null)`, `DecideBotAbilityCardBeforeFate(...)`, `DecideBotAbilityCardBeforeKey(...)`, `DecideGhostAction(...)`, `DecideBotGhostActiveAction(...)` (`static`), `ComputeBotGhostRoamTarget(...)` (`static`)
- **`AIPlayerController`** (`Instance` Singleton): `SetPlayerAsBot(int playerIndex, bool isBot)`, `IsBot(int playerIndex)`
- **`BotData.cs`**: `BotPersonality` (`Cesur`, `Urkek`, `Sinsi`, `Sovmen`), `BotDifficulty` (`Kolay`, `Normal`, `Zor`), `BotProfile` (`MapArchetypeToPersonality`, `GetPersonalityLabel`, `ForCharacterSlot`, `GenerateRandom`)

---

## 4.4. Ses, Kamera ve Masaüstü Oynanış (`Audio/`, `CameraSystem/`, `Gameplay/`)

| Dosya | Satır | Namespace | Ana Tip(ler) | Sorumluluk Özeti |
| :--- | :---: | :--- | :--- | :--- |
| [AudioManager.cs](../../Assets/_Project/Scripts/Audio/AudioManager.cs) | 555 | `FingerMonster.Audio` | `AudioManager : MonoBehaviour` | 2D/3D spatial ses havuzu, düşük geçiren filtre, kasap/hayalet/daktilo sesleri |
| [SoundEffectsGenerator.cs](../../Assets/_Project/Scripts/Audio/SoundEffectsGenerator.cs) | 272 | `FingerMonster.Audio` | `SoundEffectsGenerator` (`static class`) | 44.1 kHz prosedürel `AudioClip` sentezleyici (11 farklı klip) |
| [PlayerCameraController.cs](../../Assets/_Project/Scripts/CameraSystem/PlayerCameraController.cs) | 659 | `FingerMonster.Player` | `PlayerCameraController : MonoBehaviour` | Sabit koltuk POV, emote pan, hayalet serbest uçuş ve izleyici yörünge kamerası |
| [CartoonFXManager.cs](../../Assets/_Project/Scripts/Gameplay/CartoonFXManager.cs) | 291 | `FingerMonster.Gameplay` | `CartoonFXManager : MonoBehaviour` | Kamera sarsıntısı, boya/kan sıçraması, yüzen çizgi roman yazıları ve konfeti |
| [FlickeringBulb.cs](../../Assets/_Project/Scripts/Gameplay/FlickeringBulb.cs) | 128 | `FingerMonster.Gameplay` | `FlickeringBulb : MonoBehaviour` | Titreyen ve sallanan tavan ampulü efekti |
| [RoomLightingController.cs](../../Assets/_Project/Scripts/Gameplay/RoomLightingController.cs) | 175 | `FingerMonster.Gameplay` | `RoomLightingController : MonoBehaviour` | Masa spotu, ortam ışığı ve infaz modu kırmızı aydınlatma geçişleri |
| [TypewriterKey.cs](../../Assets/_Project/Scripts/Gameplay/TypewriterKey.cs) | 338 | `FingerMonster.Gameplay` | `TypewriterKey : MonoBehaviour` | Tekil daktilo tuşu tıklama, hover, X-Ray ve lanet görselleştirmesi |
| [TypewriterMachine.cs](../../Assets/_Project/Scripts/Gameplay/TypewriterMachine.cs) | 262 | `FingerMonster.Gameplay` | `TypewriterMachine : MonoBehaviour` | 20 tuşlu daktilo yönetimi, şaryo ilerlemesi ve makine sarsıntısı |
| [TypewriterPaper.cs](../../Assets/_Project/Scripts/Gameplay/TypewriterPaper.cs) | 158 | `FingerMonster.Gameplay` | `TypewriterPaper : MonoBehaviour` | Daktilo kâğıdına harf ekleme ve kâğıdı uçurma animasyonu |

### 4.4.1. `Audio/`
- **`AudioManager`** (`Instance` Singleton):
  - Property'ler: `MasterVolume`, `SfxVolume`, `MusicVolume`
  - Public Metotlar: `PlayTypewriterClick()`, `PlayTrapTriggered()`, `PlayTrapSting()`, `PlayBell()`, `PlayButcherStinger()`, `PlayUIClick()`, `PlayMalletBonk()`, `PlayPanDong()`, `PlayWhipSlap()`, `PlayPieSplat()`, `PlayButcherGiggle()`, `PlayGhostCurse(Vector3? worldPos = null)`, `PlayGhostCurseExplosion(Vector3? worldPos = null)`, `PlayGhostEffect(GhostEffectType effectType, Vector3? worldPos = null)`, `PlayGhostScare(Vector3 worldPos)`, `PlayGhostThrow(Vector3 worldPos)`, `PlayGhostSplat(int itemTypeIndex, Vector3 worldPos)`, `PlayGhostSplat(ThrowableItemType item, Vector3 worldPos)`, `PlayButcherWeaponHit(ExecutionWeaponType weapon, Vector3 worldPos)`, `PlayGhostSpatial(AudioClip clip, Vector3 worldPos, float volumeScale = 1f, float minDist = 1.2f, float maxDist = 14f)`, `StartCartoonSuspenseLoop()`, `StopCartoonSuspenseLoop()`, `SetExecutionAudioFilter(bool muffled)`, `SetMasterVolume(float vol)`, `SetSfxVolume(float vol)`, `SetMusicVolume(float vol)`
- **`SoundEffectsGenerator`** (`private const int SampleRate = 44100`):
  - Statik Üreticiler: `CreateTypewriterClickClip()`, `CreateTrapStingerClip()`, `CreateBellDingClip()`, `CreateButcherStingerClip()`, `CreateMalletBonkClip()`, `CreatePanDongClip()`, `CreateWhipSlapClip()`, `CreatePieSplatClip()`, `CreateButcherGiggleClip()`, `CreateCartoonSuspenseMusicLoop()`, `CreateUIClickClip()`

### 4.4.2. `CameraSystem/`
- **`PlayerCameraController`** (`FingerMonster.Player`, `Instance` Singleton):
  - Property & Event'ler: `IsEmotePanActive`, `IsGhostSpectatorMode`, `GhostSpectatingSeatIndex`, `CurrentGhostMode`, `CurrentGhostWorldPosition`, `CurrentGhostWorldRotation`, `OnGhostModeChanged`
  - Public Metotlar: `ApplyFixedTransform()`, `SetLocalSeat(int seatIndex, bool snapImmediately = false)`, `TriggerEmotePan(int targetSeatIndex, float duration = 1.8f)`, `CalculateRelativeYawOffset(int fromSeat, int toSeat)` (`static`), `SetTarget(Transform newTarget, bool snapImmediately = false)`, `SetExternalOverride(Transform overrideTarget)`, `ClearExternalOverride()`, `SetGhostSpectatorMode(bool active, int startSeat = 0, float orbitSpeed = 12f)`, `TriggerGentleScareShake(float duration = 0.6f, float intensity = 0.045f)`, `CycleGhostSpectatorTarget(int direction, Func<int, bool> isSeatAlive = null)`

### 4.4.3. `Gameplay/`
- **`CartoonFXManager`** (`Instance` Singleton): `PlaySafeKeyFX`, `PlayTrapSnapFX`, `PlayButcherStrikeFX`, `ShakeCamera`, `SpawnPaintSplat`, `SpawnRealisticBloodCluster`, `SpawnFloatingComicText`, `SpawnComicPopup`, `PlayConfetti`
- **`FlickeringBulb`**: `SetMuted(bool muted)`
- **`RoomLightingController`** (`Instance` Singleton): `SetExecutionMode(bool active)`, `SetNormalState()`, `SetTurnState(Color playerTint)`, `SetButcherState()`, `SetupLights(Light spot, Light ambient, Light dramatic, FlickeringBulb bulb = null)`
- **`TypewriterKey`** (`IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler`):
  - Property'ler: `KeyIndex`, `KeyChar`, `IsPressed`, `HasXRayReveal`, `XRayIsTrap`, `IsCursed`
  - Public Metotlar: `Initialize(int index, char character)`, `SetHovered(bool hovered)`, `ApplyXRayVisual(bool isTrap)`, `ClearXRayVisual()`, `SetCursedVisual(bool cursed, bool visibleToLocalGhost)`, `OnPointerEnter`, `OnPointerExit`, `OnPointerClick`, `HandleHover(bool hovered)`, `HandleClick()`, `AnimatePress(bool isTrap)`, `ResetKey()`
- **`TypewriterMachine`** (`private const int MaxCarriageSteps = 10`):
  - Property'ler: `Paper`, `Keys`
  - Public Metotlar: `SetupKeys(string alphabetLetters)`, `PressKey(int keyIndex, bool isTrap)`, `ResetAllKeys()`, `ClearAllXRayVisuals()`, `ClearAllCurseVisuals()`, `ShakeMachine(float duration = 0.4f, float intensity = 0.018f)`, `GetKey(int index)`, `SetKeysList(List<TypewriterKey> newKeys)`, `SetPaper(TypewriterPaper newPaper)`, `SetCarriage(Transform carriage)`
- **`TypewriterPaper`**:
  - Public Metotlar: `AppendCharacter(char c)`, `ClearPaper()`, `PlayPaperFlyAway()`

---

> [← Önceki Modül: Modül 2/5](./02-lifecycle-and-flows.md) | [Ana Mimari İndeksi](../../UYGULAMA_KAPSAMLI_REHBERI_VE_GUVENLIK_RAPORU.md) | [Sonraki Modül: Modül 4/5 →](./04-inventory-screens-and-components.md)
