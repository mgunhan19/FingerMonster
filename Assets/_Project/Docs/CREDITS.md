# FingerMonster Ses ve Varlık Kaynakları (CREDITS)

## Geçici Prosedürel Yer Tutucu Sesler (CC0 / Proje İçi Üretim)

Otomatik tarayıcı oturumu/telif doğrulaması olmadan doğrudan harici CC0 ses dosyaları indirilemediği için aşağıdaki dosyalar **geçici** olarak `44.1 kHz, 16-bit Mono PCM (.wav)` formatında prosedürel sentez ile üretilmiştir. Üretim aşamasında aşağıdaki adımları izleyerek gerçek CC0 ses dosyaları ile değiştirebilirsiniz.

### Hayalet Sesleri (`Assets/_Project/Audio/Ghost/`)
| Dosya | Durum | Açıklama | Süre |
| :--- | :--- | :--- | :--- |
| `ghost_buuu_scare.wav` | **geçici** | Hayalet korkutma ("BUUU") sesi | ~0.85s |
| `ghost_throw_whoosh.wav` | **geçici** | Nesne fırlatma rüzgar/whoosh sesi | ~0.30s |
| `ghost_pie_splat.wav` | **geçici** | Kremalı pasta çarpma/splat sesi | ~0.38s |
| `ghost_tomato_splat.wav` | **geçici** | Domates ezilme/splat sesi | ~0.32s |
| `ghost_paper_hit.wav` | **geçici** | Kağıt topu çarpma/hışırtı sesi | ~0.22s |
| `ghost_sausage_bonk.wav` | **geçici** | Sosis lastiksi bonk sesi | ~0.28s |

### Kasap Silah Sesleri (`Assets/_Project/Audio/Butcher/`)
| Dosya | Durum | Açıklama | Süre |
| :--- | :--- | :--- | :--- |
| `butcher_mallet_hit.wav` | **geçici** | Dev et çekici darbe sesi | ~0.42s |
| `butcher_pan_clang.wav` | **geçici** | Döküm tava metalik çınlama ("DÖNKK") | ~0.65s |
| `butcher_whip_snap.wav` | **geçici** | Sosis kırbacı şaklama ("ŞRAAK") | ~0.28s |
| `butcher_pie_splat.wav` | **geçici** | Kremalı pasta fırlatıcı patlama/splat | ~0.45s |

---

## Gerçek CC0 Seslerle Değiştirme Adımları (Kullanıcı Rehberi)

1. **OpenGameArt.org üzerinden arama (Giriş gerektirmez):**
   - `https://opengameart.org/art-search-advanced` adresine gidin.
   - **Art Type** olarak `Sound Effect`, **Licenses** olarak yalnızca `CC0` kutucuğunu işaretleyin.
   - Önerilen arama terimleri ve paketler:
     - Fırlatma/Whoosh için: `"Swishes sound pack"` veya `whoosh`
     - Splat/Pasta/Domates için: `"Squish Sound Effects"` veya `splat` / `squish`
     - Tokmak/Tava/Kırbaç için: `"37 hits/punches"`, `"100 CC0 Metal and Wood SFX"` veya `pan clang`, `whip crack`, `cartoon bonk`
     - Hayalet "Buuu" için: `ghost boo`, `cartoon ghost`, `monster`
2. **Freesound.org üzerinden arama (Ücretsiz hesap girişi gerekir):**
   - `https://freesound.org/` adresine giriş yapın.
   - Arama çubuğuna ilgili efekti yazın (ör. `pie splat`, `frying pan hit`, `whip crack`, `ghost boo`).
   - Sağdaki **Licenses** filtresinden yalnızca **Creative Commons 0 (CC0)** seçeneğini aktif edin.
   - `.wav` formatındaki dosyaları indirin.
3. **Dosyaları Yerleştirme:**
   - İndirdiğiniz `.wav` (veya `.ogg`) dosyalarını yukarıdaki tablolarda belirtilen **aynı dosya adlarıyla** `Assets/_Project/Audio/Ghost/` ve `Assets/_Project/Audio/Butcher/` klasörlerinin üzerine yazın.
   - Unity Editör'e döndüğünüzde `AudioManager` (`Assets/_Project/Scripts/Audio/AudioManager.cs`) veya ilgili Inspector referanslarını kontrol edin; yeni seslerin lisans/yazar/URL bilgilerini bu `Assets/_Project/Docs/CREDITS.md` dosyasına ekleyin.
