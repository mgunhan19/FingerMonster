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

namespace FingerMonster.Editor
{
    /// <summary>
    /// FingerMonster sahnelerini (MainMenu ve Game), hiyerarşiyi, ışıklandırmayı,
    /// daktiloyu, 20 tuşu, kameraları ve tüm UI bileşenlerini otomatik olarak inşa eden editör aracı.
    /// </summary>
    public static class SceneSetupTool
    {
        [MenuItem("FingerMonster/Build All Scenes")]
        public static void BuildAllScenes()
        {
            EnsureTMPResources();
            BuildGameScene();
            BuildMainMenuScene();
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[SceneSetupTool] Tüm sahneler (MainMenu ve Game) başarıyla inşa edildi ve Build Settings'e eklendi!</color>");
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

        #region GAME SCENE BUILDER
        public static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Materials
            Material floorMat = CreateColoredMaterial("Mat_Floor", new Color(0.12f, 0.12f, 0.14f));
            Material tableMat = CreateColoredMaterial("Mat_Table", new Color(0.24f, 0.15f, 0.08f));
            Material typewriterMat = CreateColoredMaterial("Mat_Typewriter", new Color(0.18f, 0.18f, 0.2f));
            Material keyNormalMat = CreateColoredMaterial("Mat_KeyNormal", new Color(0.9f, 0.88f, 0.82f));
            Material paperMat = CreateColoredMaterial("Mat_Paper", new Color(0.96f, 0.95f, 0.92f));
            Material chairMat = CreateColoredMaterial("Mat_Chair", new Color(0.15f, 0.15f, 0.15f));
            Material butcherMat = CreateColoredMaterial("Mat_Butcher", new Color(0.45f, 0.1f, 0.1f));

            // 2. Camera Setup (Kullanıcının belirlediği X:0, Y:2.28, Z:-2.31 ve rotasyon X:27.956, FOV:60)
            GameObject cameraObj = new GameObject("Main Camera");
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.05f);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            cameraObj.AddComponent<AudioListener>();
            cameraObj.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            cameraObj.tag = "MainCamera";
            cameraObj.transform.position = new Vector3(0f, 2.28f, -2.31f);
            cameraObj.transform.rotation = Quaternion.Euler(27.956f, 0f, 0f);
            PlayerCameraController camController = cameraObj.AddComponent<PlayerCameraController>();

            // 3. Lighting Setup
            GameObject lightingRoot = new GameObject("Lighting");

            GameObject ambientObj = new GameObject("Ambient Room Light");
            ambientObj.transform.SetParent(lightingRoot.transform);
            Light ambientLight = ambientObj.AddComponent<Light>();
            ambientLight.type = LightType.Directional;
            ambientLight.color = new Color(0.5f, 0.55f, 0.65f);
            ambientLight.intensity = 0.35f;
            ambientLight.transform.rotation = Quaternion.Euler(50, -30, 0);

            GameObject spotObj = new GameObject("Table Spot Light");
            spotObj.transform.SetParent(lightingRoot.transform);
            spotObj.transform.position = new Vector3(0, 3.2f, 0);
            spotObj.transform.rotation = Quaternion.Euler(90, 0, 0);
            Light spotLight = spotObj.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.range = 8f;
            spotLight.spotAngle = 55f;
            spotLight.intensity = 2.5f;
            spotLight.color = new Color(1.0f, 0.95f, 0.85f);

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

            RoomLightingController lightingController = lightingRoot.AddComponent<RoomLightingController>();
            lightingController.SetupLights(spotLight, ambientLight, redLight);

