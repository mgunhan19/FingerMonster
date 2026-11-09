# FingerMonster - Third Party Credits & Asset Attributions

Tüm asset ve kütüphane kaynakları telifsiz / ticari kullanıma uygun (CC0 / MIT / Standard Free License) prensibiyle listelenmiştir.

## Sesler (Audio)
- **Prosedürel SFX & Müzik Sentezleyici (Built-in Audio Synthesizer)**:
  - Harici dosya bağımlılığı olmadan %100 taşınabilir, gecikmesiz ve hatasız çalışması için Unity `AudioClip.Create` yöntemiyle gerçek zamanlı sentezlenmiş:
    - Mekanik daktilo tuş vuruşu (mechanical click)
    - Satır sonu zili (carriage return ding)
    - Tuzak kapanma ve el kopma stinger sesi
    - Kasap gerilim tonu (butcher stinger)
    - 4 Absürt Silah Sesi: Dev Et Tokmağı ("BOONNNK!"), Tava ("DÖNNNKKK!"), Sosis Kırbacı ("ŞŞŞRAAAK!"), Pasta ("SSSPLLLAATTT!")
    - Kasap komik kıkırdaması (pitch-modulated butcher giggle)
    - Çizgi film gerilim pizzicato döngüsü (cartoon suspense loop)
    - Seyirciler için boğuk infaz yansıma filtresi (low-pass audio filter)
  - Kod: `SoundEffectsGenerator.cs`, `AudioManager.cs`

## Görsel Efektler ve Partiküller (Visual FX)
- **Stilize Çizgi Roman / Çizgi Film Efekt Motoru (Cartoon FX Engine)**:
  - Gerçekçi kan/vahşet yerine karanlık komedi tonuna uygun sansürlü puf duman bulutu, dönen çizgi film yıldızları, konfetiler, kamera sarsıntısı ve zıplayan 3D çizgi roman baloncukları (`GÜVENLİ!`, `ÇAAATT!`, `POW! ELENDİ!`).
  - Kopan el çizgi film fiziği (`SeveredHandPhysics`) ve yerde 5 saniye komik seğirme animasyonu.
  - Zifiri karanlık & daktilo silindirinde silüet yansıma efekti (`ExecutionReflectionSystem`).
  - Kod: `CartoonFXManager.cs`, `ExecutionReflectionSystem.cs`, `SeveredHandPhysics.cs`

## Karakterler ve Animasyon (Characters & Procedural Rigs)
- **4 Çizgi Film Karakter Arketipi**:
  - `Kabadayı` (Geniş omuzlu, ağırbaşlı, kırmızı kasketli)
  - `Korkak` (Uzun, ince, titrek, mavi silindir şapkalı)
  - `Sinsi` (Hafif eğik, yeşil kukuletalı, hızlı göz kırpan)
  - `Şovmen` (Kibar, altın papyonlu, teatral jestli)
- **Prosedürel İskelet Sürücüsü (CharacterAnimatorDriver)**:
  - Nefes alma (breathing), göz kırpma (blinking), düşünme (head tilt), 2 elle tuşa basma, tek elle basma ve kafa atma ("DÖNK!").
  - 16+ Senkronize Karakter Emote Hareketi (alay dansı, göğüs yumruklama, sahte ağlama, maymun taklidi, vb.).
  - 4 Silaha özel elenme animasyonu (yassılma, sersemlik yıldızları, 720° fırıldak dönüşü, kayıp düşme).
  - Kod: `CharacterDefinition.cs`, `CharacterAnimatorDriver.cs`, `PlayerVisualAvatar.cs`, `EmoteWheelUI.cs`

## Ağ Altyapısı ve Kütüphaneler (Networking)
- **Unity Netcode for GameObjects (v2.2.0)**: Unity Technologies (Unity Companion License / Apache-2.0) - Sunucu yetkili (Server-Authoritative) ağ senkronizasyonu, RPC mekanizmaları ve NetworkVariable yönetimi.
- **Unity Transport (v2.4.0)**: Unity Technologies - Düşük gecikmeli UDP tabanlı ağ taşıma katmanı.
- **LAN Discovery**: Yerel ağda otomatik oda bulma (UDP broadcast port 47777).
- **Mono.Cecil (v1.11.4)**: Jb Evain (MIT License) - Netcode IL post-processing bağımlılığı.

## Font ve Görsel Kaynaklar
- **Unity UI / TextMeshPro**: Unity Technologies (UGUI & TextMeshPro vektörel SDF fontları ve UI bileşenleri).
- **Unity Universal Render Pipeline (URP)**: Unity Technologies.
- **Kenney.nl / Poly Pizza**: İlerleyen aşamalar için planlanan ticari kullanıma uygun CC0/MIT varlık referansları.
