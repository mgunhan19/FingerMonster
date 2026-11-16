# FingerMonster — MCP ve Alt Ajan (Subagent) Kullanım Rehberi

Bu rehber, **FingerMonster** projesinde Antigravity ile **Unity 6 (`6000.3.19f1` URP)** arasındaki **Model Context Protocol (`unityMCP`)** bağlantısını ve bundan sonraki tüm görevlerde kullanılacak **kalıcı kurallar ile 5 özel alt ajanı (`subagents`)** eksiksiz biçimde kullanmanız için hazırlanmıştır.

> Tüm yapılandırmalar oyun koduna (`Assets/Scripts/...`) dokunulmadan kurulmuş ve proje içindeki resmi dokümanlar / paket kaynak kodları okunarak doğrulanmıştır.

---

## 1. Ne Kuruldu ve Hangi Dosyalar Hazırlandı?

| Bileşen | Dosya / Konum | Açıklama ve Doğrulanan Kaynak |
| :--- | :--- | :--- |
| **Kalıcı Kurallar** | `AGENTS.md` ve `GEMINI.md` | Alt ajan kullanma zorunluluğu, dosya sahipliği, "önce kanıtla raporla" kuralı, Unity derleme kontrolü, yedek alma ve Türkçe raporlama standardı + proje sabitleri (`URP`, `Netcode`, `Bodyguards`, `ExecutionReflectionSystem`, `HandSeverSystem` min `0.0001f`). |
| **5 Özel Alt Ajan** | `.agents/agents/` altında:<br>• `unity-compile-fixer.md`<br>• `unity-lifecycle-reviewer.md`<br>• `unity-scene-auditor.md`<br>• `unity-netcode-reviewer.md`<br>• `unity-ui-input-reviewer.md` | Antigravity resmi formatına (`name`, `description`, `subagent: true`, `mainAgent: false`; `migrate-agent-settings/SKILL.md` doğrulamalı) uygun tanımlandı. Hiçbirine kısıtlayıcı/hatalı `tools` alanı yazılmadı ve hepsi **"önce kanıtla raporla, onay almadan dosya değiştirme"** kuralına bağlandı. |
| **Unity MCP Paketi** | `Packages/manifest.json` | `"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity"` paketi eklendi (`10.3.1-beta.11`, Unity `2021.3+` / `6000.3.19f1` uyumlu). Paket `Library/PackageCache/com.coplaydev.unity-mcp@4bb5477d05fb` altında başarıyla çözümlendi ve `Editor.log` içinde **`0` CS hatası** ile derlendi. |
| **Global ve Proje MCP Ayarı** | • Global: `C:\Users\mgunh\.gemini\config\mcp_config.json` (Antigravity 2.0) & `~/.gemini/antigravity-ide/mcp_config.json`<br>• Proje referans: `.agents/mcp_config.json` | Unity paketindeki `AntigravityConfigurator.cs` global ayara `"serverUrl": "http://127.0.0.1:8080/mcp"` (`"type": "http"`) yazdı ve Antigravity 46 adet `unityMCP` aracını yükledi. Proje içi `.agents/mcp_config.json` ise `uvx --prerelease explicit --from mcpforunityserver>=0.0.0a0 mcp-for-unity --transport stdio` komutuyla yedek/stdio olarak ayarlandı. |
| **Windows Yedek Köprü Script'i** | `tools/unity_mcp_bridge/mcp_http_bridge.py` | Unity `Assets/` klasörü **dışında** tutulur (Unity C# derlemesini etkilemez). Antigravity'nin doğrudan HTTP bağlantısında `"invalid trailing data"` veya `"Mcp-Session-Id"` hatası vermesi durumunda stdio JSON satırlarını `http://127.0.0.1:8080/mcp` adresine güvenli biçimde çevirir. |
| **Sistem Önkoşulları** | Kurulu & Doğrulandı | `git 2.56.0`, `Python 3.12.10`, `uv 0.13.8`, `uvx 0.13.8` ve `mcpforunityserver 10.3.0` kurulu ve çalışır durumda doğrulandı. |

---

## 2. Kullanıcının Elle Yapması Gereken Adımlar (Tıkla-Tıkla)

Paket Unity'ye kurulmuş ve Antigravity tarafındaki `unityMCP` tanımları yüklenmiş durumdadır. Canlı bağlantı için Unity ve Antigravity'de şu adımları izleyin:

