> ← [Ana Mimari İndeksine Dön](../../UYGULAMA_KAPSAMLI_REHBERI_VE_GUVENLIK_RAPORU.md) | **Modül 5/5:** Güvenlik Matrisi, Doğrulama Protokolü & Teknik Çözümler

# 5. Güvenlik Matrisi, Doğrulama Protokolü & Teknik Çözümler

Bu belge, **FingerMonster** ağ güvenliği modelini, çalışma zamanı görsel/fiziksel kararlılık korumalarını, Unity doğrulama test protokolünü ve geliştirme sürecindeki temel Ar-Ge mühendislik çözümlerini özetler.

---

## 5.1. Netcode Server-Authoritative Güvenlik Matrisi

Çok oyunculu tüm durum geçişleri ve oyuncu eylemleri `Assets/_Project/Scripts/Networking/NetworkGameManager.cs` (`#L20`, `#L790-L1546`) üzerinde **Server-Authoritative** olarak doğrulanır:

| Güvenlik Alanı | Uygulanan Mekanizma | Kod Kanıtı |
| :--- | :--- | :--- |
| **RPC Otorite Doğrulaması** | İstemciler oyun durumunu doğrudan değiştiremez; tüm eylemler (`RequestSelectCardServerRpc`, `RequestPlayAbilityCardServerRpc`, `RequestPressKeyServerRpc`, `RequestGhostCurseKeyServerRpc`, `RequestGhostSelectWeaponServerRpc`, `SubmitGhostTransformServerRpc`) sunucu tarafında sıra, koltuk sahipliği, hayatta olma/hayalet durumu ve enerji maliyeti açısından doğrulanır. | `NetworkGameManager.cs #L790-L1546` |
| **Tuzak İndekslerinin Gizlenmesi** | Tuzak dağılımı yalnızca sunucuda tutulur; istemcilere tuzak listesi gönderilmez. İstemci yalnızca `KeyResolve` sonucunda veya `XRay` kartı sunucu onaylı kullanıldığında ilgili tuşun sonucunu öğrenir. | `NetworkGameManager.cs` |
| **Verimli Ağ Serileştirmesi** | 4 koltuğun senkronize verisi `NetworkPlayerSlot` struct'ı üzerinden `INetworkSerializable` (`NetworkSerialize<T>`) ve `FixedString64Bytes` ile bayt düzeyinde serileştirilerek `NetworkList<NetworkPlayerSlot>` içinde taşınır. | `NetworkPlayerSlot.cs #L12-L89` |
| **Bot Fair Play Koruması** | Yapay zekâ (`AIBrain.cs`) tuzak indekslerine doğrudan erişemez; yalnızca açık tahta verisini ve olasılık oranını (`T_kalan / N_kalan`) kullanır. | `AIBrain.cs #L38` |

---

## 5.2. Runtime Kararlılık & Görsel Güvenlik Korumaları

1. **Kemik Ölçeği NaN & Tekil Matris Koruması (`HandSeverSystem.cs #L259-L269`):**
   - Kopan el kemikleri `Vector3.zero` yerine `new Vector3(0.0001f, 0.0001f, 0.0001f)` değerine çekilir. Bu sayede `Invalid AABB`, sıfıra bölme ve matris inversiyon hataları önlenir.
2. **Işığa Duyarlı Erişilebilirlik & Hayalet Efekt Kısıtlaması (`SettingsMenuController.cs #L43-L48`, `GhostScareAndThrowSystem.cs #L102-L121`, `#L257-L260`):**
   - `SettingsMenuController.AreGhostEffectsEnabled()` (`PlayerPrefs` anahtarı `PrefGhostEffects`) kapalıysa hayalet korkutma vinyeti ve ekran sıçrama (splat) efektleri hiç tetiklenmez.
   - `SettingsMenuController.AreGhostEffectsReduced()` (`PlayerPrefs` anahtarı `PrefReduceGhostEffects`) açıksa alfa yoğunluğu otomatik düşürülür (`NormalVignetteAlpha = 0.65f` yerine `ReducedVignetteAlpha = 0.35f`; `NormalScreenSplatAlpha = 0.45f` yerine `ReducedScreenSplatAlpha = 0.25f`).
3. **Kenar Vinyet & Splat Ekran Alan Sınırı (`GhostScareAndThrowSystem.cs #L423-L453`):**
   - Korkutma vinyeti (`BuildVignetteEdges`) ve ekran sıçramaları (`BuildScreenEdgeSplats`) yalnızca ekranın kenar şeritlerini/köşelerini kaplar; toplam kaplanan alan ekranın `<= %30`'u ile sınırlandırılmıştır (`VignetteTop`: üst `%8`, `VignetteBottom`: alt `%8`, `VignetteLeft`/`Right`: yan `%6`; köşe splat'ları `%16x%18` / `%16x%16`). Merkez görüş alanı ve daktilo tuşları her zaman açık kalır.
