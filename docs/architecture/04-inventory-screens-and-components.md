> ← [Ana Mimari İndeksine Dön](../../UYGULAMA_KAPSAMLI_REHBERI_VE_GUVENLIK_RAPORU.md) | **Modül 4/5:** Oyuncu, Karakter, El Kopma, Sıra, UI/HUD ve Editor Araçları Envanteri (Bölüm 4.5–4.6)

# Modül 4: Oyuncu, UI/HUD ve Editor Araçları Kod Envanteri (Bölüm 4.5–4.6)

Bu belge, `Assets/_Project/Scripts/Players/`, `Assets/_Project/Scripts/UI/` ve `Assets/_Project/Scripts/Editor/` klasörlerindeki tüm C# dosyalarının gerçek sınıf, enum, struct, sabit, alan ve public metot imzalarını içerir.

---

## 4.5. Oyuncu, Karakter, El Kopma ve Sıra Bileşenleri (`Players/`)

| Dosya | Satır | Namespace | Ana Tip(ler) | Sorumluluk Özeti |
| :--- | :---: | :--- | :--- | :--- |
| [CharacterDefinition.cs](../../Assets/_Project/Scripts/Players/CharacterDefinition.cs) | 164 | `FingerMonster.Player` | `CharacterArchetype`, `EmoteType`, `CharacterDefinition` | 4 karakter arketipi (Kabadayı, Korkak, Sinsi, Şovmen) ve 16 emote tanımı |
| [CharacterAnimatorDriver.cs](../../Assets/_Project/Scripts/Players/CharacterAnimatorDriver.cs) | 888 | `FingerMonster.Player` | `CharacterAnimatorDriver : MonoBehaviour` | Humanoid Animator, IK hedefleme, prosedürel kemik fallback ve emote/infaz animasyonları |
| [CharacterIKBridge.cs](../../Assets/_Project/Scripts/Players/CharacterIKBridge.cs) | 36 | `FingerMonster.Player` | `CharacterIKBridge : MonoBehaviour` | `OnAnimatorIK(int)` çağrılarını `CharacterAnimatorDriver.HandleAnimatorIK` metoduna köprüler |
| [HandSeverSystem.cs](../../Assets/_Project/Scripts/Players/HandSeverSystem.cs) | 296 | `FingerMonster.Player` | `HandSeverSystem : MonoBehaviour` | El kemiklerini min `0.0001f` ölçeğe çekerek gizler, sargı açar ve kopan eli fırlatır |
| [SeveredHandPhysics.cs](../../Assets/_Project/Scripts/Players/SeveredHandPhysics.cs) | 172 | `FingerMonster.Player` | `SeveredHandPhysics : MonoBehaviour` | Kopan elin fiziksel fırlatılması, yuvarlanması ve sıfırlanması |
| [PlayerData.cs](../../Assets/_Project/Scripts/Players/PlayerData.cs) | 264 | `FingerMonster.Player` | `PlayerData` | Oyuncu kimliği, eller, skor, yetenek kartları, hayalet durumu ve ruh enerjisi modeli |
| [PlayerInputHandler.cs](../../Assets/_Project/Scripts/Players/PlayerInputHandler.cs) | 308 | `FingerMonster.Player` | `PlayerInputHandler : MonoBehaviour` | 3D raycast ile daktilo tuşu tıklama/hover yönetimi |
| [PlayerSeat.cs](../../Assets/_Project/Scripts/Players/PlayerSeat.cs) | 215 | `FingerMonster.Player` | `PlayerSeat : MonoBehaviour` | Masa koltuğu kamera/kasap çapa noktaları ve karakter prefab ataması |
| [PlayerVisualAvatar.cs](../../Assets/_Project/Scripts/Players/PlayerVisualAvatar.cs) | 847 | `FingerMonster.Player` | `PlayerVisualAvatar : MonoBehaviour` | Oyuncu avatarı, hayalet şeffaflığı/yakınlık solması, korku tepkisi ve splat görselleri |
| [TurnController.cs](../../Assets/_Project/Scripts/Players/TurnController.cs) | 142 | `FingerMonster.Player` | `TurnController : MonoBehaviour` | Sıra ilerletme, yön tersine çevirme (`IsReversed`) ve hayatta kalan sorguları |