            // 4. Environment (Floor & Walls)
            GameObject envRoot = new GameObject("Environment");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(envRoot.transform);
            floor.transform.position = new Vector3(0, -0.25f, 0);
            floor.transform.localScale = new Vector3(14f, 0.5f, 14f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            // 5. Central Table
            GameObject tableRoot = new GameObject("Table");
            tableRoot.transform.position = Vector3.zero;

            GameObject tableTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tableTop.name = "TableTop";
            tableTop.transform.SetParent(tableRoot.transform);
            tableTop.transform.position = new Vector3(0, 0.75f, 0);
            tableTop.transform.localScale = new Vector3(3.2f, 0.1f, 2.2f);
            tableTop.GetComponent<Renderer>().sharedMaterial = tableMat;
            if (tableTop.GetComponent<Collider>() != null) Object.DestroyImmediate(tableTop.GetComponent<Collider>());

            // Masa Ayakları
            Vector3[] legOffsets = new Vector3[] {
                new Vector3(-1.4f, 0.35f, -0.9f),
                new Vector3(1.4f, 0.35f, -0.9f),
                new Vector3(-1.4f, 0.35f, 0.9f),
                new Vector3(1.4f, 0.35f, 0.9f)
            };
            foreach (var legPos in legOffsets)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leg.name = "Leg";
                leg.transform.SetParent(tableRoot.transform);
                leg.transform.position = legPos;
                leg.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
                leg.GetComponent<Renderer>().sharedMaterial = tableMat;
                if (leg.GetComponent<Collider>() != null) Object.DestroyImmediate(leg.GetComponent<Collider>());
            }

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

