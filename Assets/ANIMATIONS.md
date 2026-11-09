# FingerMonster - Animasyonlar, Silahlar ve Görsel Efektler Kılavuzu (ANIMATIONS.md)

Bu belge, FingerMonster'da yer alan tüm karakter durum animasyonlarını, absürt kasap infaz silahlarını, el kopma fizik efektlerini, karanlıkta yansıma mekaniğini ve 16 farklı karakter emote'unu detaylandırır.

---

## 1. Karakter Arketipi ve Görsel Kimlikleri

| Koltuk | Karakter Adı | Arketip | Görsel Nitelikler | Kişilik & Duruş |
| :--- | :--- | :--- | :--- | :--- |
| **Koltuk 1 (Güney)** | **Kabadayı Kazım** | `Kabadayi` | Geniş omuzlu, çatık kaşlı, koyu kırmızı ceket, iri çene | Kendine aşırı güvenen, göğsünü kabartan mağrur duruş |
| **Koltuk 2 (Kuzey)** | **Tereddütlü Toni** | `Korkak` | Kambur, soluk camgöbeği renk, irice fırlayan pörtlek gözler | Tir tir titreyen, her an sandalyenin altına saklanacak gibi duran |
| **Koltuk 3 (Batı)** | **Sinsi Salih** | `Sinsi` | Zehir yeşili, sıska, kıvrak hatlar, sivri burun | Alaycı sırıtan, parmak çıtlatan, başkalarının acısına gülen |
| **Koltuk 4 (Doğu)** | **Şovmen Şinasi** | `Sovmen` | Canlı altın sarısı frak, silindir şapka (Top Hat), büyük papyon | Teatral, abartılı jestler yapan, her an sahnede gibi dans eden |

---

## 2. Temel Durum Animasyonları (State Machine)

| Animasyon Durumu | Tetiklenme Olayı | Görsel Davranış (Squash & Stretch / Kemik) | Ses / Görsel Efekt (VFX) |
| :--- | :--- | :--- | :--- |
| **Idle (Nefes Alma)** | Karakterin sırası beklenirken | Ritmik gövde yaylanması, göz kırpma döngüsü. Korkak karakterde ek olarak tir tir titreme (`Mathf.Sin * 30Hz`). | - |
| **Düşünme (Thinking)** | Sıra karaktere geldiğinde (1-3 sn) | Baş hafif yana yatık, gözler daktilo tuşlarına odaklanır, tereddütlü hafif salınım. | Tuşun üzerinde sarı nişan vurgusu (`SetHovered`) |
| **Tuşa Basma (2 El)** | İki eli sağlamken tuşa basınca | İki kolla öne doğru yaylanma, gövdenin masaya 12 derece eğilmesi. | `TypewriterClick` mekanik vuruş sesi |
| **Tuşa Basma (1 El)** | Bir eli kopmuşken tuşa basınca | Sağlam kalan tek kolla yana eğilerek sertçe daktiloya uzanma. | `TypewriterClick` + `Şak` vuruşu |
| **Tuşa Basma (Elsiz)** | **İki eli de kopmuşken tuşa basınca** | **Önce arkaya gerilir, ardından kafasını daktiloya küt diye vurarak basar!** (Looney Tunes tarzı). | "DÖNK!" çizgi roman balonu + yay darbesi |
| **Sevinç / Rahatlama** | Güvenli tuşa basıldığında | 3 kez neşeyle yaylanma (Bounce), göğüs kabartma, derin "Oh be!" hareketi. | `BellDing` başarı çanı + yeşil popup |
| **Şok / Korku** | Tuzağa basıldığında veya rakip elenince | İri gözler yuvalarından fırlar, gövde arkaya fırlar, 40Hz frekansta dehşet titremesi. | `Gasp / Sting` stinger sesi |
| **El Kopma (Sever)** | Gizli tuzak tuşuna basıldığında | El anında masadan ayrılır, `SeveredHandPhysics` ile havada 360° taklalar atarak uçar. Bileğe yara bandı yapışır. | `TrapStinger` + Renkli boya/konfeti sıvısı + "PUF!" dumanı |
| **Yerde El Seğirmesi** | Kopan el masaya/yere düştükten sonra | El 5 saniye boyunca yerde ritmik olarak parmaklarıyla komik şekilde seğirir/sıçrar, sonra puf diye kaybolur. | "ŞOP!" minyatür darbe sesi |

