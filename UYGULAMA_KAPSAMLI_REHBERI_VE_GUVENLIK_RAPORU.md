# 🧠 FINGERMONSTER — MERKEZİ TEKNİK BEYİN, MİMARİ İNDEKS VE GÜVENLİK REHBERİ

> **ANTIGRAVITY & YAPAY ZEKÂ TEK DOĞRULUK KAYNAĞI PROTOKOLÜ (SSOT)**
> Bu belge, **FingerMonster** (Unity 6000.3.19f1 · URP · Netcode for GameObjects) projesinin **Tek Doğruluk Kaynağı (Single Source of Truth)** ana indeksidir. Herhangi bir C# script'i, prefab, sahne, ScriptableObject veya mimari kural üzerinde çalışmaya başlamadan önce bu belge üzerinden ilgili modüle yönelinmelidir.

---

## 🔒 ZORUNLU 4 ADIMLI PROTOKOL

1. **Önce Hızlı Görev Arama Matrisine Bak (Bölüm 0):** Değişiklik yapılacak özelliğin gerçek dosya yolunu ve ilgili sembolleri (sınıf, alan, metot) aşağıdan tespit et.
2. **Modüler Dosya Envanterini İncele (Bölüm 4):** İlgili sistemin detaylı dosya envanterini yalnızca gerekli modülden oku:
   - Çekirdek, Ağ, Yapay Zekâ ve Servisler `[4.1–4.4]`: [`./docs/architecture/03-inventory-core-and-services.md`](./docs/architecture/03-inventory-core-and-services.md)
   - UI, HUD, Kamera, Efektler ve Editor Araçları `[4.5–4.6]`: [`./docs/architecture/04-inventory-screens-and-components.md`](./docs/architecture/04-inventory-screens-and-components.md)
3. **Kırılmaz Kurallara ve Oyun Mimarisine Uy (Bölüm 2, 3, 5):**
   - Tasarım ve Kırılmaz Kurallar: [`./docs/architecture/01-design-and-rules.md`](./docs/architecture/01-design-and-rules.md)
   - Oynanış Döngüsü, Ekonomi ve Yetenek/Hayalet Mekaniği: [`./docs/architecture/02-gameplay-and-economy.md`](./docs/architecture/02-gameplay-and-economy.md)
   - Güvenlik, Testler ve Ar-Ge: [`./docs/architecture/05-security-tests-and-rnd.md`](./docs/architecture/05-security-tests-and-rnd.md)
4. **Test Et ve Canlı Belgeyi Güncelle:**
   - Unity `6000.3.19f1` batchmode derlemesi ile `CS...` derleme hatası olmadığını doğrula.
   - `GameAutomatedTest` (`Assets/_Project/Scripts/Editor/GameAutomatedTest.cs`) testlerini ve `ProjectOrganizerTool.ScanMissingReferences` (`Assets/_Project/Scripts/Editor/ProjectOrganizerTool.cs`) taramasını çalıştır.
   - Yapılan mimari veya dosya değişikliklerinden sonra bu ana indeksi (`UYGULAMA_KAPSAMLI_REHBERI_VE_GUVENLIK_RAPORU.md`), ilgili `./docs/architecture/*.md` dosyasını ve gerekiyorsa `PROJE_HARITASI.md` belgesini güncelle.

---

## BÖLÜM 0: "HANGİ ÖZELLİK HANGİ DOSYADA?" HIZLI GÖREV ARAMA MATRİSİ