            // 7. 4 Player Seats (Masa etrafında simetrik 2.1m aralık)
            List<PlayerSeat> seats = new List<PlayerSeat>();
            Vector3[] seatPositions = new Vector3[] {
                new Vector3(0, 0, -2.1f),      // P1 (Güney)
                new Vector3(0, 0, 2.1f),       // P2 (Kuzey)
                new Vector3(-2.1f, 0, 0),      // P3 (Batı)
                new Vector3(2.1f, 0, 0)        // P4 (Doğu)
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

                // Koltuk gövdesi
                GameObject chairModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chairModel.name = "ChairVisual";
                chairModel.transform.SetParent(seatObj.transform);
                chairModel.transform.localPosition = new Vector3(0, 0.45f, 0);
                chairModel.transform.localScale = new Vector3(0.6f, 0.1f, 0.6f);
                chairModel.GetComponent<Renderer>().sharedMaterial = chairMat;
                if (chairModel.GetComponent<Collider>() != null) Object.DestroyImmediate(chairModel.GetComponent<Collider>());

                GameObject backrest = GameObject.CreatePrimitive(PrimitiveType.Cube);
                backrest.name = "Backrest";
                backrest.transform.SetParent(seatObj.transform);
                backrest.transform.localPosition = new Vector3(0, 0.85f, -0.25f);
                backrest.transform.localScale = new Vector3(0.6f, 0.7f, 0.1f);
                backrest.GetComponent<Renderer>().sharedMaterial = chairMat;
                if (backrest.GetComponent<Collider>() != null) Object.DestroyImmediate(backrest.GetComponent<Collider>());

                // Kamera Bağlantı Noktası (Kullanıcının belirlediği X:0, Y:2.28, Z:-2.31 ve rotasyon X:27.956°)
                GameObject camAnchor = new GameObject("CameraMount");
                camAnchor.transform.SetParent(seatObj.transform);
                camAnchor.transform.localPosition = new Vector3(0f, 2.28f, -0.21f);
                camAnchor.transform.localRotation = Quaternion.Euler(27.956f, 0f, 0f);

                // Kasap Belirme Noktası (Koltuğun arkası)
                GameObject butcherAnchor = new GameObject("ButcherSpawnPoint");
                butcherAnchor.transform.SetParent(seatObj.transform);
                butcherAnchor.transform.localPosition = new Vector3(0, 0, -0.7f);
                butcherAnchor.transform.localRotation = Quaternion.identity;

                // 4 Farklı Çizgi Film Karakteri (Kabadayı, Korkak, Sinsi, Şovmen)
                CharacterArchetype archetype = (CharacterArchetype)i;
                var charDef = CharacterDefinition.GetDefinition(archetype);

                GameObject avatarRoot = new GameObject($"PlayerAvatar_{archetype}");
                avatarRoot.transform.SetParent(seatObj.transform);
                avatarRoot.transform.localPosition = new Vector3(0, 0.45f, 0);
                avatarRoot.transform.localRotation = Quaternion.identity;

                // Gövde (Torso)
                GameObject torsoObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                torsoObj.name = "Torso";
                torsoObj.transform.SetParent(avatarRoot.transform);
                torsoObj.transform.localPosition = new Vector3(0, 0.45f, 0);

                // Karakter arketipine göre gövde oranları
                if (archetype == CharacterArchetype.Kabadayi) torsoObj.transform.localScale = new Vector3(0.58f, 0.48f, 0.42f);
                else if (archetype == CharacterArchetype.Korkak) torsoObj.transform.localScale = new Vector3(0.38f, 0.42f, 0.32f);
                else if (archetype == CharacterArchetype.Sinsi) torsoObj.transform.localScale = new Vector3(0.42f, 0.52f, 0.35f);
                else torsoObj.transform.localScale = new Vector3(0.46f, 0.46f, 0.36f);

                torsoObj.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial($"Mat_Player_{i}", charDef.PrimaryColor);
                if (torsoObj.GetComponent<Collider>() != null) Object.DestroyImmediate(torsoObj.GetComponent<Collider>());

                // Baş (Head)
                GameObject headObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                headObj.name = "Head";
                headObj.transform.SetParent(torsoObj.transform);
                headObj.transform.localPosition = new Vector3(0, 0.95f, 0);
                headObj.transform.localScale = new Vector3(0.72f, 0.72f, 0.72f);
                headObj.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial($"Mat_PlayerHead_{i}", charDef.PrimaryColor * 1.15f);
                if (headObj.GetComponent<Collider>() != null) Object.DestroyImmediate(headObj.GetComponent<Collider>());

                // Çizgi Film Gözleri (Sol & Sağ)
                GameObject leftEye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leftEye.name = "LeftEye";
                leftEye.transform.SetParent(headObj.transform);
                leftEye.transform.localPosition = new Vector3(-0.16f, 0.12f, 0.38f);
                leftEye.transform.localScale = (archetype == CharacterArchetype.Korkak) ? new Vector3(0.24f, 0.24f, 0.24f) : new Vector3(0.16f, 0.16f, 0.16f);
                leftEye.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial("Mat_EyeWhite", Color.white);
                if (leftEye.GetComponent<Collider>() != null) Object.DestroyImmediate(leftEye.GetComponent<Collider>());

                GameObject rightEye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rightEye.name = "RightEye";
                rightEye.transform.SetParent(headObj.transform);
                rightEye.transform.localPosition = new Vector3(0.16f, 0.12f, 0.38f);
                rightEye.transform.localScale = (archetype == CharacterArchetype.Korkak) ? new Vector3(0.24f, 0.24f, 0.24f) : new Vector3(0.16f, 0.16f, 0.16f);
                rightEye.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial("Mat_EyeWhite", Color.white);
                if (rightEye.GetComponent<Collider>() != null) Object.DestroyImmediate(rightEye.GetComponent<Collider>());

                // Karakter Aksesuarı (Şovmen Silindir Şapka vb.)
                GameObject accessoryObj = new GameObject("Accessory");
                accessoryObj.transform.SetParent(headObj.transform, false);

                if (archetype == CharacterArchetype.Sovmen)
                {
                    GameObject hatBrim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    hatBrim.name = "HatBrim";
                    hatBrim.transform.SetParent(accessoryObj.transform);
                    hatBrim.transform.localPosition = new Vector3(0, 0.45f, 0);
                    hatBrim.transform.localScale = new Vector3(0.55f, 0.03f, 0.55f);
                    hatBrim.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial("Mat_TopHat", new Color(0.12f, 0.12f, 0.15f));
                    if (hatBrim.GetComponent<Collider>() != null) Object.DestroyImmediate(hatBrim.GetComponent<Collider>());

                    GameObject hatCrown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    hatCrown.name = "HatCrown";
                    hatCrown.transform.SetParent(accessoryObj.transform);
                    hatCrown.transform.localPosition = new Vector3(0, 0.7f, 0);
                    hatCrown.transform.localScale = new Vector3(0.38f, 0.24f, 0.38f);
                    hatCrown.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial("Mat_TopHat", new Color(0.12f, 0.12f, 0.15f));
                    if (hatCrown.GetComponent<Collider>() != null) Object.DestroyImmediate(hatCrown.GetComponent<Collider>());
                }

                // Kol pivotları
                GameObject lArmPivot = new GameObject("LeftArmPivot");
                lArmPivot.transform.SetParent(torsoObj.transform);
                lArmPivot.transform.localPosition = new Vector3(-0.35f, 0.4f, 0);

                GameObject rArmPivot = new GameObject("RightArmPivot");
                rArmPivot.transform.SetParent(torsoObj.transform);
                rArmPivot.transform.localPosition = new Vector3(0.35f, 0.4f, 0);

                // Sol ve Sağ Çizgi Film Kolları (Gövdeden masaya doğru uzanan kollar)
                Material torsoMat = torsoObj.GetComponent<Renderer>().sharedMaterial;
                Material handMat = CreateColoredMaterial($"Mat_PlayerHand_{i}", charDef.PrimaryColor);

                GameObject lArmMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                lArmMesh.name = "LeftArmMesh";
                lArmMesh.transform.SetParent(lArmPivot.transform, false);
                lArmMesh.transform.localPosition = new Vector3(-0.06f, -0.15f, 0.45f);
                lArmMesh.transform.localRotation = Quaternion.Euler(38f, 18f, -12f);
                lArmMesh.transform.localScale = new Vector3(0.18f, 0.52f, 0.18f);
                lArmMesh.GetComponent<Renderer>().sharedMaterial = torsoMat;
                if (lArmMesh.GetComponent<Collider>() != null) Object.DestroyImmediate(lArmMesh.GetComponent<Collider>());

                GameObject rArmMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                rArmMesh.name = "RightArmMesh";
                rArmMesh.transform.SetParent(rArmPivot.transform, false);
                rArmMesh.transform.localPosition = new Vector3(0.06f, -0.15f, 0.45f);
                rArmMesh.transform.localRotation = Quaternion.Euler(38f, -18f, 12f);
                rArmMesh.transform.localScale = new Vector3(0.18f, 0.52f, 0.18f);
                rArmMesh.GetComponent<Renderer>().sharedMaterial = torsoMat;
                if (rArmMesh.GetComponent<Collider>() != null) Object.DestroyImmediate(rArmMesh.GetComponent<Collider>());

                // Sol Bilek Sargısı / Bandajı (El kopunca görünür olur)
                GameObject leftBandage = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leftBandage.name = "LeftWristBandage";
                leftBandage.transform.SetParent(avatarRoot.transform, false);
                leftBandage.transform.localPosition = new Vector3(-0.42f, 0.44f, 1.02f);
                leftBandage.transform.localScale = new Vector3(0.20f, 0.14f, 0.10f);
                leftBandage.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial("Mat_Bandage", new Color(0.95f, 0.9f, 0.75f));
                if (leftBandage.GetComponent<Collider>() != null) Object.DestroyImmediate(leftBandage.GetComponent<Collider>());
                leftBandage.SetActive(false);

                // Sağ Bilek Sargısı / Bandajı (El kopunca görünür olur)
                GameObject rightBandage = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rightBandage.name = "RightWristBandage";
                rightBandage.transform.SetParent(avatarRoot.transform, false);
                rightBandage.transform.localPosition = new Vector3(0.42f, 0.44f, 1.02f);
                rightBandage.transform.localScale = new Vector3(0.20f, 0.14f, 0.10f);
                rightBandage.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial("Mat_Bandage", new Color(0.95f, 0.9f, 0.75f));
                if (rightBandage.GetComponent<Collider>() != null) Object.DestroyImmediate(rightBandage.GetComponent<Collider>());
                rightBandage.SetActive(false);

                // Masaya uzanan Sol El (Avuç İçi + 3 Parmak + Başparmak)
                GameObject leftHand = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leftHand.name = "LeftHand";
                leftHand.transform.SetParent(avatarRoot.transform, false);
                leftHand.transform.localPosition = new Vector3(-0.42f, 0.44f, 1.18f);
                leftHand.transform.localRotation = Quaternion.Euler(0, 15f, 0);
                leftHand.transform.localScale = new Vector3(0.20f, 0.08f, 0.22f);
                leftHand.GetComponent<Renderer>().sharedMaterial = handMat;
                if (leftHand.GetComponent<Collider>() != null) Object.DestroyImmediate(leftHand.GetComponent<Collider>());
                var lBox = leftHand.AddComponent<BoxCollider>();
                lBox.size = new Vector3(0.24f, 0.12f, 0.32f);
                lBox.enabled = false;
                leftHand.AddComponent<SeveredHandPhysics>();

                // Sol El Parmakları
                for (int f = 0; f < 3; f++)
                {
                    GameObject finger = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    finger.name = $"LeftFinger_{f}";
                    finger.transform.SetParent(leftHand.transform, false);
                    finger.transform.localPosition = new Vector3(-0.06f + (f * 0.06f), 0f, 0.14f);
                    finger.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                    finger.transform.localScale = new Vector3(0.045f, 0.07f, 0.045f);
                    finger.GetComponent<Renderer>().sharedMaterial = handMat;
                    if (finger.GetComponent<Collider>() != null) Object.DestroyImmediate(finger.GetComponent<Collider>());
                }
                GameObject lThumb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                lThumb.name = "LeftThumb";
                lThumb.transform.SetParent(leftHand.transform, false);
                lThumb.transform.localPosition = new Vector3(0.12f, 0f, 0.02f);
                lThumb.transform.localRotation = Quaternion.Euler(0, 45f, 90f);
                lThumb.transform.localScale = new Vector3(0.05f, 0.06f, 0.05f);
                lThumb.GetComponent<Renderer>().sharedMaterial = handMat;
                if (lThumb.GetComponent<Collider>() != null) Object.DestroyImmediate(lThumb.GetComponent<Collider>());

                // Masaya uzanan Sağ El (Avuç İçi + 3 Parmak + Başparmak)
                GameObject rightHand = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rightHand.name = "RightHand";
                rightHand.transform.SetParent(avatarRoot.transform, false);
                rightHand.transform.localPosition = new Vector3(0.42f, 0.44f, 1.18f);
                rightHand.transform.localRotation = Quaternion.Euler(0, -15f, 0);
                rightHand.transform.localScale = new Vector3(0.20f, 0.08f, 0.22f);
                rightHand.GetComponent<Renderer>().sharedMaterial = handMat;
                if (rightHand.GetComponent<Collider>() != null) Object.DestroyImmediate(rightHand.GetComponent<Collider>());
                var rBox = rightHand.AddComponent<BoxCollider>();
                rBox.size = new Vector3(0.24f, 0.12f, 0.32f);
                rBox.enabled = false;
                rightHand.AddComponent<SeveredHandPhysics>();

                // Sağ El Parmakları
                for (int f = 0; f < 3; f++)
                {
                    GameObject finger = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    finger.name = $"RightFinger_{f}";
                    finger.transform.SetParent(rightHand.transform, false);
                    finger.transform.localPosition = new Vector3(-0.06f + (f * 0.06f), 0f, 0.14f);
                    finger.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                    finger.transform.localScale = new Vector3(0.045f, 0.07f, 0.045f);
                    finger.GetComponent<Renderer>().sharedMaterial = handMat;
                    if (finger.GetComponent<Collider>() != null) Object.DestroyImmediate(finger.GetComponent<Collider>());
                }
                GameObject rThumb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                rThumb.name = "RightThumb";
                rThumb.transform.SetParent(rightHand.transform, false);
                rThumb.transform.localPosition = new Vector3(-0.12f, 0f, 0.02f);
                rThumb.transform.localRotation = Quaternion.Euler(0, -45f, 90f);
                rThumb.transform.localScale = new Vector3(0.05f, 0.06f, 0.05f);
                rThumb.GetComponent<Renderer>().sharedMaterial = handMat;
                if (rThumb.GetComponent<Collider>() != null) Object.DestroyImmediate(rThumb.GetComponent<Collider>());

                // Animatör Motoru (CharacterAnimatorDriver)
                CharacterAnimatorDriver animDriver = avatarRoot.AddComponent<CharacterAnimatorDriver>();
                animDriver.Archetype = archetype;
                animDriver.SetupBones(avatarRoot.transform, torsoObj.transform, headObj.transform, lArmPivot.transform, rArmPivot.transform, leftEye.transform, rightEye.transform, accessoryObj.transform);
                animDriver.SetupHands(leftHand, rightHand, leftBandage, rightBandage);

                PlayerVisualAvatar avatarComp = avatarRoot.AddComponent<PlayerVisualAvatar>();
                avatarComp.Archetype = archetype;
                SetSerializedField(avatarComp, "headTransform", headObj.transform);
                SetSerializedField(avatarComp, "torsoTransform", torsoObj.transform);
                SetSerializedField(avatarComp, "leftHandObj", leftHand);
                SetSerializedField(avatarComp, "rightHandObj", rightHand);
                SetSerializedField(avatarComp, "leftWristBandage", leftBandage);
                SetSerializedField(avatarComp, "rightWristBandage", rightBandage);
                SetSerializedField(avatarComp, "animatorDriver", animDriver);

                List<Renderer> avatarRenderers = new List<Renderer> {
                    headObj.GetComponent<Renderer>(),
                    torsoObj.GetComponent<Renderer>(),
                    lArmMesh.GetComponent<Renderer>(),
                    rArmMesh.GetComponent<Renderer>(),
                    leftHand.GetComponent<Renderer>(),
                    rightHand.GetComponent<Renderer>()
                };
                avatarRenderers.AddRange(leftHand.GetComponentsInChildren<Renderer>());
                avatarRenderers.AddRange(rightHand.GetComponentsInChildren<Renderer>());
                SetSerializedField(avatarComp, "playerColoredRenderers", avatarRenderers.ToArray());

                PlayerSeat seatComp = seatObj.AddComponent<PlayerSeat>();
                SetSerializedField(seatComp, "cameraMountPoint", camAnchor.transform);
                seatComp.SetupSeat(i, charDef.PrimaryColor);
                seatComp.SetAvatar(avatarComp);
                seats.Add(seatComp);
            }

