using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FingerMonster.Editor
{
    /// <summary>
    /// FingerMonster projesi icin idempotent varlik duzenleme ve Missing Referans tarama Editor araci.
    /// Tum tasima islemleri Unity AssetDatabase API'si uzerinden yapilir.
    /// </summary>
    public static class ProjectOrganizerTool
    {
        private const string MoveLogPath = "Assets/_Project/Docs/MoveLog.csv";

        private struct MoveEntry
        {
            public string OldPath;
            public string NewPath;
            public string Kind;
        }

        private static readonly HashSet<string> UnusedMaterialNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Mat_EyeWhite.mat",
            "Mat_Steel.mat",
            "Mat_TopHat.mat",
            "Mat_Player_0.mat",
            "Mat_Player_1.mat",
            "Mat_Player_2.mat",
            "Mat_Player_3.mat",
            "Mat_PlayerHand_0.mat",
            "Mat_PlayerHand_1.mat",
            "Mat_PlayerHand_2.mat",
            "Mat_PlayerHand_3.mat",
            "Mat_PlayerHead_0.mat",
            "Mat_PlayerHead_1.mat",
            "Mat_PlayerHead_2.mat",
            "Mat_PlayerHead_3.mat"
        };

        private static readonly string[] ButcherFbxFiles =
        {
            "Butcher_Attack.fbx",
            "Butcher_Idle.fbx",
            "Butcher_Taunt1.fbx",
            "Butcher_Taunt2.fbx",
            "Butcher_Walking.fbx",
            "kasap.fbx"
        };

        private static readonly string[] RenderingSettingAssets =
        {
            "DefaultVolumeProfile.asset",
            "GameVolumeProfile.asset",
            "PC_RPAsset.asset",
            "PC_Renderer.asset",
            "Mobile_RPAsset.asset",
            "Mobile_Renderer.asset",
            "UniversalRenderPipelineGlobalSettings.asset"
        };

        private static readonly string[] DebugScriptPaths =
        {
            "Assets/Scripts/Gameplay/NaNDetector.cs",
            "Assets/Scripts/Editor/ForgeAssetInspector.cs",
            "Assets/Scripts/Editor/CharacterDiagnosticTool.cs",
            "Assets/Scripts/Editor/FindAllCharactersInScene.cs",
            "Assets/Scripts/Editor/FindInvalidAABBInScene.cs",
            "Assets/Scripts/Editor/RemoveAllParticlesTool.cs",
            "Assets/Scripts/Editor/ValidateForgeEnvironment.cs"
        };

        private static readonly List<MoveEntry> PendingMoveLog = new List<MoveEntry>();

        [MenuItem("Tools/Proje Düzeni/Missing Referans Tara")]
        public static void ScanMissingReferences()
        {
            int scenesScanned = 0;
            int prefabsScanned = 0;
            int missingScripts = 0;
            int missingObjectRefs = 0;

            string originalScenePath = SceneManager.GetActiveScene().path;

            try
            {
                string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
                foreach (string guid in sceneGuids)
                {
                    string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(scenePath) || !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                        continue;

                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    scenesScanned++;

                    GameObject[] roots = scene.GetRootGameObjects();
                    foreach (GameObject root in roots)
                    {
                        ScanGameObjectHierarchy(root, scenePath, ref missingScripts, ref missingObjectRefs);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MISSING_AUDIT] Sahne taramasi sirasinda hata: {ex}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(originalScenePath) && File.Exists(ToFullPath(originalScenePath)))
                {
                    try
                    {
                        EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MISSING_AUDIT] Orijinal sahne geri yuklenemedi ({originalScenePath}): {ex.Message}");
                    }
                }
            }

            try
            {
                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
                foreach (string guid in prefabGuids)
                {
                    string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                        continue;

                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null)
                        continue;

                    prefabsScanned++;
                    ScanGameObjectHierarchy(prefab, prefabPath, ref missingScripts, ref missingObjectRefs);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MISSING_AUDIT] Prefab taramasi sirasinda hata: {ex}");
            }

            Debug.Log($"[MISSING_AUDIT] ScenesScanned={scenesScanned} PrefabsScanned={prefabsScanned} MissingScripts={missingScripts} MissingObjectRefs={missingObjectRefs}");
        }

        [MenuItem("Tools/Proje Düzeni/Grup 1 - ThirdParty")]
        public static void ApplyGroup1_ThirdParty()
        {
            PendingMoveLog.Clear();
            SafeMoveAsset("Assets/3DForge", "Assets/ThirdParty/3DForge", "Folder");
            FlushMoveLog();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Proje Düzeni/Grup 2 - Audio ve Art")]
        public static void ApplyGroup2_AudioAndArt()
        {
            PendingMoveLog.Clear();

            SafeMoveAsset("Assets/Audio", "Assets/_Project/Audio", "Folder");

            foreach (string fbxName in ButcherFbxFiles)
            {
                SafeMoveAsset($"Assets/Prefabs/{fbxName}", $"Assets/_Project/Art/Characters/Butcher/{fbxName}", "Model");
            }

            SafeMoveAsset("Assets/Characters/Bodyguards", "Assets/_Project/Art/Characters/Bodyguards", "Folder");
            SafeMoveAsset("Assets/Textures/Environment", "Assets/_Project/Art/Textures/Environment", "Folder");

            MoveMaterials(moveUnused: false);

            FlushMoveLog();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Proje Düzeni/Grup 3 - Animasyon ve Prefablar")]
        public static void ApplyGroup3_AnimationAndPrefabs()
        {
            PendingMoveLog.Clear();

            SafeMoveAsset(
                "Assets/Characters/Animations/BodyguardAnimator.controller",
                "Assets/_Project/Animation/Controllers/BodyguardAnimator.controller",
                "AnimatorController");

            MoveMatchingFilesInFolder(
                "Assets/Characters/Animations",
                "*.overrideController",
                "Assets/_Project/Animation/Overrides",
                "AnimatorOverrideController",
                fileName => fileName.StartsWith("AOC_", StringComparison.OrdinalIgnoreCase));

            SafeMoveAsset(
                "Assets/Characters/Animations/UpperBodyEmoteMask.mask",
                "Assets/_Project/Animation/Avatars-Masks/UpperBodyEmoteMask.mask",
                "AvatarMask");

            SafeMoveAsset(
                "Assets/Characters/Animations/Mixamo",
                "Assets/_Project/Animation/Clips/Mixamo",
                "Folder");

            MoveMatchingFilesInFolder(
                "Assets/Characters/Animations",
                "*.fbx",
                "Assets/_Project/Animation/Clips/Other",
                "AnimationFBX");

            MoveMatchingFilesInFolder(
                "Assets/Characters/Animations",
                "*.anim",
                "Assets/_Project/Animation/Clips/Other",
                "AnimationClip");

            SafeMoveAsset("Assets/Characters/Prefabs", "Assets/_Project/Prefabs/Characters", "Folder");

            FlushMoveLog();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Proje Düzeni/Grup 4 - Sahneler, Ayarlar, SO ve Dokümanlar")]
        public static void ApplyGroup4_ScenesSettingsSOAndDocs()
        {
            PendingMoveLog.Clear();

            SafeMoveAsset("Assets/Scenes/MainMenu.unity", "Assets/_Project/Scenes/MainMenu.unity", "Scene");
            SafeMoveAsset("Assets/Scenes/Game.unity", "Assets/_Project/Scenes/Game.unity", "Scene");

            SafeMoveAsset(
                "Assets/Settings/DefaultGameSettings.asset",
                "Assets/_Project/ScriptableObjects/Settings/DefaultGameSettings.asset",
                "ScriptableObject");

            SafeMoveAsset("Assets/Characters/EmoteData", "Assets/_Project/ScriptableObjects/Emotes", "Folder");

            foreach (string assetName in RenderingSettingAssets)
            {
                SafeMoveAsset($"Assets/Settings/{assetName}", $"Assets/_Project/Settings/Rendering/{assetName}", "RenderSettings");
            }

            SafeMoveAsset(
                "Assets/InputSystem_Actions.inputactions",
                "Assets/_Project/Settings/Input/InputSystem_Actions.inputactions",
                "InputActions");

            SafeMoveAsset("Assets/CREDITS.md", "Assets/_Project/Docs/CREDITS.md", "Doc");
            SafeMoveAsset("Assets/ANIMATIONS.md", "Assets/_Project/Docs/ANIMATIONS.md", "Doc");
            SafeMoveAsset("Assets/Scripts/AI/README.md", "Assets/_Project/Docs/AI_README.md", "Doc");
            SafeMoveAsset("Assets/Scripts/Networking/README.md", "Assets/_Project/Docs/Networking_README.md", "Doc");

            FlushMoveLog();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Proje Düzeni/Grup 5 - Arşiv")]
        public static void ApplyGroup5_Archive()
        {
            PendingMoveLog.Clear();

            SafeMoveAsset("Assets/BodyGuards", "Assets/_Archive/BodyGuards_Original", "Folder");
            SafeMoveAsset("Assets/TutorialInfo", "Assets/_Archive/TutorialInfo", "Folder");
            SafeMoveAsset("Assets/Readme.asset", "Assets/_Archive/TutorialInfo/Readme.asset", "Asset");
            SafeMoveAsset("Assets/Scenes/SampleScene.unity", "Assets/_Archive/SampleScene/SampleScene.unity", "Scene");
            SafeMoveAsset("Assets/Settings/SampleSceneProfile.asset", "Assets/_Archive/SampleScene/SampleSceneProfile.asset", "Asset");

            MoveMaterials(moveUnused: true);

            foreach (string scriptPath in DebugScriptPaths)
            {
                string fileName = Path.GetFileName(scriptPath);
                SafeMoveAsset(scriptPath, $"Assets/_Archive/Debug-Scripts/Editor/{fileName}", "DebugScript");
            }

            FlushMoveLog();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Proje Düzeni/Grup 6 - Scriptler")]
        public static void ApplyGroup6_Scripts()
        {
            PendingMoveLog.Clear();

            SafeMoveAsset("Assets/Scripts/AI", "Assets/_Project/Scripts/AI", "Folder");
            SafeMoveAsset("Assets/Scripts/Audio", "Assets/_Project/Scripts/Audio", "Folder");
            SafeMoveAsset("Assets/Scripts/Core", "Assets/_Project/Scripts/Core", "Folder");
            SafeMoveAsset("Assets/Scripts/Networking", "Assets/_Project/Scripts/Networking", "Folder");
            SafeMoveAsset("Assets/Scripts/UI", "Assets/_Project/Scripts/UI", "Folder");

            SafeMoveAsset("Assets/Scripts/Gameplay/CardData.cs", "Assets/_Project/Scripts/Cards/CardData.cs", "Script");
            SafeMoveAsset("Assets/Scripts/Gameplay/CardSelectionController.cs", "Assets/_Project/Scripts/Cards/CardSelectionController.cs", "Script");

            SafeMoveAsset("Assets/Scripts/Gameplay/GhostScareAndThrowSystem.cs", "Assets/_Project/Scripts/Ghost/GhostScareAndThrowSystem.cs", "Script");
            SafeMoveAsset("Assets/Scripts/Data/ThrowableItemData.cs", "Assets/_Project/Scripts/Ghost/ThrowableItemData.cs", "Script");

            SafeMoveAsset("Assets/Scripts/Gameplay/ButcherPlaceholder.cs", "Assets/_Project/Scripts/Butcher/ButcherPlaceholder.cs", "Script");
            SafeMoveAsset("Assets/Scripts/Gameplay/ExecutionReflectionSystem.cs", "Assets/_Project/Scripts/Butcher/ExecutionReflectionSystem.cs", "Script");
            SafeMoveAsset("Assets/Scripts/Gameplay/ExecutionWeapons.cs", "Assets/_Project/Scripts/Butcher/ExecutionWeapons.cs", "Script");
            SafeMoveAsset("Assets/Scripts/Data/WeaponData.cs", "Assets/_Project/Scripts/Butcher/WeaponData.cs", "Script");

            SafeMoveAsset("Assets/Scripts/Player/CharacterEmoteData.cs", "Assets/_Project/Scripts/Emotes/CharacterEmoteData.cs", "Script");
            SafeMoveAsset("Assets/Scripts/Player/PlayerCameraController.cs", "Assets/_Project/Scripts/CameraSystem/PlayerCameraController.cs", "Script");

            MoveMatchingFilesInFolder("Assets/Scripts/Gameplay", "*.cs", "Assets/_Project/Scripts/Gameplay", "Script");
            MoveMatchingFilesInFolder("Assets/Scripts/Player", "*.cs", "Assets/_Project/Scripts/Players", "Script");

            // Editor scriptleri tasinirken ProjectOrganizerTool.cs en sonda tasinir.
            MoveMatchingFilesInFolder(
                "Assets/Scripts/Editor",
                "*.cs",
                "Assets/_Project/Scripts/Editor",
                "EditorScript",
                fileName => !string.Equals(fileName, "ProjectOrganizerTool.cs", StringComparison.OrdinalIgnoreCase));

            SafeMoveAsset(
                "Assets/Scripts/Editor/ProjectOrganizerTool.cs",
                "Assets/_Project/Scripts/Editor/ProjectOrganizerTool.cs",
                "EditorScript");

            FlushMoveLog();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Proje Düzeni/Uygula")]
        public static void ApplyAll()
        {
            ApplyGroup1_ThirdParty();
            ApplyGroup2_AudioAndArt();
            ApplyGroup3_AnimationAndPrefabs();
            ApplyGroup4_ScenesSettingsSOAndDocs();
            ApplyGroup5_Archive();
            ApplyGroup6_Scripts();

            CleanupEmptyFolders();
            AssetDatabase.Refresh();

            Debug.Log("[ProjectOrganizerTool] Tum gruplar (1-6) basariyla uygulandi.");
        }

        private static void ScanGameObjectHierarchy(GameObject root, string assetPath, ref int missingScripts, ref int missingObjectRefs)
        {
            if (root == null)
                return;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in transforms)
            {
                GameObject go = t.gameObject;
                string hierarchyPath = GetHierarchyPath(t);

                int missingScriptCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missingScriptCount > 0)
                {
                    missingScripts += missingScriptCount;
                    Debug.LogWarning($"[MISSING_SCRIPT] Path={assetPath} GameObject={hierarchyPath} Count={missingScriptCount}");
                }

                Component[] components = go.GetComponents<Component>();
                foreach (Component comp in components)
                {
                    if (comp == null)
                        continue;

                    SerializedObject so = new SerializedObject(comp);
                    SerializedProperty prop = so.GetIterator();
                    while (prop.NextVisible(true))
                    {
                        if (prop.propertyType == SerializedPropertyType.ObjectReference &&
                            prop.objectReferenceValue == null &&
                            prop.objectReferenceInstanceIDValue != 0)
                        {
                            missingObjectRefs++;
                            Debug.LogWarning($"[MISSING_OBJECT_REF] Path={assetPath} GameObject={hierarchyPath} Component={comp.GetType().Name} Property={prop.propertyPath}");
                        }
                    }
                }
            }
        }

        private static string GetHierarchyPath(Transform t)
        {
            if (t == null)
                return string.Empty;

            var parts = new List<string>();
            Transform current = t;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static void MoveMaterials(bool moveUnused)
        {
            const string srcFolder = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(srcFolder))
                return;

            string fullSrc = ToFullPath(srcFolder);
            if (!Directory.Exists(fullSrc))
                return;

            string[] files = Directory.GetFiles(fullSrc, "*.mat", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string fullFile in files)
            {
                string fileName = Path.GetFileName(fullFile);
                bool isUnused = UnusedMaterialNames.Contains(fileName);

                if (moveUnused && isUnused)
                {
                    SafeMoveAsset($"{srcFolder}/{fileName}", $"Assets/_Archive/UnusedMaterials/{fileName}", "UnusedMaterial");
                }
                else if (!moveUnused && !isUnused)
                {
                    SafeMoveAsset($"{srcFolder}/{fileName}", $"Assets/_Project/Art/Materials/{fileName}", "Material");
                }
            }
        }

        private static void MoveMatchingFilesInFolder(
            string srcFolder,
            string searchPattern,
            string dstFolder,
            string kind,
            Func<string, bool> filter = null)
        {
            if (!AssetDatabase.IsValidFolder(srcFolder))
                return;

            string fullSrc = ToFullPath(srcFolder);
            if (!Directory.Exists(fullSrc))
                return;

            string[] files = Directory.GetFiles(fullSrc, searchPattern, SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string fullFile in files)
            {
                string fileName = Path.GetFileName(fullFile);
                if (filter != null && !filter(fileName))
                    continue;

                SafeMoveAsset($"{srcFolder}/{fileName}", $"{dstFolder}/{fileName}", kind);
            }
        }

        private static void CleanupEmptyFolders()
        {
            string[] candidateFolders =
            {
                "Assets/Characters/Animations",
                "Assets/Scripts/Data",
                "Assets/Scripts/Gameplay",
                "Assets/Scripts/Player",
                "Assets/Scripts/Editor",
                "Assets/Prefabs",
                "Assets/Materials",
                "Assets/Textures",
                "Assets/Audio",
                "Assets/Characters",
                "Assets/Scenes",
                "Assets/Settings",
                "Assets/Scripts"
            };

            foreach (string folder in candidateFolders)
            {
                DeleteFolderIfEmpty(folder);
            }
        }

        private static void DeleteFolderIfEmpty(string folderAssetPath)
        {
            if (!AssetDatabase.IsValidFolder(folderAssetPath))
                return;

            string fullPath = ToFullPath(folderAssetPath);
            if (!Directory.Exists(fullPath))
                return;

            string[] entries = Directory.GetFileSystemEntries(fullPath);
            foreach (string entry in entries)
            {
                if (entry.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Bos olmayan alt klasor varsa veya dosya varsa silme
                return;
            }

            try
            {
                if (AssetDatabase.DeleteAsset(folderAssetPath))
                {
                    Debug.Log($"[ProjectOrganizerTool] Bos klasor silindi: {folderAssetPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProjectOrganizerTool] Bos klasor silinemedi ({folderAssetPath}): {ex.Message}");
            }
        }

        public static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return;

            folderPath = folderPath.Replace("\\", "/").TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            int lastSlash = folderPath.LastIndexOf('/');
            if (lastSlash <= 0)
                return;

            string parent = folderPath.Substring(0, lastSlash);
            string folderName = folderPath.Substring(lastSlash + 1);

            EnsureFolder(parent);

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string guid = AssetDatabase.CreateFolder(parent, folderName);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogError($"[ProjectOrganizerTool] Klasor olusturulamadi: {folderPath}");
                }
            }
        }

        public static void SafeMoveAsset(string src, string dst, string kind)
        {
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dst))
                return;

            src = src.Replace("\\", "/");
            dst = dst.Replace("\\", "/");

            if (string.Equals(src, dst, StringComparison.OrdinalIgnoreCase))
                return;

            if (!AssetExists(src))
                return;

            if (AssetExists(dst))
            {
                Debug.LogWarning($"[ProjectOrganizerTool] Hedef zaten mevcut, atlaniyor: {src} -> {dst}");
                return;
            }

            string dstParent = GetParentFolder(dst);
            EnsureFolder(dstParent);

            try
            {
                string error = AssetDatabase.MoveAsset(src, dst);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"[ProjectOrganizerTool] Tasima hatasi ({src} -> {dst}): {error}");
                    return;
                }

                PendingMoveLog.Add(new MoveEntry
                {
                    OldPath = src,
                    NewPath = dst,
                    Kind = kind
                });
                Debug.Log($"[ProjectOrganizerTool] Tasindi ({kind}): {src} -> {dst}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProjectOrganizerTool] Tasima exception ({src} -> {dst}): {ex}");
            }
        }

        private static bool AssetExists(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return true;

            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                return true;

            string fullPath = ToFullPath(assetPath);
            return File.Exists(fullPath) || Directory.Exists(fullPath);
        }

        private static string GetParentFolder(string assetPath)
        {
            int lastSlash = assetPath.LastIndexOf('/');
            return lastSlash > 0 ? assetPath.Substring(0, lastSlash) : "Assets";
        }

        private static string ToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        }

        private static void FlushMoveLog()
        {
            if (PendingMoveLog.Count == 0)
                return;

            try
            {
                EnsureFolder(GetParentFolder(MoveLogPath));
                string fullLogPath = ToFullPath(MoveLogPath);
                bool needsHeader = !File.Exists(fullLogPath);

                var sb = new StringBuilder();
                if (needsHeader)
                {
                    sb.AppendLine("EskiYol,YeniYol,Tur");
                }

                foreach (MoveEntry entry in PendingMoveLog)
                {
                    sb.AppendLine($"{EscapeCsv(entry.OldPath)},{EscapeCsv(entry.NewPath)},{EscapeCsv(entry.Kind)}");
                }

                File.AppendAllText(fullLogPath, sb.ToString(), Encoding.UTF8);
                PendingMoveLog.Clear();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProjectOrganizerTool] MoveLog.csv yazma hatasi: {ex}");
            }
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Contains(",") || value.Contains("\""))
                return $"\"{value.Replace("\"", "\"\"")}\"";

            return value;
        }
    }
}