| Kullanıcının İstediği İşlem / Özellik | Hedef Dosya Yolu | İlgili Değişken / State / Fonksiyon |
| :--- | :--- | :--- |
| **Oyun Döngüsü & FSM** | `Assets/_Project/Scripts/Core/GameManager.cs`<br>`Assets/_Project/Scripts/Core/GameEnums.cs` | `GameState`, `CurrentState`, `InitializeGame()`, `StartRound()`, `ResolveKey()` |
| **Oyun Ayarları & Tuş/Tuzak/Enerji Sabitleri** | `Assets/_Project/Scripts/Core/GameSettings.cs`<br>`Assets/_Project/ScriptableObjects/Settings/DefaultGameSettings.asset` | `trapCount`, `totalKeys`, `ghostStartSoulEnergy`, `butcherWeapons`, `throwableItems` |
| **Oturum & Mod Seçimi** | `Assets/_Project/Scripts/Core/GameSessionData.cs` | `SelectedMode`, `SelectedDifficulty`, `SelectedCharacterIndex`, `GetTrapCount()` |
| **Sıra Yönetimi** | `Assets/_Project/Scripts/Players/TurnController.cs` | `ActivePlayerIndex`, `IsReversed`, `AdvanceToNextAlivePlayer()` |
| **Daktilo Makinesi, Tuşlar & Kağıt** | `Assets/_Project/Scripts/Gameplay/TypewriterMachine.cs`<br>`Assets/_Project/Scripts/Gameplay/TypewriterKey.cs`<br>`Assets/_Project/Scripts/Gameplay/TypewriterPaper.cs` | `SetupKeys()`, `PressKey()`, `ApplyXRayVisual()`, `SetCursedVisual()` |
| **Kart Seçimi & Yetenek Kartları** | `Assets/_Project/Scripts/Cards/CardData.cs`<br>`Assets/_Project/Scripts/Cards/CardSelectionController.cs`<br>`Assets/_Project/Scripts/Core/GameManager.cs` | `AbilityCardType`, `ShowCardSelection()`, `RevealOneMonsterHint()`, `GameManager.TryPlayAbilityCard()` |
| **El Kopma & Fizik** | `Assets/_Project/Scripts/Players/HandSeverSystem.cs`<br>`Assets/_Project/Scripts/Players/SeveredHandPhysics.cs` | `SeverHand()`, `0.0001f` minimum kemik ölçeği kuralı, `Launch()` |
| **Kasap Mekaniği & Silahlar** | `Assets/_Project/Scripts/Butcher/ButcherPlaceholder.cs`<br>`Assets/_Project/Scripts/Butcher/ExecutionWeapons.cs`<br>`Assets/_Project/Scripts/Butcher/WeaponData.cs` | `ExecutionWeaponType`, `EquipWeaponData()`, `AppearAt()`, `SpawnOrUpdateGhostPreview()` |
| **İnfaz Yansıma Optiği** | `Assets/_Project/Scripts/Butcher/ExecutionReflectionSystem.cs` | `StartExecutionSequence()`, `ConfigureVisibility()`, `IsLocalViewerGhost`, `ghostOnlyLayer = 11` |
| **Hayalet Aktif Oynanış, Korkutma & Fırlatma** | `Assets/_Project/Scripts/Ghost/GhostScareAndThrowSystem.cs`<br>`Assets/_Project/Scripts/Ghost/ThrowableItemData.cs` | `CanScareTarget()`, `ExecuteScareVisuals()`, `LaunchThrowableVisual()`, `ClearAllActiveEffects()` |
| **Hayalet HUD & Enerji/Bekleme Süreleri** | `Assets/_Project/Scripts/UI/GhostHUD.cs` | `MaxSoulSlots = 3`, `CurseEnergyCost = 2`, `ScareEnergyCost = 1`, `ThrowEnergyCost = 1`, `CheckPanelOverlaps()` |
| **Kamera Sistemi & Hayalet Serbest Uçuş** | `Assets/_Project/Scripts/CameraSystem/PlayerCameraController.cs` | `CurrentGhostMode`, `GhostMovementMode`, `SetGhostSpectatorMode()`, `CycleGhostSpectatorTarget()`, `TriggerGentleScareShake()` |
| **Karakter Tanımları, Avatar & Saydamlık** | `Assets/_Project/Scripts/Players/CharacterDefinition.cs`<br>`Assets/_Project/Scripts/Players/PlayerVisualAvatar.cs`<br>`Assets/_Project/Scripts/Players/CharacterAnimatorDriver.cs`<br>`Assets/_Project/Scripts/Players/PlayerSeat.cs` | `CharacterArchetype`, `SetGhostMode()`, `UpdateGhostProximityFade()` |
| **Emote Sistemi & Tekerlek** | `Assets/_Project/Scripts/UI/EmoteWheelUI.cs`<br>`Assets/_Project/Scripts/UI/EmoteGuidePanelUI.cs`<br>`Assets/_Project/Scripts/Emotes/CharacterEmoteData.cs` | `TryExecuteEmoteSlot()`, `CycleTarget()` |
| **LAN Ağ Altyapısı & Netcode** | `Assets/_Project/Scripts/Networking/NetworkGameManager.cs`<br>`Assets/_Project/Scripts/Networking/NetworkRoomManager.cs`<br>`Assets/_Project/Scripts/Networking/LANDiscovery.cs`<br>`Assets/_Project/Scripts/Networking/NetworkPlayerSlot.cs`<br>`Assets/DefaultNetworkPrefabs.asset` | `ServerInitializeMatch()`, `RequestPressKeyServerRpc`, `StartHost()`, `StartClient()` |
| **Bot Yapay Zekâsı** | `Assets/_Project/Scripts/AI/AIBrain.cs`<br>`Assets/_Project/Scripts/AI/AIPlayerController.cs`<br>`Assets/_Project/Scripts/AI/BotData.cs` | `BotPersonality`, `DecideAndExecuteTurn()`, `DecideBotGhostActiveAction()`, `ComputeBotGhostRoamTarget()` |
| **Ses Sentezleyici & Spatial Ses** | `Assets/_Project/Scripts/Audio/AudioManager.cs`<br>`Assets/_Project/Scripts/Audio/SoundEffectsGenerator.cs` | `PlayGhostScare()`, `PlayGhostThrow()`, `PlayGhostSplat()`, `PlayButcherWeaponHit()`, `SetExecutionAudioFilter()` |
| **Efektler & Oda Işıklandırması** | `Assets/_Project/Scripts/Gameplay/CartoonFXManager.cs`<br>`Assets/_Project/Scripts/Gameplay/RoomLightingController.cs`<br>`Assets/_Project/Scripts/Gameplay/FlickeringBulb.cs` | `PlayTrapSnapFX()`, `SpawnRealisticBloodCluster()`, `SetExecutionMode()` |
| **Ana Menü, Oyun HUD, Duraklatma, Ayarlar & Oyun Sonu** | `Assets/_Project/Scripts/UI/MainMenuController.cs`<br>`Assets/_Project/Scripts/UI/HUDController.cs`<br>`Assets/_Project/Scripts/UI/PlayerCardUI.cs`<br>`Assets/_Project/Scripts/UI/AbilityCardHUD.cs`<br>`Assets/_Project/Scripts/UI/PauseMenuController.cs`<br>`Assets/_Project/Scripts/UI/SettingsMenuController.cs`<br>`Assets/_Project/Scripts/UI/GameOverUIController.cs` | Menü akışı, tur HUD'ı, kart çubuğu, duraklatma, ayarlar ve maç sonu ekranı yönetimi |
| **Sahne Kurulumu, Bodyguard/Mixamo Pipeline, Build, Test & Proje Düzeni** | `Assets/_Project/Scripts/Editor/SceneSetupTool.cs`<br>`Assets/_Project/Scripts/Editor/BodyguardSetupTool.cs`<br>`Assets/_Project/Scripts/Editor/WeaponOffsetTool.cs`<br>`Assets/_Project/Scripts/Editor/BuildGameTool.cs`<br>`Assets/_Project/Scripts/Editor/GameAutomatedTest.cs`<br>`Assets/_Project/Scripts/Editor/ProjectOrganizerTool.cs` | Sahne otomasyonu, Bodyguard/Mixamo kurulumu, silah hizalama, derleme, otomatik testler ve `ScanMissingReferences` |

