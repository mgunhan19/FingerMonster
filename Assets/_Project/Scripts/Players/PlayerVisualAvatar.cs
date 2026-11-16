using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using FingerMonster.Core;

namespace FingerMonster.Player
{
    /// <summary>
    /// Masada oturan 3D karakter avatarını temsil eder.
    /// Karakterin animasyon sürücüsünü (CharacterAnimatorDriver) ve el kopma mekaniğini (HandSeverSystem) koordine eder.
    /// </summary>
    public class PlayerVisualAvatar : MonoBehaviour
    {
        [Header("Karakter Kimliği")]
        public CharacterArchetype Archetype = CharacterArchetype.Kabadayi;

        [Header("Görsel Parçalar (Eski Yedek)")]
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform torsoTransform;
        [SerializeField] private GameObject leftHandObj;
        [SerializeField] private GameObject rightHandObj;

        [Header("Yara Bantları (Bandages)")]
        [SerializeField] private GameObject leftWristBandage;
        [SerializeField] private GameObject rightWristBandage;

        [Header("3D Karakter Sistemleri")]
        [SerializeField] private CharacterAnimatorDriver animatorDriver;
        [SerializeField] private HandSeverSystem handSeverSystem;

        [Header("Oyuncu Vurgu Renkleri (Sadece Aksesuar/Rozet)")]
        [SerializeField] private Renderer[] playerColoredRenderers;

        private Vector3 initialHeadLocalPos;
        private Quaternion initialHeadLocalRot = Quaternion.identity;
        private Vector3 initialTorsoLocalPos;
        private Quaternion initialTorsoLocalRot = Quaternion.identity;
        private Vector3 initialLeftHandLocalPos;
        private Quaternion initialLeftHandLocalRot = Quaternion.identity;
        private Vector3 initialRightHandLocalPos;
        private Quaternion initialRightHandLocalRot = Quaternion.identity;
        private Color myColor = Color.white;
        private Vector3 initialRootLocalPos;
        private Quaternion initialRootLocalRot = Quaternion.identity;

        private readonly Dictionary<Renderer, Material[]> originalSharedMaterials = new Dictionary<Renderer, Material[]>();
        private readonly List<Material> ghostRuntimeMaterials = new List<Material>();
        private readonly List<Material> ghostCosmeticMaterials = new List<Material>();
        private readonly List<GameObject> activeCharacterSplats = new List<GameObject>();
        private readonly List<Material> activeSplatMaterials = new List<Material>();
        private bool isGhostMode = false;
        private bool isGhostFreeFlightActive = false;
        private float currentGhostAlpha = 0.42f;
        private GameObject ghostCosmeticRoot;
        private Coroutine ghostFloatRoutine;
        private Coroutine scareFlinchRoutine;

        private static bool IsValidRotation(Quaternion q)
        {
            return float.IsFinite(q.x) && float.IsFinite(q.y) && float.IsFinite(q.z) && float.IsFinite(q.w) &&
                   (q.x != 0f || q.y != 0f || q.z != 0f || q.w != 0f);
        }

        public CharacterAnimatorDriver AnimatorDriver => animatorDriver;
        public HandSeverSystem HandSever => handSeverSystem;

        private void Awake()
        {
            if (animatorDriver == null) animatorDriver = GetComponent<CharacterAnimatorDriver>() ?? gameObject.AddComponent<CharacterAnimatorDriver>();
            if (handSeverSystem == null) handSeverSystem = GetComponent<HandSeverSystem>();

            animatorDriver.Archetype = Archetype;

            if (headTransform != null)
            {
                initialHeadLocalPos = headTransform.localPosition;
                if (IsValidRotation(headTransform.localRotation)) initialHeadLocalRot = headTransform.localRotation;
            }
            if (torsoTransform != null)
            {
                initialTorsoLocalPos = torsoTransform.localPosition;
                if (IsValidRotation(torsoTransform.localRotation)) initialTorsoLocalRot = torsoTransform.localRotation;
            }
            if (leftHandObj != null)
            {
                initialLeftHandLocalPos = leftHandObj.transform.localPosition;
                if (IsValidRotation(leftHandObj.transform.localRotation)) initialLeftHandLocalRot = leftHandObj.transform.localRotation;
            }
            if (rightHandObj != null)
            {
                initialRightHandLocalPos = rightHandObj.transform.localPosition;
                if (IsValidRotation(rightHandObj.transform.localRotation)) initialRightHandLocalRot = rightHandObj.transform.localRotation;
            }

            initialRootLocalPos = transform.localPosition;
            if (IsValidRotation(transform.localRotation)) initialRootLocalRot = transform.localRotation;
            CacheOriginalMaterials();

            ResetAvatar();
        }

