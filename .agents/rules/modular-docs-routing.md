# Modüler Mimari Dokümantasyon Yönlendirme Kuralı

FingerMonster projesinde herhangi bir C# script'i, sahne, prefab, ScriptableObject veya proje kuralı üzerinde değişiklik yapmadan önce aşağıdaki yönlendirme protokolü **zorunludur**:

1. **Önce Ana İndekse Bak:** İlk olarak proje kökündeki [`UYGULAMA_KAPSAMLI_REHBERI_VE_GUVENLIK_RAPORU.md`](../../UYGULAMA_KAPSAMLI_REHBERI_VE_GUVENLIK_RAPORU.md) dosyasının **Bölüm 0 (Hızlı Görev Arama Matrisi)** tablosunu incele; hedef dosya yolunu ve ilgili sınıf/metot/değişken sembollerini tespit et.
2. **Yalnızca İlgili Modülü Oku:** Tüm mimari dokümanları topluca okumak yerine yalnızca görevinle doğrudan ilgili `docs/architecture/0X-*.md` modülünü oku:
   - Kırılmaz teknik kurallar, URP/Netcode kısıtları, `0.0001f` kemik ölçeği ve `ghostOnlyLayer = 11`: `docs/architecture/01-design-and-rules.md`
   - Oyun döngüsü, hayalet enerjisi, yetenek kartları, bot kararları: `docs/architecture/02-gameplay-and-economy.md`
   - Çekirdek, Networking, AI, Butcher, Ghost, Players envanteri: `docs/architecture/03-inventory-core-and-services.md`
   - UI, HUD, Kamera, Emote, Gameplay ve Editor araçları envanteri: `docs/architecture/04-inventory-screens-and-components.md`
   - Güvenlik, `GameAutomatedTest`, `ProjectOrganizerTool.ScanMissingReferences` ve batchmode testleri: `docs/architecture/05-security-tests-and-rnd.md`
3. **Değişiklik Sonrası Canlı Dokümanı Güncelle:** Kod, sahne veya dosya yapısında yapılan her kalıcı değişiklikten sonra ilgili `docs/architecture/0X-*.md` modülünü ve gerekiyorsa `UYGULAMA_KAPSAMLI_REHBERI_VE_GUVENLIK_RAPORU.md` ile `PROJE_HARITASI.md` dosyalarını aynı görev içinde güncelle.
