using System.Collections;
using UnityEngine;

namespace FingerMonster.Gameplay
{
    /// <summary>
    /// Odanın kasvetli atmosferini, daktilo spot ışığını ve kasap sekansındaki
    /// dramatik kırmızı/flicker ışık efektlerini yönetir.
    /// </summary>
    public class RoomLightingController : MonoBehaviour
    {
        [Header("Işık Kaynakları")]
        [SerializeField] private Light tableSpotLight;
        [SerializeField] private Light ambientRoomLight;
        [SerializeField] private Light dramaticRedLight;

        [Header("Varsayılan Değerler")]
        [SerializeField] private float normalSpotIntensity = 2.5f;
        [SerializeField] private float normalAmbientIntensity = 0.35f;
        [SerializeField] private Color normalSpotColor = new Color(1.0f, 0.95f, 0.85f);

        [Header("Kasap Sekansı Değerleri")]
        [SerializeField] private float butcherSpotIntensity = 0.8f;
        [SerializeField] private float butcherAmbientIntensity = 0.05f;
        [SerializeField] private Color butcherSpotColor = new Color(0.85f, 0.1f, 0.1f);

        public static RoomLightingController Instance { get; private set; }

        private Coroutine activeTransition;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            ApplyNormalLighting(instant: true);
        }

        /// <summary>
        /// Kasap / infaz sekansı aydınlatma modunu açar veya kapatır.
        /// </summary>
        public void SetExecutionMode(bool active)
        {
            if (active)
            {
                SetButcherState();
            }
            else
            {
                SetNormalState();
            }
        }

        /// <summary>
        /// Standart kasvetli oda ve makine spot ışığı ayarına döner.
        /// </summary>
        public void SetNormalState()
        {
            if (activeTransition != null) StopCoroutine(activeTransition);
            activeTransition = StartCoroutine(TransitionRoutine(normalSpotIntensity, normalAmbientIntensity, normalSpotColor, redLightActive: false));
        }

        /// <summary>
        /// Sıradaki oyuncunun rengine göre daktilo spotuna hafif renk tonu yansıtır.
        /// </summary>
        public void SetTurnState(Color playerTint)
        {
            if (activeTransition != null) StopCoroutine(activeTransition);
            Color tinted = Color.Lerp(normalSpotColor, playerTint, 0.3f);
            activeTransition = StartCoroutine(TransitionRoutine(normalSpotIntensity, normalAmbientIntensity, tinted, redLightActive: false));
        }

        /// <summary>
        /// Kasap sekansında tüm odayı karartıp kırmızı alarm/gerilim atmosferine geçer.
        /// </summary>
        public void SetButcherState()
        {
            if (activeTransition != null) StopCoroutine(activeTransition);
            activeTransition = StartCoroutine(TransitionRoutine(butcherSpotIntensity, butcherAmbientIntensity, butcherSpotColor, redLightActive: true));
        }

        private IEnumerator TransitionRoutine(float targetSpotInt, float targetAmbientInt, Color targetSpotCol, bool redLightActive)
        {
            float elapsed = 0f;
            float duration = 0.6f;

            float startSpotInt = tableSpotLight != null ? tableSpotLight.intensity : 0f;
            float startAmbientInt = ambientRoomLight != null ? ambientRoomLight.intensity : 0f;
            Color startSpotCol = tableSpotLight != null ? tableSpotLight.color : Color.white;

            if (dramaticRedLight != null)
            {
                dramaticRedLight.enabled = redLightActive;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (tableSpotLight != null)
                {
                    tableSpotLight.intensity = Mathf.Lerp(startSpotInt, targetSpotInt, t);
                    tableSpotLight.color = Color.Lerp(startSpotCol, targetSpotCol, t);
                }

                if (ambientRoomLight != null)
                {
                    ambientRoomLight.intensity = Mathf.Lerp(startAmbientInt, targetAmbientInt, t);
                }

                yield return null;
            }

            if (tableSpotLight != null)
            {
                tableSpotLight.intensity = targetSpotInt;
                tableSpotLight.color = targetSpotCol;
            }

            if (ambientRoomLight != null)
            {
                ambientRoomLight.intensity = targetAmbientInt;
            }
        }

        private void ApplyNormalLighting(bool instant)
        {
            if (tableSpotLight != null)
            {
                tableSpotLight.intensity = normalSpotIntensity;
                tableSpotLight.color = normalSpotColor;
            }

            if (ambientRoomLight != null)
            {
                ambientRoomLight.intensity = normalAmbientIntensity;
            }

            if (dramaticRedLight != null)
            {
                dramaticRedLight.enabled = false;
            }
        }

        public void SetupLights(Light spot, Light ambient, Light dramatic)
        {
            tableSpotLight = spot;
            ambientRoomLight = ambient;
            dramaticRedLight = dramatic;
            ApplyNormalLighting(instant: true);
        }
    }
}

