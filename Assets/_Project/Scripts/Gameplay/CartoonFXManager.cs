using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace FingerMonster.Gameplay
{
    /// <summary>
    /// Çizgi film tarzı, abartılı ve komik görsel efektleri (puf dumanı, konfeti/yıldızlar,
    /// boya sıçramaları, ekran sarsıntısı ve çizgi roman tarzı yüzen metinler) yöneten efekt yöneticisi.
    /// Gerçekçi vahşet içermez; tamamen stilize ve eğlencelidir.
    /// Çizgi film tarzı görsel efektleri (ekran sarsıntısı, boya sıçramaları ve çizgi roman tarzı yüzen metinler) yöneten efekt yöneticisi.
    /// Kullanıcı talebi doğrultusunda hiçbir ParticleSystem bileşeni içermez; tüm efektler procedural mesh/UI tabanlıdır.
    /// </summary>
    public class CartoonFXManager : MonoBehaviour
    {
        public static CartoonFXManager Instance { get; private set; }

        private static readonly Color RealisticBloodColor = new Color(0.32f, 0.015f, 0.015f, 0.96f);
        private static readonly Color DarkCoagulatedBloodColor = new Color(0.20f, 0.008f, 0.008f, 0.98f);

        [Header("Partikül Prefabları / Sistemleri (Devre Dışı)")]
        [SerializeField] private ParticleSystem confettiParticleSystem;
        [SerializeField] private ParticleSystem smokePuffParticleSystem;
        [SerializeField] private ParticleSystem starBurstParticleSystem;

        [Header("Ekran Sarsıntısı (Camera Shake)")]
        [SerializeField] private Transform cameraTransform;

        private Vector3 originalCamPos;
        private Coroutine shakeRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            CleanupAnyLegacyParticles();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void CleanupAnyLegacyParticles()
        {
            if (confettiParticleSystem != null)
            {
                Destroy(confettiParticleSystem.gameObject);
                confettiParticleSystem = null;
            }
            if (smokePuffParticleSystem != null)
            {
                Destroy(smokePuffParticleSystem.gameObject);
                smokePuffParticleSystem = null;
            }
            if (starBurstParticleSystem != null)
            {
                Destroy(starBurstParticleSystem.gameObject);
                starBurstParticleSystem = null;
            }

            string[] legacyNames = { "FX_Confetti", "FX_SmokePuff", "FX_StarBurst" };
            for (int i = 0; i < legacyNames.Length; i++)
            {
                Transform child = transform.Find(legacyNames[i]);
                if (child != null) Destroy(child.gameObject);
            }

            var childParticles = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < childParticles.Length; i++)
            {
                if (childParticles[i] != null)
                {
                    if (childParticles[i].gameObject != gameObject)
                        Destroy(childParticles[i].gameObject);
                    else
                        Destroy(childParticles[i]);
                }
            }
        }

        /// <summary>
        /// Güvenli tuşa basıldığında çalışır (Partikül ve yazı tamamen devre dışı - no-op).
        /// </summary>
        public void PlaySafeKeyFX(Vector3 position)
        {
            // No-op: Partikül veya çizgi film yazısı oynatılmaz.
        }

        /// <summary>
        /// Tuzak tuş tetiklendiğinde kamera sarsıntısı ve darbe noktasında gerçekçi koyu kırmızı kan lekesi oluşturur.
        /// </summary>
        public void PlayTrapSnapFX(Vector3 position, Color playerColor)
        {
            ShakeCamera(0.28f, 0.09f);
            SpawnRealisticBloodCluster(position, 4, 0.14f);
        }

        /// <summary>
        /// Kasap satırı/çekici indiğinde güçlü kamera sarsıntısı ve masa/bilek çevresinde gerçekçi kan birikintisi + sıçrama decalleri oluşturur.
        /// </summary>
        public void PlayButcherStrikeFX(Vector3 position)
        {
            ShakeCamera(0.55f, 0.18f);
            SpawnRealisticBloodCluster(position, 8, 0.26f);
        }


        /// <summary>
        /// Kamera sarsıntısı (Trauma / Game Juice) efekti.
        /// </summary>
        public void ShakeCamera(float duration = 0.3f, float magnitude = 0.1f)
        {
            if (cameraTransform == null)
            {
                if (Camera.main != null) cameraTransform = Camera.main.transform;
                else return;
            }

            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(CameraShakeRoutine(duration, magnitude));
        }

        private IEnumerator CameraShakeRoutine(float duration, float magnitude)
        {
            float elapsed = 0f;
            Vector3 basePos = new Vector3(0f, 2.28f, -2.31f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float damp = 1f - (elapsed / duration);
                float offsetX = (Random.value * 2f - 1f) * magnitude * damp;
                float offsetY = (Random.value * 2f - 1f) * magnitude * damp;

                if (cameraTransform != null)
                {
                    cameraTransform.position = basePos + new Vector3(offsetX, offsetY, 0);
                }
                yield return null;
            }

            if (cameraTransform != null)
            {
                cameraTransform.position = basePos;
                var pcc = cameraTransform.GetComponent<FingerMonster.Player.PlayerCameraController>();
                if (pcc != null) pcc.ApplyFixedTransform();
            }
        }

        /// <summary>
        /// Masada veya zeminde elin koptuğu/çarptığı yere gerçekçi koyu kan lekesi bırakır.
        /// </summary>
        public void SpawnPaintSplat(Vector3 position, Color splatColor)
        {
            StartCoroutine(PaintSplatRoutine(position, RealisticBloodColor, 0.22f));
        }

        /// <summary>
        /// Merkezde bir ana ıslak kan birikintisi ve etrafında düzensiz dağılmış koyu kan damlası decal'leri oluşturur.
        /// </summary>
        public void SpawnRealisticBloodCluster(Vector3 centerPos, int dropletCount = 6, float mainPoolRadius = 0.22f)
        {
            SpawnBloodDecal(centerPos, mainPoolRadius, RealisticBloodColor);

            for (int i = 0; i < dropletCount; i++)
            {
                Vector2 randCircle = Random.insideUnitCircle.normalized * Random.Range(mainPoolRadius * 0.55f, mainPoolRadius * 1.85f);
                Vector3 dropPos = centerPos + new Vector3(randCircle.x, 0f, randCircle.y);
                float dropRadius = Random.Range(0.03f, 0.09f);
                Color dropColor = (i % 2 == 0) ? RealisticBloodColor : DarkCoagulatedBloodColor;
                SpawnBloodDecal(dropPos, dropRadius, dropColor);
            }
        }

        private void SpawnBloodDecal(Vector3 pos, float radius, Color bloodColor)
        {
            StartCoroutine(PaintSplatRoutine(pos, bloodColor, radius));
        }

        private IEnumerator PaintSplatRoutine(Vector3 tablePos, Color color, float radius = 0.22f)
        {
            Color effectiveColor = RealisticBloodColor;
            if (color == DarkCoagulatedBloodColor)
            {
                effectiveColor = DarkCoagulatedBloodColor;
            }

            GameObject splatObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            splatObj.name = "RealisticBloodDecal";
            splatObj.transform.position = new Vector3(tablePos.x, tablePos.y + 0.004f, tablePos.z);
            splatObj.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            splatObj.transform.localScale = new Vector3(0.01f, 0.0018f, 0.01f);

            var col = splatObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = splatObj.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = rend.material;
                mat.color = effectiveColor;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", effectiveColor);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.88f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.88f);
            }

            // Organik yayılma animasyonu
            float elapsed = 0f;
            float popDuration = 0.22f;
            float aspect = Random.Range(0.82f, 1.18f);
            Vector3 targetScale = new Vector3(radius * aspect, 0.0018f, radius / aspect);

            while (elapsed < popDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / popDuration;
                if (splatObj != null)
                {
                    splatObj.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, Mathf.Sin(t * Mathf.PI * 0.5f));
                }
                yield return null;
            }

            if (splatObj != null)
            {
                splatObj.transform.localScale = targetScale;
            }

            // Tur boyunca masada kalsın, 10 saniye sonra yavaşça solsun
            yield return new WaitForSeconds(10f);

            float fadeElapsed = 0f;
            float fadeDur = 2.0f;
            while (fadeElapsed < fadeDur)
            {
                fadeElapsed += Time.deltaTime;
                float alpha = 1f - (fadeElapsed / fadeDur);
                if (rend != null)
                {
                    Color c = effectiveColor;
                    c.a = alpha * effectiveColor.a;
                    rend.material.color = c;
                    if (rend.material.HasProperty("_BaseColor")) rend.material.SetColor("_BaseColor", c);
                }
                yield return null;
            }

            if (splatObj != null)
            {
                Destroy(splatObj);
            }
        }

        /// <summary>
        /// Çizgi roman tarzı yüzen metinler tamamen devre dışı bırakıldı (no-op).
        /// </summary>
        public void SpawnFloatingComicText(Vector3 worldPos, string text, Color color, float size = 1f)
        {
            return;
        }

        /// <summary>
        /// Çizgi roman tarzı vuruş/pop-up yazıları tamamen devre dışı bırakıldı (no-op).
        /// </summary>
        public void SpawnComicPopup(Vector3 position, string text, Color color, float size = 1.0f)
        {
            return;
        }

        /// <summary>
        /// Konfeti partikülü tamamen devre dışı bırakıldı (no-op).
        /// </summary>
        public void PlayConfetti(Vector3 position, Color color)
        {
            // No-op: Partikül oynatılmaz.
        }
    }
}

