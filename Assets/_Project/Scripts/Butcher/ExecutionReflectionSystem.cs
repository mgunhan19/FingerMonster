using System.Collections;
using UnityEngine;
using FingerMonster.Audio;
using FingerMonster.Player;

namespace FingerMonster.Gameplay
{
    /// <summary>
    /// Kasap sekansı sırasında odayı karartan, diğer oyunculara kasabı sadece
    /// daktilonun parlak yüzeyinde silüet yansıması olarak gösteren,
    /// kurbana ise arkasından yükselen devasa gölge gerilimini yaşatan yansıma ve atmosfer kontrolcüsü.
    /// </summary>
    public class ExecutionReflectionSystem : MonoBehaviour
    {
        public static ExecutionReflectionSystem Instance { get; private set; }

        [Header("Yansıma ve Kamera Referansları")]
        [SerializeField] private Camera reflectionCamera;
        [SerializeField] private Renderer reflectionSurfaceRenderer;
        [SerializeField] private Light executionSpotlight;
        [SerializeField] private Light silhouetteRimLight;

        [Header("Yansıma Render Dokusu")]
        [SerializeField] private RenderTexture reflectionTexture;

        private int butcherLayer = 10; // ButcherSecret layer
        private int ghostOnlyLayer = 11; // GhostOnly layer
        private bool isExecutionActive = false;
        public bool IsExecutionActive => isExecutionActive;
        public bool IsLocalViewerGhost { get; set; } = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Instance.gameObject != gameObject)
                {
                    Destroy(Instance.gameObject);
                }
                else
                {
                    Destroy(this);
                    return;
                }
            }
            Instance = this;

            ResolveGhostOnlyLayer();
            EnsureReflectionTexture();
            ConfigureVisibility(false);
        }

        private void ResolveGhostOnlyLayer()
        {
            int namedLayer = LayerMask.NameToLayer("GhostOnly");
            ghostOnlyLayer = namedLayer >= 0 ? namedLayer : 11;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (reflectionTexture != null)
            {
                if (reflectionCamera != null && reflectionCamera.targetTexture == reflectionTexture)
                {
                    reflectionCamera.targetTexture = null;
                }
                if (reflectionSurfaceRenderer != null && reflectionSurfaceRenderer.sharedMaterial != null)
                {
                    reflectionSurfaceRenderer.sharedMaterial.mainTexture = null;
                }
                reflectionTexture.Release();
                if (Application.isPlaying)
                {
                    Destroy(reflectionTexture);
                }
                else
                {
                    DestroyImmediate(reflectionTexture);
                }
                reflectionTexture = null;
            }
        }

        private void EnsureReflectionTexture()
        {
            if (reflectionTexture == null)
            {
                reflectionTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32)
                {
                    name = "RT_ExecutionSilhouetteReflection"
                };
            }

            if (reflectionCamera != null)
            {
                reflectionCamera.targetTexture = reflectionTexture;
                reflectionCamera.enabled = false;
            }

            if (reflectionSurfaceRenderer != null && reflectionSurfaceRenderer.material != null)
            {
                reflectionSurfaceRenderer.material.mainTexture = reflectionTexture;
            }
        }

        /// <summary>
        /// İnfaz sekansını başlatır.
        /// </summary>
        /// <param name="victimSeatIndex">Kurban koltuk indeksi (0..3)</param>
        /// <param name="isLocalPlayerVictim">Yerel oyuncu kurban mı yoksa seyirci mi?</param>
        public void StartExecutionSequence(int victimSeatIndex, bool isLocalPlayerVictim)
        {
            isExecutionActive = true;

            // 1. Odayı zifiri karanlığa büründür (hayalet izleyici normal ışıkta izler)
            if (!IsLocalViewerGhost && RoomLightingController.Instance != null)
            {
                RoomLightingController.Instance.SetExecutionMode(true);
            }

            // 2. Dar kırmızımsı/soğuk kontur spotu aç
            if (executionSpotlight != null)
            {
                executionSpotlight.enabled = true;
                executionSpotlight.color = new Color(0.85f, 0.2f, 0.2f);
                executionSpotlight.intensity = 3.5f;
            }

            if (silhouetteRimLight != null)
            {
                silhouetteRimLight.enabled = true;
                silhouetteRimLight.color = new Color(0.4f, 0.7f, 1.0f);
            }

            // 3. Yansıma kamerasını kurban koltuğuna doğrult ve aktifleştir
            if (reflectionCamera != null)
            {
                reflectionCamera.cullingMask |= (1 << butcherLayer);
                var seats = Object.FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None);
                for (int i = 0; i < seats.Length; i++)
                {
                    if (seats[i].PlayerIndex == victimSeatIndex)
                    {
                        Vector3 victimHead = seats[i].transform.position + Vector3.up * 1.25f;
                        Vector3 seatDir = seats[i].transform.position;
                        seatDir.y = 0f;
                        Vector3 offsetDir = seatDir.sqrMagnitude > 0.001f ? seatDir.normalized : Vector3.forward;
                        Vector3 camOrigin = new Vector3(0f, 1.35f, 0f) + offsetDir * -0.45f;
                        reflectionCamera.transform.position = camOrigin;

                        Vector3 lookDir = victimHead - camOrigin;
                        if (lookDir.sqrMagnitude > 0.001f)
                        {
                            Vector3 forward = lookDir.normalized;
                            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
                            reflectionCamera.transform.rotation = Quaternion.LookRotation(forward, up);
                        }
                        break;
                    }
                }
                reflectionCamera.enabled = true;
            }

            // 4. Görünürlük yönetimi: Seyirciler için kasap doğrudan görünmez, sadece yansımada belirir
            ConfigureVisibility(isLocalPlayerVictim);

            // 5. Ses atmosferi: Seyirciler için boğuk/uzaktan yankılı ses (hayalet izleyici net duyar)
            if (!IsLocalViewerGhost && AudioManager.Instance != null)
            {
                AudioManager.Instance.SetExecutionAudioFilter(!isLocalPlayerVictim);
            }

            Debug.Log($"[ExecutionReflection] İnfaz başladı! Kurban Koltuğu: {victimSeatIndex}, Yerel Kurban mı: {isLocalPlayerVictim}");
        }

        public void ConfigureVisibility(bool isLocalPlayerVictim)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            ResolveGhostOnlyLayer();

            // Kasap hiçbir zaman kameradan gizlenmez; tüm oyuncular tarafından doğrudan görünür.
            mainCam.cullingMask |= (1 << butcherLayer);

            // Hayaletlere özel önizleme katmanı yalnızca hayalet izleyicinin kamerasında görünür.
            if (IsLocalViewerGhost)
            {
                mainCam.cullingMask |= (1 << ghostOnlyLayer);
            }
            else
            {
                mainCam.cullingMask &= ~(1 << ghostOnlyLayer);
            }
        }

        /// <summary>
        /// İnfaz tamamlandığında ışıkları ve normal kamera modunu geri yükler.
        /// </summary>
        public void EndExecutionSequence()
        {
            isExecutionActive = false;

            if (RoomLightingController.Instance != null)
            {
                RoomLightingController.Instance.SetExecutionMode(false);
            }

            if (executionSpotlight != null) executionSpotlight.enabled = false;
            if (silhouetteRimLight != null) silhouetteRimLight.enabled = false;

            if (reflectionCamera != null) reflectionCamera.enabled = false;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.cullingMask |= (1 << butcherLayer);
                if (IsLocalViewerGhost)
                {
                    mainCam.cullingMask |= (1 << ghostOnlyLayer);
                }
                else
                {
                    mainCam.cullingMask &= ~(1 << ghostOnlyLayer);
                }
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetExecutionAudioFilter(false);
            }

            Debug.Log("[ExecutionReflection] İnfaz sekansı tamamlandı. Normal ışıklandırmaya dönüldü.");
        }

        public void SetupComponents(Camera reflCam, Renderer reflSurface, Light spot, Light rim)
        {
            reflectionCamera = reflCam;
            reflectionSurfaceRenderer = reflSurface;
            executionSpotlight = spot;
            silhouetteRimLight = rim;
            EnsureReflectionTexture();
        }
    }
}