---

## BÖLÜM 1: PROFESYONEL KLASÖR MİMARİSİ VE AKTİF DOSYA HARİTASI

```text
FingerMonster/
├── AGENTS.md                                              # Tüm AI ajanlar için bağlayıcı çalışma kuralları
├── GEMINI.md                                              # Antigravity ajan özet kuralları
├── PROJE_HARITASI.md                                      # Disk üzerindeki klasör ve varlık haritası
├── UYGULAMA_KAPSAMLI_REHBERI_VE_GUVENLIK_RAPORU.md        # Merkezi Teknik Beyin (SSOT) ana indeksi (bu dosya)
├── docs/
│   └── architecture/                                      # Modüler teknik mimari dokümanları (01..05)
└── Assets/
    ├── DefaultNetworkPrefabs.asset                        # Unity Netcode for GameObjects ağ prefab kayıtları
    ├── _Project/                                          # Aktif proje varlıkları, kodlar ve sahneler
    │   ├── Animation/                                     # Animator Controller, AOC'ler, maskeler, Mixamo & Other klipler
    │   ├── Art/                                           # Bodyguards & Butcher modelleri, Mat_* materyalleri, dokular
    │   ├── Audio/                                         # Butcher/ ve Ghost/ ses efektleri (.wav)
    │   ├── Docs/                                          # AI_README.md, ANIMATIONS.md, ArsivListesi.md, CREDITS.md, MoveLog.csv, Networking_README.md
    │   ├── Prefabs/                                       # PlayerCharacter_Feg/Feyzi/Kenan/MMG.prefab & SeveredHands/
    │   ├── Scenes/                                        # MainMenu.unity, Game.unity
    │   ├── ScriptableObjects/                             # Emotes/ (EmoteData_*.asset) & Settings/ (DefaultGameSettings.asset)
    │   ├── Scripts/                                       # AI, Audio, Butcher, CameraSystem, Cards, Core, Editor, Emotes, Gameplay, Ghost, Networking, Players, UI
    │   └── Settings/                                      # Input/ (InputSystem_Actions.inputactions) & Rendering/ (URP varlıkları ve Volume profilleri)
    ├── ThirdParty/                                        # Harici varlık paketleri (3DForge/Fantasy_Interiors/Villages_&_Towns/) - dokunulmaz
    ├── _Archive/                                          # Silinmeden arşivlenen eski varlıklar (BodyGuards_Original, Debug-Scripts, SampleScene, TutorialInfo, UnusedMaterials)
    ├── Plugins/                                           # Roslyn derleyici/analizör DLL'leri
    └── TextMesh Pro/                                      # TextMesh Pro fontları, shader'ları ve kaynakları
```

