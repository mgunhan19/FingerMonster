# FingerMonster — Proje Haritası (`PROJE_HARITASI.md`)

Bu belge, FingerMonster Unity projesinin disk üzerindeki güncel klasör yapısını ve aranan her sistem, varlık veya ayarın tam konumunu içerir. Tüm yollar proje köküne (`Assets/...`) göredir.

---

## 1. Klasör Ağacı ve İçerikler

```text
Assets/
├── DefaultNetworkPrefabs.asset          # Unity Netcode for GameObjects ağ prefab listesi
├── _Project/                            # Projeye özgü tüm aktif varlıklar, kodlar ve sahneler
│   ├── Animation/                       # Animasyon varlıkları
│   │   ├── Avatars-Masks/               # Avatar maskeleri (UpperBodyEmoteMask.mask)
│   │   ├── Clips/
│   │   │   ├── Mixamo/                  # Mixamo'dan içe aktarılacak yeni animasyon klipleri (README_Mixamo.txt, Mixamo_Import_Report.txt)
│   │   │   └── Other/                   # Mevcut .anim klipleri ve .fbx animasyon kaynakları (Sitting, PressKey, Emote_00..15 vb.)
│   │   ├── Controllers/                 # Ana Animator Controller (BodyguardAnimator.controller)
│   │   └── Overrides/                   # Karakter arketip AnimatorOverrideController'ları (AOC_Feg, AOC_Feyzi, AOC_Kenan, AOC_MMG)
│   ├── Art/                             # Görsel varlıklar
│   │   ├── Characters/
│   │   │   ├── Bodyguards/              # 4 oyuncu karakteri için kullanılan Bodyguard FBX mesh'leri, dokuları (.tga) ve URP materyalleri
│   │   │   └── Butcher/                 # Kasap karakter modeli (kasap.fbx) ve animasyon FBX'leri (Butcher_Idle, Butcher_Attack vb.)
│   │   ├── Materials/                   # Sahne, daktilo, silah, poster ve karakter materyalleri (Mat_*.mat)
│   │   └── Textures/
│   │       └── Environment/             # Çevre ve poster dokuları (Tex_*.png)
│   ├── Audio/                           # Ses efektleri (.wav)
│   │   ├── Butcher/                     # Kasap silah sesleri (butcher_mallet_hit.wav, butcher_pan_clang.wav, butcher_pie_splat.wav, butcher_whip_snap.wav)
│   │   └── Ghost/                       # Hayalet korkutma ve fırlatma sesleri (ghost_buuu_scare.wav, ghost_paper_hit.wav vb.)
│   ├── Docs/                            # Proje dokümantasyonu ve taşınma kayıtları
│   │   ├── AI_README.md                 # Yapay zeka bot sistemi dokümantasyonu
│   │   ├── ANIMATIONS.md                # Animasyon sistemi ve klip kuralları
│   │   ├── ArsivListesi.md              # Assets/_Archive/ altına taşınan varlıkların listesi ve nedenleri
│   │   ├── CREDITS.md                   # Ses ve varlık kaynakları / lisans bilgileri
│   │   ├── MoveLog.csv                  # Proje düzenleme sırasında yapılan dosya taşıma kayıtları
│   │   └── Networking_README.md         # Netcode / LAN lobi altyapısı dokümantasyonu
│   ├── Prefabs/
│   │   └── Characters/                  # Oyuncu karakter prefab'ları (PlayerCharacter_Feg/Feyzi/Kenan/MMG.prefab)
│   │       └── SeveredHands/            # Kopan sol/sağ el prefab'ları ve Meshes/ alt klasörü
│   ├── Scenes/                          # Oyun sahneleri (MainMenu.unity, Game.unity)
│   ├── ScriptableObjects/               # Veri varlıkları (.asset)
│   │   ├── Emotes/                      # Karakter emote tanımları (EmoteData_Feg/Feyzi/Kenan/MMG.asset)
│   │   └── Settings/                    # Oyun kuralı ve ayar varlığı (DefaultGameSettings.asset)
│   ├── Scripts/                         # Tüm C# kaynak kodları (sistemlere göre ayrılmış)
│   │   ├── AI/                          # Bot yapay zekası (AIBrain.cs, AIPlayerController.cs, BotData.cs)
│   │   ├── Audio/                       # Ses yönetimi ve prosedürel SFX üretimi (AudioManager.cs, SoundEffectsGenerator.cs)
│   │   ├── Butcher/                     # Kasap, silahlar ve yansıma infazı (ButcherPlaceholder.cs, ExecutionReflectionSystem.cs, ExecutionWeapons.cs, WeaponData.cs)
│   │   ├── CameraSystem/                # Koltuk/oyuncu kamera kontrolü (PlayerCameraController.cs)
│   │   ├── Cards/                       # Yetenek ve tuzak kartları (CardData.cs, CardSelectionController.cs)
│   │   ├── Core/                        # Çekirdek oyun döngüsü, ayarlar ve komutlar (GameManager.cs, GameSettings.cs, GameEnums.cs, GameSessionData.cs, ICommand.cs, PressKeyCommand.cs)
│   │   ├── Editor/                      # Unity Editor araçları ve otomasyonlar (ProjectOrganizerTool.cs, SceneSetupTool.cs, BuildGameTool.cs, BodyguardSetupTool.cs, GameAutomatedTest.cs, WeaponOffsetTool.cs)
│   │   ├── Emotes/                      # Emote veri yapısı (CharacterEmoteData.cs)
│   │   ├── Gameplay/                    # Daktilo, ışıklandırma ve efektler (TypewriterMachine.cs, TypewriterKey.cs, TypewriterPaper.cs, RoomLightingController.cs, FlickeringBulb.cs, CartoonFXManager.cs)
│   │   ├── Ghost/                       # Hayalet korkutma ve nesne fırlatma (GhostScareAndThrowSystem.cs, ThrowableItemData.cs)
│   │   ├── Networking/                  # Unity Netcode & LAN Discovery (NetworkGameManager.cs, NetworkRoomManager.cs, NetworkPlayerSlot.cs, LANDiscovery.cs)
│   │   ├── Players/                     # Oyuncu, sıra, el kopma ve animasyon sürücüleri (TurnController.cs, HandSeverSystem.cs, SeveredHandPhysics.cs, CharacterAnimatorDriver.cs, CharacterDefinition.cs, CharacterIKBridge.cs, PlayerData.cs, PlayerInputHandler.cs, PlayerSeat.cs, PlayerVisualAvatar.cs)
│   │   └── UI/                          # Tüm HUD, menü, lobi ve emote arayüz script'leri (HUDController.cs, GhostHUD.cs, AbilityCardHUD.cs, EmoteWheelUI.cs, EmoteGuidePanelUI.cs, MainMenuController.cs, NetworkLobbyUI.cs, PauseMenuController.cs, SettingsMenuController.cs, GameOverUIController.cs, PlayerCardUI.cs)
│   └── Settings/                        # Proje giriş ve URP görüntü ayarları
│       ├── Input/                       # New Input System aksiyonları (InputSystem_Actions.inputactions)
│       └── Rendering/                   # URP varlıkları ve Volume profilleri (PC_RPAsset.asset, PC_Renderer.asset, Mobile_RPAsset.asset, Mobile_Renderer.asset, DefaultVolumeProfile.asset, GameVolumeProfile.asset, UniversalRenderPipelineGlobalSettings.asset)
├── ThirdParty/                          # Harici varlık paketleri (iç yapısına dokunulmaz)
│   └── 3DForge/
│       └── Fantasy_Interiors/
│           └── Villages_&_Towns/        # 3DForge iç mekan modelleri, materyalleri, prefab'ları ve dokuları
├── _Archive/                            # Silinmek yerine arşivlenen eski, kullanılmayan veya hata ayıklama dosyaları
│   ├── BodyGuards_Original/             # Orijinal BodyGuards paketinin kullanılmayan demo sahnesi ve kaynakları
│   ├── Debug-Scripts/                   # Geçici hata ayıklama ve sahne doğrulama Editor script'leri
│   ├── SampleScene/                     # Unity şablonundan kalan örnek sahne ve profil
│   ├── TutorialInfo/                    # Unity URP şablon eğitim dosyaları
│   └── UnusedMaterials/                 # Artık kullanılmayan eski materyaller
├── Plugins/
│   └── Roslyn/                          # Roslyn derleyici/analizör DLL dosyaları
└── TextMesh Pro/                        # TextMesh Pro fontları, shader'ları, kaynakları ve örnekleri
```

