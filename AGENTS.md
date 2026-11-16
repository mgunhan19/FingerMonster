# FingerMonster - Geliştirici & Ajan Kuralları (AGENTS.md)

Bu belge, FingerMonster projesinde çalışacak tüm yapay zeka ajanları ve alt ajanlar (subagents) için zorunlu, bağlayıcı ve kalıcı çalışma kurallarını içerir.

---

## 1. Alt Ajan (Subagent) Kullanım Zorunluluğu ve Koordinasyon
- **Ana Ajanın Rolü**: Ana ajan (coordinator), tek satırlık ve önemsiz düzeltmeler hariç tek başına doğrudan kod yazmaz.
- **Görev Bölme**: Her görev önce mantıksal, bağımsız parçalara bölünür (araştırma, denetim, düzenleme, doğrulama).
- **Alt Ajan Çağrısı**: Her parça için uygun alt ajan (`invoke_subagent`) başlatılır. Ana ajan sadece koordinasyon, doğrulama ve birleştirme yapar.

## 2. Alt Ajan Bağlamı (Context) İlkesi
- Alt ajanlar ana ajanın sohbet geçmişini devralmaz (context isolation).
- Her alt ajana gönderilen görev tarifi **kısa, eksiksiz ve bağımsız** olmalıdır:
  - Proje özeti ve ilgili sistem bilgisi,
  - Ulaşılması gereken net hedef,
  - Dokunabileceği / okuyacağı dosyaların listesi (dosya sahipliği),
  - Kesin yasaklar ve kısıtlamalar,
  - Beklenen çıktı formatı (kanıtlı rapor formatı).

## 3. Dosya Sahipliği ve Çakışma Yönetimi
- Aynı dosyayı iki alt ajan kesinlikle aynı anda düzenleyemez. Her alt ajana açık dosya sahipliği atanır.
- Çakışma riski varsa ilgili alt ajan ayrı bir git branch/worktree üzerinde çalıştırılır ve sonrasında ana ajan tarafından birleştirilir.
- Sıralama kuralı: Önce araştırma ve okuma yapan ajanlar çalışır, bulgular onaylandıktan sonra düzenleme yapan ajanlar devreye girer.

## 4. "Önce Kanıtla Raporla" İlkesi
- Alt ajanlar bir sorunu iddia ederken veya çözüm önerirken mutlaka kanıt sunmalıdır:
  - **Dosya yolu** ve **satır numarası**,
  - **Hatanın / problemin teknik nedeni**,
  - **Önerilen kesin çözüm**.
- Tahmin yürütülmez. Ana ajan doğrulamadan ve onay vermeden doğrudan dosya düzenlenmez.

## 5. Doğrulama ve Derleme Zorunluluğu
- Her kod/sahne değişikliğinden sonra Unity projesi derlenir.
- Console çıktısında hiçbir `CS...` derleme hatası ve yeni çalışma zamanı exception'ı olmadığı doğrulanmalıdır.
- Test edilip doğrulanmayan hiçbir iş "tamamlandı" olarak raporlanamaz.

## 6. Proje Bütünlüğü ve Tasarım Koruması
- Kullanıcı açıkça talep etmedikçe yeni özellik eklenmez; tasarım, HUD, ortam, ışıklandırma ve kamera açıları bozulmaz.
- Herhangi bir dosya silinmeden önce mutlaka yedeği alınır.

## 7. Raporlama Standardı
- Her görev tamamlandığında kullanıcıya Türkçe, kanıtlı ve net bir rapor sunulur:
  - **(a) Yapılanlar**,
  - **(b) Değişen / Eklenen Dosyalar**,
  - **(c) Doğrulama ve Derleme Sonucu**,
  - **(d) Varsa Kalan Sorunlar**,
  - **(e) Kullanıcının Elle Yapması Gerekenler**.

## 8. Proje Gerçekleri ve Teknik Kısıtlar (FingerMonster)
- **Render Pipeline**: Universal Render Pipeline (URP).
- **Ağ Altyapısı**: Unity Netcode for GameObjects + Unity Transport (Server-Authoritative LAN lobi ve host/client akışı).
- **Singleton & DontDestroyOnLoad**: `AudioManager`, `CartoonFXManager`, `GameManager` gibi yöneticilerin mükerrer oluşumu engellenmeli, sahne geçişlerinde bellek sızıntısına yol açılmamalıdır.
- **Karakter Sistemi**: 4 farklı çizgi film arketipi (Feg, Kenan, Feyzi, MMG / Kabadayı, Korkak, Sinsi, Şovmen) ve Bodyguards modelleri.
- **İnfaz Sekansı (ExecutionReflectionSystem)**: Oda kararır, seyirciler kasabı doğrudan göremez; silüet ve yansıma daktilo silindirinde parlar.
- **El Kopma Mekaniği (HandSeverSystem & SeveredHandPhysics)**: Kopan el kemiklerinin ölçeği sıfır (`0`) yapılmaz; transform bozulmasını ve NaN hatalarını önlemek için minimum `0.0001f` değerine çekilir.

