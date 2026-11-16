> ← [Ana Mimari İndeksine Dön](../../UYGULAMA_KAPSAMLI_REHBERI_VE_GUVENLIK_RAPORU.md) | **Modül 1/5:** Kırılmaz Mimari & Tasarım Kuralları

# 2. Kırılmaz Mimari & Tasarım Kuralları

Bu belge, **FingerMonster** (Unity 6 `6000.3.19f1`, URP) projesinde geriye dönük uyumluluğu, görsel bütünlüğü, çalışma zamanı kararlılığını ve adil oynanışı korumak için uyulması zorunlu temel mimari kuralları tanımlar.

---

## 2.1. Render Pipeline & Görsel Dil

Proje **Universal Render Pipeline (URP)** üzerinde çalışır; PC ve Mobil için ayrı renderer/asset profilleri tanımlıdır:

| Varlık / Bileşen | Dosya Yolu | Rolü |
| :--- | :--- | :--- |
| Global URP Ayarları | `Assets/_Project/Settings/Rendering/UniversalRenderPipelineGlobalSettings.asset` | Proje geneli URP varsayılanları |
| PC & Mobil RP Asset'leri | `Assets/_Project/Settings/Rendering/PC_RPAsset.asset`, `Mobile_RPAsset.asset` | Platforma özgü gölge, MSAA ve render kalitesi |
| Renderer Profilleri | `Assets/_Project/Settings/Rendering/PC_Renderer.asset`, `Mobile_Renderer.asset` | Forward renderer ve katman geçişleri |
| Post-Processing Profilleri | `Assets/_Project/Settings/Rendering/DefaultVolumeProfile.asset`, `GameVolumeProfile.asset` | Karanlık komedi atmosferi (tonemapping, vignette, bloom) |
| Oda Aydınlatma Kontrolcüsü | `Assets/_Project/Scripts/Gameplay/RoomLightingController.cs` | Masa spotu, ortam ışığı ve kasap sekansı kırmızı ışık geçişi |
| Titreyen Ampul | `Assets/_Project/Scripts/Gameplay/FlickeringBulb.cs` | Asılı ampulün organik titreme (flicker) ve sallantı (`dangle`) efekti |

**Atmosfer Sabitleri (`RoomLightingController.cs`):**
- **Normal Durum (`#L19-L21`):** `normalSpotIntensity = 2.5f`, `normalAmbientIntensity = 0.35f`, `normalSpotColor = (1.0f, 0.95f, 0.85f)` (sıcak daktilo spotu).
- **Kasap / İnfaz Durumu (`#L24-L26`):** `butcherSpotIntensity = 0.8f`, `butcherAmbientIntensity = 0.05f`, `butcherSpotColor = (0.85f, 0.1f, 0.1f)` (karanlık oda ve dar kırmızı dramatik odak).
- **Kural:** Kullanıcı açıkça talep etmedikçe mevcut URP Volume profilleri, masa ışığı açıları ve karanlık komedi tonlaması değiştirilemez.

---

## 2.2. Kamera & Hayalet Modu Kuralları

Yerel oyuncu kamerası `Assets/_Project/Scripts/CameraSystem/PlayerCameraController.cs` tarafından yönetilir:

1. **Koltuk Sabitleme Kuralı (`#L10-L15`, `#L73-L98`):** Sıra başka bir oyuncuya veya bota geçse dahi kamera asla sıradaki oyuncuyu takip etmez; yalnızca yerel koltukta (`StandardCamPositions` / `StandardCamEulerAngles`, `fixedFieldOfView = 68f`) sabit kalır. Sadece emote sırasında rakibe yumuşak yaw dönüşü yapar ve geri döner.
2. **Hayalet Hareket Modları (`GameEnums.cs #L102-L106`, `PlayerCameraController.cs #L45`):**
   - `GhostMovementMode.FreeFlight`: Serbest uçuş (`ghostMoveSpeed = 3.5f`, `ghostSprintMultiplier = 2.0f`, pitch sınırları `-70°` ile `+75°`).
   - `GhostMovementMode.SpectatorOrbit`: Masa merkezi (`TableCenterTarget = (0f, 0.85f, 0f)`) etrafında yörünge izleme (`ghostDistanceFromTable = 2.95f`, `ghostCameraHeight = 2.25f`, `ghostOrbitSpeed = 12f`).