### 4.5.1. `CharacterDefinition.cs` (`Assets/_Project/Scripts/Players/CharacterDefinition.cs`)
- **Namespace:** `FingerMonster.Player`
- **Enumlar:**
  - `CharacterArchetype`: `Kabadayi`, `Korkak`, `Sinsi`, `Sovmen`
  - `EmoteType` (0–15): `Kabadayi_ChestThump`, `Kabadayi_BicepFlex`, `Kabadayi_PointThreat`, `Kabadayi_BoomingLaugh`, `Korkak_TrembleBite`, `Korkak_DuckTable`, `Korkak_FakeCry`, `Korkak_ChickenMock`, `Sinsi_FingerWag`, `Sinsi_SlowClap`, `Sinsi_TongueOut`, `Sinsi_HandInspect`, `Sovmen_HatBow`, `Sovmen_JazzHands`, `Sovmen_ApplausePlease`, `Sovmen_VictoryPose`
- **Sınıf `CharacterDefinition`:**
  - Alanlar: `Archetype`, `DisplayName`, `PersonalityTag`, `Title`, `Description`, `ModelAssetName`, `PrefabAssetPath`, `PrimaryColor`, `AccentColor`, `AvailableEmotes` (`EmoteType[]`)
  - Property: `FullDisplayLabel`
  - Statik Metotlar: `GetDefinitionBySeat(int seatIndex)`, `GetDefinition(CharacterArchetype archetype)`

### 4.5.2. `CharacterAnimatorDriver.cs` & `CharacterIKBridge.cs`
- **`CharacterAnimatorDriver`** (`FingerMonster.Player`):
  - Alanlar/Property'ler: `Archetype`, `humanoidAnimator`, `tableRestLeftOffset`, `tableRestRightOffset`, `maxArmReach`, `hipsBone`, `torsoBone`, `headBone`, `leftEye`, `rightEye`, `leftArmPivot`, `rightArmPivot`, `accessoryRoot`, `leftHandModel`, `rightHandModel`, `leftWristStump`, `rightWristStump`, `IsPlayingEmote`, `HumanoidAnimator`
  - Public Metotlar: `SetAnimator(Animator animator)`, `SetupBones(...)`, `SetupHands(...)`, `OnHandSevered(PlayerHand hand)`, `ResetAnimatorState()`, `HandleAnimatorIK(int layerIndex)`, `PlayThinkingAnimation(float duration, Vector3? targetLookWorld = null)`, `PlayPressKeyAnimation(int remainingHandsCount, Action onImpactCallback = null, Vector3? keyWorldPosition = null)`, `PlayJoyReliefAnimation()`, `PlayShockAnimation()`, `PlayEmoteSlot(int slotIndex1Based, Vector3? targetWorldPos = null, string overridePopupText = null, Color? overridePopupColor = null)`, `PlayEmote(EmoteType emote)`, `PlayEliminationByWeapon(int weaponIndex)`
- **`CharacterIKBridge`** (`FingerMonster.Player`):
  - Alan: `driver` (`[SerializeField]`)
  - Public Metot: `SetDriver(CharacterAnimatorDriver targetDriver)`

