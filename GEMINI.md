# FingerMonster - Ajan Kuralları Özeti (GEMINI.md)

Bu dosya, `AGENTS.md` belgesindeki kuralların Antigravity ajan yürütücüsü için özetidir.

1. **Alt Ajan Zorunluluğu**: Görevler parçalanır, alt ajanlar (`invoke_subagent`) ile paralel çalıştırılır. Ana ajan doğrudan kod yazmaz, koordine eder.
2. **Bağlam İzolasyonu**: Her alt ajana eksiksiz ve bağımsız brief verilir (hedef, dosyalar, kısıtlar, rapor formatı).
3. **Dosya Sahipliği**: Aynı dosyada aynı anda tek ajan çalışır. Önce araştırma, sonra düzenleme.
4. **Kanıtlı Raporlama**: Tahmin yasaktır. Dosya, satır ve hata nedeni kanıtlanmadan işlem yapılmaz.
5. **Derleme Doğrulaması**: Değişiklik sonrası Unity konsolunda `CS...` hatası ve exception olmadığı doğrulanmadan iş bitti denemez.
6. **Tasarım ve Kod Koruması**: İstenmedikçe tasarım/HUD/kod değiştirilmez. Dosya silmeden önce yedek alınır.
7. **Standart Rapor**: Türkçe, maddeli ve doğrulanmış teslim raporu sunulur.
8. **Proje Sabitleri**: URP, Netcode for GameObjects, Singleton kontrolleri, Bodyguards modelleri, ExecutionReflectionSystem yansıma katmanı ve HandSeverSystem kemik ölçeği kuralı (min `0.0001f`).