4. **Tekrar Oynama (Replay) & Sahne Temizliği:**
   - `GhostScareAndThrowSystem.ClearAllActiveEffects()` (`#L163-L212`) aktif tüm vinyet/splat coroutine'lerini ve UI konteynerlerini sıfırlar.
   - `ButcherPlaceholder.DestroyGhostPreview()` (`#L1748`, `GameManager.cs #L1328`, `#L1777`) hayalet silah önizleme nesnelerini temizler; böylece 3x replay döngülerinde sahnede artık nesne kalmaz.

---

## 5.3. Unity Doğrulama & Kalite Protokolü

Her kod veya sahne değişikliğinden sonra aşağıdaki doğrulama adımları uygulanır:

1. **Eksik Referans Taraması (`ProjectOrganizerTool.cs #L80-L81`):**
   - Unity Editor menüsünden `Tools/Proje Düzeni/Missing Referans Tara` (`ProjectOrganizerTool.ScanMissingReferences()`) çalıştırılarak Prefab ve sahnelerdeki kopuk referanslar taranır.
2. **Otomatik Fonksiyonel Testler (`GameAutomatedTest.cs #L23-L1690`):**
   - `FingerMonster/Run Game Verification Test`
   - `FingerMonster/Run Stage 2 LAN & AI Verification`
   - `FingerMonster/Run Stage 3 Characters & Weapons Verification`
   - `FingerMonster/Run Stage 4 Bodyguard Functional Verification`
   - `FingerMonster/Run Emote Target Cycling Test`
   - `FingerMonster/Run 3x Replay And Avatar Persistence Test`
   - `FingerMonster/Run Double Setup And HUD Verification`
   - `FingerMonster/Run Ability Cards Verification`
   - `FingerMonster/Run Ghost Assistant Verification`
   - `FingerMonster/Run Ghost Camera And HUD Layout Verification`
   - `FingerMonster/Test/Run Ghost Active Gameplay Verification`
   - Toplu doğrulama için: `FingerMonster/Run All Batchmode Verifications` (`#L1690`).
3. **Unity `6000.3.19f1` Batchmode Derleme Kontrolü (`ProjectSettings/ProjectVersion.txt`):**
   - Unity `6000.3.19f1` ile `-batchmode -quit` veya `GameAutomatedTest` çalıştırılarak Console çıktısında `CS...` derleme hatası ve yeni çalışma zamanı exception'ı bulunmadığı doğrulanır.

---

## 6. Ar-Ge & Mühendislik Çözümleri Özeti

Proje geliştirme sürecinde karşılaşılan teknik kısıtlara üretilen özgün mühendislik çözümleri:

| Modül / Problem | Mühendislik Çözümü | İlgili Dosya(lar) |
| :--- | :--- | :--- |
| **Harici Ses Dosyası Olmadan Çizgi Film Sesleri** | 44.1 kHz örnekleme hızında zarf (envelope), frekans süpürme (chirp) ve gürültü senteziyle çalışma zamanında `AudioClip` üreten **Prosedürel Ses Sentezleyici**. | `Assets/_Project/Scripts/Audio/SoundEffectsGenerator.cs`, `AudioManager.cs` |
| **Hayalet Silah Önizlemesinin Canlılara Sızması** | Kasap üzerinde **Tekil Silah Soketi** + **`GhostOnly` Layer 11** izolasyonu; yalnızca `IsLocalViewerGhost == true` olan hayalet kamerasının culling mask'ine eklenir. | `ExecutionReflectionSystem.cs`, `ButcherPlaceholder.cs` |
| **İnfazda Gerilim ve Seyirci Gizliliği** | Odayı karartan `RoomLightingController` ile birlikte `512x512` `RenderTexture` (`RT_ExecutionSilhouetteReflection`) kullanan **İnfaz Yansıma Kamerası** (`reflectionCamera`). | `ExecutionReflectionSystem.cs`, `RoomLightingController.cs` |
| **Büyüyen Proje Yapısında Güvenli Refaktör** | Grup bazlı Editor taşıma otomasyonu (`ProjectOrganizerTool.cs`), her taşımayı kaydeden `MoveLog.csv`, arşiv koruma (`Assets/_Archive/`) ve `Missing Referans Tara` denetimi. | `ProjectOrganizerTool.cs`, `Assets/_Project/Docs/MoveLog.csv`, `PROJE_HARITASI.md` |

---

> **Önceki Modül:** [← Modül 4/5: UI, Ses, Animasyon & İçerik Hattı](04-ui-audio-and-pipeline.md) | [Ana Mimari İndeksine Dön ↑](../../UYGULAMA_KAPSAMLI_REHBERI_VE_GUVENLIK_RAPORU.md)