### 4.5.3. `HandSeverSystem.cs` & `SeveredHandPhysics.cs`
- **`HandSeverSystem`** (`FingerMonster.Player`):
  - Alanlar: `targetAnimator`, `leftHandBone`, `rightHandBone`, `leftForeArmBone`, `rightForeArmBone`, `leftWristBandage`, `rightWristBandage`, `leftSeveredHandPrefab`, `rightSeveredHandPrefab`, `characterSkinMaterial`, `fxAccentColor`
  - Property'ler: `IsLeftHandSevered`, `IsRightHandSevered`, `RemainingHandsCount`, `LeftHandBone`, `RightHandBone`
  - Public Metotlar: `ResolveBones()`, `Configure(...)`, `SeverHand(PlayerHand hand, Color? overrideAccentColor = null)`, `ResetHands()`
  - *Not:* Kopan el kemiklerinin ölçeği NaN ve transform bozulmalarını önlemek için `0` yerine minimum `0.0001f` değerine çekilir.
- **`SeveredHandPhysics`** (`FingerMonster.Player`):
  - Alanlar: `launchUpwardForce` (2.0f), `launchHorizontalForce` (1.5f), `tumbleTorque` (6.5f), `twitchDuration` (4.0f)
  - Public Metotlar: `Launch(Vector3 launchDirection, Color playerColor)`, `ResetHand(Transform originalParent, Vector3 localPos, Quaternion localRot)`

### 4.5.4. `PlayerData.cs`, `PlayerInputHandler.cs`, `PlayerSeat.cs`, `PlayerVisualAvatar.cs`, `TurnController.cs`
- **`PlayerData`** (`FingerMonster.Player`):
  - Alanlar/Property'ler: `PlayerId`, `PlayerName`, `Archetype`, `PersonalityTag`, `PlayerColor`, `LeftHand`, `RightHand`, `Score`, `AbilityCards`, `HasActiveShield`, `IsFogged`, `UsedSkipLastTurn`, `UsedAbilityThisTurn`, `UsedSkipThisTurn`, `IsAlive`, `IsGhost`, `SoulEnergy`, `LastGhostEffectTime`, `RemainingHandsCount`
  - Kurucu & Metotlar: `PlayerData(int id, string name, Color color, CharacterArchetype archetype = CharacterArchetype.Kabadayi, string personalityTag = "")`, `TryAddAbilityCard(AbilityCardType card, int maxHandSize = 2)`, `RemoveAbilityCardAt(int slotIndex)`, `RemoveFirstAbilityCardOfType(AbilityCardType type)`, `HasAbilityCard(AbilityCardType type)`, `BeginTurnAbilityReset()`, `EndTurnAbilityUpdate()`, `ClearAbilityCardsAndEffects()`, `InitializeGhostEnergy(int startEnergy, int maxEnergy)`, `AddSoulEnergy(int amount, int maxEnergy)`, `TrySpendSoulEnergy(int cost)`, `SeverRandomHand()`, `ResetHands()`, `RestoreHands()`
- **`PlayerInputHandler`** (`FingerMonster.Player`):
  - Alanlar: `keyLayerMask`, `raycastDistance` (20.0f)
  - Statik Event/Callback: `public static Func<int, bool> OnKeyClickIntercept;`
- **`PlayerSeat`** (`FingerMonster.Player`):
  - Property'ler: `PlayerIndex`, `CameraMountPoint`, `ButcherSpawnPoint`, `Avatar`
  - Public Metotlar: `SetupSeat(int index, Color playerColor)`, `AssignCharacterPrefab(GameObject prefab, Color playerColor)`, `SetAvatar(PlayerVisualAvatar newAvatar)`, `SetHighlight(bool isActive)`
- **`PlayerVisualAvatar`** (`FingerMonster.Player`):
  - Property'ler: `Archetype`, `AnimatorDriver`, `HandSever`
  - Public Metotlar: `SetPlayerColor(Color color)`, `SeverHand(PlayerHand hand)`, `TriggerEmote(EmoteType emote)`, `TriggerEmoteSlot(int slotIndex1Based, Vector3? targetWorldPos = null, CharacterEmoteData emoteData = null)`, `PlayPressKey(...)`, `PlayJoyRelief()`, `PlayThinking(...)`, `PlayEliminatedByWeapon(int weaponIndex)`, `PlayEliminatedAnimation()`, `ResetAvatar()`, `SetGhostMode(bool active)`, `SetGhostFreeFlightTransform(Vector3 worldPos, Quaternion worldRot)`, `UpdateGhostProximityFade(Vector3? localLivingCameraPos, Vector3 tableCenter, float fadeDistance = 1.0f, float minAlpha = 0.15f)`, `PlayScareFlinch(bool reduced)`, `ShowCharacterSplat(Color splatColor, float duration = 3f)`, `ClearCharacterSplats()`
