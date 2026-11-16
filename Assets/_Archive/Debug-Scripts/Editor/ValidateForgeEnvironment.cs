using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace FingerMonster.EditorTools
{
    public static class ValidateForgeEnvironment
    {
        private static readonly HashSet<string> UnityPrimitives = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "Cube", "Sphere", "Cylinder", "Capsule", "Plane", "Quad"
        };

        [MenuItem("FingerMonster/Validate Forge Environment")]
        public static bool ValidateCurrentSceneMenu()
        {
            return ValidateScene(out string report, logToConsole: true);
        }

        public static bool ValidateScene(out string report, bool logToConsole = true)
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene == null || activeScene.name != "Game")
            {
                if (System.IO.File.Exists("Assets/_Project/Scenes/Game.unity"))
                {
                    EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Single);
                }
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine("                   FINGERMONSTER FORGE ENVIRONMENT VALIDATION REPORT            ");
            sb.AppendLine("================================================================================");

            MeshFilter[] allMeshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
            sb.AppendLine($"[Scan] Toplam Sahnede Bulunan MeshFilter Sayısı: {allMeshFilters.Length}");

            List<string> envObjectsReport = new List<string>();
            List<string> exceptionObjectsReport = new List<string>();
            List<string> primitiveViolations = new List<string>();
            List<string> nonForgeViolations = new List<string>();

            int envCount = 0;
            int exceptionCount = 0;

            foreach (var mf in allMeshFilters)
            {
                if (mf == null) continue;
                GameObject go = mf.gameObject;
                Mesh mesh = mf.sharedMesh;
                string meshName = mesh != null ? mesh.name : "(Null Mesh)";
                string assetPath = mesh != null ? AssetDatabase.GetAssetPath(mesh) : "(None)";

                if (IsGameLogicException(go))
                {
                    exceptionCount++;
                    exceptionObjectsReport.Add($"[İstisna / Oyun Mantığı] {GetHierarchyPath(go)} | Mesh: {meshName} | Path: {assetPath}");
                    continue;
                }

                envCount++;
                string entry = $"[Ortam / 3DForge] {GetHierarchyPath(go)} | Mesh: {meshName} | Path: {assetPath}";
                envObjectsReport.Add(entry);

                // 1. İlkel Unity Mesh Kontrolü
                if (mesh != null && (UnityPrimitives.Contains(meshName) || meshName.StartsWith("Cube") || meshName.StartsWith("Quad") || meshName.StartsWith("Cylinder") || meshName.StartsWith("Sphere") || meshName.StartsWith("Capsule") || meshName.StartsWith("Plane")))
                {
                    // Built-in primitive mesh check (asset path empty or unity default resources)
                    if (string.IsNullOrEmpty(assetPath) || assetPath.Contains("unity default resources") || assetPath.Contains("unity_builtin_extra"))
                    {
                        primitiveViolations.Add($"[İLKEL MESH YASAĞI İHLALİ] {GetHierarchyPath(go)} nesnesi Unity ilkel mesh'i ({meshName}) kullanıyor!");
                    }
                }

                // 2. 3DForge Kaynak Kontrolü
                if (string.IsNullOrEmpty(assetPath) || !assetPath.Replace('\\', '/').StartsWith("Assets/ThirdParty/3DForge"))
                {
                    nonForgeViolations.Add($"[3DFORGE DIŞI KAYNAK İHLALİ] {GetHierarchyPath(go)} nesnesinin mesh'i ({meshName}) Assets/ThirdParty/3DForge klasöründen değil! Yol: {assetPath}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"--- 1. ONAYLI OYUN MANTIĞI İSTİSNALARI ({exceptionCount} Adet) ---");
            foreach (var e in exceptionObjectsReport)
            {
                sb.AppendLine("  " + e);
            }

            sb.AppendLine();
            sb.AppendLine($"--- 2. ORTAM NESNELERİ (3DFORGE KONTROLÜ) ({envCount} Adet) ---");
            foreach (var o in envObjectsReport)
            {
                sb.AppendLine("  " + o);
            }

            sb.AppendLine();
            sb.AppendLine("--- 3. DOĞRULAMA VE İHLAL RAPORU ---");

            bool hasViolations = primitiveViolations.Count > 0 || nonForgeViolations.Count > 0;
            if (!hasViolations)
            {
                sb.AppendLine(">> [BAŞARILI - PASSED] Ortamda sıfır (0) Unity ilkel primitive mesh bulundu.");
                sb.AppendLine(">> [BAŞARILI - PASSED] Ortamdaki TÜM nesnelerin mesh kaynakları Assets/ThirdParty/3DForge paketine aittir.");
                sb.AppendLine(">> Kural Kontrolü: 100% UYUMLU.");
            }
            else
            {
                sb.AppendLine($">> [BAŞARISIZ - FAILED] Toplam {primitiveViolations.Count} ilkel mesh ve {nonForgeViolations.Count} paket dışı ihlal tespit edildi!");
                foreach (var v in primitiveViolations) sb.AppendLine("  * " + v);
                foreach (var v in nonForgeViolations) sb.AppendLine("  * " + v);
            }

            sb.AppendLine("================================================================================");
            report = sb.ToString();

            if (logToConsole)
            {
                if (hasViolations)
                {
                    Debug.LogError(report);
                }
                else
                {
                    Debug.Log(report);
                }
            }

            return !hasViolations;
        }

        private static bool IsGameLogicException(GameObject go)
        {
            Transform t = go.transform;
            while (t != null)
            {
                string name = t.name;
                if (name == "TypewriterMachine" || name.StartsWith("Key_") || name == "Body" || name == "RearHousing" || name == "CarriageRoot" || name == "PaperRoll" || name == "TypewriterPaper")
                    return true;

                if (name.StartsWith("Avatar_") || name.Contains("Bodyguard") || name.Contains("SkelMesh"))
                    return true;

                if (name == "ButcherSecretPlaceholder" || name == "ButcherCapsule" || name.StartsWith("Butcher") || name.StartsWith("Weapon_"))
                    return true;

                if (name == "CameraRoot" || name == "Main Camera" || name == "SilhouetteCamera" || name == "PaperPile" || name == "Lighting" || name == "ExecutionReflectionSystem")
                    return true;

                t = t.parent;
            }
            return false;
        }

        private static string GetHierarchyPath(GameObject go)
        {
            string path = go.name;
            Transform t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }
    }
}

