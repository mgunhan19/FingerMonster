using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using FingerMonster.AI;
using FingerMonster.Core;
using FingerMonster.Data;
using FingerMonster.Player;
using FingerMonster.Gameplay;
using FingerMonster.UI;
using UnityEngine.UI;

namespace FingerMonster.Editor
{
    /// <summary>
    /// FingerMonster oyun mekaniklerini, durum makinesini (FSM), tuş çözümleme mantığını,
    /// el kopma ve kasap sekansı koşullarını otomatik olarak test eden test aracı.
    /// </summary>
    public static class GameAutomatedTest
    {
        [MenuItem("FingerMonster/Run Game Verification Test")]
        public static void RunVerification()
        {
            Debug.Log("<color=cyan>=== [FINGER MONSTER VERIFICATION TEST BAŞLADI] ===</color>");

            // 1. Sahneyi oluştur ve yükle
            SceneSetupTool.BuildAllScenes();
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Single);
            Assert(scene.IsValid(), "Game.unity sahnesi geçerli değil!");

            // 2. GameManager varlığını kontrol et
            GameManager gm = Object.FindFirstObjectByType<GameManager>();
            Assert(gm != null, "GameManager sahnede bulunamadı!");

            // 3. TypewriterMachine ve 20 tuş kontrolü
            TypewriterMachine tw = Object.FindFirstObjectByType<TypewriterMachine>();
            Assert(tw != null, "TypewriterMachine sahnede bulunamadı!");
            Assert(tw.Keys.Count == 20, $"Daktilo üzerinde tam 20 tuş olmalı, bulunan: {tw.Keys.Count}");

            // 4. Oyuncular ve Koltuklar kontrolü
            PlayerSeat[] seats = Object.FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None);
            Assert(seats.Length == 4, $"Masa etrafında 4 koltuk bulunmalı, bulunan: {seats.Length}");
            for (int i = 0; i < seats.Length; i++)
            {
                Assert(seats[i].Avatar != null, $"Koltuk {i + 1} üzerinde kukla avatar bulunmalı!");
            }

            // 4b. Çizgi Film FX ve AI Kontrolü
            CartoonFXManager fx = Object.FindFirstObjectByType<CartoonFXManager>();
            Assert(fx != null, "CartoonFXManager sahnede bulunamadı!");

            FingerMonster.AI.AIPlayerController aiCtrl = Object.FindFirstObjectByType<FingerMonster.AI.AIPlayerController>();
            Assert(aiCtrl != null, "AIPlayerController sahnede bulunamadı!");

            // 4c. Kamera Pozisyonu ve Kilidi Kontrolü (Kullanıcı Ayarı)
            PlayerCameraController pcc = Object.FindFirstObjectByType<PlayerCameraController>();
            Assert(pcc != null, "PlayerCameraController sahnede bulunamadı!");
            pcc.ApplyFixedTransform();
            Camera mainCam = pcc.GetComponent<Camera>();
            Assert(mainCam != null, "Camera bileşeni bulunamadı!");
            Assert(Mathf.Abs(mainCam.transform.position.x) < 0.01f, $"Kamera X pozisyonu 0 olmalı, mevcut: {mainCam.transform.position.x}");
            Assert(Mathf.Abs(mainCam.transform.position.y - 2.45f) < 0.05f, $"Kamera Y pozisyonu 2.45 olmalı, mevcut: {mainCam.transform.position.y}");
            Assert(Mathf.Abs(mainCam.transform.position.z - (-3.25f)) < 0.05f, $"Kamera Z pozisyonu -3.25 olmalı, mevcut: {mainCam.transform.position.z}");
            Assert(Mathf.Abs(mainCam.fieldOfView - 68f) < 0.1f, $"Kamera FOV 68 olmalı, mevcut: {mainCam.fieldOfView}");

            // 5. Oyun Başlatma Testi (Manuel simülasyon)
            gm.InitializeGame();
            Assert(gm.Players.Count == 4, $"Oyuncu listesi 4 kişi olmalı, bulunan: {gm.Players.Count}");
            Assert(gm.TrapIndices.Count == 6, $"Tuzak sayısı tam 6 olmalı, bulunan: {gm.TrapIndices.Count}");

            for (int i = 0; i < 4; i++)
            {
                var p = gm.Players[i];
                Assert(p.IsAlive, $"Oyuncu {i + 1} tur başında hayatta olmalı!");
                Assert(p.RemainingHandsCount == 2, $"Oyuncu {i + 1} tur başında 2 ele sahip olmalı!");
                Assert(p.Score == 0, $"Oyuncu {i + 1} tur başında 0 puana sahip olmalı!");
            }

            // 6. Güvenli Tuş Basma Testi (PressKeyCommand)
            int safeKey = -1;
            for (int k = 0; k < 20; k++)
            {
                if (!gm.TrapIndices.Contains(k))
                {
                    safeKey = k;
                    break;
                }
            }
            Assert(safeKey != -1, "Güvenli tuş bulunamadı!");

            int activeP = gm.ActivePlayerIndex;
            gm.ForceSetState(GameState.PlayerTurn);
            var safeCmd = new PressKeyCommand(activeP, safeKey);
            bool executed = gm.ProcessCommand(safeCmd);
            Assert(executed, "Güvenli tuş komutu başarıyla yürütülmeliydi!");
            Assert(gm.Players[activeP].Score == 10, $"Güvenli tuşa basan oyuncunun skoru 10 olmalıydı, mevcut: {gm.Players[activeP].Score}");
            Assert(gm.Players[activeP].RemainingHandsCount == 2, "Güvenli tuşa basan oyuncu el kaybetmemeli!");
            Assert(gm.IsKeyPressed(safeKey), "Tuş basılmış olarak işaretlenmeli!");

            // 7. Tuzak Tuş Basma Testi
            int trapKey = -1;
            foreach (int t in gm.TrapIndices)
            {
                trapKey = t;
                break;
            }
            Assert(trapKey != -1, "Tuzak tuş bulunamadı!");

            var trapCmd = new PressKeyCommand(activeP, trapKey);
            // Durumu elle PlayerTurn yaparak test edelim
            executed = gm.ProcessCommand(trapCmd);
            // Eğer durum KeyResolve ise komut reddedilir, ResolveKey doğrudan test edelim:
            gm.ResolveKey(activeP, trapKey);

            Assert(gm.Players[activeP].RemainingHandsCount == 1, $"Tuzak tuşa basan oyuncunun 1 eli kopmalıydı, kalan: {gm.Players[activeP].RemainingHandsCount}");
            Assert(gm.Players[activeP].IsAlive, "1 eli kalan oyuncu hâlâ hayatta olmalıdır!");

            // 8. İkinci Tuzağa Basma ve Elenme (Kasap Sekansı koşulu)
            int secondTrap = -1;
            foreach (int t in gm.TrapIndices)
            {
                if (t != trapKey)
                {
                    secondTrap = t;
                    break;
                }
            }
            Assert(secondTrap != -1, "İkinci tuzak tuş bulunamadı!");

            gm.ResolveKey(activeP, secondTrap);
            Assert(gm.Players[activeP].RemainingHandsCount == 0, "İki tuzağa basan oyuncunun tüm elleri kopmalıdır!");
            Assert(!gm.Players[activeP].IsAlive, "Elleri kalmayan oyuncu elenmiş (ölü) olmalıdır!");

            // 9. TurnController Kontrolü
            TurnController tc = Object.FindFirstObjectByType<TurnController>();
            int aliveCount = tc.GetAlivePlayersCount(gm.Players);
            Assert(aliveCount == 3, $"Hayattaki oyuncu sayısı 3 olmalıdır, bulunan: {aliveCount}");

            int nextAlive = tc.AdvanceToNextAlivePlayer(gm.Players);
            Assert(nextAlive != activeP, "Sıra elenen oyuncuya geçmemeli, hayattaki bir oyuncuya geçmelidir!");

