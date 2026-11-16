using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FingerMonster.Player;

namespace FingerMonster.Editor
{
    /// <summary>
    /// Assets/_Archive/BodyGuards_Original paketindeki 4 karakteri orijinal dosyalarına dokunmadan
    /// Assets/_Project/Art/Characters altına kopyalar, URP materyallerini oluşturur, Humanoid Avatar'ı doğrular,
    /// kopan el mesh/prefab'larını çıkarır, Humanoid animasyon klipleri + Animator Controller +
    /// 4 Override Controller üretir ve 4 oyuncu karakter prefab'ını oluşturur.
    /// </summary>
    public static class BodyguardSetupTool
    {
        private const string SourceRoot = "Assets/_Archive/BodyGuards_Original";
        private const string DestRoot = "Assets/_Project/Art/Characters";
        private const string BodyguardsRoot = "Assets/_Project/Art/Characters/Bodyguards";
        private const string MeshesDir = "Assets/_Project/Art/Characters/Bodyguards/Meshes";
        private const string TexturesDir = "Assets/_Project/Art/Characters/Bodyguards/Textures";
        private const string MaterialsDir = "Assets/_Project/Art/Characters/Bodyguards/Materials";
        private const string AnimationsDir = "Assets/_Project/Animation/Clips/Other";
        private const string ControllersDir = "Assets/_Project/Animation/Controllers";
        private const string OverridesDir = "Assets/_Project/Animation/Overrides";
        private const string MixamoDir = "Assets/_Project/Animation/Clips/Mixamo";
        private const string EmoteDataDir = "Assets/_Project/ScriptableObjects/Emotes";
        private const string UpperBodyMaskPath = "Assets/_Project/Animation/Avatars-Masks/UpperBodyEmoteMask.mask";
        private const string PrefabsDir = "Assets/_Project/Prefabs/Characters";
        private const string SeveredHandsDir = "Assets/_Project/Prefabs/Characters/SeveredHands";
        private const string SeveredHandMeshesDir = "Assets/_Project/Prefabs/Characters/SeveredHands/Meshes";
        private const string ControllerPath = "Assets/_Project/Animation/Controllers/BodyguardAnimator.controller";

        private struct CharacterSpec
        {
            public CharacterArchetype Archetype;
            public string KeyName;       // Feg, Kenan, Feyzi, MMG
            public string FbxFileName;   // SkelMesh_Bodyguard_02.fbx
            public int PrimaryMatIndex;  // 1, 2, 3
        }

        private static readonly CharacterSpec[] Specs = new CharacterSpec[]
        {
            new CharacterSpec { Archetype = CharacterArchetype.Kabadayi, KeyName = "Feg",   FbxFileName = "SkelMesh_Bodyguard_02.fbx", PrimaryMatIndex = 2 },
            new CharacterSpec { Archetype = CharacterArchetype.Korkak,   KeyName = "Kenan", FbxFileName = "SkelMesh_Bodyguard_01.fbx", PrimaryMatIndex = 1 },
            new CharacterSpec { Archetype = CharacterArchetype.Sinsi,    KeyName = "Feyzi", FbxFileName = "SkelMesh_Bodyguard_03.fbx", PrimaryMatIndex = 3 },
            new CharacterSpec { Archetype = CharacterArchetype.Sovmen,   KeyName = "MMG",   FbxFileName = "SkelMesh_Bodyguard_04.fbx", PrimaryMatIndex = 1 },
        };

        private static int[] mIdx; // Cached muscle indices
        private static int mLeftUpperLegFront, mRightUpperLegFront;
        private static int mLeftLowerLegStretch, mRightLowerLegStretch;
        private static int mLeftFootUpDown, mRightFootUpDown;
        private static int mSpineFrontBack, mChestFrontBack, mHeadNod, mHeadTurn, mHeadTilt;
        private static int mLeftArmDownUp, mRightArmDownUp;
        private static int mLeftArmFrontBack, mRightArmFrontBack;
        private static int mLeftForearmStretch, mRightForearmStretch;
        private static int mLeftHandDownUp, mRightHandDownUp;

        // Verified signs for Bodyguard humanoid rig
        private static float signThighForward = 1f;
        private static float signKneeBend = -1f;
        private static float signArmDown = -1f;
        private static float signArmForward = 1f;
        private static float signElbowBend = -1f;
        private static float signSpineForward = 1f;
        private static float signHeadDown = -1f;
        private static float calibratedSitRootY = 0.54f;

        [MenuItem("FingerMonster/1. Setup Bodyguard Characters (Full Pipeline)")]
        public static void RunFullSetup()
        {
            if (!AssetDatabase.IsValidFolder(SourceRoot))
            {
                Debug.LogError($"[BodyguardSetup] Kaynak paket klasörü bulunamadı: {SourceRoot}");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Bodyguard Setup", "Klasörler hazırlanıyor ve varlıklar kopyalanıyor...", 0.1f);
                EnsureDirectories();
                CopySourceAssetsUntouched();

                EditorUtility.DisplayProgressBar("Bodyguard Setup", "Dokular ve URP/Lit materyalleri oluşturuluyor...", 0.25f);
                ConfigureCopiedTextures();
                Dictionary<int, Material> urpMaterials = CreateOrUpdateURPMaterials();
                EnsureProjectMaterialsAreURP();

                EditorUtility.DisplayProgressBar("Bodyguard Setup", "Humanoid Rig ve Avatar yapılandırılıyor...", 0.40f);
                ConfigureHumanoidModelsAndRemap(urpMaterials);

                EditorUtility.DisplayProgressBar("Bodyguard Setup", "Humanoid oturma pozisyonu kalibre ediliyor...", 0.55f);
                CalibrateMuscleSigns();

                EditorUtility.DisplayProgressBar("Bodyguard Setup", "Animasyon klipleri ve Animator Controller üretiliyor...", 0.70f);
                AnimatorController controller = BuildAnimationsAndController();
                CreateOrUpdateCharacterEmoteDatas();
                CreateMixamoReadme();

                EditorUtility.DisplayProgressBar("Bodyguard Setup", "Kopan el mesh/prefab'ları ve karakter prefab'ları kuruluyor...", 0.85f);
                Material bandageMat = GetOrCreateBandageMaterial();
                for (int i = 0; i < Specs.Length; i++)
                {
                    BuildCharacterAndSeveredHands(Specs[i], urpMaterials, controller, bandageMat);
                }

                ImportAndRemapMixamoAnimations();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[BodyguardSetup] TÜM ADIMLAR BAŞARIYLA TAMAMLANDI! (Feg, Kenan, Feyzi, MMG prefab'ları ve URP/Humanoid varlıkları hazır.)");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static void EnsurePrefabsExist()
        {
            foreach (var spec in Specs)
            {
                string p = $"{PrefabsDir}/PlayerCharacter_{spec.KeyName}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(p) == null)
                {
                    RunFullSetup();
                    return;
                }
            }
        }

        [MenuItem("FingerMonster/2. Import & Remap Mixamo Animations")]
        public static void ImportAndRemapMixamoAnimations()
        {
            EnsureDirectories();
            CreateOrUpdateCharacterEmoteDatas();
            CreateMixamoReadme();

            string refFbxPath = $"{MeshesDir}/SkelMesh_Bodyguard_01.fbx";
            GameObject refFbx = AssetDatabase.LoadAssetAtPath<GameObject>(refFbxPath);
            Avatar refAvatar = GetModelAvatar(refFbxPath);

            if (refFbx == null || refAvatar == null || !refAvatar.isValid)
            {
                Debug.LogError("[MixamoImport] Önce '1. Setup Bodyguard Characters (Full Pipeline)' çalıştırın.");
                return;
            }

            // Hem Animations kökündeki (kullanıcının eklediği 9 FBX) hem Mixamo/ alt klasörlerindeki FBX'leri tara
            string[] allFbxGuids = AssetDatabase.FindAssets("t:Model", new[] { AnimationsDir });
            Dictionary<string, AnimationClip> discoveredByName = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, AnimationClip> discoveredByRelPath = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
            int processedFbx = 0;

            foreach (string guid in allFbxGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Sadece Mixamo kaynaklı animasyon FBX'lerini işle
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                string fileNameNoExt = Path.GetFileNameWithoutExtension(path);
                bool isLoopingPose = fileNameNoExt.Equals("Sitting", StringComparison.OrdinalIgnoreCase)
                    || fileNameNoExt.Equals("Sitting_Idle_2H", StringComparison.OrdinalIgnoreCase)
                    || fileNameNoExt.Equals("Looking", StringComparison.OrdinalIgnoreCase)
                    || fileNameNoExt.Equals("Thinking", StringComparison.OrdinalIgnoreCase);

                bool changed = false;
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    changed = true;
                }
                if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    changed = true;
                }
                if (importer.sourceAvatar != null)
                {
                    importer.sourceAvatar = null;
                    changed = true;
                }

                // Bake Into Pose + Loop Time ayarları (karakter masadan kaymasın)
                ModelImporterClipAnimation[] clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0)
                {
                    clips = importer.defaultClipAnimations;
                }
                if (clips != null && clips.Length > 0)
                {
                    for (int c = 0; c < clips.Length; c++)
                    {
                        var ca = clips[c];
                        if (ca.loopTime != isLoopingPose) { ca.loopTime = isLoopingPose; changed = true; }
                        if (!ca.lockRootRotation) { ca.lockRootRotation = true; changed = true; }
                        if (!ca.lockRootHeightY) { ca.lockRootHeightY = true; changed = true; }
                        if (!ca.lockRootPositionXZ) { ca.lockRootPositionXZ = true; changed = true; }
                        if (!ca.keepOriginalOrientation) { ca.keepOriginalOrientation = true; changed = true; }
                        if (!ca.keepOriginalPositionY) { ca.keepOriginalPositionY = true; changed = true; }
                        if (!ca.keepOriginalPositionXZ) { ca.keepOriginalPositionXZ = true; changed = true; }
                        if (ca.heightFromFeet) { ca.heightFromFeet = false; changed = true; }
                    }
                    importer.clipAnimations = clips;
                }

                if (changed) importer.SaveAndReimport();
                processedFbx++;

                foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                    {
                        discoveredByName[fileNameNoExt] = clip;
                        discoveredByRelPath[path] = clip;
                    }
                }
            }

