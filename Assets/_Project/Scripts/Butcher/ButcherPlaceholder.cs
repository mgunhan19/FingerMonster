using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FingerMonster.Audio;
using FingerMonster.Data;
using FingerMonster.Player;

namespace FingerMonster.Gameplay
{
    /// <summary>
    /// Masanın etrafında devriye gezen, oyuncuların yanına gelip dalga geçen ve
    /// yanlış tuşa basan oyuncunun elini satırıyla kesen 3D Kasap (kasap.fbx) kontrolcüsü.
    /// Masadaki oyunculardan uzun veya geniş olmaması için otomatik boyut kalibrasyonu içerir.
    /// </summary>
    public class ButcherPlaceholder : MonoBehaviour
    {
        [Header("Görsel Parçalar")]
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Transform weaponArmPivot;

        [Header("Kasap 3D Model & Boyut Kalibrasyonu (kasap.fbx)")]
        [SerializeField] private GameObject customButcherModelPrefab;
        [Tooltip("Masada oturan oyuncuların baş hizası (~1.35m) sınırına göre kasabın maksimum boyu.")]
        [SerializeField] private float targetButcherHeight = 1.34f;
        [Tooltip("Masada oturan oyuncuların omuz genişliği (~0.50m) sınırına göre kasabın maksimum gövde genişliği.")]
        [SerializeField] private float maxButcherWidth = 0.48f;

        [Header("Absürt İnfaz Silahları (Yedek)")]
        [SerializeField] private GameObject[] weaponModels;

        [Header("Kasap Harici Animasyon Klipleri (Assets/_Project/Art/Characters/Butcher/Butcher_*.fbx)")]
        [SerializeField] private GameObject mixamoPoseDriverPrefab;
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private AnimationClip walkClip;
        [SerializeField] private AnimationClip taunt1Clip;
        [SerializeField] private AnimationClip taunt2Clip;
        [SerializeField] private AnimationClip attackClip;
        [SerializeField] private RuntimeAnimatorController butcherAnimatorController;

        // Eklemli kasap.fbx rig pivotları
        private Transform kasapInstanceRoot;
        private Transform pivotTorso;
        private Transform pivotHead;
        private Transform pivotJaw;
        private Transform pivotRightShoulder;
        private Transform pivotRightElbow;
        private Transform pivotLeftShoulder;
        private Transform pivotLeftElbow;
        private Transform pivotRightLeg;
        private Transform pivotLeftLeg;
        private Transform cleaverBladeTransform;
        private Transform weaponSocket;
        private GameObject equippedWeaponInstance;
        private Animator butcherAnimator;

        // Hayaletlere özel köşe önizleme klonu (GhostOnly Layer 11)
        private GameObject ghostPreviewRoot;
        private Transform ghostPreviewModelRoot;
        private Transform ghostPreviewWeaponSocket;
        private GameObject ghostPreviewWeaponInstance;
        private Coroutine ghostPreviewIdleRoutine;

        // Görünmez Mixamo iskelet sürücüsü ve referans kemik rotasyonları
        private Transform mixamoDriverRoot;
        private Transform mixSpine;
        private Transform mixHead;
        private Transform mixRightArm;
        private Transform mixRightForeArm;
        private Transform mixLeftArm;
        private Transform mixLeftForeArm;
        private Transform mixRightUpLeg;
        private Transform mixLeftUpLeg;

        private Quaternion refSpineRot = Quaternion.identity;
        private Quaternion refHeadRot = Quaternion.identity;
        private Quaternion refRightArmRot = Quaternion.identity;
        private Quaternion refRightForeArmRot = Quaternion.identity;
        private Quaternion refLeftArmRot = Quaternion.identity;
        private Quaternion refLeftForeArmRot = Quaternion.identity;
        private Quaternion refRightUpLegRot = Quaternion.identity;
        private Quaternion refLeftUpLegRot = Quaternion.identity;
        private bool hasMixamoDriver = false;
        private AnimationClip activeTauntClip;
        private float tauntClipElapsed = 0f;

        private Coroutine activeRoutine;
        private int currentWeaponIndex = 0;

        private enum ButcherIdleMode { Patrolling, WalkingToTaunt, Taunting }
        private ButcherIdleMode idleMode = ButcherIdleMode.Patrolling;
        private float patrolAngle = -0.78f; // Radyan
        [SerializeField] private float patrolRadius = 2.65f;
        [SerializeField] private float patrolAngularSpeed = 0.52f;
        private float nextTauntTimer = 4.5f;
        private float tauntStateTimer = 0f;
        private float patrolPauseTimer = 0f;
        private float nextPatrolPauseCountdown = 6.2f;
        private Vector3 tauntTargetPos;
        private Vector3 tauntLookTarget;
        private bool isStriking = false;

        private static readonly Vector3[] DefaultSeatPositions = new Vector3[]
        {
            new Vector3(0f, 0f, -2.05f),
            new Vector3(0f, 0f, 1.48f),
            new Vector3(-1.68f, 0f, 0f),
            new Vector3(1.68f, 0f, 0f)
        };

        public int CurrentWeaponIndex => currentWeaponIndex;

        private void Awake()
        {
            EnsureKasapModelSetup();
            SetLayerRecursively(gameObject, 0);
            if (visualRoot != null) visualRoot.SetActive(true);
            ResumePatrol();
        }

        private void OnEnable()
        {
            EnsureKasapModelSetup();
            SetLayerRecursively(gameObject, 0);
            if (visualRoot != null) visualRoot.SetActive(true);
        }

        private void OnDestroy()
        {
            DestroyGhostPreview();
        }

        private void Start()
        {
            EnsureKasapModelSetup();
            CalibrateScaleToSeatedPlayers(kasapInstanceRoot);
            ResumePatrol();
        }

        /// <summary>
        /// Dışarıdan (ör. SceneSetupTool) kasap.fbx prefab referansını atamak ve kurulumu tetiklemek için kullanılır.
        /// </summary>
        public void SetCustomButcherModelPrefab(GameObject modelPrefab)
        {
            if (modelPrefab != null)
            {
                customButcherModelPrefab = modelPrefab;
            }
            EnsureKasapModelSetup();
        }

        /// <summary>
        /// Dışarıdan (ör. SceneSetupTool) 5 yeni animasyon klibini (`Butcher_Idle`, `Butcher_Walking`, `Butcher_Taunt1`, `Butcher_Taunt2`, `Butcher_Attack`)
        /// ve Mixamo iskelet sürücüsünü atamak için kullanılır.
        /// </summary>
        public void SetButcherAnimationAssets(
            GameObject driverPrefab,
            AnimationClip idle,
            AnimationClip walk,
            AnimationClip taunt1,
            AnimationClip taunt2,
            AnimationClip attack,
            RuntimeAnimatorController controller = null)
        {
            if (driverPrefab != null) mixamoPoseDriverPrefab = driverPrefab;
            if (idle != null) idleClip = idle;
            if (walk != null) walkClip = walk;
            if (taunt1 != null) taunt1Clip = taunt1;
            if (taunt2 != null) taunt2Clip = taunt2;
            if (attack != null) attackClip = attack;
            if (controller != null) butcherAnimatorController = controller;

            EnsureKasapModelSetup();
            InitializeMixamoRetargetDriver();
        }

#if UNITY_EDITOR
        private void EnsureAnimationClipsLoadedEditor()
        {
            if (mixamoPoseDriverPrefab == null)
            {
                mixamoPoseDriverPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Butcher/Butcher_Idle.fbx");
            }
            if (idleClip == null) idleClip = LoadFirstFbxClipEditor("Assets/_Project/Art/Characters/Butcher/Butcher_Idle.fbx");
            if (walkClip == null) walkClip = LoadFirstFbxClipEditor("Assets/_Project/Art/Characters/Butcher/Butcher_Walking.fbx");
            if (taunt1Clip == null) taunt1Clip = LoadFirstFbxClipEditor("Assets/_Project/Art/Characters/Butcher/Butcher_Taunt1.fbx");
            if (taunt2Clip == null) taunt2Clip = LoadFirstFbxClipEditor("Assets/_Project/Art/Characters/Butcher/Butcher_Taunt2.fbx");
            if (attackClip == null) attackClip = LoadFirstFbxClipEditor("Assets/_Project/Art/Characters/Butcher/Butcher_Attack.fbx");
        }

        private static AnimationClip LoadFirstFbxClipEditor(string assetPath)
        {
            Object[] allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (allAssets == null) return null;
            for (int i = 0; i < allAssets.Length; i++)
            {
                if (allAssets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
            return null;
        }
#endif

        /// <summary>
        /// kasap.fbx modelini yükler, Blender kamera/ışıklarını temizler, URP materyallerini boyar,
        /// çok eklemli (baş, çene, gövde, omuz, dirsek, bacak) rig hiyerarşisini kurar ve oyuncu boyutuna kalibre eder.
        /// </summary>
        public void EnsureKasapModelSetup()
        {
#if UNITY_EDITOR
            if (customButcherModelPrefab == null)
            {
                customButcherModelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Butcher/kasap.fbx");
            }
            EnsureAnimationClipsLoadedEditor();
#endif
            Transform existingKasap = transform.Find("KasapModelRoot");
            if (existingKasap == null && customButcherModelPrefab != null)
            {
                GameObject instance = Instantiate(customButcherModelPrefab, transform);
                instance.name = "KasapModelRoot";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                existingKasap = instance.transform;
            }

            if (existingKasap != null)
            {
                kasapInstanceRoot = existingKasap;
                kasapInstanceRoot.gameObject.SetActive(true);
                visualRoot = kasapInstanceRoot.gameObject;

                // Eski ilkel kapsül modeli varsa gizle
                Transform oldCapsule = transform.Find("ButcherCapsule");
                if (oldCapsule != null)
                {
                    oldCapsule.gameObject.SetActive(false);
                }

                DisableEmbeddedLightsAndCameras(kasapInstanceRoot);
                ApplyRealisticButcherMaterials(kasapInstanceRoot);
                BuildArticulatedKasapRig(kasapInstanceRoot);
                CalibrateScaleToSeatedPlayers(kasapInstanceRoot);
                InitializeMixamoRetargetDriver();

                butcherAnimator = kasapInstanceRoot.GetComponentInChildren<Animator>();
                if (butcherAnimator != null && butcherAnimatorController != null && butcherAnimator.runtimeAnimatorController == null)
                {
                    butcherAnimator.runtimeAnimatorController = butcherAnimatorController;
                }
            }
            else if (visualRoot == null)
            {
                Transform capsule = transform.Find("ButcherCapsule");
                visualRoot = capsule != null ? capsule.gameObject : gameObject;
            }
        }

        private void InitializeMixamoRetargetDriver()
        {
#if UNITY_EDITOR
            EnsureAnimationClipsLoadedEditor();
#endif
            if (mixamoPoseDriverPrefab == null) return;

            Transform existingDriver = transform.Find("MixamoPoseDriver");
            if (existingDriver == null)
            {
                GameObject driverObj = Instantiate(mixamoPoseDriverPrefab, transform);
                driverObj.name = "MixamoPoseDriver";
                existingDriver = driverObj.transform;
            }

            mixamoDriverRoot = existingDriver;
            mixamoDriverRoot.gameObject.SetActive(true);
            mixamoDriverRoot.localPosition = Vector3.zero;
            mixamoDriverRoot.localRotation = Quaternion.identity;
            mixamoDriverRoot.localScale = Vector3.one;

            // Sahnede yalnızca kasap.fbx görünsün; sürücü modelin tüm Renderer, Light ve Camera bileşenlerini kapat
            Renderer[] driverRenderers = mixamoDriverRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < driverRenderers.Length; i++)
            {
                if (driverRenderers[i] != null) driverRenderers[i].enabled = false;
            }

            Light[] driverLights = mixamoDriverRoot.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < driverLights.Length; i++)
            {
                if (driverLights[i] != null)
                {
                    driverLights[i].enabled = false;
                    driverLights[i].gameObject.SetActive(false);
                }
            }

            Camera[] driverCams = mixamoDriverRoot.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < driverCams.Length; i++)
            {
                if (driverCams[i] != null)
                {
                    driverCams[i].enabled = false;
                    driverCams[i].gameObject.SetActive(false);
                }
            }

