using System.Collections;
using UnityEngine;
using FingerMonster.Core;
using FingerMonster.Gameplay;

namespace FingerMonster.Player
{
    /// <summary>
    /// 3D Humanoid karakterlerin Animator durum makinesini, oturma/tuşa uzanma IK hedeflerini,
    /// tek elli / elsiz (kafayla basma) tepkilerini, 16 karakter emote'unu ve
    /// silaha özel çizgi film elenme animasyonlarını yöneten sürücü.
    /// </summary>
    public class CharacterAnimatorDriver : MonoBehaviour
    {
        [Header("Karakter Kimliği")]
        public CharacterArchetype Archetype = CharacterArchetype.Kabadayi;

        [Header("Humanoid Animator & IK")]
        [SerializeField] private Animator humanoidAnimator;
        [SerializeField] private Vector3 tableRestLeftOffset = new Vector3(-0.22f, 0.84f, 0.45f);
        [SerializeField] private Vector3 tableRestRightOffset = new Vector3(0.22f, 0.84f, 0.45f);
        [SerializeField] private float maxArmReach = 0.68f;

        [Header("Yedek Kemik Hiyerarşisi (Prosedürel Fallback)")]
        [SerializeField] private Transform hipsBone;
        [SerializeField] private Transform torsoBone;
        [SerializeField] private Transform headBone;
        [SerializeField] private Transform leftEye;
        [SerializeField] private Transform rightEye;
        [SerializeField] private Transform leftArmPivot;
        [SerializeField] private Transform rightArmPivot;
        [SerializeField] private Transform accessoryRoot;

        [Header("El Modelleri (Eski Sistem Yedeği)")]
        [SerializeField] private GameObject leftHandModel;
        [SerializeField] private GameObject rightHandModel;
        [SerializeField] private GameObject leftWristStump;
        [SerializeField] private GameObject rightWristStump;

        private Vector3 initialRootPos;
        private Quaternion initialRootRot = Quaternion.identity;
        private Vector3 initialRootScale = Vector3.one;

        private Vector3 initialTorsoPos;
        private Quaternion initialTorsoRot = Quaternion.identity;
        private Vector3 initialHeadPos;
        private Quaternion initialHeadRot = Quaternion.identity;
        private Quaternion initialLeftArmRot = Quaternion.identity;
        private Quaternion initialRightArmRot = Quaternion.identity;

        private static bool IsValidRotation(Quaternion q)
        {
            return float.IsFinite(q.x) && float.IsFinite(q.y) && float.IsFinite(q.z) && float.IsFinite(q.w) &&
                   (q.x != 0f || q.y != 0f || q.z != 0f || q.w != 0f);
        }

        private Coroutine activeActionRoutine;
        private Coroutine activeEmoteRoutine;
        private bool isEliminated = false;
        private bool hasLeftHand = true;
        private bool hasRightHand = true;
        private int remainingHands = 2;

        // IK State
        private bool isPressingWithIK = false;
        private bool pressUseRightHand = true;
        private float pressIKBlend = 0f;
        private Vector3 pressTargetWorldPos;
        private Vector3 lookTargetWorldPos;
        private bool hasCustomLookTarget = false;
        private bool isPlayingExclusiveClip = false;

        // Animator parameter hashes
        private static readonly int HashHandsCount = Animator.StringToHash("HandsCount");
        private static readonly int HashArchetypeId = Animator.StringToHash("ArchetypeId");
        private static readonly int HashIsThinking = Animator.StringToHash("IsThinking");
        private static readonly int HashIsEliminated = Animator.StringToHash("IsEliminated");
        private static readonly int HashEmoteId = Animator.StringToHash("EmoteId");
        private static readonly int HashEmoteSlot = Animator.StringToHash("EmoteSlot");
        private static readonly int HashPressKey = Animator.StringToHash("PressKey");
        private static readonly int HashJoy = Animator.StringToHash("Joy");
        private static readonly int HashShock = Animator.StringToHash("Shock");
        private static readonly int HashHandLost = Animator.StringToHash("HandLost");
        private static readonly int HashEliminated = Animator.StringToHash("Eliminated");
        private static readonly int HashEmote = Animator.StringToHash("Emote");
        private static readonly int HashReset = Animator.StringToHash("Reset");

        public bool IsPlayingEmote => activeEmoteRoutine != null;
        public Animator HumanoidAnimator => humanoidAnimator;

