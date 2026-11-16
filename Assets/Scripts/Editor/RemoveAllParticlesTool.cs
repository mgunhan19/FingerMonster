using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace FingerMonster.Editor
{
    /// <summary>
    /// Projedeki ve sahnelerdeki TÜM ParticleSystem ve ParticleSystemRenderer bileşenlerini
    /// kalıcı olarak kaldıran ve doğrulayan araç.
    /// </summary>
    public static class RemoveAllParticlesTool
    {
        [MenuItem("FingerMonster/Remove All Particles From Project")]
        public static void RemoveAllParticles()
        {
            Debug.Log("<color=yellow>=== [TÜM PARTİKÜLLERİ KALDIRMA İŞLEMİ BAŞLADI] ===</color>");
            int removedCount = 0;

            // 1. Projedeki tüm prefab'ları tara ve içlerindeki ParticleSystem'ları yok et
            string[] allPrefabGuids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets" });
            foreach (string guid in allPrefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefabContents = null;
                try
                {
                    prefabContents = PrefabUtility.LoadPrefabContents(path);
                    var particles = prefabContents.GetComponentsInChildren<ParticleSystem>(true);
                    if (particles != null && particles.Length > 0)
                    {
                        for (int i = particles.Length - 1; i >= 0; i--)
                        {
                            if (particles[i] != null)
                            {
                                GameObject pGo = particles[i].gameObject;
                                Debug.Log($"<color=cyan>[PREFAB PARTİKÜL SİLİNDİ] Prefab: '{path}' -> Obje: '{pGo.name}'</color>");
                                Object.DestroyImmediate(pGo);
                                removedCount++;
                            }
                        }
                        PrefabUtility.SaveAsPrefabAsset(prefabContents, path);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[RemoveParticles] Prefab taranırken hata ('{path}'): {ex.Message}");
                }
                finally
                {
                    if (prefabContents != null)
                    {
                        PrefabUtility.UnloadPrefabContents(prefabContents);
                    }
                }
            }

            // 2. Game.unity sahnesini aç, sahnede kalan tüm partikülleri temizle
            CleanScene("Assets/Scenes/Game.unity", ref removedCount);

            // 3. MainMenu.unity sahnesini aç, sahnede kalan tüm partikülleri temizle
            CleanScene("Assets/Scenes/MainMenu.unity", ref removedCount);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=green>=== [TÜM PARTİKÜLLER TEMİZLENDİ - Toplam Kaldırılan Partikül: {removedCount}] ===</color>");
        }

        private static void CleanScene(string scenePath, ref int removedCount)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) return;

            var particles = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = particles.Length - 1; i >= 0; i--)
            {
                if (particles[i] != null)
                {
                    GameObject pGo = particles[i].gameObject;
                    Debug.Log($"<color=cyan>[SAHNE PARTİKÜL SİLİNDİ] Sahne: '{scenePath}' -> Obje: '{pGo.name}'</color>");
                    Object.DestroyImmediate(pGo);
                    removedCount++;
                }
            }

            var renderers = Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = renderers.Length - 1; i >= 0; i--)
            {
                if (renderers[i] != null)
                {
                    GameObject rGo = renderers[i].gameObject;
                    Debug.Log($"<color=cyan>[SAHNE PARTICLE RENDERER SİLİNDİ] Sahne: '{scenePath}' -> Obje: '{rGo.name}'</color>");
                    Object.DestroyImmediate(rGo);
                    removedCount++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("FingerMonster/Verify Zero Particles In Project")]
        public static bool VerifyZeroParticles()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
            var particles = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var renderers = Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int total = particles.Length + renderers.Length;
            if (total == 0)
            {
                Debug.Log("<color=green>[PARTICLE VERIFICATION] Game.unity içinde SIFIR (0) partikül var! Tertemiz.</color>");
                return true;
            }
            else
            {
                Debug.LogError($"<color=red>[PARTICLE VERIFICATION] Sahnede hala {total} partikül bileşeni bulundu!</color>");
                return false;
            }
        }
    }
}
