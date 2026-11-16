using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace FingerMonster.Editor
{
    public static class FindInvalidAABBInScene
    {
        [MenuItem("FingerMonster/Diagnose Invalid AABB In Game Scene")]
        public static void DiagnoseScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("Game.unity açılamadı!");
                return;
            }

            Debug.Log("<color=cyan>=== [INVALID AABB / NaN VE HATA DETAYLI SAHNE TARAMASI BAŞLADI] ===</color>");

            int invalidCount = 0;

            // 1. Tüm Transform'lar
            Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            Debug.Log($"[Tarama] Toplam Transform Sayısı: {allTransforms.Length}");
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                Vector3 p = t.position;
                Quaternion r = t.rotation;
                Vector3 s = t.localScale;

                bool badP = !float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z);
                bool badR = !float.IsFinite(r.x) || !float.IsFinite(r.y) || !float.IsFinite(r.z) || !float.IsFinite(r.w) || (r.x == 0 && r.y == 0 && r.z == 0 && r.w == 0);
                bool badS = !float.IsFinite(s.x) || !float.IsFinite(s.y) || !float.IsFinite(s.z) || s.x == 0 || s.y == 0 || s.z == 0;

                if (badP || badR || badS)
                {
                    invalidCount++;
                    Debug.LogError($"<color=red>[BOZUK TRANSFORM] '{GetPath(t)}' | Pos: {p} (Bad:{badP}) | Rot: {r} (Bad:{badR}) | Scale: {s} (Bad:{badS})</color>", t.gameObject);
                }
            }

            // 2. Tüm Renderer'lar ve Bounds
            Renderer[] allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            Debug.Log($"[Tarama] Toplam Renderer Sayısı: {allRenderers.Length}");
            foreach (Renderer rnd in allRenderers)
            {
                if (rnd == null) continue;
                string path = GetPath(rnd.transform);

                try
                {
                    Bounds b = rnd.bounds;
                    Vector3 c = b.center;
                    Vector3 e = b.extents;
                    Vector3 sz = b.size;

                    bool badC = !float.IsFinite(c.x) || !float.IsFinite(c.y) || !float.IsFinite(c.z);
                    bool badE = !float.IsFinite(e.x) || !float.IsFinite(e.y) || !float.IsFinite(e.z) || e.x < 0 || e.y < 0 || e.z < 0;
                    bool badSz = !float.IsFinite(sz.x) || !float.IsFinite(sz.y) || !float.IsFinite(sz.z);

                    if (badC || badE || badSz)
                    {
                        invalidCount++;
                        Debug.LogError($"<color=red>[BOZUK RENDERER BOUNDS] '{path}' ({rnd.GetType().Name}) | Center: {c} | Extents: {e} | Size: {sz}</color>", rnd.gameObject);
                    }
                }
                catch (System.Exception ex)
                {
                    invalidCount++;
                    Debug.LogError($"<color=red>[RENDERER BOUNDS EXCEPTION] '{path}': {ex.Message}</color>", rnd.gameObject);
                }

                if (rnd is SkinnedMeshRenderer smr)
                {
                    Bounds lb = smr.localBounds;
                    bool badLb = !float.IsFinite(lb.center.x) || !float.IsFinite(lb.extents.x) || lb.extents.sqrMagnitude < 0.000001f;
                    if (badLb)
                    {
                        invalidCount++;
                        Debug.LogError($"<color=red>[BOZUK SMR LOCAL BOUNDS] '{path}' | LocalBounds: {lb}</color>", smr.gameObject);
                    }
                }
            }

            // 3. Tüm Kameralar
            Camera[] allCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            Debug.Log($"[Tarama] Toplam Kamera Sayısı: {allCameras.Length}");
            foreach (Camera cam in allCameras)
            {
                if (cam == null) continue;
                string path = GetPath(cam.transform);
                bool badFov = !float.IsFinite(cam.fieldOfView) || cam.fieldOfView <= 0.01f || cam.fieldOfView >= 179f;
                bool badClip = !float.IsFinite(cam.nearClipPlane) || !float.IsFinite(cam.farClipPlane) || cam.nearClipPlane <= 0.0001f || cam.farClipPlane <= cam.nearClipPlane;

                if (badFov || badClip)
                {
                    invalidCount++;
                    Debug.LogError($"<color=red>[BOZUK KAMERA] '{path}' | FOV: {cam.fieldOfView} | Near: {cam.nearClipPlane} | Far: {cam.farClipPlane}</color>", cam.gameObject);
                }
            }

            // 4. Tüm Işıklar
            Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            Debug.Log($"[Tarama] Toplam Işık Sayısı: {allLights.Length}");
            foreach (Light l in allLights)
            {
                if (l == null) continue;
                string path = GetPath(l.transform);
                bool badRange = !float.IsFinite(l.range) || l.range <= 0.001f;
                bool badInt = !float.IsFinite(l.intensity) || l.intensity < 0;

                if (badRange || badInt)
                {
                    invalidCount++;
                    Debug.LogError($"<color=red>[BOZUK IŞIK] '{path}' | Range: {l.range} | Intensity: {l.intensity}</color>", l.gameObject);
                }
            }

            // 5. Tüm Prefab'ların (Karakterler, Eller vb.) Taraması
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets/_Project/Prefabs/Characters", "Assets/ThirdParty/3DForge/Prefabs" });
            Debug.Log($"[Tarama] Toplam Taranan Proje Prefab Sayısı: {prefabGuids.Length}");
            foreach (string guid in prefabGuids)
            {
                string pPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pPath);
                if (prefab == null) continue;

                var pRenderers = prefab.GetComponentsInChildren<Renderer>(true);
                foreach (var r in pRenderers)
                {
                    if (r is ParticleSystemRenderer psr)
                    {
                        if (psr.renderMode == ParticleSystemRenderMode.Stretch)
                        {
                            invalidCount++;
                            Debug.LogError($"<color=red>[STRETCHED BILLBOARD PREFAB] '{pPath}' -> '{r.name}' parçacık renderMode Stretched Billboard!</color>");
                        }
                        invalidCount++;
                        Debug.LogError($"<color=red>[PARTICLE SYSTEM DETECTED] '{pPath}' -> '{r.name}' parçacık sistemi tespit edildi! Oyunda hiçbir partikül olmamalı.</color>");
                    }
                    if (r is SkinnedMeshRenderer smr)
                    {
                        if (smr.sharedMesh == null)
                        {
                            invalidCount++;
                            Debug.LogError($"<color=red>[NULL MESH SMR PREFAB] '{pPath}' -> '{r.name}' sharedMesh NULL!</color>");
                        }
                    }
                }
            }

            Debug.Log($"<color={(invalidCount == 0 ? "green" : "red")}>=== [TARAMA BİTTİ | Toplam Bulunan Hata: {invalidCount}] ===</color>");
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
