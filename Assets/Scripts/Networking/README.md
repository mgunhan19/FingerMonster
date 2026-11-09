# FingerMonster - Networking Architecture (Aşama 2 - Tamamlandı)

Bu klasör, Unity Netcode for GameObjects (NGO) ve Unity Transport (UTP) altyapısı üzerine kurulu, 4 kişilik yerel ağ (LAN) çok oyunculu sistemini içerir.

## Mimari Prensipler
1. **Server-Authoritative (Yetkili Sunucu)**:
   - Gizli tuzak tuşlarının konumları yalnızca sunucunun belleğinde (`HashSet<int> serverSecretTraps`) tutulur.
   - İstemcilere asla tuzak listesi gönderilmez (hafıza tarama / paket dinleme ile hile yapılamaz).
   - Oyuncular tuşa basma isteklerini `RequestPressKeyServerRpc` ile sunucuya iletir. Sunucu sırayı ve tuşun geçerliliğini doğrular, ardından sonucu `OnKeyResolvedClientRpc` ile herkese yayınlar.
2. **Otomatik Bot Devralma (Disconnect Fallback)**:
   - Bir insan oyuncunun bağlantısı koptuğunda (`OnClientDisconnectCallback`), o koltuk düşmez; sunucu anında bir AI botu o koltuğa atar ve oyun kesintisiz devam eder.
3. **Kendi Koltuğundan Bakış (Multi-Perspective Camera)**:
   - Her istemci bilgisayarı, kendi atandığı koltuğun (Güney, Kuzey, Batı, Doğu) açısından daktiloya ve rakiplerine bakar.

## Sınıflar ve Görevleri
- **`LANDiscovery.cs`**: UDP soketi üzerinden 47777 portundan `FINGER_MONSTER|RoomName|Port|Players|Max` beacon sinyalleri yayınlar ve ağdaki odaları otomatik keşfeder. Zaman aşımına uğrayan odaları temizler.
- **`NetworkPlayerSlot.cs`**: Koltuktaki oyuncunun (insan veya bot) kimliğini, hazır durumunu, puanını ve el sayısını `INetworkSerializable` ile senkronize eden struct.
- **`NetworkGameManager.cs`**: Ana sunucu/istemci oyun döngüsü, RPC doğrulamaları, kasap sekansı ve oyun sonu senkronizasyonu.
- **`NetworkRoomManager.cs`**: Host/Client başlatma, IP/Port yönetimi, lobi durumu ve boş koltukları botlarla doldurma ayarı.
- **`NetworkLobbyUI.cs`**: Oda kurma, otomatik keşfedilen LAN odalarını listeleme, doğrudan IP ile katılma ve 4 koltuklu lobi arayüzü.
