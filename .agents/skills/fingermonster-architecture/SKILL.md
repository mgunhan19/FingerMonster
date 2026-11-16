---
name: fingermonster-architecture
description: FingerMonster Unity projesi için token verimli modüler teknik dokümantasyon (SSOT) gezinme, dosya bulma ve güncelleme becerisi.
---

# FingerMonster Mimari Gezinme ve Güncelleme Becerisi

Bu skill, FingerMonster projesinde kod, sahne, prefab veya ayar değişikliği yaparken tüm codebase'i taramak yerine Merkezi Teknik Beyin (SSOT) üzerinden minimum token ile doğru dosya ve sembollere ulaşmayı sağlar.

## Adım 1 — Hedef Dosyayı Hızlı Matristen Bul
1. `UYGULAMA_KAPSAMLI_REHBERI_VE_GUVENLIK_RAPORU.md` dosyasındaki **Bölüm 0** matrisini oku.
2. İstenen özelliğin gerçek `Assets/_Project/...` yolunu ve sembol adını al.
3. Gerekli ise `PROJE_HARITASI.md` ile disk yerleşimini doğrula.

## Adım 2 — Yalnızca İlgili Mimari Modülü Oku
Görev kapsamına göre sadece gereken modülü aç:
- **Kurallar & Kısıtlar:** `docs/architecture/01-design-and-rules.md`
- **Oynanış & Ekonomi:** `docs/architecture/02-gameplay-and-economy.md`
- **Çekirdek & Servis Envanteri (4.1–4.4):** `docs/architecture/03-inventory-core-and-services.md`
- **UI, Kamera, Efekt & Editor Envanteri (4.5–4.6):** `docs/architecture/04-inventory-screens-and-components.md`
- **Güvenlik, Otomatik Test & Ar-Ge:** `docs/architecture/05-security-tests-and-rnd.md`

## Adım 3 — Kırılmaz Sabitleri Koru
- **Render Pipeline:** URP (`PC_RPAsset.asset`, `Mobile_RPAsset.asset`).
- **Ağ:** Unity Netcode for GameObjects + Unity Transport (Server-Authoritative).
- **El Kopma (`HandSeverSystem.cs`):** Kemik ölçeği asla `0` yapılmaz, minimum `0.0001f` kullanılır.
- **İnfaz Yansıması (`ExecutionReflectionSystem.cs`):** `ghostOnlyLayer = 11`, oda kararır ve yansıma daktilo silindirinde parlar.
- **Arşivleme:** Kullanılmayan dosyalar silinmez, `Assets/_Archive/` altına taşınır ve `Assets/_Project/Docs/ArsivListesi.md` güncellenir.

## Adım 4 — Doğrula ve Dokümanı Senkronize Et
1. Unity `6000.3.19f1` batchmode ile derle (`CS...` hatası olmamalı).
2. `GameAutomatedTest` ve `ProjectOrganizerTool.ScanMissingReferences` ile sahne/referans bütünlüğünü doğrula.
3. Eklenen veya değişen her script/sembol için `UYGULAMA_KAPSAMLI_REHBERI_VE_GUVENLIK_RAPORU.md` ve ilgili `docs/architecture/0X-*.md` dosyasını güncelle.
