using UnityEngine;

namespace FingerMonster.Player
{
    /// <summary>
    /// Masanın etrafındaki her bir oyuncu koltuğunu ve o koltuğa ait kamera/kasap noktalarını tanımlar.
    /// </summary>
    public class PlayerSeat : MonoBehaviour
    {
        [Header("Oyuncu Kimliği")]
        [SerializeField] private int playerIndex = 0;
        public int PlayerIndex => playerIndex;

        [Header("Çapa Noktaları")]
        [Tooltip("Sıra bu oyuncudayken kameranın konumlanacağı ve daktiloya bakacağı nokta")]
        [SerializeField] private Transform cameraMountPoint;
        public Transform CameraMountPoint => cameraMountPoint != null ? cameraMountPoint : transform;

        [Tooltip("Kasap sekansında kasabın belireceği yer (koltuğun hemen arkası/başı)")]
        [SerializeField] private Transform butcherSpawnPoint;
        public Transform ButcherSpawnPoint => butcherSpawnPoint != null ? butcherSpawnPoint : transform;

        [Header("Oyuncu Avatarı")]
        [SerializeField] private PlayerVisualAvatar avatar;
        public PlayerVisualAvatar Avatar => avatar;

        [Header("Görsel Vurgu")]
        [SerializeField] private Renderer chairRenderer;
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f);
        [SerializeField] private Color activeColor = new Color(1f, 0.8f, 0.2f);

        private Material chairMaterial;

        private void Awake()
        {
            if (chairRenderer != null)
            {
                chairMaterial = chairRenderer.material;
            }
        }

        public void SetupSeat(int index, Color playerColor)
        {
            playerIndex = index;
            normalColor = playerColor * 0.4f;
            activeColor = playerColor;
            SetHighlight(false);

            // Koltuk koordinatlarının masadaki doğru simetrik yerine oturmasını sağla
            Vector3[] standardSeatPositions = new Vector3[] {
                new Vector3(0, 0, -2.1f),      // P1 (Güney)
                new Vector3(0, 0, 2.1f),       // P2 (Kuzey)
                new Vector3(-2.1f, 0, 0),      // P3 (Batı)
                new Vector3(2.1f, 0, 0)        // P4 (Doğu)
            };
            Quaternion[] standardSeatRotations = new Quaternion[] {
                Quaternion.Euler(0, 0, 0),
                Quaternion.Euler(0, 180, 0),
                Quaternion.Euler(0, 90, 0),
                Quaternion.Euler(0, -90, 0)
            };

            if (index >= 0 && index < standardSeatPositions.Length)
            {
                transform.position = standardSeatPositions[index];
                transform.rotation = standardSeatRotations[index];
            }

            if (cameraMountPoint == null)
            {
                Transform foundMount = transform.Find("CameraMount");
                if (foundMount != null) cameraMountPoint = foundMount;
            }

            if (avatar != null)
            {
                avatar.SetPlayerColor(playerColor);
            }
        }

        public void SetAvatar(PlayerVisualAvatar newAvatar)
        {
            avatar = newAvatar;
        }

        /// <summary>
        /// Koltuğun sıradaki oyuncu olduğunu belirten ışık/renk vurgusunu açıp kapatır.
        /// </summary>
        public void SetHighlight(bool isActive)
        {
            if (chairRenderer != null && chairMaterial != null)
            {
                chairMaterial.color = isActive ? activeColor : normalColor;
            }
        }

        private void OnDestroy()
        {
            if (chairMaterial != null)
            {
                Destroy(chairMaterial);
            }
        }
    }
}