        private void CacheOriginalMaterials()
        {
            originalSharedMaterials.Clear();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                originalSharedMaterials[r] = r.sharedMaterials;
            }
        }

        public void SetPlayerColor(Color color)
        {
            myColor = color;
            // Sadece özel atanmış vurgu/rozet renderer'larını boya; 3D karakterin dokusunu bozma
            if (playerColoredRenderers == null || playerColoredRenderers.Length == 0)
            {
                return;
            }

            foreach (var r in playerColoredRenderers)
            {
                if (r != null && !r.name.Contains("Bandage") && !(r is SkinnedMeshRenderer))
                {
                    r.material.color = color;
                    if (r.material.HasProperty("_BaseColor"))
                    {
                        r.material.SetColor("_BaseColor", color);
                    }
                }
            }
        }

        /// <summary>
        /// Belirtilen elin kopmasını sağlar (HandSeverSystem varsa kemik gizleme + fizik prefab kullanır).
        /// </summary>
        public void SeverHand(PlayerHand hand)
        {
            if (hand == PlayerHand.None) return;

            if (handSeverSystem != null)
            {
                handSeverSystem.SeverHand(hand, myColor);
            }
            else
            {
                GameObject targetHand = (hand == PlayerHand.Left) ? leftHandObj : rightHandObj;
                GameObject targetBandage = (hand == PlayerHand.Left) ? leftWristBandage : rightWristBandage;
                Vector3 flyDir = (hand == PlayerHand.Left) ? (Vector3.left + Vector3.back * 0.4f) : (Vector3.right + Vector3.back * 0.4f);

                if (targetHand != null)
                {
                    SeveredHandPhysics phys = targetHand.GetComponent<SeveredHandPhysics>();
                    if (phys == null) phys = targetHand.AddComponent<SeveredHandPhysics>();
                    phys.Launch(flyDir, myColor);
                }

                if (targetBandage != null) targetBandage.SetActive(true);
            }

            if (animatorDriver != null)
            {
                animatorDriver.OnHandSevered(hand);
                animatorDriver.PlayShockAnimation();
            }
        }

        public void TriggerEmote(EmoteType emote)
        {
            if (animatorDriver != null)
            {
                animatorDriver.PlayEmote(emote);
            }
        }

        public void TriggerEmoteSlot(int slotIndex1Based, Vector3? targetWorldPos = null, CharacterEmoteData emoteData = null)
        {
            if (animatorDriver != null)
            {
                EmoteSlotInfo info = emoteData != null ? emoteData.GetSlot(slotIndex1Based) : null;
                animatorDriver.PlayEmoteSlot(
                    slotIndex1Based,
                    targetWorldPos,
                    info != null ? info.PopupText : null,
                    info != null ? (Color?)info.PopupColor : null);
            }
        }

        public void PlayPressKey(int remainingHandsCount, System.Action onImpactCallback = null, Vector3? keyWorldPosition = null)
        {
            if (animatorDriver != null)
            {
                animatorDriver.PlayPressKeyAnimation(remainingHandsCount, onImpactCallback, keyWorldPosition);
            }
            else
            {
                onImpactCallback?.Invoke();
            }
        }

        public void PlayJoyRelief()
        {
            if (animatorDriver != null)
            {
                animatorDriver.PlayJoyReliefAnimation();
            }
        }

        public void PlayThinking(float duration, Vector3? targetLookWorld = null)
        {
            if (animatorDriver != null)
            {
                animatorDriver.PlayThinkingAnimation(duration, targetLookWorld);
            }
        }

