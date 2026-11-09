using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace FingerMonster.Networking
{
    /// <summary>
    /// Keşfedilen bir LAN oyun odasını temsil eden veri modeli.
    /// </summary>
    [Serializable]
    public class DiscoveredRoom
    {
        public string RoomName;
        public string IpAddress;
        public string IPAddress => IpAddress;
        public ushort Port;
        public int CurrentPlayers;
        public int PlayerCount => CurrentPlayers;
        public int MaxPlayers;
        public float LastSeenTime;

        public string DisplayString => $"{RoomName} ({CurrentPlayers}/{MaxPlayers}) - {IpAddress}:{Port}";
    }

    /// <summary>
    /// UDP Broadcast ile yerel ağdaki (LAN) odaları otomatik keşfeden ve
    /// Host durumunda periyodik oda sinyali (beacon) yayınlayan ağ bileşeni.
    /// </summary>
    public class LANDiscovery : MonoBehaviour
    {
        public static LANDiscovery Instance { get; private set; }

        [Header("Ağ Yapılandırması")]
        [SerializeField] private int discoveryPort = 47777;
        [SerializeField] private float broadcastInterval = 1.0f;
        [SerializeField] private float roomTimeout = 3.5f;

        private UdpClient broadcastSender;
        private UdpClient broadcastReceiver;
        private Thread receiveThread;
        private bool isBroadcasting = false;
        private bool isListening = false;

        private string currentRoomName = "FingerMonster Oda";
        private ushort currentGamePort = 7777;
        private int currentPlayers = 1;
        private int maxPlayers = 4;
        private float lastBroadcastTime = 0f;

        private readonly Dictionary<string, DiscoveredRoom> discoveredRooms = new Dictionary<string, DiscoveredRoom>();
        private readonly object lockObj = new object();

        public Dictionary<string, DiscoveredRoom> DiscoveredRooms
        {
            get
            {
                lock (lockObj)
                {
                    return new Dictionary<string, DiscoveredRoom>(discoveredRooms);
                }
            }
        }

        public event Action<List<DiscoveredRoom>> OnRoomsUpdated;

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

        private void Update()
        {
            // Host yayın döngüsü
            if (isBroadcasting && Time.time - lastBroadcastTime >= broadcastInterval)
            {
                lastBroadcastTime = Time.time;
                SendBeacon();
            }

            // Süresi dolan eski odaları temizle
            if (isListening)
            {
                bool changed = false;
                lock (lockObj)
                {
                    List<string> expiredKeys = new List<string>();
                    foreach (var kvp in discoveredRooms)
                    {
                        if (Time.time - kvp.Value.LastSeenTime > roomTimeout)
                        {
                            expiredKeys.Add(kvp.Key);
                        }
                    }

                    foreach (var key in expiredKeys)
                    {
                        discoveredRooms.Remove(key);
                        changed = true;
                    }
                }

                if (changed)
                {
                    NotifyRoomsUpdated();
                }
            }
        }

        private void OnDestroy()
        {
            StopDiscovery();
            if (Instance == this) Instance = null;
        }

        private void OnApplicationQuit()
        {
            StopDiscovery();
        }

        /// <summary>
        /// Host için LAN üzerinde periyodik oda yayını (beacon) başlatır.
        /// </summary>
        public void StartBroadcasting(string roomName, ushort gamePort, int currentPlayersCount, int maxPlayersCount = 4)
        {
            currentRoomName = roomName;
            currentGamePort = gamePort;
            currentPlayers = currentPlayersCount;
            maxPlayers = maxPlayersCount;

            if (isBroadcasting) return;

            try
            {
                broadcastSender = new UdpClient();
                broadcastSender.EnableBroadcast = true;
                isBroadcasting = true;
                SendBeacon();
                Debug.Log($"[LANDiscovery] Oda yayını başladı: {roomName} Port:{discoveryPort}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LANDiscovery] Broadcast başlatılamadı: {ex.Message}");
            }
        }

        public void UpdatePlayerCount(int count)
        {
            currentPlayers = count;
        }

        /// <summary>
        /// Host oda yayınını durdurur.
        /// </summary>
        public void StopBroadcasting()
        {
            isBroadcasting = false;
            if (broadcastSender != null)
            {
                try { broadcastSender.Close(); } catch { }
                broadcastSender = null;
            }
        }

        /// <summary>
        /// İstemciler için yerel ağdaki odaları dinleme iş parçacığını (thread) başlatır.
        /// </summary>
        public void StartListening()
        {
            if (isListening) return;

            lock (lockObj)
            {
                discoveredRooms.Clear();
            }

            try
            {
                broadcastReceiver = new UdpClient();
                broadcastReceiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                broadcastReceiver.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));

                isListening = true;
                receiveThread = new Thread(ReceiveThreadLoop) { IsBackground = true };
                receiveThread.Start();
                Debug.Log($"[LANDiscovery] LAN odaları dinleniyor... Port:{discoveryPort}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LANDiscovery] Dinleyici başlatılamadı: {ex.Message}");
            }
        }

        /// <summary>
        /// İstemci dinleyicisini durdurur.
        /// </summary>
        public void StopListening()
        {
            isListening = false;
            if (broadcastReceiver != null)
            {
                try { broadcastReceiver.Close(); } catch { }
                broadcastReceiver = null;
            }

            if (receiveThread != null && receiveThread.IsAlive)
            {
                try { receiveThread.Abort(); } catch { }
                receiveThread = null;
            }
        }

        public void StopDiscovery()
        {
            StopBroadcasting();
            StopListening();
        }

        private void SendBeacon()
        {
            if (broadcastSender == null) return;

            try
            {
                // Format: FINGER_MONSTER|RoomName|Port|CurrentPlayers|MaxPlayers
                string message = $"FINGER_MONSTER|{currentRoomName}|{currentGamePort}|{currentPlayers}|{maxPlayers}";
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);
                broadcastSender.Send(bytes, bytes.Length, endPoint);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LANDiscovery] Beacon gönderme hatası: {ex.Message}");
            }
        }

        private void ReceiveThreadLoop()
        {
            IPEndPoint senderEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (isListening && broadcastReceiver != null)
            {
                try
                {
                    byte[] data = broadcastReceiver.Receive(ref senderEndPoint);
                    string message = Encoding.UTF8.GetString(data);

                    if (message.StartsWith("FINGER_MONSTER|"))
                    {
                        string[] parts = message.Split('|');
                        if (parts.Length >= 5)
                        {
                            string roomName = parts[1];
                            ushort gamePort = ushort.Parse(parts[2]);
                            int cur = int.Parse(parts[3]);
                            int max = int.Parse(parts[4]);
                            string ip = senderEndPoint.Address.ToString();

                            string key = $"{ip}:{gamePort}";

                            lock (lockObj)
                            {
                                if (!discoveredRooms.ContainsKey(key))
                                {
                                    discoveredRooms[key] = new DiscoveredRoom();
                                }

                                var r = discoveredRooms[key];
                                r.RoomName = roomName;
                                r.IpAddress = ip;
                                r.Port = gamePort;
                                r.CurrentPlayers = cur;
                                r.MaxPlayers = max;
                                r.LastSeenTime = Time.time;
                            }
                        }
                    }
                }
                catch (SocketException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Ağ kesintisi veya thread iptali
                }
            }
        }

        private void NotifyRoomsUpdated()
        {
            List<DiscoveredRoom> list = new List<DiscoveredRoom>();
            lock (lockObj)
            {
                list.AddRange(discoveredRooms.Values);
            }
            OnRoomsUpdated?.Invoke(list);
        }

        public List<DiscoveredRoom> GetDiscoveredRooms()
        {
            lock (lockObj)
            {
                return new List<DiscoveredRoom>(discoveredRooms.Values);
            }
        }
    }
}

