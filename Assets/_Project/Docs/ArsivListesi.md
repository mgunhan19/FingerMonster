# FingerMonster — Arşiv Listesi (`ArsivListesi.md`)

Bu belge, proje temizliği sırasında silinmek yerine güvenli şekilde `Assets/_Archive/` klasörü altına taşınan tüm dosya gruplarını ve arşivlenme nedenlerini içerir.

---

## Arşivlenen Gruplar

| Arşiv Klasörü (`Assets/_Archive/...`) | Eski Konumu | İçerik Özeti | Neden Arşivlendi? |
| :--- | :--- | :--- | :--- |
| `BodyGuards_Original/` | `Assets/BodyGuards/` | `BodyGuardsScene.unity`, `BodyGuardsSceneSettings.lighting`, `ReadMe.txt.txt`, `Meshes/` (`screen.fbx`, `SkelMesh_Bodyguard_01..04.fbx`), `Meshes/Materials/` (`Screen.mat`, `Boduguard_01..03_D.mat`), `Textures/` | Aktif oyuncu karakter varlıkları `Assets/_Project/Art/Characters/Bodyguards/` altına taşındı; orijinal paketin demo sahnesi, ekran modeli ve yedek kopyaları aktif proje ağacını kalabalıklaştırmaması için arşivlendi. |
| `Debug-Scripts/` | `Assets/Scripts/Gameplay/` ve `Assets/Scripts/Editor/` | `Editor/` altında `CharacterDiagnosticTool.cs`, `FindAllCharactersInScene.cs`, `FindInvalidAABBInScene.cs`, `ForgeAssetInspector.cs`, `NaNDetector.cs`, `RemoveAllParticlesTool.cs`, `ValidateForgeEnvironment.cs` | Geliştirme sürecinde tek seferlik hata ayıklama (AABB, NaN, partikül, karakter/ortam doğrulama) amacıyla yazılmış geçici araçlar. Günlük derleme ve menü kalabalığını azaltmak için arşivlendi. |
| `SampleScene/` | `Assets/Scenes/SampleScene.unity`, `Assets/Settings/SampleSceneProfile.asset` | Unity URP şablonundan gelen varsayılan örnek sahne ve Volume profili | Projede aktif olarak yalnızca `Assets/_Project/Scenes/MainMenu.unity` ve `Game.unity` kullanıldığı için şablon sahnesi arşivlendi. |
| `TutorialInfo/` | `Assets/TutorialInfo/`, `Assets/Readme.asset` | `Icons/URP.png`, `Scripts/Readme.cs`, `Scripts/Editor/ReadmeEditor.cs`, `Layout.wlt`, `Readme.asset` | Unity URP proje şablonuyla birlikte gelen başlangıç eğitim dosyaları; proje için gerekli olmadığı ve silinme yasağı bulunduğu için arşivlendi. |
| `UnusedMaterials/` | `Assets/Materials/` | `Mat_EyeWhite.mat`, `Mat_Player_0..3.mat`, `Mat_PlayerHand_0..3.mat`, `Mat_PlayerHead_0..3.mat`, `Mat_Steel.mat`, `Mat_TopHat.mat` | Eski prototip küp/placeholder karakter döneminden kalan ve artık aktif sahnelerde kullanılmayan materyaller. |

---

## Notlar
- `AGENTS.md` kuralı gereği kullanılmayan veya geçici dosyalar doğrudan silinmez; `Assets/_Archive/` altına taşınır ve bu dosya güncellenir.
- Taşıma işlemlerinin tam kaynak/hedef kaydı `Assets/_Project/Docs/MoveLog.csv` dosyasında tutulmaktadır.