- **`TurnController`** (`FingerMonster.Player`):
  - Property'ler: `ActivePlayerIndex`, `IsReversed`
  - Public Metotlar: `ResetTurn(int startingIndex = 0)`, `SetReversed(bool reversed)`, `ToggleDirection()`, `AdvanceToNextAlivePlayer(IReadOnlyList<PlayerData> players)`, `PeekNextAlivePlayer(IReadOnlyList<PlayerData> players)`, `GetAlivePlayersCount(IReadOnlyList<PlayerData> players)`, `GetSoleSurvivor(IReadOnlyList<PlayerData> players)`, `GetHighestScorePlayer(IReadOnlyList<PlayerData> players)`

---

## 4.6. UI / HUD Bileşenleri (`UI/`) ve Editor Araçları (`Editor/`)

### 4.6.1. UI / HUD Bileşenleri (`Assets/_Project/Scripts/UI/`)

| Dosya | Satır | Namespace | Ana Tip(ler) | Sorumluluk Özeti |
| :--- | :---: | :--- | :--- | :--- |
| [MainMenuController.cs](../../Assets/_Project/Scripts/UI/MainMenuController.cs) | 821 | `FingerMonster.UI` | `MainMenuController : MonoBehaviour` | Ana menü butonları, mod/zorluk seçimi, karakter seçimi ve 3D sahne vitrini |
| [NetworkLobbyUI.cs](../../Assets/_Project/Scripts/UI/NetworkLobbyUI.cs) | 620 | `FingerMonster.UI` | `NetworkLobbyUI`, `PlayerSeatCardUI` (`private`) | LAN oda tarayıcısı, doğrudan IP katılımı, oda içi koltuk/bot/hazır yönetimi |
| [HUDController.cs](../../Assets/_Project/Scripts/UI/HUDController.cs) | 143 | `FingerMonster.UI` | `HUDController : MonoBehaviour` | Üst tur/kalan tuş barı, duyuru afişi (`Banner`) ve 4 oyuncu kartı yönetimi |
| [PlayerCardUI.cs](../../Assets/_Project/Scripts/UI/PlayerCardUI.cs) | 138 | `FingerMonster.UI` | `PlayerCardUI : MonoBehaviour` | Tekil oyuncu skor, aktif sıra vurgusu, el rozetleri ve yetenek kartı sayacı |
| [AbilityCardHUD.cs](../../Assets/_Project/Scripts/UI/AbilityCardHUD.cs) | 880 | `FingerMonster.UI` | `AbilityCardHUD`, `SlotUI` (`private`), `SlotHoverBridge` (`private`) | 2 slotlu yerel yetenek kartı HUD'ı, ikon önbelleği ve kart oynama tetikleyicisi |
| [GhostHUD.cs](../../Assets/_Project/Scripts/UI/GhostHUD.cs) | 1620 | `FingerMonster.UI` | `GhostHUD : MonoBehaviour` | Hayalet (Kasabın Yardımcısı) ruh enerjisi, lanet, silah önizleme, korkutma ve fırlatma HUD'ı |
| [EmoteWheelUI.cs](../../Assets/_Project/Scripts/UI/EmoteWheelUI.cs) | 725 | `FingerMonster.UI` | `EmoteWheelUI : MonoBehaviour` | 5 slotlu alt emote çubuğu, isteğe bağlı emote çarkı, hedef seçimi ve bekleme süresi |
| [EmoteGuidePanelUI.cs](../../Assets/_Project/Scripts/UI/EmoteGuidePanelUI.cs) | 150 | `FingerMonster.UI` | `EmoteGuidePanelUI : MonoBehaviour` | 4 karakterin emote rehberi ve hayalet kontrolleri yardım paneli |
| [PauseMenuController.cs](../../Assets/_Project/Scripts/UI/PauseMenuController.cs) | 87 | `FingerMonster.UI` | `PauseMenuController : MonoBehaviour` | Duraklatma menüsü (`IsPaused`, `TogglePause`, `Pause`, `Resume`) |
| [SettingsMenuController.cs](../../Assets/_Project/Scripts/UI/SettingsMenuController.cs) | 367 | `FingerMonster.UI` | `SettingsMenuController : MonoBehaviour` | `PlayerPrefs` tabanlı ses (Master/SFX/Music) ve hayalet efekti ayarları |
| [GameOverUIController.cs](../../Assets/_Project/Scripts/UI/GameOverUIController.cs) | 162 | `FingerMonster.UI` | `GameOverUIController : MonoBehaviour` | Oyun sonu kazanan ekranı, skor özeti ve yeniden başlat/ana menü butonları |

