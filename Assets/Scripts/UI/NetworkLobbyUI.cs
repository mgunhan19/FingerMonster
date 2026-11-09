using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using FingerMonster.Networking;
using FingerMonster.Audio;
using FingerMonster.AI;

namespace FingerMonster.UI
{
    /// <summary>
    /// LAN Multiplayer lobi arayüzü kontrolcüsü.
    /// UDP ile LAN oda keşfi, doğrudan IP bağlantısı, oda kurma (Host),
    /// 4 koltuk göstergesi, hazır durumu ve bot doldurma seçeneklerini yönetir.
    /// </summary>
    public class NetworkLobbyUI : MonoBehaviour
    {
        [Header("Ana Paneller")]
        [SerializeField] private GameObject lobbyRoot;
        [SerializeField] private GameObject browserPanel;
        [SerializeField] private GameObject roomPanel;

        [Header("Tarayıcı (Browser) Bileşenleri")]
        [SerializeField] private TMP_InputField playerNameInput;
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private TMP_InputField directIpInput;
        [SerializeField] private TMP_InputField directPortInput;
        [SerializeField] private Button directJoinButton;
        [SerializeField] private Transform discoveredRoomsContainer;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button browserBackButton;

        [Header("Oda (Room) Bileşenleri")]
        [SerializeField] private TextMeshProUGUI roomHeaderTitle;
        [SerializeField] private TextMeshProUGUI roomDetailsText;
        [SerializeField] private Transform seatCardsContainer;
        [SerializeField] private Button botFillToggleButton;
        [SerializeField] private TextMeshProUGUI botFillToggleText;
        [SerializeField] private Button readyToggleButton;
        [SerializeField] private TextMeshProUGUI readyToggleText;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveRoomButton;

        private bool isLocalReady = false;
        private float discoveryRefreshTimer = 0f;
        private readonly List<GameObject> roomEntryObjects = new List<GameObject>();
        private readonly List<PlayerSeatCardUI> seatCardControllers = new List<PlayerSeatCardUI>();

        private void Awake()
        {
            if (createRoomButton != null) createRoomButton.onClick.AddListener(OnCreateRoomClicked);
            if (directJoinButton != null) directJoinButton.onClick.AddListener(OnDirectJoinClicked);
            if (browserBackButton != null) browserBackButton.onClick.AddListener(HideLobby);

            if (botFillToggleButton != null) botFillToggleButton.onClick.AddListener(OnToggleBotFill);
            if (readyToggleButton != null) readyToggleButton.onClick.AddListener(OnToggleReady);
            if (startGameButton != null) startGameButton.onClick.AddListener(OnStartGameClicked);
            if (leaveRoomButton != null) leaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);

            if (playerNameInput != null && string.IsNullOrEmpty(playerNameInput.text))
            {
                playerNameInput.text = "Oyuncu " + Random.Range(10, 99);
            }
            if (roomNameInput != null && string.IsNullOrEmpty(roomNameInput.text))
            {
                roomNameInput.text = "Daktilo Masası";
            }
            if (directIpInput != null && string.IsNullOrEmpty(directIpInput.text))
            {
                directIpInput.text = "127.0.0.1";
            }
            if (directPortInput != null && string.IsNullOrEmpty(directPortInput.text))
            {
                directPortInput.text = "7777";
            }
        }

        private void Start()
        {
            InitSeatCards();
            UpdateBotFillButtonUI();
        }

        private void OnEnable()
        {
            if (NetworkRoomManager.Instance != null)
            {
                NetworkRoomManager.Instance.OnJoinedLobby += HandleJoinedLobby;
                NetworkRoomManager.Instance.OnLeftLobby += HandleLeftLobby;
            }

            // LAN Keşfini dinlemeye başla
            if (LANDiscovery.Instance != null)
            {
                LANDiscovery.Instance.StartListening();
            }

            ShowBrowserPanel();
        }

        private void OnDisable()
        {
            if (NetworkRoomManager.Instance != null)
            {
                NetworkRoomManager.Instance.OnJoinedLobby -= HandleJoinedLobby;
                NetworkRoomManager.Instance.OnLeftLobby -= HandleLeftLobby;
            }
        }