### Adım A — Unity Editor Tarafı (Her Oturumda Bağlantıyı Başlatma)
1. Unity Editor penceresine geçin (açık değilse projeyi açın) ve **Console** sekmesinde kırmızı bir `CS...` derleme hatası olmadığını kontrol edin.
2. Unity üst menüsünden **`Window > MCP for Unity`** yoluna tıklayın (veya klavyeden **`Ctrl + Shift + M`** kısayoluna basın). *(Kaynak: Paket `README.md` satır 5-6 ve 94)*
3. Açılan pencerede sırasıyla şu bölümleri kontrol edin:
   - **MCP Client Configuration**: Client olarak **Antigravity 2.0** (veya kullandığınız IDE ise **Antigravity IDE**) seçili olsun. Daha önce yapmadıysanız **`Auto Configure`** / **`Auto-Setup`** butonuna bir kez basın.
   - **Server Status**: **`HTTP Server Command`** başlığını açın ve gerekirse **`Start Local HTTP Server`** butonuna tıklayın (`http://127.0.0.1:8080/mcp` / `MCP 6500` portunu başlatır).
   - **Unity Bridge**: Durum **`Stopped`** görünüyorsa **`Start Bridge`** butonuna tıklayın; durumun **`Running`** (yeşil nokta) olduğundan emin olun.

### Adım B — Antigravity Tarafı (MCP Araçlarını Yenileme)
1. Antigravity penceresinin sağ üst / sohbet araç çubuğundan **`Additional Options (...) > MCP Servers`** menüsünü açın. *(Kaynak: `agy-customizations/docs/mcp_servers.md` satır 92-93)*
2. Listede **`unityMCP`** sunucusunun etkin (enabled) olduğunu görün. Gerekirse **Yenile / Reload** yapın veya Antigravity'yi kapatıp açın.

---

## 3. Bağlantı Testi Komutu (Gerçek Araç Adlarıyla)

Unity'de **Start Local HTTP Server** + **Start Bridge** (`Running`) aktif olduktan sonra Antigravity sohbetine şu test komutunu yazabilirsiniz:

> *"Unity MCP bağlantısını doğrula: `read_console` (`action: "get"`, `count: "5"`) ile son 5 konsol mesajını kontrol et, ardından `manage_scene` (`action: "get_active"` ve `action: "get_hierarchy"`) ve `find_gameobjects` kullanarak aktif sahnedeki ilk nesneleri raporla."*