            // 8. Butcher Character & 4 Absurd Execution Weapons
            GameObject butcherObj = new GameObject("ButcherPlaceholder");
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
            butcherComp.Hide();

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
            gameManagerObj.AddComponent<AudioManager>();
            gameManagerObj.AddComponent<FingerMonster.AI.AIBrain>();
            gameManagerObj.AddComponent<FingerMonster.AI.AIPlayerController>();

            // GameManager Inspector Bağlantılarını Ata
            SetSerializedField(gm, "settings", defaultSettings);
            SetSerializedField(gm, "typewriter", typewriterMachine);
            SetSerializedField(gm, "turnController", tc);
            SetSerializedField(gm, "cameraController", camController);
            SetSerializedField(gm, "lightingController", lightingController);
            SetSerializedField(gm, "butcher", butcherComp);
            SetSerializedField(gm, "playerSeats", seats);
            SetSerializedField(gm, "hudController", hud);
            SetSerializedField(gm, "gameOverController", gameOverUI);
            SetSerializedField(gm, "pauseMenuController", pauseMenu);

            // NetworkManager & NetworkGameManager
            GameObject netMgrObj = new GameObject("NetworkManager");
            NetworkManager netMgr = netMgrObj.AddComponent<NetworkManager>();
            UnityTransport transport = netMgrObj.AddComponent<UnityTransport>();
            netMgr.NetworkConfig.NetworkTransport = transport;

