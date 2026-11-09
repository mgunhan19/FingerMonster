using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FingerMonster.Player;
using FingerMonster.Gameplay;
using FingerMonster.UI;
using FingerMonster.Audio;

namespace FingerMonster.Core
{
    /// <summary>
    /// FingerMonster oyununun merkezi durum makinesi ve hakem sınıfı.
    /// Tüm oyuncu verileri, tuzak konumları, tur sırası ve durum geçişleri burada denetlenir.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Ayarlar")]
        [SerializeField] private GameSettings settings;
        public GameSettings Settings => settings;

        [Header("Sahne Referansları")]
        [SerializeField] private TypewriterMachine typewriter;
        [SerializeField] private TurnController turnController;
        [SerializeField] private PlayerCameraController cameraController;
        [SerializeField] private RoomLightingController lightingController;
        [SerializeField] private ButcherPlaceholder butcher;
        [SerializeField] private List<PlayerSeat> playerSeats = new List<PlayerSeat>();

        [Header("UI Referansları")]
        [SerializeField] private HUDController hudController;
        [SerializeField] private GameOverUIController gameOverController;
        [SerializeField] private PauseMenuController pauseMenuController;

        // Oyun Durumu
        public GameState CurrentState { get; private set; } = GameState.Lobby;
        public int CurrentRound { get; private set; } = 1;
        public int ActivePlayerIndex => turnController != null ? turnController.ActivePlayerIndex : 0;
        public TypewriterMachine Typewriter => typewriter;

        public List<PlayerData> Players { get; private set; } = new List<PlayerData>();
        public HashSet<int> TrapIndices { get; private set; } = new HashSet<int>();
        public HashSet<int> PressedIndices { get; private set; } = new HashSet<int>();

        public int RemainingSafeKeysCount
        {
            get
            {
                int totalSafe = (settings != null ? settings.totalKeys : 20) - TrapIndices.Count;
                int pressedSafe = 0;
                foreach (int idx in PressedIndices)
                {
                    if (!TrapIndices.Contains(idx)) pressedSafe++;
                }
                return Mathf.Max(0, totalSafe - pressedSafe);
            }
        }

        // Olaylar (Events)
        public event Action<GameState> OnStateChanged;
        public event Action<PlayerData> OnTurnChanged;

        private void Awake()
        {
            Application.runInBackground = true;

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureSettings();
        }

