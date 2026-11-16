using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FingerMonster.Player;
using FingerMonster.Audio;

namespace FingerMonster.UI
{
    /// <summary>
    /// Oyuncunun istediği zaman [H] tuşu veya HUD butonuyla açıp kapatabildiği,
    /// tüm 4 karakterin (Feg, Kenan, Feyzi, MMG) yapabildiği 5'er hareketi ve
    /// kontrolleri gösteren karakter hareket rehberi paneli.
    /// </summary>
    public class EmoteGuidePanelUI : MonoBehaviour
    {
        public static EmoteGuidePanelUI Instance { get; private set; }

        [Header("Panel Kökü ve Kontroller")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button toggleHudButton;
        [SerializeField] private TextMeshProUGUI[] characterColumns = new TextMeshProUGUI[4];
        [SerializeField] private CharacterEmoteData[] characterEmoteDatas = new CharacterEmoteData[4];
        [SerializeField] private TextMeshProUGUI ghostControlsText;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(ClosePanel);
            }

            if (toggleHudButton != null)
            {
                toggleHudButton.onClick.RemoveAllListeners();
                toggleHudButton.onClick.AddListener(TogglePanel);
            }

            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void Start()
        {
            PopulateGuide();
        }

        public void SetEmoteDatas(CharacterEmoteData[] datas)
        {
            characterEmoteDatas = datas;
            PopulateGuide();
        }

        public void TogglePanel()
        {
            if (panelRoot == null) return;
            bool next = !panelRoot.activeSelf;
            panelRoot.SetActive(next);
            if (next) PopulateGuide();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
        }

        public void OpenPanel()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(true);
            PopulateGuide();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
        }

        public void ClosePanel()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(false);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
        }

        public void PopulateGuide()
        {
            if (characterColumns == null) return;

            for (int i = 0; i < 4 && i < characterColumns.Length; i++)
            {
                if (characterColumns[i] == null) continue;

                CharacterArchetype arch = (CharacterArchetype)i;
                var def = CharacterDefinition.GetDefinition(arch);
                CharacterEmoteData data = (characterEmoteDatas != null && i < characterEmoteDatas.Length && characterEmoteDatas[i] != null)
                    ? characterEmoteDatas[i]
                    : CharacterEmoteData.CreateRuntimeFallback(arch);

                string hex = ColorUtility.ToHtmlStringRGB(def.PrimaryColor);
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine($"<b><size=22><color=#{hex}>{def.DisplayName}</color></size></b> <size=16><color=#CCCCCC>[{def.PersonalityTag}]</color></size>");
                sb.AppendLine($"<size=13><color=#AAAAAA><i>{def.Title}</i></color></size>\n");

                if (data.Slots != null)
                {
                    for (int s = 0; s < data.Slots.Length; s++)
                    {
                        var slot = data.Slots[s];
                        if (slot == null) continue;
                        sb.AppendLine($"<b><color=#FFD95A>[{slot.SlotNumber}]</color> {slot.DisplayName}</b>");
                        sb.AppendLine($"<size=13><color=#D5D5D5>{slot.Description}</color></size>\n");
                    }
                }

                characterColumns[i].text = sb.ToString();
            }

            PopulateGhostControls();
        }

        private void PopulateGhostControls()
        {
            if (ghostControlsText == null && panelRoot != null)
            {
                Transform existing = panelRoot.transform.Find("GuideControls");
                if (existing != null)
                {
                    ghostControlsText = existing.GetComponent<TextMeshProUGUI>();
                }
            }

            if (ghostControlsText == null) return;

            RectTransform rt = ghostControlsText.rectTransform;
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 1200f), Mathf.Max(rt.sizeDelta.y, 52f));
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -290f);
            }

            ghostControlsText.fontSize = 14f;
            ghostControlsText.text =
                "1-5: Hareket Yap   |   Q / E: Hedef Değiştir   |   C: Emote Çarkı   |   H: Aç / Kapat\n" +
                "<b><color=#99C2FF>HAYALET AKTİF OYNANIŞ:</color></b> Tab: Serbest/İzleme  |  WASD: Uç  |  Space/Ctrl: Yukarı/Aşağı  |  Shift: Hızlı  |  Sağ Tık: Bak  |  F: Buuu!  |  1-4 / Tekerlek: Eşya Seç  |  Sol Tık: Fırlat";
        }
    }
}

