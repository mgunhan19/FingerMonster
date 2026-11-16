using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using FingerMonster.Audio;
using FingerMonster.Core;
using FingerMonster.Data;
using FingerMonster.Player;
using FingerMonster.UI;

namespace FingerMonster.Gameplay
{
    /// <summary>
    /// Hayaletin "Buuu!" korkutma tepkilerini, parabolik fırlatılabilir nesne uçuşlarını,
    /// karakter/yüzey sıçrama lekelerini ve yerel ekran kenarı efektlerini yöneten sahne yardımcısı.
    /// </summary>
    public class GhostScareAndThrowSystem : MonoBehaviour
    {
        public static GhostScareAndThrowSystem Instance { get; private set; }

        private const float DefaultScareDuration = 0.6f;
        private const float DefaultDecalDuration = 8.0f;
        private const float DefaultSplatDuration = 3.0f;
        private const float NormalVignetteAlpha = 0.65f;
        private const float ReducedVignetteAlpha = 0.35f;
        private const float NormalScreenSplatAlpha = 0.45f;
        private const float ReducedScreenSplatAlpha = 0.25f;

        private static readonly MethodInfo AvatarPlayScareFlinchMethod =
            typeof(PlayerVisualAvatar).GetMethod("PlayScareFlinch", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly MethodInfo AvatarShowCharacterSplatMethod =
            typeof(PlayerVisualAvatar).GetMethod("ShowCharacterSplat", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly MethodInfo CameraGentleScareShakeMethod =
            typeof(PlayerCameraController).GetMethod("TriggerGentleScareShake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private readonly List<GameObject> activeProjectiles = new List<GameObject>();
        private readonly List<GameObject> activeDecals = new List<GameObject>();
        private readonly List<Coroutine> activeCoroutines = new List<Coroutine>();
        private readonly List<Material> runtimeMaterials = new List<Material>();

        private Canvas overlayCanvas;
        private GameObject vignetteContainer;
        private readonly List<Image> vignetteEdgeImages = new List<Image>();
        private GameObject screenSplatContainer;
        private readonly List<Image> screenSplatImages = new List<Image>();

        private Coroutine vignetteCoroutine;
        private Coroutine screenSplatCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            ClearAllActiveEffects();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Hayaletin yaşayan hedefi korkutabilmesi için menzil ve ön yarımküre kontrolünü yapar.
        /// </summary>
        public static bool CanScareTarget(Vector3 ghostPos, Transform livingTarget, float maxRange = 3.0f)
        {
            if (livingTarget == null || maxRange <= 0f) return false;

            Vector3 toGhost = ghostPos - livingTarget.position;
            float sqrDist = toGhost.sqrMagnitude;
            if (sqrDist > maxRange * maxRange || sqrDist < 0.0001f)
            {
                return false;
            }

            Vector3 dirToGhost = toGhost.normalized;
            return Vector3.Dot(livingTarget.forward, dirToGhost) > 0.1f;
        }

        /// <summary>
        /// Korkutma ("Buuu!") sesini, avatar irkilmesini ve yerel hedef için ışığa duyarlı güvenli ekran/kamera etkisini tetikler.
        /// </summary>
        public void ExecuteScareVisuals(
            int targetSeat,
            Vector3 ghostPos,
            PlayerVisualAvatar targetAvatar,
            PlayerCameraController localCam,
            bool isLocalTarget)
        {
            AudioManager.Instance?.PlayGhostScare(ghostPos);

            if (!SettingsMenuController.AreGhostEffectsEnabled())
            {
                return;
            }

            bool reduced = SettingsMenuController.AreGhostEffectsReduced();

            if (targetAvatar != null && AvatarPlayScareFlinchMethod != null)
            {
                AvatarPlayScareFlinchMethod.Invoke(targetAvatar, new object[] { reduced });
            }

            if (isLocalTarget)
            {
                float duration = GameManager.Instance != null && GameManager.Instance.Settings != null
                    ? GameManager.Instance.Settings.ghostScareEffectDuration
                    : DefaultScareDuration;

                float maxAlpha = reduced ? ReducedVignetteAlpha : NormalVignetteAlpha;
                TriggerEdgeVignette(Mathf.Max(0.2f, duration), maxAlpha);

                if (!reduced && localCam != null && CameraGentleScareShakeMethod != null)
                {
                    CameraGentleScareShakeMethod.Invoke(localCam, new object[] { 0.6f, 0.045f });
                }
            }
        }

        /// <summary>
        /// Karikatür fırlatılabilir nesneyi parabolik yayda uçurur ve çarpma anında ses/leke etkilerini uygular.
        /// </summary>
        public void LaunchThrowableVisual(
            ThrowableItemData item,
            Vector3 startPos,
            Vector3 endPos,
            int hitSeat,
            PlayerVisualAvatar hitAvatar,
            bool isLocalTarget)
        {
            if (item == null) return;

            AudioManager.Instance?.PlayGhostThrow(startPos);

            GameObject projectile = CreateCartoonProjectile(item);
            projectile.transform.position = startPos;
            activeProjectiles.Add(projectile);

            Coroutine routine = StartCoroutine(AnimateThrowableFlight(
                projectile,
                item,
                startPos,
                endPos,
                hitSeat,
                hitAvatar,
                isLocalTarget));
            activeCoroutines.Add(routine);
        }

        /// <summary>
        /// Tüm uçan nesneleri, yüzey lekelerini ve ekran kenarı sıçrama/vinyet UI öğelerini temizler.
        /// </summary>
        public void ClearAllActiveEffects()
        {
            for (int i = 0; i < activeCoroutines.Count; i++)
            {
                if (activeCoroutines[i] != null)
                {
                    StopCoroutine(activeCoroutines[i]);
                }
            }
            activeCoroutines.Clear();

            if (vignetteCoroutine != null)
            {
                StopCoroutine(vignetteCoroutine);
                vignetteCoroutine = null;
            }

            if (screenSplatCoroutine != null)
            {
                StopCoroutine(screenSplatCoroutine);
                screenSplatCoroutine = null;
            }

            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                if (activeProjectiles[i] != null) Destroy(activeProjectiles[i]);
            }
            activeProjectiles.Clear();

            for (int i = activeDecals.Count - 1; i >= 0; i--)
            {
                if (activeDecals[i] != null) Destroy(activeDecals[i]);
            }
            activeDecals.Clear();

            for (int i = runtimeMaterials.Count - 1; i >= 0; i--)
            {
                if (runtimeMaterials[i] != null) Destroy(runtimeMaterials[i]);
            }
            runtimeMaterials.Clear();

            if (vignetteContainer != null)
            {
                vignetteContainer.SetActive(false);
            }

            if (screenSplatContainer != null)
            {
                screenSplatContainer.SetActive(false);
            }
        }

        private IEnumerator AnimateThrowableFlight(
            GameObject projectile,
            ThrowableItemData item,
            Vector3 startPos,
            Vector3 endPos,
            int hitSeat,
            PlayerVisualAvatar hitAvatar,
            bool isLocalTarget)
        {
            float duration = Mathf.Max(0.12f, item.flightDuration);
            float arcHeight = Mathf.Max(0.1f, item.arcHeight);
            float elapsed = 0f;
            Vector3 spinAxis = new Vector3(1.1f, 0.7f, 0.9f).normalized;

            while (elapsed < duration && projectile != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                Vector3 pos = Vector3.Lerp(startPos, endPos, t);
                pos.y += 4f * arcHeight * t * (1f - t);

                projectile.transform.position = pos;
                projectile.transform.Rotate(spinAxis, 540f * Time.deltaTime, Space.Self);
                yield return null;
            }

            if (projectile != null)
            {
                activeProjectiles.Remove(projectile);
                Destroy(projectile);
            }

            AudioManager.Instance?.PlayGhostSplat(item.itemType, endPos);

            if (hitSeat >= 0)
            {
                if (hitAvatar != null && AvatarShowCharacterSplatMethod != null)
                {
                    AvatarShowCharacterSplatMethod.Invoke(hitAvatar, new object[] { item.splatColor, item.splatDuration });
                }

                if (isLocalTarget && SettingsMenuController.AreGhostEffectsEnabled())
                {
                    float splatDuration = item.splatDuration > 0.1f ? item.splatDuration : DefaultSplatDuration;
                    TriggerScreenEdgeSplat(item.splatColor, splatDuration);
                }
            }
            else
            {
                float decalDuration = GameManager.Instance != null && GameManager.Instance.Settings != null
                    ? GameManager.Instance.Settings.ghostDecalDuration
                    : DefaultDecalDuration;

                Coroutine decalRoutine = StartCoroutine(SpawnTemporaryDecal(endPos, item.splatColor, Mathf.Max(0.5f, decalDuration)));
                activeCoroutines.Add(decalRoutine);
            }
        }

        private IEnumerator SpawnTemporaryDecal(Vector3 worldPos, Color splatColor, float duration)
        {
            GameObject decal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            decal.name = "GhostSurfaceSplatDecal";
            StripColliders(decal);

            decal.transform.position = worldPos + Vector3.up * 0.015f;
            decal.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            decal.transform.localScale = new Vector3(0.45f, 0.01f, 0.45f);

            Renderer rend = decal.GetComponent<Renderer>();
            Material mat = CreateUnlitOrLitMaterial(splatColor);
            if (rend != null && mat != null)
            {
                rend.sharedMaterial = mat;
            }

            activeDecals.Add(decal);

            float fadeStart = Mathf.Max(0f, duration - 0.8f);
            float elapsed = 0f;

            while (elapsed < duration && decal != null)
            {
                elapsed += Time.deltaTime;
                if (elapsed > fadeStart && mat != null)
                {
                    float fadeT = Mathf.Clamp01((elapsed - fadeStart) / Mathf.Max(0.1f, duration - fadeStart));
                    Color c = splatColor;
                    c.a = Mathf.Lerp(splatColor.a, 0f, fadeT);
                    mat.color = c;
                }
                yield return null;
            }

            if (decal != null)
            {
                activeDecals.Remove(decal);
                Destroy(decal);
            }
        }

        private void TriggerEdgeVignette(float duration, float maxAlpha)
        {
            EnsureOverlayCanvas();
            if (vignetteContainer == null) return;

            if (vignetteCoroutine != null)
            {
                StopCoroutine(vignetteCoroutine);
            }
            vignetteCoroutine = StartCoroutine(AnimateEdgeVignette(duration, maxAlpha));
        }

        private IEnumerator AnimateEdgeVignette(float duration, float maxAlpha)
        {
            vignetteContainer.SetActive(true);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Yumuşak sinüs eğrisi: ani flaş olmadan kararır ve açılır (ışığa duyarlı güvenli)
                float alpha = Mathf.Sin(t * Mathf.PI) * maxAlpha;

                for (int i = 0; i < vignetteEdgeImages.Count; i++)
                {
                    if (vignetteEdgeImages[i] != null)
                    {
                        vignetteEdgeImages[i].color = new Color(0.03f, 0.04f, 0.08f, alpha);
                    }
                }
                yield return null;
            }

            for (int i = 0; i < vignetteEdgeImages.Count; i++)
            {
                if (vignetteEdgeImages[i] != null)
                {
                    vignetteEdgeImages[i].color = new Color(0.03f, 0.04f, 0.08f, 0f);
                }
            }

            vignetteContainer.SetActive(false);
            vignetteCoroutine = null;
        }

        private void TriggerScreenEdgeSplat(Color splatColor, float duration)
        {
            EnsureOverlayCanvas();
            if (screenSplatContainer == null) return;

            if (screenSplatCoroutine != null)
            {
                StopCoroutine(screenSplatCoroutine);
            }

            bool reduced = SettingsMenuController.AreGhostEffectsReduced();
            float maxAlpha = reduced ? ReducedScreenSplatAlpha : NormalScreenSplatAlpha;
            screenSplatCoroutine = StartCoroutine(AnimateScreenEdgeSplat(splatColor, duration, maxAlpha));
        }

        private IEnumerator AnimateScreenEdgeSplat(Color baseColor, float duration, float maxAlpha)
        {
            screenSplatContainer.SetActive(true);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Lerp(maxAlpha, 0f, t * t);

                for (int i = 0; i < screenSplatImages.Count; i++)
                {
                    if (screenSplatImages[i] != null)
                    {
                        screenSplatImages[i].color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                    }
                }
                yield return null;
            }

            screenSplatContainer.SetActive(false);
            screenSplatCoroutine = null;
        }