        private void Awake()
        {
            if (humanoidAnimator == null)
            {
                humanoidAnimator = GetComponentInChildren<Animator>();
            }

            if (humanoidAnimator != null)
            {
                var bridge = humanoidAnimator.GetComponent<CharacterIKBridge>();
                if (bridge == null) bridge = humanoidAnimator.gameObject.AddComponent<CharacterIKBridge>();
                bridge.SetDriver(this);
            }

            CaptureInitialTransforms();
            SyncAnimatorParameters();
        }

        private void Start()
        {
            if (!HasValidAnimatorController())
            {
                StartCoroutine(IdleBreathingLoop());
                StartCoroutine(EyeBlinkLoop());
            }
        }

        private bool HasValidAnimatorController()
        {
            return humanoidAnimator != null && humanoidAnimator.runtimeAnimatorController != null;
        }

        public void SetAnimator(Animator animator)
        {
            humanoidAnimator = animator;
            if (humanoidAnimator != null)
            {
                var bridge = humanoidAnimator.GetComponent<CharacterIKBridge>();
                if (bridge == null) bridge = humanoidAnimator.gameObject.AddComponent<CharacterIKBridge>();
                bridge.SetDriver(this);
            }
            SyncAnimatorParameters();
        }

        public void SetupBones(Transform hips, Transform torso, Transform head, Transform lArm, Transform rArm, Transform lEye, Transform rEye, Transform accessory)
        {
            hipsBone = hips;
            torsoBone = torso;
            headBone = head;
            leftArmPivot = lArm;
            rightArmPivot = rArm;
            leftEye = lEye;
            rightEye = rEye;
            accessoryRoot = accessory;
            CaptureInitialTransforms();
        }

        public void SetupHands(GameObject lHand, GameObject rHand, GameObject lStump, GameObject rStump)
        {
            leftHandModel = lHand;
            rightHandModel = rHand;
            leftWristStump = lStump;
            rightWristStump = rStump;

            if (leftWristStump != null) leftWristStump.SetActive(false);
            if (rightWristStump != null) rightWristStump.SetActive(false);
        }

        private void CaptureInitialTransforms()
        {
            initialRootPos = transform.localPosition;
            if (IsValidRotation(transform.localRotation)) initialRootRot = transform.localRotation;
            if (transform.localScale.sqrMagnitude > 0.001f) initialRootScale = transform.localScale;
            else initialRootScale = Vector3.one;

            if (torsoBone != null)
            {
                initialTorsoPos = torsoBone.localPosition;
                if (IsValidRotation(torsoBone.localRotation)) initialTorsoRot = torsoBone.localRotation;
            }
            if (headBone != null)
            {
                initialHeadPos = headBone.localPosition;
                if (IsValidRotation(headBone.localRotation)) initialHeadRot = headBone.localRotation;
            }
            if (leftArmPivot != null && IsValidRotation(leftArmPivot.localRotation)) initialLeftArmRot = leftArmPivot.localRotation;
            if (rightArmPivot != null && IsValidRotation(rightArmPivot.localRotation)) initialRightArmRot = rightArmPivot.localRotation;
        }

        private void SyncAnimatorParameters()
        {
            if (!HasValidAnimatorController()) return;
            humanoidAnimator.SetInteger(HashHandsCount, remainingHands);
            humanoidAnimator.SetInteger(HashArchetypeId, (int)Archetype);
            humanoidAnimator.SetBool(HashIsEliminated, isEliminated);
        }

        public void OnHandSevered(PlayerHand hand)
        {
            if (hand == PlayerHand.Left) hasLeftHand = false;
            else if (hand == PlayerHand.Right) hasRightHand = false;

            remainingHands = (hasLeftHand ? 1 : 0) + (hasRightHand ? 1 : 0);
            SyncAnimatorParameters();

            if (HasValidAnimatorController())
            {
                humanoidAnimator.SetTrigger(HashHandLost);
            }
        }

