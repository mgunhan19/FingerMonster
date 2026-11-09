using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using FingerMonster.Core;
using FingerMonster.Gameplay;
using FingerMonster.Player;
using FingerMonster.UI;
using FingerMonster.Audio;
using FingerMonster.AI;

namespace FingerMonster.Networking
{
    /// <summary>
    /// FingerMonster LAN Multiplayer için yetkili sunucu (Server-Authoritative) oyun yöneticisi.
    /// Tuzakların yeri YALNIZCA sunucuda tutulur ve istemcilere asla sızdırılmaz.
    /// Tüm oyuncu eylemleri ServerRpc ile doğrulanır ve ClientRpc ile senkronize edilir.
    /// Bağlantısı kopan oyuncunun yerini yapay zekâ botu devralır.
    /// </summary>
    public class NetworkGameManager : NetworkBehaviour
    {
        public static NetworkGameManager Instance { get; private set; }

        [Header("Ağ Senkronize Değişkenleri")]
        public NetworkVariable<int> ActivePlayerIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> CurrentRound = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> RemainingSafeKeys = new NetworkVariable<int>(14, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<GameState> CurrentNetworkState = new NetworkVariable<GameState>(GameState.Lobby, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkList<NetworkPlayerSlot> PlayerSlots;

        [Header("Sunucu Özel Verileri (Hile Korumalı)")]
        private readonly HashSet<int> serverSecretTraps = new HashSet<int>();
        private readonly HashSet<int> serverPressedKeys = new HashSet<int>();
        private readonly Dictionary<ulong, int> clientToSeatMap = new Dictionary<ulong, int>();

        [Header("Yerel Sahne Referansları")]
        [SerializeField] private TypewriterMachine typewriter;
        [SerializeField] private HUDController hudController;
        [SerializeField] private GameOverUIController gameOverController;
        [SerializeField] private RoomLightingController lightingController;
        [SerializeField] private ButcherPlaceholder butcher;
        [SerializeField] private List<PlayerSeat> playerSeats = new List<PlayerSeat>();
        [SerializeField] private AIBrain aiBrain;

        public int LocalSeatIndex { get; private set; } = 0;
        public bool IsLocalPlayerTurn => ActivePlayerIndex.Value == LocalSeatIndex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            PlayerSlots = new NetworkList<NetworkPlayerSlot>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

            EnsureLocalReferences();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            PlayerSlots.OnListChanged += HandlePlayerSlotsChanged;
            ActivePlayerIndex.OnValueChanged += HandleActivePlayerChanged;
            CurrentRound.OnValueChanged += (oldVal, newVal) => UpdateHUDInfo();
            RemainingSafeKeys.OnValueChanged += (oldVal, newVal) => UpdateHUDInfo();

            if (IsServer)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleServerClientDisconnect;
                if (PlayerSlots.Count == 0)
                {
                    if (NetworkRoomManager.Instance != null && NetworkRoomManager.Instance.PendingSlots != null && NetworkRoomManager.Instance.PendingSlots.Count > 0)
                    {
                        ServerInitializeMatch(NetworkRoomManager.Instance.PendingSlots, NetworkRoomManager.Instance.PendingTrapCount);
                    }
                    else
                    {
                        List<NetworkPlayerSlot> defaultSlots = new List<NetworkPlayerSlot>();
                        defaultSlots.Add(new NetworkPlayerSlot(NetworkManager.Singleton.LocalClientId, 0, "Host (Sen)", false, new Color(0.9f, 0.25f, 0.25f)));
                        Color[] c = new Color[] { new Color(0.9f, 0.25f, 0.25f), new Color(0.25f, 0.55f, 0.95f), new Color(0.25f, 0.85f, 0.35f), new Color(0.95f, 0.75f, 0.2f) };
                        for (int i = 1; i < 4; i++)
                        {
                            BotProfile b = BotProfile.GenerateRandom(i);
                            defaultSlots.Add(new NetworkPlayerSlot(ulong.MaxValue - (ulong)i, i, b.BotName, true, c[i]));
                        }
                        ServerInitializeMatch(defaultSlots, 6);
                    }
                }
            }

            DetermineLocalSeat();
            ConfigureLocalCameraForSeat(LocalSeatIndex);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleServerClientDisconnect;
            }
            base.OnNetworkDespawn();
        }