            Animator driverAnim = mixamoDriverRoot.GetComponentInChildren<Animator>(true);
            if (driverAnim != null)
            {
                if (butcherAnimatorController != null)
                {
                    driverAnim.runtimeAnimatorController = butcherAnimatorController;
                }
                // SampleAnimation ile doğrudan sürüleceği için Animator'un kendi üzerine yazmasını önle
                driverAnim.enabled = false;
            }

            mixSpine = FindDeepChild(mixamoDriverRoot, "mixamorig:Spine1");
            if (mixSpine == null) mixSpine = FindDeepChild(mixamoDriverRoot, "mixamorig:Spine");
            mixHead = FindDeepChild(mixamoDriverRoot, "mixamorig:Head");
            mixRightArm = FindDeepChild(mixamoDriverRoot, "mixamorig:RightArm");
            mixRightForeArm = FindDeepChild(mixamoDriverRoot, "mixamorig:RightForeArm");
            mixLeftArm = FindDeepChild(mixamoDriverRoot, "mixamorig:LeftArm");
            mixLeftForeArm = FindDeepChild(mixamoDriverRoot, "mixamorig:LeftForeArm");
            mixRightUpLeg = FindDeepChild(mixamoDriverRoot, "mixamorig:RightUpLeg");
            mixLeftUpLeg = FindDeepChild(mixamoDriverRoot, "mixamorig:LeftUpLeg");

            hasMixamoDriver = (mixSpine != null || mixRightArm != null || mixRightUpLeg != null);
            if (!hasMixamoDriver) return;

            AnimationClip refClip = idleClip != null ? idleClip : walkClip;
            if (refClip != null)
            {
                refClip.SampleAnimation(mixamoDriverRoot.gameObject, 0f);
            }

            Quaternion invRoot = Quaternion.Inverse(mixamoDriverRoot.rotation);
            if (mixSpine != null) refSpineRot = invRoot * mixSpine.rotation;
            if (mixHead != null) refHeadRot = invRoot * mixHead.rotation;
            if (mixRightArm != null) refRightArmRot = invRoot * mixRightArm.rotation;
            if (mixRightForeArm != null) refRightForeArmRot = invRoot * mixRightForeArm.rotation;
            if (mixLeftArm != null) refLeftArmRot = invRoot * mixLeftArm.rotation;
            if (mixLeftForeArm != null) refLeftForeArmRot = invRoot * mixLeftForeArm.rotation;
            if (mixRightUpLeg != null) refRightUpLegRot = invRoot * mixRightUpLeg.rotation;
            if (mixLeftUpLeg != null) refLeftUpLegRot = invRoot * mixLeftUpLeg.rotation;
        }

        private bool SampleAndRetargetClip(AnimationClip clip, float timeSeconds, bool loop, float jawExtraAngle = 0f, float blendFactor = 1f)
        {
            if (clip == null || !hasMixamoDriver || mixamoDriverRoot == null || kasapInstanceRoot == null || pivotTorso == null)
            {
                return false;
            }

            float clipLen = Mathf.Max(0.01f, clip.length);
            float sampleT = loop ? Mathf.Repeat(timeSeconds, clipLen) : Mathf.Clamp(timeSeconds, 0f, clipLen);
            clip.SampleAnimation(mixamoDriverRoot.gameObject, sampleT);

            Quaternion invDriverRoot = Quaternion.Inverse(mixamoDriverRoot.rotation);
            Quaternion kasapRootRot = kasapInstanceRoot.rotation;
            float slerpT = Mathf.Clamp01(blendFactor);

            // 1. Gövde (Torso)
            if (mixSpine != null && pivotTorso != null)
            {
                Quaternion curRot = invDriverRoot * mixSpine.rotation;
                Quaternion delta = curRot * Quaternion.Inverse(refSpineRot);
                Quaternion targetWorld = kasapRootRot * delta;
                pivotTorso.rotation = slerpT >= 0.99f ? targetWorld : Quaternion.Slerp(pivotTorso.rotation, targetWorld, slerpT);
            }

            // 2. Baş (Head) ve Çene (Jaw)
            if (mixHead != null && pivotHead != null)
            {
                Quaternion curRot = invDriverRoot * mixHead.rotation;
                Quaternion delta = curRot * Quaternion.Inverse(refHeadRot);
                Quaternion targetWorld = kasapRootRot * delta;
                pivotHead.rotation = slerpT >= 0.99f ? targetWorld : Quaternion.Slerp(pivotHead.rotation, targetWorld, slerpT);
            }

            if (pivotJaw != null)
            {
                float baseJaw = 2.5f + Mathf.Abs(Mathf.Sin(Time.time * 3.4f)) * 3.5f + jawExtraAngle;
                pivotJaw.localRotation = Quaternion.Slerp(pivotJaw.localRotation, Quaternion.Euler(baseJaw, 0f, 0f), Time.deltaTime * 14f);
            }

            // 3. Sağ Kol (Satır Tutan Kol)
            if (mixRightArm != null && pivotRightShoulder != null)
            {
                Quaternion curRot = invDriverRoot * mixRightArm.rotation;
                Quaternion delta = curRot * Quaternion.Inverse(refRightArmRot);
                Quaternion targetWorld = kasapRootRot * delta;
                pivotRightShoulder.rotation = slerpT >= 0.99f ? targetWorld : Quaternion.Slerp(pivotRightShoulder.rotation, targetWorld, slerpT);
            }

            if (mixRightForeArm != null && pivotRightElbow != null)
            {
                Quaternion curRot = invDriverRoot * mixRightForeArm.rotation;
                Quaternion delta = curRot * Quaternion.Inverse(refRightForeArmRot);
                Quaternion targetWorld = kasapRootRot * delta;
                pivotRightElbow.rotation = slerpT >= 0.99f ? targetWorld : Quaternion.Slerp(pivotRightElbow.rotation, targetWorld, slerpT);
            }

            // 4. Sol Kol
            if (mixLeftArm != null && pivotLeftShoulder != null)
            {
                Quaternion curRot = invDriverRoot * mixLeftArm.rotation;
                Quaternion delta = curRot * Quaternion.Inverse(refLeftArmRot);
                Quaternion targetWorld = kasapRootRot * delta;
                pivotLeftShoulder.rotation = slerpT >= 0.99f ? targetWorld : Quaternion.Slerp(pivotLeftShoulder.rotation, targetWorld, slerpT);
            }

            if (mixLeftForeArm != null && pivotLeftElbow != null)
            {
                Quaternion curRot = invDriverRoot * mixLeftForeArm.rotation;
                Quaternion delta = curRot * Quaternion.Inverse(refLeftForeArmRot);
                Quaternion targetWorld = kasapRootRot * delta;
                pivotLeftElbow.rotation = slerpT >= 0.99f ? targetWorld : Quaternion.Slerp(pivotLeftElbow.rotation, targetWorld, slerpT);
            }

            // 5. Bacaklar
            if (mixRightUpLeg != null && pivotRightLeg != null)
            {
                Quaternion curRot = invDriverRoot * mixRightUpLeg.rotation;
                Quaternion delta = curRot * Quaternion.Inverse(refRightUpLegRot);
                Quaternion targetWorld = kasapRootRot * delta;
                pivotRightLeg.rotation = slerpT >= 0.99f ? targetWorld : Quaternion.Slerp(pivotRightLeg.rotation, targetWorld, slerpT);
            }

            if (mixLeftUpLeg != null && pivotLeftLeg != null)
            {
                Quaternion curRot = invDriverRoot * mixLeftUpLeg.rotation;
                Quaternion delta = curRot * Quaternion.Inverse(refLeftUpLegRot);
                Quaternion targetWorld = kasapRootRot * delta;
                pivotLeftLeg.rotation = slerpT >= 0.99f ? targetWorld : Quaternion.Slerp(pivotLeftLeg.rotation, targetWorld, slerpT);
            }

            return true;
        }