        public void PlayEliminatedByWeapon(int weaponIndex)
        {
            if (animatorDriver != null)
            {
                animatorDriver.PlayEliminationByWeapon(weaponIndex);
            }
            else
            {
                PlayEliminatedAnimation();
            }
        }

        public void PlayEliminatedAnimation()
        {
            StartCoroutine(KnockoutRoutine());
        }

        private IEnumerator KnockoutRoutine()
        {
            if (torsoTransform == null) yield break;

            Quaternion startRot = torsoTransform.localRotation;
            Quaternion targetRot = Quaternion.Euler(-65f, 0, 0);
            Vector3 startPos = torsoTransform.localPosition;
            Vector3 targetPos = startPos + new Vector3(0, -0.15f, -0.2f);

            float elapsed = 0f;
            float duration = 0.6f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                torsoTransform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                torsoTransform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            torsoTransform.localRotation = targetRot;
            torsoTransform.localPosition = targetPos;
        }

        public void ResetAvatar()
        {
            SetGhostMode(false);
            ClearCharacterSplats();
            StopAllCoroutines();
            scareFlinchRoutine = null;
            isGhostFreeFlightActive = false;

            transform.localPosition = initialRootLocalPos;
            transform.localRotation = IsValidRotation(initialRootLocalRot) ? initialRootLocalRot : Quaternion.identity;

            if (handSeverSystem != null)
            {
                handSeverSystem.ResetHands();
            }

            if (animatorDriver != null)
            {
                animatorDriver.ResetAnimatorState();
            }

            if (leftHandObj != null)
            {
                var phys = leftHandObj.GetComponent<SeveredHandPhysics>();
                if (phys != null) phys.ResetHand(transform, initialLeftHandLocalPos, initialLeftHandLocalRot);
                else
                {
                    leftHandObj.transform.SetParent(transform);
                    leftHandObj.transform.localPosition = initialLeftHandLocalPos;
                    leftHandObj.transform.localRotation = initialLeftHandLocalRot;
                    leftHandObj.SetActive(true);
                }
            }

            if (rightHandObj != null)
            {
                var phys = rightHandObj.GetComponent<SeveredHandPhysics>();
                if (phys != null) phys.ResetHand(transform, initialRightHandLocalPos, initialRightHandLocalRot);
                else
                {
                    rightHandObj.transform.SetParent(transform);
                    rightHandObj.transform.localPosition = initialRightHandLocalPos;
                    rightHandObj.transform.localRotation = initialRightHandLocalRot;
                    rightHandObj.SetActive(true);
                }
            }

            if (leftWristBandage != null) leftWristBandage.SetActive(false);
            if (rightWristBandage != null) rightWristBandage.SetActive(false);

            if (headTransform != null)
            {
                headTransform.localPosition = initialHeadLocalPos;
                headTransform.localRotation = IsValidRotation(initialHeadLocalRot) ? initialHeadLocalRot : Quaternion.identity;
            }

            if (torsoTransform != null)
            {
                torsoTransform.localPosition = initialTorsoLocalPos;
                torsoTransform.localRotation = IsValidRotation(initialTorsoLocalRot) ? initialTorsoLocalRot : Quaternion.identity;
            }
        }

        private void OnDestroy()
        {
            SetGhostMode(false);
            ClearCharacterSplats();
            for (int i = 0; i < ghostCosmeticMaterials.Count; i++)
            {
                if (ghostCosmeticMaterials[i] != null)
                {
                    DestroyObjectSafe(ghostCosmeticMaterials[i]);
                }
            }
            ghostCosmeticMaterials.Clear();
            if (ghostCosmeticRoot != null)
            {
                DestroyObjectSafe(ghostCosmeticRoot);
                ghostCosmeticRoot = null;
            }
        }