        private void EnsureLocalReferences()
        {
            if (typewriter == null) typewriter = UnityEngine.Object.FindFirstObjectByType<TypewriterMachine>();
            if (hudController == null) hudController = UnityEngine.Object.FindFirstObjectByType<HUDController>(FindObjectsInactive.Include);
            if (gameOverController == null) gameOverController = UnityEngine.Object.FindFirstObjectByType<GameOverUIController>(FindObjectsInactive.Include);
            if (lightingController == null) lightingController = UnityEngine.Object.FindFirstObjectByType<RoomLightingController>();
            if (butcher == null) butcher = UnityEngine.Object.FindFirstObjectByType<ButcherPlaceholder>();
            if (aiBrain == null) aiBrain = GetComponent<AIBrain>() ?? gameObject.AddComponent<AIBrain>();

            if (playerSeats == null || playerSeats.Count == 0)
            {
                playerSeats = new List<PlayerSeat>(UnityEngine.Object.FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None));
                playerSeats.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
            }
        }

        /// <summary>
        /// İstemcinin kendi koltuğunu belirler ve kamerayı o koltuğun açısına kilitler.
        /// </summary>
        public void DetermineLocalSeat()
        {
            ulong myClientId = NetworkManager.Singleton.LocalClientId;
            LocalSeatIndex = 0;

            for (int i = 0; i < PlayerSlots.Count; i++)
            {
                if (PlayerSlots[i].ClientId == myClientId && !PlayerSlots[i].IsBot)
                {
                    LocalSeatIndex = PlayerSlots[i].SeatIndex;
                    break;
                }
            }

            ConfigureLocalCameraForSeat(LocalSeatIndex);
        }

        /// <summary>
        /// Her oyuncu kendi bilgisayarında masaya KENDİ koltuğundan bakar.
        /// </summary>
        public void ConfigureLocalCameraForSeat(int seatIndex)
        {
            PlayerCameraController cam = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>();
            if (cam == null) return;

            // Masadaki 4 koltuğun dünya çapa açıları
            Vector3[] camPositions = new Vector3[]
            {
                new Vector3(0f, 2.28f, -2.31f),    // Koltuk 1 (Güney - Varsayılan)
                new Vector3(0f, 2.28f, 2.31f),     // Koltuk 2 (Kuzey)
                new Vector3(-2.31f, 2.28f, 0f),    // Koltuk 3 (Batı)
                new Vector3(2.31f, 2.28f, 0f)      // Koltuk 4 (Doğu)
            };

            Vector3[] camAngles = new Vector3[]
            {
                new Vector3(27.956f, 0f, 0f),      // P1: Daktiloya Güneyden bakar
                new Vector3(27.956f, 180f, 0f),    // P2: Daktiloya Kuzeyden bakar
                new Vector3(27.956f, 90f, 0f),     // P3: Daktiloya Batıdan bakar
                new Vector3(27.956f, -90f, 0f)     // P4: Daktiloya Doğudan bakar
            };

            if (seatIndex >= 0 && seatIndex < camPositions.Length)
            {
                cam.transform.position = camPositions[seatIndex];
                cam.transform.rotation = Quaternion.Euler(camAngles[seatIndex]);
            }
        }

        #region SERVER TARAFI OYUN AKIŞI
        /// <summary>
        /// Sunucu tarafından 4 kişilik odayı başlatır (İnsanlar + Boş koltuklara atanan Botlar).
        /// </summary>
        public void ServerInitializeMatch(List<NetworkPlayerSlot> initialSlots, int trapCount = 6)
        {
            if (!IsServer) return;

            PlayerSlots.Clear();
            clientToSeatMap.Clear();

            for (int i = 0; i < initialSlots.Count; i++)
            {
                PlayerSlots.Add(initialSlots[i]);
                if (!initialSlots[i].IsBot)
                {
                    clientToSeatMap[initialSlots[i].ClientId] = initialSlots[i].SeatIndex;
                }
            }

            ServerStartNewRound(trapCount);
        }

        public void ServerStartNewRound(int trapCount = 6)
        {
            if (!IsServer) return;

            serverSecretTraps.Clear();
            serverPressedKeys.Clear();

            // 20 tuştan rastgele 6 tuzak seç (Yalnızca sunucu hafızasında kalır!)
            List<int> keysPool = new List<int>();
            for (int i = 0; i < 20; i++) keysPool.Add(i);

            for (int i = 0; i < keysPool.Count; i++)
            {
                int r = UnityEngine.Random.Range(i, keysPool.Count);
                int tmp = keysPool[i];
                keysPool[i] = keysPool[r];
                keysPool[r] = tmp;
            }

            int trapsToPlace = Mathf.Clamp(trapCount, 1, 19);
            for (int i = 0; i < trapsToPlace; i++)
            {
                serverSecretTraps.Add(keysPool[i]);
            }

            RemainingSafeKeys.Value = 20 - serverSecretTraps.Count;
            ActivePlayerIndex.Value = 0;
            CurrentNetworkState.Value = GameState.RoundStart;

            Debug.Log($"[NetworkGameManager-SERVER] Tur {CurrentRound.Value} Başladı! Gizli Tuzaklar: {string.Join(",", serverSecretTraps)}");

            OnRoundStartedClientRpc(CurrentRound.Value, RemainingSafeKeys.Value);
            StartCoroutine(ServerDelayToFirstTurn());
        }

        private IEnumerator ServerDelayToFirstTurn()
        {
            yield return new WaitForSeconds(1.5f);
            ServerBeginTurn(0);
        }

        private void ServerBeginTurn(int seatIndex)
        {
            if (!IsServer) return;

            ActivePlayerIndex.Value = seatIndex;
            CurrentNetworkState.Value = GameState.PlayerTurn;

            OnTurnStartedClientRpc(seatIndex);

            // Eğer bu koltuk bir BOT ise, sunucu AIBrain'i devreye sokar
            if (seatIndex < PlayerSlots.Count && PlayerSlots[seatIndex].IsBot && PlayerSlots[seatIndex].IsAlive)
            {
                StartCoroutine(ServerTriggerBotTurn(seatIndex));
            }
        }

        private IEnumerator ServerTriggerBotTurn(int seatIndex)
        {
            yield return new WaitForSeconds(0.4f);

            List<int> available = new List<int>();
            for (int i = 0; i < 20; i++)
            {
                if (!serverPressedKeys.Contains(i)) available.Add(i);
            }

            if (available.Count == 0) yield break;

            // Bot adil karar verir (Tuzakları bilmez!)
            BotProfile profile = aiBrain != null ? aiBrain.GetProfile(seatIndex) : BotProfile.GenerateRandom(seatIndex);
            float thinkTime = UnityEngine.Random.Range(1.2f, 2.5f);
            yield return new WaitForSeconds(thinkTime);

            int chosenKey = available[UnityEngine.Random.Range(0, available.Count)];

            // Bot imleç simülasyonunu tüm istemcilere bildir
            OnBotAimingClientRpc(seatIndex, chosenKey);
            yield return new WaitForSeconds(0.45f);

            if (CurrentNetworkState.Value != GameState.PlayerTurn || ActivePlayerIndex.Value != seatIndex)
            {
                yield break;
            }

            ServerExecuteKeyPress(seatIndex, chosenKey);
        }
        #endregion

        #region RPC İLETİŞİMİ
        /// <summary>
        /// İstemci tuşa bastığında sunucuya komut gönderir (Hile kontrolü yapılır).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestPressKeyServerRpc(int seatIndex, int keyIndex, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;

            // Doğrulama 1: Oyun sırası bu oyuncuda mı?
            if (CurrentNetworkState.Value != GameState.PlayerTurn || ActivePlayerIndex.Value != seatIndex)
            {
                Debug.LogWarning($"[SERVER] Geçersiz sıra! Sıra:{ActivePlayerIndex.Value}, İsteyen:{seatIndex}");
                return;
            }

            // Doğrulama 2: Bu istemci bu koltuğun gerçek sahibi mi?
            if (clientToSeatMap.TryGetValue(senderClientId, out int assignedSeat))
            {
                if (assignedSeat != seatIndex)
                {
                    Debug.LogWarning($"[SERVER] Yetkisiz hamle! Client:{senderClientId} Seat:{seatIndex}'e basamaz!");
                    return;
                }
            }

            // Doğrulama 3: Tuş daha önce basılmış mı?
            if (serverPressedKeys.Contains(keyIndex))
            {
                Debug.LogWarning($"[SERVER] Tuş {keyIndex} zaten basılmış!");
                return;
            }

            ServerExecuteKeyPress(seatIndex, keyIndex);
        }

        private void ServerExecuteKeyPress(int seatIndex, int keyIndex)
        {
            serverPressedKeys.Add(keyIndex);
            CurrentNetworkState.Value = GameState.KeyResolve;

            bool isTrap = serverSecretTraps.Contains(keyIndex);
            NetworkPlayerSlot slot = PlayerSlots[seatIndex];
            PlayerHand lostHand = PlayerHand.None;

            if (isTrap)
            {
                // Rastgele bir elini kaybet
                lostHand = (UnityEngine.Random.value > 0.5f) ? PlayerHand.Left : PlayerHand.Right;
                slot.RemainingHands = Mathf.Max(0, slot.RemainingHands - 1);
                if (slot.RemainingHands == 0)
                {
                    slot.IsAlive = false;
                }
            }
            else
            {
                slot.Score += 10;
                RemainingSafeKeys.Value = Mathf.Max(0, RemainingSafeKeys.Value - 1);
            }

            PlayerSlots[seatIndex] = slot;

            // Sonucu tüm istemcilere yay
            OnKeyResolvedClientRpc(seatIndex, keyIndex, isTrap, lostHand, slot.Score, slot.RemainingHands, slot.IsAlive);

            StartCoroutine(ServerPostKeySequence(seatIndex, isTrap, slot.IsAlive));
        }

        private IEnumerator ServerPostKeySequence(int seatIndex, bool isTrap, bool isAlive)
        {
            yield return new WaitForSeconds(1.2f);

            // Eğer oyuncu iki elini de kaybettiyse Kasap Sekansı
            if (isTrap && !isAlive)
            {
                CurrentNetworkState.Value = GameState.ButcherSequence;
                int weaponIdx = UnityEngine.Random.Range(0, 4);
                OnButcherSequenceClientRpc(seatIndex, weaponIdx);
                yield return new WaitForSeconds(3.0f);
            }

            // Tur veya Oyun Bitiş Kontrolü
            int aliveCount = 0;
            int soleSurvivor = -1;
            for (int i = 0; i < PlayerSlots.Count; i++)
            {
                if (PlayerSlots[i].IsAlive)
                {
                    aliveCount++;
                    soleSurvivor = i;
                }
            }

            // Oyun bitti: Tek kişi kaldı
            if (aliveCount <= 1)
            {
                ServerEndGame(soleSurvivor);
                yield break;
            }

            // Tur bitti: Güvenli tuş kalmadı
            if (RemainingSafeKeys.Value <= 0 || serverPressedKeys.Count >= 20)
            {
                CurrentNetworkState.Value = GameState.RoundEnd;
                OnRoundEndedClientRpc();
                yield return new WaitForSeconds(2.5f);
                CurrentRound.Value++;
                ServerStartNewRound();
                yield break;
            }

            // Sıradaki hayatta olan oyuncuya geç
            int next = (seatIndex + 1) % PlayerSlots.Count;
            while (!PlayerSlots[next].IsAlive)
            {
                next = (next + 1) % PlayerSlots.Count;
            }

            ServerBeginTurn(next);
        }

        private void ServerEndGame(int winnerSeatIndex)
        {
            CurrentNetworkState.Value = GameState.GameOver;

            string winnerName = "Herkes Elendi (Berabere)";
            Color winnerCol = Color.gray;
            int winnerScore = 0;

            if (winnerSeatIndex >= 0 && winnerSeatIndex < PlayerSlots.Count)
            {
                winnerName = PlayerSlots[winnerSeatIndex].PlayerName.ToString();
                winnerCol = PlayerSlots[winnerSeatIndex].AvatarColor;
                winnerScore = PlayerSlots[winnerSeatIndex].Score;
            }

            string recap = "--- LAN OYUN SKOR TABLOSU ---\n";
            for (int i = 0; i < PlayerSlots.Count; i++)
            {
                var s = PlayerSlots[i];
                string st = s.IsAlive ? $"({s.RemainingHands} El)" : "(Kasap Tarafından Elendi)";
                recap += $"{s.PlayerName}: {s.Score} Puan {st}\n";
            }

            OnGameOverClientRpc(winnerName, winnerCol, winnerScore, recap);
        }

        private void HandleServerClientDisconnect(ulong clientId)
        {
            if (!IsServer) return;

            if (clientToSeatMap.TryGetValue(clientId, out int seatIndex))
            {
                clientToSeatMap.Remove(clientId);

                // Boşalan koltuğu anında Yapay Zekâ Botu devralsın
                if (seatIndex < PlayerSlots.Count)
                {
                    NetworkPlayerSlot slot = PlayerSlots[seatIndex];
                    slot.IsBot = true;
                    slot.PlayerName = new FixedString64Bytes($"Bot {seatIndex + 1} (Devraldı)");
                    PlayerSlots[seatIndex] = slot;

                    Debug.Log($"[SERVER] Client {clientId} ayrıldı! Koltuk {seatIndex + 1} bot tarafından devralındı.");
                    OnSeatTakenOverClientRpc(seatIndex, slot.PlayerName.ToString());

                    // Sıra bu oyuncudaydı ise bot hamlesini tetikle
                    if (ActivePlayerIndex.Value == seatIndex && CurrentNetworkState.Value == GameState.PlayerTurn)
                    {
                        StartCoroutine(ServerTriggerBotTurn(seatIndex));
                    }
                }
            }
        }
        #endregion

        #region CLIENT RPC METOTLARI (Tüm Oyuncular Görür)
        [ClientRpc]
        private void OnRoundStartedClientRpc(int round, int safeKeys)
        {
            if (typewriter != null) typewriter.ResetAllKeys();
            if (hudController != null)
            {
                hudController.UpdateRoundInfo(round, safeKeys);
                hudController.ShowBanner($"TUR {round} BAŞLADI!", Color.white, 2.0f);
            }
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBell();
        }

        [ClientRpc]
        private void OnTurnStartedClientRpc(int seatIndex)
        {
            for (int i = 0; i < playerSeats.Count; i++)
            {
                if (playerSeats[i] != null) playerSeats[i].SetHighlight(i == seatIndex);
            }

            if (lightingController != null && seatIndex < PlayerSlots.Count)
            {
                lightingController.SetTurnState(PlayerSlots[seatIndex].AvatarColor);
            }

            if (hudController != null && seatIndex < PlayerSlots.Count)
            {
                string pName = PlayerSlots[seatIndex].PlayerName.ToString();
                Color col = PlayerSlots[seatIndex].AvatarColor;
                hudController.ShowBanner($"Sıra: <color=#{ColorUtility.ToHtmlStringRGB(col)}>{pName}</color>", col, 1.2f);
            }
        }

        [ClientRpc]
        private void OnBotAimingClientRpc(int botSeatIndex, int keyIndex)
        {
            if (typewriter != null)
            {
                TypewriterKey key = typewriter.GetKey(keyIndex);
                if (key != null)
                {
                    StartCoroutine(FlashKeyAimRoutine(key));
                }
            }
        }

        private IEnumerator FlashKeyAimRoutine(TypewriterKey key)
        {
            key.SetHovered(true);
            yield return new WaitForSeconds(0.4f);
            key.SetHovered(false);
        }

        [ClientRpc]
        private void OnKeyResolvedClientRpc(int seatIndex, int keyIndex, bool isTrap, PlayerHand lostHand, int score, int handsRemaining, bool isAlive)
        {
            // 1. Daktilo tuşuna bas
            if (typewriter != null)
            {
                typewriter.PressKey(keyIndex, isTrap);
            }

            // 2. Ses ve Efekt
            if (isTrap)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayTrapTriggered();

                Vector3 keyPos = (typewriter != null && typewriter.GetKey(keyIndex) != null) ?
                                  typewriter.GetKey(keyIndex).transform.position : Vector3.zero;

                Color pCol = (seatIndex < PlayerSlots.Count) ? (Color)PlayerSlots[seatIndex].AvatarColor : Color.red;

                if (CartoonFXManager.Instance != null)
                {
                    CartoonFXManager.Instance.PlayTrapSnapFX(keyPos, pCol);
                }

                // El kopma animasyonu
                if (seatIndex < playerSeats.Count && playerSeats[seatIndex] != null && playerSeats[seatIndex].Avatar != null)
                {
                    playerSeats[seatIndex].Avatar.SeverHand(lostHand);
                }
            }
            else
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayTypewriterClick();

                Vector3 keyPos = (typewriter != null && typewriter.GetKey(keyIndex) != null) ?
                                  typewriter.GetKey(keyIndex).transform.position : Vector3.zero;

                if (CartoonFXManager.Instance != null)
                {
                    CartoonFXManager.Instance.PlaySafeKeyFX(keyPos);
                }
            }

            UpdateHUDCards();
        }

        [ClientRpc]
        private void OnButcherSequenceClientRpc(int seatIndex, int weaponIdx)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButcherStinger();
            if (lightingController != null) lightingController.SetButcherState();

            if (butcher != null && seatIndex < playerSeats.Count && playerSeats[seatIndex] != null)
            {
                Transform spawn = playerSeats[seatIndex].ButcherSpawnPoint;
                butcher.AppearAt(spawn, seatIndex, weaponIdx, () =>
                {
                    if (playerSeats[seatIndex].Avatar != null)
                    {
                        playerSeats[seatIndex].Avatar.PlayEliminatedByWeapon(weaponIdx);
                    }
                });
            }

            if (hudController != null && seatIndex < PlayerSlots.Count)
            {
                string name = PlayerSlots[seatIndex].PlayerName.ToString();
                hudController.ShowBanner($"KASAP GELDİ!\n{name} ELENDİ!", Color.red, 2.5f);
            }

            StartCoroutine(RestoreLightingAfterDelay(2.5f));
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestEmoteServerRpc(int seatIndex, int emoteIndex)
        {
            if (!IsServer) return;
            OnEmoteExecutedClientRpc(seatIndex, emoteIndex);
        }

        [ClientRpc]
        private void OnEmoteExecutedClientRpc(int seatIndex, int emoteIndex)
        {
            if (seatIndex >= 0 && seatIndex < playerSeats.Count && playerSeats[seatIndex] != null)
            {
                var avatar = playerSeats[seatIndex].Avatar;
                if (avatar != null)
                {
                    avatar.TriggerEmote((EmoteType)emoteIndex);
                }
            }
        }

        private IEnumerator RestoreLightingAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (butcher != null) butcher.Hide();
            if (lightingController != null) lightingController.SetNormalState();
        }

        [ClientRpc]
        private void OnRoundEndedClientRpc()
        {
            if (hudController != null)
            {
                hudController.ShowBanner("TUR TAMAMLANDI!\nYeni tura geçiliyor...", Color.cyan, 2.5f);
            }
        }

        [ClientRpc]
        private void OnGameOverClientRpc(string winnerName, Color winnerColor, int score, string scoreRecap)
        {
            if (gameOverController != null)
            {
                PlayerData dummyWinner = new PlayerData(0, winnerName, winnerColor) { Score = score };
                gameOverController.ShowGameOver(dummyWinner, null);
            }
        }

        [ClientRpc]
        private void OnSeatTakenOverClientRpc(int seatIndex, string newBotName)
        {
            if (hudController != null)
            {
                hudController.ShowBanner($"Oyuncu ayrıldı! Koltuk {seatIndex + 1} bot tarafından devralındı.", Color.yellow, 2.0f);
            }
            UpdateHUDCards();
        }
        #endregion

        private void HandlePlayerSlotsChanged(NetworkListEvent<NetworkPlayerSlot> changeEvent)
        {
            UpdateHUDCards();
        }

        private void HandleActivePlayerChanged(int oldVal, int newVal)
        {
            UpdateHUDInfo();
        }

        private void UpdateHUDInfo()
        {
            if (hudController != null)
            {
                hudController.UpdateRoundInfo(CurrentRound.Value, RemainingSafeKeys.Value);
            }
        }

        private void UpdateHUDCards()
        {
            if (hudController == null) return;

            List<PlayerData> dummyList = new List<PlayerData>();
            for (int i = 0; i < PlayerSlots.Count; i++)
            {
                var slot = PlayerSlots[i];
                PlayerData p = new PlayerData(slot.SeatIndex, slot.PlayerName.ToString(), slot.AvatarColor)
                {
                    Score = slot.Score
                };
                if (slot.RemainingHands == 1) p.SeverRandomHand();
                else if (slot.RemainingHands == 0)
                {
                    p.SeverRandomHand();
                    p.SeverRandomHand();
                }
                dummyList.Add(p);
            }

            hudController.RefreshAllCards(dummyList, ActivePlayerIndex.Value);
        }

        /// <summary>
        /// Yerel oyuncunun tıklama isteği gönderdiği arabirim.
        /// </summary>
        public void LocalRequestPressKey(int keyIndex)
        {
            if (!IsLocalPlayerTurn || CurrentNetworkState.Value != GameState.PlayerTurn)
            {
                return;
            }

            RequestPressKeyServerRpc(LocalSeatIndex, keyIndex);
        }
    }
}
