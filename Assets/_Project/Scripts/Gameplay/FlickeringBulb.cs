using System.Collections;
using UnityEngine;

namespace FingerMonster.Gameplay
{
    /// <summary>
    /// Bodrum / tecrit odası arka planındaki eski asılı ampulün
    /// organik, gerilimli ve komik şekilde titremesini (flicker) sağlar.
    /// Kasap sekansı başladığında ışığı söner, normal duruma dönüldüğünde geri gelir.
    /// </summary>
    public class FlickeringBulb : MonoBehaviour
    {
        [Header("Işık & Materyal")]
        [SerializeField] private Light bulbLight;
        [SerializeField] private Renderer bulbMeshRenderer;

        [Header("Parlaklık Ayarları")]
        [SerializeField] private float baseIntensity = 1.1f;
        [SerializeField] private float minFlicker = 0.2f;
        [SerializeField] private float maxFlicker = 1.4f;

        [Header("Sallantı (Dangling)")]
        [SerializeField] private bool enableDangle = true;
        [SerializeField] private float dangleSpeed = 1.2f;
        [SerializeField] private float dangleAngle = 3.5f;

        private Material bulbMaterial;
        private Coroutine flickerRoutine;
        private Quaternion initialRotation;
        private bool isMutedByExecution = false;

        private void Awake()
        {
            if (bulbLight == null) bulbLight = GetComponentInChildren<Light>();
            if (bulbMeshRenderer != null) bulbMaterial = bulbMeshRenderer.material;
            initialRotation = transform.localRotation;
        }

        private void OnEnable()
        {
            flickerRoutine = StartCoroutine(FlickerLoop());
        }

        private void OnDisable()
        {
            if (flickerRoutine != null) StopCoroutine(flickerRoutine);
        }

        private void Update()
        {
            if (enableDangle)
            {
                float angleX = Mathf.Sin(Time.time * dangleSpeed) * dangleAngle;
                float angleZ = Mathf.Cos(Time.time * dangleSpeed * 0.85f) * (dangleAngle * 0.7f);
                transform.localRotation = initialRotation * Quaternion.Euler(angleX, 0f, angleZ);
            }
        }

        public void SetMuted(bool muted)
        {
            isMutedByExecution = muted;
            if (bulbLight != null)
            {
                bulbLight.enabled = !muted;
            }
            if (bulbMaterial != null)
            {
                if (muted)
                {
                    bulbMaterial.DisableKeyword("_EMISSION");
                    bulbMaterial.SetColor("_EmissionColor", Color.black);
                }
                else
                {
                    bulbMaterial.EnableKeyword("_EMISSION");
                    bulbMaterial.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.4f) * 1.5f);
                }
            }
        }

        private IEnumerator FlickerLoop()
        {
            while (true)
            {
                if (!isMutedByExecution)
                {
                    // Kısa rastgele aralıklarla voltaj düşüşü simülasyonu
                    float waitTime = Random.Range(0.08f, 0.45f);
                    yield return new WaitForSeconds(waitTime);

                    // %15 ihtimalle derin sönme (glitch/flicker)
                    if (Random.value < 0.15f)
                    {
                        int burstCount = Random.Range(2, 5);
                        for (int i = 0; i < burstCount; i++)
                        {
                            SetIntensity(Random.Range(minFlicker, minFlicker + 0.25f));
                            yield return new WaitForSeconds(Random.Range(0.03f, 0.08f));
                            SetIntensity(Random.Range(baseIntensity * 0.8f, maxFlicker));
                            yield return new WaitForSeconds(Random.Range(0.02f, 0.06f));
                        }
                    }
                    else
                    {
                        float target = Random.Range(baseIntensity * 0.85f, maxFlicker);
                        SetIntensity(target);
                    }
                }
                else
                {
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }

        private void SetIntensity(float val)
        {
            if (isMutedByExecution) return;
            if (bulbLight != null) bulbLight.intensity = val;
            if (bulbMaterial != null)
            {
                Color emCol = new Color(1f, 0.82f, 0.45f) * (val * 1.8f);
                bulbMaterial.SetColor("_EmissionColor", emCol);
            }
        }
    }
}

