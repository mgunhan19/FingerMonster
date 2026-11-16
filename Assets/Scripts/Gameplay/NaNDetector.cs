using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FingerMonster.Gameplay
{
    /// <summary>
    /// Play Mode Teşhis Aracı:
    /// Yeniden oynadan hemen sonra ilk 5 frame ve her 0.5 saniyede bir
    /// sahnedeki tüm Transform, Renderer bounds, Rigidbody ve Camera değerlerini tarayarak
    /// NaN, Infinity veya geçersiz AABB üreten nesneyi tam hiyerarşi yoluyla loglar.
    /// </summary>
    public class NaNDetector : MonoBehaviour
    {
        private static NaNDetector instance;
        private int framesSinceSceneLoad = 0;
        private float scanTimer = 0f;

        [Header("Otomatik Tarama Ayarı")]
        [SerializeField] private bool autoScanInPlayMode = false;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            framesSinceSceneLoad = 0;
        }

        private void LateUpdate()
        {
            if (!autoScanInPlayMode) return;

            framesSinceSceneLoad++;
            scanTimer += Time.unscaledDeltaTime;

            if (framesSinceSceneLoad <= 5 || scanTimer >= 1.0f)
            {
                scanTimer = 0f;
                ScanAllObjects($"Frame {framesSinceSceneLoad}");
            }
        }

        public static void ScanAllObjects(string tag)
        {
            int errorCount = 0;

            // 1. Transform Taraması
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null) continue;

                Vector3 p = t.position;
                Quaternion r = t.rotation;
                Vector3 s = t.localScale;

                bool badP = !float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z);
                bool badR = !float.IsFinite(r.x) || !float.IsFinite(r.y) || !float.IsFinite(r.z) || !float.IsFinite(r.w) || (r.x == 0 && r.y == 0 && r.z == 0 && r.w == 0);
                bool badS = !float.IsFinite(s.x) || !float.IsFinite(s.y) || !float.IsFinite(s.z);

                if (badP || badR || badS)
                {
                    errorCount++;
                    Debug.LogError($"<color=red>[NaNDetector][{tag}] BOZUK TRANSFORM -> Nesne: '{GetPath(t)}' | Pos: {p} (Bad: {badP}) | Rot: {r} (Bad: {badR}) | Scale: {s} (Bad: {badS})</color>", t.gameObject);
                }
            }

            // 2. Renderer / Bounds Taraması
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer rnd = renderers[i];
                if (rnd == null) continue;

                try
                {
                    Bounds b = rnd.bounds;
                    Vector3 c = b.center;
                    Vector3 e = b.extents;
                    Vector3 sz = b.size;

                    // Metin veya henüz partikül üretmemiş sistemler 0 extent verebilir; bu normaldir.
                    // Yalnızca NaN veya negatif extents hatalıdır.
                    bool badC = !float.IsFinite(c.x) || !float.IsFinite(c.y) || !float.IsFinite(c.z);
                    bool badE = !float.IsFinite(e.x) || !float.IsFinite(e.y) || !float.IsFinite(e.z) || e.x < 0 || e.y < 0 || e.z < 0;
                    bool badSz = !float.IsFinite(sz.x) || !float.IsFinite(sz.y) || !float.IsFinite(sz.z);

                    if (badC || badE || badSz)
                    {
                        errorCount++;
                        Debug.LogError($"<color=red>[NaNDetector][{tag}] BOZUK RENDERER BOUNDS -> Nesne: '{GetPath(rnd.transform)}' ({rnd.GetType().Name}) | Center: {c} | Extents: {e} | Size: {sz}</color>", rnd.gameObject);
                    }
                }
                catch (System.Exception ex)
                {
                    errorCount++;
                    Debug.LogError($"<color=red>[NaNDetector][{tag}] RENDERER BOUNDS ERİŞİM HATASI -> Nesne: '{GetPath(rnd.transform)}': {ex.Message}</color>", rnd.gameObject);
                }
            }

            // 3. Rigidbody Taraması
            Rigidbody[] rbs = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            for (int i = 0; i < rbs.Length; i++)
            {
                Rigidbody rb = rbs[i];
                if (rb == null) continue;

                Vector3 v = rb.linearVelocity;
                Vector3 av = rb.angularVelocity;
                bool badV = !float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z);
                bool badAv = !float.IsFinite(av.x) || !float.IsFinite(av.y) || !float.IsFinite(av.z);

                if (badV || badAv)
                {
                    errorCount++;
                    Debug.LogError($"<color=red>[NaNDetector][{tag}] BOZUK RIGIDBODY -> Nesne: '{GetPath(rb.transform)}' | Vel: {v} | AngVel: {av}</color>", rb.gameObject);
                }
            }

            // 4. Camera Taraması
            Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
            {
                Camera cam = cams[i];
                if (cam == null) continue;

                bool badFov = !float.IsFinite(cam.fieldOfView) || cam.fieldOfView <= 0.01f || cam.fieldOfView >= 179f;
                bool badClip = !float.IsFinite(cam.nearClipPlane) || !float.IsFinite(cam.farClipPlane) || cam.nearClipPlane <= 0.0001f || cam.farClipPlane <= cam.nearClipPlane;

                if (badFov || badClip)
                {
                    errorCount++;
                    Debug.LogError($"<color=red>[NaNDetector][{tag}] BOZUK KAMERA -> Nesne: '{GetPath(cam.transform)}' | FOV: {cam.fieldOfView} | Near: {cam.nearClipPlane} | Far: {cam.farClipPlane}</color>", cam.gameObject);
                }
            }

            if (errorCount > 0)
            {
                Debug.LogWarning($"<color=yellow>[NaNDetector][{tag}] Toplam {errorCount} bozuk değer tespit edildi!</color>");
            }
        }

        private static string GetPath(Transform t)
        {
            if (t == null) return "(null)";
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}

