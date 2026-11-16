using System.Collections.Generic;
using UnityEngine;
using FingerMonster.Core;
using FingerMonster.Gameplay;

namespace FingerMonster.Player
{
    /// <summary>
    /// 3D iskeletli karakterlerde sol ve sağ el koparma mekaniğini yöneten yeniden kullanılabilir bileşen.
    /// Kopan elin kemiğini (Hand) ve altındaki tüm parmak kemiklerini scale = 0 yaparak gizler,
    /// aynı anda aynı konumda karakterin cilt materyaline sahip çizgi film fizikli kopan el prefab'ı spawn eder.
    /// </summary>
    public class HandSeverSystem : MonoBehaviour
    {
        [Header("İskelet ve Kemik Referansları")]
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private Transform leftHandBone;
        [SerializeField] private Transform rightHandBone;
        [SerializeField] private Transform leftForeArmBone;
        [SerializeField] private Transform rightForeArmBone;

        [Header("Bilek Sargıları (Yara Bandı)")]
        [SerializeField] private GameObject leftWristBandage;
        [SerializeField] private GameObject rightWristBandage;

        [Header("Kopan El Prefab'ları ve Cilt Materyali")]
        [SerializeField] private GameObject leftSeveredHandPrefab;
        [SerializeField] private GameObject rightSeveredHandPrefab;
        [SerializeField] private Material characterSkinMaterial;
        [SerializeField] private Color fxAccentColor = new Color(0.95f, 0.45f, 0.2f);

        private readonly List<Transform> leftHandHierarchy = new List<Transform>();
        private readonly List<Transform> rightHandHierarchy = new List<Transform>();
        private readonly List<GameObject> spawnedSeveredHands = new List<GameObject>();

        private bool isLeftHandSevered = false;
        private bool isRightHandSevered = false;

        public bool IsLeftHandSevered => isLeftHandSevered;
        public bool IsRightHandSevered => isRightHandSevered;
        public int RemainingHandsCount => (isLeftHandSevered ? 0 : 1) + (isRightHandSevered ? 0 : 1);
        public Transform LeftHandBone => leftHandBone;
        public Transform RightHandBone => rightHandBone;

        private void Awake()
        {
            ResolveBones();
            ResetHands();
        }

        private void OnValidate()
        {
            ResolveBones();
        }

        private void LateUpdate()
        {
            // Animator kemik ölçeğini sıfırlarsa bile kopan el kemiklerinin scale = 0 kalmasını garanti et
            if (isLeftHandSevered)
            {
                ApplyZeroScale(leftHandHierarchy);
            }
            if (isRightHandSevered)
            {
                ApplyZeroScale(rightHandHierarchy);
            }
        }

        /// <summary>
        /// Humanoid Animator üzerinden veya kemik adlarından el ve parmak kemiklerini otomatik bulur.
        /// </summary>
        public void ResolveBones()
        {
            if (targetAnimator == null)
            {
                targetAnimator = GetComponentInChildren<Animator>();
            }

            if (targetAnimator != null && targetAnimator.isHuman)
            {
                if (leftHandBone == null) leftHandBone = targetAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
                if (rightHandBone == null) rightHandBone = targetAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                if (leftForeArmBone == null) leftForeArmBone = targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                if (rightForeArmBone == null) rightForeArmBone = targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            }

            if (leftHandBone == null) leftHandBone = FindDescendantByName(transform, "LeftHand");
            if (rightHandBone == null) rightHandBone = FindDescendantByName(transform, "RightHand");
            if (leftForeArmBone == null) leftForeArmBone = FindDescendantByName(transform, "LeftForeArm");
            if (rightForeArmBone == null) rightForeArmBone = FindDescendantByName(transform, "RightForeArm");

            CollectHierarchy(leftHandBone, leftHandHierarchy);
            CollectHierarchy(rightHandBone, rightHandHierarchy);
        }

        public void Configure(
            Animator animator,
            GameObject leftPrefab,
            GameObject rightPrefab,
            Material skinMat,
            Color accentColor,
            GameObject leftBandage = null,
            GameObject rightBandage = null)
        {
            targetAnimator = animator;
            leftSeveredHandPrefab = leftPrefab;
            rightSeveredHandPrefab = rightPrefab;
            characterSkinMaterial = skinMat;
            fxAccentColor = accentColor;
            if (leftBandage != null) leftWristBandage = leftBandage;
            if (rightBandage != null) rightWristBandage = rightBandage;
            ResolveBones();
        }