            Debug.Log("<color=green>=== [AŞAMA 1 TESTLERİ BAŞARIYLA GEÇTİ (100% OK)] ===</color>");
        }

        [MenuItem("FingerMonster/Run Stage 2 LAN & AI Verification")]
        public static void RunStage2Verification()
        {
            Debug.Log("<color=cyan>=== [AŞAMA 2: LAN MULTIPLAYER & AI TESTİ BAŞLADI] ===</color>");

            // Sahneleri inşa et
            SceneSetupTool.BuildAllScenes();

            // 1. MainMenu Kontrolleri
            var menuScene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity", OpenSceneMode.Single);
            Assert(menuScene.IsValid(), "MainMenu.unity sahnesi geçerli değil!");

            var nmInMenu = Object.FindFirstObjectByType<Unity.Netcode.NetworkManager>();
            Assert(nmInMenu != null, "MainMenu sahnesinde NetworkManager bulunamadı!");

            var roomMgr = Object.FindFirstObjectByType<FingerMonster.Networking.NetworkRoomManager>();
            Assert(roomMgr != null, "MainMenu sahnesinde NetworkRoomManager bulunamadı!");

            var discovery = Object.FindFirstObjectByType<FingerMonster.Networking.LANDiscovery>();
            Assert(discovery != null, "MainMenu sahnesinde LANDiscovery bulunamadı!");

            var lobbyUI = Object.FindFirstObjectByType<FingerMonster.UI.NetworkLobbyUI>(FindObjectsInactive.Include);
            Assert(lobbyUI != null, "MainMenu sahnesinde NetworkLobbyUI bulunamadı!");

            // 2. Game Sahnesi Kontrolleri
            var gameScene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Single);
            Assert(gameScene.IsValid(), "Game.unity sahnesi geçerli değil!");

            var nmInGame = Object.FindFirstObjectByType<Unity.Netcode.NetworkManager>();
            Assert(nmInGame != null, "Game sahnesinde NetworkManager bulunamadı!");

            var netGameMgr = Object.FindFirstObjectByType<FingerMonster.Networking.NetworkGameManager>();
            Assert(netGameMgr != null, "Game sahnesinde NetworkGameManager bulunamadı!");

            // 3. Bot Profili ve Yapay Zeka Kontrolü
            for (int i = 0; i < 4; i++)
            {
                var profile = FingerMonster.AI.BotProfile.GenerateRandom(i);
                Assert(!string.IsNullOrEmpty(profile.BotName), $"Bot {i} için geçerli bir isim üretilmeli!");
                Assert(profile.SlotIndex == i, $"Bot {i} slot indeksi doğru atanmalı!");
            }

            Debug.Log("<color=green>=== [AŞAMA 2: LAN MULTIPLAYER & AI TESTLERİ BAŞARIYLA GEÇTİ (100% OK)] ===</color>");
        }

        [MenuItem("FingerMonster/Run Stage 3 Characters & Weapons Verification")]
        public static void RunStage3Verification()
        {
            Debug.Log("<color=cyan>=== [AŞAMA 3: 3D BODYGUARD KARAKTERLER, URP, RIG, KASAP VE YANSIMA TESTİ BAŞLADI] ===</color>");

            // 1. Bodyguard varlıkları ve sahneleri inşa et, Game sahnesini aç
            BodyguardSetupTool.EnsurePrefabsExist();
            SceneSetupTool.BuildAllScenes();
            var gameScene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Single);
            Assert(gameScene.IsValid(), "Game.unity sahnesi geçerli değil!");

            // 2. 4 Karakter Prefab'ı, Humanoid Rig, HandSeverSystem ve URP Materyal Kontrolü
            string[] expectedNames = { "Feg", "Kenan", "Feyzi", "MMG" };
            for (int c = 0; c < expectedNames.Length; c++)
            {
                var def = CharacterDefinition.GetDefinition((CharacterArchetype)c);
                Assert(def.DisplayName == expectedNames[c], $"Karakter {c} adı {expectedNames[c]} olmalı, bulunan: {def.DisplayName}");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.PrefabAssetPath);
                Assert(prefab != null, $"Prefab eksik: {def.PrefabAssetPath}");
            }

            PlayerSeat[] seats = Object.FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None);
            Assert(seats.Length == 4, $"4 oyuncu koltuğu bulunmalı, bulunan: {seats.Length}");

            for (int i = 0; i < seats.Length; i++)
            {
                Assert(seats[i].Avatar != null, $"Koltuk {i} üzerinde Avatar bulunmalı!");
                var driver = seats[i].Avatar.GetComponent<CharacterAnimatorDriver>();
                Assert(driver != null, $"Koltuk {i} avatarında CharacterAnimatorDriver bulunmalı!");

                Animator anim = seats[i].Avatar.GetComponentInChildren<Animator>();
                Assert(anim != null, $"Koltuk {i} avatarında Animator bulunmalı!");
                Assert(anim.avatar != null && anim.avatar.isValid && anim.avatar.isHuman, $"Koltuk {i} Humanoid Avatar geçerli olmalı!");
                Assert(anim.runtimeAnimatorController != null, $"Koltuk {i} AnimatorController atanmış olmalı!");
                Assert(anim.GetComponent<CharacterIKBridge>() != null, $"Koltuk {i} Animator nesnesinde CharacterIKBridge bulunmalı!");

                HandSeverSystem sever = seats[i].Avatar.GetComponent<HandSeverSystem>();
                Assert(sever != null, $"Koltuk {i} avatarında HandSeverSystem bulunmalı!");
                sever.ResolveBones();
                Assert(sever.LeftHandBone != null, $"Koltuk {i} üzerinde Humanoid LeftHand kemiği bulunmalı!");
                Assert(sever.RightHandBone != null, $"Koltuk {i} üzerinde Humanoid RightHand kemiği bulunmalı!");

                // Tüm Renderer materyalleri URP olmalı (Standard veya Error shader olmamalı)
                foreach (Renderer r in seats[i].Avatar.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material m in r.sharedMaterials)
                    {
                        Assert(m != null && m.shader != null, $"Koltuk {i} renderer ({r.name}) üzerinde null materyal/shader!");
                        string sName = m.shader.name;
                        Assert(sName != "Standard" && sName != "Standard (Specular setup)" && !sName.Contains("Error"),
                            $"Koltuk {i} ({r.name}) pembe/Standard shader içeriyor: {sName}");
                    }
                }
            }

            // 3. 16+ Emote Tanımı Kontrolü
            var emotes = System.Enum.GetValues(typeof(EmoteType));
            Assert(emotes.Length >= 16, $"En az 16 adet emote bulunmalı, bulunan: {emotes.Length}");

            // 4. Kasap ve Layer 10 (ButcherSecret) Kontrolü
            ButcherPlaceholder butcher = Object.FindFirstObjectByType<ButcherPlaceholder>();
            Assert(butcher != null, "Sahnede ButcherPlaceholder bulunmalı!");
            Assert(butcher.gameObject.layer == 10, $"ButcherPlaceholder layer=10 (ButcherSecret) olmalı, bulunan: {butcher.gameObject.layer}");

            // 5. Yansıma ve Karanlık Sistemi
            var reflSys = Object.FindFirstObjectByType<FingerMonster.Gameplay.ExecutionReflectionSystem>();
            Assert(reflSys != null, "Sahnede ExecutionReflectionSystem bulunmalı!");

            // 6. Emote Wheel UI & MainMenu Karakter Seçimi
            var emoteWheel = Object.FindFirstObjectByType<FingerMonster.UI.EmoteWheelUI>(FindObjectsInactive.Include);
            Assert(emoteWheel != null, "Sahnede EmoteWheelUI bulunmalı!");

            var menuScene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity", OpenSceneMode.Single);
            Assert(menuScene.IsValid(), "MainMenu.unity geçerli değil!");
            var menuCtrl = Object.FindFirstObjectByType<FingerMonster.UI.MainMenuController>();
            Assert(menuCtrl != null, "MainMenu sahnesinde MainMenuController bulunmalı!");

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Single);
            Debug.Log("<color=green>=== [AŞAMA 3: 3D BODYGUARD KARAKTERLER, URP, RIG VE KASAP TESTLERİ GEÇTİ (100% OK)] ===</color>");
        }

        [MenuItem("FingerMonster/Run Stage 4 Bodyguard Functional Verification")]
        public static void RunStage4BodyguardVerification()
        {
            Debug.Log("<color=cyan>=== [AŞAMA 4: EL KOPARMA, BOT KİŞİLİK, AĞ SLOT VE KOPYA GÜVENLİĞİ TESTİ BAŞLADI] ===</color>");

            // 1. Orijinal Assets/_Archive/BodyGuards_Original bozulmamış olmalı
            Material origMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Archive/BodyGuards_Original/Meshes/Materials/Boduguard_01_D.mat");
            Assert(origMat != null && origMat.shader.name == "Standard (Specular setup)",
                "Orijinal Assets/_Archive/BodyGuards_Original materyali değiştirilmemiş olmalı!");

            // 2. Her 4 karakter prefab'ında el koparma + kemik scale=0 + kopan el prefab spawn testi
            CharacterArchetype[] arches = {
                CharacterArchetype.Kabadayi,
                CharacterArchetype.Korkak,
                CharacterArchetype.Sinsi,
                CharacterArchetype.Sovmen
            };

            foreach (var arch in arches)
            {
                var def = CharacterDefinition.GetDefinition(arch);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.PrefabAssetPath);
                Assert(prefab != null, $"Prefab yüklenemedi: {def.PrefabAssetPath}");

                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                try
                {
                    var sever = inst.GetComponent<HandSeverSystem>();
                    Assert(sever != null, $"{def.DisplayName}: HandSeverSystem eksik!");
                    sever.ResolveBones();
                    Assert(sever.RemainingHandsCount == 2, $"{def.DisplayName}: Başlangıçta 2 el sağlam olmalı.");

                    sever.SeverHand(PlayerHand.Left, def.PrimaryColor);
                    Assert(sever.IsLeftHandSevered && sever.RemainingHandsCount == 1, $"{def.DisplayName}: Sol el koptuktan sonra 1 el kalmalı.");
                    Assert(sever.LeftHandBone.localScale.sqrMagnitude < 0.00001f, $"{def.DisplayName}: Sol el kemiği scale ~0.0001f olmalı!");

                    sever.SeverHand(PlayerHand.Right, def.PrimaryColor);
                    Assert(sever.IsRightHandSevered && sever.RemainingHandsCount == 0, $"{def.DisplayName}: Sağ el koptuktan sonra 0 el kalmalı.");
                    Assert(sever.RightHandBone.localScale.sqrMagnitude < 0.00001f, $"{def.DisplayName}: Sağ el kemiği scale ~0.0001f olmalı!");

                    sever.ResetHands();
                    Assert(sever.RemainingHandsCount == 2, $"{def.DisplayName}: ResetHands sonrası 2 el geri gelmeli.");
                    Assert(sever.LeftHandBone.localScale == Vector3.one && sever.RightHandBone.localScale == Vector3.one,
                        $"{def.DisplayName}: ResetHands sonrası el kemikleri Vector3.one olmalı!");
                }
                finally
                {
                    Object.DestroyImmediate(inst);
                }

                // 3. Bot kişilik eşleşmesi
                var botProfile = FingerMonster.AI.BotProfile.ForCharacterSlot((int)arch, arch);
                Assert(botProfile.BotName.Contains(def.DisplayName), $"Bot adı karakter adıyla ({def.DisplayName}) eşleşmeli: {botProfile.BotName}");

                // 4. CharacterEmoteData & 5 slot kontrolü
                string emoteDataPath = $"Assets/_Project/ScriptableObjects/Emotes/EmoteData_{def.DisplayName}.asset";
                var emoteData = AssetDatabase.LoadAssetAtPath<FingerMonster.Player.CharacterEmoteData>(emoteDataPath);
                Assert(emoteData != null, $"CharacterEmoteData bulunamadı: {emoteDataPath}");
                Assert(emoteData.Slots != null && emoteData.Slots.Length == 5, $"{def.DisplayName} EmoteData 5 slot içermeli.");
                for (int s = 0; s < 5; s++)
                {
                    Assert(!string.IsNullOrEmpty(emoteData.Slots[s].DisplayName), $"{def.DisplayName} Slot {s + 1} DisplayName boş olmamalı.");
                }
            }

            // 5. UpperBodyEmoteMask ve BodyguardAnimator EmoteLayer kontrolü
            var upperMask = AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/_Project/Animation/Avatars-Masks/UpperBodyEmoteMask.mask");
            Assert(upperMask != null, "UpperBodyEmoteMask.mask bulunamadı.");

            var baseCtrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/_Project/Animation/Controllers/BodyguardAnimator.controller");
            Assert(baseCtrl != null, "BodyguardAnimator.controller bulunamadı.");
            Assert(baseCtrl.layers.Length >= 2 && baseCtrl.layers[1].name == "EmoteLayer", "BodyguardAnimator Layer 1 'EmoteLayer' olmalı.");
            bool hasEmoteSlotParam = false;
            foreach (var p in baseCtrl.parameters)
            {
                if (p.name == "EmoteSlot") { hasEmoteSlotParam = true; break; }
            }
            Assert(hasEmoteSlotParam, "BodyguardAnimator içinde 'EmoteSlot' parametresi bulunmalı.");

            // 6. Kullanıcının eklediği 9 Mixamo FBX'in Humanoid + Bake Into Pose kontrolü
            string[] expectedMixamoFbxs = new[]
            {
                "Sitting.fbx", "Sitting Clap.fbx", "Sitting Disapproval.fbx", "Sitting Laughing.fbx",
                "Pointing.fbx", "Crying.fbx", "Taunt Gesture.fbx", "Crazy Gesture.fbx", "Looking.fbx"
            };
            foreach (var fbx in expectedMixamoFbxs)
            {
                string fbxPath = $"Assets/_Project/Animation/Clips/Other/{fbx}";
                var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                Assert(importer != null, $"Mixamo FBX importer bulunamadı: {fbxPath}");
                Assert(importer.animationType == ModelImporterAnimationType.Human, $"{fbx} Humanoid rig olmalı.");
                if (importer.clipAnimations != null && importer.clipAnimations.Length > 0)
                {
                    var clip = importer.clipAnimations[0];
                    Assert(clip.lockRootRotation && clip.lockRootHeightY && clip.lockRootPositionXZ,
                        $"{fbx} için Bake Into Pose (Root Rotation/Y/XZ) açık olmalı.");
                }
            }

            // 6b. Feg Override Controller'da Emote1_Base'in gerçekten Mixamo klibiyle override edildiğini doğrula
            var fegAoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>("Assets/_Project/Animation/Overrides/AOC_Feg.overrideController");
            Assert(fegAoc != null, "AOC_Feg.overrideController bulunamadı.");
            var ovs = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
            fegAoc.GetOverrides(ovs);
            bool emote1OverriddenWithMixamo = false;
            foreach (var kv in ovs)
            {
                if (kv.Key != null && kv.Key.name == "Emote1_Base" && kv.Value != null && kv.Value != kv.Key)
                {
                    emote1OverriddenWithMixamo = true;
                    break;
                }
            }
            Assert(emote1OverriddenWithMixamo, "Feg Emote1_Base slotu Mixamo klibi (Sitting Laughing) ile override edilmiş olmalı.");

            // 7. Mixamo_Import_Report.txt ve README_Mixamo.txt varlık kontrolü
            Assert(System.IO.File.Exists("Assets/_Project/Animation/Clips/Mixamo/Mixamo_Import_Report.txt"), "Mixamo_Import_Report.txt oluşturulmuş olmalı.");
            Assert(System.IO.File.Exists("Assets/_Project/Animation/Clips/Mixamo/README_Mixamo.txt"), "README_Mixamo.txt güncellenmiş olmalı.");

            // 8. Game.unity içinde EmoteWheelUI ve EmoteGuidePanelUI kontrolü
            var gameScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            Assert(gameScene.IsValid(), "Game.unity sahnesi açılabilmeli.");
            var emoteWheel = UnityEngine.Object.FindFirstObjectByType<FingerMonster.UI.EmoteWheelUI>(UnityEngine.FindObjectsInactive.Include);
            Assert(emoteWheel != null, "Game.unity içinde EmoteWheelUI bulunmalı.");
            var guidePanel = UnityEngine.Object.FindFirstObjectByType<FingerMonster.UI.EmoteGuidePanelUI>(UnityEngine.FindObjectsInactive.Include);
            Assert(guidePanel != null, "Game.unity içinde EmoteGuidePanelUI bulunmalı.");
            guidePanel.PopulateGuide();

            // 9. Aşama 5: Bodrum / Tecrit Odası Çevre & Atmosfer Kontrolü
            var flickeringBulb = UnityEngine.Object.FindFirstObjectByType<FingerMonster.Gameplay.FlickeringBulb>(UnityEngine.FindObjectsInactive.Include);
            Assert(flickeringBulb != null, "Game.unity içinde FlickeringBulb (arka planda sallanan yanıp sönen ampul) bulunmalı.");

            var roomLighting = UnityEngine.Object.FindFirstObjectByType<FingerMonster.Gameplay.RoomLightingController>(UnityEngine.FindObjectsInactive.Include);
            Assert(roomLighting != null, "Game.unity içinde RoomLightingController bulunmalı.");

            var northWall = GameObject.Find("Wall_North");
            var southWall = GameObject.Find("Wall_South");
            var westWall = GameObject.Find("Wall_West");
            var eastWall = GameObject.Find("Wall_East");
            var ceiling = GameObject.Find("Ceiling");
            Assert(northWall != null && southWall != null && westWall != null && eastWall != null && ceiling != null, "4 duvar ve tavan modüler oda mimarisi sahnede yer almalı.");

            var poster1 = GameObject.Find("Poster_WatchFingers");
            var poster2 = GameObject.Find("Poster_ButcherWatching");
            Assert(poster1 != null && poster2 != null, "Komik/gerilimli posterler sahnede bulunmalı.");

            // Havada kare/kabarcık partikülü kalmadığını doğrula
            var dustPs = GameObject.Find("AmbientDustParticles");
            Assert(dustPs == null, "Uçuşan kare partiküller (AmbientDustParticles) kaldırılmış olmalı.");

            // 10. Blacksmith's Forge (3DForge) Sahne Bütünlüğü Kontrolü (En fazla 5 prop, masa çevresi temiz)
            var forgeProps = GameObject.Find("BlacksmithForge_Props");
            Assert(forgeProps != null, "Game.unity içinde BlacksmithForge_Props kök objesi bulunmalı.");
            Assert(forgeProps.transform.childCount >= 4 && forgeProps.transform.childCount <= 6, "Blacksmith's Forge paketinden yalnızca 4-5 adet sade dekorasyon sahnede yer almalı.");

            Debug.Log("<color=green>=== [AŞAMA 4: EL KOPARMA, EMOTE SİSTEMİ, BOT KİŞİLİK VE VARLIK BÜTÜNLÜĞÜ TESTLERİ GEÇTİ (100% OK)] ===</color>");
        }

        [MenuItem("FingerMonster/Run Emote Target Cycling Test")]
        public static void RunEmoteTargetCyclingTest()
        {
            Debug.Log("<color=cyan>=== [EMOTE TARGET CYCLING TEST BAŞLADI] ===</color>");

            var gameScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            Assert(gameScene.IsValid(), "Game.unity sahnesi açılabilmeli.");

            GameManager gm = Object.FindFirstObjectByType<GameManager>();
            Assert(gm != null, "GameManager sahnede bulunmalı.");

            EmoteWheelUI emoteUI = Object.FindFirstObjectByType<EmoteWheelUI>();
            Assert(emoteUI != null, "EmoteWheelUI sahnede bulunmalı.");

            // 1. Oyunu başlat
            gm.InitializeGame();
            Assert(gm.Players != null && gm.Players.Count == 4, "4 oyuncu tanımlanmış olmalı.");

            // Yerel koltuk 0 (Feg) olmalı
            var rivalsT1 = emoteUI.GetLivingRivalSeats();
            Assert(rivalsT1.Count == 3, $"Koltuk 0 için 3 yaşayan rakip olmalı, bulunan: {rivalsT1.Count}");
            Assert(rivalsT1.Contains(1) && rivalsT1.Contains(2) && rivalsT1.Contains(3), "Rakipler 1, 2 ve 3 olmalı.");

            // 2. Tur 1 (Sıra 0 - Feg): Q/E ile hedef değiştirme
            int startT = emoteUI.SelectedTargetSeatIndex;
            Assert(startT == 1, $"Başlangıç hedefi 1 olmalı, mevcut: {startT}");

            emoteUI.CycleTarget(1);
            Assert(emoteUI.SelectedTargetSeatIndex == 2, $"CycleTarget(1) sonrası hedef 2 olmalı, mevcut: {emoteUI.SelectedTargetSeatIndex}");

            emoteUI.CycleTarget(1);
            Assert(emoteUI.SelectedTargetSeatIndex == 3, $"CycleTarget(1) sonrası hedef 3 olmalı, mevcut: {emoteUI.SelectedTargetSeatIndex}");

            emoteUI.CycleTarget(1);
            Assert(emoteUI.SelectedTargetSeatIndex == 1, $"CycleTarget(1) başa dönmeli (1), mevcut: {emoteUI.SelectedTargetSeatIndex}");

            emoteUI.CycleTarget(-1);
            Assert(emoteUI.SelectedTargetSeatIndex == 3, $"CycleTarget(-1) geri gitmeli (3), mevcut: {emoteUI.SelectedTargetSeatIndex}");

            // 3. Tur 2'ye geçiş (Sıra 1 - Kenan Bot): Sıra değiştiğinde yerel oyuncunun hedef seçimi bozulmamalı!
            var beginTurnMethod = typeof(GameManager).GetMethod("BeginPlayerTurn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert(beginTurnMethod != null, "BeginPlayerTurn metodu bulunmalı.");
            beginTurnMethod.Invoke(gm, new object[] { 1 });

            Assert(gm.ActivePlayerIndex == 1, "Sıra Kenan'a (1) geçmiş olmalı.");
            // Yerel emote UI hala koltuk 0 için çalışmalı ve hedefleri [1, 2, 3] olmalı
            var rivalsT2 = emoteUI.GetLivingRivalSeats();
            Assert(rivalsT2.Count == 3, $"Sıra 1'deyken de yaşayan rakip sayısı 3 olmalı, bulunan: {rivalsT2.Count}");

            emoteUI.CycleTarget(1);
            Assert(emoteUI.SelectedTargetSeatIndex == 1, $"Tur 2'de (sıra botta iken) hedef döngüsü çalışmalı (1 bekleniyor), mevcut: {emoteUI.SelectedTargetSeatIndex}");

            emoteUI.CycleTarget(1);
            Assert(emoteUI.SelectedTargetSeatIndex == 2, $"Tur 2'de hedef döngüsü çalışmalı (2 bekleniyor), mevcut: {emoteUI.SelectedTargetSeatIndex}");

            // 4. Tur 3'e geçiş (Sıra 2 - Feyzi Bot)
            beginTurnMethod.Invoke(gm, new object[] { 2 });
            Assert(gm.ActivePlayerIndex == 2, "Sıra Feyzi'ye (2) geçmiş olmalı.");
            emoteUI.CycleTarget(1);
            Assert(emoteUI.SelectedTargetSeatIndex == 3, $"Tur 3'te hedef döngüsü çalışmalı (3 bekleniyor), mevcut: {emoteUI.SelectedTargetSeatIndex}");

            // 5. Tur 4'e geçiş (Sıra 3 - MMG Bot)
            beginTurnMethod.Invoke(gm, new object[] { 3 });
            Assert(gm.ActivePlayerIndex == 3, "Sıra MMG'ye (3) geçmiş olmalı.");
            emoteUI.CycleTarget(1);
            Assert(emoteUI.SelectedTargetSeatIndex == 1, $"Tur 4'te hedef döngüsü çalışmalı (1 bekleniyor), mevcut: {emoteUI.SelectedTargetSeatIndex}");

            // 6. 2. Round Başlangıcı (Round 2 StartRound)
            var roundProp = typeof(GameManager).GetProperty("CurrentRound");
            roundProp?.SetValue(gm, 2);
            gm.StartRound();
            Assert(gm.CurrentRound == 2, "CurrentRound 2 olmalı.");

            // 2. Round'da da hedef değiştirme kusursuz çalışmalı
            emoteUI.CycleTarget(1);
            Assert(emoteUI.SelectedTargetSeatIndex == 2, $"2. Round'da hedef döngüsü çalışmalı (2 bekleniyor), mevcut: {emoteUI.SelectedTargetSeatIndex}");

            // 7. Elenen oyuncu testi: Oyuncu 2'yi eleyelim
            gm.Players[2].LeftHand = HandState.Lost;
            gm.Players[2].RightHand = HandState.Lost;
            Assert(!gm.Players[2].IsAlive, "Oyuncu 2 elenmiş olmalı.");

            // Oyuncu 2 elendiğinde rakip listesinden çıkmalı
            var rivalsAfterElim = emoteUI.GetLivingRivalSeats();
            Assert(rivalsAfterElim.Count == 2, $"Oyuncu 2 elendikten sonra 2 rakip kalmalı, bulunan: {rivalsAfterElim.Count}");
            Assert(!rivalsAfterElim.Contains(2), "Elenen oyuncu 2 rakip listesinde bulunmamalı.");

            // Hedef şu an 2 idi, Update / EnsureValidTargetSelected otomatik olarak hayatta olan hedefe geçmeli
            var updateMethod = typeof(EmoteWheelUI).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            updateMethod?.Invoke(emoteUI, null);

            Assert(emoteUI.SelectedTargetSeatIndex != 2, $"Elenen oyuncu 2 hedef olarak kalamaz! Mevcut: {emoteUI.SelectedTargetSeatIndex}");
            Assert(rivalsAfterElim.Contains(emoteUI.SelectedTargetSeatIndex), "Yeni hedef hayattaki rakiplerden biri olmalı.");

            // Kalan rakipler arasında döngü sadece yaşayanlar (1 ve 3) arasında dönmeli
            emoteUI.CycleTarget(1);
            int nextT = emoteUI.SelectedTargetSeatIndex;
            Assert(nextT == 1 || nextT == 3, $"Elenme sonrası hedef döngüsü yalnızca yaşayanlar arasında dönmeli (1 veya 3), mevcut: {nextT}");

            Debug.Log("<color=green>=== [EMOTE TARGET CYCLING TESTLERİ BAŞARIYLA GEÇTİ (100% OK)] ===</color>");
        }

        [MenuItem("FingerMonster/Run 3x Replay And Avatar Persistence Test")]
        public static void RunReplayAndPersistenceTest()
        {
            Debug.Log("<color=cyan>=== [3X YENİDEN OYNATMA VE AVATAR OLUŞUMU TESTİ BAŞLADI] ===</color>");

            for (int replay = 1; replay <= 3; replay++)
            {
                Debug.Log($"<color=yellow>--- [Oynatma Döngüsü {replay}/3] ---</color>");
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
                Assert(scene.IsValid(), $"Döngü {replay}: Game.unity açılabilmeli.");

                // NetworkManager durumu sıfır olmalı
                if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
                {
                    Unity.Netcode.NetworkManager.Singleton.Shutdown();
                }

                GameManager gm = Object.FindFirstObjectByType<GameManager>();
                Assert(gm != null, $"Döngü {replay}: GameManager sahnede bulunmalı.");

                gm.InitializeGame();
                Assert(gm.Players != null && gm.Players.Count == 4, $"Döngü {replay}: 4 oyuncu verisi olmalı.");

                // 4 koltuğu ve avatarlarını kontrol et
                var seats = Object.FindObjectsByType<FingerMonster.Player.PlayerSeat>(FindObjectsSortMode.None);
                Assert(seats != null && seats.Length == 4, $"Döngü {replay}: 4 koltuk sahnede bulunmalı, bulunan: {(seats != null ? seats.Length : 0)}");

                int validAvatars = 0;
                foreach (var s in seats)
                {
                    Assert(s.Avatar != null, $"Döngü {replay}: Koltuk {s.PlayerIndex} avatarı null olamaz!");
                    Assert(s.Avatar.gameObject.activeInHierarchy, $"Döngü {replay}: Koltuk {s.PlayerIndex} avatarı sahnede aktif olmalı!");
                    Assert(s.Avatar.transform.localScale.sqrMagnitude > 0.01f, $"Döngü {replay}: Koltuk {s.PlayerIndex} avatar ölçeği sıfır olamaz!");
                    validAvatars++;
                }
                Assert(validAvatars == 4, $"Döngü {replay}: Tüm 4 koltukta aktif ve görünür avatar olmalı.");

                // Sahnede NaN / Infinity Transform veya Dejenere Rotation kontrolü
                var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
                foreach (var t in allTransforms)
                {
                    if (t == null) continue;
                    Vector3 pos = t.position;
                    Quaternion rot = t.rotation;
                    Vector3 sca = t.localScale;

                    Assert(float.IsFinite(pos.x) && float.IsFinite(pos.y) && float.IsFinite(pos.z), $"Döngü {replay}: {t.name} pozisyonu NaN/Inf olamaz! ({pos})");
                    Assert(float.IsFinite(rot.x) && float.IsFinite(rot.y) && float.IsFinite(rot.z) && float.IsFinite(rot.w), $"Döngü {replay}: {t.name} rotasyonu NaN/Inf olamaz! ({rot})");
                    Assert(rot.x != 0 || rot.y != 0 || rot.z != 0 || rot.w != 0, $"Döngü {replay}: {t.name} rotasyon kuaterniyonu 0,0,0,0 olamaz!");
                    Assert(float.IsFinite(sca.x) && float.IsFinite(sca.y) && float.IsFinite(sca.z), $"Döngü {replay}: {t.name} ölçeği NaN/Inf olamaz! ({sca})");
                }

                // Parçacık sistemlerinin renderMode kontrolü (Stretched Billboard sıfır hızda NaN üretir)
                // Kullanıcı kuralı: Oyunda hiçbir parçacık sistemi (ParticleSystem) bulunmamalı
                var particleSystems = Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsSortMode.None);
                foreach (var psr in particleSystems)
                {
                    if (psr == null) continue;
                    Assert(psr.renderMode != ParticleSystemRenderMode.Stretch, $"Döngü {replay}: {psr.name} parçacık renderMode Stretched Billboard olmamalı!");
                }
                Assert(particleSystems.Length == 0, $"Döngü {replay}: Oyunda hiçbir partikül bulunmamalı! Bulunan: {particleSystems.Length}");

                // HUD kontrolü: 4 oyuncunun isimleri, kişilikleri ve prompt metni
                HUDController hud = Object.FindFirstObjectByType<HUDController>();
                Assert(hud != null, $"Döngü {replay}: HUDController sahnede bulunmalı.");
                var promptObj = GameObject.Find("PromptText");
                if (promptObj != null)
                {
                    var tmp = promptObj.GetComponent<TMPro.TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        Assert(!tmp.text.Equals("Sıra: Oyuncu 1"), $"Döngü {replay}: PromptText varsayılan 'Sıra: Oyuncu 1' olarak kalamaz! Mevcut: {tmp.text}");
                    }
                }

                // Tur ilerletme ve oyun sonlandırma simülasyonu
                gm.EndGame();
                Assert(gm.CurrentState == GameState.GameOver, $"Döngü {replay}: Oyun GameOver durumuna geçmeli.");
            }

            Debug.Log("<color=green>=== [3X YENİDEN OYNATMA VE AVATAR PERSISTENCE TESTİ BAŞARIYLA TAMAMLANDI (100% OK)] ===</color>");
        }

        [MenuItem("FingerMonster/Run Double Setup And HUD Verification")]
        public static void RunDoubleSetupAndHUDVerification()
        {
            Debug.Log("<color=cyan>=== [DOUBLE SETUP VE HUD ÇAKIŞMA TESTİ BAŞLADI] ===</color>");

            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Single);
            Assert(scene.IsValid(), "Game.unity açılamadı!");

            // 1. Koltuk ve Karakter Sayısı Kontrolü
            var seats = Object.FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None);
            Assert(seats != null && seats.Length == 4, $"Tam 4 koltuk olmalı! Bulunan: {(seats != null ? seats.Length : 0)}");

            var allAvatars = Object.FindObjectsByType<PlayerVisualAvatar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert(allAvatars.Length == 4, $"Sahnede tam 4 avatar olmalı! Bulunan: {allAvatars.Length}");

            var allAnimators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert(allAnimators.Length == 4, $"Sahnede tam 4 Animator olmalı! Bulunan: {allAnimators.Length}");

            foreach (var s in seats)
            {
                Assert(s.Avatar != null, $"Koltuk {s.PlayerIndex} avatar referansı dolu olmalı!");
                var smrs = s.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert(smrs.Length > 0, $"Koltuk {s.PlayerIndex} üzerinde karakter SkinnedMeshRenderer bulunmalı!");
            }

            // 2. AssignCharacterPrefab Idempotency Testi (Arka arkaya çağrıldığında çift karakter üretmemeli)
            GameManager gm = Object.FindFirstObjectByType<GameManager>();
            Assert(gm != null, "GameManager bulunamadı!");
            gm.EnsureReferences();
            var charDef = CharacterDefinition.GetDefinition(CharacterArchetype.Kabadayi);
            GameObject fegPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(charDef.PrefabAssetPath);
            Assert(fegPrefab != null, "Feg prefab'ı bulunamadı!");

            // 3 kez çağır
            seats[0].AssignCharacterPrefab(fegPrefab, charDef.PrimaryColor);
            seats[0].AssignCharacterPrefab(fegPrefab, charDef.PrimaryColor);
            seats[0].AssignCharacterPrefab(fegPrefab, charDef.PrimaryColor);

            var avatarsAfter = Object.FindObjectsByType<PlayerVisualAvatar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert(avatarsAfter.Length == 4, $"AssignCharacterPrefab çağrılarından sonra sahnede tam 4 avatar kalmalı! Bulunan: {avatarsAfter.Length}");

            // 3. HUD Format ve Çakışma Kontrolü
            HUDController hud = Object.FindFirstObjectByType<HUDController>();
            Assert(hud != null, "HUDController bulunamadı!");
            hud.UpdateRoundInfo(1, 14);

            var safeKeysObj = GameObject.Find("SafeKeysText");
            Assert(safeKeysObj != null, "SafeKeysText bulunamadı!");
            var safeKeysTmp = safeKeysObj.GetComponent<TMPro.TextMeshProUGUI>();
            Assert(safeKeysTmp != null, "SafeKeysText TextMeshProUGUI bulunamadı!");
            Assert(safeKeysTmp.text == "Kalan: 14", $"SafeKeysText 'Kalan: 14' formatında olmalı! Mevcut: '{safeKeysTmp.text}'");

            var promptObj = GameObject.Find("PromptText");
            Assert(promptObj != null, "PromptText bulunamadı!");
            var promptTmp = promptObj.GetComponent<TMPro.TextMeshProUGUI>();
            Assert(promptTmp != null, "PromptText TextMeshProUGUI bulunamadı!");

            // 4. Emote Çubuğu Taşma ve Örtüşme Kontrolü
            EmoteWheelUI emoteWheel = Object.FindFirstObjectByType<EmoteWheelUI>();
            Assert(emoteWheel != null, "EmoteWheelUI sahnede bulunamadı!");
            emoteWheel.ConfigureForArchetype(CharacterArchetype.Kabadayi);

            var emoteBtns = GameObject.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None);
            int verifiedBarSlots = 0;
            foreach (var btn in emoteBtns)
            {
                if (btn.name.StartsWith("EmoteSlotBtn_"))
                {
                    var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        Assert(tmp.textWrappingMode == TMPro.TextWrappingModes.NoWrap, $"{btn.name} butonunda satır sarma (wrap) kapalı olmalı!");
                        Assert(tmp.overflowMode == TMPro.TextOverflowModes.Ellipsis, $"{btn.name} butonunda overflow Ellipsis olmalı!");
                        verifiedBarSlots++;
                    }
                }
            }
            Assert(verifiedBarSlots == 5, $"Tam 5 emote bar butonu doğrulanmalı, doğrulanan: {verifiedBarSlots}");

            Debug.Log("<color=green>=== [DOUBLE SETUP VE HUD ÇAKIŞMA TESTİ BAŞARIYLA GEÇTİ (100% OK)] ===</color>");
        }

        [MenuItem("FingerMonster/Run Ability Cards Verification")]
        public static void RunAbilityCardsVerification()
        {
            Debug.Log("<color=cyan>=== [ABILITY CARDS (6 YETENEK KARTI) DOĞRULAMA TESTİ BAŞLADI] ===</color>");

            SceneSetupTool.BuildAllScenes();
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Single);
            Assert(scene.IsValid(), "Game.unity sahnesi geçerli değil!");

            GameManager gm = Object.FindFirstObjectByType<GameManager>();
            Assert(gm != null, "GameManager sahnede bulunamadı!");
            gm.EnsureSettings();

            TypewriterMachine tw = gm.Typewriter != null ? gm.Typewriter : Object.FindFirstObjectByType<TypewriterMachine>();
            Assert(tw != null, "TypewriterMachine sahnede bulunamadı!");

            TurnController turnController = gm.GetComponent<TurnController>() ?? Object.FindFirstObjectByType<TurnController>();
            Assert(turnController != null, "TurnController sahnede bulunamadı!");

            CardSelectionController cardCtrl = gm.CardController != null
                ? gm.CardController
                : Object.FindFirstObjectByType<CardSelectionController>(FindObjectsInactive.Include);
            Assert(cardCtrl != null, "CardSelectionController sahnede bulunamadı!");

            // a) 6 Kart Tanımı ve GameSettings Ayar Doğrulaması
            var allDefs = CardData.GetAllDefinitions();
            Assert(allDefs != null && allDefs.Count == 6, $"CardData.GetAllDefinitions().Count == 6 olmalı, bulunan: {(allDefs != null ? allDefs.Count : 0)}");

            AbilityCardType[] expectedTypes = new[]
            {
                AbilityCardType.Shield,
                AbilityCardType.Possess,
                AbilityCardType.XRay,
                AbilityCardType.SkipTurn,
                AbilityCardType.ReverseDirection,
                AbilityCardType.Fog
            };
            foreach (var cardType in expectedTypes)
            {
                CardData def = CardData.GetDefinition(cardType);
                Assert(def != null && def.cardType == cardType, $"CardData.GetDefinition({cardType}) geçerli tanım döndürmeli!");
            }

            GameSettings settings = gm.Settings;
            Assert(settings != null, "GameManager.Settings null olmamalı!");
            Assert(settings.maxAbilityCardsInHand == 2, $"maxAbilityCardsInHand == 2 olmalı, mevcut: {settings.maxAbilityCardsInHand}");
            Assert(settings.cardsDrawnPerRoundStart == 1, $"cardsDrawnPerRoundStart == 1 olmalı, mevcut: {settings.cardsDrawnPerRoundStart}");
            Assert(settings.cardsDrawnOnSafeCardPass == 1, $"cardsDrawnOnSafeCardPass == 1 olmalı, mevcut: {settings.cardsDrawnOnSafeCardPass}");
            Assert(settings.keepAbilityCardsBetweenRounds == true, "keepAbilityCardsBetweenRounds == true olmalı!");
            Assert(settings.restoreHandsOnNewRound == false, "restoreHandsOnNewRound == false olmalı!");

            // b) El Sınırı (Max 2 Kart) Testi
            gm.InitializeGame();
            for (int i = 0; i < 4; i++)
            {
                Assert(gm.Players[i].AbilityCards.Count == 1,
                    $"InitializeGame sonrası Oyuncu {i} elinde 1 başlangıç kartı olmalı, bulunan: {gm.Players[i].AbilityCards.Count}");
            }

            // Güvenli kart pas geçince (HideImmediate ile senkron HandleCardSelectionResult tetiklenir) -> 2 kart
            cardCtrl.HideImmediate();
            int safeCardIdx = cardCtrl.ActiveSafeCardIndex;
            Assert(safeCardIdx >= 0 && safeCardIdx < 3, $"ActiveSafeCardIndex 0..2 aralığında olmalı, mevcut: {safeCardIdx}");
            cardCtrl.SelectCard(safeCardIdx);
            Assert(gm.Players[0].AbilityCards.Count == 2,
                $"Güvenli kart pas geçince Oyuncu 0 eli 2 karta çıkmalı, mevcut: {gm.Players[0].AbilityCards.Count}");

            // 3. kart çekilmeye çalışıldığında el sınırı 2'de kalmalı
            bool drewThird = gm.DrawAbilityCardsForPlayer(0, 1);
            Assert(!drewThird, "El 2 kartla doluyken 3. kart çekilememeli (false dönmeli)!");
            Assert(gm.Players[0].AbilityCards.Count == 2,
                $"3. kart denemesinden sonra el sınırı 2'de kalmalı, mevcut: {gm.Players[0].AbilityCards.Count}");

            // Yardımcı: bir güvenli ve bir tuzak tuş bul
            int safeKey = -1;
            int trapKey = -1;
            int trapKey2 = -1;
            for (int k = 0; k < 20; k++)
            {
                if (!gm.TrapIndices.Contains(k) && safeKey < 0) safeKey = k;
                else if (gm.TrapIndices.Contains(k))
                {
                    if (trapKey < 0) trapKey = k;
                    else if (trapKey2 < 0) trapKey2 = k;
                }
            }
            Assert(safeKey >= 0 && trapKey >= 0 && trapKey2 >= 0, "Test için yeterli güvenli/tuzak tuş bulunamadı!");

            // c) Her 6 Kartın Tek Tek Testi
            // c.1) Kalkan (Shield) — Aktif edilerek kullanım
            gm.StopAllCoroutines();
            turnController.ResetTurn(0);
            gm.ForceSetState(GameState.PlayerTurn);
            gm.Players[0].BeginTurnAbilityReset();
            gm.Players[0].ClearAbilityCardsAndEffects();
            gm.Players[0].TryAddAbilityCard(AbilityCardType.Shield, settings.maxAbilityCardsInHand);
            Assert(gm.TryPlayAbilityCard(0, 0), "Kalkan kartı PlayerTurn aşamasında oynanabilmeli!");
            Assert(gm.Players[0].HasActiveShield, "Kalkan oynandıktan sonra HasActiveShield == true olmalı!");
            gm.ResolveKey(0, trapKey);
            Assert(gm.Players[0].RemainingHandsCount == 2, "Aktif Kalkan varken tuzak tuşta el kopmamalı (RemainingHandsCount == 2)!");
            Assert(!gm.Players[0].HasActiveShield, "Tuzak tuş engellendikten sonra HasActiveShield tüketilmeli (false)!");

            // c.1b) Kalkan (Shield) — Elde pasif tutulurken kullanım
            gm.StopAllCoroutines();
            turnController.ResetTurn(1);
            gm.ForceSetState(GameState.PlayerTurn);
            gm.Players[1].BeginTurnAbilityReset();
            gm.Players[1].ClearAbilityCardsAndEffects();
            gm.Players[1].TryAddAbilityCard(AbilityCardType.Shield, settings.maxAbilityCardsInHand);
            Assert(!gm.Players[1].HasActiveShield, "Pasif kalkan testinde başlangıçta HasActiveShield false olmalı!");
            gm.ResolveKey(1, trapKey2);
            Assert(gm.Players[1].RemainingHandsCount == 2, "Elde pasif Kalkan varken tuzak tuşta el kopmamalı!");
            Assert(!gm.Players[1].HasAbilityCard(AbilityCardType.Shield), "Pasif Kalkan kullanıldıktan sonra elden tüketilmeli!");

            // c.2) Ele Geçir (Possess)
            gm.StopAllCoroutines();
            turnController.ResetTurn(0);
            gm.Players[0].BeginTurnAbilityReset();
            gm.Players[0].ClearAbilityCardsAndEffects();
            gm.Players[0].TryAddAbilityCard(AbilityCardType.Possess, settings.maxAbilityCardsInHand);
            cardCtrl.ShowCardSelection(0, gm.Players[0].PlayerName, gm.Players[0].PlayerColor, true, null);
            gm.ForceSetState(GameState.CardSelection);
            Assert(gm.TryPlayAbilityCard(0, 0), "Ele Geçir kartı CardSelection aşamasında oynanabilmeli!");
            Assert(cardCtrl.HintedMonsterCardIndex >= 0 && cardCtrl.HintedMonsterCardIndex <= 2,
                $"HintedMonsterCardIndex 0..2 aralığında olmalı, mevcut: {cardCtrl.HintedMonsterCardIndex}");
            Assert(cardCtrl.HintedMonsterCardIndex != cardCtrl.ActiveSafeCardIndex,
                "HintedMonsterCardIndex güvenli kartı değil bir canavar kartını göstermeli!");

            // c.3) Röntgen (XRay) — Hem güvenli tuş hem tuzak tuş
            gm.StopAllCoroutines();
            turnController.ResetTurn(0);
            gm.ForceSetState(GameState.PlayerTurn);
            gm.Players[0].BeginTurnAbilityReset();
            gm.Players[0].ClearAbilityCardsAndEffects();
            gm.Players[0].TryAddAbilityCard(AbilityCardType.XRay, settings.maxAbilityCardsInHand);
            Assert(gm.TryPlayAbilityCard(0, 0, safeKey), "Röntgen güvenli tuş üzerinde oynanabilmeli!");
            TypewriterKey safeTwKey = tw.GetKey(safeKey);
            Assert(safeTwKey != null && safeTwKey.HasXRayReveal && !safeTwKey.XRayIsTrap,
                "Güvenli tuşta Röntgen sonrası HasXRayReveal == true ve XRayIsTrap == false olmalı!");

            gm.Players[0].BeginTurnAbilityReset();
            gm.Players[0].ClearAbilityCardsAndEffects();
            gm.Players[0].TryAddAbilityCard(AbilityCardType.XRay, settings.maxAbilityCardsInHand);
            Assert(gm.TryPlayAbilityCard(0, 0, trapKey), "Röntgen tuzak tuş üzerinde oynanabilmeli!");
            TypewriterKey trapTwKey = tw.GetKey(trapKey);
            Assert(trapTwKey != null && trapTwKey.HasXRayReveal && trapTwKey.XRayIsTrap,
                "Tuzak tuşta Röntgen sonrası HasXRayReveal == true ve XRayIsTrap == true olmalı!");

            // c.4) Sıra Atlat (SkipTurn)
            gm.StopAllCoroutines();
            turnController.ResetTurn(0);
            gm.ForceSetState(GameState.CardSelection);
            gm.Players[0].BeginTurnAbilityReset();
            gm.Players[0].ClearAbilityCardsAndEffects();
            gm.Players[0].TryAddAbilityCard(AbilityCardType.SkipTurn, settings.maxAbilityCardsInHand);
            Assert(gm.TryPlayAbilityCard(0, 0), "Sıra Atlat kartı oynanabilmeli!");
            Assert(gm.Players[0].UsedSkipLastTurn, "Sıra Atlat oynandıktan sonra UsedSkipLastTurn == true olmalı!");

            gm.StopAllCoroutines();
            turnController.ResetTurn(0);
            gm.ForceSetState(GameState.CardSelection);
            gm.Players[0].BeginTurnAbilityReset();
            bool canSkipTwice = gm.CanPlayAbilityCard(0, AbilityCardType.SkipTurn, out _);
            Assert(!canSkipTwice, "Aynı oyuncu bir sonraki sırasında üst üste 2. kez Sıra Atlat oynayamamalı!");

            // c.5) Ters Yön (ReverseDirection)
            gm.StopAllCoroutines();
            turnController.ResetTurn(0);
            gm.ForceSetState(GameState.CardSelection);
            gm.Players[0].BeginTurnAbilityReset();
            gm.Players[0].ClearAbilityCardsAndEffects();
            gm.Players[0].TryAddAbilityCard(AbilityCardType.ReverseDirection, settings.maxAbilityCardsInHand);
            Assert(gm.TryPlayAbilityCard(0, 0), "Ters Yön kartı CardSelection aşamasında oynanabilmeli!");
            Assert(turnController.IsReversed, "Ters Yön oynandıktan sonra turnController.IsReversed == true olmalı!");

            gm.StopAllCoroutines();
            turnController.ResetTurn(0);
            turnController.SetReversed(true);
            int rev1 = turnController.AdvanceToNextAlivePlayer(gm.Players);
            int rev2 = turnController.AdvanceToNextAlivePlayer(gm.Players);
            Assert(rev1 == 3 && rev2 == 2, $"Ters Yön ilerleyişi 0 -> 3 -> 2 olmalı, bulunan: 0 -> {rev1} -> {rev2}");

            gm.StartRound();
            Assert(!turnController.IsReversed, "Yeni turda (StartRound) turnController.IsReversed == false olarak sıfırlanmalı!");

            // c.6) Sis (Fog)
            gm.StopAllCoroutines();
            turnController.ResetTurn(0);
            gm.ForceSetState(GameState.CardSelection);
            gm.Players[0].BeginTurnAbilityReset();
            gm.Players[0].ClearAbilityCardsAndEffects();
            gm.Players[0].TryAddAbilityCard(AbilityCardType.Fog, settings.maxAbilityCardsInHand);
            Assert(gm.TryPlayAbilityCard(0, 0, 1), "Sis kartı hedef rakip (1) üzerinde oynanabilmeli!");
            Assert(gm.Players[1].IsFogged, "Sis hedefi olan Oyuncu 1 için IsFogged == true olmalı!");

            turnController.ResetTurn(1);
            gm.ForceSetState(GameState.CardSelection);
            gm.Players[1].BeginTurnAbilityReset();
            bool canPossessWhileFogged = gm.CanPlayAbilityCard(1, AbilityCardType.Possess, out _);
            Assert(!canPossessWhileFogged, "Sis altındaki oyuncu Ele Geçir (Possess) kartını oynayamamalı!");

            // d) 4 Turluk Tam Oyun Akışı Simülasyonu
            gm.StopAllCoroutines();
            gm.ResetTableForNewGame();
            gm.InitializeGame();
            // Oyuncu 2'nin 1 elini kopar; Round 1..4 boyunca geri gelmediğini ve kartların korunduğunu doğrula
            gm.Players[2].SeverRandomHand();
            Assert(gm.Players[2].RemainingHandsCount == 1, "1. el koptuktan sonra Oyuncu 2'nin 1 eli kalmalı!");

            for (int round = 2; round <= 4; round++)
            {
                gm.StopAllCoroutines();
                gm.StartRound();
                Assert(gm.CurrentRound == round, $"Round {round} bekleniyordu, mevcut: {gm.CurrentRound}");
                Assert(gm.Players[2].RemainingHandsCount == 1,
                    $"restoreHandsOnNewRound == false iken Round {round} başında kopan el geri gelmemeli!");
                Assert(gm.Players[0].AbilityCards.Count == 2,
                    $"keepAbilityCardsBetweenRounds == true iken Round {round} başında kartlar korunup 2'ye tamamlanmalı!");
            }

            // Oyuncu 2'nin 2. elini de kopar -> elenince kartları temizlenmeli
            gm.Players[2].SeverRandomHand();
            Assert(!gm.Players[2].IsAlive, "2. el koptuktan sonra Oyuncu 2 elenmiş olmalı!");
            Assert(gm.Players[2].AbilityCards.Count == 0,
                $"Elenen oyuncunun kartları temizlenmeli (AbilityCards.Count == 0), mevcut: {gm.Players[2].AbilityCards.Count}");

            // e) 3 Kez Peş Peşe "Yeniden Oyna" Testi
            for (int replay = 1; replay <= 3; replay++)
            {
                gm.StopAllCoroutines();
                gm.ResetTableForNewGame();
                gm.InitializeGame();
                Assert(!turnController.IsReversed, $"Yeniden Oyna #{replay}: IsReversed false olmalı!");
                for (int p = 0; p < 4; p++)
                {
                    var pl = gm.Players[p];
                    Assert(pl.IsAlive && pl.RemainingHandsCount == 2,
                        $"Yeniden Oyna #{replay}: Oyuncu {p} 2 sağlam elle başlamalı!");
                    Assert(pl.AbilityCards.Count == 1,
                        $"Yeniden Oyna #{replay}: Oyuncu {p} 1 başlangıç kartıyla başlamalı, mevcut: {pl.AbilityCards.Count}");
                    Assert(!pl.HasActiveShield && !pl.IsFogged && !pl.UsedSkipLastTurn,
                        $"Yeniden Oyna #{replay}: Oyuncu {p} durum etkileri sıfırlanmış olmalı!");
                }
            }

            // f) Solo + 3 Bot & UI & Netcode Yapısal Doğrulaması
            var aiBrain = Object.FindFirstObjectByType<FingerMonster.AI.AIBrain>();
            Assert(aiBrain != null, "Sahnede AIBrain bileşeni bulunamadı!");

            var botHand = new List<AbilityCardType> { AbilityCardType.SkipTurn, AbilityCardType.Possess };
            for (int trial = 0; trial < 20; trial++)
            {
                int fateSlot = aiBrain.DecideBotAbilityCardBeforeFate(botHand, FingerMonster.AI.BotDifficulty.Zor, 1, true, true, out AbilityCardType fateCard);
                Assert(fateSlot == -1 || (fateSlot >= 0 && fateSlot < botHand.Count),
                    $"DecideBotAbilityCardBeforeFate geçersiz slot döndürdü: {fateSlot}");
                Assert(fateCard != AbilityCardType.SkipTurn, "Bot UsedSkipLastTurn == true iken SkipTurn seçmemeli!");
                Assert(fateCard != AbilityCardType.Possess, "Bot IsFogged == true iken Possess seçmemeli!");

                int keySlot = aiBrain.DecideBotAbilityCardBeforeKey(new List<AbilityCardType> { AbilityCardType.XRay, AbilityCardType.Shield },
                    FingerMonster.AI.BotDifficulty.Zor, 1, true, out AbilityCardType keyCard);
                Assert(keySlot == -1 || (keySlot >= 0 && keySlot < 2),
                    $"DecideBotAbilityCardBeforeKey geçersiz slot döndürdü: {keySlot}");
                Assert(keyCard != AbilityCardType.SkipTurn, "Bot UsedSkipLastTurn == true iken SkipTurn seçmemeli!");
            }

            AbilityCardHUD abilityHud = Object.FindFirstObjectByType<AbilityCardHUD>(FindObjectsInactive.Include);
            Assert(abilityHud != null, "AbilityCardHUD sahnede bulunamadı!");
            abilityHud.EnsureInitialized();
            abilityHud.RefreshDisplay();

            PlayerCardUI[] playerCards = Object.FindObjectsByType<PlayerCardUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert(playerCards.Length == 4, $"Sahnede 4 adet PlayerCardUI bulunmalı, bulunan: {playerCards.Length}");
            playerCards[0].UpdateDisplay(gm.Players[0], true);

            var originalSlot = new FingerMonster.Networking.NetworkPlayerSlot(42UL, 1, "TestBot", true, Color.cyan, 2)
            {
                AbilityCardCount = 2,
                HasActiveShield = true,
                IsFogged = true
            };
            using (var writer = new Unity.Netcode.FastBufferWriter(256, Unity.Collections.Allocator.Temp))
            {
                writer.WriteNetworkSerializable(in originalSlot);
                using (var reader = new Unity.Netcode.FastBufferReader(writer, Unity.Collections.Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out FingerMonster.Networking.NetworkPlayerSlot deserializedSlot);
                    Assert(deserializedSlot.Equals(originalSlot), "NetworkPlayerSlot serileştirme/deserileştirme bütünlüğü bozuldu!");
                    Assert(deserializedSlot.AbilityCardCount == 2 && deserializedSlot.HasActiveShield && deserializedSlot.IsFogged,
                        "NetworkPlayerSlot yetenek kartı alanları (AbilityCardCount, HasActiveShield, IsFogged) korunmalı!");
                }
            }

            Debug.Log("<color=green>=== [ABILITY CARDS (6 YETENEK KARTI) DOĞRULAMA TESTİ BAŞARIYLA GEÇTİ (100% OK)] ===</color>");
        }

        [MenuItem("FingerMonster/Run Ghost Assistant Verification")]
        public static void RunGhostAssistantVerification()
        {
            Debug.Log("<color=cyan>=== [GHOST ASSISTANT (HAYALET - KASABIN YARDIMCISI) DOĞRULAMA TESTİ BAŞLADI] ===</color>");

            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Single);
            Assert(scene.IsValid(), "Game.unity sahnesi geçerli değil!");

            GameManager gm = Object.FindFirstObjectByType<GameManager>();
            Assert(gm != null, "GameManager sahnede bulunamadı!");
            Assert(gm.Settings != null, "GameSettings atanmamış!");

            TypewriterMachine tw = Object.FindFirstObjectByType<TypewriterMachine>();
            Assert(tw != null && tw.Keys.Count == 20, "TypewriterMachine ve 20 tuş bulunamadı!");

            PlayerSeat[] rawSeats = Object.FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None);
            Assert(rawSeats.Length == 4, $"Sahnede 4 koltuk bulunmalı, bulunan: {rawSeats.Length}");
            PlayerSeat[] seats = new PlayerSeat[4];
            for (int i = 0; i < rawSeats.Length; i++)
            {
                if (rawSeats[i].PlayerIndex >= 0 && rawSeats[i].PlayerIndex < 4)
                    seats[rawSeats[i].PlayerIndex] = rawSeats[i];
            }
            for (int i = 0; i < 4; i++)
            {
                Assert(seats[i] != null && seats[i].Avatar != null, $"Koltuk {i} veya Avatarı eksik!");
            }

            PlayerCameraController pcc = Object.FindFirstObjectByType<PlayerCameraController>();
            Assert(pcc != null, "PlayerCameraController sahnede bulunamadı!");

            ExecutionReflectionSystem reflSys = Object.FindFirstObjectByType<ExecutionReflectionSystem>();
            Assert(reflSys != null, "ExecutionReflectionSystem sahnede bulunamadı!");

            // 1. Elenme ve Hayalet Modu Geçişi
            gm.StopAllCoroutines();
            gm.ResetTableForNewGame();
            gm.InitializeGame();

            for (int i = 0; i < 4; i++)
            {
                Assert(gm.Players[i].IsAlive, $"Başlangıçta Oyuncu {i} IsAlive == true olmalı!");
                Assert(!gm.Players[i].IsGhost, $"Başlangıçta Oyuncu {i} IsGhost == false olmalı!");
                Assert(gm.Players[i].SoulEnergy == 0, $"Başlangıçta Oyuncu {i} SoulEnergy == 0 olmalı!");
            }

            gm.Players[1].SeverRandomHand();
            gm.Players[1].SeverRandomHand();
            Assert(!gm.Players[1].IsAlive && gm.Players[1].IsGhost, "2 eli kesilen Oyuncu 1 elenip hayalet (IsGhost == true) olmalı!");

            gm.Players[1].InitializeGhostEnergy(gm.Settings.ghostStartSoulEnergy, gm.Settings.ghostMaxSoulEnergy);
            Assert(gm.Players[1].SoulEnergy == 1, $"Hayalet başlangıç enerjisi 1 olmalı, mevcut: {gm.Players[1].SoulEnergy}");

            seats[1].Avatar.SetGhostMode(true);
            seats[1].Avatar.ResetAvatar();

            pcc.SetGhostSpectatorMode(true, 1, 12f);
            Assert(pcc.IsGhostSpectatorMode, "SetGhostSpectatorMode(true, 1, 12f) sonrası IsGhostSpectatorMode == true olmalı!");
            int prevTarget = pcc.GhostSpectatingSeatIndex;
            pcc.CycleGhostSpectatorTarget(1);
            Assert(pcc.GhostSpectatingSeatIndex != prevTarget, "CycleGhostSpectatorTarget(1) hedef koltuğu değiştirmeli!");
            pcc.SetGhostSpectatorMode(false, 0);
            Assert(!pcc.IsGhostSpectatorMode, "SetGhostSpectatorMode(false, 0) sonrası IsGhostSpectatorMode == false olmalı!");

            reflSys.IsLocalViewerGhost = true;
            Assert(reflSys.IsLocalViewerGhost, "ExecutionReflectionSystem.IsLocalViewerGhost true olarak ayarlanabilmeli!");
            reflSys.IsLocalViewerGhost = false;
            Assert(!reflSys.IsLocalViewerGhost, "ExecutionReflectionSystem.IsLocalViewerGhost false olarak sıfırlanabilmeli!");

            // 2. Ruh Enerjisi & Tur Başı Kazanım
            gm.StopAllCoroutines();
            gm.StartRound();
            Assert(gm.Players[1].SoulEnergy == 2, $"StartRound sonrası hayalet Oyuncu 1 SoulEnergy == 2 olmalı, mevcut: {gm.Players[1].SoulEnergy}");
            Assert(gm.Players[1].SoulEnergy <= gm.Settings.ghostMaxSoulEnergy, "SoulEnergy max sınırı (3) aşmamalı!");

            // 3. Lanet Mekaniği (Pencere, Limit, Röntgen Kısıtı, Sabit Tuzak Sayısı)
            int xraySafeKey = -1;
            int safeKey = -1;
            for (int k = 0; k < gm.Settings.totalKeys; k++)
            {
                if (!gm.TrapIndices.Contains(k))
                {
                    if (xraySafeKey == -1) xraySafeKey = k;
                    else { safeKey = k; break; }
                }
            }
            Assert(xraySafeKey != -1 && safeKey != -1, "Test için 2 adet güvenli tuş bulunamadı!");

            Assert(!gm.IsGhostWindowOpen, "Pencere başlangıçta kapalı olmalı!");
            Assert(!gm.TryGhostCurseKey(1, safeKey), "Pencere kapalıyken TryGhostCurseKey false dönmeli!");

            var openWindowMethod = typeof(GameManager).GetMethod("OpenGhostWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert(openWindowMethod != null, "GameManager.OpenGhostWindow metodu bulunamadı!");
            openWindowMethod.Invoke(gm, null);
            gm.StopAllCoroutines();
            Assert(gm.IsGhostWindowOpen, "OpenGhostWindow sonrası IsGhostWindowOpen == true olmalı!");

            bool xrayOk = gm.InspectKeyWithXRay(xraySafeKey, false);
            Assert(xrayOk, "InspectKeyWithXRay güvenli tuşta true dönmeli!");
            Assert(!gm.TryGhostCurseKey(1, xraySafeKey), "Röntgen ile bakılan tuşa lanet atılması reddedilmeli (false)!");

            int trapCountBefore = gm.TrapIndices.Count;
            bool cursed = gm.TryGhostCurseKey(1, safeKey);
            Assert(cursed, "Geçerli güvenli tuşa TryGhostCurseKey true dönmeli!");
            Assert(gm.Players[1].SoulEnergy == 0, $"Lanet sonrası SoulEnergy 2 azalıp 0 olmalı, mevcut: {gm.Players[1].SoulEnergy}");
            Assert(gm.TrapIndices.Contains(safeKey), "Lanetlenen tuş TrapIndices içine eklenmeli!");
            Assert(gm.TrapIndices.Count == 6 && gm.TrapIndices.Count == trapCountBefore,
                $"Lanet sonrası toplam tuzak sayısı sabit (6) kalmalı, mevcut: {gm.TrapIndices.Count}");

            int anotherSafeKey = -1;
            for (int k = 0; k < gm.Settings.totalKeys; k++)
            {
                if (!gm.TrapIndices.Contains(k) && k != xraySafeKey) { anotherSafeKey = k; break; }
            }
            Assert(anotherSafeKey != -1, "İkinci lanet denemesi için güvenli tuş bulunamadı!");
            gm.Players[1].AddSoulEnergy(2, gm.Settings.ghostMaxSoulEnergy);
            Assert(!gm.TryGhostCurseKey(1, anotherSafeKey), "Aynı turda 2. kez lanet atılması (maxCursesPerRound = 1) reddedilmeli!");

            TypewriterKey testKey = tw.GetKey(safeKey);
            Assert(testKey != null, "Daktiloda lanetlenen tuş bulunamadı!");
            var curseVisibleField = typeof(TypewriterKey).GetField("curseVisibleToLocal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var keyMatField = typeof(TypewriterKey).GetField("keyMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert(curseVisibleField != null && keyMatField != null, "TypewriterKey görsel alanları bulunamadı!");

            testKey.SetCursedVisual(true, false);
            Material hiddenMat = keyMatField.GetValue(testKey) as Material;
            Assert(!(bool)curseVisibleField.GetValue(testKey), "Yaşayan oyuncuda curseVisibleToLocal false olmalı!");
            Assert(hiddenMat != null && hiddenMat.color.b < 0.93f && Mathf.Abs(hiddenMat.color.r - 0.72f) > 0.05f,
                "Yaşayan oyuncuda lanet mor rengi görünmemeli!");

            testKey.SetCursedVisual(true, true);
            Material ghostMat = keyMatField.GetValue(testKey) as Material;
            Assert((bool)curseVisibleField.GetValue(testKey), "Hayalette curseVisibleToLocal true olmalı!");
            Assert(ghostMat != null && Mathf.Abs(ghostMat.color.r - 0.72f) < 0.02f && Mathf.Abs(ghostMat.color.b - 0.96f) < 0.02f,
                "Hayalette lanet mor rengi görünmeli!");

            // 4. Silah Seçimi
            int energyBeforeWeapon = gm.Players[1].SoulEnergy;
            Assert(gm.TryGhostSelectWeapon(1, 2), "TryGhostSelectWeapon(1, 2) true dönmeli!");
            Assert(gm.ChosenNextButcherWeapon == 2, $"ChosenNextButcherWeapon == 2 olmalı, mevcut: {gm.ChosenNextButcherWeapon}");
            Assert(gm.Players[1].SoulEnergy == energyBeforeWeapon, "Silah seçimi ücretsiz olmalı (enerji harcamamalı)!");

            // 5. Komik Tepkiler (Cooldown, Maliyet, Ayar Toggle)
            gm.Players[1].AddSoulEnergy(2, gm.Settings.ghostMaxSoulEnergy);
            int energyBeforeEffect = gm.Players[1].SoulEnergy;
            Assert(gm.TryGhostTriggerEffect(1, GhostEffectType.EerieLaugh), "İlk TryGhostTriggerEffect çağrısı true dönmeli!");
            Assert(gm.Players[1].SoulEnergy == energyBeforeEffect - 1,
                $"Efekt sonrası SoulEnergy 1 azalmalı ({energyBeforeEffect - 1}), mevcut: {gm.Players[1].SoulEnergy}");
            Assert(!gm.TryGhostTriggerEffect(1, GhostEffectType.EerieLaugh),
                "Hemen ardından spam yapılan TryGhostTriggerEffect cooldown nedeniyle false dönmeli!");
            bool ghostEffectsSetting = SettingsMenuController.AreGhostEffectsEnabled();
            Assert(ghostEffectsSetting == (PlayerPrefs.GetInt("FM_GhostEffects", 1) == 1),
                "SettingsMenuController.AreGhostEffectsEnabled() beklenen değeri döndürmeli!");

            // 6. Bot Hayaletler (AIBrain.DecideGhostAction)
            var aiBrain = Object.FindFirstObjectByType<FingerMonster.AI.AIBrain>();
            Assert(aiBrain != null, "Sahnede AIBrain bileşeni bulunamadı!");
            aiBrain.RegisterBotProfile(1, FingerMonster.AI.BotProfile.ForCharacterSlot(1, CharacterArchetype.Sinsi));
            aiBrain.RegisterBotProfile(2, FingerMonster.AI.BotProfile.ForCharacterSlot(2, CharacterArchetype.Sovmen));

            var availableKeys = new List<int> { 0, 1, 2, 3, 4 };
            var xrayExcluded = new HashSet<int> { 0, 1 };
            int[] ghostBotSeats = { 1, 2 };
            for (int b = 0; b < ghostBotSeats.Length; b++)
            {
                for (int trial = 0; trial < 15; trial++)
                {
                    GhostActionType action = aiBrain.DecideGhostAction(
                        ghostBotSeats[b], 3, availableKeys, xrayExcluded,
                        out int botKey, out int botWeapon, out GhostEffectType botEffect);

                    Assert(System.Enum.IsDefined(typeof(GhostActionType), action), "Geçersiz GhostActionType döndü!");
                    Assert(botWeapon >= 0 && botWeapon < 4, $"Bot silah indeksi 0..3 aralığında olmalı, dönen: {botWeapon}");
                    Assert(System.Enum.IsDefined(typeof(GhostEffectType), botEffect), "Geçersiz GhostEffectType döndü!");
                    if (action == GhostActionType.CurseKey)
                    {
                        Assert(availableKeys.Contains(botKey) && !xrayExcluded.Contains(botKey),
                            $"Bot Röntgen hariç tutulan tuşlardan seçmemeli, seçilen: {botKey}");
                    }
                }
            }

            // 7. 3 Kez Art Arda "Yeniden Oyna" Temizliği & NetworkPlayerSlot.IsGhost & HUD Başlatma
            for (int replay = 1; replay <= 3; replay++)
            {
                gm.StopAllCoroutines();
                gm.ResetTableForNewGame();
                gm.InitializeGame();

                Assert(!gm.IsGhostWindowOpen, $"Yeniden Oyna #{replay}: IsGhostWindowOpen == false olmalı!");
                Assert(gm.CursedKeyIndex == -1, $"Yeniden Oyna #{replay}: CursedKeyIndex == -1 olmalı!");
                Assert(gm.CursesUsedThisRound == 0, $"Yeniden Oyna #{replay}: CursesUsedThisRound == 0 olmalı!");
                Assert(gm.ChosenNextButcherWeapon == -1, $"Yeniden Oyna #{replay}: ChosenNextButcherWeapon == -1 olmalı!");
                Assert(!pcc.IsGhostSpectatorMode, $"Yeniden Oyna #{replay}: IsGhostSpectatorMode == false olmalı!");

                for (int p = 0; p < 4; p++)
                {
                    Assert(gm.Players[p].IsAlive && !gm.Players[p].IsGhost,
                        $"Yeniden Oyna #{replay}: Oyuncu {p} IsAlive == true olmalı!");
                    Assert(gm.Players[p].SoulEnergy == 0,
                        $"Yeniden Oyna #{replay}: Oyuncu {p} SoulEnergy == 0 olmalı!");
                }
            }

            var ghostSlot = new FingerMonster.Networking.NetworkPlayerSlot(77UL, 1, "GhostBot", true, Color.magenta, 2)
            {
                IsGhost = true
            };
            using (var writer = new Unity.Netcode.FastBufferWriter(256, Unity.Collections.Allocator.Temp))
            {
                writer.WriteNetworkSerializable(in ghostSlot);
                using (var reader = new Unity.Netcode.FastBufferReader(writer, Unity.Collections.Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out FingerMonster.Networking.NetworkPlayerSlot deserializedGhostSlot);
                    Assert(deserializedGhostSlot.Equals(ghostSlot) && deserializedGhostSlot.IsGhost,
                        "NetworkPlayerSlot.IsGhost serileştirme/deserileştirme round-trip başarısız!");
                }
            }

            HUDController hudCtrl = Object.FindFirstObjectByType<HUDController>(FindObjectsInactive.Include);
            Assert(hudCtrl != null, "HUDController sahnede bulunamadı!");
            hudCtrl.SetupHUD(gm.Players);

            GhostHUD ghostHud = Object.FindFirstObjectByType<GhostHUD>(FindObjectsInactive.Include);
            Assert(ghostHud != null, "GhostHUD sahnede bulunamadı!");
            ghostHud.EnsureInitialized();

            Debug.Log("<color=green>=== [GHOST ASSISTANT DOĞRULAMA TESTİ BAŞARIYLA GEÇTİ (100% OK)] ===</color>");
        }

        [MenuItem("FingerMonster/Run Ghost Camera And HUD Layout Verification")]
        public static void RunGhostCameraAndHUDLayoutVerification()
        {
            Debug.Log("<color=cyan>=== [HAYALET KAMERA & HUD DÜZENİ DOĞRULAMA TESTİ BAŞLADI] ===</color>");

            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Single);
            Assert(scene.IsValid(), "Game.unity sahnesi geçerli değil!");

            GameManager gm = Object.FindFirstObjectByType<GameManager>();
            Assert(gm != null, "GameManager sahnede bulunamadı!");
            gm.InitializeGame();

            PlayerCameraController pcc = Object.FindFirstObjectByType<PlayerCameraController>();
            Assert(pcc != null, "PlayerCameraController sahnede bulunamadı!");
            Camera cam = pcc.GetComponent<Camera>();
            Assert(cam != null, "PlayerCameraController üzerinde Camera bileşeni bulunamadı!");

            // 1. Hayalet Kamerası Oda Sınırları & 4 Koltuğa Q/E Geçişi
            pcc.SetGhostSpectatorMode(true, 0, 12f);
            Assert(pcc.IsGhostSpectatorMode, "SetGhostSpectatorMode(true, 0, 12f) sonrası IsGhostSpectatorMode true olmalı!");
            Assert(Mathf.Abs(cam.nearClipPlane - 0.15f) < 0.01f,
                $"Hayalet modunda nearClipPlane 0.15f olmalı, mevcut: {cam.nearClipPlane}");

            System.Reflection.MethodInfo lateUpdateMethod = typeof(PlayerCameraController).GetMethod(
                "LateUpdate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            Assert(lateUpdateMethod != null, "PlayerCameraController.LateUpdate metodu reflection ile bulunamadı!");

            Vector3 tableLookOrigin = new Vector3(0f, 0.85f, 0f);
            for (int seat = 0; seat < 4; seat++)
            {
                if (seat > 0)
                {
                    pcc.CycleGhostSpectatorTarget(1);
                }

                for (int f = 0; f < 60; f++)
                {
                    lateUpdateMethod.Invoke(pcc, null);
                }

                Vector3 pos = pcc.transform.position;
                Assert(pos.x >= -3.45f && pos.x <= 3.45f &&
                       pos.y >= 1.50f && pos.y <= 2.90f &&
                       pos.z >= -3.45f && pos.z <= 3.45f,
                    $"Koltuk {seat} hayalet kamerası oda sınırları dışında! Pozisyon: {pos}");

                Vector3 toTable = (tableLookOrigin - pos).normalized;
                float dot = Vector3.Dot(pcc.transform.forward, toTable);
                Assert(dot > 0.7f,
                    $"Koltuk {seat} hayalet kamerası masa/makine merkezine bakmıyor! Dot={dot:F3}, Pos={pos}, Forward={pcc.transform.forward}");
            }

            pcc.SetGhostSpectatorMode(false, 0);
            Assert(!pcc.IsGhostSpectatorMode && Mathf.Abs(cam.nearClipPlane - 0.3f) < 0.01f,
                $"Hayalet modundan çıkınca IsGhostSpectatorMode false ve nearClipPlane 0.3f olmalı! IsGhost={pcc.IsGhostSpectatorMode}, nearClip={cam.nearClipPlane}");

            // 2. Gerçek Karakter Adı Doğrulaması (GetSpectatedCharacterDisplayName)
            GhostHUD ghostHud = Object.FindFirstObjectByType<GhostHUD>(FindObjectsInactive.Include);
            Assert(ghostHud != null, "GhostHUD sahnede bulunamadı!");
            ghostHud.EnsureInitialized();

            for (int i = 0; i < 4; i++)
            {
                string charName = ghostHud.GetSpectatedCharacterDisplayName(i);
                Assert(!charName.Contains("Oyuncu"),
                    $"Koltuk {i} adı 'Oyuncu' içermemeli, dönen: '{charName}'");
                Assert(charName.Contains("Feg") || charName.Contains("Kenan") || charName.Contains("Feyzi") || charName.Contains("MMG"),
                    $"Koltuk {i} adı gerçek karakter adını (Feg/Kenan/Feyzi/MMG) içermeli, dönen: '{charName}'");
            }

            gm.Players[1].SeverRandomHand();
            gm.Players[1].SeverRandomHand();
            Assert(!gm.Players[1].IsAlive, "Koltuk 1 iki elini kaybettikten sonra elenmiş olmalı!");
            string eliminatedName = ghostHud.GetSpectatedCharacterDisplayName(1);
            Assert(eliminatedName.Contains("(Hayalet)"),
                $"Elenen Koltuk 1 adı '(Hayalet)' içermeli, dönen: '{eliminatedName}'");

            // 3. 5 Çözünürlükte HUD Çakışma Kontrolü (CheckPanelOverlaps)
            CanvasScaler scaler = Object.FindFirstObjectByType<CanvasScaler>(FindObjectsInactive.Include);
            Assert(scaler != null, "Sahnede CanvasScaler bulunamadı!");
            Assert(scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize,
                $"CanvasScaler.uiScaleMode ScaleWithScreenSize olmalı, mevcut: {scaler.uiScaleMode}");
            Assert(scaler.referenceResolution == new Vector2(1920f, 1080f),
                $"CanvasScaler.referenceResolution (1920, 1080) olmalı, mevcut: {scaler.referenceResolution}");
            Assert(Mathf.Abs(scaler.matchWidthOrHeight - 0.5f) < 0.01f,
                $"CanvasScaler.matchWidthOrHeight 0.5f olmalı, mevcut: {scaler.matchWidthOrHeight}");

            Canvas targetCanvas = scaler.GetComponent<Canvas>();
            Assert(targetCanvas != null, "CanvasScaler üzerinde Canvas bileşeni bulunamadı!");
            RectTransform canvasRect = targetCanvas.GetComponent<RectTransform>();
            Assert(canvasRect != null, "Canvas üzerinde RectTransform bulunamadı!");

            Transform ghostRootTransform = targetCanvas.transform.Find("GhostHUD_Root");
            Assert(ghostRootTransform != null, "Canvas altında GhostHUD_Root bulunamadı!");
            bool prevGhostRootActive = ghostRootTransform.gameObject.activeSelf;
            ghostRootTransform.gameObject.SetActive(true);

            Vector2[] resolutions =
            {
                new Vector2(1920, 1080),
                new Vector2(1366, 768),
                new Vector2(2560, 1440),
                new Vector2(1920, 1200),
                new Vector2(3440, 1440)
            };

            Vector2 originalSizeDelta = canvasRect.sizeDelta;
            for (int r = 0; r < resolutions.Length; r++)
            {
                Vector2 res = resolutions[r];
                canvasRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, res.x);
                canvasRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, res.y);
                Canvas.ForceUpdateCanvases();

                bool noOverlap = ghostHud.CheckPanelOverlaps(out string overlapReport);
                Assert(noOverlap, $"Çözünürlük {res.x}x{res.y} için HUD çakışması tespit edildi: {overlapReport}");
            }

            canvasRect.sizeDelta = originalSizeDelta;
            ghostRootTransform.gameObject.SetActive(prevGhostRootActive);
            Canvas.ForceUpdateCanvases();

            // 4. Enerji 0, 1, 2, 3 & 3x Yeniden Oyna Çift Panel Koruması
            int[] testEnergies = { 0, 1, 2, 3 };
            for (int e = 0; e < testEnergies.Length; e++)
            {
                gm.Players[1].SoulEnergy = testEnergies[e];
                ghostHud.EnsureInitialized();
                Assert(gm.Players[1].SoulEnergy == testEnergies[e],
                    $"SoulEnergy={testEnergies[e]} durumunda EnsureInitialized sonrası enerji korunmalı!");
            }

            for (int replay = 0; replay < 3; replay++)
            {
                gm.ResetTableForNewGame();
                gm.InitializeGame();
                ghostHud.ResetForNewMatch();
                ghostHud.EnsureInitialized();
            }

            int ghostRootCount = 0;
            RectTransform[] allRects = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allRects.Length; i++)
            {
                if (allRects[i] != null && allRects[i].name == "GhostHUD_Root")
                {
                    ghostRootCount++;
                }
            }
            Assert(ghostRootCount == 1, $"3x Yeniden Oyna sonrası sahnede tekil GhostHUD_Root olmalı, bulunan: {ghostRootCount}");

            Debug.Log("<color=green>=== [HAYALET KAMERA & HUD DÜZENİ DOĞRULAMA TESTİ BAŞARIYLA GEÇTİ (100% OK)] ===</color>");
        }

        [MenuItem("FingerMonster/Test/Run Ghost Active Gameplay Verification")]
        public static void RunGhostActiveGameplayVerification()
        {
            Debug.Log("<color=cyan>=== [HAYALET AKTİF OYNANIŞ DOĞRULAMA TESTİ BAŞLADI] ===</color>");

            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Single);
            Assert(scene.IsValid(), "Game.unity sahnesi geçerli değil!");

            GameManager gm = Object.FindFirstObjectByType<GameManager>();
            Assert(gm != null, "GameManager sahnede bulunamadı!");
            gm.InitializeGame();

            PlayerCameraController pcc = Object.FindFirstObjectByType<PlayerCameraController>();
            Assert(pcc != null, "PlayerCameraController sahnede bulunamadı!");

            // 1. Serbest Hareket & Sınırlar & Tab Geçişi & Yakınlık Saydamlığı
            pcc.SetGhostSpectatorMode(true, 0, 12f);
            Assert(pcc.CurrentGhostMode == GhostMovementMode.FreeFlight,
                $"SetGhostSpectatorMode(true) sonrası CurrentGhostMode FreeFlight olmalı, mevcut: {pcc.CurrentGhostMode}");

            MethodInfo clampBoundsMethod = typeof(PlayerCameraController).GetMethod(
                "ClampToGhostBounds",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert(clampBoundsMethod != null, "PlayerCameraController.ClampToGhostBounds metodu bulunamadı!");

            Vector3 outOfBoundsPos = new Vector3(50f, 25f, -50f);
            Vector3 clampedPos = (Vector3)clampBoundsMethod.Invoke(null, new object[] { outOfBoundsPos });
            Assert(clampedPos.x >= -3.45f && clampedPos.x <= 3.45f &&
                   clampedPos.y >= 1.50f && clampedPos.y <= 2.90f &&
                   clampedPos.z >= -3.45f && clampedPos.z <= 3.45f,
                $"ClampToGhostBounds oda sınırları içinde kalmalı! Dönen: {clampedPos}");

            MethodInfo toggleModeMethod = typeof(PlayerCameraController).GetMethod(
                "ToggleGhostMovementMode",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert(toggleModeMethod != null, "PlayerCameraController.ToggleGhostMovementMode metodu bulunamadı!");

            toggleModeMethod.Invoke(pcc, null);
            Assert(pcc.CurrentGhostMode == GhostMovementMode.SpectatorOrbit,
                $"İlk ToggleGhostMovementMode sonrası SpectatorOrbit olmalı, mevcut: {pcc.CurrentGhostMode}");

            toggleModeMethod.Invoke(pcc, null);
            Assert(pcc.CurrentGhostMode == GhostMovementMode.FreeFlight,
                $"İkinci ToggleGhostMovementMode sonrası FreeFlight olmalı, mevcut: {pcc.CurrentGhostMode}");

            PlayerSeat[] seats = Object.FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None);
            Assert(seats != null && seats.Length > 0 && seats[0].Avatar != null, "Test için PlayerVisualAvatar bulunamadı!");
            PlayerVisualAvatar ghostAvatar = seats[0].Avatar;
            ghostAvatar.SetGhostMode(true);

            FieldInfo alphaField = typeof(PlayerVisualAvatar).GetField(
                "currentGhostAlpha",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert(alphaField != null, "PlayerVisualAvatar.currentGhostAlpha alanı bulunamadı!");

            Vector3 nearCameraPos = ghostAvatar.transform.position + new Vector3(0f, 0.5f, 0.4f);
            for (int step = 0; step < 120; step++)
            {
                ghostAvatar.UpdateGhostProximityFade(nearCameraPos, Vector3.zero, 1.0f, 0.15f);
            }
            float nearAlpha = (float)alphaField.GetValue(ghostAvatar);
            if (Time.deltaTime > 0.0001f)
            {
                Assert(Mathf.Abs(nearAlpha - 0.15f) < 0.02f,
                    $"Yakınlık (< 1m) durumunda currentGhostAlpha ~0.15f olmalı, mevcut: {nearAlpha:F3}");
            }
            else
            {
                Assert(nearAlpha >= 0.15f && nearAlpha <= 0.42f,
                    $"currentGhostAlpha geçerli aralıkta (0.15..0.42) olmalı, mevcut: {nearAlpha:F3}");
            }
            ghostAvatar.SetGhostMode(false);
            pcc.SetGhostSpectatorMode(false, 0);

            // 2. Korkutma ("Buuu!") & Ayarlar
            GameObject targetObj = new GameObject("TempScareTarget");
            targetObj.transform.position = Vector3.zero;
            targetObj.transform.rotation = Quaternion.identity; // forward = +Z

            bool frontInRange = GhostScareAndThrowSystem.CanScareTarget(new Vector3(0f, 0f, 2.0f), targetObj.transform, 3.0f);
            bool behindTarget = GhostScareAndThrowSystem.CanScareTarget(new Vector3(0f, 0f, -2.0f), targetObj.transform, 3.0f);
            bool outOfRange = GhostScareAndThrowSystem.CanScareTarget(new Vector3(0f, 0f, 4.5f), targetObj.transform, 3.0f);
            Object.DestroyImmediate(targetObj);

            Assert(frontInRange, "Ön yarımküre ve <= 3m hedefte CanScareTarget true dönmeli!");
            Assert(!behindTarget, "Hedefin arkasında CanScareTarget false dönmeli!");
            Assert(!outOfRange, "Menzil (> 3m) dışında CanScareTarget false dönmeli!");

            int prevGhostEffects = PlayerPrefs.GetInt("GhostEffectsEnabled", 1);
            int prevReduceEffects = PlayerPrefs.GetInt("ReduceGhostEffects", 0);
            try
            {
                PlayerPrefs.SetInt("GhostEffectsEnabled", 1);
                PlayerPrefs.SetInt("ReduceGhostEffects", 1);
                Assert(SettingsMenuController.AreGhostEffectsEnabled(), "AreGhostEffectsEnabled true dönmeli!");
                Assert(SettingsMenuController.AreGhostEffectsReduced(), "AreGhostEffectsReduced true dönmeli!");
            }
            finally
            {
                PlayerPrefs.SetInt("GhostEffectsEnabled", prevGhostEffects);
                PlayerPrefs.SetInt("ReduceGhostEffects", prevReduceEffects);
            }

            // 3. Fırlatılabilir Eşyalar & Ekran Kenar Sıçrama Alanı & Temizlik
            List<ThrowableItemData> throwables = ThrowableItemData.CreateRuntimeDefaultList();
            Assert(throwables != null && throwables.Count == 4,
                $"ThrowableItemData.CreateRuntimeDefaultList() 4 eşya içermeli, bulunan: {(throwables != null ? throwables.Count : -1)}");
            Assert(throwables.Exists(t => t.itemType == ThrowableItemType.CreamPie) &&
                   throwables.Exists(t => t.itemType == ThrowableItemType.Tomato) &&
                   throwables.Exists(t => t.itemType == ThrowableItemType.PaperBall) &&
                   throwables.Exists(t => t.itemType == ThrowableItemType.Sausage),
                "CreateRuntimeDefaultList() CreamPie, Tomato, PaperBall ve Sausage eşyalarının tamamını içermeli!");

            GhostScareAndThrowSystem scareSystem = Object.FindFirstObjectByType<GhostScareAndThrowSystem>(FindObjectsInactive.Include);
            if (scareSystem == null)
            {
                GameObject scareGo = new GameObject("TempGhostScareAndThrowSystem");
                scareSystem = scareGo.AddComponent<GhostScareAndThrowSystem>();
            }

            MethodInfo ensureOverlayMethod = typeof(GhostScareAndThrowSystem).GetMethod(
                "EnsureOverlayCanvas",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert(ensureOverlayMethod != null, "GhostScareAndThrowSystem.EnsureOverlayCanvas bulunamadı!");
            ensureOverlayMethod.Invoke(scareSystem, null);

            FieldInfo splatImagesField = typeof(GhostScareAndThrowSystem).GetField(
                "screenSplatImages",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert(splatImagesField != null, "GhostScareAndThrowSystem.screenSplatImages alanı bulunamadı!");
            List<Image> splatImages = splatImagesField.GetValue(scareSystem) as List<Image>;
            Assert(splatImages != null && splatImages.Count > 0, "Screen edge splat görüntüleri oluşturulmalı!");

            float totalSplatAreaRatio = 0f;
            for (int i = 0; i < splatImages.Count; i++)
            {
                RectTransform rt = splatImages[i].rectTransform;
                Vector2 size = rt.anchorMax - rt.anchorMin;
                totalSplatAreaRatio += Mathf.Max(0f, size.x) * Mathf.Max(0f, size.y);
            }
            Assert(totalSplatAreaRatio > 0f && totalSplatAreaRatio <= 0.30f,
                $"Ekran kenar sıçrama alanı ekranın <= %30'unda olmalı, mevcut: %{totalSplatAreaRatio * 100f:F1}");

            scareSystem.LaunchThrowableVisual(throwables[0], new Vector3(0f, 2f, -2f), Vector3.zero, 0, ghostAvatar, false);
            FieldInfo projectilesField = typeof(GhostScareAndThrowSystem).GetField(
                "activeProjectiles",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            FieldInfo decalsField = typeof(GhostScareAndThrowSystem).GetField(
                "activeDecals",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert(projectilesField != null && decalsField != null, "GhostScareAndThrowSystem efekt listeleri bulunamadı!");

            var activeProjectiles = projectilesField.GetValue(scareSystem) as IList;
            Assert(activeProjectiles != null && activeProjectiles.Count >= 1,
                "LaunchThrowableVisual sonrası aktif fırlatılan nesne oluşmalı!");

            scareSystem.ClearAllActiveEffects();
            var activeDecals = decalsField.GetValue(scareSystem) as IList;
            Assert(activeProjectiles.Count == 0 && (activeDecals == null || activeDecals.Count == 0),
                "ClearAllActiveEffects() sonrası kalan aktif efekt sayısı 0 olmalı!");

            // 4. Kasap Silahları & GhostOnly Önizleme & ExecutionReflectionSystem Kamera Maskesi
            ExecutionWeaponType[] allWeapons =
            {
                ExecutionWeaponType.GiantMeatMallet,
                ExecutionWeaponType.FlyingCastIronPan,
                ExecutionWeaponType.SausageWhip,
                ExecutionWeaponType.CreamPieLauncher
            };
            for (int i = 0; i < allWeapons.Length; i++)
            {
                WeaponData wd = WeaponData.CreateRuntimeDefault(allWeapons[i]);
                Assert(wd != null && wd.weaponType == allWeapons[i],
                    $"WeaponData.CreateRuntimeDefault({allWeapons[i]}) geçerli silah verisi döndürmeli!");
            }

            ButcherPlaceholder butcher = Object.FindFirstObjectByType<ButcherPlaceholder>(FindObjectsInactive.Include);
            Assert(butcher != null, "ButcherPlaceholder sahnede bulunamadı!");

            WeaponData malletData = WeaponData.CreateRuntimeDefault(ExecutionWeaponType.GiantMeatMallet);
            WeaponData panData = WeaponData.CreateRuntimeDefault(ExecutionWeaponType.FlyingCastIronPan);
            butcher.EquipWeaponData(malletData);
            butcher.EquipWeaponData(panData);

            FieldInfo kasapRootField = typeof(ButcherPlaceholder).GetField(
                "kasapInstanceRoot",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Transform kasapRoot = kasapRootField != null ? kasapRootField.GetValue(butcher) as Transform : null;
            if (kasapRoot != null)
            {
                Transform socket = null;
                Transform[] children = kasapRoot.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i] != null && children[i].name == "WeaponSocket")
                    {
                        socket = children[i];
                        break;
                    }
                }
                if (socket != null)
                {
                    int activeChildCount = 0;
                    for (int c = 0; c < socket.childCount; c++)
                    {
                        if (socket.GetChild(c) != null && socket.GetChild(c).gameObject.activeSelf)
                        {
                            activeChildCount++;
                        }
                    }
                    Assert(activeChildCount <= 1,
                        $"EquipWeaponData sonrası WeaponSocket altında tek silah olmalı (çift silah yok), bulunan: {activeChildCount}");
                }
            }

            int ghostLayer = LayerMask.NameToLayer("GhostOnly");
            if (ghostLayer < 0) ghostLayer = 11;

            butcher.SpawnOrUpdateGhostPreview(ExecutionWeaponType.SausageWhip, new Vector3(2.8f, 0f, 2.8f), Quaternion.identity);
            FieldInfo previewRootField = typeof(ButcherPlaceholder).GetField(
                "ghostPreviewRoot",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert(previewRootField != null, "ButcherPlaceholder.ghostPreviewRoot alanı bulunamadı!");

            GameObject previewRoot = previewRootField.GetValue(butcher) as GameObject;
            Assert(previewRoot != null, "SpawnOrUpdateGhostPreview sonrası ghostPreviewRoot oluşturulmalı!");
            Assert(previewRoot.layer == ghostLayer,
                $"ghostPreviewRoot katmanı GhostOnly ({ghostLayer}) olmalı, mevcut: {previewRoot.layer}");

            butcher.DestroyGhostPreview();
            Assert(previewRootField.GetValue(butcher) == null,
                "DestroyGhostPreview() sonrası ghostPreviewRoot null olmalı!");

            ExecutionReflectionSystem reflectionSystem = Object.FindFirstObjectByType<ExecutionReflectionSystem>(FindObjectsInactive.Include);
            if (reflectionSystem != null)
            {
                reflectionSystem.IsLocalViewerGhost = false;
                reflectionSystem.ConfigureVisibility(false);
                if (Camera.main != null)
                {
                    Assert((Camera.main.cullingMask & (1 << ghostLayer)) == 0,
                        "Yaşayan oyuncu kamera maskesi GhostOnly bitini içermemeli!");
                }
            }

            // 5. Ses & Bot Hayaletler
            FingerMonster.Audio.AudioManager audioMgr = FingerMonster.Audio.AudioManager.Instance != null
                ? FingerMonster.Audio.AudioManager.Instance
                : Object.FindFirstObjectByType<FingerMonster.Audio.AudioManager>(FindObjectsInactive.Include);
            if (audioMgr != null)
            {
                audioMgr.PlayGhostScare(Vector3.zero);
                audioMgr.PlayGhostThrow(Vector3.zero);
                for (int i = 0; i < throwables.Count; i++)
                {
                    audioMgr.PlayGhostSplat(throwables[i].itemType, Vector3.zero);
                }
                for (int i = 0; i < allWeapons.Length; i++)
                {
                    audioMgr.PlayButcherWeaponHit(allWeapons[i], Vector3.zero);
                }
            }

            BotPersonality[] personalities =
            {
                BotPersonality.Sinsi,
                BotPersonality.Sovmen,
                BotPersonality.Urkek,
                BotPersonality.Cesur
            };
            List<int> aliveSeats = new List<int> { 0, 2, 3 };
            float[] scareImmunity = new float[4];
            Vector3[] seatPositions =
            {
                new Vector3(0f, 0f, -2.05f),
                new Vector3(0f, 0f, 1.48f),
                new Vector3(-1.68f, 0f, 0f),
                new Vector3(1.68f, 0f, 0f)
            };
            Bounds roomBounds = new Bounds(new Vector3(0f, 2.20f, 0f), new Vector3(6.8f, 1.30f, 6.8f));

            for (int p = 0; p < personalities.Length; p++)
            {
                BotGhostDecision decision = AIBrain.DecideBotGhostActiveAction(
                    personalities[p], 3, aliveSeats, scareImmunity, true, true);
                Assert(decision.Choice != BotGhostActiveChoice.None,
                    $"{personalities[p]} bot hayalet 3 enerjiyle geçerli bir aksiyon seçmeli!");

                Vector3 roamTarget = AIBrain.ComputeBotGhostRoamTarget(personalities[p], 1, seatPositions, roomBounds, 4.2f);
                Assert(!float.IsNaN(roamTarget.x) && !float.IsNaN(roamTarget.y) && !float.IsNaN(roamTarget.z),
                    $"{personalities[p]} ComputeBotGhostRoamTarget NaN üretmemeli!");
                Assert(roomBounds.Contains(roamTarget),
                    $"{personalities[p]} ComputeBotGhostRoamTarget oda sınırları içinde kalmalı! Dönen: {roamTarget}");
            }

            // 6. 3x Replay Döngüsü
            for (int replay = 0; replay < 3; replay++)
            {
                gm.ResetTableForNewGame();
                gm.InitializeGame();
                scareSystem.ClearAllActiveEffects();
                butcher.DestroyGhostPreview();

                Assert(activeProjectiles.Count == 0 && (activeDecals == null || activeDecals.Count == 0),
                    $"Replay {replay + 1} sonrası GhostScareAndThrowSystem temiz olmalı!");
                Assert(previewRootField.GetValue(butcher) == null,
                    $"Replay {replay + 1} sonrası ButcherPlaceholder GhostPreview null olmalı!");
            }

            Debug.Log("<color=green>=== [HAYALET AKTİF OYNANIŞ DOĞRULAMA TESTİ BAŞARIYLA GEÇTİ (100% OK)] ===</color>");
        }

        [MenuItem("FingerMonster/Run All Batchmode Verifications")]
        public static void RunAllBatchmodeVerifications()
        {
            BodyguardSetupTool.RunFullSetup();
            SceneSetupTool.BuildAllScenes();
            RunVerification();
            RunStage2Verification();
            RunStage3Verification();
            RunStage4BodyguardVerification();
            RunEmoteTargetCyclingTest();
            RunReplayAndPersistenceTest();
            RunAbilityCardsVerification();
            RunGhostAssistantVerification();
            RunGhostCameraAndHUDLayoutVerification();
            RunGhostActiveGameplayVerification();
            Debug.Log("<color=green>[ALL BATCHMODE VERIFICATIONS PASSED]</color>");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Debug.LogError($"<color=red>[ASSERT FAILED] {message}</color>");
                throw new System.Exception("Test Başarısız: " + message);
            }
        }
    }
}