        private void EnsureOverlayCanvas()
        {
            if (overlayCanvas != null) return;

            GameObject canvasObj = new GameObject("GhostScareAndSplatOverlayCanvas", typeof(RectTransform));
            canvasObj.transform.SetParent(transform, false);

            overlayCanvas = canvasObj.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 45;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            BuildVignetteEdges(canvasObj.transform);
            BuildScreenEdgeSplats(canvasObj.transform);
        }

        private void BuildVignetteEdges(Transform parent)
        {
            vignetteContainer = new GameObject("EdgeVignetteContainer", typeof(RectTransform));
            vignetteContainer.transform.SetParent(parent, false);
            StretchFull(vignetteContainer.GetComponent<RectTransform>());

            // Sadece ekranın dört dış kenarında ince şeritler (toplam alan <= %20, orta alan tamamen açık)
            vignetteEdgeImages.Clear();
            vignetteEdgeImages.Add(CreateEdgeImage(vignetteContainer.transform, "VignetteTop", new Vector2(0f, 0.92f), new Vector2(1f, 1f)));
            vignetteEdgeImages.Add(CreateEdgeImage(vignetteContainer.transform, "VignetteBottom", new Vector2(0f, 0f), new Vector2(1f, 0.08f)));
            vignetteEdgeImages.Add(CreateEdgeImage(vignetteContainer.transform, "VignetteLeft", new Vector2(0f, 0.08f), new Vector2(0.06f, 0.92f)));
            vignetteEdgeImages.Add(CreateEdgeImage(vignetteContainer.transform, "VignetteRight", new Vector2(0.94f, 0.08f), new Vector2(1f, 0.92f)));

            vignetteContainer.SetActive(false);
        }

