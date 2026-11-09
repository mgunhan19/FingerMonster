using System.Collections;
using UnityEngine;
using FingerMonster.Core;
using FingerMonster.Gameplay;

namespace FingerMonster.Player
{
    /// <summary>
    /// Masada oturan çizgi film tarzı oyuncu kuklasını (baş, gövde, masadaki sol ve sağ el) temsil eder.
    /// Karakterin animasyon motoru (CharacterAnimatorDriver) ve el kopma fiziğini (SeveredHandPhysics) koordine eder.
    /// </summary>
    public class PlayerVisualAvatar : MonoBehaviour
    {
        [Header("Karakter Kimliği")]
        public CharacterArchetype Archetype = CharacterArchetype.Kabadayi;

        [Header("Görsel Parçalar")]
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform torsoTransform;
        [SerializeField] private GameObject leftHandObj;
        [SerializeField] private GameObject rightHandObj;

        [Header("Yara Bantları (Bandages)")]
        [SerializeField] private GameObject leftWristBandage;
        [SerializeField] private GameObject rightWristBandage;

        [Header("Animatör Sürücüsü")]
        [SerializeField] private CharacterAnimatorDriver animatorDriver;

        [Header("Renkler")]
        [SerializeField] private Renderer[] playerColoredRenderers;

        private Vector3 initialHeadLocalPos;
        private Quaternion initialHeadLocalRot;
        private Vector3 initialTorsoLocalPos;
        private Quaternion initialTorsoLocalRot;
        private Vector3 initialLeftHandLocalPos;
        private Quaternion initialLeftHandLocalRot = Quaternion.identity;
        private Vector3 initialRightHandLocalPos;
        private Quaternion initialRightHandLocalRot = Quaternion.identity;
        private Color myColor = Color.white;

        public CharacterAnimatorDriver AnimatorDriver => animatorDriver;

        private void Awake()
        {
            if (animatorDriver == null) animatorDriver = GetComponent<CharacterAnimatorDriver>() ?? gameObject.AddComponent<CharacterAnimatorDriver>();

            if (headTransform != null)
            {
                initialHeadLocalPos = headTransform.localPosition;
                initialHeadLocalRot = headTransform.localRotation;
            }

            if (torsoTransform != null)
            {
                initialTorsoLocalPos = torsoTransform.localPosition;
                initialTorsoLocalRot = torsoTransform.localRotation;
            }

            if (leftHandObj != null)
            {
                initialLeftHandLocalPos = leftHandObj.transform.localPosition;
                initialLeftHandLocalRot = leftHandObj.transform.localRotation;
            }
            if (rightHandObj != null)
            {
                initialRightHandLocalPos = rightHandObj.transform.localPosition;
                initialRightHandLocalRot = rightHandObj.transform.localRotation;
            }

            ResetAvatar();
        }

        public void SetPlayerColor(Color color)
        {
            myColor = color;
            if (playerColoredRenderers == null || playerColoredRenderers.Length == 0)
            {
                playerColoredRenderers = GetComponentsInChildren<Renderer>();
            }

            foreach (var r in playerColoredRenderers)
            {
                if (r != null && !r.name.Contains("Bandage"))
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
        /// Belirtilen elin çizgi film fiziğiyle (SeveredHandPhysics) taklalar atarak masadan kopmasını sağlar.
        /// </summary>
        public void SeverHand(PlayerHand hand)
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

            if (targetBandage != null)
            {
                targetBandage.SetActive(true);
            }

            if (animatorDriver != null)
            {
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

        public void PlayPressKey(int remainingHandsCount, System.Action onImpactCallback = null)
        {
            if (animatorDriver != null)
            {
                animatorDriver.PlayPressKeyAnimation(remainingHandsCount, onImpactCallback);
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

        public void PlayThinking(float duration)
        {
            if (animatorDriver != null)
            {
                animatorDriver.PlayThinkingAnimation(duration);
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
            StopAllCoroutines();

            if (leftHandObj != null)
            {
                var phys = leftHandObj.GetComponent<SeveredHandPhysics>();
                if (phys != null)
                {
                    phys.ResetHand(transform, initialLeftHandLocalPos, initialLeftHandLocalRot);
                }
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
                if (phys != null)
                {
                    phys.ResetHand(transform, initialRightHandLocalPos, initialRightHandLocalRot);
                }
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
                headTransform.localRotation = initialHeadLocalRot;
            }

            if (torsoTransform != null)
            {
                torsoTransform.localPosition = initialTorsoLocalPos;
                torsoTransform.localRotation = initialTorsoLocalRot;
            }
        }
    }
}