#### Önemli Detaylar (`UI/`):
- **`MainMenuController`**: `SelectCharacter(int index)`, `RefreshCharacterCards()`, `EnhanceMainMenuLayoutAndShowcase()`, `EnsureMainMenu3DShowcase()`
- **`NetworkLobbyUI`**: `OpenLobby()`, `HideLobby()`, `ConnectToTargetRoom(string ip, ushort port, string pName)`, `RefreshRoomUI()`
- **`HUDController`**: `SetupHUD(IReadOnlyList<PlayerData> players)`, `UpdateRoundInfo(int roundNumber, int remainingSafeKeys)`, `SetPromptText(string text)`, `UpdateTurn(PlayerData activePlayer, IReadOnlyList<PlayerData> players)`, `RefreshAllCards(IReadOnlyList<PlayerData> players, int activePlayerId)`, `ShowBanner(string message, Color color, float duration = 2.0f)`
- **`PlayerCardUI`**: `Initialize(PlayerData player)`, `UpdateDisplay(PlayerData player, bool isCurrentTurn)`
- **`AbilityCardHUD`** (`Instance` Singleton; `SlotCount = 2`, `AnnouncementDuration = 1.8f`): `EnsureInitialized()`, `TryPlaySlot(int slotIndex)`, `RefreshDisplay()`
- **`GhostHUD`** (`Instance` Singleton; `MaxSoulSlots = 3`, `CurseEnergyCost = 2`, `EffectEnergyCost = 1`, `DefaultEffectCooldown = 3.0f`, `ScareEnergyCost = 1`, `DefaultScareCooldown = 6.0f`, `ThrowEnergyCost = 1`, `DefaultThrowCooldown = 5.0f`):
  - Event'ler: `OnScareRequested`, `OnThrowRequested`
  - Property & Metotlar: `SelectedThrowable`, `EnsureInitialized()`, `ResetForNewMatch()`, `EnsureCanvasScaler()`, `GetSpectatedCharacterDisplayName(int seat)`, `CheckPanelOverlaps(out string report)`, `SelectThrowable(ThrowableItemType itemType)`, `NotifyScareCooldownStarted()`, `NotifyThrowCooldownStarted()`
- **`EmoteWheelUI`** (`Instance` Singleton):
  - Property'ler: `IsOnCooldown`, `SelectedTargetSeatIndex`
  - Public Metotlar: `SetCharacterEmoteDatas(CharacterEmoteData[] datas)`, `GetEmoteData(CharacterArchetype archetype)`, `ConfigureForSeat(int seatIndex, CharacterArchetype archetype)`, `ConfigureForArchetype(CharacterArchetype archetype)`, `CycleTarget(int step)`, `GetLivingRivalSeats()`, `TryExecuteEmoteSlot(int slotIndex1Based)`, `ExecuteLocalEmoteSlot(int senderSeat, int slotIndex1Based, int targetSeat)`, `ShowIncomingEmoteNotification(int senderSeat, int targetSeat, int slotIndex1Based, CharacterEmoteData senderData = null)`