        private void DisableEmbeddedLightsAndCameras(Transform root)
        {
            if (root == null) return;

            Camera[] cams = root.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] != null)
                {
                    cams[i].enabled = false;
                    cams[i].gameObject.SetActive(false);
                }
            }

            Light[] lights = root.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    lights[i].enabled = false;
                    lights[i].gameObject.SetActive(false);
                }
            }

            Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allChildren.Length; i++)
            {
                Transform t = allChildren[i];
                if (t == null || t == root) continue;
                if (t.name == "Kamera" || t.name == "Ana_Isik" || t.name.StartsWith("Kamera") || t.name.StartsWith("Ana_Isik"))
                {
                    t.gameObject.SetActive(false);
                }
            }
        }

        private void ApplyRealisticButcherMaterials(Transform root)
        {
            if (root == null) return;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");
            if (urpLit == null) return;

            Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;

                string objName = r.gameObject.name;
                Material[] sharedMats = r.sharedMaterials;
                if (sharedMats == null || sharedMats.Length == 0) continue;

                Material[] updatedMats = new Material[sharedMats.Length];
                for (int m = 0; m < sharedMats.Length; m++)
                {
                    string rawMatName = sharedMats[m] != null ? sharedMats[m].name : string.Empty;
                    string cleanMatName = rawMatName.Replace(" (Instance)", "").Trim();
                    string cacheKey = cleanMatName + "_" + ResolveObjectMaterialGroup(objName);

                    if (!materialCache.TryGetValue(cacheKey, out Material matInstance) || matInstance == null)
                    {
                        matInstance = new Material(urpLit);
                        matInstance.name = string.IsNullOrEmpty(cleanMatName) ? ("Kasap_" + objName) : ("Kasap_" + cleanMatName);
                        ConfigureButcherMaterialProperties(matInstance, cleanMatName, objName);
                        materialCache[cacheKey] = matInstance;
                    }

                    updatedMats[m] = matInstance;
                }

                r.sharedMaterials = updatedMats;
            }
        }

        private string ResolveObjectMaterialGroup(string objName)
        {
            if (string.IsNullOrEmpty(objName)) return "Default";
            if (objName.StartsWith("Kan_Akinti") || objName.StartsWith("Kan_Damla")) return "Blood";
            if (objName == "Satir_Bicak" || objName == "Satir_Sirt") return "CleaverBlade";
            if (objName == "Satir_Sap") return "CleaverHandle";
            if (objName.StartsWith("Onluk")) return "Apron";
            if (objName == "Yaka") return "Shirt";
            return "Mat";
        }

        private void ConfigureButcherMaterialProperties(Material mat, string matName, string objName)
        {
            Color baseColor = new Color(0.74f, 0.56f, 0.46f);
            float smoothness = 0.32f;
            float metallic = 0.0f;

            if (objName.StartsWith("Kan_Akinti") || objName.StartsWith("Kan_Damla") ||
                matName.StartsWith("Yara_Et") || matName.StartsWith("Dolgu_Kirmizi"))
            {
                baseColor = new Color(0.34f, 0.015f, 0.015f);
                smoothness = 0.88f;
                metallic = 0.05f;
            }
            else if (objName == "Satir_Bicak" || objName == "Satir_Sirt" || matName.StartsWith("Satir_Metal"))
            {
                baseColor = new Color(0.75f, 0.77f, 0.80f);
                smoothness = 0.85f;
                metallic = 0.90f;
            }
            else if (objName == "Satir_Sap" || matName.StartsWith("Sap"))
            {
                baseColor = new Color(0.26f, 0.14f, 0.07f);
                smoothness = 0.38f;
                metallic = 0.0f;
            }
            else if (objName.StartsWith("Onluk") || matName.StartsWith("Onluk"))
            {
                baseColor = new Color(0.36f, 0.11f, 0.09f);
                smoothness = 0.40f;
                metallic = 0.05f;
            }
            else if (matName.StartsWith("Deri_Gercekci"))
            {
                baseColor = new Color(0.74f, 0.56f, 0.46f);
                smoothness = 0.32f;
                metallic = 0.0f;
            }
            else if (matName.StartsWith("Gomlek") || objName == "Yaka")
            {
                baseColor = new Color(0.20f, 0.19f, 0.17f);
                smoothness = 0.18f;
                metallic = 0.0f;
            }
            else if (matName.StartsWith("Pantolon"))
            {
                baseColor = new Color(0.13f, 0.12f, 0.11f);
                smoothness = 0.20f;
                metallic = 0.0f;
            }
            else if (matName.StartsWith("Bot"))
            {
                baseColor = new Color(0.05f, 0.04f, 0.035f);
                smoothness = 0.55f;
                metallic = 0.10f;
            }
            else if (matName.StartsWith("Yara_Kenar") || matName.StartsWith("Karunkul"))
            {
                baseColor = new Color(0.46f, 0.12f, 0.12f);
                smoothness = 0.50f;
                metallic = 0.0f;
            }
            else if (matName.StartsWith("Iz_Soluk"))
            {
                baseColor = new Color(0.66f, 0.48f, 0.42f);
                smoothness = 0.30f;
                metallic = 0.0f;
            }
            else if (matName.StartsWith("Kemik") || matName.StartsWith("Dis"))
            {
                baseColor = new Color(0.85f, 0.80f, 0.70f);
                smoothness = 0.50f;
                metallic = 0.0f;
            }
            else if (matName.StartsWith("Goz_Saglam"))
            {
                baseColor = new Color(0.88f, 0.85f, 0.80f);
                smoothness = 0.82f;
                metallic = 0.0f;
            }
            else if (matName.StartsWith("Goz_Hasarli"))
            {
                baseColor = new Color(0.72f, 0.30f, 0.28f);
                smoothness = 0.85f;
                metallic = 0.0f;
            }
            else if (matName.StartsWith("Kornea") || matName.StartsWith("Kenar_Mavi"))
            {
                baseColor = new Color(0.10f, 0.14f, 0.18f);
                smoothness = 0.92f;
                metallic = 0.0f;
            }
            else if (matName.StartsWith("Kil"))
            {
                baseColor = new Color(0.04f, 0.03f, 0.025f);
                smoothness = 0.22f;
                metallic = 0.0f;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        }

        private void BuildArticulatedKasapRig(Transform root)
        {
            if (root == null) return;

            Transform existingTorso = root.Find("Pivot_Torso");
            if (existingTorso != null)
            {
                pivotTorso = existingTorso;
                pivotRightLeg = root.Find("Pivot_RightLeg");
                pivotLeftLeg = root.Find("Pivot_LeftLeg");
                pivotHead = FindDeepChild(pivotTorso, "Pivot_Head");
                pivotJaw = FindDeepChild(pivotTorso, "Pivot_Jaw");
                pivotRightShoulder = FindDeepChild(pivotTorso, "Pivot_RightShoulder");
                pivotRightElbow = FindDeepChild(pivotTorso, "Pivot_RightElbow");
                pivotLeftShoulder = FindDeepChild(pivotTorso, "Pivot_LeftShoulder");
                pivotLeftElbow = FindDeepChild(pivotTorso, "Pivot_LeftElbow");
                cleaverBladeTransform = FindDeepChild(root, "Satir_Bicak");
                if (pivotRightShoulder != null) weaponArmPivot = pivotRightShoulder;
                return;
            }

            // Rig kurulurken geçici olarak kök dönüşümünü sıfırla ki tüm eklem eksenleri kusursuz hizalansın
            Vector3 savedPos = transform.position;
            Quaternion savedRot = transform.rotation;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            Transform karin = root.Find("Karin");
            Transform boyun = root.Find("Boyun");
            Transform cene = root.Find("Cene_Kemigi");
            Transform omuzSag = root.Find("Omuz_Sag");
            Transform dirsekSag = root.Find("Dirsek_Sag");
            Transform omuzSol = root.Find("Omuz_Sol");
            Transform dirsekSol = root.Find("Dirsek_Sol");
            Transform bacakSag = root.Find("Bacak_Sag");
            Transform bacakSol = root.Find("Bacak_Sol");

            pivotRightLeg = CreatePivot(root, "Pivot_RightLeg", bacakSag != null ? bacakSag.position + new Vector3(0f, 0.32f, 0f) : new Vector3(0.095f, 0.79f, 0f));
            pivotLeftLeg = CreatePivot(root, "Pivot_LeftLeg", bacakSol != null ? bacakSol.position + new Vector3(0f, 0.32f, 0f) : new Vector3(-0.095f, 0.79f, 0f));

            pivotTorso = CreatePivot(root, "Pivot_Torso", karin != null ? karin.position : new Vector3(0f, 0.95f, 0f));
            pivotHead = CreatePivot(pivotTorso, "Pivot_Head", boyun != null ? boyun.position : new Vector3(0f, 1.50f, 0f));
            pivotJaw = CreatePivot(pivotHead, "Pivot_Jaw", cene != null ? cene.position : new Vector3(0f, 1.573f, 0.02f));

            pivotRightShoulder = CreatePivot(pivotTorso, "Pivot_RightShoulder", omuzSag != null ? omuzSag.position : new Vector3(0.20f, 1.37f, 0f));
            pivotRightElbow = CreatePivot(pivotRightShoulder, "Pivot_RightElbow", dirsekSag != null ? dirsekSag.position : new Vector3(0.285f, 1.05f, 0.01f));

            pivotLeftShoulder = CreatePivot(pivotTorso, "Pivot_LeftShoulder", omuzSol != null ? omuzSol.position : new Vector3(-0.20f, 1.37f, 0f));
            pivotLeftElbow = CreatePivot(pivotLeftShoulder, "Pivot_LeftElbow", dirsekSol != null ? dirsekSol.position : new Vector3(-0.285f, 1.05f, 0.01f));

            // Mevcut doğrudan çocukları listele ve ilgili eklem pivotlarına bağla
            List<Transform> directChildren = new List<Transform>();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && !child.name.StartsWith("Pivot_"))
                {
                    directChildren.Add(child);
                }
            }

            for (int i = 0; i < directChildren.Count; i++)
            {
                Transform child = directChildren[i];
                string n = child.name;
                if (n == "Kamera" || n == "Ana_Isik") continue;

                if (n == "Bacak_Sag" || n == "Bot_Sag")
                {
                    child.SetParent(pivotRightLeg, true);
                }
                else if (n == "Bacak_Sol" || n == "Bot_Sol")
                {
                    child.SetParent(pivotLeftLeg, true);
                }
                else if (n == "Cene_Kemigi" || n.StartsWith("Dis_Alt_"))
                {
                    child.SetParent(pivotJaw, true);
                }
                else if (n == "Boyun" || n.StartsWith("SKM_") || n == "Kafa" ||
                         n.StartsWith("Goz_") || n.StartsWith("Kornea_") || n.StartsWith("Kapak_") ||
                         n.StartsWith("Karunkul_") || n.StartsWith("Kulak_") || n.StartsWith("Dis_Ust_") ||
                         n == "Dolgu_Kirmizi" || n == "Kenar_Mavi")
                {
                    child.SetParent(pivotHead, true);
                }
                else if (n == "Omuz_Sag" || n == "UstKol_Sag")
                {
                    child.SetParent(pivotRightShoulder, true);
                }
                else if (n == "Dirsek_Sag" || n == "OnKol_Sag" || n == "Avuc_Sag" ||
                         n == "Basparmak_Sag" || n.StartsWith("Parmak_Sag_") ||
                         n.StartsWith("Satir_") || n.StartsWith("Kan_Akinti"))
                {
                    child.SetParent(pivotRightElbow, true);
                }
                else if (n == "Omuz_Sol" || n == "UstKol_Sol")
                {
                    child.SetParent(pivotLeftShoulder, true);
                }
                else if (n == "Dirsek_Sol" || n == "OnKol_Sol" || n == "Avuc_Sol" ||
                         n == "Basparmak_Sol" || n.StartsWith("Parmak_Sol_"))
                {
                    child.SetParent(pivotLeftElbow, true);
                }
                else
                {
                    // Karin, Govde, Onluk, Onluk_Aski, Yaka, Kan_Damla* vb.
                    child.SetParent(pivotTorso, true);
                }
            }

            cleaverBladeTransform = FindDeepChild(root, "Satir_Bicak");
            weaponArmPivot = pivotRightShoulder;

            transform.position = savedPos;
            transform.rotation = savedRot;
        }

        private Transform CreatePivot(Transform parent, string pivotName, Vector3 worldPos)
        {
            GameObject go = new GameObject(pivotName);
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.position = worldPos;
            t.rotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return t;
        }

        private Transform FindDeepChild(Transform parent, string targetName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c.name == targetName) return c;
                Transform found = FindDeepChild(c, targetName);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Kasabın boyunu ve genişliğini masada oturan oyunculara göre kalibre eder;
        /// kasabın oyunculardan kesinlikle daha uzun veya daha geniş olmamasını sağlar.
        /// </summary>
        public void CalibrateScaleToSeatedPlayers(Transform root)
        {
            if (root == null) return;

            Vector3 savedPos = transform.position;
            Quaternion savedRot = transform.rotation;

            transform.position = new Vector3(savedPos.x, 0f, savedPos.z);
            transform.rotation = Quaternion.identity;

            ResetRigToNeutralPose();
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds totalBounds = new Bounds(root.position, Vector3.zero);
            bool hasBounds = false;
            Bounds torsoBounds = new Bounds(root.position, Vector3.zero);
            bool hasTorsoBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.gameObject.activeInHierarchy) continue;

                if (!hasBounds)
                {
                    totalBounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    totalBounds.Encapsulate(r.bounds);
                }

                string rName = r.gameObject.name;
                if (rName == "Govde" || rName == "Karin" || rName.StartsWith("Onluk") || rName.StartsWith("Omuz_"))
                {
                    if (!hasTorsoBounds)
                    {
                        torsoBounds = r.bounds;
                        hasTorsoBounds = true;
                    }
                    else
                    {
                        torsoBounds.Encapsulate(r.bounds);
                    }
                }
            }

            float playerTopY = 1.34f;
            float playerWidth = 0.48f;

            var seats = Object.FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None);
            if (seats != null && seats.Length > 0)
            {
                float sumTopY = 0f;
                float sumWidth = 0f;
                int count = 0;

                for (int i = 0; i < seats.Length; i++)
                {
                    PlayerSeat seat = seats[i];
                    if (seat == null || seat.Avatar == null) continue;

                    Renderer[] seatRenderers = seat.Avatar.GetComponentsInChildren<Renderer>(true);
                    Bounds sb = new Bounds(seat.Avatar.transform.position, Vector3.zero);
                    bool hasSb = false;
                    for (int j = 0; j < seatRenderers.Length; j++)
                    {
                        Renderer sr = seatRenderers[j];
                        if (sr == null || !sr.gameObject.activeInHierarchy) continue;
                        if (!hasSb)
                        {
                            sb = sr.bounds;
                            hasSb = true;
                        }
                        else
                        {
                            sb.Encapsulate(sr.bounds);
                        }
                    }

                    if (hasSb && sb.max.y > 0.85f && sb.max.y < 2.1f)
                    {
                        sumTopY += sb.max.y;
                        float seatW = Mathf.Max(sb.size.x, sb.size.z);
                        sumWidth += (seatW > 0.25f && seatW < 0.80f) ? seatW : 0.48f;
                        count++;
                    }
                }

                if (count > 0)
                {
                    playerTopY = sumTopY / count;
                    playerWidth = sumWidth / count;
                }
            }

            float rawHeight = hasBounds ? totalBounds.size.y : 1.82f;
            float rawTorsoWidth = hasTorsoBounds ? torsoBounds.size.x : (hasBounds ? totalBounds.size.x * 0.75f : 0.62f);

            float allowedHeight = Mathf.Min(targetButcherHeight, playerTopY * 0.96f);
            float allowedWidth = Mathf.Min(maxButcherWidth, playerWidth * 0.95f);

            float scaleH = allowedHeight / Mathf.Max(0.01f, rawHeight);
            float scaleW = allowedWidth / Mathf.Max(0.01f, rawTorsoWidth);
            float finalScale = Mathf.Clamp(Mathf.Min(scaleH, scaleW), 0.35f, 1.0f);

            root.localScale = new Vector3(finalScale, finalScale, finalScale);

            // Ölçekleme sonrasında bot tabanını tam zemine (y = 0) oturt
            float minY = transform.position.y;
            bool foundMin = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                if (!foundMin || r.bounds.min.y < minY)
                {
                    minY = r.bounds.min.y;
                    foundMin = true;
                }
            }

            if (foundMin)
            {
                float groundOffset = transform.position.y - minY;
                root.localPosition = new Vector3(0f, groundOffset, 0f);
            }

            transform.position = savedPos;
            transform.rotation = savedRot;
        }

        private void ResetRigToNeutralPose()
        {
            if (pivotTorso != null) pivotTorso.localRotation = Quaternion.identity;
            if (pivotHead != null) pivotHead.localRotation = Quaternion.identity;
            if (pivotJaw != null) pivotJaw.localRotation = Quaternion.identity;
            if (pivotRightShoulder != null) pivotRightShoulder.localRotation = Quaternion.identity;
            if (pivotRightElbow != null) pivotRightElbow.localRotation = Quaternion.identity;
            if (pivotLeftShoulder != null) pivotLeftShoulder.localRotation = Quaternion.identity;
            if (pivotLeftElbow != null) pivotLeftElbow.localRotation = Quaternion.identity;
            if (pivotRightLeg != null) pivotRightLeg.localRotation = Quaternion.identity;
            if (pivotLeftLeg != null) pivotLeftLeg.localRotation = Quaternion.identity;
        }

        public void ResumePatrol()
        {
            SetLayerRecursively(gameObject, 0);
            if (visualRoot != null)
            {
                visualRoot.SetActive(true);
            }
            SelectWeapon(currentWeaponIndex);
            isStriking = false;
            idleMode = ButcherIdleMode.Patrolling;
        }

        /// <summary>
        /// Kasabın merkezdeki masanın, oturan 4 oyuncunun ve duvar önü dekorların içinden geçmesini kesin olarak önler.
        /// </summary>
        public Vector3 ClampWalkablePosition(Vector3 flatPos)
        {
            flatPos.y = 0f;

            // 1) Masa ve Daktilo Engeli (minTableRadius = 1.58f)
            const float minTableRadius = 1.58f;
            if (flatPos.magnitude < minTableRadius)
            {
                flatPos = (flatPos.sqrMagnitude > 0.0001f ? flatPos.normalized : Vector3.forward) * minTableRadius;
            }

            // 2) 4 Oyuncu Koltuğu ve Karakteri Engeli (seatKeepOutRadius = 0.86f)
            const float seatKeepOutRadius = 0.86f;
            for (int i = 0; i < DefaultSeatPositions.Length; i++)
            {
                Vector3 seatFlat = DefaultSeatPositions[i];
                seatFlat.y = 0f;
                Vector3 diff = flatPos - seatFlat;
                diff.y = 0f;
                float dist = diff.magnitude;
                if (dist < seatKeepOutRadius)
                {
                    Vector3 pushDir = dist > 0.001f ? (diff / dist) : (seatFlat.sqrMagnitude > 0.001f ? seatFlat.normalized : Vector3.forward);
                    flatPos = seatFlat + pushDir * seatKeepOutRadius;
                }
            }

            var seats = Object.FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None);
            if (seats != null)
            {
                for (int i = 0; i < seats.Length; i++)
                {
                    if (seats[i] == null) continue;
                    Vector3 seatFlat = seats[i].transform.position;
                    seatFlat.y = 0f;
                    Vector3 diff = flatPos - seatFlat;
                    diff.y = 0f;
                    float dist = diff.magnitude;
                    if (dist < seatKeepOutRadius)
                    {
                        Vector3 pushDir = dist > 0.001f ? (diff / dist) : (seatFlat.sqrMagnitude > 0.001f ? seatFlat.normalized : Vector3.forward);
                        flatPos = seatFlat + pushDir * seatKeepOutRadius;
                    }
                }
            }

            // Tekrar masa kontrolü: itme sonrası masaya yaklaştıysa dışarı çıkar
            if (flatPos.magnitude < minTableRadius)
            {
                flatPos = (flatPos.sqrMagnitude > 0.0001f ? flatPos.normalized : Vector3.forward) * minTableRadius;
            }

            // 3) Duvarlar ve Demirci Dekorları Sınırı (maxCoord = 3.35f, maxRadius = 3.40f)
            flatPos.x = Mathf.Clamp(flatPos.x, -3.35f, 3.35f);
            flatPos.z = Mathf.Clamp(flatPos.z, -3.35f, 3.35f);
            if (flatPos.magnitude > 3.40f)
            {
                flatPos = flatPos.normalized * 3.40f;
            }

            return flatPos;
        }

        private Vector3 MoveAlongSafeRing(Vector3 flatCurrent, Vector3 flatTarget, float linearSpeed, float dt, out Vector3 moveDir, out float remainingDist)
        {
            flatCurrent.y = 0f;
            flatTarget.y = 0f;

            float curAngDeg = Mathf.Atan2(flatCurrent.z, flatCurrent.x) * Mathf.Rad2Deg;
            float tgtAngDeg = Mathf.Atan2(flatTarget.z, flatTarget.x) * Mathf.Rad2Deg;
            float angDiffAbs = Mathf.Abs(Mathf.DeltaAngle(curAngDeg, tgtAngDeg));

            float safeCorridorR = Mathf.Max(patrolRadius, 2.65f);
            float angularStepDeg = (linearSpeed / Mathf.Max(2.2f, patrolRadius)) * Mathf.Rad2Deg * dt;
            float nextAngDeg = Mathf.MoveTowardsAngle(curAngDeg, tgtAngDeg, angularStepDeg);
            float nextAngRad = nextAngDeg * Mathf.Deg2Rad;

            float curR = flatCurrent.magnitude;
            float targetR = Mathf.Max(1.62f, flatTarget.magnitude);
            float desiredR = angDiffAbs > 18f ? safeCorridorR : targetR;
            float nextR = Mathf.MoveTowards(curR, desiredR, 2.4f * dt);

            Vector3 rawNext = new Vector3(Mathf.Cos(nextAngRad) * nextR, 0f, Mathf.Sin(nextAngRad) * nextR);
            Vector3 clampedNext = ClampWalkablePosition(rawNext);

            Vector3 stepVec = clampedNext - flatCurrent;
            stepVec.y = 0f;
            if (stepVec.sqrMagnitude > 0.00001f)
            {
                moveDir = stepVec.normalized;
            }
            else
            {
                Vector3 direct = flatTarget - flatCurrent;
                direct.y = 0f;
                moveDir = direct.sqrMagnitude > 0.0001f ? direct.normalized : transform.forward;
            }

            remainingDist = Vector3.Distance(clampedNext, flatTarget);
            return clampedNext;
        }

        private void Update()
        {
            if (isStriking) return;

            switch (idleMode)
            {
                case ButcherIdleMode.Patrolling:
                {
                    if (patrolPauseTimer > 0f)
                    {
                        patrolPauseTimer -= Time.deltaTime;
                        Vector3 pausedFlat = ClampWalkablePosition(new Vector3(transform.position.x, 0f, transform.position.z));
                        transform.position = new Vector3(pausedFlat.x, 0f, pausedFlat.z);

                        Vector3 toCenterPause = (Vector3.zero - pausedFlat).normalized;
                        if (toCenterPause.sqrMagnitude > 0.001f)
                        {
                            Quaternion pauseRot = Quaternion.LookRotation(toCenterPause, Vector3.up);
                            transform.rotation = Quaternion.Slerp(transform.rotation, pauseRot, Time.deltaTime * 6f);
                        }

                        if (!SampleAndRetargetClip(idleClip, Time.time, true, 0f, Time.deltaTime * 10f))
                        {
                            AnimateArticulatedPose(ButcherIdleMode.Patrolling, Time.time * 1.5f);
                        }
                        UpdateAnimatorParameters(false, 0f, false);
                        break;
                    }

                    nextPatrolPauseCountdown -= Time.deltaTime;
                    if (nextPatrolPauseCountdown <= 0f)
                    {
                        patrolPauseTimer = Random.Range(1.1f, 1.8f);
                        nextPatrolPauseCountdown = Random.Range(6.5f, 10.5f);
                        break;
                    }

                    patrolAngle += patrolAngularSpeed * Time.deltaTime;
                    float safePatrolR = Mathf.Max(patrolRadius, 2.60f);
                    Vector3 rawCirclePos = new Vector3(
                        Mathf.Cos(patrolAngle) * safePatrolR,
                        0f,
                        Mathf.Sin(patrolAngle) * safePatrolR
                    );
                    Vector3 targetCirclePos = ClampWalkablePosition(rawCirclePos);

                    float cycle = Time.time * 5.8f;
                    float walkBob = Mathf.Abs(Mathf.Sin(cycle)) * 0.036f;
                    Vector3 nextFlat = Vector3.Lerp(
                        new Vector3(transform.position.x, 0f, transform.position.z),
                        targetCirclePos,
                        Time.deltaTime * 6f
                    );
                    nextFlat = ClampWalkablePosition(nextFlat);
                    transform.position = new Vector3(nextFlat.x, walkBob, nextFlat.z);

                    Vector3 tangent = new Vector3(-Mathf.Sin(patrolAngle), 0f, Mathf.Cos(patrolAngle));
                    Vector3 toCenter = (Vector3.zero - new Vector3(transform.position.x, 0f, transform.position.z)).normalized;
                    if (toCenter.sqrMagnitude < 0.001f) toCenter = Vector3.forward;
                    Vector3 lookDir = Vector3.Slerp(tangent, toCenter, 0.38f).normalized;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up)
                            * Quaternion.Euler(0f, 0f, Mathf.Sin(cycle * 0.5f) * 2.0f);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 7f);
                    }

                    if (!SampleAndRetargetClip(walkClip, Time.time * 1.05f, true, 0f, Time.deltaTime * 12f))
                    {
                        AnimateArticulatedPose(ButcherIdleMode.Patrolling, cycle);
                    }
                    UpdateAnimatorParameters(true, 1.0f, false);

                    nextTauntTimer -= Time.deltaTime;
                    if (nextTauntTimer <= 0f)
                    {
                        nextTauntTimer = Random.Range(5.0f, 8.5f);
                        TryStartTaunt();
                    }
                    break;
                }

                case ButcherIdleMode.WalkingToTaunt:
                {
                    Vector3 flatCurrent = new Vector3(transform.position.x, 0f, transform.position.z);
                    Vector3 flatTarget = ClampWalkablePosition(new Vector3(tauntTargetPos.x, 0f, tauntTargetPos.z));
                    tauntTargetPos = flatTarget;

                    float cycle = Time.time * 8.2f;
                    float walkBob = Mathf.Abs(Mathf.Sin(cycle)) * 0.042f;
                    Vector3 nextFlat = MoveAlongSafeRing(flatCurrent, flatTarget, 3.1f, Time.deltaTime, out Vector3 moveDir, out float dist);
                    transform.position = new Vector3(nextFlat.x, walkBob, nextFlat.z);

                    if (moveDir.sqrMagnitude > 0.001f)
                    {
                        Quaternion walkRot = Quaternion.LookRotation(moveDir, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, walkRot, Time.deltaTime * 9f);
                    }

                    if (!SampleAndRetargetClip(walkClip, Time.time * 1.35f, true, 2f, Time.deltaTime * 14f))
                    {
                        AnimateArticulatedPose(ButcherIdleMode.WalkingToTaunt, cycle);
                    }
                    UpdateAnimatorParameters(true, 1.4f, false);

                    float angDiff = Mathf.Abs(Mathf.DeltaAngle(
                        Mathf.Atan2(nextFlat.z, nextFlat.x) * Mathf.Rad2Deg,
                        Mathf.Atan2(flatTarget.z, flatTarget.x) * Mathf.Rad2Deg));

                    if (dist < 0.22f || angDiff < 5.5f)
                    {
                        idleMode = ButcherIdleMode.Taunting;
                        activeTauntClip = (Random.value < 0.5f && taunt1Clip != null)
                            ? taunt1Clip
                            : (taunt2Clip != null ? taunt2Clip : taunt1Clip);
                        tauntClipElapsed = 0f;
                        tauntStateTimer = activeTauntClip != null ? Mathf.Clamp(activeTauntClip.length, 2.0f, 3.4f) : 2.4f;
                    }
                    break;
                }

                case ButcherIdleMode.Taunting:
                {
                    tauntStateTimer -= Time.deltaTime;
                    tauntClipElapsed += Time.deltaTime;

                    Vector3 lookVec = tauntLookTarget - transform.position;
                    lookVec.y = 0f;
                    if (lookVec.sqrMagnitude > 0.001f)
                    {
                        Quaternion baseLook = Quaternion.LookRotation(lookVec.normalized, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, baseLook, Time.deltaTime * 10f);
                    }

                    Vector3 safeTauntFlat = ClampWalkablePosition(tauntTargetPos);
                    float laughShake = Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 0.024f;
                    transform.position = new Vector3(safeTauntFlat.x, laughShake, safeTauntFlat.z);

                    AnimationClip clipToPlay = activeTauntClip != null ? activeTauntClip : (taunt1Clip != null ? taunt1Clip : taunt2Clip);
                    float laughJaw = Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 18f;
                    if (!SampleAndRetargetClip(clipToPlay, tauntClipElapsed, true, laughJaw, Time.deltaTime * 14f))
                    {
                        AnimateArticulatedPose(ButcherIdleMode.Taunting, Time.time);
                    }
                    UpdateAnimatorParameters(false, 0f, true);

                    if (tauntStateTimer <= 0f)
                    {
                        patrolAngle = Mathf.Atan2(transform.position.z, transform.position.x);
                        idleMode = ButcherIdleMode.Patrolling;
                    }
                    break;
                }
            }
        }

        private void AnimateArticulatedPose(ButcherIdleMode mode, float phase)
        {
            float dt = Time.deltaTime * 10f;

            if (pivotTorso == null)
            {
                // Yedek eski kapsül kol hareketi
                if (weaponArmPivot != null)
                {
                    if (mode == ButcherIdleMode.Taunting)
                    {
                        weaponArmPivot.localRotation = Quaternion.Euler(
                            -55f + Mathf.Sin(phase * 13f) * 22f,
                            Mathf.Sin(phase * 6.5f) * 14f,
                            0f
                        );
                    }
                    else
                    {
                        weaponArmPivot.localRotation = Quaternion.Slerp(
                            weaponArmPivot.localRotation,
                            Quaternion.Euler(-24f + Mathf.Sin(phase) * 10f, 0f, 0f),
                            dt
                        );
                    }
                }
                return;
            }

            switch (mode)
            {
                case ButcherIdleMode.Patrolling:
                {
                    float legSwing = Mathf.Sin(phase) * 24f;
                    if (pivotRightLeg != null)
                        pivotRightLeg.localRotation = Quaternion.Slerp(pivotRightLeg.localRotation, Quaternion.Euler(legSwing, 0f, 0f), dt);
                    if (pivotLeftLeg != null)
                        pivotLeftLeg.localRotation = Quaternion.Slerp(pivotLeftLeg.localRotation, Quaternion.Euler(-legSwing, 0f, 0f), dt);

                    pivotTorso.localRotation = Quaternion.Slerp(
                        pivotTorso.localRotation,
                        Quaternion.Euler(6f + Mathf.Abs(Mathf.Sin(phase)) * 3f, Mathf.Sin(phase) * 5f, Mathf.Cos(phase) * 2.5f),
                        dt
                    );

                    if (pivotHead != null)
                    {
                        float headYaw = Mathf.Sin(Time.time * 2.1f) * 14f;
                        float headRoll = Mathf.Cos(Time.time * 1.7f) * 5f;
                        pivotHead.localRotation = Quaternion.Slerp(
                            pivotHead.localRotation,
                            Quaternion.Euler(4f, headYaw, headRoll),
                            dt
                        );
                    }

                    if (pivotJaw != null)
                    {
                        float jawAngle = 2f + Mathf.Abs(Mathf.Sin(Time.time * 3.2f)) * 4f;
                        pivotJaw.localRotation = Quaternion.Slerp(pivotJaw.localRotation, Quaternion.Euler(jawAngle, 0f, 0f), dt);
                    }

                    if (pivotLeftShoulder != null)
                        pivotLeftShoulder.localRotation = Quaternion.Slerp(pivotLeftShoulder.localRotation, Quaternion.Euler(-Mathf.Sin(phase) * 18f, 0f, -4f), dt);
                    if (pivotLeftElbow != null)
                        pivotLeftElbow.localRotation = Quaternion.Slerp(pivotLeftElbow.localRotation, Quaternion.Euler(-18f - Mathf.Max(0f, -Mathf.Sin(phase)) * 12f, 0f, 0f), dt);

                    if (pivotRightShoulder != null)
                        pivotRightShoulder.localRotation = Quaternion.Slerp(pivotRightShoulder.localRotation, Quaternion.Euler(-24f + Mathf.Sin(phase) * 10f, -6f, 5f), dt);
                    if (pivotRightElbow != null)
                        pivotRightElbow.localRotation = Quaternion.Slerp(pivotRightElbow.localRotation, Quaternion.Euler(-32f + Mathf.Cos(phase) * 8f, 0f, 0f), dt);
                    break;
                }

                case ButcherIdleMode.WalkingToTaunt:
                {
                    float legSwing = Mathf.Sin(phase) * 28f;
                    if (pivotRightLeg != null)
                        pivotRightLeg.localRotation = Quaternion.Slerp(pivotRightLeg.localRotation, Quaternion.Euler(legSwing, 0f, 0f), dt);
                    if (pivotLeftLeg != null)
                        pivotLeftLeg.localRotation = Quaternion.Slerp(pivotLeftLeg.localRotation, Quaternion.Euler(-legSwing, 0f, 0f), dt);

                    pivotTorso.localRotation = Quaternion.Slerp(
                        pivotTorso.localRotation,
                        Quaternion.Euler(10f + Mathf.Abs(Mathf.Sin(phase)) * 4f, Mathf.Sin(phase) * 6f, 0f),
                        dt
                    );

                    if (pivotHead != null)
                        pivotHead.localRotation = Quaternion.Slerp(pivotHead.localRotation, Quaternion.Euler(5f, Mathf.Sin(Time.time * 4f) * 6f, 5f), dt);
                    if (pivotJaw != null)
                        pivotJaw.localRotation = Quaternion.Slerp(pivotJaw.localRotation, Quaternion.Euler(5f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 6f, 0f, 0f), dt);

                    if (pivotLeftShoulder != null)
                        pivotLeftShoulder.localRotation = Quaternion.Slerp(pivotLeftShoulder.localRotation, Quaternion.Euler(-Mathf.Sin(phase) * 22f, 0f, -5f), dt);
                    if (pivotLeftElbow != null)
                        pivotLeftElbow.localRotation = Quaternion.Slerp(pivotLeftElbow.localRotation, Quaternion.Euler(-24f, 0f, 0f), dt);

                    if (pivotRightShoulder != null)
                        pivotRightShoulder.localRotation = Quaternion.Slerp(pivotRightShoulder.localRotation, Quaternion.Euler(-36f + Mathf.Sin(phase) * 12f, -8f, 6f), dt);
                    if (pivotRightElbow != null)
                        pivotRightElbow.localRotation = Quaternion.Slerp(pivotRightElbow.localRotation, Quaternion.Euler(-38f + Mathf.Cos(phase) * 10f, 0f, 0f), dt);
                    break;
                }

                case ButcherIdleMode.Taunting:
                {
                    float t = phase;
                    if (pivotRightLeg != null)
                        pivotRightLeg.localRotation = Quaternion.Slerp(pivotRightLeg.localRotation, Quaternion.Euler(-4f, 0f, 3f), dt);
                    if (pivotLeftLeg != null)
                        pivotLeftLeg.localRotation = Quaternion.Slerp(pivotLeftLeg.localRotation, Quaternion.Euler(4f, 0f, -3f), dt);

                    // Gövde oyuncuya doğru alaycı şekilde eğilir ve kahkahayla sarsılır
                    pivotTorso.localRotation = Quaternion.Slerp(
                        pivotTorso.localRotation,
                        Quaternion.Euler(16f + Mathf.Sin(t * 6f) * 5f, Mathf.Sin(t * 4.5f) * 8f, Mathf.Sin(t * 5f) * 4f),
                        dt
                    );

                    // Baş alaycı şekilde yana yatar
                    if (pivotHead != null)
                    {
                        pivotHead.localRotation = Quaternion.Slerp(
                            pivotHead.localRotation,
                            Quaternion.Euler(8f + Mathf.Sin(t * 12f) * 5f, Mathf.Sin(t * 4f) * 10f, Mathf.Sin(t * 5f) * 14f),
                            dt
                        );
                    }

                    // Çene kahkaha atarmış gibi açılıp kapanır
                    if (pivotJaw != null)
                    {
                        float laughJaw = Mathf.Abs(Mathf.Sin(t * 14f)) * 18f;
                        pivotJaw.localRotation = Quaternion.Euler(laughJaw, 0f, 0f);
                    }

                    // Sol kol oyuncunun ellerini işaret eder
                    if (pivotLeftShoulder != null)
                        pivotLeftShoulder.localRotation = Quaternion.Slerp(pivotLeftShoulder.localRotation, Quaternion.Euler(-42f + Mathf.Sin(t * 8f) * 10f, 15f, -8f), dt);
                    if (pivotLeftElbow != null)
                        pivotLeftElbow.localRotation = Quaternion.Slerp(pivotLeftElbow.localRotation, Quaternion.Euler(-28f + Mathf.Cos(t * 8f) * 12f, 0f, 0f), dt);

                    // Sağ kol kanlı satırı oyuncunun ellerine doğru sallayarak tehdit eder
                    if (pivotRightShoulder != null)
                        pivotRightShoulder.localRotation = Quaternion.Euler(-58f + Mathf.Sin(t * 13f) * 22f, Mathf.Sin(t * 6.5f) * 14f, 8f);
                    if (pivotRightElbow != null)
                        pivotRightElbow.localRotation = Quaternion.Euler(-35f + Mathf.Cos(t * 13f) * 18f, 0f, 0f);
                    break;
                }
            }
        }

        private void UpdateAnimatorParameters(bool isWalking, float moveSpeed, bool isTaunting, bool triggerAttack = false)
        {
            if (butcherAnimator == null || butcherAnimator.runtimeAnimatorController == null) return;

            AnimatorControllerParameter[] parameters = butcherAnimator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                string pName = parameters[i].name;
                AnimatorControllerParameterType pType = parameters[i].type;

                if (pName == "IsWalking" && pType == AnimatorControllerParameterType.Bool)
                    butcherAnimator.SetBool("IsWalking", isWalking);
                else if (pName == "MoveSpeed" && pType == AnimatorControllerParameterType.Float)
                    butcherAnimator.SetFloat("MoveSpeed", moveSpeed);
                else if (pName == "Taunt" && pType == AnimatorControllerParameterType.Bool)
                    butcherAnimator.SetBool("Taunt", isTaunting);
                else if (triggerAttack && pName == "Attack" && pType == AnimatorControllerParameterType.Trigger)
                    butcherAnimator.SetTrigger("Attack");
            }
        }

        private void TryStartTaunt()
        {
            Vector3 seatPos = Vector3.zero;
            var seats = Object.FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None);
            if (seats != null && seats.Length > 0)
            {
                PlayerSeat picked = seats[Random.Range(0, seats.Length)];
                if (picked != null)
                {
                    seatPos = picked.transform.position;
                }
            }

            if (seatPos.sqrMagnitude < 0.01f)
            {
                Vector3[] fallbackSeats = new Vector3[]
                {
                    new Vector3(0f, 0f, -1.65f),
                    new Vector3(-1.65f, 0f, 0f),
                    new Vector3(0f, 0f, 1.65f),
                    new Vector3(1.65f, 0f, 0f)
                };
                seatPos = fallbackSeats[Random.Range(0, fallbackSeats.Length)];
            }

            Vector3 seatDir = new Vector3(seatPos.x, 0f, seatPos.z).normalized;
            if (seatDir.sqrMagnitude < 0.001f) seatDir = Vector3.back;

            Vector3 rawTauntPos = seatDir * 2.05f + Vector3.Cross(Vector3.up, seatDir) * 0.72f;
            tauntTargetPos = ClampWalkablePosition(rawTauntPos);
            tauntLookTarget = seatPos + Vector3.up * 0.9f;
            idleMode = ButcherIdleMode.WalkingToTaunt;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null) return;
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                if (child != null) SetLayerRecursively(child.gameObject, layer);
            }
        }

        /// <summary>
        /// Kasabı kurbanın yanına yürütür ve doğrudan görünür şekilde satırla el kesim sekansını başlatır.
        /// </summary>
        public void AppearAt(Transform spawnAnchor, int victimSeatIndex = 0, int weaponIndex = -1, System.Action onStrikeCallback = null)
        {
            DestroyGhostPreview();
            EnsureKasapModelSetup();
            SetLayerRecursively(gameObject, 0);

            currentWeaponIndex = (weaponIndex >= 0 && weaponIndex < 4) ? weaponIndex : Random.Range(0, 4);
            SelectWeapon(currentWeaponIndex);

            if (visualRoot != null)
            {
                visualRoot.SetActive(true);
            }

            isStriking = true;

            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(SmashAnimationRoutine(spawnAnchor, victimSeatIndex, currentWeaponIndex, onStrikeCallback));
        }

        /// <summary>
        /// kasap.fbx rigindeki Avuc_Sag (yoksa Pivot_RightElbow veya weaponArmPivot) altında WeaponSocket Transform'unu bulur veya oluşturur.
        /// </summary>
        public Transform GetOrCreateWeaponSocket()
        {
            if (weaponSocket != null) return weaponSocket;
            weaponSocket = GetOrCreateWeaponSocketOn(kasapInstanceRoot, pivotRightElbow, weaponArmPivot != null ? weaponArmPivot : transform);
            return weaponSocket;
        }

        private Transform GetOrCreateWeaponSocketOn(Transform modelRoot, Transform fallbackElbow, Transform fallbackPivot)
        {
            Transform parent = modelRoot != null ? FindDeepChild(modelRoot, "Avuc_Sag") : null;
            if (parent == null) parent = fallbackElbow;
            if (parent == null) parent = fallbackPivot;
            if (parent == null) parent = modelRoot != null ? modelRoot : transform;

            Transform existing = parent.Find("WeaponSocket");
            if (existing != null)
            {
                return existing;
            }

            GameObject socketGo = new GameObject("WeaponSocket");
            Transform socket = socketGo.transform;
            socket.SetParent(parent, false);
            socket.localPosition = Vector3.zero;
            socket.localRotation = Quaternion.identity;
            socket.localScale = Vector3.one;
            return socket;
        }

        private void SetBuiltInCleaverVisible(Transform modelRoot, bool visible)
        {
            if (modelRoot == null) return;
            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r != null && r.gameObject.name.StartsWith("Satir_"))
                {
                    r.enabled = visible;
                }
            }
        }

        private static void ClearWeaponSocket(Transform socket, ref GameObject trackedInstance)
        {
            trackedInstance = null;
            if (socket == null) return;

            for (int i = socket.childCount - 1; i >= 0; i--)
            {
                Transform child = socket.GetChild(i);
                if (child == null) continue;
                child.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        /// <summary>
        /// Seçilen WeaponData'yı WeaponSocket altında kuşanır. Çift silahı önlemek için mevcut silahı temizler ve yerleşik Satir_* meshlerini gizler.
        /// </summary>
        public void EquipWeaponData(WeaponData data)
        {
            if (kasapInstanceRoot == null)
            {
                EnsureKasapModelSetup();
            }

            Transform socket = GetOrCreateWeaponSocket();
            EquipWeaponOnSocket(socket, ref equippedWeaponInstance, kasapInstanceRoot, data);
        }

        private void EquipWeaponOnSocket(Transform socket, ref GameObject trackedInstance, Transform modelRoot, WeaponData data)
        {
            ClearWeaponSocket(socket, ref trackedInstance);
            if (socket == null) return;

            if (data == null)
            {
                SetBuiltInCleaverVisible(modelRoot, true);
                return;
            }

            SetBuiltInCleaverVisible(modelRoot, false);

            GameObject weaponObj = data.weaponPrefab != null
                ? Instantiate(data.weaponPrefab, socket, false)
                : CreatePrimitiveWeaponPlaceholder(data.weaponType, socket);

            weaponObj.transform.localPosition = data.localPositionOffset;
            weaponObj.transform.localRotation = Quaternion.Euler(data.localEulerOffset);
            weaponObj.transform.localScale = data.localScale == Vector3.zero ? Vector3.one : data.localScale;
            trackedInstance = weaponObj;
        }

        private GameObject CreatePrimitiveWeaponPlaceholder(ExecutionWeaponType type, Transform parent)
        {
            GameObject root = new GameObject($"PlaceholderWeapon_{type}");
            root.transform.SetParent(parent, false);

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            GameObject AddPart(PrimitiveType primitive, string partName, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Color color, float smoothness = 0.45f, float metallic = 0.0f)
            {
                GameObject part = GameObject.CreatePrimitive(primitive);
                part.name = partName;
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = localPos;
                part.transform.localRotation = Quaternion.Euler(localEuler);
                part.transform.localScale = localScale;

                Collider col = part.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }

                Renderer rend = part.GetComponent<Renderer>();
                if (rend != null && litShader != null)
                {
                    Material mat = new Material(litShader);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                    rend.sharedMaterial = mat;
                }

                return part;
            }

            switch (type)
            {
                case ExecutionWeaponType.GiantMeatMallet:
                    AddPart(PrimitiveType.Cylinder, "MalletHandle", new Vector3(0f, 0.22f, 0f), Vector3.zero, new Vector3(0.055f, 0.24f, 0.055f), new Color(0.42f, 0.22f, 0.10f));
                    AddPart(PrimitiveType.Cylinder, "MalletHead", new Vector3(0f, 0.46f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.22f, 0.20f, 0.22f), new Color(0.88f, 0.16f, 0.16f), 0.55f);
                    AddPart(PrimitiveType.Cylinder, "MalletBand", new Vector3(0f, 0.46f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.23f, 0.04f, 0.23f), new Color(1f, 0.85f, 0.15f), 0.7f, 0.2f);
                    break;

                case ExecutionWeaponType.FlyingCastIronPan:
                    AddPart(PrimitiveType.Cube, "PanHandle", new Vector3(0f, 0.16f, 0f), Vector3.zero, new Vector3(0.05f, 0.30f, 0.03f), new Color(0.14f, 0.14f, 0.16f), 0.6f, 0.75f);
                    AddPart(PrimitiveType.Cylinder, "PanBody", new Vector3(0f, 0.42f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.34f, 0.045f, 0.34f), new Color(0.10f, 0.10f, 0.12f), 0.65f, 0.85f);
                    AddPart(PrimitiveType.Cylinder, "EggWhite", new Vector3(0f, 0.42f, 0.05f), new Vector3(90f, 0f, 0f), new Vector3(0.16f, 0.012f, 0.16f), Color.white, 0.8f);
                    AddPart(PrimitiveType.Sphere, "EggYolk", new Vector3(0f, 0.42f, 0.065f), Vector3.zero, new Vector3(0.07f, 0.07f, 0.04f), new Color(1f, 0.78f, 0.08f), 0.85f);
                    break;

                case ExecutionWeaponType.SausageWhip:
                    AddPart(PrimitiveType.Cylinder, "WhipHandle", new Vector3(0f, 0.10f, 0f), Vector3.zero, new Vector3(0.05f, 0.12f, 0.05f), new Color(0.25f, 0.15f, 0.08f));
                    for (int i = 0; i < 4; i++)
                    {
                        float y = 0.25f + i * 0.14f;
                        float z = Mathf.Sin(i * 0.8f) * 0.05f;
                        AddPart(PrimitiveType.Capsule, $"SausageLink_{i}", new Vector3(0f, y, z), new Vector3(i * 14f, 0f, (i % 2 == 0 ? 12f : -12f)), new Vector3(0.085f, 0.085f, 0.085f), new Color(0.88f, 0.32f, 0.14f), 0.6f);
                    }
                    AddPart(PrimitiveType.Sphere, "MustardTip", new Vector3(0f, 0.76f, 0.04f), Vector3.zero, new Vector3(0.06f, 0.06f, 0.06f), new Color(1f, 0.82f, 0.1f), 0.75f);
                    break;

                case ExecutionWeaponType.CreamPieLauncher:
                default:
                    AddPart(PrimitiveType.Cylinder, "PieTin", new Vector3(0f, 0.10f, 0f), Vector3.zero, new Vector3(0.32f, 0.03f, 0.32f), new Color(0.78f, 0.80f, 0.84f), 0.85f, 0.85f);
                    AddPart(PrimitiveType.Cylinder, "PieCrust", new Vector3(0f, 0.135f, 0f), Vector3.zero, new Vector3(0.29f, 0.025f, 0.29f), new Color(0.86f, 0.64f, 0.32f), 0.35f);
                    AddPart(PrimitiveType.Sphere, "CreamMound", new Vector3(0f, 0.17f, 0f), Vector3.zero, new Vector3(0.26f, 0.14f, 0.26f), new Color(1f, 0.98f, 0.94f), 0.7f);
                    AddPart(PrimitiveType.Sphere, "PieCherry", new Vector3(0f, 0.25f, 0f), Vector3.zero, new Vector3(0.065f, 0.065f, 0.065f), new Color(0.92f, 0.08f, 0.15f), 0.9f);
                    break;
            }

            return root;
        }

        private void SelectWeapon(int index)
        {
            currentWeaponIndex = Mathf.Clamp(index, 0, 3);

            if (weaponModels != null && weaponModels.Length > 0)
            {
                bool useFallbackWeapons = (kasapInstanceRoot == null);
                for (int i = 0; i < weaponModels.Length; i++)
                {
                    if (weaponModels[i] != null)
                    {
                        weaponModels[i].SetActive(useFallbackWeapons && i == currentWeaponIndex);
                    }
                }
            }

            WeaponData data = ExecutionWeaponData.GetWeaponData((ExecutionWeaponType)currentWeaponIndex);
            EquipWeaponData(data);
        }

        /// <summary>
        /// Hayalet oyunculara özel (GhostOnly Layer 11) oda köşesi kasap önizleme klonunu oluşturur veya günceller.
        /// NetworkObject içermez ve yaşayan oyuncular tarafından görülmez.
        /// </summary>
        public void SpawnOrUpdateGhostPreview(ExecutionWeaponType weapon, Vector3 cornerPos, Quaternion cornerRot)
        {
            int ghostLayer = LayerMask.NameToLayer("GhostOnly");
            if (ghostLayer < 0) ghostLayer = 11;

            if (ghostPreviewRoot == null)
            {
                ghostPreviewRoot = new GameObject("ButcherGhostPreview");
                ghostPreviewRoot.transform.SetPositionAndRotation(cornerPos, cornerRot);

                if (customButcherModelPrefab != null)
                {
                    GameObject model = Instantiate(customButcherModelPrefab, ghostPreviewRoot.transform);
                    model.name = "KasapGhostPreviewModel";
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.identity;
                    model.transform.localScale = kasapInstanceRoot != null ? kasapInstanceRoot.localScale : new Vector3(0.74f, 0.74f, 0.74f);
                    ghostPreviewModelRoot = model.transform;

                    // Güvenlik: Prefab üzerinde ağ bileşeni varsa temizle
                    MonoBehaviour[] behaviours = model.GetComponentsInChildren<MonoBehaviour>(true);
                    for (int i = 0; i < behaviours.Length; i++)
                    {
                        if (behaviours[i] != null && behaviours[i].GetType().Name == "NetworkObject")
                        {
                            if (Application.isPlaying) Destroy(behaviours[i]);
                            else DestroyImmediate(behaviours[i]);
                        }
                    }

                    DisableEmbeddedLightsAndCameras(ghostPreviewModelRoot);
                    ApplyRealisticButcherMaterials(ghostPreviewModelRoot);

                    Animator previewAnim = ghostPreviewModelRoot.GetComponentInChildren<Animator>(true);
                    if (previewAnim != null && butcherAnimatorController != null)
                    {
                        previewAnim.runtimeAnimatorController = butcherAnimatorController;
                        previewAnim.enabled = true;
                    }
                }
                else
                {
                    GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    fallback.name = "KasapGhostPreviewFallback";
                    fallback.transform.SetParent(ghostPreviewRoot.transform, false);
                    fallback.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                    fallback.transform.localScale = new Vector3(0.45f, 0.7f, 0.45f);
                    Collider col = fallback.GetComponent<Collider>();
                    if (col != null)
                    {
                        if (Application.isPlaying) Destroy(col);
                        else DestroyImmediate(col);
                    }
                    ghostPreviewModelRoot = fallback.transform;
                }

                ghostPreviewWeaponSocket = GetOrCreateWeaponSocketOn(ghostPreviewModelRoot, null, ghostPreviewModelRoot);

                if (Application.isPlaying)
                {
                    if (ghostPreviewIdleRoutine != null) StopCoroutine(ghostPreviewIdleRoutine);
                    ghostPreviewIdleRoutine = StartCoroutine(GhostPreviewIdleRoutine());
                }
            }
            else
            {
                ghostPreviewRoot.transform.SetPositionAndRotation(cornerPos, cornerRot);
                ghostPreviewRoot.SetActive(true);
            }

            WeaponData data = ExecutionWeaponData.GetWeaponData(weapon);
            EquipWeaponOnSocket(ghostPreviewWeaponSocket, ref ghostPreviewWeaponInstance, ghostPreviewModelRoot, data);
            SetLayerRecursively(ghostPreviewRoot, ghostLayer);
        }

        private IEnumerator GhostPreviewIdleRoutine()
        {
            while (ghostPreviewRoot != null)
            {
                if (ghostPreviewModelRoot != null)
                {
                    float bob = Mathf.Sin(Time.time * 2.4f) * 0.02f;
                    float sway = Mathf.Sin(Time.time * 1.6f) * 3.5f;
                    ghostPreviewModelRoot.localPosition = new Vector3(0f, bob, 0f);
                    ghostPreviewModelRoot.localRotation = Quaternion.Euler(0f, sway, 0f);
                }
                yield return null;
            }
            ghostPreviewIdleRoutine = null;
        }

        /// <summary>
        /// Hayalet önizleme klonunu tamamen yok eder (infaz başlangıcı, Replay veya Ana Menü dönüşünde çağrılır).
        /// </summary>
        public void DestroyGhostPreview()
        {
            if (ghostPreviewIdleRoutine != null)
            {
                StopCoroutine(ghostPreviewIdleRoutine);
                ghostPreviewIdleRoutine = null;
            }

            if (ghostPreviewRoot != null)
            {
                ghostPreviewRoot.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(ghostPreviewRoot);
                }
                else
                {
                    DestroyImmediate(ghostPreviewRoot);
                }
            }

            ghostPreviewRoot = null;
            ghostPreviewModelRoot = null;
            ghostPreviewWeaponSocket = null;
            ghostPreviewWeaponInstance = null;
        }

        /// <summary>
        /// Silaha özel hit sesi, renkli VFX partikül patlaması ve komik kurban tepkisini tetikler.
        /// </summary>
        public void PlayWeaponExecutionEffects(ExecutionWeaponType weapon, Vector3 targetPos)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayButcherWeaponHit(weapon, targetPos);
            }

            WeaponData data = ExecutionWeaponData.GetWeaponData(weapon);
            Color fxColor = data != null ? data.vfxColor : Color.white;

            if (CartoonFXManager.Instance != null)
            {
                CartoonFXManager.Instance.PlayButcherStrikeFX(targetPos);
                CartoonFXManager.Instance.PlayConfetti(targetPos + Vector3.up * 0.35f, fxColor);
            }

            SpawnWeaponColorBurstVFX(targetPos, fxColor);
            TriggerNearestVictimReaction(weapon, targetPos);
        }

        private void SpawnWeaponColorBurstVFX(Vector3 pos, Color color)
        {
            GameObject burstGo = new GameObject("WeaponExecutionBurstVFX");
            burstGo.transform.position = pos + Vector3.up * 0.2f;

            ParticleSystem ps = burstGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 5.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = color;
            main.gravityModifier = 0.9f;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.enabled = true;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 24) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            ps.Play();
        }

        private void TriggerNearestVictimReaction(ExecutionWeaponType weapon, Vector3 targetPos)
        {
            var seats = Object.FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None);
            if (seats == null || seats.Length == 0) return;

            PlayerSeat nearest = null;
            float bestDistSq = 6.25f; // 2.5m eşik
            Vector2 targetFlat = new Vector2(targetPos.x, targetPos.z);

            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i] == null) continue;
                Vector3 sp = seats[i].transform.position;
                float distSq = (new Vector2(sp.x, sp.z) - targetFlat).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    nearest = seats[i];
                }
            }

            if (nearest != null)
            {
                CharacterAnimatorDriver driver = nearest.GetComponentInChildren<CharacterAnimatorDriver>(true);
                if (driver != null)
                {
                    driver.PlayEliminationByWeapon((int)weapon);
                }
            }
        }

        public void Hide()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            if (ExecutionReflectionSystem.Instance != null && ExecutionReflectionSystem.Instance.IsExecutionActive)
            {
                ExecutionReflectionSystem.Instance.EndExecutionSequence();
            }

            ResumePatrol();
        }

        private IEnumerator SmashAnimationRoutine(Transform spawnPoint, int victimSeatIndex, int weaponIdx, System.Action onStrikeCallback)
        {
            ExecutionWeaponData weaponData = ExecutionWeaponData.GetData((ExecutionWeaponType)weaponIdx);

            // 1. AŞAMA: Kurbanın yanına masa ve oyuncuların etrafından (polar yörüngeyle) yürüme (0.55f sn)
            Vector3 startWalkPos = ClampWalkablePosition(new Vector3(transform.position.x, 0f, transform.position.z));
            Vector3 strikePos = startWalkPos;
            Vector3 lookTarget = new Vector3(0f, 0.78f, 0f);

            if (spawnPoint != null)
            {
                strikePos = ClampWalkablePosition(spawnPoint.position + spawnPoint.right * 0.78f);
                Vector3 handsCenter = Vector3.Lerp(new Vector3(spawnPoint.position.x, 0f, spawnPoint.position.z), Vector3.zero, 0.55f);
                lookTarget = new Vector3(handsCenter.x, 0.78f, handsCenter.z);
            }

            Vector3 toVictimHands = lookTarget - strikePos;
            toVictimHands.y = 0f;
            Quaternion strikeRot = toVictimHands.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toVictimHands.normalized, Vector3.up)
                : (spawnPoint != null ? spawnPoint.rotation : transform.rotation);

            float walkDuration = 0.55f;
            float walkElapsed = 0f;
            Vector3 prevFlatPos = startWalkPos;

            UpdateAnimatorParameters(true, 2.0f, false);

            while (walkElapsed < walkDuration)
            {
                walkElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(walkElapsed / walkDuration);
                float smoothT = t * t * (3f - 2f * t);
                float cycle = Time.time * 11f;
                float walkBob = Mathf.Abs(Mathf.Sin(cycle)) * 0.048f;

                float startAngDeg = Mathf.Atan2(startWalkPos.z, startWalkPos.x) * Mathf.Rad2Deg;
                float endAngDeg = Mathf.Atan2(strikePos.z, strikePos.x) * Mathf.Rad2Deg;
                float curAngRad = Mathf.LerpAngle(startAngDeg, endAngDeg, smoothT) * Mathf.Deg2Rad;
                float startR = new Vector2(startWalkPos.x, startWalkPos.z).magnitude;
                float endR = new Vector2(strikePos.x, strikePos.z).magnitude;
                float baseR = Mathf.Lerp(startR, endR, smoothT);
                float safeArcR = Mathf.Max(baseR, Mathf.Lerp(baseR, 2.70f, Mathf.Sin(smoothT * Mathf.PI)));
                Vector3 rawFlat = new Vector3(Mathf.Cos(curAngRad) * safeArcR, 0f, Mathf.Sin(curAngRad) * safeArcR);
                Vector3 flatPos = ClampWalkablePosition(rawFlat);

                transform.position = new Vector3(flatPos.x, walkBob, flatPos.z);

                Vector3 moveStep = flatPos - prevFlatPos;
                moveStep.y = 0f;
                prevFlatPos = flatPos;

                if (t < 0.75f && moveStep.sqrMagnitude > 0.00001f)
                {
                    Quaternion arcLook = Quaternion.LookRotation(moveStep.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, arcLook, Time.deltaTime * 14f);
                }
                else
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, strikeRot, Time.deltaTime * 16f);
                }

                if (!SampleAndRetargetClip(walkClip, Time.time * 1.65f, true, 6f, 0.9f))
                {
                    float legSwing = Mathf.Sin(cycle) * 32f;
                    if (pivotRightLeg != null) pivotRightLeg.localRotation = Quaternion.Euler(legSwing, 0f, 0f);
                    if (pivotLeftLeg != null) pivotLeftLeg.localRotation = Quaternion.Euler(-legSwing, 0f, 0f);
                    if (pivotTorso != null) pivotTorso.localRotation = Quaternion.Slerp(pivotTorso.localRotation, Quaternion.Euler(12f, 0f, 0f), Time.deltaTime * 12f);
                    if (pivotHead != null) pivotHead.localRotation = Quaternion.Slerp(pivotHead.localRotation, Quaternion.Euler(10f, 0f, 0f), Time.deltaTime * 12f);
                    if (pivotJaw != null) pivotJaw.localRotation = Quaternion.Slerp(pivotJaw.localRotation, Quaternion.Euler(8f, 0f, 0f), Time.deltaTime * 12f);

                    if (pivotRightShoulder != null)
                    {
                        pivotRightShoulder.localRotation = Quaternion.Slerp(
                            pivotRightShoulder.localRotation,
                            Quaternion.Euler(-45f, -6f, 6f),
                            Time.deltaTime * 12f
                        );
                    }
                    else if (weaponArmPivot != null)
                    {
                        weaponArmPivot.localRotation = Quaternion.Slerp(
                            weaponArmPivot.localRotation,
                            Quaternion.Euler(-40f, 0f, 0f),
                            Time.deltaTime * 12f
                        );
                    }

                    if (pivotRightElbow != null)
                    {
                        pivotRightElbow.localRotation = Quaternion.Slerp(
                            pivotRightElbow.localRotation,
                            Quaternion.Euler(-40f, 0f, 0f),
                            Time.deltaTime * 12f
                        );
                    }
                }

                yield return null;
            }

            transform.position = strikePos;
            transform.rotation = strikeRot;

            if (pivotRightLeg != null) pivotRightLeg.localRotation = Quaternion.Euler(-6f, 0f, 3f);
            if (pivotLeftLeg != null) pivotLeftLeg.localRotation = Quaternion.Euler(6f, 0f, -3f);

            if (weaponArmPivot == null && pivotRightShoulder == null)
            {
                onStrikeCallback?.Invoke();
                ResumePatrol();
                yield break;
            }

            UpdateAnimatorParameters(false, 0f, false, true);

            // 2. AŞAMA: Silahı (Satırı) kaldırma / kurma (0.32f sn) - Butcher_Attack klibiyle harmanlanır
            float windupElapsed = 0f;
            float windupDuration = 0.32f;
            float attackClipLen = attackClip != null ? Mathf.Max(0.1f, attackClip.length) : 1.0f;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayButcherGiggle();
            }

            Quaternion torsoStart = pivotTorso != null ? pivotTorso.localRotation : Quaternion.identity;
            Quaternion torsoWindup = Quaternion.Euler(-14f, -8f, 0f);
            Quaternion torsoSmash = Quaternion.Euler(22f, 6f, 0f);

            Quaternion rShoulderStart = pivotRightShoulder != null ? pivotRightShoulder.localRotation : (weaponArmPivot != null ? weaponArmPivot.localRotation : Quaternion.identity);
            Quaternion rShoulderWindup = Quaternion.Euler(-95f, -10f, 8f);
            Quaternion rShoulderSmash = Quaternion.Euler(35f, 4f, -4f);

            Quaternion rElbowStart = pivotRightElbow != null ? pivotRightElbow.localRotation : Quaternion.identity;
            Quaternion rElbowWindup = Quaternion.Euler(-35f, 0f, 0f);
            Quaternion rElbowSmash = Quaternion.Euler(15f, 0f, 0f);

            while (windupElapsed < windupDuration)
            {
                windupElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(windupElapsed / windupDuration);
                float shake = Mathf.Sin(windupElapsed * 42f) * 3.5f;

                bool sampledAttack = SampleAndRetargetClip(attackClip, t * 0.42f * attackClipLen, false, 18f, 0.85f);

                if (pivotTorso != null)
                {
                    pivotTorso.localRotation = sampledAttack
                        ? Quaternion.Slerp(pivotTorso.localRotation, torsoWindup, 0.45f)
                        : Quaternion.Slerp(torsoStart, torsoWindup, t);
                }
                if (!sampledAttack)
                {
                    if (pivotHead != null)
                        pivotHead.localRotation = Quaternion.Slerp(pivotHead.localRotation, Quaternion.Euler(14f, 0f, -4f), t);
                    if (pivotJaw != null)
                        pivotJaw.localRotation = Quaternion.Slerp(pivotJaw.localRotation, Quaternion.Euler(22f, 0f, 0f), t);
                    if (pivotLeftShoulder != null)
                        pivotLeftShoulder.localRotation = Quaternion.Slerp(pivotLeftShoulder.localRotation, Quaternion.Euler(18f, 0f, -10f), t);
                }

                if (pivotRightShoulder != null)
                {
                    Quaternion baseShoulder = Quaternion.Slerp(rShoulderStart, rShoulderWindup, t) * Quaternion.Euler(shake, 0f, 0f);
                    pivotRightShoulder.localRotation = sampledAttack
                        ? Quaternion.Slerp(pivotRightShoulder.localRotation, baseShoulder, 0.72f)
                        : baseShoulder;
                }
                else if (weaponArmPivot != null)
                {
                    weaponArmPivot.localRotation = Quaternion.Slerp(rShoulderStart, rShoulderWindup, t) * Quaternion.Euler(shake, 0f, 0f);
                }

                if (pivotRightElbow != null)
                {
                    Quaternion baseElbow = Quaternion.Slerp(rElbowStart, rElbowWindup, t);
                    pivotRightElbow.localRotation = sampledAttack
                        ? Quaternion.Slerp(pivotRightElbow.localRotation, baseElbow, 0.72f)
                        : baseElbow;
                }

                yield return null;
            }

            // 3. AŞAMA: Keskin indirme / satır kesme darbesi (0.10f sn)
            float smashElapsed = 0f;
            float smashDuration = 0.10f;

            while (smashElapsed < smashDuration)
            {
                smashElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(smashElapsed / smashDuration);
                float fastT = t * t;

                bool sampledAttack = SampleAndRetargetClip(attackClip, (0.42f + fastT * 0.23f) * attackClipLen, false, 6f, 0.85f);

                if (pivotTorso != null)
                {
                    Quaternion targetTorso = Quaternion.Slerp(torsoWindup, torsoSmash, fastT);
                    pivotTorso.localRotation = sampledAttack
                        ? Quaternion.Slerp(pivotTorso.localRotation, targetTorso, 0.65f)
                        : targetTorso;
                }
                if (pivotJaw != null)
                    pivotJaw.localRotation = Quaternion.Slerp(Quaternion.Euler(22f, 0f, 0f), Quaternion.Euler(4f, 0f, 0f), fastT);

                if (pivotRightShoulder != null)
                {
                    Quaternion targetShoulder = Quaternion.Slerp(rShoulderWindup, rShoulderSmash, fastT);
                    pivotRightShoulder.localRotation = sampledAttack
                        ? Quaternion.Slerp(pivotRightShoulder.localRotation, targetShoulder, 0.85f)
                        : targetShoulder;
                }
                else if (weaponArmPivot != null)
                {
                    weaponArmPivot.localRotation = Quaternion.Slerp(rShoulderWindup, rShoulderSmash, fastT);
                }

                if (pivotRightElbow != null)
                {
                    Quaternion targetElbow = Quaternion.Slerp(rElbowWindup, rElbowSmash, fastT);
                    pivotRightElbow.localRotation = sampledAttack
                        ? Quaternion.Slerp(pivotRightElbow.localRotation, targetElbow, 0.85f)
                        : targetElbow;
                }

                yield return null;
            }

            if (pivotTorso != null) pivotTorso.localRotation = torsoSmash;
            if (pivotRightShoulder != null) pivotRightShoulder.localRotation = rShoulderSmash;
            else if (weaponArmPivot != null) weaponArmPivot.localRotation = rShoulderSmash;
            if (pivotRightElbow != null) pivotRightElbow.localRotation = rElbowSmash;

            // 4. TAM DARBE ANI: Silaha özel ses, renkli VFX patlaması, kan birikintisi ve kurban tepkisi
            Vector3 impactPos = cleaverBladeTransform != null
                ? new Vector3(cleaverBladeTransform.position.x, 0.78f, cleaverBladeTransform.position.z)
                : (Vector3.Lerp(transform.position, lookTarget, 0.65f) + Vector3.up * 0.78f);
            impactPos.y = 0.78f;

            PlayWeaponExecutionEffects(weaponData.WeaponType, impactPos);

            onStrikeCallback?.Invoke();

            // 5. AŞAMA: Toparlanma ve devriyeye dönüş (0.75f sn)
            float recoverElapsed = 0f;
            float recoverDur = 0.75f;
            Quaternion recoverShoulderTarget = Quaternion.Euler(-24f, -6f, 5f);
            Quaternion recoverElbowTarget = Quaternion.Euler(-32f, 0f, 0f);
            Quaternion recoverTorsoTarget = Quaternion.Euler(6f, 0f, 0f);

            while (recoverElapsed < recoverDur)
            {
                recoverElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(recoverElapsed / recoverDur);

                if (!SampleAndRetargetClip(attackClip, (0.65f + t * 0.35f) * attackClipLen, false, 2f, 0.75f))
                {
                    if (pivotTorso != null)
                        pivotTorso.localRotation = Quaternion.Slerp(torsoSmash, recoverTorsoTarget, t);
                    if (pivotHead != null)
                        pivotHead.localRotation = Quaternion.Slerp(pivotHead.localRotation, Quaternion.Euler(4f, 0f, 0f), t);
                    if (pivotJaw != null)
                        pivotJaw.localRotation = Quaternion.Slerp(pivotJaw.localRotation, Quaternion.Euler(2f, 0f, 0f), t);

                    if (pivotRightShoulder != null)
                        pivotRightShoulder.localRotation = Quaternion.Slerp(rShoulderSmash, recoverShoulderTarget, t);
                    else if (weaponArmPivot != null)
                        weaponArmPivot.localRotation = Quaternion.Slerp(rShoulderSmash, recoverShoulderTarget, t);

                    if (pivotRightElbow != null)
                        pivotRightElbow.localRotation = Quaternion.Slerp(rElbowSmash, recoverElbowTarget, t);
                }

                yield return null;
            }

            isStriking = false;
            patrolAngle = Mathf.Atan2(transform.position.z, transform.position.x);
            idleMode = ButcherIdleMode.Patrolling;
            if (visualRoot != null)
            {
                visualRoot.SetActive(true);
            }
            activeRoutine = null;
        }

        private void PlayWeaponImpactSound(ExecutionWeaponType type)
        {
            if (AudioManager.Instance == null) return;

            switch (type)
            {
                case ExecutionWeaponType.GiantMeatMallet:
                    AudioManager.Instance.PlayMalletBonk();
                    break;
                case ExecutionWeaponType.FlyingCastIronPan:
                    AudioManager.Instance.PlayPanDong();
                    break;
                case ExecutionWeaponType.SausageWhip:
                    AudioManager.Instance.PlayWhipSlap();
                    break;
                case ExecutionWeaponType.CreamPieLauncher:
                default:
                    AudioManager.Instance.PlayPieSplat();
                    break;
            }
        }

        public void SetupWeapons(Transform pivot, GameObject[] weapons)
        {
            if (pivotRightShoulder == null)
            {
                weaponArmPivot = pivot;
            }
            weaponModels = weapons;
        }
    }
}