---

## 3. Kasap İnfaz Silahları ve Elenme Animasyonları

İki eli de giden kurban elendiğinde, Kasap arkasında belirir ve rastgele 4 absürt silahtan birini kuşanır:

| Silah Türü | Silah Adı & Görünüm | Kasap Vuruş Hareketi | Kurbanın Silaha Özel Elenme Animasyonu | Çizgi Film Ses & Yazı Efekti |
| :--- | :--- | :--- | :--- | :--- |
| **0. Silah** | **Dev Çizgi Film Tokmağı** (Kocaman silindirik ahşap tokmak) | Havaya kaldırıp 40Hz tereddüt titremesinden sonra küt diye tepesine indirir. | **Pancake Yassılaşma:** Karakter dikey eksende akordeon gibi sıkışıp masanın üstünde incecik pancake haline gelir (`Scale: 1.7x, 0.15y, 1.7z`). | "BOONNNK!" (Tok bas darbesi) + Kırmızı çizgi roman balonu |
| **1. Silah** | **Uçan Döküm Tava** (Ağır siyah döküm tava) | Arkadan savurarak oyuncunun kafasına yapıştırır. | **Yıldızlarla Devrilme:** Başının etrafında dönen sarı yıldızlar eşliğinde sersemleyip arkaya sandalyeden düşer. | "DÖNNNKKK!" (Metalik tava yankısı) + Sarı "DÖNKKK!" balonu |
| **2. Silah** | **Acılı Sosis Kırbacı** (Birbirine bağlı 3 boğum kırmızı sosis) | Kırbaç gibi şaklatarak oyuncunun etrafında döndürür. | **Fırıldak Dönüşü:** Karakter sandalyenin üstünde fırıldak gibi 720 derece dönerek sersemler ve yana devrilir. | "ŞŞŞRAAAK!" (Islak şaplak sesi) + Turuncu popup |
| **3. Silah** | **Kremalı Pasta Fırlatıcı** (Dev kremalı pasta + vişne) | Doğrudan oyuncunun yüzünün ortasına yapıştırır. | **Kremalı Kayma:** Yüzü krema partikülleriyle kaplanır, arkaya doğru sandalyeden kayarak masanın altına düşer. | "SSSPLLLAATTT!" (Islak pasta patlaması) + Pembe popup |

---

## 4. Karanlıkta Yansıma Efekti (Diğer Oyuncular İçin)

- **Seyirciler (Diğer Oyuncular) İçin**:
  - Oda tamamen kararır (`RoomLightingController.SetExecutionMode(true)`).
  - Kasabın doğrudan 3D modeli diğer oyuncuların kameralarının Culling Mask'ından gizlenir.
  - Masanın ortasındaki daktilonun kavisli metal yüzeyinde (`ReflectionCamera` + RenderTexture) kurbanın arkasında beliren kasabın ve havaya kalkan dev silahın **soğuk mavi / kan kırmızısı silüet yansıması** görülür.
  - Seyirciler için infaz sesleri `AudioLowPassFilter` ile uzaktan boğuk ve yankılı duyulur.
- **Kurbanın Kendisi İçin**:
  - Kurban masadan kalkamaz ve arkasına dönemez (dehşet anı).
  - Önündeki beyaz daktilo kağıdında ve metal parçalarda arkasında yükselen devasa gölgeyi görür; son anda arkadan dev tokmak/tava iner!

---

## 5. 16+ Komik Karakter Emote Sistemi

Oyuncular veya botlar 'E' tuşuna (veya Gamepad Y butonuna) basarak 4 dilimli Radyal Emote Tekerleğini açar (2.5 sn cooldown, ağda senkron):