3. **Oda Sınırları (`GhostRoomBounds`, `#L67-L69`):** Hayalet serbest uçuşta `center: (0, 2.20, 0)`, `size: (6.8, 1.30, 6.8)` hacminin dışına çıkamaz; duvar ve masa çarpışmaları `ghostSphereCastRadius = 0.25f` ile engellenir.
4. **Yakın Kırpma Düzlemi (`nearClipPlane`):**
   - Normal oyuncu modunda (`#L147`): `cam.nearClipPlane = 0.3f`.
   - Hayalet modunda (`#L184-L186`, `#L387`): `cam.nearClipPlane = 0.15f` (masaya ve karakterlere yakın uçuşlarda geometri kesilmesini önlemek için).
5. **Hayalet Yakınlaşma Saydamlığı (`PlayerVisualAvatar.cs #L541-L585`):** `UpdateGhostProximityFade()` metodu, hayalet avatarı yaşayan yerel oyuncunun kamerasına `fadeDistance = 1.0f` (`< 1m`) mesafeye girdiğinde veya kamera ile masa/daktilo görüş hattının arasına girdiğinde materyal alfasını yumuşakça `minAlpha = 0.15f` değerine düşürür. Normal hayalet saydamlığı `0.42f` seviyesindedir.

---

## 2.3. El Kopma & İskelet Bütünlüğü Kuralı

`Assets/_Project/Scripts/Players/HandSeverSystem.cs` ve `Assets/_Project/Scripts/Players/SeveredHandPhysics.cs` bileşenleri el kopma mekaniğini yönetir:

- **Sıfır Ölçek Yasağı (`HandSeverSystem.cs #L259-L269`):** Kopan el ve altındaki parmak kemiklerinin `localScale` değeri **asla `Vector3.zero` (`0`) yapılmaz**. Bunun yerine `ApplyZeroScale()` içinde `new Vector3(0.0001f, 0.0001f, 0.0001f)` kullanılır.
- **Neden:** `0` ölçek Unity fizik ve skinned mesh matrislerinde tekil matris (*singular matrix / division by zero*), `NaN` transform bozulması ve `Invalid AABB` hatalarına yol açar.
- **Animator Koruması (`#L56-L67`):** `LateUpdate()` içinde her kare kopmuş ellerin kemik hiyerarşisine `ApplyZeroScale()` yeniden uygulanır; böylece Animator klibi kemik ölçeğini geri yazsa bile kopmuş el görünmez kalır.
- **Fiziksel Kopan El (`#L171-L210`):** Kopan elin cilt materyali (`characterSkinMaterial`) yeni spawn edilen `SeveredHandPhysics` örneğine aktarılır ve bilek bölgesinde koyu yara/bandaj görseli aktif edilir (`#L144-L159`).

---

## 2.4. İnfaz Yansıma Optiği (`ExecutionReflectionSystem`)

İki elini kaybeden oyuncunun infaz sekansı `Assets/_Project/Scripts/Butcher/ExecutionReflectionSystem.cs` tarafından kontrol edilir:

- **Yansıma Kamerası ve Doku (`#L85-L105`):** `reflectionCamera` görüntüyü `512x512` `ARGB32` `RenderTexture` (`RT_ExecutionSilhouetteReflection`) üzerine işler ve bu doku `reflectionSurfaceRenderer` (daktilo silindiri / parlak yüzey) materyaline atanır.
- **Kurban Yönelimi (`#L137-L163`):** `reflectionCamera`, masa merkezine yakın konumdan kurban koltuğunun baş hizasına (`seats[i].transform.position + Vector3.up * 1.25f`) çevrilir ve `butcherLayer` (Layer `10`) culling mask'e dahil edilir.
- **Hayalet Katmanı ve Işık Kuralı (`#L27`, `#L30`, `#L117`, `#L177-L196`):**
  - `GhostOnly` katmanı **Layer `11`** olarak çözümlenir (`ResolveGhostOnlyLayer()`).
  - `IsLocalViewerGhost == true` ise:
    - Oda karartılmaz (`!IsLocalViewerGhost` kontrolü, `#L117`) ve infaz ses filtresi boğuklaştırılmaz (`#L169`).
    - Ana kameranın `cullingMask` değerine `(1 << ghostOnlyLayer)` eklenir; `false` ise temizlenir (`mainCam.cullingMask &= ~(1 << ghostOnlyLayer)`).

---

## 2.5. Singleton & `DontDestroyOnLoad` Kuralı

Sahne geçişlerinde mükerrer yönetici oluşumunu ve bellek sızıntılarını önlemek için tüm çekirdek yöneticiler tekil (Singleton) koruma uygular:

| Yönetici Sınıfı | Dosya Yolu | Tekil Koruma & `DontDestroyOnLoad` Davranışı |
| :--- | :--- | :--- |
| `GameManager` | `Assets/_Project/Scripts/Core/GameManager.cs` | `Awake()` içinde `Instance != null && Instance != this` kontrolü (`#L106`) |
| `NetworkGameManager` | `Assets/_Project/Scripts/Networking/NetworkGameManager.cs` | `Awake()` içinde `Instance != null && Instance != this` kontrolü (`#L121`) |
| `AudioManager` | `Assets/_Project/Scripts/Audio/AudioManager.cs` | `Instance` kontrolü + `DontDestroyOnLoad(gameObject)` (`#L74-L83`) |
| `CartoonFXManager` | `Assets/_Project/Scripts/Gameplay/CartoonFXManager.cs` | `Awake()` içinde `Instance != null && Instance != this` kontrolü (`#L35`) |
| `NetworkRoomManager` | `Assets/_Project/Scripts/Networking/NetworkRoomManager.cs` | `Instance` kontrolü + **yalnızca kök GameObject ise** `if (transform.parent == null) DontDestroyOnLoad(gameObject)` (`#L41-L47`) |

**Zorunlu Kural:** `DontDestroyOnLoad` çağrısı yalnızca hiyerarşi kökündeki (`transform.parent == null`) GameObject'lerde yapılmalıdır; aksi halde Unity uyarısı üretir ve alt nesne yaşam döngüsünü bozar.

---

## 2.6. Karakter & Bot Kimlik Bütünlüğü

4 oynanabilir karakter arketipi `Assets/_Project/Scripts/Players/CharacterDefinition.cs` (`#L9-L15`, `#L72-L162`) içinde tanımlıdır ve `Bodyguards` iskelet modelleriyle eşleştirilmiştir:

| Arketip (`CharacterArchetype`) | İsim | Kişilik Etiketi | Model Asset (`ModelAssetName`) | Prefab Yolu |
| :--- | :--- | :--- | :--- | :--- |
| `Kabadayi` | **Feg** | Cesur | `SkelMesh_Bodyguard_02` | `Assets/_Project/Prefabs/Characters/PlayerCharacter_Feg.prefab` |
| `Korkak` | **Kenan** | Ürkek | `SkelMesh_Bodyguard_01` | `Assets/_Project/Prefabs/Characters/PlayerCharacter_Kenan.prefab` |
| `Sinsi` | **Feyzi** | Sinsi | `SkelMesh_Bodyguard_03` | `Assets/_Project/Prefabs/Characters/PlayerCharacter_Feyzi.prefab` |
| `Sovmen` | **MMG** | Şovmen | `SkelMesh_Bodyguard_04` | `Assets/_Project/Prefabs/Characters/PlayerCharacter_MMG.prefab` |

- **Bot Fair Play Kuralı (`Assets/_Project/Scripts/AI/AIBrain.cs #L38`):** Botlar tuzak yerlerini **ASLA bilmez**. Zorluk seviyesine göre yalnızca basılmış tuşları filtreler veya kalan tuzak/tuş olasılık oranını (`P_tuzak = T_kalan / N_kalan`) hesaplar (`BotData.cs #L21-L26`).

---

## 2.7. Dosya Yerleşim & İsimlendirme Kuralları

`AGENTS.md` ve `PROJE_HARITASI.md` uyarınca tüm proje varlıkları aşağıdaki düzene uymak zorundadır:

- **Proje Varlıkları (`Assets/_Project/`):**
  - Script'ler görevine göre `Assets/_Project/Scripts/<Sistem>/` altına konur.
  - Materyaller `Assets/_Project/Art/Materials/`, dokular `Assets/_Project/Art/Textures/`, prefab'lar `Assets/_Project/Prefabs/`, animasyonlar `Assets/_Project/Animation/Clips/`, sesler `Assets/_Project/Audio/`, ScriptableObject'ler `Assets/_Project/ScriptableObjects/` altına yerleştirilir.
- **Üçüncü Taraf Paketler (`Assets/ThirdParty/`):** Dış varlık paketleri bu klasöre konur ve iç klasör yapısına dokunulmaz.
- **Arşivleme (`Assets/_Archive/`):** Kullanılmayan, geçici veya kopya dosyalar **silinmez**; `Assets/_Archive/` altına taşınır ve `Assets/_Project/Docs/ArsivListesi.md` güncellenir.
- **Harita ve Taşıma Kayıtları:** Yeni ana klasör veya sistem eklendiğinde `PROJE_HARITASI.md` güncellenir. `ProjectOrganizerTool.cs` üzerinden yapılan tüm dosya taşımaları `Assets/_Project/Docs/MoveLog.csv` dosyasına işlenir.

---

> **Sonraki Modül:** [Modül 2/5: Oyun Döngüsü, Yetenek Kartları & Hayalet Ekonomisi →](02-gameplay-and-economy.md)