        private void BuildScreenEdgeSplats(Transform parent)
        {
            screenSplatContainer = new GameObject("ScreenEdgeSplatContainer", typeof(RectTransform));
            screenSplatContainer.transform.SetParent(parent, false);
            StretchFull(screenSplatContainer.GetComponent<RectTransform>());

            // Toplam ekran alanı ~%11.2 (<= %25 sınırı içinde), ortadaki daktilo tuşlarını ve kartları asla kapatmaz
            screenSplatImages.Clear();
            screenSplatImages.Add(CreateEdgeImage(screenSplatContainer.transform, "SplatTopLeft", new Vector2(0f, 0.82f), new Vector2(0.16f, 1f)));
            screenSplatImages.Add(CreateEdgeImage(screenSplatContainer.transform, "SplatTopRight", new Vector2(0.84f, 0.82f), new Vector2(1f, 1f)));
            screenSplatImages.Add(CreateEdgeImage(screenSplatContainer.transform, "SplatBottomLeft", new Vector2(0f, 0f), new Vector2(0.18f, 0.16f)));
            screenSplatImages.Add(CreateEdgeImage(screenSplatContainer.transform, "SplatBottomRight", new Vector2(0.84f, 0f), new Vector2(1f, 0.16f)));

            screenSplatContainer.SetActive(false);
        }