        public void ResetAnimatorState()
        {
            StopAllCoroutines();
            activeActionRoutine = null;
            activeEmoteRoutine = null;
            isEliminated = false;
            hasLeftHand = true;
            hasRightHand = true;
            remainingHands = 2;
            isPressingWithIK = false;
            pressIKBlend = 0f;
            isPlayingExclusiveClip = false;
            hasCustomLookTarget = false;

            transform.localPosition = initialRootPos;
            transform.localRotation = IsValidRotation(initialRootRot) ? initialRootRot : Quaternion.identity;
            transform.localScale = (initialRootScale.sqrMagnitude > 0.001f) ? initialRootScale : Vector3.one;

            ResetBonesToNeutral();

            if (HasValidAnimatorController())
            {
                humanoidAnimator.SetBool(HashIsThinking, false);
                humanoidAnimator.SetBool(HashIsEliminated, false);
                humanoidAnimator.SetInteger(HashHandsCount, 2);
                humanoidAnimator.SetTrigger(HashReset);
            }
            else
            {
                StartCoroutine(IdleBreathingLoop());
                StartCoroutine(EyeBlinkLoop());
            }
        }

        #region IK (MASAYA OTURMA VE TUŞA UZANMA)
        public void HandleAnimatorIK(int layerIndex)
        {
            if (humanoidAnimator == null || !humanoidAnimator.isHuman || isEliminated)
            {
                return;
            }

            // Bakış IK: hedef rakip, daktilo veya hedef tuşa bak (emote sırasında da hedefe bakar)
            Vector3 typewriterLook = hasCustomLookTarget
                ? lookTargetWorldPos
                : transform.TransformPoint(new Vector3(0f, 0.95f, 1.0f));
            humanoidAnimator.SetLookAtWeight(hasCustomLookTarget ? 0.85f : 0.55f, hasCustomLookTarget ? 0.30f : 0.15f, 0.75f, 0.25f, 0.5f);
            humanoidAnimator.SetLookAtPosition(typewriterLook);

            if (isPlayingExclusiveClip)
            {
                humanoidAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                humanoidAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                humanoidAnimator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                humanoidAnimator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
                return;
            }

            Transform leftShoulder = humanoidAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform rightShoulder = humanoidAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);

            Vector3 leftRestTarget = transform.TransformPoint(tableRestLeftOffset);
            Vector3 rightRestTarget = transform.TransformPoint(tableRestRightOffset);

            float leftWeight = hasLeftHand ? 0.55f : 0f;
            float rightWeight = hasRightHand ? 0.55f : 0f;
            Vector3 leftTarget = leftRestTarget;
            Vector3 rightTarget = rightRestTarget;

            if (isPressingWithIK && pressIKBlend > 0.01f && remainingHands > 0)
            {
                if (pressUseRightHand && hasRightHand)
                {
                    rightTarget = Vector3.Lerp(rightRestTarget, pressTargetWorldPos, pressIKBlend);
                    rightWeight = Mathf.Lerp(0.55f, 0.95f, pressIKBlend);
                }
                else if (hasLeftHand)
                {
                    leftTarget = Vector3.Lerp(leftRestTarget, pressTargetWorldPos, pressIKBlend);
                    leftWeight = Mathf.Lerp(0.55f, 0.95f, pressIKBlend);
                }
            }

            ApplyArmIK(AvatarIKGoal.LeftHand, leftShoulder, leftTarget, leftWeight);
            ApplyArmIK(AvatarIKGoal.RightHand, rightShoulder, rightTarget, rightWeight);
        }

        private void ApplyArmIK(AvatarIKGoal goal, Transform shoulderBone, Vector3 targetWorldPos, float weight)
        {
            if (weight <= 0.001f)
            {
                humanoidAnimator.SetIKPositionWeight(goal, 0f);
                humanoidAnimator.SetIKRotationWeight(goal, 0f);
                return;
            }

            Vector3 clampedTarget = targetWorldPos;
            if (shoulderBone != null)
            {
                Vector3 delta = targetWorldPos - shoulderBone.position;
                float dist = delta.magnitude;
                if (dist > maxArmReach && dist > 0.001f)
                {
                    clampedTarget = shoulderBone.position + (delta / dist) * maxArmReach;
                }
            }

            humanoidAnimator.SetIKPositionWeight(goal, weight);
            humanoidAnimator.SetIKRotationWeight(goal, weight * 0.45f);
            humanoidAnimator.SetIKPosition(goal, clampedTarget);
            humanoidAnimator.SetIKRotation(goal, transform.rotation * Quaternion.Euler(20f, 0f, 0f));
        }
        #endregion