- **`EmoteGuidePanelUI`** (`Instance` Singleton): `IsOpen`, `SetEmoteDatas(CharacterEmoteData[] datas)`, `TogglePanel()`, `OpenPanel()`, `ClosePanel()`, `PopulateGuide()`
- **`PauseMenuController`**: `IsPaused`, `TogglePause()`, `Pause()`, `Resume()`
- **`SettingsMenuController`**:
  - `PlayerPrefs` Sabitleri: `FingerMonster_MasterVol`, `FingerMonster_SfxVol`, `FingerMonster_MusicVol`, `FingerMonster_GhostEffects`, `FingerMonster_ReduceGhostEffects`
  - Statik Sorgular: `AreGhostEffectsEnabled()`, `AreGhostEffectsReduced()`
  - Public Metotlar: `OpenSettings()`, `CloseSettings()`, `SyncSlidersFromAudio()`, `AdjustMasterStep(float delta)`, `AdjustSfxStep(float delta)`, `AdjustMusicStep(float delta)`
- **`GameOverUIController`**: `ShowGameOver(PlayerData winner, IReadOnlyList<PlayerData> allPlayers, string overrideScoreRecap = null)`, `Hide()`

---

### 4.6.2. Editor Araçları (`Assets/_Project/Scripts/Editor/`)

| Dosya | Satır | Namespace | Ana Tip(ler) | Menü / Araç Özeti |
| :--- | :---: | :--- | :--- | :--- |
| [SceneSetupTool.cs](../../Assets/_Project/Scripts/Editor/SceneSetupTool.cs) | 1979 | `FingerMonster.Editor` | `SceneSetupTool` (`static class`) | `Game.unity` ve `MainMenu.unity` sahne kurulumu, URP materyal dönüşümü ve kasap animasyon senkronizasyonu |
| [BodyguardSetupTool.cs](../../Assets/_Project/Scripts/Editor/BodyguardSetupTool.cs) | 1880 | `FingerMonster.Editor` | `BodyguardSetupTool` (`static class`), `CharacterSpec`, `KeyframePose` | Bodyguard karakter prefabları, Mixamo animasyon remap, `BodyguardAnimator.controller` ve `EmoteData` üretimi |
| [WeaponOffsetTool.cs](../../Assets/_Project/Scripts/Editor/WeaponOffsetTool.cs) | 191 | `FingerMonster.Editor` | `WeaponOffsetTool : EditorWindow` | Kasap silahlarının el yerleşim ofsetlerini düzenleyen Editor penceresi (`Window/FingerMonster/Weapon Offset Tool`) |
| [BuildGameTool.cs](../../Assets/_Project/Scripts/Editor/BuildGameTool.cs) | 64 | `FingerMonster.Editor` | `BuildGameTool` (`static class`) | Windows Standalone (`.exe`) derleme aracı (`FingerMonster/Build Windows Standalone (.exe)`) |
| [GameAutomatedTest.cs](../../Assets/_Project/Scripts/Editor/GameAutomatedTest.cs) | 1718 | `FingerMonster.Editor` | `GameAutomatedTest` (`static class`) | 12 adet otomatik doğrulama ve batchmode test paketi |
| [ProjectOrganizerTool.cs](../../Assets/_Project/Scripts/Editor/ProjectOrganizerTool.cs) | 655 | `FingerMonster.Editor` | `ProjectOrganizerTool` (`static class`) | Varlıkları `Assets/_Project/` standart klasörlerine taşıma, missing referans tarama ve `MoveLog.csv` kaydı |