        private static Image CreateEdgeImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = Color.clear;
            return img;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private GameObject CreateCartoonProjectile(ThrowableItemData item)
        {
            GameObject root = new GameObject($"GhostProjectile_{item.itemType}");

            switch (item.itemType)
            {
                case ThrowableItemType.CreamPie:
                {
                    GameObject baseDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    baseDisc.transform.SetParent(root.transform, false);
                    baseDisc.transform.localScale = new Vector3(0.32f, 0.04f, 0.32f);
                    ApplyColor(baseDisc, new Color(0.85f, 0.65f, 0.38f));

                    GameObject cream = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    cream.transform.SetParent(root.transform, false);
                    cream.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                    cream.transform.localScale = new Vector3(0.28f, 0.14f, 0.28f);
                    ApplyColor(cream, item.primaryColor);
                    break;
                }
                case ThrowableItemType.Tomato:
                {
                    GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    body.transform.SetParent(root.transform, false);
                    body.transform.localScale = new Vector3(0.20f, 0.17f, 0.20f);
                    ApplyColor(body, item.primaryColor);

                    GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stem.transform.SetParent(root.transform, false);
                    stem.transform.localPosition = new Vector3(0f, 0.09f, 0f);
                    stem.transform.localScale = new Vector3(0.05f, 0.04f, 0.05f);
                    ApplyColor(stem, new Color(0.18f, 0.65f, 0.22f));
                    break;
                }
                case ThrowableItemType.PaperBall:
                {
                    GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    ball.transform.SetParent(root.transform, false);
                    ball.transform.localScale = new Vector3(0.18f, 0.16f, 0.19f);
                    ApplyColor(ball, item.primaryColor);
                    break;
                }
                case ThrowableItemType.Sausage:
                default:
                {
                    GameObject sausage = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    sausage.transform.SetParent(root.transform, false);
                    sausage.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    sausage.transform.localScale = new Vector3(0.10f, 0.18f, 0.10f);
                    ApplyColor(sausage, item.primaryColor);
                    break;
                }
            }

            StripColliders(root);
            return root;
        }

        private void ApplyColor(GameObject target, Color color)
        {
            Renderer rend = target.GetComponent<Renderer>();
            if (rend == null) return;
            Material mat = CreateUnlitOrLitMaterial(color);
            if (mat != null)
            {
                rend.sharedMaterial = mat;
            }
        }

        private Material CreateUnlitOrLitMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            Material mat = new Material(shader);
            mat.color = color;
            runtimeMaterials.Add(mat);
            return mat;
        }

        private static void StripColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) Destroy(colliders[i]);
            }
        }
    }
}
