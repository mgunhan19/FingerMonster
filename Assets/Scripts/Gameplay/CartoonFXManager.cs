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
    /// </summary>
    public class CartoonFXManager : MonoBehaviour
    {
        public static CartoonFXManager Instance { get; private set; }

        [Header("Partikül Prefabları / Sistemleri")]
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

            EnsureProceduralParticles();
        }

        private void EnsureProceduralParticles()
        {
            // Eğer partikül sistemleri Inspector'dan atanmamışsa prosedürel olarak oluştur
            if (confettiParticleSystem == null)
            {
                GameObject confettiObj = new GameObject("FX_Confetti");
                confettiObj.transform.SetParent(transform);
                confettiParticleSystem = confettiObj.AddComponent<ParticleSystem>();
                ConfigureConfetti(confettiParticleSystem);
            }

            if (smokePuffParticleSystem == null)
            {
                GameObject smokeObj = new GameObject("FX_SmokePuff");
                smokeObj.transform.SetParent(transform);
                smokePuffParticleSystem = smokeObj.AddComponent<ParticleSystem>();
                ConfigureSmoke(smokePuffParticleSystem);
            }

            if (starBurstParticleSystem == null)
            {
                GameObject starObj = new GameObject("FX_StarBurst");
                starObj.transform.SetParent(transform);
                starBurstParticleSystem = starObj.AddComponent<ParticleSystem>();
                ConfigureStars(starBurstParticleSystem);
            }
        }

        private void ConfigureConfetti(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = 1.2f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.gravityModifier = 1.5f;

            // Rastgele renkli konfetiler
            var col = ps.colorOverLifetime;
            col.enabled = true;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 35) });
        }

        private void ConfigureSmoke(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.4f;
            main.startLifetime = 0.8f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startColor = new Color(0.95f, 0.95f, 0.95f, 0.85f);
            main.gravityModifier = -0.2f; // Yukarı hafif süzülen puf dumanı

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 25) });
        }

        private void ConfigureStars(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = 0.9f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.startColor = new Color(1.0f, 0.85f, 0.1f, 1f); // Parlak sarı çizgi film yıldızları
            main.gravityModifier = 0.8f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 60f;
            shape.radius = 0.15f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20) });
        }

        /// <summary>
        /// Güvenli tuşa basıldığında küçük neşeli konfeti ve mürekkep pufu patlatır.
        /// </summary>
        public void PlaySafeKeyFX(Vector3 position)
        {
            if (confettiParticleSystem != null)
            {
                confettiParticleSystem.transform.position = position;
                confettiParticleSystem.Play();
            }

            SpawnFloatingComicText(position + new Vector3(0, 0.3f, 0), "GÜVENLİ!", Color.green, 1.2f);
        }

        /// <summary>
        /// Tuzak tuş tetiklendiğinde sansürlü puf dumanı, çizgi film yıldızları ve ekran sarsıntısı tetikler.
        /// </summary>
        public void PlayTrapSnapFX(Vector3 position, Color playerColor)
        {
            if (smokePuffParticleSystem != null)
            {
                smokePuffParticleSystem.transform.position = position;
                smokePuffParticleSystem.Play();
            }

            if (starBurstParticleSystem != null)
            {
                starBurstParticleSystem.transform.position = position;
                starBurstParticleSystem.Play();
            }

            ShakeCamera(0.35f, 0.12f);
            SpawnFloatingComicText(position + new Vector3(0, 0.4f, 0), "ÇAAATT!", Color.red, 1.6f);
        }

        /// <summary>
        /// Kasap sekansı başladığında dramatik ve komik büyük çizgi roman pufu ve sarsıntı oluşturur.
        /// </summary>
        public void PlayButcherStrikeFX(Vector3 position)
        {
            if (smokePuffParticleSystem != null)
            {
                smokePuffParticleSystem.transform.position = position;
                smokePuffParticleSystem.Play();
            }

            if (starBurstParticleSystem != null)
            {
                starBurstParticleSystem.transform.position = position;
                starBurstParticleSystem.Play();
            }

            ShakeCamera(0.7f, 0.22f);
            SpawnFloatingComicText(position + new Vector3(0, 1.2f, 0), "POW!! ELENDİ!", Color.yellow, 2.5f);
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
        /// Masada elin koptuğu yere stilize renkli boya/mürekkep sıçraması (paint splat) bırakır.
        /// Gerçekçi kan içermez; Looney Tunes tarzı canlı renkli komik boya lekesidir.
        /// </summary>
        public void SpawnPaintSplat(Vector3 tablePos, Color color)
        {
            StartCoroutine(PaintSplatRoutine(tablePos, color));
        }

        private IEnumerator PaintSplatRoutine(Vector3 tablePos, Color color)
        {
            GameObject splatObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            splatObj.name = "ComicPaintSplat";
            splatObj.transform.position = new Vector3(tablePos.x, tablePos.y + 0.005f, tablePos.z);
            splatObj.transform.localScale = new Vector3(0.01f, 0.002f, 0.01f);

            var col = splatObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = splatObj.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = color;
                if (rend.material.HasProperty("_BaseColor")) rend.material.SetColor("_BaseColor", color);
            }

            // Hızlıca yayılma animasyonu (pop-in scale)
            float elapsed = 0f;
            float popDuration = 0.2f;
            Vector3 targetScale = new Vector3(0.36f, 0.002f, 0.32f);

            while (elapsed < popDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / popDuration;
                splatObj.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, Mathf.Sin(t * Mathf.PI * 0.5f));
                yield return null;
            }

            splatObj.transform.localScale = targetScale;

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
                    Color c = color;
                    c.a = alpha;
                    rend.material.color = c;
                }
                yield return null;
            }

            Destroy(splatObj);
        }

        /// <summary>
        /// 3D dünyada yukarı doğru süzülen ve büyüyüp kaybolan çizgi roman tarzı metin balonu oluşturur.
        /// </summary>
        public void SpawnFloatingComicText(Vector3 worldPos, string text, Color color, float size = 1f)
        {
            StartCoroutine(FloatingComicTextRoutine(worldPos, text, color, size));
        }

        /// <summary>
        /// Çizgi roman tarzı vuruş/pop-up yazısı üretir (örn: BOONNNK!, DÖNNNKKK!).
        /// </summary>
        public void SpawnComicPopup(Vector3 worldPos, string text, Color color, float size = 2f)
        {
            SpawnFloatingComicText(worldPos, text, color, size);
        }

        /// <summary>
        /// Belirtilen konumda renkli kutlama/zafer konfetisi patlatır.
        /// </summary>
        public void PlayConfetti(Vector3 position, Color tint)
        {
            if (confettiParticleSystem != null)
            {
                confettiParticleSystem.transform.position = position;
                var main = confettiParticleSystem.main;
                main.startColor = tint;
                confettiParticleSystem.Play();
            }
        }

        private IEnumerator FloatingComicTextRoutine(Vector3 worldPos, string text, Color color, float size)
        {
            GameObject textObj = new GameObject("ComicFloatingText");
            textObj.transform.position = worldPos;

            TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 7f * size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;

            // Kameraya bakacak şekilde yönlendir
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                textObj.transform.forward = mainCam.transform.forward;
            }

            float duration = 0.9f;
            float elapsed = 0f;
            Vector3 startScale = Vector3.one * 0.4f;
            Vector3 peakScale = Vector3.one * 1.2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Yukarı doğru süzül
                textObj.transform.position += Vector3.up * (Time.deltaTime * 0.9f);

                // Pop-in animasyonu ve şeffaflaşma
                float scaleT = Mathf.Sin(t * Mathf.PI);
                textObj.transform.localScale = Vector3.Lerp(startScale, peakScale, scaleT);

                Color c = color;
                c.a = Mathf.Clamp01(1f - t);
                tmp.color = c;

                if (mainCam != null)
                {
                    textObj.transform.forward = mainCam.transform.forward;
                }

                yield return null;
            }

            Destroy(textObj);
        }
    }
}