#### Editor Menü Komutları (`[MenuItem]`):
- **`SceneSetupTool`**:
  - `Tools/FingerMonster/Setup Game Scene` & `FingerMonster/Setup Game Scene` → `SetupGameSceneMenu()`
  - `Tools/FingerMonster/Build All Scenes (MainMenu + Game)` & `FingerMonster/Build All Scenes` → `BuildAllScenes()`
  - `Tools/FingerMonster/Sync Butcher Animations (kasap.fbx)` → `SyncExternalButcherAnimations()`
  - Diğer public yardımcılar: `Ensure3DForgeMaterialsURP()`, `BuildGameScene()`, `BuildMainMenuScene()`, `EnsureDecorativePropsInActiveScene()`
- **`BodyguardSetupTool`**:
  - `FingerMonster/1. Setup Bodyguard Characters (Full Pipeline)` → `RunFullSetup()`
  - `FingerMonster/2. Import & Remap Mixamo Animations` → `ImportAndRemapMixamoAnimations()`
  - Diğer public yardımcılar: `EnsurePrefabsExist()`, `CreateOrUpdateCharacterEmoteDatas()`
- **`WeaponOffsetTool`**: `Window/FingerMonster/Weapon Offset Tool` → `ShowWindow()`
- **`BuildGameTool`**: `FingerMonster/Build Windows Standalone (.exe)` → `BuildStandaloneWindows()`
- **`GameAutomatedTest`**:
  - `FingerMonster/Run Game Verification Test` (`RunVerification`)
  - `FingerMonster/Run Stage 2 LAN & AI Verification` (`RunStage2Verification`)
  - `FingerMonster/Run Stage 3 Characters & Weapons Verification` (`RunStage3Verification`)
  - `FingerMonster/Run Stage 4 Bodyguard Functional Verification` (`RunStage4BodyguardVerification`)
  - `FingerMonster/Run Emote Target Cycling Test` (`RunEmoteTargetCyclingTest`)
  - `FingerMonster/Run 3x Replay And Avatar Persistence Test` (`RunReplayAndPersistenceTest`)
  - `FingerMonster/Run Double Setup And HUD Verification` (`RunDoubleSetupAndHUDVerification`)
  - `FingerMonster/Run Ability Cards Verification` (`RunAbilityCardsVerification`)
  - `FingerMonster/Run Ghost Assistant Verification` (`RunGhostAssistantVerification`)
  - `FingerMonster/Run Ghost Camera And HUD Layout Verification` (`RunGhostCameraAndHUDLayoutVerification`)
  - `FingerMonster/Test/Run Ghost Active Gameplay Verification` (`RunGhostActiveGameplayVerification`)
  - `FingerMonster/Run All Batchmode Verifications` (`RunAllBatchmodeVerifications`)
- **`ProjectOrganizerTool`**:
  - `Tools/Proje Düzeni/Missing Referans Tara` (`ScanMissingReferences`)
  - `Tools/Proje Düzeni/Grup 1 - ThirdParty` (`ApplyGroup1_ThirdParty`)
  - `Tools/Proje Düzeni/Grup 2 - Audio ve Art` (`ApplyGroup2_AudioAndArt`)
  - `Tools/Proje Düzeni/Grup 3 - Animasyon ve Prefablar` (`ApplyGroup3_AnimationAndPrefabs`)
  - `Tools/Proje Düzeni/Grup 4 - Sahneler, Ayarlar, SO ve Dokümanlar` (`ApplyGroup4_ScenesSettingsSOAndDocs`)
  - `Tools/Proje Düzeni/Grup 5 - Arşiv` (`ApplyGroup5_Archive`)
  - `Tools/Proje Düzeni/Grup 6 - Scriptler` (`ApplyGroup6_Scripts`)
  - `Tools/Proje Düzeni/Uygula` (`ApplyAll`)

---

> [← Önceki Modül: Modül 3/5](./03-inventory-core-and-services.md) | [Ana Mimari İndeksi](../../UYGULAMA_KAPSAMLI_REHBERI_VE_GUVENLIK_RAPORU.md) | [Sonraki Modül: Modül 5/5 →](./05-security-and-testing.md)