            GameObject netGameMgrObj = new GameObject("NetworkGameManager");
            netGameMgrObj.AddComponent<NetworkObject>();
            NetworkGameManager netGm = netGameMgrObj.AddComponent<NetworkGameManager>();
            SetSerializedField(netGm, "typewriter", typewriterMachine);
            SetSerializedField(netGm, "hudController", hud);
            SetSerializedField(netGm, "gameOverController", gameOverUI);
            SetSerializedField(netGm, "lightingController", lightingController);
            SetSerializedField(netGm, "butcher", butcherComp);
            SetSerializedField(netGm, "playerSeats", seats);
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
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.08f);
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
            GameObject mainPanel = CreateUIPanel(canvasObj.transform, "MainPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 720));

            // Başlık
            GameObject titleObj = CreateUIText(mainPanel.transform, "TitleText", "FINGER MONSTER", 72, FontStyles.Bold, new Color(0.95f, 0.2f, 0.2f), new Vector2(0, 200), new Vector2(750, 100));
            CreateUIText(mainPanel.transform, "SubtitleText", "4 Kişilik Sıra Tabanlı Karanlık Komedi Parti Oyunu", 26, FontStyles.Normal, new Color(0.85f, 0.85f, 0.85f), new Vector2(0, 130), new Vector2(750, 50));

            // Butonlar
            Button playBtn = CreateUIButton(mainPanel.transform, "PlayButton", "TEK BİLGİSAYAR (SOLO / HOTSEAT)", new Vector2(0, 50), new Vector2(480, 65), new Color(0.2f, 0.65f, 0.3f));
            Button lanBtn = CreateUIButton(mainPanel.transform, "LanButton", "LAN ÇOK OYUNCULU (HOST / KATIL)", new Vector2(0, -30), new Vector2(480, 65), new Color(0.2f, 0.45f, 0.85f));
            Button settingsBtn = CreateUIButton(mainPanel.transform, "SettingsButton", "AYARLAR", new Vector2(0, -110), new Vector2(480, 65), new Color(0.35f, 0.35f, 0.45f));
            Button quitBtn = CreateUIButton(mainPanel.transform, "QuitButton", "ÇIKIŞ", new Vector2(0, -190), new Vector2(480, 65), new Color(0.6f, 0.2f, 0.2f));

            // Ayarlar Paneli (Pop-up)
            GameObject settingsPanel = CreateUIPanel(canvasObj.transform, "SettingsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 560));
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

            // Lobi Paneli (Pop-up)
            GameObject lobbyPanel = CreateUIPanel(canvasObj.transform, "LobbyPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 700));
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
            SetSerializedField(menuController, "settingsButton", settingsBtn);
            SetSerializedField(menuController, "quitButton", quitBtn);
            SetSerializedField(menuController, "settingsController", settingsCtrl);
            SetSerializedField(menuController, "networkLobbyUI", lobbyUI);

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

            // Üst Bilgi Barı
            GameObject topBar = CreateUIPanel(hudRoot.transform, "TopBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50), new Vector2(0, 80));
            topBar.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.85f);

            GameObject roundTextObj = CreateUIText(topBar.transform, "RoundText", "TUR 1", 32, FontStyles.Bold, Color.yellow, new Vector2(-600, 0), new Vector2(250, 60));
            GameObject safeKeysTextObj = CreateUIText(topBar.transform, "SafeKeysText", "Kalan Güvenli Tuş: 14", 28, FontStyles.Normal, Color.white, new Vector2(0, 0), new Vector2(400, 60));
            GameObject promptTextObj = CreateUIText(topBar.transform, "PromptText", "Sıra: Oyuncu 1", 32, FontStyles.Bold, Color.cyan, new Vector2(600, 0), new Vector2(300, 60));

            // Bildirim Afişi (Banner)
            GameObject bannerObj = CreateUIPanel(hudRoot.transform, "Banner", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(900, 100));
            bannerObj.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.9f);
            GameObject bannerTextObj = CreateUIText(bannerObj.transform, "BannerText", "BİLDİRİM", 36, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(850, 90));
            bannerObj.SetActive(false);

            // Alt Bar (Oyuncu Kartları)
            GameObject bottomBar = CreateUIPanel(hudRoot.transform, "BottomBar", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 75), new Vector2(0, 130));
            bottomBar.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.65f);