        /// <summary>
        /// Avatarı "Kasabın Yardımcısı (Hayalet)" yarı saydam moduna geçirir veya normal haline döndürür.
        /// </summary>
        public void SetGhostMode(bool active)
        {
            if (active == isGhostMode && (!active || ghostRuntimeMaterials.Count > 0))
            {
                return;
            }

            isGhostMode = active;

            if (ghostFloatRoutine != null)
            {
                StopCoroutine(ghostFloatRoutine);
                ghostFloatRoutine = null;
            }

            if (active)
            {
                isGhostFreeFlightActive = false;
                currentGhostAlpha = 0.42f;
                CleanupGhostRuntimeMaterials();

                foreach (var kvp in originalSharedMaterials)
                {
                    Renderer r = kvp.Key;
                    Material[] origMats = kvp.Value;
                    if (r == null || origMats == null) continue;

                    Material[] ghostMats = new Material[origMats.Length];
                    for (int i = 0; i < origMats.Length; i++)
                    {
                        if (origMats[i] == null) continue;
                        Material gm = new Material(origMats[i]);
                        ConfigureUrpGhostMaterial(gm, 0.42f);
                        ghostRuntimeMaterials.Add(gm);
                        ghostMats[i] = gm;
                    }
                    r.sharedMaterials = ghostMats;
                }

                EnsureGhostCosmetics();
                if (ghostCosmeticRoot != null)
                {
                    ghostCosmeticRoot.SetActive(true);
                }

                ghostFloatRoutine = StartCoroutine(GhostFloatRoutine());
            }
            else
            {
                isGhostFreeFlightActive = false;
                currentGhostAlpha = 0.42f;
                RestoreOriginalMaterials();
                CleanupGhostRuntimeMaterials();

                if (ghostCosmeticRoot != null)
                {
                    ghostCosmeticRoot.transform.localPosition = Vector3.zero;
                    ghostCosmeticRoot.SetActive(false);
                }

                if (torsoTransform != null)
                {
                    torsoTransform.localPosition = initialTorsoLocalPos;
                }
                transform.localPosition = initialRootLocalPos;
                transform.localRotation = IsValidRotation(initialRootLocalRot) ? initialRootLocalRot : Quaternion.identity;
            }
        }