        #region IDLE & NEFES ALMA DÖNGÜSÜ (FALLBACK)
        private IEnumerator IdleBreathingLoop()
        {
            while (!isEliminated)
            {
                if (activeActionRoutine == null && activeEmoteRoutine == null)
                {
                    float time = Time.time;
                    float breathSpeed = (Archetype == CharacterArchetype.Korkak) ? 5.5f : 2.5f;
                    float breathAmount = (Archetype == CharacterArchetype.Korkak) ? 0.04f : 0.02f;

                    if (torsoBone != null)
                    {
                        float breath = Mathf.Sin(time * breathSpeed) * breathAmount;
                        torsoBone.localPosition = initialTorsoPos + Vector3.up * breath;

                        if (Archetype == CharacterArchetype.Korkak)
                        {
                            float shake = Mathf.Sin(time * 30f) * 1.5f;
                            torsoBone.localRotation = initialTorsoRot * Quaternion.Euler(0, shake, 0);
                        }
                    }

                    if (headBone != null)
                    {
                        float nod = Mathf.Sin(time * (breathSpeed * 0.8f)) * 2f;
                        headBone.localRotation = initialHeadRot * Quaternion.Euler(nod, 0, 0);
                    }
                }
                yield return null;
            }
        }

        private IEnumerator EyeBlinkLoop()
        {
            while (!isEliminated)
            {
                yield return new WaitForSeconds(Random.Range(2.5f, 6.0f));

                if (leftEye != null && rightEye != null && activeEmoteRoutine == null)
                {
                    Vector3 origScale = leftEye.localScale;
                    leftEye.localScale = new Vector3(origScale.x, 0.05f, origScale.z);
                    rightEye.localScale = new Vector3(origScale.x, 0.05f, origScale.z);
                    yield return new WaitForSeconds(0.12f);
                    leftEye.localScale = origScale;
                    rightEye.localScale = origScale;
                }
            }
        }
        #endregion

        #region OYUN DURUM ANİMASYONLARI
        public void PlayThinkingAnimation(float duration, Vector3? targetLookWorld = null)
        {
            if (isEliminated) return;
            if (targetLookWorld.HasValue)
            {
                lookTargetWorldPos = targetLookWorld.Value;
                hasCustomLookTarget = true;
            }
            if (activeActionRoutine != null) StopCoroutine(activeActionRoutine);
            activeActionRoutine = StartCoroutine(ThinkingRoutine(duration));
        }

        private IEnumerator ThinkingRoutine(float duration)
        {
            if (HasValidAnimatorController())
            {
                humanoidAnimator.SetBool(HashIsThinking, true);
                yield return new WaitForSeconds(duration);
                humanoidAnimator.SetBool(HashIsThinking, false);
                hasCustomLookTarget = false;
                activeActionRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            Quaternion targetHeadRot = initialHeadRot * Quaternion.Euler(-10f, 15f, 5f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (headBone != null)
                {
                    headBone.localRotation = Quaternion.Slerp(initialHeadRot, targetHeadRot, Mathf.PingPong(elapsed * 2f, 1f));
                }
                yield return null;
            }

            hasCustomLookTarget = false;
            ResetBonesToNeutral();
            activeActionRoutine = null;
        }

        public void PlayPressKeyAnimation(int remainingHandsCount, System.Action onImpactCallback = null, Vector3? keyWorldPosition = null)
        {
            if (isEliminated)
            {
                onImpactCallback?.Invoke();
                return;
            }

            remainingHands = remainingHandsCount;
            SyncAnimatorParameters();

            if (keyWorldPosition.HasValue)
            {
                pressTargetWorldPos = keyWorldPosition.Value;
                lookTargetWorldPos = keyWorldPosition.Value;
                hasCustomLookTarget = true;
            }
            else
            {
                pressTargetWorldPos = transform.TransformPoint(new Vector3(0f, 0.90f, 0.85f));
            }

            if (activeActionRoutine != null) StopCoroutine(activeActionRoutine);
            activeActionRoutine = StartCoroutine(PressKeyRoutine(remainingHandsCount, onImpactCallback));
        }

        private IEnumerator PressKeyRoutine(int handsCount, System.Action onImpactCallback)
        {
            if (HasValidAnimatorController())
            {
                humanoidAnimator.SetBool(HashIsThinking, false);
                humanoidAnimator.SetInteger(HashHandsCount, handsCount);
                humanoidAnimator.SetTrigger(HashPressKey);

                pressUseRightHand = hasRightHand;
                isPressingWithIK = handsCount > 0;

                float dur = (handsCount == 0) ? 0.58f : 0.44f;
                float elapsed = 0f;
                bool fired = false;

                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / dur;
                    pressIKBlend = Mathf.Sin(t * Mathf.PI);

                    if (t >= 0.52f && !fired)
                    {
                        fired = true;
                        onImpactCallback?.Invoke();
                    }

                    yield return null;
                }

                isPressingWithIK = false;
                pressIKBlend = 0f;
                hasCustomLookTarget = false;
                activeActionRoutine = null;
                yield break;
            }

            if (handsCount >= 2) yield return StartCoroutine(TwoHandedPressRoutine(onImpactCallback));
            else if (handsCount == 1) yield return StartCoroutine(SingleHandPressRoutine(onImpactCallback));
            else yield return StartCoroutine(HeadSlamPressRoutine(onImpactCallback));

            ResetBonesToNeutral();
            activeActionRoutine = null;
        }