        /// <summary>
        /// Belirtilen eli koparır: kemikleri scale=0 ile gizler, puf/boya partikülü oynatır
        /// ve aynı konumda fiziksel kopan el prefab'ı fırlatır.
        /// </summary>
        public void SeverHand(PlayerHand hand, Color? overrideAccentColor = null)
        {
            if (hand == PlayerHand.None) return;
            ResolveBones();

            bool isLeft = (hand == PlayerHand.Left);
            if (isLeft && isLeftHandSevered) return;
            if (!isLeft && isRightHandSevered) return;

            if (isLeft) isLeftHandSevered = true;
            else isRightHandSevered = true;

            Transform handBone = isLeft ? leftHandBone : rightHandBone;
            List<Transform> hierarchy = isLeft ? leftHandHierarchy : rightHandHierarchy;
            GameObject bandage = isLeft ? leftWristBandage : rightWristBandage;
            GameObject prefab = isLeft ? leftSeveredHandPrefab : rightSeveredHandPrefab;
            Color fxColor = overrideAccentColor ?? fxAccentColor;

            Vector3 spawnPos = handBone != null ? handBone.position : (transform.position + transform.up * 0.9f + transform.forward * 0.4f);
            Quaternion spawnRot = handBone != null ? handBone.rotation : transform.rotation;

            // 1. El ve altındaki parmak kemiklerini scale = 0.0001f yaparak gizle
            ApplyZeroScale(hierarchy);

            // 2. Bilek kesik yara modelini aç ve beyaz çizgi film bandajı görünümünü engelleyip koyu kan/kesik yara rengine boya
            if (bandage != null)
            {
                bandage.SetActive(true);
                Color woundColor = new Color(0.32f, 0.015f, 0.015f);
                var woundRenderers = bandage.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < woundRenderers.Length; i++)
                {
                    if (woundRenderers[i] != null)
                    {
                        Material mat = woundRenderers[i].material;
                        mat.color = woundColor;
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", woundColor);
                        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.85f);
                    }
                }
            }

            // 3. Kopma noktasında gerçekçi kan sıçraması, kan birikintisi ve sarsıntı
            if (CartoonFXManager.Instance != null)
            {
                CartoonFXManager.Instance.PlayButcherStrikeFX(spawnPos);
            }

            // 4. Kopan el prefab'ını aynı konumda spawn et ve fiziksel olarak fırlat
            SpawnFlyingSeveredHand(prefab, spawnPos, spawnRot, isLeft, fxColor);
        }

        private void SpawnFlyingSeveredHand(GameObject prefab, Vector3 spawnPos, Quaternion spawnRot, bool isLeft, Color fxColor)
        {
            GameObject handInstance;
            if (prefab != null)
            {
                handInstance = Instantiate(prefab, spawnPos, spawnRot);
            }
            else
            {
                // Güvenli yedek (prefab atanmamışsa basit çizgi film el küpü)
                handInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handInstance.transform.SetPositionAndRotation(spawnPos, spawnRot);
                handInstance.transform.localScale = new Vector3(0.11f, 0.05f, 0.16f);
            }

            handInstance.name = isLeft ? "SeveredHand_Left_Instance" : "SeveredHand_Right_Instance";
            spawnedSeveredHands.Add(handInstance);

            if (characterSkinMaterial != null)
            {
                var renderers = handInstance.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        renderers[i].sharedMaterial = characterSkinMaterial;
                    }
                }
            }

            SeveredHandPhysics physics = handInstance.GetComponent<SeveredHandPhysics>();
            if (physics == null)
            {
                physics = handInstance.AddComponent<SeveredHandPhysics>();
            }

            Vector3 localSide = isLeft ? -transform.right : transform.right;
            Vector3 flyDir = (localSide + (-transform.forward * 0.35f) + Vector3.up * 0.4f).normalized;
            physics.Launch(flyDir, fxColor);
        }

        /// <summary>
        /// Yeni tur başında elleri ve kemik ölçeklerini sıfırlar.
        /// </summary>
        public void ResetHands()
        {
            isLeftHandSevered = false;
            isRightHandSevered = false;

            RestoreOneScale(leftHandHierarchy);
            RestoreOneScale(rightHandHierarchy);

            if (leftWristBandage != null) leftWristBandage.SetActive(false);
            if (rightWristBandage != null) rightWristBandage.SetActive(false);

            for (int i = 0; i < spawnedSeveredHands.Count; i++)
            {
                if (spawnedSeveredHands[i] != null)
                {
                    Destroy(spawnedSeveredHands[i]);
                }
            }
            spawnedSeveredHands.Clear();
        }

        private static void CollectHierarchy(Transform root, List<Transform> list)
        {
            list.Clear();
            if (root == null) return;
            list.Add(root);
            for (int i = 0; i < root.childCount; i++)
            {
                CollectRecursive(root.GetChild(i), list);
            }
        }

        private static void CollectRecursive(Transform current, List<Transform> list)
        {
            if (current == null) return;
            // Bilek sargısı yanlışlıkla el altında ise gizlemeyelim
            if (current.name.Contains("Bandage")) return;
            list.Add(current);
            for (int i = 0; i < current.childCount; i++)
            {
                CollectRecursive(current.GetChild(i), list);
            }
        }

        private static void ApplyZeroScale(List<Transform> bones)
        {
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i] != null)
                {
                    // Sıfır yerine 0.0001f kullanarak tekil matris (singular matrix / division by zero) ve Invalid AABB hatalarını önle
                    bones[i].localScale = new Vector3(0.0001f, 0.0001f, 0.0001f);
                }
            }
        }

        private static void RestoreOneScale(List<Transform> bones)
        {
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i] != null)
                {
                    bones[i].localScale = Vector3.one;
                }
            }
        }

        private static Transform FindDescendantByName(Transform parent, string boneName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == boneName) return child;
                Transform found = FindDescendantByName(child, boneName);
                if (found != null) return found;
            }
            return null;
        }
    }
}