            // Kullanıcının eklediği 9 Mixamo klibini kişilik slotlarına yedek harita olarak eşle
            var sharedEmoteFallbackMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Feg",   new[] { "Sitting Laughing", "Taunt Gesture", "Pointing", "Sitting Clap", "Crazy Gesture" } },
                { "Kenan", new[] { "Crying", "Looking", "Sitting Disapproval", "Crazy Gesture", "Pointing" } },
                { "Feyzi", new[] { "Sitting Clap", "Sitting Disapproval", "Pointing", "Taunt Gesture", "Sitting Laughing" } },
                { "MMG",   new[] { "Crazy Gesture", "Sitting Clap", "Sitting Laughing", "Taunt Gesture", "Pointing" } }
            };

            System.Text.StringBuilder reportSb = new System.Text.StringBuilder();
            reportSb.AppendLine($"MIXAMO IMPORT & REMAP RAPORU ({DateTime.Now:yyyy-MM-dd HH:mm:ss})");
            reportSb.AppendLine($"İşlenen FBX sayısı: {processedFbx}");
            reportSb.AppendLine(new string('-', 60));

            int matchedCount = 0;
            int missingCount = 0;

            foreach (var spec in Specs)
            {
                string aocPath = $"{OverridesDir}/AOC_{spec.KeyName}.overrideController";
                AnimatorOverrideController aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(aocPath);
                if (aoc == null) continue;

                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                aoc.GetOverrides(overrides);

                // Temel Sitting & Thinking kliplerini güncelle (Sitting.fbx / Looking.fbx mevcutsa)
                for (int i = 0; i < overrides.Count; i++)
                {
                    AnimationClip orig = overrides[i].Key;
                    if (orig == null) continue;

                    if (orig.name.StartsWith("Sitting_Idle_2H", StringComparison.OrdinalIgnoreCase))
                    {
                        if (discoveredByName.TryGetValue("Sitting", out AnimationClip sitClip))
                        {
                            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, sitClip);
                            matchedCount++;
                        }
                    }
                    else if (orig.name.Equals("Thinking", StringComparison.OrdinalIgnoreCase))
                    {
                        if (discoveredByName.TryGetValue("Looking", out AnimationClip lookClip))
                        {
                            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, lookClip);
                            matchedCount++;
                        }
                    }
                    else if (orig.name.StartsWith("Emote", StringComparison.OrdinalIgnoreCase) && orig.name.EndsWith("_Base", StringComparison.OrdinalIgnoreCase))
                    {
                        // Emote1_Base .. Emote5_Base -> slot 1..5
                        if (int.TryParse(orig.name.Substring(5, 1), out int slotNum) && slotNum >= 1 && slotNum <= 5)
                        {
                            string expectedFile = $"Emote{slotNum}_{spec.KeyName}";
                            string expectedPath = $"{MixamoDir}/{spec.KeyName}/{expectedFile}.fbx";

                            if (discoveredByRelPath.TryGetValue(expectedPath, out AnimationClip exactCharClip))
                            {
                                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, exactCharClip);
                                matchedCount++;
                                reportSb.AppendLine($"[OK] {spec.KeyName} Slot {slotNum}: '{expectedPath}' bağlandı.");
                            }
                            else
                            {
                                missingCount++;
                                string sharedName = sharedEmoteFallbackMap[spec.KeyName][slotNum - 1];
                                if (discoveredByName.TryGetValue(sharedName, out AnimationClip sharedFallback))
                                {
                                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, sharedFallback);
                                    matchedCount++;
                                    string warnMsg = $"[EKSİK] '{expectedPath}' bulunamadı -> Yedek Mixamo klibi '{sharedName}.fbx' kullanıldı.";
                                    Debug.LogWarning($"[MixamoImport] {warnMsg}");
                                    reportSb.AppendLine(warnMsg);
                                }
                                else
                                {
                                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, orig);
                                    string warnMsg = $"[EKSİK] '{expectedPath}' bulunamadı -> Prosedürel yer tutucu '{orig.name}' kullanıldı.";
                                    Debug.LogWarning($"[MixamoImport] {warnMsg}");
                                    reportSb.AppendLine(warnMsg);
                                }
                            }
                        }
                    }
                    else if (discoveredByName.TryGetValue($"{orig.name}_{spec.KeyName}", out AnimationClip charClip))
                    {
                        overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, charClip);
                        matchedCount++;
                    }
                    else if (discoveredByName.TryGetValue(orig.name, out AnimationClip sharedClip))
                    {
                        overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, sharedClip);
                        matchedCount++;
                    }
                }

                aoc.ApplyOverrides(overrides);
                EditorUtility.SetDirty(aoc);
            }

            string reportPath = $"{MixamoDir}/Mixamo_Import_Report.txt";
            File.WriteAllText(reportPath, reportSb.ToString());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MixamoImport] {processedFbx} Mixamo FBX işlendi, {matchedCount} yuva güncellendi, {missingCount} özel dosya eksik (Rapor: {reportPath}).");
        }

        private static void EnsureDirectories()
        {
            string[] dirs = new string[]
            {
                DestRoot,
                BodyguardsRoot,
                MeshesDir,
                TexturesDir,
                MaterialsDir,
                AnimationsDir,
                ControllersDir,
                OverridesDir,
                MixamoDir,
                $"{MixamoDir}/Feg",
                $"{MixamoDir}/Kenan",
                $"{MixamoDir}/Feyzi",
                $"{MixamoDir}/MMG",
                EmoteDataDir,
                PrefabsDir,
                SeveredHandsDir,
                SeveredHandMeshesDir
            };

            foreach (string d in dirs)
            {
                EnsureFolder(d);
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(assetPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            if (!string.IsNullOrEmpty(parent))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static void CopySourceAssetsUntouched()
        {
            // FBX modelleri kopyala
            for (int i = 1; i <= 4; i++)
            {
                string src = $"{SourceRoot}/Meshes/SkelMesh_Bodyguard_0{i}.fbx";
                string dst = $"{MeshesDir}/SkelMesh_Bodyguard_0{i}.fbx";
                if (File.Exists(src) && !File.Exists(dst))
                {
                    AssetDatabase.CopyAsset(src, dst);
                }
            }

            // Dokuları kopyala (D, N, Ao, S)
            string[] suffixes = { "D", "N", "Ao", "S" };
            for (int i = 1; i <= 3; i++)
            {
                foreach (string s in suffixes)
                {
                    string file = $"Boduguard_0{i}_{s}.tga";
                    string src = $"{SourceRoot}/Textures/{file}";
                    string dst = $"{TexturesDir}/{file}";
                    if (File.Exists(src) && !File.Exists(dst))
                    {
                        AssetDatabase.CopyAsset(src, dst);
                    }
                }
            }

            AssetDatabase.Refresh();
        }

        private static void ConfigureCopiedTextures()
        {
            for (int i = 1; i <= 3; i++)
            {
                string normalPath = $"{TexturesDir}/Boduguard_0{i}_N.tga";
                TextureImporter normImp = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if (normImp != null && normImp.textureType != TextureImporterType.NormalMap)
                {
                    normImp.textureType = TextureImporterType.NormalMap;
                    normImp.sRGBTexture = false;
                    normImp.SaveAndReimport();
                }

                foreach (string nonColorSuffix in new[] { "Ao", "S" })
                {
                    string p = $"{TexturesDir}/Boduguard_0{i}_{nonColorSuffix}.tga";
                    TextureImporter imp = AssetImporter.GetAtPath(p) as TextureImporter;
                    if (imp != null && imp.sRGBTexture)
                    {
                        imp.sRGBTexture = false;
                        imp.SaveAndReimport();
                    }
                }
            }
        }

        private static Dictionary<int, Material> CreateOrUpdateURPMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                urpLit = Shader.Find("Standard");
                Debug.LogWarning("[BodyguardSetup] Universal Render Pipeline/Lit bulunamadı, Standard kullanıldı.");
            }

            var result = new Dictionary<int, Material>();
            for (int i = 1; i <= 3; i++)
            {
                string matPath = $"{MaterialsDir}/Mat_Bodyguard_0{i}_URP.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(urpLit);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                else
                {
                    mat.shader = urpLit;
                }

                Texture2D diff = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/Boduguard_0{i}_D.tga");
                Texture2D norm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/Boduguard_0{i}_N.tga");
                Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/Boduguard_0{i}_Ao.tga");
                Texture2D spec = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/Boduguard_0{i}_S.tga");

                // URP/Lit Specular setup
                if (mat.HasProperty("_WorkflowMode")) mat.SetFloat("_WorkflowMode", 0f);
                mat.EnableKeyword("_SPECULAR_SETUP");

                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", diff);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);

                if (norm != null && mat.HasProperty("_BumpMap"))
                {
                    mat.SetTexture("_BumpMap", norm);
                    if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1.0f);
                    mat.EnableKeyword("_NORMALMAP");
                }

                if (ao != null && mat.HasProperty("_OcclusionMap"))
                {
                    mat.SetTexture("_OcclusionMap", ao);
                    if (mat.HasProperty("_OcclusionStrength")) mat.SetFloat("_OcclusionStrength", 0.85f);
                    mat.EnableKeyword("_OCCLUSIONMAP");
                }

                if (spec != null && mat.HasProperty("_SpecGlossMap"))
                {
                    mat.SetTexture("_SpecGlossMap", spec);
                    mat.EnableKeyword("_SPECGLOSSMAP");
                }

                if (mat.HasProperty("_SpecColor")) mat.SetColor("_SpecColor", new Color(0.28f, 0.28f, 0.28f, 1f));
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);

                EditorUtility.SetDirty(mat);
                result[i] = mat;
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        private static void EnsureProjectMaterialsAreURP()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Art/Materials" });
            int fixedCount = 0;
            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) continue;

                string sName = mat.shader.name;
                if (sName == "Standard" || sName == "Standard (Specular setup)" || sName.Contains("Error"))
                {
                    Color c = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                    Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    float gloss = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.4f;

                    mat.shader = urpLit;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                    if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", gloss);
                    EditorUtility.SetDirty(mat);
                    fixedCount++;
                }
            }
            if (fixedCount > 0)
            {
                Debug.Log($"[BodyguardSetup] Assets/_Project/Art/Materials altındaki {fixedCount} eski materyal URP/Lit shader'ına dönüştürüldü.");
            }
        }

        private static void ConfigureHumanoidModelsAndRemap(Dictionary<int, Material> urpMaterials)
        {
            string[] fbxMatNames = { "Boduguard_01_D", "Boduguard_02_D", "Boduguard_03_D" };

            for (int i = 1; i <= 4; i++)
            {
                string fbxPath = $"{MeshesDir}/SkelMesh_Bodyguard_0{i}.fbx";
                ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogError($"[BodyguardSetup] ModelImporter alınamadı: {fbxPath}");
                    continue;
                }

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.isReadable = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.materialLocation = ModelImporterMaterialLocation.External;

                // FBX materyal isimlerini URP materyallerine bağla
                for (int m = 0; m < fbxMatNames.Length; m++)
                {
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), fbxMatNames[m]);
                    importer.AddRemap(id, urpMaterials[m + 1]);
                }

                importer.SaveAndReimport();

                Avatar avatar = GetModelAvatar(fbxPath);
                if (avatar == null || !avatar.isValid || !avatar.isHuman)
                {
                    Debug.LogError($"[BodyguardSetup] HATA: {fbxPath} Humanoid Avatar doğrulanamadı!");
                }
                else
                {
                    Debug.Log($"[BodyguardSetup] {fbxPath} Humanoid Avatar doğrulandı (isValid={avatar.isValid}, isHuman={avatar.isHuman}).");
                }
            }
        }

        private static Avatar GetModelAvatar(string fbxPath)
        {
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (obj is Avatar av) return av;
            }
            return null;
        }

        private static void CacheMuscleIndices()
        {
            if (mIdx != null) return;
            string[] names = HumanTrait.MuscleName;
            mIdx = new int[names.Length];

            mLeftUpperLegFront = FindMuscle("Left Upper Leg Front-Back");
            mRightUpperLegFront = FindMuscle("Right Upper Leg Front-Back");
            mLeftLowerLegStretch = FindMuscle("Left Lower Leg Stretch");
            mRightLowerLegStretch = FindMuscle("Right Lower Leg Stretch");
            mLeftFootUpDown = FindMuscle("Left Foot Up-Down");
            mRightFootUpDown = FindMuscle("Right Foot Up-Down");

            mSpineFrontBack = FindMuscle("Spine Front-Back");
            mChestFrontBack = FindMuscle("Chest Front-Back");
            mHeadNod = FindMuscle("Head Nod Down-Up");
            mHeadTurn = FindMuscle("Head Turn Left-Right");
            mHeadTilt = FindMuscle("Head Tilt Left-Right");

            mLeftArmDownUp = FindMuscle("Left Arm Down-Up");
            mRightArmDownUp = FindMuscle("Right Arm Down-Up");
            mLeftArmFrontBack = FindMuscle("Left Arm Front-Back");
            mRightArmFrontBack = FindMuscle("Right Arm Front-Back");
            mLeftForearmStretch = FindMuscle("Left Forearm Stretch");
            mRightForearmStretch = FindMuscle("Right Forearm Stretch");
            mLeftHandDownUp = FindMuscle("Left Hand Down-Up");
            mRightHandDownUp = FindMuscle("Right Hand Down-Up");
        }

        private static int FindMuscle(string name)
        {
            int idx = Array.IndexOf(HumanTrait.MuscleName, name);
            if (idx < 0) Debug.LogWarning($"[BodyguardSetup] Muscle bulunamadı: {name}");
            return idx;
        }

        private static void CalibrateMuscleSigns()
        {
            CacheMuscleIndices();
            string samplePath = $"{MeshesDir}/SkelMesh_Bodyguard_01.fbx";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(samplePath);
            if (fbx == null) return;

            GameObject temp = UnityEngine.Object.Instantiate(fbx, Vector3.zero, Quaternion.identity);
            try
            {
                Animator anim = temp.GetComponent<Animator>();
                if (anim == null || anim.avatar == null || !anim.avatar.isHuman) return;

                HumanPoseHandler handler = new HumanPoseHandler(anim.avatar, temp.transform);
                HumanPose basePose = new HumanPose();
                handler.GetHumanPose(ref basePose);

                Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                Transform lKnee = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Transform lFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform lUpperArm = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform lHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                Transform head = anim.GetBoneTransform(HumanBodyBones.Head);

                if (hips != null && lKnee != null)
                {
                    signThighForward = PickSign(handler, basePose, mLeftUpperLegFront, 0.7f,
                        () => lKnee.position.z - hips.position.z);
                }
                if (lKnee != null && lFoot != null && mLeftLowerLegStretch >= 0)
                {
                    // Uyluk 0.75f iken diz bükme yönünü kıyasla (ayak dizin altına/gerisine iner)
                    float posScore = EvaluateWithMuscle(handler, basePose, mLeftUpperLegFront, signThighForward * 0.75f,
                        mLeftLowerLegStretch, +0.75f, () => -lFoot.position.z + (lKnee.position.y - lFoot.position.y));
                    float negScore = EvaluateWithMuscle(handler, basePose, mLeftUpperLegFront, signThighForward * 0.75f,
                        mLeftLowerLegStretch, -0.75f, () => -lFoot.position.z + (lKnee.position.y - lFoot.position.y));
                    signKneeBend = posScore >= negScore ? 1f : -1f;
                }

                if (lUpperArm != null && lHand != null)
                {
                    signArmDown = PickSign(handler, basePose, mLeftArmDownUp, 0.6f, () => lUpperArm.position.y - lHand.position.y);
                    signArmForward = PickSign(handler, basePose, mLeftArmFrontBack, 0.6f, () => lHand.position.z - lUpperArm.position.z);

                    // Kollar aşağıdayken dirsek bükme Z+ verir
                    float posE = EvaluateWithMuscle(handler, basePose, mLeftArmDownUp, signArmDown * 0.55f,
                        mLeftForearmStretch, +0.6f, () => lHand.position.z - lUpperArm.position.z);
                    float negE = EvaluateWithMuscle(handler, basePose, mLeftArmDownUp, signArmDown * 0.55f,
                        mLeftForearmStretch, -0.6f, () => lHand.position.z - lUpperArm.position.z);
                    signElbowBend = posE >= negE ? 1f : -1f;
                }

                if (hips != null && head != null)
                {
                    signSpineForward = PickSign(handler, basePose, mSpineFrontBack, 0.4f, () => head.position.z - hips.position.z);
                    signHeadDown = PickSign(handler, basePose, mHeadNod, 0.4f, () => -head.forward.y);
                }

                // Oturma pozisyonunu test edip hips yüksekliğini sandalye üstüne (y = 0.50f) göre ayarla
                HumanPose sitPose = basePose;
                sitPose.muscles = (float[])basePose.muscles.Clone();
                SetMuscle(sitPose.muscles, mLeftUpperLegFront, signThighForward * 0.76f);
                SetMuscle(sitPose.muscles, mRightUpperLegFront, signThighForward * 0.76f);
                SetMuscle(sitPose.muscles, mLeftLowerLegStretch, signKneeBend * 0.72f);
                SetMuscle(sitPose.muscles, mRightLowerLegStretch, signKneeBend * 0.72f);
                handler.SetHumanPose(ref sitPose);

                if (hips != null && lFoot != null)
                {
                    float footToHips = hips.position.y - lFoot.position.y;
                    // Ayak yere (y >= 0.04) bassın, kalça sandalye y=0.50 üstünde dursun
                    calibratedSitRootY = Mathf.Clamp(Mathf.Max(0.55f, footToHips + 0.05f), 0.48f, 0.64f);
                }

                Debug.Log($"[BodyguardSetup] Kalibrasyon: thigh={signThighForward}, knee={signKneeBend}, armDown={signArmDown}, armFwd={signArmForward}, elbow={signElbowBend}, sitY={calibratedSitRootY:F2}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        private static float PickSign(HumanPoseHandler handler, HumanPose basePose, int muscleIdx, float mag, Func<float> metric)
        {
            if (muscleIdx < 0) return 1f;
            float pScore = EvaluateWithMuscle(handler, basePose, muscleIdx, +mag, -1, 0f, metric);
            float nScore = EvaluateWithMuscle(handler, basePose, muscleIdx, -mag, -1, 0f, metric);
            return pScore >= nScore ? 1f : -1f;
        }

        private static float EvaluateWithMuscle(HumanPoseHandler handler, HumanPose basePose, int m1, float v1, int m2, float v2, Func<float> metric)
        {
            HumanPose pose = new HumanPose
            {
                bodyPosition = basePose.bodyPosition,
                bodyRotation = basePose.bodyRotation,
                muscles = (float[])basePose.muscles.Clone()
            };
            SetMuscle(pose.muscles, m1, v1);
            SetMuscle(pose.muscles, m2, v2);
            handler.SetHumanPose(ref pose);
            return metric();
        }

        private static void SetMuscle(float[] muscles, int idx, float val)
        {
            if (muscles != null && idx >= 0 && idx < muscles.Length)
            {
                muscles[idx] = Mathf.Clamp(val, -1f, 1f);
            }
        }

        private struct KeyframePose
        {
            public float Time;
            public float RootY;
            public float SpineTilt;     // + = öne eğilme
            public float ChestTilt;
            public float HeadNod;       // + = aşağı bakma
            public float HeadTurn;      // + = sağa bakma
            public float HeadRoll;
            public float LArmDown;
            public float RArmDown;
            public float LArmFwd;
            public float RArmFwd;
            public float LElbowBend;
            public float RElbowBend;
            public float LHandDown;
            public float RHandDown;
            public float ThighFwd;
            public float KneeBend;

            public static KeyframePose SitBase(float time = 0f) => new KeyframePose
            {
                Time = time,
                RootY = calibratedSitRootY,
                SpineTilt = 0.08f,
                ChestTilt = 0.04f,
                HeadNod = 0.08f,
                LArmDown = 0.52f,
                RArmDown = 0.52f,
                LArmFwd = 0.35f,
                RArmFwd = 0.35f,
                LElbowBend = 0.58f,
                RElbowBend = 0.58f,
                LHandDown = 0.0f,
                RHandDown = 0.0f,
                ThighFwd = 0.76f,
                KneeBend = 0.72f
            };
        }

        private static AnimatorController BuildAnimationsAndController()
        {
            // Temel ortak klipler
            var idle2H = CreateHumanoidClip("Sitting_Idle_2H", true,
                SitPoseWithBreath(0f, 0.02f, 0f),
                SitPoseWithBreath(0.8f, 0.06f, 0.03f),
                SitPoseWithBreath(1.6f, 0.02f, 0f));

            var idle1H = CreateHumanoidClip("Sitting_Idle_1H", true,
                SitPoseWithTilt(0f, 0.14f, -0.04f, 0.04f),
                SitPoseWithTilt(0.9f, 0.18f, 0.04f, 0.06f),
                SitPoseWithTilt(1.8f, 0.14f, -0.04f, 0.04f));

            var idle0H = CreateHumanoidClip("Sitting_Idle_0H", true,
                SitPoseWithTilt(0f, 0.22f, -0.08f, 0.12f),
                SitPoseWithTilt(0.8f, 0.28f, 0.08f, 0.16f),
                SitPoseWithTilt(1.6f, 0.22f, -0.08f, 0.12f));

            var thinking = CreateHumanoidClip("Thinking", true,
                PoseThinking(0f, -0.15f),
                PoseThinking(0.75f, 0.15f),
                PoseThinking(1.5f, -0.15f));

            var press2H = CreateHumanoidClip("PressKey_2H", false,
                KeyframePose.SitBase(0f),
                PoseReach(0.18f, 0.28f, 0.65f, 0.20f),
                KeyframePose.SitBase(0.50f));

            var press1H = CreateHumanoidClip("PressKey_1H", false,
                KeyframePose.SitBase(0f),
                PoseReach(0.18f, 0.32f, 0.72f, 0.15f),
                KeyframePose.SitBase(0.50f));

            var headSlam = CreateHumanoidClip("PressKey_HeadSlam", false,
                KeyframePose.SitBase(0f),
                PoseHeadSlam(0.18f, -0.35f, -0.25f),
                PoseHeadSlam(0.35f, 0.65f, 0.45f),
                KeyframePose.SitBase(0.65f));

            var joy = CreateHumanoidClip("Joy", false,
                KeyframePose.SitBase(0f),
                PoseArmsUp(0.25f, -0.15f, -0.55f, 0.4f),
                PoseArmsUp(0.55f, -0.12f, -0.45f, 0.3f),
                KeyframePose.SitBase(0.90f));

            var shock = CreateHumanoidClip("Shock", false,
                KeyframePose.SitBase(0f),
                PoseShock(0.15f, -0.30f, -0.12f),
                PoseShock(0.35f, -0.28f, 0.12f),
                KeyframePose.SitBase(0.75f));

            var handLost = CreateHumanoidClip("HandLost", false,
                KeyframePose.SitBase(0f),
                PoseShock(0.18f, -0.35f, -0.15f),
                PoseShock(0.50f, -0.22f, 0.10f),
                KeyframePose.SitBase(0.90f));

            var eliminated = CreateHumanoidClip("Eliminated", false,
                KeyframePose.SitBase(0f),
                PoseEliminated(0.35f, -0.45f, 0.15f),
                PoseEliminated(0.90f, 0.65f, 0.55f));

            // 16 Emote klipleri
            AnimationClip[] emoteClips = new AnimationClip[16];
            for (int e = 0; e < 16; e++)
            {
                emoteClips[e] = BuildEmoteClip((EmoteType)e);
            }

            // Animator Controller oluştur
            AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter("HandsCount", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("ArchetypeId", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("IsThinking", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("IsEliminated", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("EmoteId", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("EmoteSlot", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("PressKey", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Joy", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Shock", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("HandLost", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Eliminated", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Emote", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Reset", AnimatorControllerParameterType.Trigger);

            // Default parametre değerleri
            var parameters = ctrl.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == "HandsCount") parameters[i].defaultInt = 2;
            }
            ctrl.parameters = parameters;

            AnimatorControllerLayer layer0 = ctrl.layers[0];
            layer0.iKPass = true;
            var layers = ctrl.layers;
            layers[0] = layer0;
            ctrl.layers = layers;

            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

            AnimatorState stIdle2H = sm.AddState("Sitting_Idle_2H"); stIdle2H.motion = idle2H;
            AnimatorState stIdle1H = sm.AddState("Sitting_Idle_1H"); stIdle1H.motion = idle1H;
            AnimatorState stIdle0H = sm.AddState("Sitting_Idle_0H"); stIdle0H.motion = idle0H;
            sm.defaultState = stIdle2H;

            AnimatorState stThink = sm.AddState("Thinking"); stThink.motion = thinking;
            AnimatorState stPress2H = sm.AddState("PressKey_2H"); stPress2H.motion = press2H;
            AnimatorState stPress1H = sm.AddState("PressKey_1H"); stPress1H.motion = press1H;
            AnimatorState stPress0H = sm.AddState("PressKey_HeadSlam"); stPress0H.motion = headSlam;
            AnimatorState stJoy = sm.AddState("Joy"); stJoy.motion = joy;
            AnimatorState stShock = sm.AddState("Shock"); stShock.motion = shock;
            AnimatorState stHandLost = sm.AddState("HandLost"); stHandLost.motion = handLost;
            AnimatorState stElim = sm.AddState("Eliminated"); stElim.motion = eliminated;

            // Idle <-> Idle geçişleri (HandsCount bazlı)
            AddCondTransition(stIdle2H, stIdle1H, false, 0.2f, (AnimatorConditionMode.Equals, 1f, "HandsCount"));
            AddCondTransition(stIdle2H, stIdle0H, false, 0.2f, (AnimatorConditionMode.Equals, 0f, "HandsCount"));
            AddCondTransition(stIdle1H, stIdle0H, false, 0.2f, (AnimatorConditionMode.Equals, 0f, "HandsCount"));

            // Thinking geçişleri
            AddCondTransition(stIdle2H, stThink, false, 0.15f, (AnimatorConditionMode.If, 0f, "IsThinking"));
            AddCondTransition(stIdle1H, stThink, false, 0.15f, (AnimatorConditionMode.If, 0f, "IsThinking"));
            AddCondTransition(stIdle0H, stThink, false, 0.15f, (AnimatorConditionMode.If, 0f, "IsThinking"));
            AddIdleReturnTransitions(stThink, stIdle2H, stIdle1H, stIdle0H, false, 0.15f, (AnimatorConditionMode.IfNot, 0f, "IsThinking"));

            // Trigger tabanlı durumlar (AnyState üzerinden hızlı tepki)
            var tPress2 = sm.AddAnyStateTransition(stPress2H);
            tPress2.hasExitTime = false; tPress2.duration = 0.08f; tPress2.canTransitionToSelf = false;
            tPress2.AddCondition(AnimatorConditionMode.If, 0f, "PressKey");
            tPress2.AddCondition(AnimatorConditionMode.Equals, 2f, "HandsCount");
            tPress2.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsEliminated");

            var tPress1 = sm.AddAnyStateTransition(stPress1H);
            tPress1.hasExitTime = false; tPress1.duration = 0.08f; tPress1.canTransitionToSelf = false;
            tPress1.AddCondition(AnimatorConditionMode.If, 0f, "PressKey");
            tPress1.AddCondition(AnimatorConditionMode.Equals, 1f, "HandsCount");
            tPress1.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsEliminated");

            var tPress0 = sm.AddAnyStateTransition(stPress0H);
            tPress0.hasExitTime = false; tPress0.duration = 0.08f; tPress0.canTransitionToSelf = false;
            tPress0.AddCondition(AnimatorConditionMode.If, 0f, "PressKey");
            tPress0.AddCondition(AnimatorConditionMode.Equals, 0f, "HandsCount");
            tPress0.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsEliminated");

            AddIdleReturnTransitions(stPress2H, stIdle2H, stIdle1H, stIdle0H, true, 0.15f);
            AddIdleReturnTransitions(stPress1H, stIdle2H, stIdle1H, stIdle0H, true, 0.15f);
            AddIdleReturnTransitions(stPress0H, stIdle2H, stIdle1H, stIdle0H, true, 0.15f);

            AddTriggerFromAny(sm, stJoy, "Joy", false);
            AddIdleReturnTransitions(stJoy, stIdle2H, stIdle1H, stIdle0H, true, 0.2f);

            AddTriggerFromAny(sm, stShock, "Shock", false);
            AddIdleReturnTransitions(stShock, stIdle2H, stIdle1H, stIdle0H, true, 0.2f);

            AddTriggerFromAny(sm, stHandLost, "HandLost", false);
            AddIdleReturnTransitions(stHandLost, stIdle2H, stIdle1H, stIdle0H, true, 0.2f);

            var tElim = sm.AddAnyStateTransition(stElim);
            tElim.hasExitTime = false; tElim.duration = 0.12f; tElim.canTransitionToSelf = false;
            tElim.AddCondition(AnimatorConditionMode.If, 0f, "Eliminated");

            var tReset = sm.AddAnyStateTransition(stIdle2H);
            tReset.hasExitTime = false; tReset.duration = 0.1f; tReset.canTransitionToSelf = true;
            tReset.AddCondition(AnimatorConditionMode.If, 0f, "Reset");

            // Emote state'leri (Yalnızca EmoteSlot == 0 iken eski EmoteId fallback'i)
            for (int e = 0; e < 16; e++)
            {
                AnimatorState stE = sm.AddState($"Emote_{e:00}");
                stE.motion = emoteClips[e];
                var tE = sm.AddAnyStateTransition(stE);
                tE.hasExitTime = false; tE.duration = 0.12f; tE.canTransitionToSelf = false;
                tE.AddCondition(AnimatorConditionMode.If, 0f, "Emote");
                tE.AddCondition(AnimatorConditionMode.Equals, e, "EmoteId");
                tE.AddCondition(AnimatorConditionMode.Equals, 0, "EmoteSlot");
                tE.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsEliminated");

                AddIdleReturnTransitions(stE, stIdle2H, stIdle1H, stIdle0H, true, 0.2f);
            }

            // Katman 1: Üst Gövde Emote Katmanı (Emote1..Emote5 — Karakterler masada otururken bacakları bozmaz)
            AvatarMask upperMask = GetOrCreateUpperBodyMask();
            ctrl.AddLayer("EmoteLayer");
            var allLayers = ctrl.layers;
            allLayers[1].defaultWeight = 1f;
            allLayers[1].avatarMask = upperMask;
            allLayers[1].iKPass = false;
            ctrl.layers = allLayers;

            AnimatorStateMachine emoteSm = ctrl.layers[1].stateMachine;
            AnimatorState stEmpty = emoteSm.AddState("Empty");
            stEmpty.motion = null;
            emoteSm.defaultState = stEmpty;

            var tEmoteReset = emoteSm.AddAnyStateTransition(stEmpty);
            tEmoteReset.hasExitTime = false; tEmoteReset.duration = 0.1f; tEmoteReset.canTransitionToSelf = false;
            tEmoteReset.AddCondition(AnimatorConditionMode.If, 0f, "Reset");

            var tEmoteElim = emoteSm.AddAnyStateTransition(stEmpty);
            tEmoteElim.hasExitTime = false; tEmoteElim.duration = 0.1f; tEmoteElim.canTransitionToSelf = false;
            tEmoteElim.AddCondition(AnimatorConditionMode.If, 0f, "Eliminated");

            for (int s = 1; s <= 5; s++)
            {
                AnimationClip baseSlotClip = CreateEmoteBaseClip(s, emoteClips[Mathf.Clamp(s - 1, 0, emoteClips.Length - 1)]);
                AnimatorState stSlot = emoteSm.AddState($"Emote{s}");
                stSlot.motion = baseSlotClip;

                var tSlot = emoteSm.AddAnyStateTransition(stSlot);
                tSlot.hasExitTime = false;
                tSlot.duration = 0.12f;
                tSlot.canTransitionToSelf = true;
                tSlot.AddCondition(AnimatorConditionMode.If, 0f, "Emote");
                tSlot.AddCondition(AnimatorConditionMode.Equals, s, "EmoteSlot");
                tSlot.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsEliminated");

                var tExit = stSlot.AddTransition(stEmpty);
                tExit.hasExitTime = true;
                tExit.exitTime = 0.92f;
                tExit.duration = 0.2f;
            }

            // Her karakter için kişiliğe özel AnimatorOverrideController (Idle duruş farkları ile)
            BuildCharacterOverrideController("Feg", ctrl, idle2H,
                SitPoseWithTilt(0f, 0.04f, 0f, 0.02f), SitPoseWithTilt(0.9f, 0.08f, 0.04f, 0.03f));
            BuildCharacterOverrideController("Kenan", ctrl, idle2H,
                SitPoseWithTilt(0f, 0.18f, -0.08f, 0.12f), SitPoseWithTilt(0.45f, 0.22f, 0.08f, 0.16f));
            BuildCharacterOverrideController("Feyzi", ctrl, idle2H,
                SitPoseWithTilt(0f, 0.14f, -0.12f, 0.05f), SitPoseWithTilt(1.0f, 0.16f, 0.12f, 0.08f));
            BuildCharacterOverrideController("MMG", ctrl, idle2H,
                SitPoseWithTilt(0f, 0.00f, -0.05f, -0.04f), SitPoseWithTilt(0.85f, 0.03f, 0.05f, -0.02f));

            AssetDatabase.SaveAssets();
            return ctrl;
        }

        private static void BuildCharacterOverrideController(
            string keyName,
            AnimatorController baseController,
            AnimationClip baseIdle2H,
            KeyframePose p1,
            KeyframePose p2)
        {
            AnimationClip charIdle = CreateHumanoidClip($"Sitting_Idle_2H_{keyName}", true, p1, p2, new KeyframePose
            {
                Time = p2.Time * 2f,
                RootY = p1.RootY,
                SpineTilt = p1.SpineTilt,
                ChestTilt = p1.ChestTilt,
                HeadNod = p1.HeadNod,
                HeadTurn = p1.HeadTurn,
                HeadRoll = p1.HeadRoll,
                LArmDown = p1.LArmDown,
                RArmDown = p1.RArmDown,
                LArmFwd = p1.LArmFwd,
                RArmFwd = p1.RArmFwd,
                LElbowBend = p1.LElbowBend,
                RElbowBend = p1.RElbowBend,
                ThighFwd = p1.ThighFwd,
                KneeBend = p1.KneeBend
            });

            string aocPath = $"{OverridesDir}/AOC_{keyName}.overrideController";
            AnimatorOverrideController aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(aocPath);
            if (aoc == null)
            {
                aoc = new AnimatorOverrideController(baseController);
                AssetDatabase.CreateAsset(aoc, aocPath);
            }
            else
            {
                aoc.runtimeAnimatorController = baseController;
            }
            aoc[baseIdle2H] = charIdle;
            EditorUtility.SetDirty(aoc);
        }

        private static void AddCondTransition(AnimatorState from, AnimatorState to, bool hasExit, float dur, params (AnimatorConditionMode, float, string)[] conds)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = hasExit;
            t.exitTime = hasExit ? 0.9f : 0f;
            t.duration = dur;
            foreach (var c in conds)
            {
                t.AddCondition(c.Item1, c.Item2, c.Item3);
            }
        }

        private static void AddIdleReturnTransitions(AnimatorState from, AnimatorState idle2, AnimatorState idle1, AnimatorState idle0, bool hasExit, float dur, params (AnimatorConditionMode, float, string)[] extraConds)
        {
            AddWithHands(from, idle2, 2, hasExit, dur, extraConds);
            AddWithHands(from, idle1, 1, hasExit, dur, extraConds);
            AddWithHands(from, idle0, 0, hasExit, dur, extraConds);
        }

        private static void AddWithHands(AnimatorState from, AnimatorState to, int handsCount, bool hasExit, float dur, params (AnimatorConditionMode, float, string)[] extraConds)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = hasExit;
            t.exitTime = hasExit ? 0.9f : 0f;
            t.duration = dur;
            t.AddCondition(AnimatorConditionMode.Equals, handsCount, "HandsCount");
            if (extraConds != null)
            {
                foreach (var c in extraConds) t.AddCondition(c.Item1, c.Item2, c.Item3);
            }
        }

        private static void AddTriggerFromAny(AnimatorStateMachine sm, AnimatorState dest, string trigger, bool allowWhileEliminated)
        {
            var t = sm.AddAnyStateTransition(dest);
            t.hasExitTime = false;
            t.duration = 0.1f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            if (!allowWhileEliminated)
            {
                t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsEliminated");
            }
        }

        private static AnimationClip BuildEmoteClip(EmoteType emote)
        {
            string clipName = $"Emote_{(int)emote:00}_{emote}";
            KeyframePose s = KeyframePose.SitBase(0f);
            KeyframePose mid1 = KeyframePose.SitBase(0.45f);
            KeyframePose mid2 = KeyframePose.SitBase(0.95f);
            KeyframePose mid3 = KeyframePose.SitBase(1.40f);
            KeyframePose end = KeyframePose.SitBase(1.80f);

            switch (emote)
            {
                case EmoteType.Kabadayi_ChestThump:
                    mid1.SpineTilt = -0.10f; mid1.LArmDown = 0.05f; mid1.RArmDown = 0.05f; mid1.LElbowBend = -0.3f; mid1.RElbowBend = -0.3f;
                    mid2.SpineTilt = -0.05f; mid2.LArmDown = 0.15f; mid2.RArmDown = 0.15f; mid2.LElbowBend = 0.65f; mid2.RElbowBend = 0.65f;
                    mid3 = mid1; mid3.Time = 1.35f;
                    break;
                case EmoteType.Kabadayi_BicepFlex:
                    mid1.SpineTilt = -0.08f; mid1.LArmDown = -0.35f; mid1.RArmDown = -0.35f; mid1.LElbowBend = -0.35f; mid1.RElbowBend = -0.35f;
                    mid2 = mid1; mid2.Time = 1.25f; mid2.HeadTurn = 0.25f;
                    break;
                case EmoteType.Kabadayi_PointThreat:
                    mid1.RArmDown = 0.0f; mid1.RArmFwd = 0.85f; mid1.RElbowBend = 0.9f; mid1.SpineTilt = 0.22f;
                    mid2 = mid1; mid2.Time = 1.2f; mid2.HeadNod = 0.2f;
                    break;
                case EmoteType.Kabadayi_BoomingLaugh:
                    mid1.SpineTilt = -0.30f; mid1.HeadNod = -0.45f; mid1.LArmDown = 0.1f; mid1.RArmDown = 0.1f;
                    mid2.SpineTilt = -0.18f; mid2.HeadNod = -0.25f;
                    mid3 = mid1; mid3.Time = 1.3f;
                    break;
                case EmoteType.Korkak_TrembleBite:
                    mid1.SpineTilt = 0.25f; mid1.LArmDown = 0.25f; mid1.RArmDown = 0.25f; mid1.LArmFwd = 0.75f; mid1.RArmFwd = 0.75f; mid1.HeadTurn = -0.2f;
                    mid2 = mid1; mid2.Time = 0.9f; mid2.HeadTurn = 0.2f;
                    mid3 = mid1; mid3.Time = 1.35f; mid3.HeadTurn = -0.18f;
                    break;
                case EmoteType.Korkak_DuckTable:
                    mid1.SpineTilt = 0.65f; mid1.ChestTilt = 0.45f; mid1.HeadNod = 0.45f; mid1.LArmDown = -0.2f; mid1.RArmDown = -0.2f;
                    mid2 = mid1; mid2.Time = 1.3f;
                    break;
                case EmoteType.Korkak_FakeCry:
                    mid1.SpineTilt = 0.22f; mid1.HeadNod = 0.35f; mid1.LArmFwd = 0.65f; mid1.RArmFwd = 0.65f; mid1.LElbowBend = -0.35f; mid1.RElbowBend = -0.35f;
                    mid2 = mid1; mid2.Time = 1.0f; mid2.HeadRoll = 0.2f;
                    break;
                case EmoteType.Korkak_ChickenMock:
                    mid1.LArmDown = -0.55f; mid1.RArmDown = -0.55f; mid1.HeadNod = 0.25f;
                    mid2.LArmDown = 0.25f; mid2.RArmDown = 0.25f; mid2.HeadNod = -0.15f;
                    mid3 = mid1; mid3.Time = 1.35f;
                    break;
                case EmoteType.Sinsi_FingerWag:
                    mid1.RArmDown = 0.05f; mid1.RArmFwd = 0.75f; mid1.HeadRoll = -0.25f; mid1.HeadTurn = -0.15f;
                    mid2 = mid1; mid2.Time = 0.95f; mid2.HeadTurn = 0.15f; mid2.RHandDown = 0.4f;
                    mid3 = mid1; mid3.Time = 1.35f;
                    break;
                case EmoteType.Sinsi_SlowClap:
                    mid1.LArmDown = 0.2f; mid1.RArmDown = 0.2f; mid1.LArmFwd = 0.6f; mid1.RArmFwd = 0.6f;
                    mid2 = mid1; mid2.Time = 0.95f; mid2.LArmFwd = 0.4f; mid2.RArmFwd = 0.4f;
                    mid3 = mid1; mid3.Time = 1.35f;
                    break;
                case EmoteType.Sinsi_TongueOut:
                    mid1.SpineTilt = 0.18f; mid1.HeadRoll = 0.3f; mid1.LArmDown = -0.3f; mid1.RArmDown = -0.3f;
                    mid2 = mid1; mid2.Time = 1.2f; mid2.HeadRoll = -0.3f;
                    break;
                case EmoteType.Sinsi_HandInspect:
                    mid1.SpineTilt = 0.25f; mid1.HeadTurn = 0.35f; mid1.RArmFwd = 0.8f; mid1.LArmFwd = 0.8f;
                    mid2 = mid1; mid2.Time = 1.25f; mid2.HeadTurn = -0.35f;
                    break;
                case EmoteType.Sovmen_HatBow:
                    mid1.SpineTilt = 0.55f; mid1.HeadNod = 0.35f; mid1.RArmDown = -0.4f; mid1.LArmDown = 0.4f;
                    mid2 = mid1; mid2.Time = 1.25f;
                    break;
                case EmoteType.Sovmen_JazzHands:
                    mid1.SpineTilt = -0.1f; mid1.LArmDown = -0.45f; mid1.RArmDown = -0.15f; mid1.HeadRoll = 0.2f;
                    mid2.SpineTilt = -0.1f; mid2.LArmDown = -0.15f; mid2.RArmDown = -0.45f; mid2.HeadRoll = -0.2f;
                    mid3 = mid1; mid3.Time = 1.35f;
                    break;
                case EmoteType.Sovmen_ApplausePlease:
                    mid1.LArmDown = -0.55f; mid1.RArmDown = -0.55f; mid1.HeadNod = -0.25f;
                    mid2 = mid1; mid2.Time = 1.15f; mid2.SpineTilt = -0.12f;
                    break;
                case EmoteType.Sovmen_VictoryPose:
                    mid1.SpineTilt = -0.18f; mid1.RArmDown = -0.75f; mid1.LArmDown = -0.35f; mid1.HeadNod = -0.25f;
                    mid2 = mid1; mid2.Time = 1.30f;
                    break;
            }

            return CreateHumanoidClip(clipName, false, s, mid1, mid2, mid3, end);
        }

        private static KeyframePose SitPoseWithBreath(float t, float spine, float armLift)
        {
            KeyframePose p = KeyframePose.SitBase(t);
            p.SpineTilt = spine;
            p.LArmDown -= armLift;
            p.RArmDown -= armLift;
            return p;
        }

        private static KeyframePose SitPoseWithTilt(float t, float spine, float headTurn, float headNod)
        {
            KeyframePose p = KeyframePose.SitBase(t);
            p.SpineTilt = spine;
            p.HeadTurn = headTurn;
            p.HeadNod = headNod;
            return p;
        }

        private static KeyframePose PoseThinking(float t, float turn)
        {
            KeyframePose p = KeyframePose.SitBase(t);
            p.SpineTilt = 0.16f;
            p.HeadNod = 0.14f;
            p.HeadTurn = turn;
            p.HeadRoll = turn * 0.4f;
            p.RArmFwd = 0.55f;
            return p;
        }

        private static KeyframePose PoseReach(float t, float spine, float armFwd, float armDown)
        {
            KeyframePose p = KeyframePose.SitBase(t);
            p.SpineTilt = spine;
            p.LArmFwd = armFwd;
            p.RArmFwd = armFwd;
            p.LArmDown = armDown;
            p.RArmDown = armDown;
            p.HeadNod = 0.25f;
            return p;
        }

        private static KeyframePose PoseHeadSlam(float t, float spine, float headNod)
        {
            KeyframePose p = KeyframePose.SitBase(t);
            p.SpineTilt = spine;
            p.ChestTilt = spine * 0.6f;
            p.HeadNod = headNod;
            return p;
        }

        private static KeyframePose PoseArmsUp(float t, float spine, float armDown, float armFwd)
        {
            KeyframePose p = KeyframePose.SitBase(t);
            p.SpineTilt = spine;
            p.LArmDown = armDown;
            p.RArmDown = armDown;
            p.LArmFwd = armFwd;
            p.RArmFwd = armFwd;
            p.HeadNod = -0.2f;
            return p;
        }

        private static KeyframePose PoseShock(float t, float spine, float headTurn)
        {
            KeyframePose p = KeyframePose.SitBase(t);
            p.SpineTilt = spine;
            p.HeadNod = -0.22f;
            p.HeadTurn = headTurn;
            p.LArmDown = -0.1f;
            p.RArmDown = -0.1f;
            return p;
        }

        private static KeyframePose PoseEliminated(float t, float spine, float headNod)
        {
            KeyframePose p = KeyframePose.SitBase(t);
            p.RootY = calibratedSitRootY - 0.04f;
            p.SpineTilt = spine;
            p.ChestTilt = spine * 0.5f;
            p.HeadNod = headNod;
            p.LArmDown = 0.7f;
            p.RArmDown = 0.7f;
            p.LArmFwd = -0.1f;
            p.RArmFwd = -0.1f;
            return p;
        }

        private static AnimationClip CreateHumanoidClip(string clipName, bool loop, params KeyframePose[] poses)
        {
            string clipPath = $"{AnimationsDir}/{clipName}.anim";
            AnimationClip clip = new AnimationClip
            {
                name = clipName,
                frameRate = 30f
            };

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionXZ = true;
            settings.keepOriginalPositionY = true;
            settings.heightFromFeet = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AddCurve(clip, "RootT.x", poses, _ => 0f);
            AddCurve(clip, "RootT.y", poses, p => p.RootY);
            AddCurve(clip, "RootT.z", poses, _ => 0f);

            AddCurve(clip, "RootQ.x", poses, _ => 0f);
            AddCurve(clip, "RootQ.y", poses, _ => 0f);
            AddCurve(clip, "RootQ.z", poses, _ => 0f);
            AddCurve(clip, "RootQ.w", poses, _ => 1f);

            AddMuscleCurve(clip, "Left Upper Leg Front-Back", poses, p => signThighForward * p.ThighFwd);
            AddMuscleCurve(clip, "Right Upper Leg Front-Back", poses, p => signThighForward * p.ThighFwd);
            AddMuscleCurve(clip, "Left Lower Leg Stretch", poses, p => signKneeBend * p.KneeBend);
            AddMuscleCurve(clip, "Right Lower Leg Stretch", poses, p => signKneeBend * p.KneeBend);
            AddMuscleCurve(clip, "Left Foot Up-Down", poses, _ => 0.1f);
            AddMuscleCurve(clip, "Right Foot Up-Down", poses, _ => 0.1f);

            AddMuscleCurve(clip, "Spine Front-Back", poses, p => signSpineForward * p.SpineTilt);
            AddMuscleCurve(clip, "Chest Front-Back", poses, p => signSpineForward * p.ChestTilt);
            AddMuscleCurve(clip, "Head Nod Down-Up", poses, p => signHeadDown * p.HeadNod);
            AddMuscleCurve(clip, "Head Turn Left-Right", poses, p => p.HeadTurn);
            AddMuscleCurve(clip, "Head Tilt Left-Right", poses, p => p.HeadRoll);

            AddMuscleCurve(clip, "Left Arm Down-Up", poses, p => signArmDown * p.LArmDown);
            AddMuscleCurve(clip, "Right Arm Down-Up", poses, p => signArmDown * p.RArmDown);
            AddMuscleCurve(clip, "Left Arm Front-Back", poses, p => signArmForward * p.LArmFwd);
            AddMuscleCurve(clip, "Right Arm Front-Back", poses, p => signArmForward * p.RArmFwd);
            AddMuscleCurve(clip, "Left Forearm Stretch", poses, p => signElbowBend * p.LElbowBend);
            AddMuscleCurve(clip, "Right Forearm Stretch", poses, p => signElbowBend * p.RElbowBend);
            AddMuscleCurve(clip, "Left Hand Down-Up", poses, p => p.LHandDown);
            AddMuscleCurve(clip, "Right Hand Down-Up", poses, p => p.RHandDown);

            AssetDatabase.CreateAsset(clip, clipPath);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        }

        private static void AddCurve(AnimationClip clip, string propertyName, KeyframePose[] poses, Func<KeyframePose, float> selector)
        {
            AnimationCurve curve = new AnimationCurve();
            foreach (var p in poses)
            {
                curve.AddKey(new Keyframe(p.Time, selector(p)));
            }
            clip.SetCurve(string.Empty, typeof(Animator), propertyName, curve);
        }

        private static void AddMuscleCurve(AnimationClip clip, string muscleName, KeyframePose[] poses, Func<KeyframePose, float> selector)
        {
            if (Array.IndexOf(HumanTrait.MuscleName, muscleName) < 0) return;
            AddCurve(clip, muscleName, poses, selector);
        }

        private static Material GetOrCreateBandageMaterial()
        {
            string matPath = "Assets/_Project/Art/Materials/Mat_Bandage.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader urp = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(urp);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.95f, 0.93f, 0.86f));
                EnsureFolder("Assets/_Project/Art/Materials");
                AssetDatabase.CreateAsset(mat, matPath);
            }
            return mat;
        }

        private static void BuildCharacterAndSeveredHands(
            CharacterSpec spec,
            Dictionary<int, Material> urpMaterials,
            AnimatorController controller,
            Material bandageMat)
        {
            string fbxPath = $"{MeshesDir}/{spec.FbxFileName}";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null)
            {
                Debug.LogError($"[BodyguardSetup] FBX bulunamadı: {fbxPath}");
                return;
            }

            Material skinMat = urpMaterials[spec.PrimaryMatIndex];

            // 1) Karakter kök nesnesini oluştur
            GameObject root = new GameObject($"PlayerCharacter_{spec.KeyName}");
            try
            {
                GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                modelInstance.name = "Model";
                modelInstance.transform.SetParent(root.transform, false);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

                // Model üzerindeki Animator'u yapılandır (AOC override ile)
                Animator anim = modelInstance.GetComponent<Animator>();
                if (anim == null) anim = modelInstance.AddComponent<Animator>();
                string aocPath = $"{OverridesDir}/AOC_{spec.KeyName}.overrideController";
                AnimatorOverrideController aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(aocPath);
                anim.runtimeAnimatorController = (RuntimeAnimatorController)aoc ?? controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // Render materyallerinin URP olduğundan emin ol
                foreach (SkinnedMeshRenderer smr in modelInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.updateWhenOffscreen = true;
                    Material[] mats = smr.sharedMaterials;
                    bool dirty = false;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] == null || mats[m].shader == null || mats[m].shader.name.Contains("Standard"))
                        {
                            mats[m] = skinMat;
                            dirty = true;
                        }
                    }
                    if (dirty) smr.sharedMaterials = mats;
                }

                // Humanoid el ve önkol kemiklerini bul
                Transform leftHandBone = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.LeftHand) : null;
                Transform rightHandBone = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
                Transform leftForeArmBone = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.LeftLowerArm) : null;
                Transform rightForeArmBone = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightLowerArm) : null;

                // 2) Kopan el prefab'larını BodyGuard_Hands SkinnedMeshRenderer'ından çıkar
                CreateSeveredHandPrefab(modelInstance, leftHandBone, leftForeArmBone, rightHandBone, skinMat, bandageMat, spec.KeyName, true, out GameObject leftHandPrefab);
                CreateSeveredHandPrefab(modelInstance, rightHandBone, rightForeArmBone, leftHandBone, skinMat, bandageMat, spec.KeyName, false, out GameObject rightHandPrefab);

                // 3) Bilek sargıları (yara bantları) - önkol kemiklerinin bilek ucuna eklenir
                GameObject lBandage = CreateWristBandage("LeftWristBandage", leftForeArmBone ?? modelInstance.transform, leftHandBone, bandageMat);
                GameObject rBandage = CreateWristBandage("RightWristBandage", rightForeArmBone ?? modelInstance.transform, rightHandBone, bandageMat);

                // 4) Bileşenleri bağla
                var ikBridge = modelInstance.GetComponent<CharacterIKBridge>() ?? modelInstance.AddComponent<CharacterIKBridge>();
                var driver = root.AddComponent<CharacterAnimatorDriver>();
                driver.Archetype = spec.Archetype;
                SetField(driver, "humanoidAnimator", anim);
                SetField(ikBridge, "driver", driver);

                var sever = root.AddComponent<HandSeverSystem>();
                SetField(sever, "targetAnimator", anim);
                SetField(sever, "leftHandBone", leftHandBone);
                SetField(sever, "rightHandBone", rightHandBone);
                SetField(sever, "leftForeArmBone", leftForeArmBone);
                SetField(sever, "rightForeArmBone", rightForeArmBone);
                SetField(sever, "leftWristBandage", lBandage);
                SetField(sever, "rightWristBandage", rBandage);
                SetField(sever, "leftSeveredHandPrefab", leftHandPrefab);
                SetField(sever, "rightSeveredHandPrefab", rightHandPrefab);
                SetField(sever, "characterSkinMaterial", skinMat);

                var avatar = root.AddComponent<PlayerVisualAvatar>();
                avatar.Archetype = spec.Archetype;
                SetField(avatar, "animatorDriver", driver);
                SetField(avatar, "handSeverSystem", sever);
                SetField(avatar, "leftWristBandage", lBandage);
                SetField(avatar, "rightWristBandage", rBandage);

                string prefabPath = $"{PrefabsDir}/PlayerCharacter_{spec.KeyName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[BodyguardSetup] Karakter prefab'ı kaydedildi: {prefabPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateWristBandage(string name, Transform parentForeArm, Transform handBone, Material bandageMat)
        {
            GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            b.name = name;
            b.transform.SetParent(parentForeArm, false);

            Collider c = b.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.DestroyImmediate(c);

            Renderer r = b.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = bandageMat;

            if (handBone != null && parentForeArm != handBone && handBone.IsChildOf(parentForeArm))
            {
                Vector3 toHand = parentForeArm.InverseTransformPoint(handBone.position);
                b.transform.localPosition = toHand * 0.90f;
                if (toHand.sqrMagnitude > 0.0001f)
                {
                    b.transform.localRotation = Quaternion.FromToRotation(Vector3.up, toHand.normalized);
                }
            }
            else
            {
                b.transform.localPosition = Vector3.zero;
            }

            b.transform.localScale = new Vector3(0.085f, 0.032f, 0.085f);
            b.SetActive(false);
            return b;
        }

        private static void CreateSeveredHandPrefab(
            GameObject modelInstance,
            Transform targetHandBone,
            Transform targetForeArmBone,
            Transform otherHandBone,
            Material skinMat,
            Material bandageMat,
            string keyName,
            bool isLeft,
            out GameObject savedPrefab)
        {
            savedPrefab = null;
            string side = isLeft ? "Left" : "Right";
            string meshAssetPath = $"{SeveredHandMeshesDir}/Mesh_Hand_{side}_{keyName}.asset";
            string prefabPath = $"{SeveredHandsDir}/SeveredHand_{side}_{keyName}.prefab";

            // El SMR'sini bul
            SkinnedMeshRenderer handsSmr = null;
            foreach (SkinnedMeshRenderer smr in modelInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.name.IndexOf("Hand", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    handsSmr = smr;
                    break;
                }
            }

            Mesh extractedMesh = null;
            if (handsSmr != null && handsSmr.sharedMesh != null && targetHandBone != null)
            {
                extractedMesh = ExtractSingleSideHandMesh(handsSmr, targetHandBone, targetForeArmBone, otherHandBone, keyName, side);
                if (extractedMesh != null)
                {
                    extractedMesh.name = $"Mesh_Hand_{side}_{keyName}";
                    Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
                    if (existing != null) AssetDatabase.DeleteAsset(meshAssetPath);
                    AssetDatabase.CreateAsset(extractedMesh, meshAssetPath);
                    extractedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
                }
            }

            GameObject handObj = new GameObject($"SeveredHand_{side}_{keyName}");
            try
            {
                MeshFilter mf = handObj.AddComponent<MeshFilter>();
                MeshRenderer mr = handObj.AddComponent<MeshRenderer>();

                if (extractedMesh != null)
                {
                    mf.sharedMesh = extractedMesh;
                }
                else
                {
                    // Yedek küp/kapsül (SMR yoksa)
                    GameObject tempPrim = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    mf.sharedMesh = tempPrim.GetComponent<MeshFilter>().sharedMesh;
                    UnityEngine.Object.DestroyImmediate(tempPrim);
                }

                mr.sharedMaterial = (handsSmr != null && handsSmr.sharedMaterial != null) ? handsSmr.sharedMaterial : skinMat;

                // Bilek kesim kapağı (bandaj görünümü)
                GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cap.name = "WristBandageCap";
                cap.transform.SetParent(handObj.transform, false);
                cap.transform.localPosition = Vector3.zero;
                cap.transform.localScale = new Vector3(0.06f, 0.015f, 0.06f);
                if (cap.GetComponent<Collider>() != null) UnityEngine.Object.DestroyImmediate(cap.GetComponent<Collider>());
                cap.GetComponent<Renderer>().sharedMaterial = bandageMat;

                Rigidbody rb = handObj.AddComponent<Rigidbody>();
                rb.mass = 0.8f;
                rb.isKinematic = true;

                BoxCollider bc = handObj.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.14f, 0.08f, 0.20f);
                bc.enabled = false;

                handObj.AddComponent<SeveredHandPhysics>();

                savedPrefab = PrefabUtility.SaveAsPrefabAsset(handObj, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(handObj);
            }
        }

        private static Mesh ExtractSingleSideHandMesh(
            SkinnedMeshRenderer handsSmr,
            Transform targetHandBone,
            Transform targetForeArmBone,
            Transform otherHandBone,
            string keyName,
            string side)
        {
            Mesh srcMesh = handsSmr.sharedMesh;
            Mesh baked = new Mesh();
            handsSmr.BakeMesh(baked);

            Transform[] bones = handsSmr.bones;
            HashSet<int> targetIndices = new HashSet<int>();
            HashSet<int> otherIndices = new HashSet<int>();
            HashSet<int> targetForeArmIndices = new HashSet<int>();

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                if (bones[i] == targetHandBone || bones[i].IsChildOf(targetHandBone))
                {
                    targetIndices.Add(i);
                }
                else if (otherHandBone != null && (bones[i] == otherHandBone || bones[i].IsChildOf(otherHandBone)))
                {
                    otherIndices.Add(i);
                }
                if (targetForeArmBone != null && bones[i] == targetForeArmBone)
                {
                    targetForeArmIndices.Add(i);
                }
            }

            Vector3[] srcVerts = baked.vertices;
            Vector3[] srcNorms = baked.normals;
            Vector2[] srcUvs = srcMesh.uv;
            BoneWeight[] bws = srcMesh.boneWeights;
            int[] srcTris = baked.triangles;

            if (srcVerts.Length == 0 || srcTris.Length == 0)
            {
                UnityEngine.Object.DestroyImmediate(baked);
                return null;
            }

            bool[] isTargetVert = new bool[srcVerts.Length];
            int foreArmSeamVerts = 0;

            for (int v = 0; v < srcVerts.Length; v++)
            {
                float wTarget = 0f;
                float wOther = 0f;
                float wForeArm = 0f;

                if (v < bws.Length)
                {
                    AccumulateWeight(bws[v].boneIndex0, bws[v].weight0, targetIndices, otherIndices, targetForeArmIndices, ref wTarget, ref wOther, ref wForeArm);
                    AccumulateWeight(bws[v].boneIndex1, bws[v].weight1, targetIndices, otherIndices, targetForeArmIndices, ref wTarget, ref wOther, ref wForeArm);
                    AccumulateWeight(bws[v].boneIndex2, bws[v].weight2, targetIndices, otherIndices, targetForeArmIndices, ref wTarget, ref wOther, ref wForeArm);
                    AccumulateWeight(bws[v].boneIndex3, bws[v].weight3, targetIndices, otherIndices, targetForeArmIndices, ref wTarget, ref wOther, ref wForeArm);
                }

                if (wTarget > 0.05f && wTarget >= wOther)
                {
                    isTargetVert[v] = true;
                    if (wForeArm > 0.05f) foreArmSeamVerts++;
                }
                else if (wTarget == 0f && wOther == 0f)
                {
                    // Ağırlık yoksa el kemiklerine yakınlığa göre seç
                    Vector3 worldV = handsSmr.transform.TransformPoint(srcVerts[v]);
                    float dTarget = Vector3.SqrMagnitude(worldV - targetHandBone.position);
                    float dOther = otherHandBone != null ? Vector3.SqrMagnitude(worldV - otherHandBone.position) : float.MaxValue;
                    isTargetVert[v] = dTarget <= dOther;
                }
            }

            if (foreArmSeamVerts > 0)
            {
                Debug.Log($"[BodyguardSetup] {keyName} ({side}Hand): {foreArmSeamVerts} bilek dikiş verteksi önkol kemiği ile paylaşımlı ağırlığa sahip (WristBandage tarafından kapatılıyor).");
            }

            Dictionary<int, int> remap = new Dictionary<int, int>();
            List<Vector3> outVerts = new List<Vector3>();
            List<Vector3> outNorms = new List<Vector3>();
            List<Vector2> outUvs = new List<Vector2>();
            List<int> outTris = new List<int>();

            for (int t = 0; t < srcTris.Length; t += 3)
            {
                int i0 = srcTris[t];
                int i1 = srcTris[t + 1];
                int i2 = srcTris[t + 2];

                int hits = (isTargetVert[i0] ? 1 : 0) + (isTargetVert[i1] ? 1 : 0) + (isTargetVert[i2] ? 1 : 0);
                if (hits < 2) continue;

                int[] triIdx = { i0, i1, i2 };
                for (int k = 0; k < 3; k++)
                {
                    int srcIdx = triIdx[k];
                    if (!remap.TryGetValue(srcIdx, out int dstIdx))
                    {
                        dstIdx = outVerts.Count;
                        remap[srcIdx] = dstIdx;

                        // Bileği pivot alacak şekilde targetHandBone yerel uzayına çevir
                        Vector3 worldPos = handsSmr.transform.TransformPoint(srcVerts[srcIdx]);
                        outVerts.Add(targetHandBone.InverseTransformPoint(worldPos));

                        if (srcIdx < srcNorms.Length)
                        {
                            Vector3 worldNorm = handsSmr.transform.TransformDirection(srcNorms[srcIdx]).normalized;
                            outNorms.Add(targetHandBone.InverseTransformDirection(worldNorm).normalized);
                        }
                        else
                        {
                            outNorms.Add(Vector3.up);
                        }

                        outUvs.Add(srcIdx < srcUvs.Length ? srcUvs[srcIdx] : Vector2.zero);
                    }
                    outTris.Add(dstIdx);
                }
            }

            UnityEngine.Object.DestroyImmediate(baked);

            if (outVerts.Count == 0 || outTris.Count == 0) return null;

            Mesh handMesh = new Mesh();
            handMesh.SetVertices(outVerts);
            handMesh.SetNormals(outNorms);
            handMesh.SetUVs(0, outUvs);
            handMesh.SetTriangles(outTris, 0);
            handMesh.RecalculateBounds();
            return handMesh;
        }

        private static void AccumulateWeight(
            int boneIdx,
            float weight,
            HashSet<int> targetIndices,
            HashSet<int> otherIndices,
            HashSet<int> foreArmIndices,
            ref float wTarget,
            ref float wOther,
            ref float wForeArm)
        {
            if (weight <= 0f) return;
            if (targetIndices.Contains(boneIdx)) wTarget += weight;
            if (otherIndices.Contains(boneIdx)) wOther += weight;
            if (foreArmIndices.Contains(boneIdx)) wForeArm += weight;
        }

        private static AvatarMask GetOrCreateUpperBodyMask()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);

            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static AnimationClip CreateEmoteBaseClip(int slotIndex, AnimationClip sourceClip)
        {
            string clipPath = $"{AnimationsDir}/Emote{slotIndex}_Base.anim";
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null) AssetDatabase.DeleteAsset(clipPath);

            AnimationClip clone = UnityEngine.Object.Instantiate(sourceClip);
            clone.name = $"Emote{slotIndex}_Base";
            AssetDatabase.CreateAsset(clone, clipPath);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        }

        public static CharacterEmoteData[] CreateOrUpdateCharacterEmoteDatas()
        {
            EnsureDirectories();
            CharacterEmoteData[] results = new CharacterEmoteData[4];
            for (int i = 0; i < Specs.Length; i++)
            {
                var spec = Specs[i];
                string assetPath = $"{EmoteDataDir}/EmoteData_{spec.KeyName}.asset";
                CharacterEmoteData asset = AssetDatabase.LoadAssetAtPath<CharacterEmoteData>(assetPath);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<CharacterEmoteData>();
                    AssetDatabase.CreateAsset(asset, assetPath);
                }

                var def = CharacterDefinition.GetDefinition(spec.Archetype);
                asset.Archetype = spec.Archetype;
                asset.CharacterName = def.DisplayName;
                asset.PersonalityTag = def.PersonalityTag;
                asset.Slots = CharacterEmoteData.GetDefaultSlotsForArchetype(spec.Archetype);
                EditorUtility.SetDirty(asset);
                results[i] = asset;
            }
            return results;
        }

        private static void CreateMixamoReadme()
        {
            string path = $"{MixamoDir}/README_Mixamo.txt";
            string content =
@"MIXAMO EMOTE & ANIMASYON KILAVUZU
=================================
Sistem şu klasör yapısını otomatik olarak tarar:
- Assets/_Project/Animation/Clips/Other/*.fbx            (Genel/ortak Mixamo klipleri: Sitting, Looking, Pointing vb.)
- Assets/_Project/Animation/Clips/Mixamo/Feg/      (Feg'e özel Emote1_Feg.fbx .. Emote5_Feg.fbx)
- Assets/_Project/Animation/Clips/Mixamo/Kenan/    (Kenan'a özel Emote1_Kenan.fbx .. Emote5_Kenan.fbx)
- Assets/_Project/Animation/Clips/Mixamo/Feyzi/    (Feyzi'ye özel Emote1_Feyzi.fbx .. Emote5_Feyzi.fbx)
- Assets/_Project/Animation/Clips/Mixamo/MMG/      (MMG'ye özel Emote1_MMG.fbx .. Emote5_MMG.fbx)

MIXAMO INDIRME AYARLARI:
- Format: FBX for Unity (.fbx)
- Skin: Without Skin
- Frame Rate: 30 fps

KARAKTER BASINA BEKLENEN 5 DOSYA VE ONERILEN ARAMA KELIMELERI:
1) FEG (Cesur / Kabadayı):
   - Emote1_Feg.fbx : Sitting Laughing, Laughing
   - Emote2_Feg.fbx : Taunt Gesture, Chest Pound
   - Emote3_Feg.fbx : Pointing, Threaten
   - Emote4_Feg.fbx : Sitting Clap, Sarcastic Clap
   - Emote5_Feg.fbx : Crazy Gesture, Flexing

2) KENAN (Ürkek / Panikleyen Protokol):
   - Emote1_Kenan.fbx : Crying, Sad Idle
   - Emote2_Kenan.fbx : Looking, Terrified
   - Emote3_Kenan.fbx : Sitting Disapproval, Shaking Head No
   - Emote4_Kenan.fbx : Crazy Gesture, Nervous
   - Emote5_Kenan.fbx : Pointing, Blaming

3) FEYZI (Sinsi / Kurnaz Fedai):
   - Emote1_Feyzi.fbx : Sitting Clap, Slow Clap
   - Emote2_Feyzi.fbx : Sitting Disapproval, Finger Wag
   - Emote3_Feyzi.fbx : Pointing, Sneaky Point
   - Emote4_Feyzi.fbx : Taunt Gesture, Mock
   - Emote5_Feyzi.fbx : Sitting Laughing, Evil Laugh

4) MMG (Şovmen / VIP Dansçı):
   - Emote1_MMG.fbx : Crazy Gesture, Hip Hop Dancing
   - Emote2_MMG.fbx : Sitting Clap, Cheering
   - Emote3_MMG.fbx : Sitting Laughing, Victory
   - Emote4_MMG.fbx : Taunt Gesture, Bow
   - Emote5_MMG.fbx : Pointing, Jazz Hands

NOT: Özel Emote1..5_<Ad>.fbx dosyaları henüz eklenmemişse sistem hata vermez;
öncelikle Assets/_Project/Animation/Clips/Other/ içindeki mevcut Mixamo FBX'lerini
(Sitting, Sitting Clap, Sitting Disapproval, Sitting Laughing, Pointing, Crying,
Taunt Gesture, Crazy Gesture, Looking), onlar da yoksa prosedürel klipleri kullanır.

KURULUM:
Dosyaları ilgili klasörlere attıktan sonra Unity menüsünden:
'FingerMonster -> 2. Import & Remap Mixamo Animations' seçeneğine tıklayın.
Tüm FBX'ler otomatik Humanoid + Bake Into Pose yapılır, bacaklar UpperBodyEmoteMask ile
sandalyede sabitlenir ve eksik dosya varsa Mixamo_Import_Report.txt dosyasına yazılır.
";
            File.WriteAllText(path, content);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target as UnityEngine.Object);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                if (prop.propertyType == SerializedPropertyType.ObjectReference)
                {
                    prop.objectReferenceValue = value as UnityEngine.Object;
                }
                else if (prop.propertyType == SerializedPropertyType.Color && value is Color col)
                {
                    prop.colorValue = col;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning($"[BodyguardSetup] Alan bulunamadı: {target.GetType().Name}.{fieldName}");
            }
        }
    }
}