        private void ConfigureUrpGhostMaterial(Material mat, float alpha)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            Color baseCol = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
            Color ghostCol = Color.Lerp(baseCol, new Color(0.55f, 0.68f, 1.0f, 1f), 0.55f);
            ghostCol.a = alpha;
            mat.color = ghostCol;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", ghostCol);
            }

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.28f, 0.45f, 0.92f) * 0.65f);
            }
        }

        private void EnsureGhostCosmetics()
        {
            if (ghostCosmeticRoot != null) return;

            ghostCosmeticRoot = new GameObject("GhostButcherCosmetics");
            ghostCosmeticRoot.transform.SetParent(transform, false);

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // Basit prosedürel kasap şapkası (kafanın üstünde)
            GameObject hatObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hatObj.name = "GhostButcherHat";
            RemoveCollider(hatObj);
            hatObj.transform.SetParent(headTransform != null ? headTransform : ghostCosmeticRoot.transform, false);
            hatObj.transform.localPosition = headTransform != null ? new Vector3(0f, 0.24f, 0f) : new Vector3(0f, 1.68f, 0f);
            hatObj.transform.localScale = new Vector3(0.28f, 0.08f, 0.28f);

            // Göğüs önlüğü (gövde önünde)
            GameObject apronObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            apronObj.name = "GhostButcherApron";
            RemoveCollider(apronObj);
            apronObj.transform.SetParent(torsoTransform != null ? torsoTransform : ghostCosmeticRoot.transform, false);
            apronObj.transform.localPosition = torsoTransform != null ? new Vector3(0f, -0.04f, 0.16f) : new Vector3(0f, 1.05f, 0.18f);
            apronObj.transform.localScale = new Vector3(0.30f, 0.42f, 0.03f);

            if (urpLit != null)
            {
                Material hatMat = new Material(urpLit);
                hatMat.color = new Color(0.85f, 0.90f, 1.0f, 0.55f);
                ConfigureUrpGhostMaterial(hatMat, 0.52f);
                ghostCosmeticMaterials.Add(hatMat);
                Renderer hatRenderer = hatObj.GetComponent<Renderer>();
                if (hatRenderer != null) hatRenderer.sharedMaterial = hatMat;

                Material apronMat = new Material(urpLit);
                apronMat.color = new Color(0.65f, 0.72f, 0.95f, 0.48f);
                ConfigureUrpGhostMaterial(apronMat, 0.48f);
                ghostCosmeticMaterials.Add(apronMat);
                Renderer apronRenderer = apronObj.GetComponent<Renderer>();
                if (apronRenderer != null) apronRenderer.sharedMaterial = apronMat;
            }

            // Başka transformlara bağlandıysa da SetActive ile toplu gizlenebilmesi için hat/apron'u takip et
            // Not: Eğer headTransform/torsoTransform altındalarsa ghostCosmeticRoot aktif/pasif durumu onları etkilemez;
            // bu yüzden dünya konumuna göre ghostCosmeticRoot altında tutup baş/gövdeye göre konumlamak en güvenlisidir.
            if (headTransform != null)
            {
                hatObj.transform.SetParent(ghostCosmeticRoot.transform, true);
            }
            if (torsoTransform != null)
            {
                apronObj.transform.SetParent(ghostCosmeticRoot.transform, true);
            }
        }

        private IEnumerator GhostFloatRoutine()
        {
            Transform floatTarget = torsoTransform != null ? torsoTransform : transform;
            Vector3 basePos = torsoTransform != null ? initialTorsoLocalPos : initialRootLocalPos;
            float t = 0f;

            while (isGhostMode)
            {
                t += Time.deltaTime;
                float offsetY = Mathf.Sin(t * 2.2f) * 0.045f;
                if (torsoTransform != null || !isGhostFreeFlightActive)
                {
                    floatTarget.localPosition = basePos + new Vector3(0f, offsetY, 0f);
                }
                if (ghostCosmeticRoot != null && torsoTransform != null)
                {
                    ghostCosmeticRoot.transform.localPosition = new Vector3(0f, offsetY, 0f);
                }
                yield return null;
            }
        }

        /// <summary>
        /// Hayalet avatarını oda içinde serbest uçuş konumuna taşır;
        /// GhostFloatRoutine'in torsoTransform üzerinde yaptığı yerel süzülmeyi bozmaz.
        /// </summary>
        public void SetGhostFreeFlightTransform(Vector3 worldPos, Quaternion worldRot)
        {
            isGhostFreeFlightActive = true;
            if (float.IsFinite(worldPos.x) && float.IsFinite(worldPos.y) && float.IsFinite(worldPos.z))
            {
                transform.position = worldPos;
            }
            if (IsValidRotation(worldRot))
            {
                transform.rotation = worldRot;
            }
        }

        /// <summary>
        /// Hayalet avatarı yaşayan yerel oyuncunun kamerasına fadeDistance içindeyse veya
        /// kamera ile masa/daktilo arasına giriyorsa materyal alfasını yumuşakça minAlpha'ya düşürür.
        /// </summary>
        public void UpdateGhostProximityFade(Vector3? localLivingCameraPos, Vector3 tableCenter, float fadeDistance = 1.0f, float minAlpha = 0.15f)
        {
            if (!isGhostMode) return;

            float targetAlpha = 0.42f;

            if (localLivingCameraPos.HasValue)
            {
                Vector3 camPos = localLivingCameraPos.Value;
                Vector3 ghostCenter = transform.position + Vector3.up * 0.6f;

                float distToCam = Mathf.Min(
                    Vector3.Distance(transform.position, camPos),
                    Vector3.Distance(ghostCenter, camPos));

                bool shouldFade = distToCam <= fadeDistance;

                if (!shouldFade)
                {
                    Vector3 ab = tableCenter - camPos;
                    float abLenSq = ab.sqrMagnitude;
                    if (abLenSq > 0.001f)
                    {
                        float t = Mathf.Clamp01(Vector3.Dot(ghostCenter - camPos, ab) / abLenSq);
                        if (t > 0.02f && t < 0.98f)
                        {
                            Vector3 closest = camPos + ab * t;
                            if (Vector3.Distance(ghostCenter, closest) <= 0.75f)
                            {
                                shouldFade = true;
                            }
                        }
                    }
                }

                if (shouldFade)
                {
                    targetAlpha = minAlpha;
                }
            }

            currentGhostAlpha = Mathf.MoveTowards(currentGhostAlpha, targetAlpha, Time.deltaTime * 3.5f);
            ApplyAlphaToGhostMaterialList(ghostRuntimeMaterials, currentGhostAlpha);
            ApplyAlphaToGhostMaterialList(ghostCosmeticMaterials, Mathf.Min(0.55f, currentGhostAlpha + 0.06f));
        }

        private static void ApplyAlphaToGhostMaterialList(List<Material> materials, float alpha)
        {
            for (int i = 0; i < materials.Count; i++)
            {
                Material mat = materials[i];
                if (mat == null) continue;

                Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
                c.a = alpha;
                mat.color = c;
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", c);
                }
            }
        }

        /// <summary>
        /// Korkutulan yaşayan oyuncuda kısa irkilme/sıçrama animasyonu oynatır.
        /// </summary>
        public void PlayScareFlinch(bool reduced)
        {
            if (isGhostMode) return;
            if (scareFlinchRoutine != null)
            {
                StopCoroutine(scareFlinchRoutine);
            }
            scareFlinchRoutine = StartCoroutine(ScareFlinchRoutine(reduced));
        }

        private IEnumerator ScareFlinchRoutine(bool reduced)
        {
            if (torsoTransform == null && headTransform == null)
            {
                scareFlinchRoutine = null;
                yield break;
            }

            float scale = reduced ? 0.4f : 1.0f;
            Vector3 torsoStartPos = initialTorsoLocalPos;
            Quaternion torsoStartRot = IsValidRotation(initialTorsoLocalRot) ? initialTorsoLocalRot : Quaternion.identity;
            Quaternion headStartRot = IsValidRotation(initialHeadLocalRot) ? initialHeadLocalRot : Quaternion.identity;

            Vector3 torsoPeakPos = torsoStartPos + new Vector3(0f, 0.055f * scale, -0.045f * scale);
            Quaternion torsoPeakRot = torsoStartRot * Quaternion.Euler(-11f * scale, 0f, 0f);
            Quaternion headPeakRot = headStartRot * Quaternion.Euler(-14f * scale, 0f, 0f);

            float upDuration = 0.11f;
            float downDuration = 0.28f;
            float elapsed = 0f;

            while (elapsed < upDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / upDuration));
                if (torsoTransform != null)
                {
                    torsoTransform.localPosition = Vector3.Lerp(torsoStartPos, torsoPeakPos, t);
                    torsoTransform.localRotation = Quaternion.Slerp(torsoStartRot, torsoPeakRot, t);
                }
                if (headTransform != null)
                {
                    headTransform.localRotation = Quaternion.Slerp(headStartRot, headPeakRot, t);
                }
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < downDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / downDuration));
                if (torsoTransform != null)
                {
                    torsoTransform.localPosition = Vector3.Lerp(torsoPeakPos, torsoStartPos, t);
                    torsoTransform.localRotation = Quaternion.Slerp(torsoPeakRot, torsoStartRot, t);
                }
                if (headTransform != null)
                {
                    headTransform.localRotation = Quaternion.Slerp(headPeakRot, headStartRot, t);
                }
                yield return null;
            }

            if (torsoTransform != null)
            {
                torsoTransform.localPosition = torsoStartPos;
                torsoTransform.localRotation = torsoStartRot;
            }
            if (headTransform != null)
            {
                headTransform.localRotation = headStartRot;
            }
            scareFlinchRoutine = null;
        }

        /// <summary>
        /// Fırlatılan eşya çarptığında karakter üzerinde geçici karikatür sıçrama lekesi gösterir.
        /// </summary>
        public void ShowCharacterSplat(Color splatColor, float duration = 3f)
        {
            Transform parent = torsoTransform != null ? torsoTransform : (headTransform != null ? headTransform : transform);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            GameObject splatGroup = new GameObject("CharacterSplat");
            splatGroup.transform.SetParent(parent, false);
            splatGroup.transform.localPosition = new Vector3(
                Random.Range(-0.10f, 0.10f),
                Random.Range(0.02f, 0.22f),
                0.18f);

            Material splatMat = null;
            if (urpLit != null)
            {
                splatMat = new Material(urpLit);
                splatMat.color = splatColor;
                if (splatMat.HasProperty("_BaseColor"))
                {
                    splatMat.SetColor("_BaseColor", splatColor);
                }
                activeSplatMaterials.Add(splatMat);
            }

            for (int i = 0; i < 3; i++)
            {
                GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                blob.name = $"SplatBlob_{i}";
                RemoveCollider(blob);
                blob.transform.SetParent(splatGroup.transform, false);
                blob.transform.localPosition = i == 0
                    ? Vector3.zero
                    : new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(-0.07f, 0.07f), Random.Range(-0.01f, 0.02f));
                float r = i == 0 ? 0.13f : Random.Range(0.06f, 0.09f);
                blob.transform.localScale = new Vector3(r, r, r * 0.35f);

                Renderer rend = blob.GetComponent<Renderer>();
                if (rend != null && splatMat != null)
                {
                    rend.sharedMaterial = splatMat;
                }
            }

            activeCharacterSplats.Add(splatGroup);
            StartCoroutine(CharacterSplatLifetimeRoutine(splatGroup, splatMat, Mathf.Max(0.25f, duration)));
        }

        private IEnumerator CharacterSplatLifetimeRoutine(GameObject splatGroup, Material splatMat, float duration)
        {
            if (splatGroup == null) yield break;

            Vector3 targetScale = Vector3.one;
            splatGroup.transform.localScale = Vector3.one * 0.2f;

            float popTime = 0.12f;
            float elapsed = 0f;
            while (elapsed < popTime && splatGroup != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / popTime);
                splatGroup.transform.localScale = Vector3.Lerp(Vector3.one * 0.2f, targetScale, t);
                yield return null;
            }

            float holdTime = Mathf.Max(0f, duration - 0.35f);
            if (holdTime > 0f)
            {
                yield return new WaitForSeconds(holdTime);
            }

            float shrinkTime = 0.23f;
            elapsed = 0f;
            while (elapsed < shrinkTime && splatGroup != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / shrinkTime);
                splatGroup.transform.localScale = Vector3.Lerp(targetScale, Vector3.zero, t);
                yield return null;
            }

            if (splatGroup != null)
            {
                activeCharacterSplats.Remove(splatGroup);
                DestroyObjectSafe(splatGroup);
            }
            if (splatMat != null)
            {
                activeSplatMaterials.Remove(splatMat);
                DestroyObjectSafe(splatMat);
            }
        }

        /// <summary>
        /// Karakter üzerindeki tüm aktif sıçrama lekelerini hemen temizler.
        /// </summary>
        public void ClearCharacterSplats()
        {
            for (int i = 0; i < activeCharacterSplats.Count; i++)
            {
                if (activeCharacterSplats[i] != null)
                {
                    DestroyObjectSafe(activeCharacterSplats[i]);
                }
            }
            activeCharacterSplats.Clear();

            for (int i = 0; i < activeSplatMaterials.Count; i++)
            {
                if (activeSplatMaterials[i] != null)
                {
                    DestroyObjectSafe(activeSplatMaterials[i]);
                }
            }
            activeSplatMaterials.Clear();
        }

        private void RestoreOriginalMaterials()
        {
            foreach (var kvp in originalSharedMaterials)
            {
                if (kvp.Key != null && kvp.Value != null)
                {
                    kvp.Key.sharedMaterials = kvp.Value;
                }
            }
        }

        private void CleanupGhostRuntimeMaterials()
        {
            for (int i = 0; i < ghostRuntimeMaterials.Count; i++)
            {
                if (ghostRuntimeMaterials[i] != null)
                {
                    DestroyObjectSafe(ghostRuntimeMaterials[i]);
                }
            }
            ghostRuntimeMaterials.Clear();
        }

        private static void RemoveCollider(GameObject obj)
        {
            Collider col = obj.GetComponent<Collider>();
            if (col != null)
            {
                DestroyObjectSafe(col);
            }
        }

        private static void DestroyObjectSafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }
    }
}
