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
            if (avatar == null)
            {
                avatar = GetComponentInChildren<PlayerVisualAvatar>(true);
            }
        }

        public void SetupSeat(int index, Color playerColor)
        {
            playerIndex = index;
            normalColor = playerColor * 0.4f;
            activeColor = playerColor;
            SetHighlight(false);

            // Koltuk koordinatlarının masadaki doğru yerine oturmasını sağla
            // Güney (P1) kamera önünü kapatmamak için z=-2.05, diğerleri masaya yakın
            Vector3[] standardSeatPositions = new Vector3[] {
                new Vector3(0, 0, -2.05f),     // P1 (Güney)
                new Vector3(0, 0, 1.48f),      // P2 (Kuzey)
                new Vector3(-1.68f, 0, 0),     // P3 (Batı)
                new Vector3(1.68f, 0, 0)       // P4 (Doğu)
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
            if (butcherSpawnPoint == null)
            {
                Transform foundButcher = transform.Find("ButcherSpawnPoint");
                if (foundButcher != null) butcherSpawnPoint = foundButcher;
            }

            if (avatar == null)
            {
                var found = GetComponentsInChildren<PlayerVisualAvatar>(true);
                if (found.Length > 0)
                {
                    avatar = found[0];
                    for (int f = 1; f < found.Length; f++)
                    {
                        if (found[f] != null)
                        {
                            found[f].gameObject.SetActive(false);
                            if (Application.isPlaying) Destroy(found[f].gameObject);
                            else DestroyImmediate(found[f].gameObject);
                        }
                    }
                }
            }

            if (avatar != null)
            {
                avatar.gameObject.SetActive(true);
                avatar.SetPlayerColor(playerColor);
                avatar.ResetAvatar();
            }
        }

        public void AssignCharacterPrefab(GameObject prefab, Color playerColor)
        {
            if (prefab == null) return;

            if (avatar == null)
            {
                avatar = GetComponentInChildren<PlayerVisualAvatar>(true);
            }

            var prefabAvatar = prefab.GetComponent<PlayerVisualAvatar>();
            if (avatar != null && prefabAvatar != null && avatar.Archetype == prefabAvatar.Archetype)
            {
                avatar.gameObject.SetActive(true);
                avatar.transform.localPosition = Vector3.zero;
                avatar.transform.localRotation = Quaternion.identity;
                avatar.transform.localScale = Vector3.one;
                avatar.SetPlayerColor(playerColor);
                avatar.ResetAvatar();
                return;
            }

            if (avatar != null)
            {
                avatar.gameObject.SetActive(false);
            }

            CleanExistingCharacterObjects();

            GameObject instance = Instantiate(prefab, transform);
            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(true);

            avatar = instance.GetComponent<PlayerVisualAvatar>();
            if (avatar != null)
            {
                avatar.SetPlayerColor(playerColor);
                avatar.ResetAvatar();
            }
        }

        private void CleanExistingCharacterObjects()
        {
            avatar = null;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == null) continue;
                if (child == cameraMountPoint || child == butcherSpawnPoint) continue;
                string cName = child.name;
                if (cName.Contains("Camera") || cName.Contains("Butcher") || cName.Contains("Stool") || cName.Contains("fi_vil_forge_stool")) continue;

                if (child.GetComponent<PlayerVisualAvatar>() != null ||
                    child.GetComponentInChildren<SkinnedMeshRenderer>(true) != null ||
                    child.GetComponentInChildren<Animator>(true) != null ||
                    cName.StartsWith("Avatar_") ||
                    cName.StartsWith("PlayerCharacter_") ||
                    cName.Contains("Bodyguard") ||
                    cName.Contains("Model"))
                {
                    child.gameObject.SetActive(false);
                    foreach (var r in child.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                    foreach (var a in child.GetComponentsInChildren<Animator>(true)) a.enabled = false;

                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
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
                DestroyImmediate(chairMaterial);
                chairMaterial = null;
            }
        }
    }
}

