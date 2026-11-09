using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Netcode;
using TMPro;
using FingerMonster.Player;
using FingerMonster.Audio;
using FingerMonster.Networking;

namespace FingerMonster.UI
{
    /// <summary>
    /// Oyuncunun 'E' tuşuna veya gamepad tuşuna basılı tutarak 4 yönlü radyal menüden
    /// komik jest/alay (Emote) seçmesini sağlayan tekerlek arayüzü kontrolcüsü.
    /// Spam koruması (2.5 sn cooldown) ve ağ senkronizasyonunu yönetir.
    /// </summary>
    public class EmoteWheelUI : MonoBehaviour
    {
        public static EmoteWheelUI Instance { get; private set; }

        [Header("Tekerlek Paneli")]
        [SerializeField] private GameObject wheelRoot;
        [SerializeField] private Image cooldownRing;
        [SerializeField] private TextMeshProUGUI centerStatusText;

        [Header("4 Dilim (Yukarı, Sağ, Aşağı, Sol)")]
        [SerializeField] private Image[] sliceHighlights;
        [SerializeField] private TextMeshProUGUI[] sliceLabels;

        [Header("Ayarlar")]
        [SerializeField] private float cooldownDuration = 2.5f;

        private CharacterArchetype localArchetype = CharacterArchetype.Kabadayi;
        private EmoteType[] activeEmotes;
        private int selectedSliceIndex = -1;
        private float currentCooldown = 0f;
        private bool isWheelOpen = false;

        public bool IsOnCooldown => currentCooldown > 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (wheelRoot != null) wheelRoot.SetActive(false);
        }

        private void Start()
        {
            ConfigureForArchetype(localArchetype);
        }

        public void ConfigureForArchetype(CharacterArchetype archetype)
        {
            localArchetype = archetype;
            var def = CharacterDefinition.GetDefinition(archetype);
            activeEmotes = def.AvailableEmotes;

            string[] defaultLabels = new string[] { "EMOTE 1", "EMOTE 2", "EMOTE 3", "EMOTE 4" };

            switch (archetype)
            {
                case CharacterArchetype.Kabadayi:
                    defaultLabels = new string[] { "GÖĞÜS YUMRUKLA", "KAS GÖSTERİSİ", "PARMAKLA TEHDİT", "GÜR KAHKAHA" };
                    break;
                case CharacterArchetype.Korkak:
                    defaultLabels = new string[] { "TİTRE & TIRNAK YE", "MASAYA SAKLAN", "SAHTE AĞLAMA", "TAVUK TAKLİDİ" };
                    break;
                case CharacterArchetype.Sinsi:
                    defaultLabels = new string[] { "PARMAK SALLA", "ALAYCI ALKIŞ", "DİL ÇIKAR", "ELE DÜRBÜNLE BAK" };
                    break;
                case CharacterArchetype.Sovmen:
                    defaultLabels = new string[] { "ŞAPKA REVERANSI", "CAZ ELLERİ", "ALKIŞ İSTE", "ZAFER POZU" };
                    break;
            }

            for (int i = 0; i < sliceLabels.Length && i < defaultLabels.Length; i++)
            {
                if (sliceLabels[i] != null) sliceLabels[i].text = defaultLabels[i];
            }
        }

        private void Update()
        {
            // Cooldown sayacı
            if (currentCooldown > 0f)
            {
                currentCooldown -= Time.deltaTime;
                if (cooldownRing != null)
                {
                    cooldownRing.fillAmount = currentCooldown / cooldownDuration;
                }
            }
            else if (cooldownRing != null)
            {
                cooldownRing.fillAmount = 0f;
            }

            // 'E' Tuşu ve Gamepad ile açıp kapama
            HandleInput();
        }

        private void HandleInput()
        {
            bool holdEmoteKey = false;

            if (Keyboard.current != null && Keyboard.current.eKey.isPressed)
            {
                holdEmoteKey = true;
            }

            if (Gamepad.current != null && Gamepad.current.buttonNorth.isPressed)
            {
                holdEmoteKey = true;
            }

            if (holdEmoteKey && !isWheelOpen)
            {
                OpenWheel();
            }
            else if (!holdEmoteKey && isWheelOpen)
            {
                CloseAndExecuteEmote();
            }

            if (isWheelOpen)
            {
                UpdateSliceSelection();
            }
        }

        private void OpenWheel()
        {
            isWheelOpen = true;
            if (wheelRoot != null) wheelRoot.SetActive(true);
            selectedSliceIndex = -1;
            ClearHighlights();
        }

        private void UpdateSliceSelection()
        {
            Vector2 dir = Vector2.zero;

            if (Gamepad.current != null)
            {
                dir = Gamepad.current.rightStick.ReadValue();
                if (dir.sqrMagnitude < 0.2f) dir = Gamepad.current.leftStick.ReadValue();
            }

            if (dir.sqrMagnitude < 0.2f && Mouse.current != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                dir = (mousePos - screenCenter).normalized;
            }

            if (dir.sqrMagnitude > 0.3f)
            {
                // Açıyı hesapla (-180..180)
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360f;

                // 4 Yön Dilimleri (0: Yukarı [45-135], 1: Sağ [315-45], 2: Aşağı [225-315], 3: Sol [135-225])
                if (angle >= 45f && angle < 135f) selectedSliceIndex = 0; // Yukarı
                else if (angle >= 315f || angle < 45f) selectedSliceIndex = 1; // Sağ
                else if (angle >= 225f && angle < 315f) selectedSliceIndex = 2; // Aşağı
                else selectedSliceIndex = 3; // Sol

                HighlightSlice(selectedSliceIndex);
            }
            else
            {
                selectedSliceIndex = -1;
                ClearHighlights();
            }
        }

        private void HighlightSlice(int index)
        {
            for (int i = 0; i < sliceHighlights.Length; i++)
            {
                if (sliceHighlights[i] != null)
                {
                    sliceHighlights[i].color = (i == index) ? new Color(1f, 0.85f, 0.2f, 0.85f) : new Color(0.2f, 0.2f, 0.25f, 0.6f);
                }
            }
        }

        private void ClearHighlights()
        {
            for (int i = 0; i < sliceHighlights.Length; i++)
            {
                if (sliceHighlights[i] != null)
                {
                    sliceHighlights[i].color = new Color(0.2f, 0.2f, 0.25f, 0.6f);
                }
            }
        }

        private void CloseAndExecuteEmote()
        {
            isWheelOpen = false;
            if (wheelRoot != null) wheelRoot.SetActive(false);

            if (selectedSliceIndex >= 0 && activeEmotes != null && selectedSliceIndex < activeEmotes.Length)
            {
                if (currentCooldown > 0f)
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
                    return;
                }

                EmoteType chosenEmote = activeEmotes[selectedSliceIndex];
                currentCooldown = cooldownDuration;

                // Emote'u tetikle (Yerel ve Ağ senkron)
                ExecuteEmote(chosenEmote);
            }
        }

        private void ExecuteEmote(EmoteType emote)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();

            // Ağ oturumu aktifse sunucuya ilet
            if (NetworkGameManager.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                int mySeat = NetworkGameManager.Instance.LocalSeatIndex;
                NetworkGameManager.Instance.RequestEmoteServerRpc(mySeat, (int)emote);
            }
            else
            {
                // Yerel oturumda doğrudan yerel kuklayı oynat
                var avatar = FindFirstObjectByType<PlayerVisualAvatar>();
                if (avatar != null)
                {
                    avatar.TriggerEmote(emote);
                }
            }
        }
    }
}

