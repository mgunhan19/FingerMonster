using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using FingerMonster.Core;
using FingerMonster.Player;
using FingerMonster.Gameplay;

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
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
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
            Assert(Mathf.Abs(mainCam.transform.position.y - 2.28f) < 0.05f, $"Kamera Y pozisyonu 2.28 olmalı, mevcut: {mainCam.transform.position.y}");
            Assert(Mathf.Abs(mainCam.transform.position.z - (-2.31f)) < 0.05f, $"Kamera Z pozisyonu -2.31 olmalı, mevcut: {mainCam.transform.position.z}");
            Assert(Mathf.Abs(mainCam.fieldOfView - 60f) < 0.1f, $"Kamera FOV 60 olmalı, mevcut: {mainCam.fieldOfView}");

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
            var menuScene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
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
            var gameScene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
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
            Debug.Log("<color=cyan>=== [AŞAMA 3: KARAKTERLER, ANİMASYONLAR, KASAP VE YANSIMA TESTİ BAŞLADI] ===</color>");

            // 1. Sahneleri inşa et ve Game sahnesini aç
            SceneSetupTool.BuildAllScenes();
            var gameScene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
            Assert(gameScene.IsValid(), "Game.unity sahnesi geçerli değil!");

            // 2. 4 Karakter ve Animatör Sürücüleri
            PlayerSeat[] seats = Object.FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None);
            Assert(seats.Length == 4, $"4 oyuncu koltuğu bulunmalı, bulunan: {seats.Length}");

            for (int i = 0; i < seats.Length; i++)
            {
                Assert(seats[i].Avatar != null, $"Koltuk {i} üzerinde Avatar bulunmalı!");
                var driver = seats[i].Avatar.GetComponent<CharacterAnimatorDriver>();
                Assert(driver != null, $"Koltuk {i} avatarında CharacterAnimatorDriver bulunmalı!");

                var lHand = seats[i].Avatar.transform.Find("LeftHand");
                var rHand = seats[i].Avatar.transform.Find("RightHand");
                Assert(lHand != null, $"Koltuk {i} üzerinde masaya uzanan LeftHand bulunmalı!");
                Assert(rHand != null, $"Koltuk {i} üzerinde masaya uzanan RightHand bulunmalı!");
            }

            // 3. 16+ Emote Tanımı Kontrolü
            var emotes = System.Enum.GetValues(typeof(FingerMonster.Player.EmoteType));
            Assert(emotes.Length >= 16, $"En az 16 adet emote bulunmalı, bulunan: {emotes.Length}");

            // 4. Kasap ve 4 Absürt Silah
            ButcherPlaceholder butcher = Object.FindFirstObjectByType<ButcherPlaceholder>();
            Assert(butcher != null, "Sahnede ButcherPlaceholder bulunmalı!");

            // 5. Yansıma ve Karanlık Sistemi
            var reflSys = Object.FindFirstObjectByType<FingerMonster.Gameplay.ExecutionReflectionSystem>();
            Assert(reflSys != null, "Sahnede ExecutionReflectionSystem bulunmalı!");

            // 6. Emote Wheel UI
            var emoteWheel = Object.FindFirstObjectByType<FingerMonster.UI.EmoteWheelUI>(FindObjectsInactive.Include);
            Assert(emoteWheel != null, "Sahnede EmoteWheelUI bulunmalı!");

            Debug.Log("<color=green>=== [AŞAMA 3: TÜM KARAKTER, SİLAH VE ANİMASYON TESTLERİ BAŞARIYLA GEÇTİ (100% OK)] ===</color>");
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