### A) Kabadayı Kazım (Koltuk 1)
1. **Göğüs Yumruklama (`ChestThump`)**: Gorilla gibi göğsünü yumruklar ("GEL BURAYA!").
2. **Pazu Şişirme (`BicepFlex`)**: Çizgi film gibi tek kolunun pazusunu şişirir ("KAS GÜCÜ!").
3. **Parmakla Tehdit (`PointThreat`)**: Rakibi parmağıyla gösterir ("SIRADAKİ SENSİN!").
4. **Gürültülü Kahkaha (`BoomingLaugh`)**: Başını geriye atıp gövdesi sarsılarak güler ("MUAHAHA!").

### B) Tereddütlü Toni (Koltuk 2)
1. **Titreme & Tırnak Yeme (`TrembleBite`)**: Korkudan tir tir titreyerek tırnaklarını kemirir ("AYY ANNEM!").
2. **Masaya Saklanma (`DuckTable`)**: Başını masa hizasının altına indirip gizlenir ("GÖRMÜYORUM!").
3. **Sahte Ağlama (`FakeCry`)**: Kollarıyla gözlerini ovuşturup sahte gözyaşı döker ("ÜHÜ ÜHÜ!").
4. **Tavuk Taklidi (`ChickenMock`)**: Dirseklerini kanat gibi çırparak rakiple alay eder ("GIDIK GIDIK!").

### C) Sinsi Salih (Koltuk 3)
1. **Parmak Sallama (`FingerWag`)**: "Cık cık cık" yaparak parmağını iki yana sallar ("CIK CIK CIK!").
2. **Alaycı Alkış (`SlowClap`)**: Yavaş, yapmacık ve alaycı bir şekilde el çırpar ("BRAVO DAHA İYİSİ YOK!").
3. **Dil Çıkarma (`TongueOut`)**: Kulaklarını tutup dil çıkarır ("YAA YAA!").
4. **Ele Dürbünle Bakma (`HandInspect`)**: Parmaklarıyla dürbün yapıp rakibin kopan koluna bakar ("ELİNE NE OLDU?").

### D) Şovmen Şinasi (Koltuk 4)
1. **Şapka Reveransı (`HatBow`)**: Silindir şapkasını çıkarıp masaya doğru artistik reverans yapar ("TEŞEKKÜRLER!").
2. **Caz Elleri (`JazzHands`)**: Kollarını iki yana açıp parmaklarını titreterek dans eder ("ŞOV BAŞLASIN!").
3. **Alkış İsteme (`ApplausePlease`)**: Başını yana eğip havaya doğru alkış işareti yapar ("ALKIŞŞŞ!").
4. **Zafer Pozu (`VictoryPose`)**: Tek ayağını atar gibi artistik zafer pozu verir ("ZAFER BENİM!").

---

## 6. Değiştirilecek Varlıklar (Asset Replacement Guide)

Proje şu anda harici hesap gerektirmeyen, %100 taşınabilir prosedürel stilize modeller ve sentetik ses sentezleyicisi ile eksiksiz çalışmaktadır. İleride Mixamo veya Kenney FBX paketleri eklenmek istendiğinde aşağıdaki eşleştirmeler kullanılabilir:

| Varlık / Sistem | Mevcut Durum | Değiştirilecek Önerilen Ücretsiz Paket / Kaynak |
| :--- | :--- | :--- |
| **Karakter Modelleri (1..4)** | `[DEĞİŞTİRİLEBİLİR]` Prosedürel Humanoid Kuklalar (Gözler, şapka, oranlar) | **Mixamo / Kenney Character Assets** (Humanoid Rig destekli çizgi film karakterleri FBX olarak doğrudan atanabilir) |
| **Kasap Modeli** | `[DEĞİŞTİRİLEBİLİR]` Önlüklü ve şapkalı prosedürel iri kapsül model | **Poly Pizza / Kenney Mini Characters** ("Butcher" veya "Chef" low-poly modeli) |
| **4 Absürt Silah** | `[DEĞİŞTİRİLEBİLİR]` Prosedürel Tokmak, Tava, Sosis ve Pasta 3D meshleri | **Kenney Weapon / Prop Pack** veya özel FBX modelleri |
| **Ses Efektleri** | `[HAZIR & ÇALIŞIYOR]` `SoundEffectsGenerator` sentetik dalga motoru | **Freesound.org / Sonniss GDC** (CC0 lisanslı çizgi film foley WAV sesleri) |