            List<PlayerCardUI> cards = new List<PlayerCardUI>();
            float cardWidth = 320f;
            float spacing = 360f;
            float startOffset = -1.5f * spacing;

            for (int i = 0; i < 4; i++)
            {
                GameObject cardObj = CreateUIPanel(bottomBar.transform, $"PlayerCard_{i + 1}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(startOffset + (i * spacing), 0), new Vector2(cardWidth, 110));
                PlayerCardUI cardUI = cardObj.AddComponent<PlayerCardUI>();

                GameObject pName = CreateUIText(cardObj.transform, "PlayerName", $"Oyuncu {i + 1}", 22, FontStyles.Bold, GetPlayerColor(i), new Vector2(-60, 28), new Vector2(160, 30));
                GameObject pScore = CreateUIText(cardObj.transform, "Score", "Skor: 0", 18, FontStyles.Normal, Color.white, new Vector2(75, 28), new Vector2(120, 30));

                // Sol El Badge
                GameObject leftBadge = CreateUIPanel(cardObj.transform, "LeftHandBadge", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-75, -20), new Vector2(130, 32));
                leftBadge.GetComponent<Image>().color = new Color(0.2f, 0.75f, 0.3f);
                GameObject leftText = CreateUIText(leftBadge.transform, "LeftText", "SOL: OK", 16, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(120, 30));

                // Sağ El Badge
                GameObject rightBadge = CreateUIPanel(cardObj.transform, "RightHandBadge", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(75, -20), new Vector2(130, 32));
                rightBadge.GetComponent<Image>().color = new Color(0.2f, 0.75f, 0.3f);
                GameObject rightText = CreateUIText(rightBadge.transform, "RightText", "SAĞ: OK", 16, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(120, 30));

                // Elendi Paneli
                GameObject elimOverlay = CreateUIPanel(cardObj.transform, "EliminatedOverlay", new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
                elimOverlay.GetComponent<Image>().color = new Color(0.4f, 0.05f, 0.05f, 0.88f);
                CreateUIText(elimOverlay.transform, "ElimText", "ELENDİ (KASAP)", 22, FontStyles.Bold, Color.red, Vector2.zero, new Vector2(250, 40));
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
            GameObject wheelObj = CreateUIPanel(canvasRoot, "EmoteWheel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440, 440));
            wheelObj.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.94f);

            // Cooldown Ring
            GameObject ringObj = new GameObject("CooldownRing");
            ringObj.transform.SetParent(wheelObj.transform, false);
            var ringRect = ringObj.AddComponent<RectTransform>();
            ringRect.sizeDelta = new Vector2(450, 450);
            var ringImg = ringObj.AddComponent<Image>();
            ringImg.color = new Color(1f, 0.45f, 0.15f, 0.85f);
            ringImg.type = Image.Type.Filled;
            ringImg.fillMethod = Image.FillMethod.Radial360;
            ringImg.fillAmount = 0f;

            // Merkez Başlık
            GameObject centerTextObj = CreateUIText(wheelObj.transform, "CenterTitle", "EMOTE SEÇ\n<size=16>[E Tuşu / Gamepad Y]</size>", 22, FontStyles.Bold, Color.yellow, Vector2.zero, new Vector2(220, 60));

            // 4 Dilim Panelleri (0: Yukarı, 1: Sağ, 2: Aşağı, 3: Sol)
            Vector2[] slicePositions = new Vector2[] {
                new Vector2(0, 135),    // Yukarı
                new Vector2(135, 0),    // Sağ
                new Vector2(0, -135),   // Aşağı
                new Vector2(-135, 0)    // Sol
            };

            Image[] highlights = new Image[4];
            TextMeshProUGUI[] labels = new TextMeshProUGUI[4];

            for (int i = 0; i < 4; i++)
            {
                GameObject sliceObj = CreateUIPanel(wheelObj.transform, $"Slice_{i}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), slicePositions[i], new Vector2(145, 50));
                Image sImg = sliceObj.GetComponent<Image>();
                sImg.color = new Color(0.2f, 0.2f, 0.26f, 0.65f);
                highlights[i] = sImg;

                GameObject lObj = CreateUIText(sliceObj.transform, "Label", $"EMOTE {i + 1}", 16, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(140, 45));
                labels[i] = lObj.GetComponent<TextMeshProUGUI>();
            }

            EmoteWheelUI wheelCtrl = wheelObj.AddComponent<EmoteWheelUI>();
            SetSerializedField(wheelCtrl, "wheelRoot", wheelObj);
            SetSerializedField(wheelCtrl, "cooldownRing", ringImg);
            SetSerializedField(wheelCtrl, "sliceHighlights", highlights);
            SetSerializedField(wheelCtrl, "sliceLabels", labels);

            wheelObj.SetActive(false);
            return wheelCtrl;
        }

        private static Material CreateColoredMaterial(string name, Color color)
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
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            }
            return mat;
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

