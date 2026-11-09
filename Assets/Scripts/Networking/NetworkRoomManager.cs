using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using FingerMonster.AI;

namespace FingerMonster.Networking
{
    /// <summary>
    /// LAN Multiplayer oda yönetimi, bağlantı kurma (Host/Client), lobi durumu ve koltuk tahsisini yönetir.
    /// </summary>
    public class NetworkRoomManager : MonoBehaviour
    {
        public static NetworkRoomManager Instance { get; private set; }

        [Header("Ağ Portu")]
        [SerializeField] private ushort defaultPort = 7777;

        [Header("Lobi Ayarları")]
        [SerializeField] private bool fillEmptySeatsWithBots = true;
        public bool FillEmptySeatsWithBots
        {
            get => fillEmptySeatsWithBots;
            set => fillEmptySeatsWithBots = value;
        }

        public string LocalPlayerName { get; set; } = "Oyuncu";
        public string CurrentRoomName { get; private set; } = "FingerMonster LAN";
        public List<NetworkPlayerSlot> PendingSlots { get; private set; }
        public int PendingTrapCount { get; private set; } = 6;

        public event Action OnJoinedLobby;
        public event Action OnLeftLobby;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Host olarak LAN oyunu kurar.
        /// </summary>
        public bool StartHost(string roomName, ushort port = 0)
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[NetworkRoomManager] NetworkManager sahnede bulunamadı!");
                return false;
            }

            ushort targetPort = (port == 0) ? defaultPort : port;
            CurrentRoomName = string.IsNullOrEmpty(roomName) ? "FingerMonster Oda" : roomName;

            SetTransportConnection("0.0.0.0", targetPort);

            bool success = NetworkManager.Singleton.StartHost();
            if (success)
            {
                Debug.Log($"[NetworkRoomManager] Host başlatıldı! Port: {targetPort}");

                // LAN Broadcast başlat
                if (LANDiscovery.Instance != null)
                {
                    LANDiscovery.Instance.StartBroadcasting(CurrentRoomName, targetPort, 1, 4);
                }

                OnJoinedLobby?.Invoke();
            }
            return success;
        }

        /// <summary>
        /// İstemci olarak belirtilen IP ve Port'a bağlanır.
        /// </summary>
        public bool StartClient(string ipAddress, ushort port = 0)
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[NetworkRoomManager] NetworkManager sahnede bulunamadı!");
                return false;
            }

            ushort targetPort = (port == 0) ? defaultPort : port;
            string targetIp = string.IsNullOrEmpty(ipAddress) ? "127.0.0.1" : ipAddress;

            SetTransportConnection(targetIp, targetPort);

            bool success = NetworkManager.Singleton.StartClient();
            if (success)
            {
                Debug.Log($"[NetworkRoomManager] İstemci olarak bağlanılıyor: {targetIp}:{targetPort}");
                OnJoinedLobby?.Invoke();
            }
            return success;
        }

        /// <summary>
        /// Bağlantıyı keser, yayını durdurur ve lobi ekranına döner.
        /// </summary>
        public void Disconnect()
        {
            if (LANDiscovery.Instance != null)
            {
                LANDiscovery.Instance.StopDiscovery();
            }

            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                NetworkManager.Singleton.Shutdown();
            }

            OnLeftLobby?.Invoke();
        }

        private void SetTransportConnection(string ip, ushort port)
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.ConnectionData.Address = ip;
                transport.ConnectionData.Port = port;
                transport.ConnectionData.ServerListenAddress = "0.0.0.0";
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                int count = NetworkManager.Singleton.ConnectedClientsList.Count;
                if (LANDiscovery.Instance != null)
                {
                    LANDiscovery.Instance.UpdatePlayerCount(count);
                }
                Debug.Log($"[NetworkRoomManager] Yeni oyuncu bağlandı! ClientId:{clientId}, Toplam:{count}");
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                int count = NetworkManager.Singleton.ConnectedClientsList.Count;
                if (LANDiscovery.Instance != null)
                {
                    LANDiscovery.Instance.UpdatePlayerCount(count);
                }
            }

            // Eğer ben sunucudan koptuysam
            if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Host tarafından oyunu başlatır. Boş koltuklar ayara göre botlarla doldurulur.
        /// </summary>
        public void StartGameMatch(int trapCount = 6)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            List<NetworkPlayerSlot> slots = new List<NetworkPlayerSlot>();
            var connectedClients = NetworkManager.Singleton.ConnectedClientsList;

            // 1. Bağlı gerçek oyuncuları koltuklara ata
            for (int i = 0; i < connectedClients.Count && i < 4; i++)
            {
                ulong cId = connectedClients[i].ClientId;
                string pName = (cId == NetworkManager.Singleton.LocalClientId) ? LocalPlayerName : $"Oyuncu {i + 1}";
                Color pColor = GetSeatColor(i);

                slots.Add(new NetworkPlayerSlot(cId, i, pName, isBot: false, pColor));
            }

            // 2. Boş koltukları Botlarla doldur (Eğer açık ise)
            if (fillEmptySeatsWithBots)
            {
                for (int i = slots.Count; i < 4; i++)
                {
                    BotProfile botProf = BotProfile.GenerateRandom(i);
                    slots.Add(new NetworkPlayerSlot(ulong.MaxValue - (ulong)i, i, botProf.BotName, isBot: true, GetSeatColor(i)));
                }
            }

            PendingSlots = slots;
            PendingTrapCount = trapCount;

            // 3. NetworkGameManager'ı başlat
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.ServerInitializeMatch(slots, trapCount);
            }
            else
            {
                // Sahne geçişi ile Game sahnesine yükleme
                NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
            }
        }

        private Color GetSeatColor(int seat)
        {
            switch (seat)
            {
                case 0: return new Color(0.9f, 0.25f, 0.25f);
                case 1: return new Color(0.25f, 0.55f, 0.95f);
                case 2: return new Color(0.25f, 0.85f, 0.35f);
                case 3: return new Color(0.95f, 0.75f, 0.2f);
                default: return Color.white;
            }
        }
    }
}