        private IEnumerator TwoHandedPressRoutine(System.Action onImpactCallback)
        {
            float elapsed = 0f;
            float dur = 0.45f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                if (torsoBone != null) torsoBone.localRotation = initialTorsoRot * Quaternion.Euler(Mathf.Sin(t * Mathf.PI) * 12f, 0, 0);
                if (rightArmPivot != null) rightArmPivot.localRotation = initialRightArmRot * Quaternion.Euler(Mathf.Sin(t * Mathf.PI) * -35f, 0, 0);
                if (t >= 0.5f && onImpactCallback != null) { onImpactCallback.Invoke(); onImpactCallback = null; }
                yield return null;
            }
        }

        private IEnumerator SingleHandPressRoutine(System.Action onImpactCallback)
        {
            float elapsed = 0f;
            float dur = 0.5f;
            Transform activeArm = (rightHandModel != null && rightHandModel.activeSelf) ? rightArmPivot : leftArmPivot;
            Quaternion initArmRot = (activeArm == rightArmPivot) ? initialRightArmRot : initialLeftArmRot;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                if (torsoBone != null) torsoBone.localRotation = initialTorsoRot * Quaternion.Euler(8f, Mathf.Sin(t * Mathf.PI) * 10f, 0);
                if (activeArm != null) activeArm.localRotation = initArmRot * Quaternion.Euler(Mathf.Sin(t * Mathf.PI) * -45f, 0, 0);
                if (t >= 0.5f && onImpactCallback != null) { onImpactCallback.Invoke(); onImpactCallback = null; }
                yield return null;
            }
        }

        private IEnumerator HeadSlamPressRoutine(System.Action onImpactCallback)
        {
            float elapsed = 0f;
            float dur = 0.6f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                if (torsoBone != null && headBone != null)
                {
                    if (t < 0.35f)
                    {
                        float back = (t / 0.35f) * -15f;
                        torsoBone.localRotation = initialTorsoRot * Quaternion.Euler(back, 0, 0);
                    }
                    else if (t < 0.6f)
                    {
                        float slam = Mathf.Lerp(-15f, 35f, (t - 0.35f) / 0.25f);
                        torsoBone.localRotation = initialTorsoRot * Quaternion.Euler(slam, 0, 0);
                        headBone.localRotation = initialHeadRot * Quaternion.Euler(slam * 0.8f, 0, 0);
                    }
                    else
                    {
                        torsoBone.localRotation = Quaternion.Slerp(torsoBone.localRotation, initialTorsoRot, (t - 0.6f) / 0.4f);
                        headBone.localRotation = Quaternion.Slerp(headBone.localRotation, initialHeadRot, (t - 0.6f) / 0.4f);
                    }
                }
                if (t >= 0.58f && onImpactCallback != null)
                {
                    onImpactCallback.Invoke();
                    onImpactCallback = null;
                }
                yield return null;
            }
        }

        public void PlayJoyReliefAnimation()
        {
            if (isEliminated) return;
            if (activeActionRoutine != null) StopCoroutine(activeActionRoutine);
            activeActionRoutine = StartCoroutine(JoyReliefRoutine());
        }

        private IEnumerator JoyReliefRoutine()
        {
            if (HasValidAnimatorController())
            {
                isPlayingExclusiveClip = true;
                humanoidAnimator.SetTrigger(HashJoy);
                yield return new WaitForSeconds(0.9f);
                isPlayingExclusiveClip = false;
                activeActionRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            float dur = 0.8f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                float bounce = Mathf.Sin(t * Mathf.PI * 3f) * 0.05f;
                if (torsoBone != null) torsoBone.localPosition = initialTorsoPos + Vector3.up * bounce;
                if (headBone != null) headBone.localRotation = initialHeadRot * Quaternion.Euler(-15f * Mathf.Sin(t * Mathf.PI), 0, 0);
                yield return null;
            }
            ResetBonesToNeutral();
            activeActionRoutine = null;
        }

        public void PlayShockAnimation()
        {
            if (isEliminated) return;
            if (activeActionRoutine != null) StopCoroutine(activeActionRoutine);
            activeActionRoutine = StartCoroutine(ShockRoutine());
        }

        private IEnumerator ShockRoutine()
        {
            if (HasValidAnimatorController())
            {
                isPlayingExclusiveClip = true;
                humanoidAnimator.SetTrigger(HashShock);
                yield return new WaitForSeconds(0.75f);
                isPlayingExclusiveClip = false;
                activeActionRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            float dur = 0.7f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float shake = Mathf.Sin(elapsed * 40f) * 6f;
                if (torsoBone != null) torsoBone.localRotation = initialTorsoRot * Quaternion.Euler(-10f, shake, 0);
                if (headBone != null) headBone.localRotation = initialHeadRot * Quaternion.Euler(-15f, -shake * 1.5f, 0);
                yield return null;
            }
            ResetBonesToNeutral();
            activeActionRoutine = null;
        }
        #endregion

        #region EMOTE SİSTEMİ (5 SLOT MIXAMO + 16 PROSEDÜREL)
        public void PlayEmoteSlot(int slotIndex1Based, Vector3? targetWorldPos = null, string overridePopupText = null, Color? overridePopupColor = null)
        {
            if (isEliminated) return;
            if (activeEmoteRoutine != null) StopCoroutine(activeEmoteRoutine);
            activeEmoteRoutine = StartCoroutine(EmoteSlotProcessRoutine(slotIndex1Based, targetWorldPos, overridePopupText, overridePopupColor));
        }

        private IEnumerator EmoteSlotProcessRoutine(int slotIndex1Based, Vector3? targetWorldPos, string overridePopupText, Color? overridePopupColor)
        {
            int clampedSlot = Mathf.Clamp(slotIndex1Based, 1, 5);
            float duration = 2.0f;

            EmoteType fallbackEmote = (EmoteType)((int)Archetype * 4 + Mathf.Clamp(clampedSlot - 1, 0, 3));

            Quaternion targetRootRot = initialRootRot;
            if (targetWorldPos.HasValue)
            {
                lookTargetWorldPos = targetWorldPos.Value + Vector3.up * 1.15f;
                hasCustomLookTarget = true;

                Vector3 flatDir = targetWorldPos.Value - transform.position;
                flatDir.y = 0f;
                if (flatDir.sqrMagnitude > 0.01f)
                {
                    Quaternion desiredWorld = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
                    Quaternion desiredLocal = transform.parent != null
                        ? Quaternion.Inverse(transform.parent.rotation) * desiredWorld
                        : desiredWorld;
                    float angle = Quaternion.Angle(initialRootRot, desiredLocal);
                    float maxTurn = Mathf.Min(angle, 32f);
                    targetRootRot = Quaternion.RotateTowards(initialRootRot, desiredLocal, maxTurn);
                }
            }

            if (HasValidAnimatorController())
            {
                isPlayingExclusiveClip = true;
                humanoidAnimator.SetInteger(HashEmoteSlot, clampedSlot);
                humanoidAnimator.SetInteger(HashEmoteId, (int)fallbackEmote);
                humanoidAnimator.SetTrigger(HashEmote);

                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    if (targetWorldPos.HasValue)
                    {
                        float blend = elapsed < 0.25f ? (elapsed / 0.25f) : (elapsed > duration - 0.3f ? (duration - elapsed) / 0.3f : 1f);
                        transform.localRotation = Quaternion.Slerp(initialRootRot, targetRootRot, Mathf.Clamp01(blend));
                    }
                    yield return null;
                }

                transform.localRotation = initialRootRot;
                hasCustomLookTarget = false;
                isPlayingExclusiveClip = false;
                activeEmoteRoutine = null;
                yield break;
            }

            float procElapsed = 0f;
            while (procElapsed < duration)
            {
                procElapsed += Time.deltaTime;
                float progress = procElapsed / duration;
                AnimateEmoteBones(fallbackEmote, progress);
                yield return null;
            }

            transform.localRotation = initialRootRot;
            hasCustomLookTarget = false;
            ResetBonesToNeutral();
            activeEmoteRoutine = null;
        }

        public void PlayEmote(EmoteType emote)
        {
            if (isEliminated) return;
            if (activeEmoteRoutine != null) StopCoroutine(activeEmoteRoutine);
            activeEmoteRoutine = StartCoroutine(EmoteProcessRoutine(emote));
        }

        private IEnumerator EmoteProcessRoutine(EmoteType emote)
        {
            float elapsed = 0f;
            float duration = 1.8f;

            if (HasValidAnimatorController())
            {
                isPlayingExclusiveClip = true;
                humanoidAnimator.SetInteger(HashEmoteSlot, ((int)emote % 4) + 1);
                humanoidAnimator.SetInteger(HashEmoteId, (int)emote);
                humanoidAnimator.SetTrigger(HashEmote);
                yield return new WaitForSeconds(duration);
                isPlayingExclusiveClip = false;
                activeEmoteRoutine = null;
                yield break;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                AnimateEmoteBones(emote, progress);
                yield return null;
            }

            ResetBonesToNeutral();
            activeEmoteRoutine = null;
        }

        private static void GetEmotePopupInfo(EmoteType emote, out string comicText, out Color popupColor)
        {
            comicText = "HA!";
            popupColor = Color.white;

            switch (emote)
            {
                case EmoteType.Kabadayi_ChestThump: comicText = "GEL BURAYA!"; popupColor = Color.red; break;
                case EmoteType.Kabadayi_BicepFlex: comicText = "KAS GÜCÜ!"; popupColor = Color.yellow; break;
                case EmoteType.Kabadayi_PointThreat: comicText = "SIRADAKİ SENSİN!"; popupColor = new Color(1f, 0.4f, 0.1f); break;
                case EmoteType.Kabadayi_BoomingLaugh: comicText = "MUAHAHA!"; popupColor = Color.red; break;

                case EmoteType.Korkak_TrembleBite: comicText = "AYY ANNEM!"; popupColor = Color.cyan; break;
                case EmoteType.Korkak_DuckTable: comicText = "GÖRMÜYORUM!"; popupColor = Color.gray; break;
                case EmoteType.Korkak_FakeCry: comicText = "ÜHÜ ÜHÜ!"; popupColor = Color.blue; break;
                case EmoteType.Korkak_ChickenMock: comicText = "GIDIK GIDIK!"; popupColor = Color.yellow; break;

                case EmoteType.Sinsi_FingerWag: comicText = "CIK CIK CIK!"; popupColor = Color.green; break;
                case EmoteType.Sinsi_SlowClap: comicText = "BRAVO DAHA İYİSİ YOK!"; popupColor = Color.green; break;
                case EmoteType.Sinsi_TongueOut: comicText = "YAA YAA!"; popupColor = Color.magenta; break;
                case EmoteType.Sinsi_HandInspect: comicText = "ELİNE NE OLDU?"; popupColor = Color.green; break;

                case EmoteType.Sovmen_HatBow: comicText = "TEŞEKKÜRLER!"; popupColor = Color.yellow; break;
                case EmoteType.Sovmen_JazzHands: comicText = "ŞOV BAŞLASIN!"; popupColor = Color.cyan; break;
                case EmoteType.Sovmen_ApplausePlease: comicText = "ALKIŞŞŞ!"; popupColor = Color.white; break;
                case EmoteType.Sovmen_VictoryPose: comicText = "ZAFER BENİM!"; popupColor = Color.yellow; break;
            }
        }

        private void AnimateEmoteBones(EmoteType emote, float progress)
        {
            float wave = Mathf.Sin(progress * Mathf.PI * 4f);
            switch (emote)
            {
                case EmoteType.Kabadayi_ChestThump:
                    if (rightArmPivot != null) rightArmPivot.localRotation = initialRightArmRot * Quaternion.Euler(-30f + wave * 25f, 20f, 0);
                    if (torsoBone != null) torsoBone.localPosition = initialTorsoPos + Vector3.forward * (Mathf.Abs(wave) * 0.04f);
                    break;
                case EmoteType.Korkak_TrembleBite:
                    if (headBone != null) headBone.localRotation = initialHeadRot * Quaternion.Euler(15f, Mathf.Sin(progress * 50f) * 10f, 0);
                    if (torsoBone != null) torsoBone.localPosition = initialTorsoPos + Random.insideUnitSphere * 0.03f;
                    break;
                case EmoteType.Sinsi_FingerWag:
                    if (rightArmPivot != null) rightArmPivot.localRotation = initialRightArmRot * Quaternion.Euler(-35f, 0, wave * 20f);
                    if (headBone != null) headBone.localRotation = initialHeadRot * Quaternion.Euler(-5f, wave * 15f, 0);
                    break;
                case EmoteType.Sovmen_HatBow:
                    if (torsoBone != null) torsoBone.localRotation = initialTorsoRot * Quaternion.Euler(Mathf.Sin(progress * Mathf.PI) * 40f, 0, 0);
                    if (accessoryRoot != null) accessoryRoot.localPosition = Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.15f);
                    break;
                default:
                    if (headBone != null) headBone.localRotation = initialHeadRot * Quaternion.Euler(0, wave * 20f, 0);
                    break;
            }
        }
        #endregion

        #region KASAP İNFAZ ELENME ANİMASYONLARI
        public void PlayEliminationByWeapon(int weaponIndex)
        {
            isEliminated = true;
            isPressingWithIK = false;
            isPlayingExclusiveClip = true;

            if (activeActionRoutine != null) StopCoroutine(activeActionRoutine);
            if (activeEmoteRoutine != null) StopCoroutine(activeEmoteRoutine);

            if (HasValidAnimatorController())
            {
                humanoidAnimator.SetBool(HashIsEliminated, true);
                humanoidAnimator.SetTrigger(HashEliminated);
            }

            switch (weaponIndex)
            {
                case 0: StartCoroutine(FlattenPancakeRoutine()); break;
                case 1: StartCoroutine(SpinAndKnockoutRoutine()); break;
                case 2: StartCoroutine(WhirlwindSpinRoutine()); break;
                case 3:
                default: StartCoroutine(CreamPieSlipRoutine()); break;
            }
        }

        private IEnumerator FlattenPancakeRoutine()
        {
            float elapsed = 0f;
            float dur = 0.35f;
            Vector3 initScale = transform.localScale;
            Vector3 pancakeScale = new Vector3(initScale.x * 1.65f, initScale.y * 0.18f, initScale.z * 1.65f);
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(initScale, pancakeScale, elapsed / dur);
                yield return null;
            }
            transform.localScale = pancakeScale;
        }

        private IEnumerator SpinAndKnockoutRoutine()
        {
            float elapsed = 0f;
            float dur = 0.8f;
            Quaternion initRot = transform.localRotation;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                transform.localRotation = initRot * Quaternion.Euler(-75f * t, t * 180f, 0);
                transform.localPosition += Vector3.down * (Time.deltaTime * 0.35f);
                yield return null;
            }
        }

        private IEnumerator WhirlwindSpinRoutine()
        {
            float elapsed = 0f;
            float dur = 0.9f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                transform.Rotate(Vector3.up, 900f * Time.deltaTime, Space.World);
                yield return null;
            }
            transform.localRotation = Quaternion.Euler(-85f, 0, 0);
        }

        private IEnumerator CreamPieSlipRoutine()
        {
            float elapsed = 0f;
            float dur = 0.6f;
            Vector3 initLocalPos = transform.localPosition;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                transform.localPosition = initLocalPos + (-Vector3.forward) * (t * 0.55f) + Vector3.down * (t * 0.35f);
                transform.localRotation = initialRootRot * Quaternion.Euler(-45f * t, 0, 0);
                yield return null;
            }
        }
        #endregion

        private void ResetBonesToNeutral()
        {
            if (torsoBone != null)
            {
                torsoBone.localPosition = initialTorsoPos;
                torsoBone.localRotation = IsValidRotation(initialTorsoRot) ? initialTorsoRot : Quaternion.identity;
            }
            if (headBone != null)
            {
                headBone.localPosition = initialHeadPos;
                headBone.localRotation = IsValidRotation(initialHeadRot) ? initialHeadRot : Quaternion.identity;
            }
            if (leftArmPivot != null) leftArmPivot.localRotation = IsValidRotation(initialLeftArmRot) ? initialLeftArmRot : Quaternion.identity;
            if (rightArmPivot != null) rightArmPivot.localRotation = IsValidRotation(initialRightArmRot) ? initialRightArmRot : Quaternion.identity;
        }
    }
}