---

## 2. "Ne Arıyorsun? Nerede?" Tablosu

| Aradığın Sistem / Varlık | Konum (Gerçek Dosya / Klasör Yolu) | Açıklama |
| :--- | :--- | :--- |
| **Kasap silahı ayarı** | `Assets/_Project/Scripts/Butcher/WeaponData.cs`<br>`Assets/_Project/Scripts/Butcher/ExecutionWeapons.cs` | Silah veri modeli (`WeaponData`) ve kasap infaz silahlarının sahne içi davranışı/seçimi. |
| **Kart tanımları** | `Assets/_Project/Scripts/Cards/CardData.cs`<br>`Assets/_Project/Scripts/Cards/CardSelectionController.cs` | Yetenek/seçim kartlarının veri yapısı ve kart seçim akışı. |
| **Hayalet sesleri** | `Assets/_Project/Audio/Ghost/`<br>`Assets/_Project/Scripts/Audio/AudioManager.cs` | Hayalet korkutma (`ghost_buuu_scare.wav`) ve fırlatma sesleri (`ghost_*.wav`) ile ses yöneticisi. |
| **Kasap sesleri** | `Assets/_Project/Audio/Butcher/` | Kasap silah darbe/patlama sesleri (`butcher_mallet_hit.wav`, `butcher_pan_clang.wav`, `butcher_pie_splat.wav`, `butcher_whip_snap.wav`). |
| **Spiker/duyuru ve `GameSettings`** | `Assets/_Project/ScriptableObjects/Settings/DefaultGameSettings.asset`<br>`Assets/_Project/Scripts/Core/GameSettings.cs` | Oyun kuralları, tur/zamanlama parametreleri, kart ve spiker/duyuru ayarlarının tanımlandığı ScriptableObject ve sınıfı. |
| **Karakter animasyonları & Controller/Override** | `Assets/_Project/Animation/Controllers/BodyguardAnimator.controller`<br>`Assets/_Project/Animation/Overrides/`<br>`Assets/_Project/Animation/Avatars-Masks/UpperBodyEmoteMask.mask`<br>`Assets/_Project/Animation/Clips/Other/` | Ana Animator Controller, 4 karakter arketipi için Override Controller'lar (`AOC_Feg`, `AOC_Feyzi`, `AOC_Kenan`, `AOC_MMG`), üst gövde maskesi ve mevcut animasyon klipleri. |
| **Mixamo animasyonlarının konacağı yer** | `Assets/_Project/Animation/Clips/Mixamo/` | Yeni indirilecek Mixamo `.fbx` animasyonlarının yerleştirileceği klasör (`README_Mixamo.txt` ve `Mixamo_Import_Report.txt` buradadır). |
| **Emote tanımları** | `Assets/_Project/ScriptableObjects/Emotes/`<br>`Assets/_Project/Scripts/Emotes/CharacterEmoteData.cs` | 4 arketipin emote varlıkları (`EmoteData_Feg.asset`, `EmoteData_Feyzi.asset`, `EmoteData_Kenan.asset`, `EmoteData_MMG.asset`) ve veri sınıfı. |
| **HUD ve UI script'leri** | `Assets/_Project/Scripts/UI/` | `HUDController.cs`, `GhostHUD.cs`, `AbilityCardHUD.cs`, `EmoteWheelUI.cs`, `EmoteGuidePanelUI.cs`, `MainMenuController.cs`, `NetworkLobbyUI.cs`, `PauseMenuController.cs`, `SettingsMenuController.cs`, `GameOverUIController.cs`, `PlayerCardUI.cs`. |
| **Kamera script'i** | `Assets/_Project/Scripts/CameraSystem/PlayerCameraController.cs` | Oyuncu koltuğu ve masa kamera açıları/geçiş kontrolcüsü. |
| **Ağ kodu (Netcode)** | `Assets/_Project/Scripts/Networking/`<br>`Assets/DefaultNetworkPrefabs.asset` | `NetworkGameManager.cs`, `NetworkRoomManager.cs`, `NetworkPlayerSlot.cs`, `LANDiscovery.cs` ve ağ prefab kayıt listesi. |
| **Sahneler** | `Assets/_Project/Scenes/` | `MainMenu.unity` ve `Game.unity`. |
| **Işık ve URP Volume ayarları** | `Assets/_Project/Settings/Rendering/`<br>`Assets/_Project/Scripts/Gameplay/RoomLightingController.cs`<br>`Assets/_Project/Scripts/Gameplay/FlickeringBulb.cs` | `PC_RPAsset.asset`, `Mobile_RPAsset.asset`, `PC_Renderer.asset`, `Mobile_Renderer.asset`, `DefaultVolumeProfile.asset`, `GameVolumeProfile.asset`, `UniversalRenderPipelineGlobalSettings.asset` ve oda aydınlatma script'leri. |
| **Input Actions** | `Assets/_Project/Settings/Input/InputSystem_Actions.inputactions` | Unity New Input System giriş haritaları ve tuş atamaları. |
| **Üçüncü taraf paketler** | `Assets/ThirdParty/3DForge/` | `Fantasy_Interiors/Villages_&_Towns/` iç mekan asset paketi. |
| **Arşiv** | `Assets/_Archive/` | Eski veya kullanılmayan dosyalar (`BodyGuards_Original/`, `Debug-Scripts/`, `SampleScene/`, `TutorialInfo/`, `UnusedMaterials/`). Detaylar için `Assets/_Project/Docs/ArsivListesi.md`. |
| **Proje düzeni aracı** | `Assets/_Project/Scripts/Editor/ProjectOrganizerTool.cs` | Proje klasör düzenleme ve taşıma otomasyonu Editor aracı. |
