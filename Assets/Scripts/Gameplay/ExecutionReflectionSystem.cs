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
        private bool isExecutionActive = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureReflectionTexture();
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

            // 1. Odayı zifiri karanlığa büründür
            if (RoomLightingController.Instance != null)
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

            // 3. Yansıma kamerasını aktifleştir
            if (reflectionCamera != null)
            {
                reflectionCamera.enabled = true;
            }

            // 4. Görünürlük yönetimi: Seyirciler için kasap doğrudan görünmez, sadece yansımada belirir
            ConfigureVisibility(isLocalPlayerVictim);

            // 5. Ses atmosferi: Seyirciler için boğuk/uzaktan yankılı ses
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetExecutionAudioFilter(!isLocalPlayerVictim);
            }

            Debug.Log($"[ExecutionReflection] İnfaz başladı! Kurban Koltuğu: {victimSeatIndex}, Yerel Kurban mı: {isLocalPlayerVictim}");
        }

        private void ConfigureVisibility(bool isLocalPlayerVictim)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            if (isLocalPlayerVictim)
            {
                // Kurban: Önündeki daktilo yansımasında devasa silüeti görür, arkaya dönemez
                mainCam.cullingMask |= (1 << butcherLayer);
            }
            else
            {
                // Seyirci (Diğer Oyuncular): Kasabı doğrudan göremez (Culling Mask'tan çıkarılır),
                // sadece daktilonun parlak yüzeyindeki silüet yansımasında görür!
                mainCam.cullingMask &= ~(1 << butcherLayer);
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