Doğrulanmış gerçek `unityMCP` araç isimlerinden bazıları (tamamı `C:\Users\mgunh\.gemini\antigravity\mcp\unityMCP\` altında kayıtlıdır):
- **Konsol & Editör**: `read_console`, `manage_editor`, `execute_menu_item`, `refresh_unity`
- **Sahne & Hiyerarşi**: `manage_scene`, `find_gameobjects`, `manage_gameobject`, `manage_components`
- **Script & Varlık**: `manage_script`, `validate_script`, `manage_asset`, `manage_prefabs`, `manage_ui`, `manage_camera`

---

## 4. Sık Karşılaşılan Hatalar ve Çözümleri

### Sorun 1: Bağlantı kurulamıyor / `[WinError 10061] Hedef makine etkin olarak reddettiğinden bağlantı kurulamadı`
- **Neden**: Unity Editor kapalıdır veya `Window > MCP for Unity` içindeki **Local HTTP Server** / **Unity Bridge** başlatılmamıştır.
- **Çözüm**: Unity'de `Ctrl + Shift + M` ile pencereyi açın; sırasıyla **`Start Local HTTP Server`** ve **`Start Bridge`** butonlarına tıklayın. Adresin `localhost` yerine **`http://127.0.0.1:8080/mcp`** olduğundan emin olun (Windows'ta `localhost` bazen IPv6 `::1` adresine çözümlendiği için bağlantı reddedilebilir; paketteki `HttpEndpointUtility.cs` bu nedenle `127.0.0.1` kullanır).

### Sorun 2: Port çakışması (`8080` veya `6500` portu başka uygulamada kullanımda)
- **Neden**: Başka bir yerel servis (Docker, web sunucusu, Unreal MCP vb.) aynı portu dinliyordur.
- **Çözüm**:
  1. Unity'de `Window > MCP for Unity` içindeki URL/port ayarından yerel portu boş bir porta (ör. `8081`) çekin.
  2. `C:\Users\mgunh\.gemini\config\mcp_config.json` içindeki `unityMCP.serverUrl` değerini `"http://127.0.0.1:8081/mcp"` olarak güncelleyin (veya `Auto Configure` butonuna tekrar basın).
  3. Yedek Python köprüsü kullanıyorsanız `env` içine `"UNITY_MCP_HTTP_URL": "http://127.0.0.1:8081/mcp"` yazın.

### Sorun 3: Paket çözülmüyor veya `CS...` hatası çıkıyor
- **Neden**: `git` PATH üzerinde bulunamıyor olabilir veya Unity AssetDatabase yenilenmemiştir.
- **Çözüm**:
  1. Terminalde `git --version` komutunu doğrulayın (`git 2.56.0` kurulu görünüyor).
  2. `Packages/manifest.json` içinde `"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity"` satırının durduğunu doğrulayın.
  3. Unity'de `Window > Package Manager` açıp sol üstteki yenileme ikonuna basın ve Console çıktısını temizleyin.

### Sorun 4: Özel alt ajanlar (`unity-*`) listede görünmüyor veya çalışmıyor
- **Neden**: `.agents/agents/*.md` YAML başlığında (frontmatter) sözdizimi hatası veya geçersiz bir `tools:` kısıtlaması olabilir.
- **Çözüm**: `.agents/agents/` altındaki 5 dosyada yalnızca `name`, `description`, `subagent: true`, `mainAgent: false` alanlarının bulunduğunu kontrol edin (mevcut tümünde doğrulanmıştır), ardından sohbeti/pencereyi yenileyin.

### Sorun 5 (Windows): `"invalid trailing data"` veya `"Mcp-Session-Id"` hatası alıyorum
- **Neden**: Antigravity HTTP istemcisi ile sunucunun SSE/chunked yanıtı veya `Mcp-Session-Id` oturum başlığı (header) Windows üzerinde uyumsuz kalabiliyor.
- **Çözüm A (`uvx` Stdio modu)**:
  `C:\Users\mgunh\.gemini\config\mcp_config.json` içindeki `unityMCP` bloğunu şununla değiştirin:
  ```json
  "unityMCP": {
    "command": "uvx",
    "args": [
      "--prerelease",
      "explicit",
      "--from",
      "mcpforunityserver>=0.0.0a0",
      "mcp-for-unity",
      "--transport",
      "stdio"
    ]
  }
  ```
- **Çözüm B (Yedek Python Köprüsü — `tools/unity_mcp_bridge/mcp_http_bridge.py`)**:
  Bu script Antigravity'den **stdio** ile tek satır JSON-RPC okur, `http://127.0.0.1:8080/mcp` adresine POST atar, sunucudan gelen `Mcp-Session-Id` başlığını sonraki tüm isteklere ekler, SSE (`data: ...`) veya trailing data fazlalıklarını `json.JSONDecoder().raw_decode` ile temizler. Kullanmak için `C:\Users\mgunh\.gemini\config\mcp_config.json` içine şunu yazın:
  ```json
  "unityMCP": {
    "command": "python",
    "args": [
      "C:\\Users\\mgunh\\Desktop\\New_GameFingerMonster\\FingerMonster\\tools\\unity_mcp_bridge\\mcp_http_bridge.py"
    ],
    "env": {
      "UNITY_MCP_HTTP_URL": "http://127.0.0.1:8080/mcp"
    }
  }
  ```

---

## 5. Alt Ajanları Çağırma Komut Şablonu

Bundan sonraki her hata ayıklama veya geliştirme isteğinizde aşağıdaki şablonu kopyalayıp köşeli parantezleri doldurarak Antigravity'ye gönderebilirsiniz:

```text
AGENTS.md kurallarına uyarak ilerle. Tek başına kod yazma; önce görevi parçalara ayır ve ilgili alt ajanları (unity-compile-fixer, unity-lifecycle-reviewer, unity-scene-auditor, unity-netcode-reviewer, unity-ui-input-reviewer) invoke_subagent ile çalıştır.

HEDEF: [Çözmek istediğiniz sorun veya görev — örn. Yeniden oyna sonrası karakter spawn olmaması ve Q/E emote hedef seçimi hatası]
DOKUNULMAYACAKLAR: [Örn. Mevcut HUD tasarımı, ışıklandırma, kamera açıları; istenmeyen yeni özellik ekleme]
ÇALIŞMA KURALI:
1. Önce salt-okunur araştırma ajanları dosya yolu + satır numarası kanıtıyla kök nedeni raporlasın.
2. Onay/doğrulama sonrası yalnızca dosya sahibi tek ajan düzenleme yapsın.
3. İşlem sonunda read_console / Editor.log üzerinden 0 CS hatası ve 0 yeni exception doğrulansın ve standart Türkçe rapor verilsin.
```