---

## MODÜLER MİMARİ DOKÜMANTASYON İNDEKSİ

Proje dokümantasyonu token verimliliği ve tek doğruluk kaynağı ilkesi gereği 5 odaklanmış modüle bölünmüştür. İlgili görev için yalnızca gereken modülü okuyun:

1. **[01 — Tasarım ve Kırılmaz Kurallar](./docs/architecture/01-design-and-rules.md)**: URP, Netcode Server-Authoritative mimari, Singleton/DontDestroyOnLoad kuralları, ExecutionReflectionSystem yansıma optiği (`ghostOnlyLayer = 11`), HandSeverSystem `0.0001f` kemik ölçeği kuralı ve dosya yerleşim/arşivleme standartları.
2. **[02 — Oynanış, Ekonomi ve Sistem Mekaniği](./docs/architecture/02-gameplay-and-economy.md)**: Tur döngüsü (`GameState`), tuzak/tuş çözümü, yetenek kartları (`AbilityCardType`), el kopma fiziği, hayalet ruh enerjisi (`MaxSoulSlots = 3`), korkutma/fırlatma ve bot yapay zekâsı.
3. **[03 — Envanter: Çekirdek, Ağ, Yapay Zekâ ve Servisler](./docs/architecture/03-inventory-core-and-services.md)** *(Bölüm 4.1–4.4)*: `Core/`, `Networking/`, `AI/`, `Audio/`, `Butcher/`, `Cards/`, `Ghost/`, `Players/` script envanteri ve sorumlulukları.
4. **[04 — Envanter: Sahneler, Ekranlar, UI, Kamera ve Editor Araçları](./docs/architecture/04-inventory-screens-and-components.md)** *(Bölüm 4.5–4.6)*: `UI/`, `CameraSystem/`, `Emotes/`, `Gameplay/`, `Editor/` script envanteri, sahneler ve ScriptableObject/Prefab bağlantıları.
5. **[05 — Güvenlik, Testler ve Ar-Ge](./docs/architecture/05-security-tests-and-rnd.md)**: Server-Authoritative doğrulama, `GameAutomatedTest`, `ProjectOrganizerTool.ScanMissingReferences`, batchmode derleme kontrolü ve açık Ar-Ge başlıkları.