        private void Update()
        {
            if (!lobbyRoot.activeInHierarchy) return;

            discoveryRefreshTimer += Time.deltaTime;
            if (discoveryRefreshTimer >= 0.8f)
            {
                discoveryRefreshTimer = 0f;
                if (browserPanel != null && browserPanel.activeSelf)
                {
                    RefreshDiscoveredRoomsList();
                }
                else if (roomPanel != null && roomPanel.activeSelf)
                {
                    RefreshRoomSeatsUI();
                }
            }
        }

        public void OpenLobby()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            if (lobbyRoot != null) lobbyRoot.SetActive(true);
            ShowBrowserPanel();
        }

        public void HideLobby()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            if (NetworkRoomManager.Instance != null) NetworkRoomManager.Instance.Disconnect();
            if (lobbyRoot != null) lobbyRoot.SetActive(false);
        }

        private void ShowBrowserPanel()
        {
            if (browserPanel != null) browserPanel.SetActive(true);
            if (roomPanel != null) roomPanel.SetActive(false);
            if (statusText != null) statusText.text = "Ağdaki LAN odaları taranıyor...";
        }

        private void ShowRoomPanel()
        {
            if (browserPanel != null) browserPanel.SetActive(false);
            if (roomPanel != null) roomPanel.SetActive(true);
            isLocalReady = false;
            UpdateReadyButtonUI();
            RefreshRoomSeatsUI();
        }

        #region TARAYICI (BROWSER) VE BAĞLANTI İŞLEMLERİ
        private void OnCreateRoomClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            string rName = roomNameInput != null ? roomNameInput.text : "Oda";
            string pName = playerNameInput != null ? playerNameInput.text : "Host";

            if (NetworkRoomManager.Instance != null)
            {
                NetworkRoomManager.Instance.LocalPlayerName = pName;
                ushort port = 7777;
                if (directPortInput != null && ushort.TryParse(directPortInput.text, out ushort parsedPort))
                {
                    port = parsedPort;
                }

                if (statusText != null) statusText.text = "Oda kuruluyor (Host başlatılıyor)...";
                bool ok = NetworkRoomManager.Instance.StartHost(rName, port);
                if (ok)
                {
                    ShowRoomPanel();
                }
                else
                {
                    if (statusText != null) statusText.text = "<color=red>Oda kurulamadı! Port kullanımda olabilir.</color>";
                }
            }
        }

        private void OnDirectJoinClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            string ip = directIpInput != null ? directIpInput.text.Trim() : "127.0.0.1";
            string pName = playerNameInput != null ? playerNameInput.text : "Oyuncu";
            ushort port = 7777;
            if (directPortInput != null && ushort.TryParse(directPortInput.text, out ushort parsedPort))
            {
                port = parsedPort;
            }

            ConnectToTargetRoom(ip, port, pName);
        }

        public void ConnectToTargetRoom(string ip, ushort port, string pName)
        {
            if (NetworkRoomManager.Instance != null)
            {
                NetworkRoomManager.Instance.LocalPlayerName = pName;
                if (statusText != null) statusText.text = $"{ip}:{port} adresine bağlanılıyor...";
                bool ok = NetworkRoomManager.Instance.StartClient(ip, port);
                if (ok)
                {
                    ShowRoomPanel();
                }
                else
                {
                    if (statusText != null) statusText.text = "<color=red>Bağlantı başlatılamadı!</color>";
                }
            }
        }

        private void RefreshDiscoveredRoomsList()
        {
            if (LANDiscovery.Instance == null || discoveredRoomsContainer == null) return;

            var rooms = LANDiscovery.Instance.DiscoveredRooms;

            // Mevcut liste elemanlarını temizle
            for (int i = 0; i < roomEntryObjects.Count; i++)
            {
                if (roomEntryObjects[i] != null) Destroy(roomEntryObjects[i]);
            }
            roomEntryObjects.Clear();

            if (rooms.Count == 0)
            {
                if (statusText != null) statusText.text = "Yerel ağda aktif oda bulunamadı. Bir oda kurabilir veya IP ile katılabilirsiniz.";
                return;
            }

            if (statusText != null) statusText.text = $"Bulunan LAN Odaları ({rooms.Count}):";

            foreach (var kvp in rooms)
            {
                var room = kvp.Value;
                GameObject entryObj = new GameObject("RoomEntry_" + room.RoomName);
                entryObj.transform.SetParent(discoveredRoomsContainer, false);

                var entryRect = entryObj.AddComponent<RectTransform>();
                entryRect.sizeDelta = new Vector2(560, 50);

                var bgImg = entryObj.AddComponent<Image>();
                bgImg.color = new Color(0.18f, 0.18f, 0.22f, 0.9f);

                // Oda Bilgisi Metni
                GameObject infoTextObj = new GameObject("InfoText");
                infoTextObj.transform.SetParent(entryObj.transform, false);
                var infoRect = infoTextObj.AddComponent<RectTransform>();
                infoRect.anchorMin = new Vector2(0, 0);
                infoRect.anchorMax = new Vector2(0.7f, 1);
                infoRect.offsetMin = new Vector2(15, 0);
                infoRect.offsetMax = Vector2.zero;

                var infoText = infoTextObj.AddComponent<TextMeshProUGUI>();
                infoText.fontSize = 20;
                infoText.color = Color.white;
                infoText.alignment = TextAlignmentOptions.MidlineLeft;
                infoText.text = $"<b>{room.RoomName}</b> ({room.PlayerCount}/{room.MaxPlayers}) - {room.IPAddress}:{room.Port}";

                // Katıl Butonu
                GameObject joinBtnObj = new GameObject("JoinButton");
                joinBtnObj.transform.SetParent(entryObj.transform, false);
                var joinRect = joinBtnObj.AddComponent<RectTransform>();
                joinRect.anchorMin = new Vector2(0.75f, 0.1f);
                joinRect.anchorMax = new Vector2(0.98f, 0.9f);
                joinRect.offsetMin = Vector2.zero;
                joinRect.offsetMax = Vector2.zero;

                var joinImg = joinBtnObj.AddComponent<Image>();
                joinImg.color = new Color(0.2f, 0.65f, 0.3f);

                var joinBtn = joinBtnObj.AddComponent<Button>();
                string targetIp = room.IPAddress;
                ushort targetPort = room.Port;
                string pName = playerNameInput != null ? playerNameInput.text : "Oyuncu";
                joinBtn.onClick.AddListener(() => ConnectToTargetRoom(targetIp, targetPort, pName));

                GameObject btnTextObj = new GameObject("Text");
                btnTextObj.transform.SetParent(joinBtnObj.transform, false);
                var btnTextRect = btnTextObj.AddComponent<RectTransform>();
                btnTextRect.anchorMin = Vector2.zero;
                btnTextRect.anchorMax = Vector2.one;
                btnTextRect.sizeDelta = Vector2.zero;

                var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
                btnText.fontSize = 18;
                btnText.fontStyle = FontStyles.Bold;
                btnText.color = Color.white;
                btnText.alignment = TextAlignmentOptions.Center;
                btnText.text = "KATIL";

                roomEntryObjects.Add(entryObj);
            }
        }
        #endregion

        #region ODA (ROOM) LOBİSİ VE KOLTUK YÖNETİMİ
        private void InitSeatCards()
        {
            if (seatCardsContainer == null) return;
            seatCardControllers.Clear();

            // 4 Koltuk için UI kartlarını hazırla
            string[] seatPositions = new string[] { "Güney (Ön)", "Kuzey (Karşı)", "Batı (Sol)", "Doğu (Sağ)" };
            Color[] seatColors = new Color[]
            {
                new Color(0.9f, 0.25f, 0.25f),
                new Color(0.25f, 0.55f, 0.95f),
                new Color(0.25f, 0.85f, 0.35f),
                new Color(0.95f, 0.75f, 0.2f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject cardObj = new GameObject($"SeatCard_{i}");
                cardObj.transform.SetParent(seatCardsContainer, false);

                var rect = cardObj.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(180, 220);

                var img = cardObj.AddComponent<Image>();
                img.color = new Color(0.14f, 0.14f, 0.18f, 0.95f);

                // Koltuk başlığı
                GameObject seatTitle = new GameObject("SeatTitle");
                seatTitle.transform.SetParent(cardObj.transform, false);
                var tRect = seatTitle.AddComponent<RectTransform>();
                tRect.anchorMin = new Vector2(0, 0.75f);
                tRect.anchorMax = new Vector2(1, 1);
                tRect.offsetMin = Vector2.zero;
                tRect.offsetMax = Vector2.zero;
                var tText = seatTitle.AddComponent<TextMeshProUGUI>();
                tText.text = $"KOLTUK {i + 1}\n<size=14>{seatPositions[i]}</size>";
                tText.fontSize = 18;
                tText.fontStyle = FontStyles.Bold;
                tText.color = seatColors[i];
                tText.alignment = TextAlignmentOptions.Center;

                // Oyuncu Adı
                GameObject nameObj = new GameObject("PlayerName");
                nameObj.transform.SetParent(cardObj.transform, false);
                var nRect = nameObj.AddComponent<RectTransform>();
                nRect.anchorMin = new Vector2(0, 0.35f);
                nRect.anchorMax = new Vector2(1, 0.7f);
                nRect.offsetMin = Vector2.zero;
                nRect.offsetMax = Vector2.zero;
                var nText = nameObj.AddComponent<TextMeshProUGUI>();
                nText.text = "BOŞ";
                nText.fontSize = 20;
                nText.fontStyle = FontStyles.Bold;
                nText.color = Color.gray;
                nText.alignment = TextAlignmentOptions.Center;

                // Durum Rozeti (HAZIR / BEKLİYOR / BOT)
                GameObject statusObj = new GameObject("StatusBadge");
                statusObj.transform.SetParent(cardObj.transform, false);
                var sRect = statusObj.AddComponent<RectTransform>();
                sRect.anchorMin = new Vector2(0.1f, 0.08f);
                sRect.anchorMax = new Vector2(0.9f, 0.3f);
                sRect.offsetMin = Vector2.zero;
                sRect.offsetMax = Vector2.zero;
                var sImg = statusObj.AddComponent<Image>();
                sImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

                GameObject sTextObj = new GameObject("StatusText");
                sTextObj.transform.SetParent(statusObj.transform, false);
                var stRect = sTextObj.AddComponent<RectTransform>();
                stRect.anchorMin = Vector2.zero;
                stRect.anchorMax = Vector2.one;
                stRect.sizeDelta = Vector2.zero;
                var stText = sTextObj.AddComponent<TextMeshProUGUI>();
                stText.text = "-";
                stText.fontSize = 16;
                stText.fontStyle = FontStyles.Bold;
                stText.color = Color.white;
                stText.alignment = TextAlignmentOptions.Center;

                PlayerSeatCardUI cardCtrl = new PlayerSeatCardUI
                {
                    Root = cardObj,
                    NameText = nText,
                    StatusText = stText,
                    StatusBackground = sImg
                };
                seatCardControllers.Add(cardCtrl);
            }
        }

        private void RefreshRoomSeatsUI()
        {
            if (NetworkManager.Singleton == null) return;

            bool isServer = NetworkManager.Singleton.IsServer;

            if (roomHeaderTitle != null && NetworkRoomManager.Instance != null)
            {
                roomHeaderTitle.text = NetworkRoomManager.Instance.CurrentRoomName;
            }

            if (roomDetailsText != null)
            {
                roomDetailsText.text = isServer ? "Statü: <b>ODA KURUCUSU (HOST)</b>" : "Statü: <b>KATILIMCI (CLIENT)</b>";
            }

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(isServer);
            }

            if (botFillToggleButton != null)
            {
                botFillToggleButton.gameObject.SetActive(isServer);
            }

            var clients = NetworkManager.Singleton.ConnectedClientsList;
            int connectedCount = clients != null ? clients.Count : 0;
            bool fillBots = NetworkRoomManager.Instance != null && NetworkRoomManager.Instance.FillEmptySeatsWithBots;

            for (int i = 0; i < seatCardControllers.Count; i++)
            {
                var card = seatCardControllers[i];
                if (i < connectedCount)
                {
                    ulong cId = clients[i].ClientId;
                    bool isMe = (cId == NetworkManager.Singleton.LocalClientId);
                    string displayName = isMe ? $"{NetworkRoomManager.Instance?.LocalPlayerName ?? "Oyuncu"} (SEN)" : $"Oyuncu {i + 1}";
                    if (cId == NetworkManager.Singleton.LocalClientId && isServer) displayName += " [HOST]";

                    card.NameText.text = displayName;
                    card.NameText.color = Color.white;

                    // Hazır durumu
                    card.StatusText.text = isServer ? "HOST" : (isLocalReady && isMe ? "HAZIR" : "BEKLİYOR");
                    card.StatusBackground.color = (isServer || (isLocalReady && isMe)) ? new Color(0.2f, 0.7f, 0.3f) : new Color(0.85f, 0.65f, 0.15f);
                }
                else if (fillBots)
                {
                    card.NameText.text = $"Bot {i + 1}";
                    card.NameText.color = new Color(0.7f, 0.85f, 1f);
                    card.StatusText.text = "BOT (HAZIR)";
                    card.StatusBackground.color = new Color(0.2f, 0.5f, 0.8f);
                }
                else
                {
                    card.NameText.text = "BOŞ KOLTUK";
                    card.NameText.color = Color.gray;
                    card.StatusText.text = "BEKLENİYOR";
                    card.StatusBackground.color = new Color(0.3f, 0.3f, 0.3f);
                }
            }
        }

        private void OnToggleBotFill()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            if (NetworkRoomManager.Instance != null)
            {
                NetworkRoomManager.Instance.FillEmptySeatsWithBots = !NetworkRoomManager.Instance.FillEmptySeatsWithBots;
                UpdateBotFillButtonUI();
                RefreshRoomSeatsUI();
            }
        }

        private void UpdateBotFillButtonUI()
        {
            if (botFillToggleText == null || NetworkRoomManager.Instance == null) return;
            bool fill = NetworkRoomManager.Instance.FillEmptySeatsWithBots;
            botFillToggleText.text = fill ? "BOŞ KOLTUKLAR: <b>BOTLARLA DOLDUR (AÇIK)</b>" : "BOŞ KOLTUKLAR: <b>SADECE İNSAN (KAPALI)</b>";
            if (botFillToggleButton != null)
            {
                botFillToggleButton.GetComponent<Image>().color = fill ? new Color(0.2f, 0.55f, 0.8f) : new Color(0.5f, 0.3f, 0.3f);
            }
        }

        private void OnToggleReady()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            isLocalReady = !isLocalReady;
            UpdateReadyButtonUI();
            RefreshRoomSeatsUI();
        }

        private void UpdateReadyButtonUI()
        {
            if (readyToggleText != null)
            {
                readyToggleText.text = isLocalReady ? "HAZIRSINIZ (İPTAL ET)" : "HAZIR OL";
            }
            if (readyToggleButton != null)
            {
                readyToggleButton.GetComponent<Image>().color = isLocalReady ? new Color(0.8f, 0.3f, 0.3f) : new Color(0.2f, 0.7f, 0.3f);
            }
        }

        private void OnStartGameClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            if (NetworkRoomManager.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                Debug.Log("[NetworkLobbyUI] Host oyunu başlatıyor!");
                NetworkRoomManager.Instance.StartGameMatch(6);
            }
        }

        private void OnLeaveRoomClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            if (NetworkRoomManager.Instance != null)
            {
                NetworkRoomManager.Instance.Disconnect();
            }
            ShowBrowserPanel();
        }

        private void HandleJoinedLobby()
        {
            ShowRoomPanel();
        }

        private void HandleLeftLobby()
        {
            ShowBrowserPanel();
        }
        #endregion

        private class PlayerSeatCardUI
        {
            public GameObject Root;
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI StatusText;
            public Image StatusBackground;
        }
    }
}