        public void EnsureSettings()
        {
            if (Instance == null) Instance = this;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GameSettings>();
            }
            EnsureReferences();
        }

        public void EnsureReferences()
        {
            if (hudController == null) hudController = UnityEngine.Object.FindFirstObjectByType<HUDController>(FindObjectsInactive.Include);
            if (gameOverController == null) gameOverController = UnityEngine.Object.FindFirstObjectByType<GameOverUIController>(FindObjectsInactive.Include);
            if (pauseMenuController == null) pauseMenuController = UnityEngine.Object.FindFirstObjectByType<PauseMenuController>(FindObjectsInactive.Include);
            if (turnController == null) turnController = GetComponent<TurnController>();
            if (cameraController == null) cameraController = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>();
            if (lightingController == null) lightingController = UnityEngine.Object.FindFirstObjectByType<RoomLightingController>();
            if (butcher == null) butcher = UnityEngine.Object.FindFirstObjectByType<ButcherPlaceholder>();
            if (typewriter == null) typewriter = UnityEngine.Object.FindFirstObjectByType<TypewriterMachine>();
        }

        private void Start()
        {
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                Debug.Log("[GameManager] LAN Multiplayer oturumu aktif - Yerel GameManager devre dışı bırakılıyor.");
                enabled = false;
                return;
            }

            ApplySessionSettings();
            InitializeGame();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Ana Menü'de seçilen zorluk (tuzak sayısı) ve bot/insan yapılandırmasını uygular.
        /// Asıl ScriptableObject asset'ini değiştirmemek için çalışma zamanı kopyası kullanılır.
        /// </summary>
        private void ApplySessionSettings()
        {
            settings = Instantiate(settings);
            settings.trapCount = GameSessionData.GetTrapCount();

            var ai = FingerMonster.AI.AIPlayerController.Instance;
            if (ai != null)
            {
                for (int i = 0; i < settings.playerCount; i++)
                {
                    ai.SetPlayerAsBot(i, GameSessionData.IsSlotBot(i));
                }
            }
        }

        /// <summary>
        /// Belirtilen oyuncu bilgisayar (bot) tarafından mı kontrol ediliyor?
        /// </summary>
        public bool IsBotPlayer(int playerIndex)
        {
            var ai = FingerMonster.AI.AIPlayerController.Instance;
            return ai != null && ai.IsBot(playerIndex);
        }

        /// <summary>
        /// Oyunu başlatır, oyuncuları tanımlar ve ilk turu hazırlar.
        /// </summary>
        public void InitializeGame()
        {
            EnsureSettings();
            ChangeState(GameState.Lobby);

            // 4 Oyuncuyu yapılandır
            Players.Clear();
            for (int i = 0; i < settings.playerCount; i++)
            {
                Color pColor = (i < settings.playerColors.Length) ? settings.playerColors[i] : Color.white;
                PlayerData p = new PlayerData(i, IsBotPlayer(i) ? $"Bot {i + 1}" : $"Oyuncu {i + 1}", pColor);
                Players.Add(p);

                if (i < playerSeats.Count && playerSeats[i] != null)
                {
                    playerSeats[i].SetupSeat(i, pColor);
                }
            }

            if (cameraController != null)
            {
                cameraController.ApplyFixedTransform();
            }

            if (turnController != null)
            {
                turnController.ResetTurn(0);
            }

            if (hudController != null)
            {
                hudController.SetupHUD(Players);
            }

            StartRound();
        }

        /// <summary>
        /// Yeni bir tur başlatır: Tuşları ve tuzakları yeniden dağıtır.
        /// </summary>
        public void StartRound()
        {
            ChangeState(GameState.RoundStart);
            PressedIndices.Clear();
            TrapIndices.Clear();

            // Daktiloyu sıfırla
            if (typewriter != null)
            {
                typewriter.ResetAllKeys();
                typewriter.SetupKeys(settings.alphabetLetters);
            }

            // Oyuncu kukla avatarlarını sıfırla
            for (int i = 0; i < playerSeats.Count; i++)
            {
                if (playerSeats[i] != null && playerSeats[i].Avatar != null)
                {
                    playerSeats[i].Avatar.ResetAvatar();
                }
            }

            // Rastgele 6 gizli tuzak seç
            List<int> allIndices = new List<int>();
            for (int i = 0; i < settings.totalKeys; i++) allIndices.Add(i);

            // Karıştır (Fisher-Yates)
            for (int i = 0; i < allIndices.Count; i++)
            {
                int r = UnityEngine.Random.Range(i, allIndices.Count);
                int tmp = allIndices[i];
                allIndices[i] = allIndices[r];
                allIndices[r] = tmp;
            }

            int trapsToPlace = Mathf.Min(settings.trapCount, settings.totalKeys - 1);
            for (int i = 0; i < trapsToPlace; i++)
            {
                TrapIndices.Add(allIndices[i]);
            }

            Debug.Log($"[GameManager] Tur {CurrentRound} Başladı! Tuzak Tuş İndeksleri: {string.Join(", ", TrapIndices)}");

            if (hudController != null)
            {
                hudController.UpdateRoundInfo(CurrentRound, RemainingSafeKeysCount);
                hudController.ShowBanner($"TUR {CurrentRound} BAŞLADI!", Color.white, 1.8f);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBell();
            }

            StartCoroutine(DelayToFirstTurn());
        }

        private IEnumerator DelayToFirstTurn()
        {
            yield return new WaitForSeconds(settings.turnTransitionDelay);
            BeginPlayerTurn(turnController.ActivePlayerIndex);
        }

        /// <summary>
        /// Sıradaki oyuncunun turunu açar ve kamerayı onun açısına odaklar.
        /// </summary>
        private void BeginPlayerTurn(int playerIndex)
        {
            ChangeState(GameState.PlayerTurn);

            PlayerData activePlayer = Players[playerIndex];
            Debug.Log($"[GameManager] Sıra: {activePlayer.PlayerName}");

            // Koltuk ve ışık vurgusu
            for (int i = 0; i < playerSeats.Count; i++)
            {
                if (playerSeats[i] != null)
                {
                    playerSeats[i].SetHighlight(i == playerIndex);
                }
            }

            if (lightingController != null)
            {
                lightingController.SetTurnState(activePlayer.PlayerColor);
            }

            // Kamerayı oyuncunun koltuk açısına taşı
            if (cameraController != null && playerIndex < playerSeats.Count && playerSeats[playerIndex] != null)
            {
                bool snap = (CurrentRound == 1 && playerIndex == 0);
                cameraController.SetTarget(playerSeats[playerIndex].CameraMountPoint, snapImmediately: snap);
            }

            if (hudController != null)
            {
                hudController.UpdateTurn(activePlayer, Players);
            }

            OnTurnChanged?.Invoke(activePlayer);
        }

        /// <summary>
        /// Gelen ICommand eylemini doğrular ve yürütür.
        /// </summary>
        public bool ProcessCommand(ICommand command)
        {
            if (command == null) return false;

            if (command.Validate(this))
            {
                command.Execute(this);
                return true;
            }

            return false;
        }

        public bool IsKeyPressed(int keyIndex)
        {
            return PressedIndices.Contains(keyIndex);
        }

        /// <summary>
        /// PressKeyCommand tarafından tetiklenir: Tuşun güvenli mi yoksa tuzak mı olduğunu çözer.
        /// </summary>
        public void ResolveKey(int playerIndex, int keyIndex)
        {
            ChangeState(GameState.KeyResolve);
            PressedIndices.Add(keyIndex);

            bool isTrap = TrapIndices.Contains(keyIndex);
            PlayerData player = Players[playerIndex];

            // Daktilo tuş animasyonu
            if (typewriter != null)
            {
                typewriter.PressKey(keyIndex, isTrap);
            }

            if (hudController != null)
            {
                hudController.UpdateRoundInfo(CurrentRound, RemainingSafeKeysCount);
            }

            Vector3 keyWorldPos = (typewriter != null && typewriter.GetKey(keyIndex) != null) 
                ? typewriter.GetKey(keyIndex).transform.position 
                : Vector3.zero;

            if (!isTrap)
            {
                // GÜVENLİ TUŞ
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayTypewriterClick();
                }

                if (CartoonFXManager.Instance != null)
                {
                    CartoonFXManager.Instance.PlaySafeKeyFX(keyWorldPos);
                }

                player.Score += settings.pointsPerSafeKey;
                Debug.Log($"[GameManager] {player.PlayerName} güvenli tuşa bastı! Skor: {player.Score}");

                if (hudController != null)
                {
                    hudController.RefreshAllCards(Players, playerIndex);
                    hudController.ShowBanner($"{player.PlayerName} harfi yazdı! (+{settings.pointsPerSafeKey} Puan)", Color.green, 1.0f);
                }

                StartCoroutine(PostKeyRoutine(playerIndex, isTrap: false));
            }
            else
            {
                // TUZAK TUŞ!
                ChangeState(GameState.HandLost);

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayTrapTriggered();
                }

                PlayerHand lostHand = player.SeverRandomHand();
                string handName = (lostHand == PlayerHand.Left) ? "SOL" : "SAĞ";
                Debug.Log($"[GameManager] TUZAK! {player.PlayerName} {handName} elini kaybetti!");

                // Çizgi film patlama ve el fırlama animasyonu
                if (CartoonFXManager.Instance != null)
                {
                    CartoonFXManager.Instance.PlayTrapSnapFX(keyWorldPos, player.PlayerColor);
                }

                if (playerIndex < playerSeats.Count && playerSeats[playerIndex] != null && playerSeats[playerIndex].Avatar != null)
                {
                    playerSeats[playerIndex].Avatar.SeverHand(lostHand);
                }

                if (hudController != null)
                {
                    hudController.RefreshAllCards(Players, playerIndex);
                    hudController.ShowBanner($"TUZAK! {player.PlayerName} {handName} elini kaybetti!", Color.red, 1.8f);
                }

                StartCoroutine(PostKeyRoutine(playerIndex, isTrap: true));
            }
        }

        private IEnumerator PostKeyRoutine(int playerIndex, bool isTrap)
        {
            yield return new WaitForSeconds(settings.keyResolveDelay);

            PlayerData player = Players[playerIndex];

            if (isTrap && !player.IsAlive)
            {
                // Oyuncunun iki eli de gitti -> Kasap Sekansı!
                yield return StartCoroutine(HandleButcherSequence(player));
            }

            // Tur ve oyun sonu kontrolleri
            CheckGameProgress();
        }

        /// <summary>
        /// İki eli de kopan oyuncunun başında önlüklü kasabın belirdiği sekans.
        /// </summary>
        private IEnumerator HandleButcherSequence(PlayerData eliminatedPlayer)
        {
            ChangeState(GameState.ButcherSequence);
            Debug.Log($"[GameManager] KASAP SAHNESİ: {eliminatedPlayer.PlayerName} için kasap geldi!");

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayButcherStinger();
            }

            if (lightingController != null)
            {
                lightingController.SetButcherState();
            }

            // Kasabı oyuncunun koltuğu arkasına yerleştir
            if (butcher != null && eliminatedPlayer.PlayerId < playerSeats.Count)
            {
                Transform spawnAnchor = playerSeats[eliminatedPlayer.PlayerId].ButcherSpawnPoint;
                int weaponIdx = UnityEngine.Random.Range(0, 4);
                butcher.AppearAt(spawnAnchor, eliminatedPlayer.PlayerId, weaponIdx, () =>
                {
                    if (playerSeats[eliminatedPlayer.PlayerId].Avatar != null)
                    {
                        playerSeats[eliminatedPlayer.PlayerId].Avatar.PlayEliminatedByWeapon(weaponIdx);
                    }
                });
            }

            if (hudController != null)
            {
                hudController.ShowBanner($"KASAP GELDİ!\n{eliminatedPlayer.PlayerName} ELENDİ!", new Color(1f, 0.2f, 0.2f), settings.butcherSequenceDuration);
            }

            yield return new WaitForSeconds(settings.butcherSequenceDuration);

            if (butcher != null)
            {
                butcher.Hide();
            }

            if (lightingController != null)
            {
                lightingController.SetNormalState();
            }
        }

        /// <summary>
        /// Turun veya oyunun bitip bitmediğini denetler, bitmediyse sırayı sonraki oyuncuya aktarır.
        /// </summary>
        private void CheckGameProgress()
        {
            int aliveCount = turnController.GetAlivePlayersCount(Players);

            // Tek kişi kaldıysa oyun biter
            if (aliveCount <= 1)
            {
                EndGame();
                return;
            }

            // Kalan güvenli tuş kalmadıysa veya tüm tuzaklar tetiklendiyse tur biter
            if (RemainingSafeKeysCount <= 0 || PressedIndices.Count >= settings.totalKeys)
            {
                StartCoroutine(RoundEndRoutine());
                return;
            }

            // Sıradaki hayatta olan oyuncuya geç
            int nextPlayer = turnController.AdvanceToNextAlivePlayer(Players);
            if (nextPlayer != -1)
            {
                StartCoroutine(TransitionToNextTurn(nextPlayer));
            }
            else
            {
                EndGame();
            }
        }

        private IEnumerator TransitionToNextTurn(int nextPlayer)
        {
            yield return new WaitForSeconds(settings.turnTransitionDelay);
            BeginPlayerTurn(nextPlayer);
        }

        private IEnumerator RoundEndRoutine()
        {
            ChangeState(GameState.RoundEnd);
            Debug.Log("[GameManager] Tur sona erdi!");

            if (hudController != null)
            {
                hudController.ShowBanner("TUR TAMAMLANDI!\nYeni tura hazırlanılıyor...", Color.cyan, 2.5f);
            }

            yield return new WaitForSeconds(2.5f);

            CurrentRound++;
            StartRound();
        }

        /// <summary>
        /// Oyunu sonlandırır, kazananı belirler ve GameOver ekranını açar.
        /// </summary>
        public void EndGame()
        {
            ChangeState(GameState.GameOver);
            Debug.Log("[GameManager] Oyun Bitti!");

            PlayerData winner = null;
            int aliveCount = turnController.GetAlivePlayersCount(Players);

            if (aliveCount == 1)
            {
                winner = turnController.GetSoleSurvivor(Players);
            }
            else
            {
                // Birden fazla hayatta kalan varsa veya herkes elendiyse puan (skor) belirler
                winner = turnController.GetHighestScorePlayer(Players);
            }

            if (gameOverController == null)
            {
                gameOverController = UnityEngine.Object.FindFirstObjectByType<GameOverUIController>(FindObjectsInactive.Include);
            }

            if (gameOverController != null)
            {
                gameOverController.ShowGameOver(winner, Players);
            }
            else
            {
                Debug.LogWarning("[GameManager] GameOverUIController referansı bulunamadı!");
            }
        }

        public void TogglePause()
        {
            if (pauseMenuController != null)
            {
                pauseMenuController.TogglePause();
            }
        }

        public void ForceSetState(GameState newState)
        {
            ChangeState(newState);
        }

        private void ChangeState(GameState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }
    }
}

