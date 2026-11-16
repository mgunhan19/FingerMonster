using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using FingerMonster.Core;
using FingerMonster.Gameplay;
using FingerMonster.Player;
using FingerMonster.UI;
using FingerMonster.Audio;
using FingerMonster.Networking;
using FingerMonster.EditorTools;

namespace FingerMonster.Editor
{
    /// <summary>
    /// FingerMonster sahnelerini (MainMenu ve Game), hiyerarşiyi, ışıklandırmayı,
    /// daktiloyu, 20 tuşu, kameraları ve tüm UI bileşenlerini otomatik olarak inşa eden editör aracı.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneSetupTool
    {
        private const string PillarPath = "Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Pillars/fi_vil_pillar8_02.prefab";
        private const string WallCandlePath = "Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Props/Lighting/WallMounted/fi_vil_light_candle_wall02_lit_b.prefab";
        private const string SmallBenchPath = "Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Forge/fi_vil_forge_workbensh_small1.prefab";
        private const string AnvilPath = "Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Forge/fi_vil_forge_anvil.prefab";
        private const string HammerPath = "Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Forge/Forge_Props/fi_vil_forge_hammer2.prefab";
        private const string StoolPath = "Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Forge/Forge_Props/fi_vil_forge_stool1.prefab";
        private const string BarrelClusterPath = "Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Props/Containers/Barrels/fi_vil_container_barrel_small_sidex3.prefab";
        private const string WeaponRackPath = "Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Forge/Forge_Blades/fi_vil_forge_weaponrack3_full.prefab";
        private const string ChainPath = "Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Forge/Forge_Props/fi_vil_forge_chain1_medium.prefab";

        static SceneSetupTool()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                EnsureDecorativePropsInActiveScene();
            }
        }

        [MenuItem("Tools/FingerMonster/Setup Game Scene")]
        [MenuItem("FingerMonster/Setup Game Scene")]
        public static void SetupGameSceneMenu()
        {
            EnsureTMPResources();
            BodyguardSetupTool.EnsurePrefabsExist();
            SyncExternalButcherAnimations();
            BackupPlaceholderSceneIfNeeded();
            BuildGameScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateForgeEnvironment.ValidateScene(out string valReport, logToConsole: true);
            Debug.Log("<color=green>[SceneSetupTool] Game sahnesi dekoratif 3DForge varlıkları ve görünür Kasap ile başarıyla kuruldu!</color>");
        }

        [MenuItem("Tools/FingerMonster/Build All Scenes (MainMenu + Game)")]
        [MenuItem("FingerMonster/Build All Scenes")]
        public static void BuildAllScenes()
        {
            EnsureTMPResources();
            BodyguardSetupTool.EnsurePrefabsExist();
            SyncExternalButcherAnimations();
            BackupPlaceholderSceneIfNeeded();
            BuildGameScene();
            BuildMainMenuScene();
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ValidateForgeEnvironment.ValidateScene(out string valReport, logToConsole: true);
            Debug.Log("<color=green>[SceneSetupTool] Tüm sahneler (MainMenu ve Game) başarıyla inşa edildi ve Build Settings'e eklendi!</color>");
        }

        [MenuItem("Tools/FingerMonster/Sync Butcher Animations (kasap.fbx)")]
        public static void SyncExternalButcherAnimations()
        {
            string[] searchFolders = new string[] { "Assets/Characters/Animations", "Assets/Prefabs" };
            string[] expectedClips = new string[]
            {
                "Butcher_Walking.fbx",
                "Butcher_Walk.fbx",
                "Butcher_Idle.fbx",
                "Butcher_Taunt1.fbx",
                "Butcher_Taunt2.fbx",
                "Butcher_Attack.fbx"
            };

            List<string> foundClips = new List<string>();
            foreach (string folder in searchFolders)
            {
                if (!System.IO.Directory.Exists(folder)) continue;

                foreach (string fileName in expectedClips)
                {
                    string assetPath = $"{folder}/{fileName}";
                    if (!System.IO.File.Exists(assetPath)) continue;

                    ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                    if (importer != null)
                    {
                        bool dirty = false;
                        if (!importer.importAnimation)
                        {
                            importer.importAnimation = true;
                            dirty = true;
                        }
                        if (dirty)
                        {
                            importer.SaveAndReimport();
                        }
                    }

                    Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                    int clipCount = 0;
                    foreach (Object sub in subAssets)
                    {
                        if (sub is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                        {
                            clipCount++;
                        }
                    }
                    foundClips.Add($"{assetPath} ({clipCount} klip)");
                }
            }

            if (foundClips.Count > 0)
            {
                Debug.Log($"<color=cyan>[SceneSetupTool] Harici Kasap animasyonları doğrulandı ({foundClips.Count} dosya): {string.Join(", ", foundClips)}</color>");
            }
            else
            {
                Debug.Log("[SceneSetupTool] Harici Kasap animasyon FBX dosyası henüz bulunamadı (Butcher_Walking/Idle/Taunt1/Taunt2/Attack.fbx). Eklemli prosedürel kasap.fbx rig sistemi aktif.");
            }
        }

        private static AnimationClip LoadFirstClipFromFbx(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (subAssets == null) return null;
            foreach (Object sub in subAssets)
            {
                if (sub is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
            return null;
        }

        private static void WireButcherAnimationsToComponent(ButcherPlaceholder butcherComp)
        {
            if (butcherComp == null) return;

            GameObject driverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Butcher_Idle.fbx");
            AnimationClip idleClip = LoadFirstClipFromFbx("Assets/Prefabs/Butcher_Idle.fbx");
            AnimationClip walkClip = LoadFirstClipFromFbx("Assets/Prefabs/Butcher_Walking.fbx") ?? LoadFirstClipFromFbx("Assets/Prefabs/Butcher_Walk.fbx");
            AnimationClip taunt1Clip = LoadFirstClipFromFbx("Assets/Prefabs/Butcher_Taunt1.fbx");
            AnimationClip taunt2Clip = LoadFirstClipFromFbx("Assets/Prefabs/Butcher_Taunt2.fbx");
            AnimationClip attackClip = LoadFirstClipFromFbx("Assets/Prefabs/Butcher_Attack.fbx");

            SerializedObject soButcher = new SerializedObject(butcherComp);
            SerializedProperty propDriver = soButcher.FindProperty("mixamoPoseDriverPrefab");
            if (propDriver != null && driverPrefab != null) propDriver.objectReferenceValue = driverPrefab;
            SerializedProperty propIdle = soButcher.FindProperty("idleClip");
            if (propIdle != null && idleClip != null) propIdle.objectReferenceValue = idleClip;
            SerializedProperty propWalk = soButcher.FindProperty("walkClip");
            if (propWalk != null && walkClip != null) propWalk.objectReferenceValue = walkClip;
            SerializedProperty propTaunt1 = soButcher.FindProperty("taunt1Clip");
            if (propTaunt1 != null && taunt1Clip != null) propTaunt1.objectReferenceValue = taunt1Clip;
            SerializedProperty propTaunt2 = soButcher.FindProperty("taunt2Clip");
            if (propTaunt2 != null && taunt2Clip != null) propTaunt2.objectReferenceValue = taunt2Clip;
            SerializedProperty propAttack = soButcher.FindProperty("attackClip");
            if (propAttack != null && attackClip != null) propAttack.objectReferenceValue = attackClip;
            soButcher.ApplyModifiedPropertiesWithoutUndo();

            butcherComp.SetButcherAnimationAssets(driverPrefab, idleClip, walkClip, taunt1Clip, taunt2Clip, attackClip, null);
        }

        private static void BackupPlaceholderSceneIfNeeded()
        {
            const string srcScene = "Assets/Scenes/Game.unity";
            const string backupScene = "Assets/Scenes/Backup_Game_Placeholders.unity";
            if (System.IO.File.Exists(srcScene) && !System.IO.File.Exists(backupScene))
            {
                AssetDatabase.CopyAsset(srcScene, backupScene);
                Debug.Log($"[SceneSetupTool] Eski yer tutucu sahne yedeklendi: {backupScene}");
            }
        }

        private static void EnsureTMPResources()
        {
            if (!System.IO.Directory.Exists("Assets/TextMesh Pro"))
            {
                Debug.Log("[SceneSetupTool] TMP Essential Resources içe aktarılıyor...");
                string[] packagePaths = System.IO.Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", System.IO.SearchOption.AllDirectories);
                if (packagePaths.Length > 0)
                {
                    AssetDatabase.ImportPackage(packagePaths[0], false);
                }
            }
        }

        private static void UpdateBuildSettings()
        {
            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Game.unity", true)
            };
            EditorBuildSettings.scenes = scenes;
        }

        public static void Ensure3DForgeMaterialsURP()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/3DForge" });
            int convertedCount = 0;
            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) continue;

                if (mat.shader.name == "Standard" || mat.shader.name.StartsWith("Standard") || mat.shader.name.Contains("Error"))
                {
                    Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                    Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                    Texture emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
                    Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
                    float glossiness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.4f;

                    mat.shader = urpLit;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                    if (mainTex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
                    if (bumpMap != null && mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", bumpMap);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", glossiness);

                    if (emissionMap != null || emissionColor.maxColorComponent > 0.01f)
                    {
                        mat.EnableKeyword("_EMISSION");
                        if (emissionMap != null && mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", emissionMap);
                        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionColor);
                    }

                    EditorUtility.SetDirty(mat);
                    convertedCount++;
                }
            }

            if (convertedCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[SceneSetupTool] 3DForge paketindeki {convertedCount} materyal URP/Lit shader'ına dönüştürüldü.");
            }
        }

        #region GAME SCENE BUILDER
        public static void BuildGameScene()
        {
            Ensure3DForgeMaterialsURP();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Materials (URP Lit / Dokulu ve Stilize Yüzeyler)
            Material floorMat = CreateColoredMaterial("Mat_Floor", new Color(0.18f, 0.18f, 0.20f), "Assets/Textures/Environment/Tex_Floor_Tiles.png", smoothness: 0.15f, metallic: 0.05f);
            Material wallMat = CreateColoredMaterial("Mat_Wall_Concrete", new Color(0.32f, 0.32f, 0.34f), "Assets/Textures/Environment/Tex_Wall_DirtyConcrete.png", smoothness: 0.12f, metallic: 0.02f);
            Material ceilingMat = CreateColoredMaterial("Mat_Ceiling", new Color(0.14f, 0.14f, 0.16f), "Assets/Textures/Environment/Tex_Wall_DirtyConcrete.png", smoothness: 0.08f, metallic: 0.0f);
            Material tableMat = CreateColoredMaterial("Mat_Table", new Color(0.24f, 0.15f, 0.08f), "Assets/Textures/Environment/Tex_Table_DarkWood.png", smoothness: 0.65f, metallic: 0.08f); // Camsı/cilalı yansıma
            Material typewriterMat = CreateColoredMaterial("Mat_Typewriter", new Color(0.22f, 0.22f, 0.25f), "Assets/Textures/Environment/Tex_Typewriter_Metal.png", smoothness: 0.72f, metallic: 0.45f); // Metalik yansıma
            Material keyNormalMat = CreateColoredMaterial("Mat_KeyNormal", new Color(0.9f, 0.88f, 0.82f), null, smoothness: 0.4f, metallic: 0.1f);
            Material paperMat = CreateColoredMaterial("Mat_Paper", new Color(0.96f, 0.95f, 0.92f), null, smoothness: 0.05f, metallic: 0.0f);
            Material chairMat = CreateColoredMaterial("Mat_Chair", new Color(0.15f, 0.15f, 0.15f), null, smoothness: 0.25f, metallic: 0.1f);
            Material butcherMat = CreateColoredMaterial("Mat_Butcher", new Color(0.45f, 0.1f, 0.1f), null, smoothness: 0.2f, metallic: 0.0f);
            Material poster1Mat = CreateColoredMaterial("Mat_Poster_WatchFingers", Color.white, "Assets/Textures/Environment/Tex_Poster_WatchFingers.png", smoothness: 0.1f, metallic: 0.0f);
            Material poster2Mat = CreateColoredMaterial("Mat_Poster_ButcherWatching", Color.white, "Assets/Textures/Environment/Tex_Poster_ButcherWatching.png", smoothness: 0.1f, metallic: 0.0f);
            Material bulbMat = CreateColoredMaterial("Mat_BulbGlass", new Color(1.0f, 0.92f, 0.65f), null, smoothness: 0.9f, metallic: 0.1f);
            Material cordMat = CreateColoredMaterial("Mat_BlackCord", new Color(0.08f, 0.08f, 0.08f), null, smoothness: 0.1f, metallic: 0.0f);
            Material windowMat = CreateColoredMaterial("Mat_DarkWindow", new Color(0.05f, 0.07f, 0.10f), null, smoothness: 0.85f, metallic: 0.1f);

            // 2. Camera Setup (Geniş açı: kendi karakterimizin hareketlerini ve tüm masayı rahat görmek için)
            GameObject cameraObj = new GameObject("Main Camera");
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.05f);
            cam.fieldOfView = 68f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            cameraObj.AddComponent<AudioListener>();
            cameraObj.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            cameraObj.tag = "MainCamera";
            cameraObj.transform.position = new Vector3(0f, 2.45f, -3.25f);
            cameraObj.transform.rotation = Quaternion.Euler(26f, 0f, 0f);
            PlayerCameraController camController = cameraObj.AddComponent<PlayerCameraController>();

            // 3. Lighting Setup (Kasvetli / Düşük Ortam Işığı + Masa Spotu + Yanıp Sönen Ampul)
            GameObject lightingRoot = new GameObject("Lighting");

            GameObject ambientObj = new GameObject("Ambient Room Light");
            ambientObj.transform.SetParent(lightingRoot.transform);
            Light ambientLight = ambientObj.AddComponent<Light>();
            ambientLight.type = LightType.Directional;
            ambientLight.color = new Color(0.35f, 0.40f, 0.50f);
            ambientLight.intensity = 0.18f; // Gerilimli düşük ortam ışığı
            ambientLight.transform.rotation = Quaternion.Euler(50, -30, 0);

            GameObject spotObj = new GameObject("Table Spot Light");
            spotObj.transform.SetParent(lightingRoot.transform);
            spotObj.transform.position = new Vector3(0, 3.4f, 0);
            spotObj.transform.rotation = Quaternion.Euler(90, 0, 0);
            Light spotLight = spotObj.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.range = 8.5f;
            spotLight.spotAngle = 52f;
            spotLight.intensity = 2.4f;
            spotLight.color = new Color(1.0f, 0.94f, 0.82f); // Sıcak tek odak spot

            GameObject redObj = new GameObject("Dramatic Red Light");
            redObj.transform.SetParent(lightingRoot.transform);
            redObj.transform.position = new Vector3(0, 3.0f, 0);
            redObj.transform.rotation = Quaternion.Euler(90, 0, 0);
            Light redLight = redObj.AddComponent<Light>();
            redLight.type = LightType.Spot;
            redLight.range = 8f;
            redLight.spotAngle = 65f;
            redLight.intensity = 3.5f;
            redLight.color = new Color(0.95f, 0.1f, 0.1f);
            redLight.enabled = false;

            // Arka Planda Asılı ve Yanıp Sönen Atmosferik Demirci Işığı (3DForge Chain & Light)
            GameObject bulbRoot = new GameObject("FlickeringHangingLight");
            bulbRoot.transform.SetParent(lightingRoot.transform);
            bulbRoot.transform.position = new Vector3(-1.8f, 3.0f, 2.2f);

            // Demirci Zinciri (3DForge)
            SpawnForgeProp("Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Forge/Forge_Props/fi_vil_forge_chain1_medium.prefab",
                bulbRoot.transform, Vector3.zero, Quaternion.identity, Vector3.one);

            // Işık Kaynağı
            GameObject bulbPointLightObj = new GameObject("ForgePointLight");
            bulbPointLightObj.transform.SetParent(bulbRoot.transform);
            bulbPointLightObj.transform.localPosition = new Vector3(0, -0.6f, 0);
            Light bulbLightComp = bulbPointLightObj.AddComponent<Light>();
            bulbLightComp.type = LightType.Point;
            bulbLightComp.range = 6.0f;
            bulbLightComp.intensity = 1.1f;
            bulbLightComp.color = new Color(1.0f, 0.85f, 0.55f);

            FlickeringBulb flickeringComp = bulbRoot.AddComponent<FlickeringBulb>();

            RoomLightingController lightingController = lightingRoot.AddComponent<RoomLightingController>();
            lightingController.SetupLights(spotLight, ambientLight, redLight, flickeringComp);

            // 4. Environment (100% Blacksmith's Forge - 3DForge Modüler Mimari - Sıfır Unity İlkel Mesh'i)
            GameObject envRoot = new GameObject("Environment");

            // 4a. Modüler Taş Zemin (5x5 ızgara, 25 adet fi_vil_floor_stone03 taş karo, X:[-5..5], Z:[-5..5])
            GameObject floorRoot = new GameObject("StoneFloor");
            floorRoot.transform.SetParent(envRoot.transform);
            float[] gridCoords = new float[] { -5.0f, -2.5f, 0.0f, 2.5f, 5.0f };
            string floorPrefabPath = "Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Floors/fi_vil_floor_stone03.prefab";
            for (int r = 0; r < gridCoords.Length; r++)
            {
                for (int c = 0; c < gridCoords.Length; c++)
                {
                    SpawnForgeProp(floorPrefabPath, floorRoot.transform,
                        new Vector3(gridCoords[c], 0f, gridCoords[r]), Quaternion.identity, Vector3.one);
                }
            }

            // 4b. Modüler Ahşap Tavan (5x5 ızgara, 25 adet fi_vil_floor_wood04 ahşap karo, Y: 3.0m, aşağı bakan ahşap kaplama)
            GameObject ceilRoot = new GameObject("WoodCeiling");
            ceilRoot.transform.SetParent(envRoot.transform);
            string ceilPrefabPath = "Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Floors/fi_vil_floor_wood04.prefab";
            for (int r = 0; r < gridCoords.Length; r++)
            {
                for (int c = 0; c < gridCoords.Length; c++)
                {
                    SpawnForgeProp(ceilPrefabPath, ceilRoot.transform,
                        new Vector3(gridCoords[c], 3.0f, gridCoords[r]), Quaternion.Euler(180f, 0f, 0f), Vector3.one);
                }
            }

            // 4c. Dört Duvar (Modüler fi_vil_wall01_01 parçalarıyla odayı tam çevreleyen taş duvarlar)
            GameObject wallsRoot = new GameObject("StoneWalls");
            wallsRoot.transform.SetParent(envRoot.transform);
            string wallPrefabPath = "Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Walls/wall_01/fi_vil_wall01_01.prefab";

            // Doğu Duvarı (X = +5.0 tile hattı, duvar yüzeyi X:+6.2, odaya dönük)
            for (int i = 0; i < gridCoords.Length; i++)
            {
                SpawnForgeProp(wallPrefabPath, wallsRoot.transform,
                    new Vector3(5.0f, 0f, gridCoords[i]), Quaternion.Euler(0f, 0f, 0f), Vector3.one);
            }

            // Batı Duvarı (X = -5.0 tile hattı, duvar yüzeyi X:-6.2, odaya dönük)
            for (int i = 0; i < gridCoords.Length; i++)
            {
                SpawnForgeProp(wallPrefabPath, wallsRoot.transform,
                    new Vector3(-5.0f, 0f, gridCoords[i]), Quaternion.Euler(0f, 180f, 0f), Vector3.one);
            }

            // Kuzey Duvarı (Z = +5.0 tile hattı, duvar yüzeyi Z:+6.2, odaya dönük)
            for (int i = 0; i < gridCoords.Length; i++)
            {
                SpawnForgeProp(wallPrefabPath, wallsRoot.transform,
                    new Vector3(gridCoords[i], 0f, 5.0f), Quaternion.Euler(0f, 270f, 0f), Vector3.one);
            }
            // Kuzey Duvarı Ortasında Demirci Kapısı (3DForge fi_vil_door04)
            SpawnForgeProp("Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Doors/fi_vil_door04.prefab",
                wallsRoot.transform, new Vector3(0f, 0f, 6.18f), Quaternion.Euler(0f, 180f, 0f), Vector3.one);

            // Güney Duvarı (Z = -5.0 tile hattı, duvar yüzeyi Z:-6.2, odaya dönük)
            for (int i = 0; i < gridCoords.Length; i++)
            {
                SpawnForgeProp(wallPrefabPath, wallsRoot.transform,
                    new Vector3(gridCoords[i], 0f, -5.0f), Quaternion.Euler(0f, 90f, 0f), Vector3.one);
            }

            // 4d. Köşe ve Duvar Bölme Sütunları (4 köşe + 8 duvar bölme = 12 taş sütun)
            GameObject pillarsRoot = new GameObject("Pillars");
            pillarsRoot.transform.SetParent(envRoot.transform);
            InstantiateForgePrefab(PillarPath, pillarsRoot.transform, new Vector3(-5.8f, 0f, -5.8f), Quaternion.identity, Vector3.one, "Pillar_Corner_SW");
            InstantiateForgePrefab(PillarPath, pillarsRoot.transform, new Vector3(5.8f, 0f, -5.8f), Quaternion.identity, Vector3.one, "Pillar_Corner_SE");
            InstantiateForgePrefab(PillarPath, pillarsRoot.transform, new Vector3(-5.8f, 0f, 5.8f), Quaternion.identity, Vector3.one, "Pillar_Corner_NW");
            InstantiateForgePrefab(PillarPath, pillarsRoot.transform, new Vector3(5.8f, 0f, 5.8f), Quaternion.identity, Vector3.one, "Pillar_Corner_NE");

            // 4e. Düzenli ve Simetrik Duvar Kenarı / Köşe Dekorasyonu (Kasap r=2.55m koridoru açık, |x|>=3.8f veya |z|>=3.8f)
            GameObject forgeRoot = new GameObject("BlacksmithForge_Props");
            forgeRoot.transform.SetParent(envRoot.transform);

            // 1) Taş Ocak / Yanan Demirci Ocağı (Kuzey-Doğu Köşesi, duvara dayalı, loş kor görünümü)
            GameObject forgeInst = InstantiateForgePrefab("Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Forge/fi_vil_forge_forge3_lit.prefab",
                forgeRoot.transform, new Vector3(4.5f, 0f, 4.5f), Quaternion.Euler(0, -135f, 0), Vector3.one * 0.90f, "Forge_Hearth_NE");
            if (forgeInst != null)
            {
                var forgeLights = forgeInst.GetComponentsInChildren<Light>();
                foreach (var fl in forgeLights)
                {
                    fl.intensity = Mathf.Clamp(fl.intensity * 0.18f, 0.4f, 1.2f);
                    fl.color = new Color(0.95f, 0.35f, 0.08f);
                    fl.range = Mathf.Min(fl.range, 2.5f);
                }
            }

            // 2) Büyük Örs ve Çekiç (Doğu Duvarı Dibinde)
            InstantiateForgePrefab(AnvilPath, forgeRoot.transform, new Vector3(5.0f, 0f, 0.5f), Quaternion.Euler(0, -90f, 0), Vector3.one, "Forge_Anvil_East");
            InstantiateForgePrefab(HammerPath, forgeRoot.transform, new Vector3(5.0f, 0.82f, 0.5f), Quaternion.Euler(0, 45f, 90f), Vector3.one, "Forge_Hammer_East");

            // 3) Silah / Kılıç Askılığı (Kuzey Duvarı Dibinde)
            InstantiateForgePrefab(WeaponRackPath, forgeRoot.transform, new Vector3(2.0f, 0f, 5.75f), Quaternion.Euler(0, 180f, 0), Vector3.one, "Forge_WeaponRack_North");

            // 4) Ahşap Fıçılar (Güney-Batı Köşesi, duvara dayalı)
            InstantiateForgePrefab(BarrelClusterPath, forgeRoot.transform, new Vector3(-4.8f, 0f, -4.6f), Quaternion.Euler(0, 30f, 0), Vector3.one, "Forge_Barrels_SW");

            // 5) Ekstra Dekoratif Sütunlar, Simetrik Şamdanlar, Yan Tezgahlar, Silah Rafları ve Tavan Zincirleri
            PopulateExtraDecorativeProps(pillarsRoot.transform, forgeRoot.transform);

            // URP Post-Processing Volume
            GameObject volumeObj = new GameObject("EnvironmentPostProcessingVolume");
            volumeObj.transform.SetParent(envRoot.transform);
            UnityEngine.Rendering.Volume vol = volumeObj.AddComponent<UnityEngine.Rendering.Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            UnityEngine.Rendering.VolumeProfile defaultProf = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Settings/DefaultVolumeProfile.asset");
            if (defaultProf != null) vol.sharedProfile = defaultProf;

            // 5. Central Table (3DForge Forged Workbench Table - Sıfır İlkel Mesh)
            GameObject tableRoot = new GameObject("Table");
            tableRoot.transform.position = Vector3.zero;

            SpawnForgeProp("Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Forge/fi_vil_forge_workbensh_large1.prefab",
                tableRoot.transform, Vector3.zero, Quaternion.Euler(0, 90f, 0), new Vector3(2.2f, 0.81f, 1.55f));

            // 6. Typewriter Machine
            GameObject typewriterObj = new GameObject("TypewriterMachine");
            typewriterObj.transform.position = new Vector3(0, 0.8f, 0);
            TypewriterMachine typewriterMachine = typewriterObj.AddComponent<TypewriterMachine>();

            // Gövde (Düşük taban kasası - tuşların görüşünü asla engellemez)
            GameObject twBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            twBody.name = "Body";
            twBody.transform.SetParent(typewriterObj.transform);
            twBody.transform.localPosition = new Vector3(0, 0.045f, 0);
            twBody.transform.localScale = new Vector3(1.18f, 0.09f, 0.85f);
            twBody.GetComponent<Renderer>().sharedMaterial = typewriterMat;
            if (twBody.GetComponent<Collider>() != null) Object.DestroyImmediate(twBody.GetComponent<Collider>());

            // Arka Mekanizma Bloğu (Kızağın oturduğu yükseltilmiş arka hazne)
            GameObject twRear = GameObject.CreatePrimitive(PrimitiveType.Cube);
            twRear.name = "RearHousing";
            twRear.transform.SetParent(typewriterObj.transform);
            twRear.transform.localPosition = new Vector3(0, 0.13f, 0.22f);
            twRear.transform.localScale = new Vector3(1.08f, 0.09f, 0.38f);
            twRear.GetComponent<Renderer>().sharedMaterial = typewriterMat;
            if (twRear.GetComponent<Collider>() != null) Object.DestroyImmediate(twRear.GetComponent<Collider>());

            // Kâğıt Kızağı Grubu (Carriage Root)
            GameObject carriageRoot = new GameObject("CarriageRoot");
            carriageRoot.transform.SetParent(typewriterObj.transform);
            carriageRoot.transform.localPosition = Vector3.zero;
            carriageRoot.transform.localRotation = Quaternion.identity;

            // Kâğıt Rulosu & Kâğıt
            GameObject paperRoll = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            paperRoll.name = "PaperRoll";
            paperRoll.transform.SetParent(carriageRoot.transform);
            paperRoll.transform.localPosition = new Vector3(0, 0.22f, 0.22f);
            paperRoll.transform.localRotation = Quaternion.Euler(0, 0, 90);
            paperRoll.transform.localScale = new Vector3(0.12f, 0.55f, 0.12f);
            paperRoll.GetComponent<Renderer>().sharedMaterial = typewriterMat;
            if (paperRoll.GetComponent<Collider>() != null) Object.DestroyImmediate(paperRoll.GetComponent<Collider>());

            GameObject paperObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            paperObj.name = "TypewriterPaper";
            paperObj.transform.SetParent(carriageRoot.transform);
            paperObj.transform.localPosition = new Vector3(0, 0.36f, 0.25f);
            paperObj.transform.localRotation = Quaternion.Euler(15, 0, 0);
            paperObj.transform.localScale = new Vector3(0.9f, 0.45f, 1f);
            paperObj.GetComponent<Renderer>().sharedMaterial = paperMat;
            if (paperObj.GetComponent<Collider>() != null) Object.DestroyImmediate(paperObj.GetComponent<Collider>());

            TypewriterPaper twPaper = paperObj.AddComponent<TypewriterPaper>();
            GameObject paperTextObj = new GameObject("PaperTextMesh");
            paperTextObj.transform.SetParent(paperObj.transform);
            paperTextObj.transform.localPosition = new Vector3(-0.40f, 0.38f, -0.01f);
            paperTextObj.transform.localRotation = Quaternion.identity;
            TextMeshPro paperTMP = paperTextObj.AddComponent<TextMeshPro>();
            paperTMP.fontSize = 1.35f;
            paperTMP.characterSpacing = 4f;
            paperTMP.lineSpacing = 10f;
            paperTMP.color = new Color(0.12f, 0.12f, 0.16f);
            paperTMP.alignment = TextAlignmentOptions.TopLeft;
            paperTMP.rectTransform.sizeDelta = new Vector2(0.80f, 0.40f);

            // 20 Daktilo Tuşunu Düzenli Diz (Üst sıra 10, Alt sıra 10 harf - kademeli, eğimli ve masada belirgin)
            List<TypewriterKey> keysList = new List<TypewriterKey>();
            string letters = "QWERTYUIOPASDFGHJKLZ";

            float stepX = 0.094f;
            float topStartX = -4.5f * stepX;
            float bottomStartX = topStartX + (stepX * 0.5f); // Kademeli (staggered) daktilo dizilimi

            float[] rowZ = new float[] { -0.07f, -0.22f };
            float[] rowY = new float[] { 0.155f, 0.125f };

            int keyIndex = 0;
            for (int r = 0; r < 2; r++)
            {
                float rowStartX = (r == 0) ? topStartX : bottomStartX;
                for (int c = 0; c < 10; c++)
                {
                    if (keyIndex >= 20) break;

                    GameObject keyObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    keyObj.name = $"Key_{keyIndex}_{letters[keyIndex]}";
                    keyObj.transform.SetParent(typewriterObj.transform);
                    keyObj.transform.localPosition = new Vector3(rowStartX + (c * stepX), rowY[r], rowZ[r]);
                    keyObj.transform.localRotation = Quaternion.Euler(16f, 0, 0);
                    keyObj.transform.localScale = new Vector3(0.082f, 0.038f, 0.082f);
                    keyObj.GetComponent<Renderer>().sharedMaterial = keyNormalMat;

                    // Geniş ve rahat tıklama için temiz BoxCollider
                    var oldCol = keyObj.GetComponent<Collider>();
                    if (oldCol != null) Object.DestroyImmediate(oldCol);
                    BoxCollider bc = keyObj.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0, 0.1f, 0);
                    bc.size = new Vector3(1.3f, 2.5f, 1.3f);

                    TypewriterKey keyComp = keyObj.AddComponent<TypewriterKey>();

                    // Tuş üzerindeki harf etiketi
                    GameObject labelObj = new GameObject("KeyLabel");
                    labelObj.transform.SetParent(keyObj.transform);
                    labelObj.transform.localPosition = new Vector3(0, 1.08f, 0);
                    labelObj.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    labelObj.transform.localScale = Vector3.one * 0.13f;
                    TextMeshPro keyTMP = labelObj.AddComponent<TextMeshPro>();
                    keyTMP.text = letters[keyIndex].ToString();
                    keyTMP.fontSize = 28f;
                    keyTMP.fontStyle = FontStyles.Bold;
                    keyTMP.color = new Color(0.12f, 0.12f, 0.14f);
                    keyTMP.alignment = TextAlignmentOptions.Center;

                    SetSerializedField(keyComp, "keyRenderer", keyObj.GetComponent<Renderer>());
                    SetSerializedField(keyComp, "movingCap", keyObj.transform);
                    SetSerializedField(keyComp, "labelText", keyTMP);

                    keyComp.Initialize(keyIndex, letters[keyIndex]);
                    keysList.Add(keyComp);
                    keyIndex++;
                }
            }

            typewriterMachine.SetKeysList(keysList);
            typewriterMachine.SetPaper(twPaper);
            typewriterMachine.SetCarriage(carriageRoot.transform);

            // 7. 4 Player Seats (3D Bodyguard Karakterleri: Feg, Kenan, Feyzi, MMG)
            BodyguardSetupTool.EnsurePrefabsExist();
            List<PlayerSeat> seats = new List<PlayerSeat>();
            GameObject[] charPrefabs = new GameObject[4];
            Vector3[] seatPositions = new Vector3[] {
                new Vector3(0f, 0f, -2.05f),   // P1 (Güney - Feg)
                new Vector3(0f, 0f, 1.48f),    // P2 (Kuzey - Kenan)
                new Vector3(-1.68f, 0f, 0f),   // P3 (Batı - Feyzi)
                new Vector3(1.68f, 0f, 0f)     // P4 (Doğu - MMG)
            };
            Quaternion[] seatRotations = new Quaternion[] {
                Quaternion.Euler(0, 0, 0),
                Quaternion.Euler(0, 180, 0),
                Quaternion.Euler(0, 90, 0),
                Quaternion.Euler(0, -90, 0)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject seatObj = new GameObject($"PlayerSeat_{i + 1}");
                seatObj.transform.position = seatPositions[i];
                seatObj.transform.rotation = seatRotations[i];

                // Ahşap Demirci Taburesi (3DForge Stool - Sıfır İlkel Mesh)
                SpawnForgeProp("Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Forge/Forge_Props/fi_vil_forge_stool1.prefab",
                    seatObj.transform, Vector3.zero, Quaternion.identity, Vector3.one);

                // Kamera Bağlantı Noktası (P1 dünya konumu: X:0, Y:2.45, Z:-3.25 ve rotasyon X:26°)
                GameObject camAnchor = new GameObject("CameraMount");
                camAnchor.transform.SetParent(seatObj.transform);
                camAnchor.transform.localPosition = new Vector3(0f, 2.45f, -1.20f);
                camAnchor.transform.localRotation = Quaternion.Euler(26f, 0f, 0f);

                // Kasap Belirme Noktası (Koltuğun arkası)
                GameObject butcherAnchor = new GameObject("ButcherSpawnPoint");
                butcherAnchor.transform.SetParent(seatObj.transform);
                butcherAnchor.transform.localPosition = new Vector3(0, 0, -0.85f);
                butcherAnchor.transform.localRotation = Quaternion.identity;

                // 3D Bodyguard Karakter Prefab'ı (Feg, Kenan, Feyzi, MMG)
                CharacterArchetype archetype = (CharacterArchetype)i;
                var charDef = CharacterDefinition.GetDefinition(archetype);
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(charDef.PrefabAssetPath);
                charPrefabs[i] = prefabAsset;

                PlayerVisualAvatar avatarComp = null;
                if (prefabAsset != null)
                {
                    GameObject avatarInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, seatObj.transform);
                    avatarInstance.name = $"Avatar_{charDef.DisplayName}";
                    avatarInstance.transform.localPosition = Vector3.zero;
                    avatarInstance.transform.localRotation = Quaternion.identity;
                    avatarInstance.transform.localScale = Vector3.one;
                    avatarComp = avatarInstance.GetComponent<PlayerVisualAvatar>();
                    if (avatarComp != null)
                    {
                        avatarComp.SetPlayerColor(charDef.PrimaryColor);
                    }
                }
                else
                {
                    Debug.LogError($"[SceneSetupTool] Karakter prefab'ı bulunamadı: {charDef.PrefabAssetPath}");
                }

                PlayerSeat seatComp = seatObj.AddComponent<PlayerSeat>();
                SetSerializedField(seatComp, "cameraMountPoint", camAnchor.transform);
                seatComp.SetupSeat(i, charDef.PrimaryColor);
                seatComp.SetAvatar(avatarComp);
                seats.Add(seatComp);
            }

            // 8. Butcher Character & 4 Absurd Execution Weapons
            GameObject butcherObj = new GameObject("ButcherPlaceholder");
            butcherObj.transform.position = new Vector3(1.8f, 0f, -1.8f); // Masa etrafındaki devriye çemberi üzerinde başlasın
            GameObject bVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bVisual.name = "ButcherCapsule";
            bVisual.transform.SetParent(butcherObj.transform);
            bVisual.transform.localPosition = new Vector3(0, 1.25f, 0);
            bVisual.transform.localScale = new Vector3(0.85f, 1.25f, 0.85f);
            bVisual.GetComponent<Renderer>().sharedMaterial = butcherMat;

            // Kasap Önlüğü
            GameObject bApron = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bApron.name = "ButcherApron";
            bApron.transform.SetParent(bVisual.transform);
            bApron.transform.localPosition = new Vector3(0, -0.05f, 0.44f);
            bApron.transform.localScale = new Vector3(0.7f, 0.8f, 0.08f);
            bApron.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial("Mat_ButcherApron", new Color(0.95f, 0.95f, 0.92f));
            if (bApron.GetComponent<Collider>() != null) Object.DestroyImmediate(bApron.GetComponent<Collider>());

            // Kasap Şapkası
            GameObject bHat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bHat.name = "ChefHat";
            bHat.transform.SetParent(bVisual.transform);
            bHat.transform.localPosition = new Vector3(0, 0.95f, 0);
            bHat.transform.localScale = new Vector3(0.55f, 0.25f, 0.55f);
            bHat.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial("Mat_ChefHat", Color.white);
            if (bHat.GetComponent<Collider>() != null) Object.DestroyImmediate(bHat.GetComponent<Collider>());

            // Silah Taşıyıcı Kol Pivotu
            GameObject bArmPivot = new GameObject("WeaponArmPivot");
            bArmPivot.transform.SetParent(bVisual.transform);
            bArmPivot.transform.localPosition = new Vector3(0.55f, 0.65f, 0.2f);

            // Silah 0: Dev Et Çekici (Giant Meat Mallet)
            GameObject wMallet = new GameObject("Weapon_GiantMallet");
            wMallet.transform.SetParent(bArmPivot.transform, false);
            GameObject mHandle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mHandle.transform.SetParent(wMallet.transform);
            mHandle.transform.localPosition = new Vector3(0, 0.45f, 0);
            mHandle.transform.localScale = new Vector3(0.08f, 0.5f, 0.08f);
            mHandle.GetComponent<Renderer>().sharedMaterial = tableMat;
            GameObject mHead = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mHead.transform.SetParent(wMallet.transform);
            mHead.transform.localPosition = new Vector3(0, 0.9f, 0);
            mHead.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            mHead.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);
            mHead.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial("Mat_MalletHead", new Color(0.45f, 0.25f, 0.12f));

            // Silah 1: Uçan Döküm Tava (Flying Cast-Iron Pan)
            GameObject wPan = new GameObject("Weapon_CastIronPan");
            wPan.transform.SetParent(bArmPivot.transform, false);
            GameObject panBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            panBody.transform.SetParent(wPan.transform);
            panBody.transform.localPosition = new Vector3(0, 0.8f, 0.1f);
            panBody.transform.localScale = new Vector3(0.55f, 0.05f, 0.55f);
            panBody.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial("Mat_CastIron", new Color(0.1f, 0.1f, 0.12f));
            GameObject panHandle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panHandle.transform.SetParent(wPan.transform);
            panHandle.transform.localPosition = new Vector3(0, 0.35f, 0.1f);
            panHandle.transform.localScale = new Vector3(0.08f, 0.45f, 0.04f);
            panHandle.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial("Mat_CastIron", new Color(0.1f, 0.1f, 0.12f));

            // Silah 2: Acılı Sosis Kırbacı (Sausage Whip)
            GameObject wWhip = new GameObject("Weapon_SausageWhip");
            wWhip.transform.SetParent(bArmPivot.transform, false);
            for (int s = 0; s < 3; s++)
            {
                GameObject sLink = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                sLink.transform.SetParent(wWhip.transform);
                sLink.transform.localPosition = new Vector3(0, 0.35f + s * 0.32f, s * 0.08f);
                sLink.transform.localRotation = Quaternion.Euler(s * 15f, 0, 0);
                sLink.transform.localScale = new Vector3(0.15f, 0.22f, 0.15f);
                sLink.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial("Mat_Sausage", new Color(0.85f, 0.2f, 0.15f));
            }

            // Silah 3: Kremalı Pasta Fırlatıcı (Cream Pie Launcher)
            GameObject wPie = new GameObject("Weapon_CreamPie");
            wPie.transform.SetParent(bArmPivot.transform, false);
            GameObject pieCrust = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pieCrust.transform.SetParent(wPie.transform);
            pieCrust.transform.localPosition = new Vector3(0, 0.7f, 0);
            pieCrust.transform.localScale = new Vector3(0.5f, 0.06f, 0.5f);
            pieCrust.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial("Mat_PieCrust", new Color(0.92f, 0.75f, 0.4f));
            GameObject pieCream = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pieCream.transform.SetParent(wPie.transform);
            pieCream.transform.localPosition = new Vector3(0, 0.78f, 0);
            pieCream.transform.localScale = new Vector3(0.42f, 0.18f, 0.42f);
            pieCream.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial("Mat_PieCream", new Color(0.98f, 0.95f, 0.96f));
            GameObject pieCherry = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pieCherry.transform.SetParent(wPie.transform);
            pieCherry.transform.localPosition = new Vector3(0, 0.88f, 0);
            pieCherry.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
            pieCherry.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial("Mat_Cherry", Color.red);

            GameObject[] butcherWeapons = new GameObject[] { wMallet, wPan, wWhip, wPie };

            ButcherPlaceholder butcherComp = butcherObj.AddComponent<ButcherPlaceholder>();
            butcherComp.SetupWeapons(bArmPivot.transform, butcherWeapons);

            GameObject kasapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/kasap.fbx");
            if (kasapPrefab != null)
            {
                bVisual.SetActive(false); // Eski ilkel ButcherCapsule görünmesin, yedek olarak kapalı dursun
                SerializedObject soButcher = new SerializedObject(butcherComp);
                SerializedProperty propCustomModel = soButcher.FindProperty("customButcherModelPrefab");
                if (propCustomModel != null)
                {
                    propCustomModel.objectReferenceValue = kasapPrefab;
                }
                soButcher.ApplyModifiedPropertiesWithoutUndo();
                butcherComp.SetCustomButcherModelPrefab(kasapPrefab);
            }
            else
            {
                bVisual.SetActive(true);
            }

            WireButcherAnimationsToComponent(butcherComp);

            wMallet.SetActive(true);
            wPan.SetActive(false);
            wWhip.SetActive(false);
            wPie.SetActive(false);
            SetLayerRecursivelyEditor(butcherObj, 0); // Default katman (Layer 0) — Kasap sahnede sürekli görünür şekilde devriye gezer

            // 9. UI Canvas
            GameObject canvasObj = new GameObject("GameCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // EventSystem
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();

            // HUD
            HUDController hud = CreateHUDHierarchy(canvasObj.transform);

            // Pause Menu
            PauseMenuController pauseMenu = CreatePauseMenuHierarchy(canvasObj.transform);

            // Game Over Screen
            GameOverUIController gameOverUI = CreateGameOverHierarchy(canvasObj.transform);

            // Emote Wheel UI
            EmoteWheelUI emoteWheel = CreateEmoteWheelHierarchy(canvasObj.transform);

            // Card Selection Mini-Game UI
            FingerMonster.Gameplay.CardSelectionController cardSelectionUI = CreateCardSelectionHierarchy(canvasObj.transform);

            // 10. Cartoon FX Manager
            GameObject fxObj = new GameObject("CartoonFXManager");
            fxObj.AddComponent<CartoonFXManager>();

            // 10b. Execution Darkness & Silhouette Reflection System
            GameObject reflObj = new GameObject("ExecutionReflectionSystem");
            ExecutionReflectionSystem reflSys = reflObj.AddComponent<ExecutionReflectionSystem>();

            GameObject reflCamObj = new GameObject("ReflectionCamera");
            reflCamObj.transform.SetParent(reflObj.transform);
            reflCamObj.transform.position = new Vector3(0, 1.35f, -0.4f);
            reflCamObj.transform.rotation = Quaternion.Euler(15f, 0, 0);
            Camera reflCam = reflCamObj.AddComponent<Camera>();
            reflCam.clearFlags = CameraClearFlags.SolidColor;
            reflCam.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            reflCam.fieldOfView = 65f;
            reflCam.enabled = false;

            GameObject execSpotObj = new GameObject("ExecutionSpotlight");
            execSpotObj.transform.SetParent(reflObj.transform);
            execSpotObj.transform.position = new Vector3(0, 2.8f, 0);
            execSpotObj.transform.rotation = Quaternion.Euler(75f, 0, 0);
            Light execSpot = execSpotObj.AddComponent<Light>();
            execSpot.type = LightType.Spot;
            execSpot.spotAngle = 45f;
            execSpot.color = new Color(0.9f, 0.2f, 0.2f);
            execSpot.intensity = 3.5f;
            execSpot.range = 8.0f;
            execSpot.enabled = false;

            GameObject rimObj = new GameObject("SilhouetteRimLight");
            rimObj.transform.SetParent(reflObj.transform);
            rimObj.transform.position = new Vector3(0, 1.2f, 1.5f);
            rimObj.transform.rotation = Quaternion.Euler(180f, 0, 0);
            Light rimLight = rimObj.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.color = new Color(0.35f, 0.65f, 1.0f);
            rimLight.intensity = 1.2f;
            rimLight.enabled = false;

            Renderer drumRenderer = typewriterObj != null ? typewriterObj.GetComponentInChildren<Renderer>() : null;
            reflSys.SetupComponents(reflCam, drumRenderer, execSpot, rimLight);

            // 11. GameManager & Controllers Root
            GameSettings defaultSettings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Settings/DefaultGameSettings.asset");
            if (defaultSettings == null)
            {
                defaultSettings = ScriptableObject.CreateInstance<GameSettings>();
                AssetDatabase.CreateAsset(defaultSettings, "Assets/Settings/DefaultGameSettings.asset");
            }

            GameObject gameManagerObj = new GameObject("GameManager");
            GameManager gm = gameManagerObj.AddComponent<GameManager>();
            TurnController tc = gameManagerObj.AddComponent<TurnController>();
            PlayerInputHandler inputHandler = gameManagerObj.AddComponent<PlayerInputHandler>();
            gameManagerObj.AddComponent<FingerMonster.AI.AIBrain>();
            gameManagerObj.AddComponent<FingerMonster.AI.AIPlayerController>();

            // Bağımsız AudioManager (GameManager'dan ayrı tutulmalıdır)
            GameObject audioMgrObj = new GameObject("AudioManager");
            audioMgrObj.AddComponent<AudioManager>();

            // GameManager Inspector Bağlantılarını Ata
            SetSerializedField(gm, "settings", defaultSettings);
            SetSerializedField(gm, "typewriter", typewriterMachine);
            SetSerializedField(gm, "turnController", tc);
            SetSerializedField(gm, "cameraController", camController);
            SetSerializedField(gm, "lightingController", lightingController);
            SetSerializedField(gm, "butcher", butcherComp);
            SetSerializedField(gm, "playerSeats", seats);
            SetSerializedField(gm, "characterPrefabs", charPrefabs);
            SetSerializedField(gm, "hudController", hud);
            SetSerializedField(gm, "gameOverController", gameOverUI);
            SetSerializedField(gm, "pauseMenuController", pauseMenu);
            SetSerializedField(gm, "cardSelectionController", cardSelectionUI);

            // NetworkManager & NetworkGameManager
            // NetworkManager MainMenu sahnesinde DontDestroyOnLoad olarak korunur; Game sahnesinde tekil test harici mükerrer oluşturulmaz:
            if (Object.FindFirstObjectByType<NetworkManager>() == null)
            {
                GameObject netMgrObj = new GameObject("NetworkManager");
                NetworkManager netMgr = netMgrObj.AddComponent<NetworkManager>();
                UnityTransport transport = netMgrObj.AddComponent<UnityTransport>();
                netMgr.NetworkConfig.NetworkTransport = transport;
            }

            GameObject netGameMgrObj = new GameObject("NetworkGameManager");
            netGameMgrObj.AddComponent<NetworkObject>();
            NetworkGameManager netGm = netGameMgrObj.AddComponent<NetworkGameManager>();
            SetSerializedField(netGm, "typewriter", typewriterMachine);
            SetSerializedField(netGm, "hudController", hud);
            SetSerializedField(netGm, "gameOverController", gameOverUI);
            SetSerializedField(netGm, "lightingController", lightingController);
            SetSerializedField(netGm, "butcher", butcherComp);
            SetSerializedField(netGm, "playerSeats", seats);
            SetSerializedField(netGm, "characterPrefabs", charPrefabs);
            SetSerializedField(netGm, "aiBrain", gameManagerObj.GetComponent<FingerMonster.AI.AIBrain>());

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Game.unity");
            Debug.Log("[SceneSetupTool] Game sahnesi başarıyla kaydedildi.");
        }
        #endregion

        #region MAIN MENU SCENE BUILDER
        public static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Kamera
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.06f);
            cam.fieldOfView = 58f;
            camObj.transform.position = new Vector3(0f, 1.55f, -3.10f);
            camObj.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            camObj.AddComponent<AudioListener>();

            // Event System
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();

            // Canvas
            GameObject canvasObj = new GameObject("MainMenuCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // AudioManager
            GameObject audioMgrObj = new GameObject("AudioManager");
            audioMgrObj.AddComponent<AudioManager>();

            // NetworkManager, RoomManager & LANDiscovery
            GameObject netMgrObj = new GameObject("NetworkManager");
            NetworkManager netMgr = netMgrObj.AddComponent<NetworkManager>();
            UnityTransport transport = netMgrObj.AddComponent<UnityTransport>();
            netMgr.NetworkConfig.NetworkTransport = transport;
            netMgrObj.AddComponent<NetworkRoomManager>();
            netMgrObj.AddComponent<LANDiscovery>();

            // Ana Panel
            GameObject mainPanel = CreateUIPanel(canvasObj.transform, "MainPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 820));
            mainPanel.GetComponent<Image>().color = new Color(0.07f, 0.06f, 0.08f, 0.84f);

            // Başlık
            GameObject titleObj = CreateUIText(mainPanel.transform, "TitleText", "FINGER MONSTER", 66, FontStyles.Bold, new Color(0.98f, 0.24f, 0.18f), new Vector2(0, 330), new Vector2(850, 90));
            CreateUIText(mainPanel.transform, "SubtitleText", "4 Kişilik Sıra Tabanlı Karanlık Komedi Parti Oyunu", 21, FontStyles.Normal, new Color(0.92f, 0.86f, 0.76f), new Vector2(0, 274), new Vector2(850, 40));

            // Karakter Seçim Kartları (Feg, Kenan, Feyzi, MMG) — 205x96 geniş kartlar
            CreateUIText(mainPanel.transform, "CharSelectTitle", "KARAKTERİNİ SEÇ:", 20, FontStyles.Bold, new Color(1f, 0.85f, 0.35f), new Vector2(0, 232), new Vector2(700, 32));
            Button[] charSelectBtns = new Button[4];
            Image[] charCardBgs = new Image[4];
            float cardStartX = -330f;
            float cardStepX = 220f;
            for (int c = 0; c < 4; c++)
            {
                var def = CharacterDefinition.GetDefinition((CharacterArchetype)c);
                float cx = cardStartX + (c * cardStepX);
                string cardLabel =
                    $"<b><size=112%>{def.DisplayName}</size></b>\n" +
                    $"<size=80%><color=#{ColorUtility.ToHtmlStringRGB(def.AccentColor)}>[{def.PersonalityTag}]</color></size>\n" +
                    $"<size=72%><color=#CFC8BC>{def.Title}</color></size>";
                Button cBtn = CreateUIButton(mainPanel.transform, $"CharBtn_{def.DisplayName}", cardLabel, new Vector2(cx, 162), new Vector2(205, 96), def.PrimaryColor * 0.65f);
                TextMeshProUGUI cTmp = cBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (cTmp != null) cTmp.fontSize = 18f;
                charSelectBtns[c] = cBtn;
                charCardBgs[c] = cBtn.GetComponent<Image>();
            }
            GameObject charInfoObj = CreateUIText(mainPanel.transform, "SelectedCharacterInfoText", "", 17, FontStyles.Normal, new Color(0.9f, 0.92f, 0.96f), new Vector2(0, 76), new Vector2(760, 64));
            TextMeshProUGUI charInfoText = charInfoObj.GetComponent<TextMeshProUGUI>();

            // Mod & Zorluk Butonları
            Button modeBtn = CreateUIButton(mainPanel.transform, "ModeButton", "MOD: 1 OYUNCU (VS 3 BOT)", new Vector2(-190, 4), new Vector2(360, 50), new Color(0.25f, 0.32f, 0.45f));
            TextMeshProUGUI modeBtnText = modeBtn.GetComponentInChildren<TextMeshProUGUI>();
            Button diffBtn = CreateUIButton(mainPanel.transform, "DifficultyButton", "ZORLUK: NORMAL (6 TUZAK)", new Vector2(190, 4), new Vector2(360, 50), new Color(0.35f, 0.28f, 0.42f));
            TextMeshProUGUI diffBtnText = diffBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Ana Butonlar (SettingsButton & QuitButton yan yana)
            Button playBtn = CreateUIButton(mainPanel.transform, "PlayButton", "TEK BİLGİSAYAR (SOLO / HOTSEAT)", new Vector2(0, -68), new Vector2(540, 62), new Color(0.78f, 0.24f, 0.16f));
            Button lanBtn = CreateUIButton(mainPanel.transform, "LanButton", "LAN ÇOK OYUNCULU (HOST / KATIL)", new Vector2(0, -142), new Vector2(540, 60), new Color(0.16f, 0.46f, 0.76f));
            Button settingsBtn = CreateUIButton(mainPanel.transform, "SettingsButton", "AYARLAR", new Vector2(-140, -218), new Vector2(260, 54), new Color(0.35f, 0.35f, 0.45f));
            Button quitBtn = CreateUIButton(mainPanel.transform, "QuitButton", "ÇIKIŞ", new Vector2(140, -218), new Vector2(260, 54), new Color(0.6f, 0.2f, 0.2f));

            // Ayarlar Paneli (Pop-up, raycastTarget = true)
            GameObject settingsPanel = CreateUIPanel(canvasObj.transform, "SettingsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 560), raycastTarget: true);
            settingsPanel.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.98f);
            CreateUIText(settingsPanel.transform, "SettingsTitle", "SES AYARLARI", 42, FontStyles.Bold, Color.white, new Vector2(0, 210), new Vector2(500, 60));

            // Genel Ses (Master)
            CreateUIText(settingsPanel.transform, "MasterLabel", "Genel Ses", 22, FontStyles.Normal, Color.white, new Vector2(-190, 110), new Vector2(180, 40));
            Button mMinusBtn = CreateUIButton(settingsPanel.transform, "MasterMinus", "-", new Vector2(-75, 110), new Vector2(38, 38), new Color(0.25f, 0.25f, 0.35f));
            Slider masterSlider = CreateUISlider(settingsPanel.transform, "MasterSlider", new Vector2(65, 110));
            Button mPlusBtn = CreateUIButton(settingsPanel.transform, "MasterPlus", "+", new Vector2(205, 110), new Vector2(38, 38), new Color(0.25f, 0.25f, 0.35f));
            GameObject mValObj = CreateUIText(settingsPanel.transform, "MasterValText", "%100", 20, FontStyles.Bold, Color.yellow, new Vector2(268, 110), new Vector2(70, 36));

            // Efekt Sesi (SFX)
            CreateUIText(settingsPanel.transform, "SfxLabel", "Efekt Sesi", 22, FontStyles.Normal, Color.white, new Vector2(-190, 30), new Vector2(180, 40));
            Button sMinusBtn = CreateUIButton(settingsPanel.transform, "SfxMinus", "-", new Vector2(-75, 30), new Vector2(38, 38), new Color(0.25f, 0.25f, 0.35f));
            Slider sfxSlider = CreateUISlider(settingsPanel.transform, "SfxSlider", new Vector2(65, 30));
            Button sPlusBtn = CreateUIButton(settingsPanel.transform, "SfxPlus", "+", new Vector2(205, 30), new Vector2(38, 38), new Color(0.25f, 0.25f, 0.35f));
            GameObject sValObj = CreateUIText(settingsPanel.transform, "SfxValText", "%100", 20, FontStyles.Bold, Color.yellow, new Vector2(268, 30), new Vector2(70, 36));

            // Müzik Sesi (BGM)
            CreateUIText(settingsPanel.transform, "MusicLabel", "Müzik Sesi", 22, FontStyles.Normal, Color.white, new Vector2(-190, -50), new Vector2(180, 40));
            Button bgmMinusBtn = CreateUIButton(settingsPanel.transform, "MusicMinus", "-", new Vector2(-75, -50), new Vector2(38, 38), new Color(0.25f, 0.25f, 0.35f));
            Slider musicSlider = CreateUISlider(settingsPanel.transform, "MusicSlider", new Vector2(65, -50));
            Button bgmPlusBtn = CreateUIButton(settingsPanel.transform, "MusicPlus", "+", new Vector2(205, -50), new Vector2(38, 38), new Color(0.25f, 0.25f, 0.35f));
            GameObject bgmValObj = CreateUIText(settingsPanel.transform, "MusicValText", "%65", 20, FontStyles.Bold, Color.yellow, new Vector2(268, -50), new Vector2(70, 36));

            Button backBtn = CreateUIButton(settingsPanel.transform, "BackButton", "KAPAT VE KAYDET", new Vector2(0, -170), new Vector2(280, 55), new Color(0.2f, 0.55f, 0.85f));

            SettingsMenuController settingsCtrl = settingsPanel.AddComponent<SettingsMenuController>();
            SetSerializedField(settingsCtrl, "settingsPanelRoot", settingsPanel);
            SetSerializedField(settingsCtrl, "masterVolumeSlider", masterSlider);
            SetSerializedField(settingsCtrl, "sfxVolumeSlider", sfxSlider);
            SetSerializedField(settingsCtrl, "musicVolumeSlider", musicSlider);
            SetSerializedField(settingsCtrl, "masterValueText", mValObj.GetComponent<TextMeshProUGUI>());
            SetSerializedField(settingsCtrl, "sfxValueText", sValObj.GetComponent<TextMeshProUGUI>());
            SetSerializedField(settingsCtrl, "musicValueText", bgmValObj.GetComponent<TextMeshProUGUI>());
            SetSerializedField(settingsCtrl, "backButton", backBtn);

            mMinusBtn.onClick.AddListener(() => settingsCtrl.AdjustMasterStep(-0.1f));
            mPlusBtn.onClick.AddListener(() => settingsCtrl.AdjustMasterStep(0.1f));
            sMinusBtn.onClick.AddListener(() => settingsCtrl.AdjustSfxStep(-0.1f));
            sPlusBtn.onClick.AddListener(() => settingsCtrl.AdjustSfxStep(0.1f));
            bgmMinusBtn.onClick.AddListener(() => settingsCtrl.AdjustMusicStep(-0.1f));
            bgmPlusBtn.onClick.AddListener(() => settingsCtrl.AdjustMusicStep(0.1f));

            settingsPanel.SetActive(false);

            // Lobi Paneli (Pop-up, raycastTarget = true)
            GameObject lobbyPanel = CreateUIPanel(canvasObj.transform, "LobbyPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 700), raycastTarget: true);
            lobbyPanel.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 0.98f);

            // 1. Browser Panel (Oda Listesi ve Bağlantı)
            GameObject browserPanel = CreateUIPanel(lobbyPanel.transform, "BrowserPanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            browserPanel.GetComponent<Image>().color = Color.clear;

            CreateUIText(browserPanel.transform, "BrowserTitle", "LAN ÇOK OYUNCULU LOBİSİ", 36, FontStyles.Bold, Color.white, new Vector2(0, 280), new Vector2(700, 50));

            // Oyuncu Adı
            CreateUIText(browserPanel.transform, "NameLabel", "Oyuncu Adınız:", 20, FontStyles.Normal, Color.yellow, new Vector2(-220, 215), new Vector2(250, 30));
            TMP_InputField pNameInput = CreateUIInputField(browserPanel.transform, "PlayerNameInput", "Oyuncu", new Vector2(-220, 180), new Vector2(250, 45));

            // Oda Adı & Kur
            CreateUIText(browserPanel.transform, "RoomLabel", "Oda Adı:", 20, FontStyles.Normal, Color.yellow, new Vector2(220, 215), new Vector2(250, 30));
            TMP_InputField rNameInput = CreateUIInputField(browserPanel.transform, "RoomNameInput", "FingerMonster Oda", new Vector2(220, 180), new Vector2(250, 45));
            Button createRoomBtn = CreateUIButton(browserPanel.transform, "CreateRoomBtn", "ODA KUR (HOST)", new Vector2(220, 115), new Vector2(250, 55), new Color(0.2f, 0.65f, 0.3f));

            // Doğrudan IP ile Bağlan
            CreateUIText(browserPanel.transform, "IpLabel", "Doğrudan IP / Port:", 20, FontStyles.Normal, Color.cyan, new Vector2(-220, 125), new Vector2(250, 30));
            TMP_InputField ipInput = CreateUIInputField(browserPanel.transform, "IpInput", "127.0.0.1", new Vector2(-280, 85), new Vector2(130, 40));
            TMP_InputField portInput = CreateUIInputField(browserPanel.transform, "PortInput", "7777", new Vector2(-160, 85), new Vector2(80, 40));
            Button directJoinBtn = CreateUIButton(browserPanel.transform, "DirectJoinBtn", "IP İLE BAĞLAN", new Vector2(-220, 25), new Vector2(250, 45), new Color(0.2f, 0.45f, 0.85f));

            // Otomatik Keşif Odaları Listesi
            CreateUIText(browserPanel.transform, "ListLabel", "--- YEREL AĞ ODALARI (UDP KEŞİF) ---", 20, FontStyles.Bold, new Color(0.85f, 0.85f, 0.85f), new Vector2(0, -35), new Vector2(600, 30));
            GameObject roomsContainer = CreateUIPanel(browserPanel.transform, "DiscoveredRoomsContainer", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -135), new Vector2(650, 140));
            roomsContainer.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 0.85f);
            var layout = roomsContainer.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.spacing = 6;
            layout.padding = new RectOffset(10, 10, 10, 10);

            GameObject statusObj = CreateUIText(browserPanel.transform, "StatusText", "LAN odaları taranıyor...", 18, FontStyles.Italic, Color.gray, new Vector2(0, -235), new Vector2(700, 30));
            TextMeshProUGUI statusText = statusObj.GetComponent<TextMeshProUGUI>();

            Button browserBackBtn = CreateUIButton(browserPanel.transform, "BrowserBackBtn", "ANA MENÜYE DÖN", new Vector2(0, -285), new Vector2(260, 50), new Color(0.5f, 0.25f, 0.25f));

            // 2. Room Panel (Oda İçi Lobi)
            GameObject roomPanel = CreateUIPanel(lobbyPanel.transform, "RoomPanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            roomPanel.GetComponent<Image>().color = Color.clear;

            GameObject rHeaderObj = CreateUIText(roomPanel.transform, "RoomHeaderTitle", "ODA ADI", 38, FontStyles.Bold, Color.yellow, new Vector2(0, 280), new Vector2(700, 50));
            TextMeshProUGUI rHeader = rHeaderObj.GetComponent<TextMeshProUGUI>();

            GameObject rDetailObj = CreateUIText(roomPanel.transform, "RoomDetailsText", "Statü: Host", 20, FontStyles.Normal, Color.white, new Vector2(0, 240), new Vector2(600, 35));
            TextMeshProUGUI rDetail = rDetailObj.GetComponent<TextMeshProUGUI>();

            // 4 Koltuk Kartları Konteyneri
            GameObject seatCardsContainer = new GameObject("SeatCardsContainer");
            seatCardsContainer.transform.SetParent(roomPanel.transform, false);
            var seatRect = seatCardsContainer.AddComponent<RectTransform>();
            seatRect.anchoredPosition = new Vector2(0, 70);
            seatRect.sizeDelta = new Vector2(800, 240);
            var seatLayout = seatCardsContainer.AddComponent<HorizontalLayoutGroup>();
            seatLayout.childControlWidth = false;
            seatLayout.childControlHeight = false;
            seatLayout.spacing = 20;
            seatLayout.childAlignment = TextAnchor.MiddleCenter;

            // Kontrol Butonları
            Button botToggleBtn = CreateUIButton(roomPanel.transform, "BotFillToggleBtn", "BOŞ KOLTUKLAR: BOTLARLA DOLDUR", new Vector2(-220, -100), new Vector2(360, 60), new Color(0.2f, 0.5f, 0.8f));
            TextMeshProUGUI botToggleText = botToggleBtn.GetComponentInChildren<TextMeshProUGUI>();

            Button readyBtn = CreateUIButton(roomPanel.transform, "ReadyToggleBtn", "HAZIR OL", new Vector2(220, -100), new Vector2(260, 60), new Color(0.2f, 0.7f, 0.3f));
            TextMeshProUGUI readyText = readyBtn.GetComponentInChildren<TextMeshProUGUI>();

            Button startMatchBtn = CreateUIButton(roomPanel.transform, "StartMatchBtn", "OYUNU BAŞLAT (HOST)", new Vector2(0, -185), new Vector2(380, 70), new Color(0.85f, 0.2f, 0.2f));

            Button leaveBtn = CreateUIButton(roomPanel.transform, "LeaveBtn", "ODADAN AYRIL", new Vector2(0, -270), new Vector2(240, 50), new Color(0.45f, 0.45f, 0.5f));

            // NetworkLobbyUI Bağlantıları
            NetworkLobbyUI lobbyUI = lobbyPanel.AddComponent<NetworkLobbyUI>();
            SetSerializedField(lobbyUI, "lobbyRoot", lobbyPanel);
            SetSerializedField(lobbyUI, "browserPanel", browserPanel);
            SetSerializedField(lobbyUI, "roomPanel", roomPanel);
            SetSerializedField(lobbyUI, "playerNameInput", pNameInput);
            SetSerializedField(lobbyUI, "roomNameInput", rNameInput);
            SetSerializedField(lobbyUI, "createRoomButton", createRoomBtn);
            SetSerializedField(lobbyUI, "directIpInput", ipInput);
            SetSerializedField(lobbyUI, "directPortInput", portInput);
            SetSerializedField(lobbyUI, "directJoinButton", directJoinBtn);
            SetSerializedField(lobbyUI, "discoveredRoomsContainer", roomsContainer.transform);
            SetSerializedField(lobbyUI, "statusText", statusText);
            SetSerializedField(lobbyUI, "browserBackButton", browserBackBtn);
            SetSerializedField(lobbyUI, "roomHeaderTitle", rHeader);
            SetSerializedField(lobbyUI, "roomDetailsText", rDetail);
            SetSerializedField(lobbyUI, "seatCardsContainer", seatCardsContainer.transform);
            SetSerializedField(lobbyUI, "botFillToggleButton", botToggleBtn);
            SetSerializedField(lobbyUI, "botFillToggleText", botToggleText);
            SetSerializedField(lobbyUI, "readyToggleButton", readyBtn);
            SetSerializedField(lobbyUI, "readyToggleText", readyText);
            SetSerializedField(lobbyUI, "startGameButton", startMatchBtn);
            SetSerializedField(lobbyUI, "leaveRoomButton", leaveBtn);

            lobbyPanel.SetActive(false);

            // Controller
            MainMenuController menuController = canvasObj.AddComponent<MainMenuController>();
            SetSerializedField(menuController, "playButton", playBtn);
            SetSerializedField(menuController, "lanMultiplayerButton", lanBtn);
            SetSerializedField(menuController, "modeButton", modeBtn);
            SetSerializedField(menuController, "modeButtonText", modeBtnText);
            SetSerializedField(menuController, "difficultyButton", diffBtn);
            SetSerializedField(menuController, "difficultyButtonText", diffBtnText);
            SetSerializedField(menuController, "settingsButton", settingsBtn);
            SetSerializedField(menuController, "quitButton", quitBtn);
            SetSerializedField(menuController, "characterSelectButtons", charSelectBtns);
            SetSerializedField(menuController, "characterCardBackgrounds", charCardBgs);
            SetSerializedField(menuController, "selectedCharacterInfoText", charInfoText);
            SetSerializedField(menuController, "settingsController", settingsCtrl);
            SetSerializedField(menuController, "networkLobbyUI", lobbyUI);

            menuController.EnhanceMainMenuLayoutAndShowcase();
            menuController.RefreshCharacterCards();

            ButcherPlaceholder showcaseButcher = Object.FindFirstObjectByType<ButcherPlaceholder>(FindObjectsInactive.Include);
            if (showcaseButcher != null)
            {
                WireButcherAnimationsToComponent(showcaseButcher);
            }

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
            Debug.Log("[SceneSetupTool] MainMenu sahnesi başarıyla kaydedildi.");
        }
        #endregion

        #region UI HIERARCHY HELPERS
        private static HUDController CreateHUDHierarchy(Transform canvasRoot)
        {
            GameObject hudRoot = new GameObject("HUD");
            hudRoot.transform.SetParent(canvasRoot, false);
            RectTransform hudRect = hudRoot.AddComponent<RectTransform>();
            hudRect.anchorMin = Vector2.zero;
            hudRect.anchorMax = Vector2.one;
            hudRect.sizeDelta = Vector2.zero;

            HUDController hudController = hudRoot.AddComponent<HUDController>();

            // Üst Bilgi Barı (Kompakt, yarı saydam ve oyun alanını kapatmayan mini sayaç)
            GameObject topBar = CreateUIPanel(hudRoot.transform, "TopBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -28), new Vector2(560, 42));
            topBar.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.72f);

            GameObject roundTextObj = CreateUIText(topBar.transform, "RoundText", "TUR 1", 16, FontStyles.Bold, new Color(1f, 0.85f, 0.3f), new Vector2(-190, 0), new Vector2(100, 32));
            GameObject promptTextObj = CreateUIText(topBar.transform, "PromptText", "Sıra: Oyuncu 1", 16, FontStyles.Bold, Color.cyan, new Vector2(10, 0), new Vector2(240, 32));
            GameObject safeKeysTextObj = CreateUIText(topBar.transform, "SafeKeysText", "Kalan: 14", 15, FontStyles.Normal, new Color(0.85f, 0.9f, 1f), new Vector2(210, 0), new Vector2(110, 32));

            // Bildirim Afişi (Banner - Kompakt)
            GameObject bannerObj = CreateUIPanel(hudRoot.transform, "Banner", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(650, 60));
            bannerObj.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.06f, 0.85f);
            GameObject bannerTextObj = CreateUIText(bannerObj.transform, "BannerText", "BİLDİRİM", 24, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(620, 50));
            bannerObj.SetActive(false);

            // Alt Bar (Oyuncu Kartları - Masayı örtmeyen 4 mini kart, ekranın alt kenarında)
            GameObject bottomBar = CreateUIPanel(hudRoot.transform, "BottomBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 38), new Vector2(880, 58));
            bottomBar.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.06f, 0.65f);

            List<PlayerCardUI> cards = new List<PlayerCardUI>();
            float cardWidth = 205f;
            float spacing = 215f;
            float startOffset = -1.5f * spacing;

            for (int i = 0; i < 4; i++)
            {
                GameObject cardObj = CreateUIPanel(bottomBar.transform, $"PlayerCard_{i + 1}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(startOffset + (i * spacing), 0), new Vector2(cardWidth, 48));
                cardObj.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.82f);
                PlayerCardUI cardUI = cardObj.AddComponent<PlayerCardUI>();

                GameObject pName = CreateUIText(cardObj.transform, "PlayerName", $"P{i + 1}", 14, FontStyles.Bold, GetPlayerColor(i), new Vector2(-62, 10), new Vector2(65, 20));
                GameObject pScore = CreateUIText(cardObj.transform, "Score", "0p", 13, FontStyles.Normal, Color.white, new Vector2(-62, -10), new Vector2(65, 18));

                // Sol El Küçük Badge (● / ✕)
                GameObject leftBadge = CreateUIPanel(cardObj.transform, "LeftHandBadge", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(10, 0), new Vector2(46, 24));
                leftBadge.GetComponent<Image>().color = new Color(0.2f, 0.75f, 0.3f);
                GameObject leftText = CreateUIText(leftBadge.transform, "LeftText", "L: ●", 12, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(44, 22));

                // Sağ El Küçük Badge (● / ✕)
                GameObject rightBadge = CreateUIPanel(cardObj.transform, "RightHandBadge", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(64, 0), new Vector2(46, 24));
                rightBadge.GetComponent<Image>().color = new Color(0.2f, 0.75f, 0.3f);
                GameObject rightText = CreateUIText(rightBadge.transform, "RightText", "R: ●", 12, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(44, 22));

                // Elendi Paneli
                GameObject elimOverlay = CreateUIPanel(cardObj.transform, "EliminatedOverlay", new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
                elimOverlay.GetComponent<Image>().color = new Color(0.4f, 0.05f, 0.05f, 0.90f);
                CreateUIText(elimOverlay.transform, "ElimText", "ELENDİ", 14, FontStyles.Bold, Color.red, Vector2.zero, new Vector2(120, 26));
                elimOverlay.SetActive(false);

                // Serialized Fields
                SetSerializedField(cardUI, "cardBackground", cardObj.GetComponent<Image>());
                SetSerializedField(cardUI, "playerNameText", pName.GetComponent<TextMeshProUGUI>());
                SetSerializedField(cardUI, "scoreText", pScore.GetComponent<TextMeshProUGUI>());
                SetSerializedField(cardUI, "leftHandBadge", leftBadge.GetComponent<Image>());
                SetSerializedField(cardUI, "leftHandText", leftText.GetComponent<TextMeshProUGUI>());
                SetSerializedField(cardUI, "rightHandBadge", rightBadge.GetComponent<Image>());
                SetSerializedField(cardUI, "rightHandText", rightText.GetComponent<TextMeshProUGUI>());
                SetSerializedField(cardUI, "eliminatedOverlay", elimOverlay);

                cards.Add(cardUI);
            }

            SetSerializedField(hudController, "roundText", roundTextObj.GetComponent<TextMeshProUGUI>());
            SetSerializedField(hudController, "remainingSafeKeysText", safeKeysTextObj.GetComponent<TextMeshProUGUI>());
            SetSerializedField(hudController, "activePlayerPromptText", promptTextObj.GetComponent<TextMeshProUGUI>());
            SetSerializedField(hudController, "bannerRoot", bannerObj);
            SetSerializedField(hudController, "bannerText", bannerTextObj.GetComponent<TextMeshProUGUI>());
            SetSerializedField(hudController, "playerCards", cards);

            return hudController;
        }

        private static PauseMenuController CreatePauseMenuHierarchy(Transform canvasRoot)
        {
            GameObject pauseRoot = CreateUIPanel(canvasRoot, "PauseMenu", new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            pauseRoot.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.04f, 0.88f);

            GameObject content = CreateUIPanel(pauseRoot.transform, "PauseContent", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 500));
            CreateUIText(content.transform, "PauseTitle", "OYUN DURAKLATILDI", 46, FontStyles.Bold, Color.white, new Vector2(0, 160), new Vector2(550, 60));

            Button resumeBtn = CreateUIButton(content.transform, "ResumeBtn", "DEVAM ET", new Vector2(0, 40), new Vector2(340, 65), new Color(0.2f, 0.6f, 0.3f));
            Button restartBtn = CreateUIButton(content.transform, "RestartBtn", "YENİDEN BAŞLAT", new Vector2(0, -45), new Vector2(340, 65), new Color(0.25f, 0.45f, 0.75f));
            Button menuBtn = CreateUIButton(content.transform, "MainMenuBtn", "ANA MENÜ", new Vector2(0, -130), new Vector2(340, 65), new Color(0.6f, 0.2f, 0.2f));

            PauseMenuController pauseCtrl = pauseRoot.AddComponent<PauseMenuController>();
            SetSerializedField(pauseCtrl, "pausePanelRoot", pauseRoot);
            SetSerializedField(pauseCtrl, "resumeButton", resumeBtn);
            SetSerializedField(pauseCtrl, "restartButton", restartBtn);
            SetSerializedField(pauseCtrl, "mainMenuButton", menuBtn);

            pauseRoot.SetActive(false);
            return pauseCtrl;
        }

        private static GameOverUIController CreateGameOverHierarchy(Transform canvasRoot)
        {
            GameObject goRoot = CreateUIPanel(canvasRoot, "GameOverPanel", new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            goRoot.GetComponent<Image>().color = new Color(0.04f, 0.02f, 0.02f, 0.95f);

            GameObject content = CreateUIPanel(goRoot.transform, "GameOverContent", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(850, 650));
            CreateUIText(content.transform, "Header", "OYUN BİTTİ", 56, FontStyles.Bold, new Color(0.95f, 0.2f, 0.2f), new Vector2(0, 220), new Vector2(600, 70));

            GameObject winTitle = CreateUIText(content.transform, "WinnerTitle", "KAZANAN: OYUNCU 1", 42, FontStyles.Bold, Color.yellow, new Vector2(0, 140), new Vector2(750, 55));
            GameObject winDetail = CreateUIText(content.transform, "WinnerDetail", "Skor: 120 Puan | Kalan El: 2/2", 24, FontStyles.Normal, Color.white, new Vector2(0, 95), new Vector2(600, 40));

            GameObject recapText = CreateUIText(content.transform, "ScoreRecap", "Skor Tablosu", 22, FontStyles.Normal, new Color(0.85f, 0.85f, 0.85f), new Vector2(0, -25), new Vector2(650, 160));

            Button restartBtn = CreateUIButton(content.transform, "RestartBtn", "TEKRAR OYNA", new Vector2(-160, -180), new Vector2(280, 65), new Color(0.2f, 0.65f, 0.3f));
            Button menuBtn = CreateUIButton(content.transform, "MainMenuBtn", "ANA MENÜ", new Vector2(160, -180), new Vector2(280, 65), new Color(0.6f, 0.25f, 0.25f));

            GameOverUIController goCtrl = goRoot.AddComponent<GameOverUIController>();
            SetSerializedField(goCtrl, "panelRoot", goRoot);
            SetSerializedField(goCtrl, "winnerTitleText", winTitle.GetComponent<TextMeshProUGUI>());
            SetSerializedField(goCtrl, "winnerDetailText", winDetail.GetComponent<TextMeshProUGUI>());
            SetSerializedField(goCtrl, "scoreBoardText", recapText.GetComponent<TextMeshProUGUI>());
            SetSerializedField(goCtrl, "restartButton", restartBtn);
            SetSerializedField(goCtrl, "mainMenuButton", menuBtn);

            goRoot.SetActive(false);
            return goCtrl;
        }

        private static GameObject CreateUIPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, bool raycastTarget = false)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            Image img = obj.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.12f, 0.8f);
            img.raycastTarget = raycastTarget;
            return obj;
        }

        private static GameObject CreateUIText(Transform parent, string name, string text, float fontSize, FontStyles style, Color color, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return obj;
        }

        private static Button CreateUIButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 sizeDelta, Color btnColor)
        {
            GameObject obj = CreateUIPanel(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, sizeDelta, raycastTarget: true);
            Image img = obj.GetComponent<Image>();
            img.color = btnColor;
            img.raycastTarget = true;

            Button btn = obj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = btnColor;
            cb.highlightedColor = btnColor * 1.2f;
            cb.pressedColor = btnColor * 0.8f;
            btn.colors = cb;

            CreateUIText(obj.transform, "BtnText", label, 24, FontStyles.Bold, Color.white, Vector2.zero, sizeDelta);
            return btn;
        }

        private static Slider CreateUISlider(Transform parent, string name, Vector2 anchoredPos)
        {
            GameObject sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent, false);
            RectTransform rect = sliderObj.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(240, 32);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.direction = Slider.Direction.LeftToRight;
            slider.interactable = true;

            // Arka Plan
            GameObject bg = CreateUIPanel(sliderObj.transform, "Background", new Vector2(0, 0.25f), new Vector2(1, 0.75f), Vector2.zero, Vector2.zero, raycastTarget: true);
            Image bgImg = bg.GetComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            // Dolu Alan (Fill Area)
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.sizeDelta = new Vector2(-16, 0);

            GameObject fill = CreateUIPanel(fillArea.transform, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, raycastTarget: false);
            Image fillImg = fill.GetComponent<Image>();
            fillImg.color = new Color(0.25f, 0.65f, 0.95f, 1f);
            fillImg.raycastTarget = false;

            // Tutamaç Alanı (Handle Slide Area)
            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0, 0);
            handleAreaRect.anchorMax = new Vector2(1, 1);
            handleAreaRect.sizeDelta = new Vector2(-16, 0);

            GameObject handle = CreateUIPanel(handleArea.transform, "Handle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 34), raycastTarget: true);
            Image handleImg = handle.GetComponent<Image>();
            handleImg.color = new Color(0.95f, 0.95f, 1.0f, 1f);
            handleImg.raycastTarget = true;

            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handleImg;

            return slider;
        }

        private static TMP_InputField CreateUIInputField(Transform parent, string name, string defaultText, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject fieldObj = CreateUIPanel(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, sizeDelta, true);
            Image img = fieldObj.GetComponent<Image>();
            img.color = new Color(0.14f, 0.14f, 0.18f, 0.95f);

            TMP_InputField inputField = fieldObj.AddComponent<TMP_InputField>();

            GameObject textArea = new GameObject("Text Area");
            textArea.transform.SetParent(fieldObj.transform, false);
            RectTransform areaRect = textArea.AddComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(10, 0);
            areaRect.offsetMax = new Vector2(-10, 0);
            textArea.AddComponent<RectMask2D>();

            GameObject textObj = CreateUIText(textArea.transform, "Text", defaultText, 20, FontStyles.Normal, Color.white, Vector2.zero, Vector2.zero);
            TextMeshProUGUI textComp = textObj.GetComponent<TextMeshProUGUI>();
            textComp.alignment = TextAlignmentOptions.MidlineLeft;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            inputField.textViewport = areaRect;
            inputField.textComponent = textComp;
            inputField.text = defaultText;

            return inputField;
        }

        private static EmoteWheelUI CreateEmoteWheelHierarchy(Transform canvasRoot)
        {
            // Her zaman aktif kök nesne (Update & Input dinleyebilmesi için)
            GameObject emoteRoot = new GameObject("EmoteSystemUI");
            emoteRoot.transform.SetParent(canvasRoot, false);
            RectTransform rootRect = emoteRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            CharacterEmoteData[] emoteDatas = BodyguardSetupTool.CreateOrUpdateCharacterEmoteDatas();
            var inputAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");

            // 1. Alt Emote Çubuğu (BottomBar'ın hemen üstünde, 5 Slot + Hedef Seçici) - Sadeleştirilmiş & CanvasGroup ile Dinamik Saydamlık
            GameObject barObj = CreateUIPanel(emoteRoot.transform, "EmoteBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(880f, 40f));
            barObj.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.65f);
            CanvasGroup barCg = barObj.AddComponent<CanvasGroup>();
            barCg.alpha = 0.25f; // Varsayılan yarı saydam, odaklanılmadığında dikkat dağıtmaz

            Button prevTargetBtn = CreateUIButton(barObj.transform, "PrevTargetBtn", "<", new Vector2(-400f, 0f), new Vector2(34f, 30f), new Color(0.2f, 0.2f, 0.26f, 0.8f));
            GameObject targetTxtObj = CreateUIText(barObj.transform, "TargetLabel", "Hedef: ...", 12, FontStyles.Bold, Color.white, new Vector2(-305f, 0f), new Vector2(145f, 30f));
            Button nextTargetBtn = CreateUIButton(barObj.transform, "NextTargetBtn", ">", new Vector2(-210f, 0f), new Vector2(34f, 30f), new Color(0.2f, 0.2f, 0.26f, 0.8f));

            Button[] barBtns = new Button[5];
            TextMeshProUGUI[] barLabels = new TextMeshProUGUI[5];
            for (int i = 0; i < 5; i++)
            {
                float bx = -105f + (i * 115f);
                Button slotBtn = CreateUIButton(barObj.transform, $"EmoteSlotBtn_{i + 1}", $"[{i + 1}]", new Vector2(bx, 0f), new Vector2(108f, 30f), new Color(0.15f, 0.17f, 0.24f, 0.85f));
                barBtns[i] = slotBtn;
                barLabels[i] = slotBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (barLabels[i] != null)
                {
                    barLabels[i].fontSize = 11f;
                    barLabels[i].textWrappingMode = TextWrappingModes.NoWrap;
                    barLabels[i].overflowMode = TextOverflowModes.Ellipsis;
                }
            }

            // Cooldown Overlay
            GameObject cdOverlayObj = CreateUIPanel(barObj.transform, "BarCooldownOverlay", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(125f, 0f), new Vector2(575f, 34f));
            Image cdOverlayImg = cdOverlayObj.GetComponent<Image>();
            cdOverlayImg.color = new Color(0f, 0f, 0f, 0.65f);
            cdOverlayImg.type = Image.Type.Filled;
            cdOverlayImg.fillMethod = Image.FillMethod.Horizontal;
            cdOverlayImg.fillAmount = 0f;
            cdOverlayObj.SetActive(false);
            GameObject cdTxtObj = CreateUIText(barObj.transform, "BarCooldownText", "", 13, FontStyles.Bold, new Color(1f, 0.7f, 0.2f), new Vector2(420f, 0f), new Vector2(45f, 26f));

            // Rehber Aç/Kapa HUD Butonu (Sağ Üst - Küçük '?' İkon Butonu)
            Button guideHudBtn = CreateUIButton(emoteRoot.transform, "ToggleGuideHudBtn", "?", new Vector2(0f, 0f), new Vector2(36f, 36f), new Color(0.15f, 0.22f, 0.35f, 0.75f));
            var guideBtnRect = guideHudBtn.GetComponent<RectTransform>();
            guideBtnRect.anchorMin = new Vector2(1f, 1f);
            guideBtnRect.anchorMax = new Vector2(1f, 1f);
            guideBtnRect.anchoredPosition = new Vector2(-30f, -28f);
            var guideBtnTxt = guideHudBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (guideBtnTxt != null) guideBtnTxt.fontSize = 20f;

            // 2. Gelen Emote Bildirim Şeridi (TopBar altı)
            GameObject notifObj = CreateUIPanel(emoteRoot.transform, "EmoteNotificationToast", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(540f, 46f));
            notifObj.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.18f, 0.92f);
            GameObject notifTxtObj = CreateUIText(notifObj.transform, "NotificationText", "", 19, FontStyles.Normal, Color.white, Vector2.zero, new Vector2(510f, 40f));
            notifObj.SetActive(false);

            // 3. İsteğe Bağlı 5 Dilimli Emote Çarkı ([C] tuşuna basılı tutunca açılır)
            GameObject wheelObj = CreateUIPanel(emoteRoot.transform, "EmoteWheel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 460f));
            wheelObj.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.94f);

            GameObject ringObj = new GameObject("CooldownRing");
            ringObj.transform.SetParent(wheelObj.transform, false);
            var ringRect = ringObj.AddComponent<RectTransform>();
            ringRect.sizeDelta = new Vector2(470f, 470f);
            var ringImg = ringObj.AddComponent<Image>();
            ringImg.color = new Color(1f, 0.45f, 0.15f, 0.85f);
            ringImg.type = Image.Type.Filled;
            ringImg.fillMethod = Image.FillMethod.Radial360;
            ringImg.fillAmount = 0f;

            GameObject centerTextObj = CreateUIText(wheelObj.transform, "CenterTitle", "EMOTE SEÇ\n<size=15>[C] veya [1-5]</size>", 20, FontStyles.Bold, Color.yellow, Vector2.zero, new Vector2(210f, 65f));

            Image[] highlights = new Image[5];
            TextMeshProUGUI[] labels = new TextMeshProUGUI[5];
            float wheelRadius = 152f;
            for (int i = 0; i < 5; i++)
            {
                float rad = (90f - (i * 72f)) * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Cos(rad) * wheelRadius, Mathf.Sin(rad) * wheelRadius);
                GameObject sliceObj = CreateUIPanel(wheelObj.transform, $"Slice_{i}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(142f, 54f));
                Image sImg = sliceObj.GetComponent<Image>();
                sImg.color = new Color(0.2f, 0.2f, 0.26f, 0.65f);
                highlights[i] = sImg;

                GameObject lObj = CreateUIText(sliceObj.transform, "Label", $"[{i + 1}]\nEMOTE {i + 1}", 14, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(136f, 48f));
                labels[i] = lObj.GetComponent<TextMeshProUGUI>();
            }
            wheelObj.SetActive(false);

            EmoteWheelUI wheelCtrl = emoteRoot.AddComponent<EmoteWheelUI>();
            SetSerializedField(wheelCtrl, "inputActions", inputAsset);
            SetSerializedField(wheelCtrl, "characterEmoteDatas", emoteDatas);
            SetSerializedField(wheelCtrl, "emoteBarRoot", barObj);
            SetSerializedField(wheelCtrl, "barSlotButtons", barBtns);
            SetSerializedField(wheelCtrl, "barSlotLabels", barLabels);
            SetSerializedField(wheelCtrl, "barCooldownOverlay", cdOverlayImg);
            SetSerializedField(wheelCtrl, "barCooldownText", cdTxtObj.GetComponent<TextMeshProUGUI>());
            SetSerializedField(wheelCtrl, "emoteBarCanvasGroup", barCg);
            SetSerializedField(wheelCtrl, "targetLabelText", targetTxtObj.GetComponent<TextMeshProUGUI>());
            SetSerializedField(wheelCtrl, "prevTargetButton", prevTargetBtn);
            SetSerializedField(wheelCtrl, "nextTargetButton", nextTargetBtn);
            SetSerializedField(wheelCtrl, "wheelRoot", wheelObj);
            SetSerializedField(wheelCtrl, "cooldownRing", ringImg);
            SetSerializedField(wheelCtrl, "centerStatusText", centerTextObj.GetComponent<TextMeshProUGUI>());
            SetSerializedField(wheelCtrl, "sliceHighlights", highlights);
            SetSerializedField(wheelCtrl, "sliceLabels", labels);
            SetSerializedField(wheelCtrl, "notificationRoot", notifObj);
            SetSerializedField(wheelCtrl, "notificationText", notifTxtObj.GetComponent<TextMeshProUGUI>());

            // 4. Karakter Hareket Rehberi Paneli ([H] ile açılıp kapatılır)
            GameObject guidePanel = CreateUIPanel(emoteRoot.transform, "EmoteGuidePanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1260f, 660f));
            guidePanel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.96f);

            CreateUIText(guidePanel.transform, "GuideTitle", "KARAKTER HAREKET (EMOTE) REHBERİ", 30, FontStyles.Bold, new Color(1f, 0.85f, 0.3f), new Vector2(-100f, 285f), new Vector2(800f, 45f));
            CreateUIText(guidePanel.transform, "GuideControls", "1-5: Hareket Yap   |   Q / E / Tab: Hedef Değiştir   |   C: Emote Çarkı   |   H: Aç / Kapat", 16, FontStyles.Normal, new Color(0.8f, 0.9f, 1f), new Vector2(0f, -295f), new Vector2(1160f, 32f));
            Button guideCloseBtn = CreateUIButton(guidePanel.transform, "GuideCloseBtn", "KAPAT [X]", new Vector2(540f, 285f), new Vector2(130f, 42f), new Color(0.65f, 0.2f, 0.2f));

            TextMeshProUGUI[] colTexts = new TextMeshProUGUI[4];
            for (int c = 0; c < 4; c++)
            {
                float cx = -445f + (c * 296f);
                GameObject colBg = CreateUIPanel(guidePanel.transform, $"GuideCol_{c}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(cx, -5f), new Vector2(282f, 510f));
                colBg.GetComponent<Image>().color = new Color(0.13f, 0.14f, 0.19f, 0.92f);
                GameObject colTxtObj = CreateUIText(colBg.transform, "ColumnText", "", 15, FontStyles.Normal, Color.white, Vector2.zero, new Vector2(260f, 485f));
                var tmp = colTxtObj.GetComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.TopLeft;
                colTexts[c] = tmp;
            }
            guidePanel.SetActive(false);

            EmoteGuidePanelUI guideCtrl = emoteRoot.AddComponent<EmoteGuidePanelUI>();
            SetSerializedField(guideCtrl, "panelRoot", guidePanel);
            SetSerializedField(guideCtrl, "closeButton", guideCloseBtn);
            SetSerializedField(guideCtrl, "toggleHudButton", guideHudBtn);
            SetSerializedField(guideCtrl, "characterColumns", colTexts);
            SetSerializedField(guideCtrl, "characterEmoteDatas", emoteDatas);

            return wheelCtrl;
        }

        private static FingerMonster.Gameplay.CardSelectionController CreateCardSelectionHierarchy(Transform canvasRoot)
        {
            GameObject cardPanelRoot = CreateUIPanel(canvasRoot, "CardSelectionPanel", new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero, raycastTarget: true);
            cardPanelRoot.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.07f, 0.92f);

            GameObject content = CreateUIPanel(cardPanelRoot.transform, "CardContent", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 560));
            GameObject titleObj = CreateUIText(content.transform, "CardTitle", "3 KART SEÇİMİ - ŞANSINI DENE!", 36, FontStyles.Bold, Color.yellow, new Vector2(0, 215), new Vector2(850, 50));
            GameObject promptObj = CreateUIText(content.transform, "CardPrompt", "1 GÜVENLİ (PAS GEÇ)   |   2 CANAVAR (DAKTİLO CEZASI)", 20, FontStyles.Normal, Color.white, new Vector2(0, 165), new Vector2(850, 36));

            List<FingerMonster.Gameplay.CardElement> cardElements = new List<FingerMonster.Gameplay.CardElement>();
            float startX = -280f;
            float stepX = 280f;

            for (int i = 0; i < 3; i++)
            {
                float x = startX + (i * stepX);
                GameObject cardBtnObj = CreateUIPanel(content.transform, $"CardBtn_{i}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -25), new Vector2(235, 330), raycastTarget: true);
                Button btn = cardBtnObj.AddComponent<Button>();
                Image bgImage = cardBtnObj.GetComponent<Image>();
                bgImage.color = new Color(0.22f, 0.22f, 0.28f);

                // Kart Arkası (Kapalı hal)
                GameObject backObj = CreateUIPanel(cardBtnObj.transform, "CardBack", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, raycastTarget: false);
                Image backImg = backObj.GetComponent<Image>();
                backImg.color = new Color(0.42f, 0.12f, 0.14f);
                CreateUIText(backObj.transform, "BackQuestion", "?", 78, FontStyles.Bold, new Color(1f, 0.85f, 0.3f), Vector2.zero, new Vector2(100, 100));

                // Kart Önü (Açık hal)
                GameObject frontObj = CreateUIPanel(cardBtnObj.transform, "CardFront", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, raycastTarget: false);
                Image frontBg = frontObj.GetComponent<Image>();
                frontBg.color = new Color(0.12f, 0.13f, 0.18f);
                GameObject frontTextObj = CreateUIText(frontObj.transform, "FrontText", "SONUÇ", 22, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(210, 85));
                frontObj.SetActive(false);

                FingerMonster.Gameplay.CardElement elem = new FingerMonster.Gameplay.CardElement
                {
                    button = btn,
                    cardBackImage = backImg,
                    cardFrontRoot = frontObj,
                    frontText = frontTextObj.GetComponent<TextMeshProUGUI>()
                };
                cardElements.Add(elem);
            }

            FingerMonster.Gameplay.CardSelectionController cardCtrl = cardPanelRoot.AddComponent<FingerMonster.Gameplay.CardSelectionController>();
            SetSerializedField(cardCtrl, "panelRoot", cardPanelRoot);
            SetSerializedField(cardCtrl, "titleText", titleObj.GetComponent<TextMeshProUGUI>());
            SetSerializedField(cardCtrl, "promptText", promptObj.GetComponent<TextMeshProUGUI>());
            SetSerializedField(cardCtrl, "cards", cardElements);

            cardPanelRoot.SetActive(false);
            return cardCtrl;
        }

        private static Material CreateColoredMaterial(string name, Color color, string texturePath = null, float smoothness = 0.2f, float metallic = 0.0f)
        {
            string matPath = $"Assets/Materials/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader);
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", smoothness);
                mat.SetFloat("_Metallic", metallic);
                if (!string.IsNullOrEmpty(texturePath))
                {
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (tex != null)
                    {
                        mat.mainTexture = tex;
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    }
                }
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", smoothness);
                mat.SetFloat("_Metallic", metallic);
                if (!string.IsNullOrEmpty(texturePath))
                {
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (tex != null)
                    {
                        mat.mainTexture = tex;
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    }
                }
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        public static void EnsureDecorativePropsInActiveScene()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid()) return;

            MainMenuController mainMenuCtrl = Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if (activeScene.name == "MainMenu" || mainMenuCtrl != null)
            {
                SyncExternalButcherAnimations();
                if (mainMenuCtrl != null)
                {
                    mainMenuCtrl.EnhanceMainMenuLayoutAndShowcase();
                    mainMenuCtrl.RefreshCharacterCards();
                }

                ButcherPlaceholder menuButcher = Object.FindFirstObjectByType<ButcherPlaceholder>(FindObjectsInactive.Include);
                if (menuButcher != null)
                {
                    WireButcherAnimationsToComponent(menuButcher);
                }

                if (!Application.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(activeScene);
                }
                return;
            }

            if (activeScene.name != "Game") return;

            GameObject envRoot = GameObject.Find("Environment");
            if (envRoot != null)
            {
                Transform pillarsRoot = envRoot.transform.Find("Pillars");
                if (pillarsRoot == null)
                {
                    GameObject pObj = new GameObject("Pillars");
                    pObj.transform.SetParent(envRoot.transform);
                    pillarsRoot = pObj.transform;
                }

                Transform forgeRoot = envRoot.transform.Find("BlacksmithForge_Props");
                if (forgeRoot == null)
                {
                    GameObject fObj = new GameObject("BlacksmithForge_Props");
                    fObj.transform.SetParent(envRoot.transform);
                    forgeRoot = fObj.transform;
                }

                // Eğer yeni dekoratif yan tezgah henüz sahnede yoksa otomatik olarak ekle
                if (forgeRoot.Find("Forge_SideBench_West") == null)
                {
                    Ensure3DForgeMaterialsURP();

                    // Eski tekil Batı şamdanını temizle (yerine simetrik çift şamdan geliyor)
                    for (int i = forgeRoot.childCount - 1; i >= 0; i--)
                    {
                        Transform child = forgeRoot.GetChild(i);
                        if (child != null && child.name.Contains("candle_wall02") && Mathf.Abs(child.localPosition.z) < 0.2f)
                        {
                            Object.DestroyImmediate(child.gameObject);
                        }
                    }

                    PopulateExtraDecorativeProps(pillarsRoot, forgeRoot);
                    if (!Application.isPlaying)
                    {
                        EditorSceneManager.MarkSceneDirty(activeScene);
                    }
                    Debug.Log("[SceneSetupTool] Aktif Game sahnesine dekoratif 3DForge oda eşyaları otomatik olarak eklendi.");
                }
            }

            SyncExternalButcherAnimations();

            // Kasabın Layer 0'da, kasap.fbx modeliyle ve görünür olduğundan emin ol
            ButcherPlaceholder butcherComp = Object.FindFirstObjectByType<ButcherPlaceholder>(FindObjectsInactive.Include);
            if (butcherComp != null)
            {
                GameObject butcherObj = butcherComp.gameObject;
                if (butcherObj.transform.position.sqrMagnitude < 0.1f)
                {
                    butcherObj.transform.position = new Vector3(1.8f, 0f, -1.8f);
                }

                GameObject kasapFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/kasap.fbx");
                if (kasapFbx != null)
                {
                    butcherComp.SetCustomButcherModelPrefab(kasapFbx);
                    Transform bCapsule = butcherObj.transform.Find("ButcherCapsule");
                    if (bCapsule != null && bCapsule.gameObject.activeSelf)
                    {
                        bCapsule.gameObject.SetActive(false);
                    }
                }
                else
                {
                    Transform bCapsule = butcherObj.transform.Find("ButcherCapsule");
                    if (bCapsule != null && !bCapsule.gameObject.activeSelf)
                    {
                        bCapsule.gameObject.SetActive(true);
                    }

                    Transform mal = butcherObj.transform.Find("ButcherCapsule/WeaponArmPivot/Weapon_GiantMallet");
                    if (mal != null && !mal.gameObject.activeSelf)
                    {
                        mal.gameObject.SetActive(true);
                    }
                }

                WireButcherAnimationsToComponent(butcherComp);
                SetLayerRecursivelyEditor(butcherObj, 0);
            }
        }

        private static void PopulateExtraDecorativeProps(Transform pillarsRoot, Transform forgeRoot)
        {
            // 1. Duvar Orta Bölme Sütunları (8 adet, fi_vil_pillar8_02.prefab)
            if (pillarsRoot != null && pillarsRoot.Find("Pillar_Wall_N1") == null)
            {
                // Kuzey & Güney
                InstantiateForgePrefab(PillarPath, pillarsRoot, new Vector3(-2.4f, 0f, 5.85f), Quaternion.identity, Vector3.one, "Pillar_Wall_N1");
                InstantiateForgePrefab(PillarPath, pillarsRoot, new Vector3(2.4f, 0f, 5.85f), Quaternion.identity, Vector3.one, "Pillar_Wall_N2");
                InstantiateForgePrefab(PillarPath, pillarsRoot, new Vector3(-2.4f, 0f, -5.85f), Quaternion.identity, Vector3.one, "Pillar_Wall_S1");
                InstantiateForgePrefab(PillarPath, pillarsRoot, new Vector3(2.4f, 0f, -5.85f), Quaternion.identity, Vector3.one, "Pillar_Wall_S2");
                // Doğu & Batı
                InstantiateForgePrefab(PillarPath, pillarsRoot, new Vector3(5.85f, 0f, -2.4f), Quaternion.identity, Vector3.one, "Pillar_Wall_E1");
                InstantiateForgePrefab(PillarPath, pillarsRoot, new Vector3(5.85f, 0f, 2.4f), Quaternion.identity, Vector3.one, "Pillar_Wall_E2");
                InstantiateForgePrefab(PillarPath, pillarsRoot, new Vector3(-5.85f, 0f, -2.4f), Quaternion.identity, Vector3.one, "Pillar_Wall_W1");
                InstantiateForgePrefab(PillarPath, pillarsRoot, new Vector3(-5.85f, 0f, 2.4f), Quaternion.identity, Vector3.one, "Pillar_Wall_W2");
            }

            if (forgeRoot == null) return;

            // 2. Simetrik Duvar Şamdanları (fi_vil_light_candle_wall02_lit_b.prefab)
            SpawnWallCandle(forgeRoot, new Vector3(-5.92f, 1.85f, -1.8f), Quaternion.Euler(0f, 90f, 0f), "Forge_Candle_West_1");
            SpawnWallCandle(forgeRoot, new Vector3(-5.92f, 1.85f, 1.8f), Quaternion.Euler(0f, 90f, 0f), "Forge_Candle_West_2");
            SpawnWallCandle(forgeRoot, new Vector3(5.92f, 1.85f, -1.8f), Quaternion.Euler(0f, -90f, 0f), "Forge_Candle_East_1");
            SpawnWallCandle(forgeRoot, new Vector3(5.92f, 1.85f, 1.8f), Quaternion.Euler(0f, -90f, 0f), "Forge_Candle_East_2");
            SpawnWallCandle(forgeRoot, new Vector3(-1.8f, 1.85f, -5.92f), Quaternion.Euler(0f, 0f, 0f), "Forge_Candle_South_1");
            SpawnWallCandle(forgeRoot, new Vector3(1.8f, 1.85f, -5.92f), Quaternion.Euler(0f, 0f, 0f), "Forge_Candle_South_2");
            SpawnWallCandle(forgeRoot, new Vector3(-2.0f, 1.85f, 5.92f), Quaternion.Euler(0f, 180f, 0f), "Forge_Candle_North_1");

            // 3. Düzenli Yan Tezgahlar ve Çalışma İstasyonları
            // Batı Çalışma Köşesi
            InstantiateForgePrefab(SmallBenchPath, forgeRoot, new Vector3(-5.1f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), Vector3.one * 1.15f, "Forge_SideBench_West");
            InstantiateForgePrefab(HammerPath, forgeRoot, new Vector3(-5.0f, 0.86f, 0.2f), Quaternion.Euler(0f, 35f, 0f), Vector3.one, "Forge_Hammer_West");
            InstantiateForgePrefab(StoolPath, forgeRoot, new Vector3(-4.4f, 0f, -0.9f), Quaternion.Euler(0f, 15f, 0f), Vector3.one, "Forge_Stool_West");

            // Güney-Doğu Silah & Örs Köşesi
            InstantiateForgePrefab(SmallBenchPath, forgeRoot, new Vector3(4.8f, 0f, -4.5f), Quaternion.Euler(0f, -45f, 0f), Vector3.one * 1.15f, "Forge_SideBench_SE");
            InstantiateForgePrefab(AnvilPath, forgeRoot, new Vector3(3.9f, 0f, -4.8f), Quaternion.Euler(0f, 20f, 0f), Vector3.one, "Forge_Anvil_SE");
            InstantiateForgePrefab(StoolPath, forgeRoot, new Vector3(5.1f, 0f, -3.4f), Quaternion.Euler(0f, -110f, 0f), Vector3.one, "Forge_Stool_SE");

            // Kuzey-Batı Erzak & Fıçı Köşesi
            InstantiateForgePrefab(BarrelClusterPath, forgeRoot, new Vector3(-4.8f, 0f, 4.6f), Quaternion.Euler(0f, 135f, 0f), Vector3.one, "Forge_Barrels_NW");
            InstantiateForgePrefab(SmallBenchPath, forgeRoot, new Vector3(-4.9f, 0f, 3.2f), Quaternion.Euler(0f, 90f, 0f), Vector3.one * 1.1f, "Forge_SideBench_NW");

            // 4. Simetrik Silah Rafları (Güney, Batı, Doğu)
            InstantiateForgePrefab(WeaponRackPath, forgeRoot, new Vector3(0f, 0f, -5.75f), Quaternion.Euler(0f, 0f, 0f), Vector3.one, "Forge_WeaponRack_South");
            InstantiateForgePrefab(WeaponRackPath, forgeRoot, new Vector3(-5.75f, 0f, -3.2f), Quaternion.Euler(0f, 90f, 0f), Vector3.one, "Forge_WeaponRack_West");
            InstantiateForgePrefab(WeaponRackPath, forgeRoot, new Vector3(5.75f, 0f, -2.5f), Quaternion.Euler(0f, -90f, 0f), Vector3.one, "Forge_WeaponRack_East");

            // 5. Tavan Dekoratif Zincirleri (4 köşe üst bölgesi)
            InstantiateForgePrefab(ChainPath, forgeRoot, new Vector3(3.8f, 2.8f, 3.8f), Quaternion.identity, Vector3.one, "Forge_CeilingChain_NE");
            InstantiateForgePrefab(ChainPath, forgeRoot, new Vector3(-3.8f, 2.8f, 3.8f), Quaternion.identity, Vector3.one, "Forge_CeilingChain_NW");
            InstantiateForgePrefab(ChainPath, forgeRoot, new Vector3(3.8f, 2.8f, -3.8f), Quaternion.identity, Vector3.one, "Forge_CeilingChain_SE");
            InstantiateForgePrefab(ChainPath, forgeRoot, new Vector3(-3.8f, 2.8f, -3.8f), Quaternion.identity, Vector3.one, "Forge_CeilingChain_SW");
        }

        private static void SpawnWallCandle(Transform parent, Vector3 localPos, Quaternion localRot, string customName)
        {
            GameObject candle = InstantiateForgePrefab(WallCandlePath, parent, localPos, localRot, Vector3.one, customName);
            if (candle != null)
            {
                var lights = candle.GetComponentsInChildren<Light>(true);
                foreach (var l in lights)
                {
                    l.intensity = Mathf.Min(l.intensity, 0.55f);
                    l.range = Mathf.Min(l.range, 3.2f);
                    l.color = new Color(1.0f, 0.78f, 0.45f);
                }
            }
        }

        private static GameObject InstantiateForgePrefab(string prefabPath, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 localScale, string customName = null)
        {
            return SpawnForgeProp(prefabPath, parent, localPos, localRot, localScale, customName);
        }

        private static GameObject SpawnForgeProp(string prefabPath, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 localScale, string customName = null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return null;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null) return null;

            if (!string.IsNullOrEmpty(customName))
            {
                instance.name = customName;
            }

            instance.transform.SetParent(parent);
            instance.transform.localPosition = localPos;
            instance.transform.localRotation = localRot;
            instance.transform.localScale = localScale;

            // Masadaki karakterlerin oturma, el koparma ve kamera açısını engellememesi için 
            // prop üzerindeki gereksiz ağır mesh collider'ları kaldır
            var cols = instance.GetComponentsInChildren<Collider>();
            foreach (var c in cols)
            {
                Object.DestroyImmediate(c);
            }

            // Kullanıcı talebi: Oyunda kesinlikle hiçbir partikül olmasın (flame, smoke, vb.)
            var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = particles.Length - 1; i >= 0; i--)
            {
                if (particles[i] != null) Object.DestroyImmediate(particles[i].gameObject);
            }
            var pRenderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = pRenderers.Length - 1; i >= 0; i--)
            {
                if (pRenderers[i] != null) Object.DestroyImmediate(pRenderers[i].gameObject);
            }

            return instance;
        }

        private static Color GetPlayerColor(int index)
        {
            switch (index)
            {
                case 0: return new Color(0.9f, 0.25f, 0.25f);
                case 1: return new Color(0.25f, 0.55f, 0.95f);
                case 2: return new Color(0.25f, 0.85f, 0.35f);
                case 3: return new Color(0.95f, 0.75f, 0.2f);
                default: return Color.white;
            }
        }

        private static void SetLayerRecursivelyEditor(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursivelyEditor(child.gameObject, layer);
            }
        }

        private static void SetSerializedField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
        #endregion
    }
}

