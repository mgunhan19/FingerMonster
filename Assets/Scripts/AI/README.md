# FingerMonster - AI Architecture (Aşama 2 - Tamamlandı)

Bu klasör, masadaki boş koltukları dolduran veya bağlantısı kopan oyuncuların yerini devralan yapay zekâ botlarını yönetir.

## Özellikler
1. **İnsansı Düşünme ve Nişan Alma (Aim Simulation)**:
   - Botlar sırası gelince pat diye basmaz; kişiliğine göre 1-3 saniye tereddüt eder.
   - Seçtiği tuşun üzerine sanki bir insan fare imlecini getirmiş gibi odaklanır (tuş 0.4 saniye sarı renkle yanar), ardından tuşa basar.
2. **Adil Oyun (Fair Play - Hile Yok)**:
   - Botlar tuzak tuşların yerini ASLA bilmez.
   - Sadece basılmış tuşları ve kalan güvenli/tuzak oranlarını tartar.
3. **Kişilikler (`BotPersonality`)**:
   - `Cesur`: Hızlı karar verir (1.0-1.6s), risk almaktan çekinmez, daktilonun merkez tuşlarına yönelir.
   - `Urkek`: Titrek ve tereddütlüdür (2.0-3.0s), kenar tuşları tercih eder.
   - `Sinsi`: Olasılıkları tartar (1.4-2.3s), rakiplerin durumuna göre bekler.
4. **Zorluk Seviyeleri (`BotDifficulty`)**:
   - `Kolay`: Tamamen rastgele seçim.
   - `Normal`: Kişilik eğilimlerine göre dengeli insansı risk.
   - `Zor`: Olasılık ve risk analizi ile en mantıklı dağılımı seçer.

## Sınıflar ve Görevleri
- **`BotData.cs`**: `BotPersonality`, `BotDifficulty` enumları ve bot isim/profil üreten `BotProfile` sınıfı.
- **`AIBrain.cs`**: Karar verme, düşünme süresi, nişan simülasyonu ve hamle yürütme motoru.
- **`AIPlayerController.cs`**: Yerel veya ağ oturumunda bot sırası geldiğinde `AIBrain`'i tetikleyen köprü.
