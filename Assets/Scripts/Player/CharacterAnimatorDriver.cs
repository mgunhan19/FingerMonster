using System.Collections;
using UnityEngine;
using FingerMonster.Audio;
using FingerMonster.Gameplay;

namespace FingerMonster.Player
{
    /// <summary>
    /// Karakterin duruşunu, nefes alma döngüsünü, tuşa basma (2 el, 1 el veya kafayla basma),
    /// göz kırpma, şok, sevinç, 16 farklı emote'u ve silaha özel elenme animasyonlarını yöneten motor.
    /// </summary>
    public class CharacterAnimatorDriver : MonoBehaviour
    {
        [Header("Karakter Kimliği")]
        public CharacterArchetype Archetype = CharacterArchetype.Kabadayi;

        [Header("Kemik ve Eklem Hiyerarşisi")]
        [SerializeField] private Transform hipsBone;
        [SerializeField] private Transform torsoBone;
        [SerializeField] private Transform headBone;
        [SerializeField] private Transform leftEye;
        [SerializeField] private Transform rightEye;
        [SerializeField] private Transform leftArmPivot;
        [SerializeField] private Transform rightArmPivot;
        [SerializeField] private Transform accessoryRoot; // Şapka, gözlük vb.

        [Header("El Modelleri")]
        [SerializeField] private GameObject leftHandModel;
        [SerializeField] private GameObject rightHandModel;
        [SerializeField] private GameObject leftWristStump;
        [SerializeField] private GameObject rightWristStump;

        private Vector3 initialTorsoPos;
        private Quaternion initialTorsoRot;
        private Vector3 initialHeadPos;
        private Quaternion initialHeadRot;
        private Quaternion initialLeftArmRot;
        private Quaternion initialRightArmRot;

        private Coroutine activeActionRoutine;
        private Coroutine activeEmoteRoutine;
        private bool isEliminated = false;
        private int remainingHands = 2;

        public bool IsPlayingEmote => activeEmoteRoutine != null;

        private void Awake()
        {
            CaptureInitialTransforms();
        }

        private void Start()
        {
            StartCoroutine(IdleBreathingLoop());
            StartCoroutine(EyeBlinkLoop());
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
            if (torsoBone != null)
            {
                initialTorsoPos = torsoBone.localPosition;
                initialTorsoRot = torsoBone.localRotation;
            }
            if (headBone != null)
            {
                initialHeadPos = headBone.localPosition;
                initialHeadRot = headBone.localRotation;
            }
            if (leftArmPivot != null) initialLeftArmRot = leftArmPivot.localRotation;
            if (rightArmPivot != null) initialRightArmRot = rightArmPivot.localRotation;
        }

        #region IDLE & NEFES ALMA DÖNGÜSÜ
        private IEnumerator IdleBreathingLoop()
        {
            while (!isEliminated)
            {
                if (activeActionRoutine == null && activeEmoteRoutine == null)
                {
                    float time = Time.time;
                    float breathSpeed = (Archetype == CharacterArchetype.Korkak) ? 5.5f : 2.5f;
                    float breathAmount = (Archetype == CharacterArchetype.Korkak) ? 0.04f : 0.02f;

                    // Nefes alma yaylanması
                    if (torsoBone != null)
                    {
                        float breath = Mathf.Sin(time * breathSpeed) * breathAmount;
                        torsoBone.localPosition = initialTorsoPos + Vector3.up * breath;

                        // Korkak karakterde sürekli titreme
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
        /// <summary>
        /// Karakterin düşünme / karar verme pozu.
        /// </summary>
        public void PlayThinkingAnimation(float duration)
        {
            if (activeActionRoutine != null) StopCoroutine(activeActionRoutine);
            activeActionRoutine = StartCoroutine(ThinkingRoutine(duration));
        }

        private IEnumerator ThinkingRoutine(float duration)
        {
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

            ResetBonesToNeutral();
            activeActionRoutine = null;
        }

        /// <summary>
        /// Tuşa basma eylemi: Kalan el sayısına göre 2 elle, tek elle veya KAFASIYLA basar!
        /// </summary>
        public void PlayPressKeyAnimation(int remainingHandsCount, System.Action onImpactCallback = null)
        {
            remainingHands = remainingHandsCount;
            if (activeActionRoutine != null) StopCoroutine(activeActionRoutine);
            activeActionRoutine = StartCoroutine(PressKeyRoutine(remainingHandsCount, onImpactCallback));
        }

        private IEnumerator PressKeyRoutine(int handsCount, System.Action onImpactCallback)
        {
            if (handsCount >= 2)
            {
                // İki elle ileri yaylanarak basma
                yield return StartCoroutine(TwoHandedPressRoutine(onImpactCallback));
            }
            else if (handsCount == 1)
            {
                // Kalan tek elle sert basma
                yield return StartCoroutine(SingleHandPressRoutine(onImpactCallback));
            }
            else
            {
                // ELİ YOK: Masaya kafasını küt diye vurarak basar (Komik cartoon gag!)
                yield return StartCoroutine(HeadSlamPressRoutine(onImpactCallback));
            }

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

                if (torsoBone != null)
                {
                    float lean = Mathf.Sin(t * Mathf.PI) * 12f;
                    torsoBone.localRotation = initialTorsoRot * Quaternion.Euler(lean, 0, 0);
                }

                if (rightArmPivot != null)
                {
                    float reach = Mathf.Sin(t * Mathf.PI) * -35f;
                    rightArmPivot.localRotation = initialRightArmRot * Quaternion.Euler(reach, 0, 0);
                }

                if (t >= 0.5f && onImpactCallback != null)
                {
                    onImpactCallback.Invoke();
                    onImpactCallback = null;
                }

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

                if (torsoBone != null)
                {
                    float sideLean = Mathf.Sin(t * Mathf.PI) * 10f;
                    torsoBone.localRotation = initialTorsoRot * Quaternion.Euler(8f, sideLean, 0);
                }

                if (activeArm != null)
                {
                    float reach = Mathf.Sin(t * Mathf.PI) * -45f;
                    activeArm.localRotation = initArmRot * Quaternion.Euler(reach, 0, 0);
                }

                if (t >= 0.5f && onImpactCallback != null)
                {
                    onImpactCallback.Invoke();
                    onImpactCallback = null;
                }

                yield return null;
            }
        }

        private IEnumerator HeadSlamPressRoutine(System.Action onImpactCallback)
        {
            // Elsiz karakter önce arkaya gerilir, sonra kafasını masaya yapıştırır
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
                        // Geriye yaylanma (Anticipation)
                        float back = (t / 0.35f) * -15f;
                        torsoBone.localRotation = initialTorsoRot * Quaternion.Euler(back, 0, 0);
                    }
                    else if (t < 0.6f)
                    {
                        // Küt diye masaya kafa atma!
                        float slam = Mathf.Lerp(-15f, 35f, (t - 0.35f) / 0.25f);
                        torsoBone.localRotation = initialTorsoRot * Quaternion.Euler(slam, 0, 0);
                        headBone.localRotation = initialHeadRot * Quaternion.Euler(slam * 0.8f, 0, 0);
                    }
                    else
                    {
                        // Sersemlenerek geri doğrulma
                        torsoBone.localRotation = Quaternion.Slerp(torsoBone.localRotation, initialTorsoRot, (t - 0.6f) / 0.4f);
                        headBone.localRotation = Quaternion.Slerp(headBone.localRotation, initialHeadRot, (t - 0.6f) / 0.4f);
                    }
                }

                if (t >= 0.58f && onImpactCallback != null)
                {
                    onImpactCallback.Invoke();
                    onImpactCallback = null;

                    // Kafa atma "DÖNK!" sesi
                    if (CartoonFXManager.Instance != null && headBone != null)
                    {
                        CartoonFXManager.Instance.SpawnComicPopup(headBone.position + Vector3.up * 0.3f, "DÖNK!", Color.yellow);
                    }
                }

                yield return null;
            }
        }

        /// <summary>
        /// Güvenli tuş sonrası sevinç ve rahatlama jesti.
        /// </summary>
        public void PlayJoyReliefAnimation()
        {
            if (activeActionRoutine != null) StopCoroutine(activeActionRoutine);
            activeActionRoutine = StartCoroutine(JoyReliefRoutine());
        }

        private IEnumerator JoyReliefRoutine()
        {
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

        /// <summary>
        /// Tuzak veya rakip elenmesi anında komik şok/korku tepkisi.
        /// </summary>
        public void PlayShockAnimation()
        {
            if (activeActionRoutine != null) StopCoroutine(activeActionRoutine);
            activeActionRoutine = StartCoroutine(ShockRoutine());
        }

        private IEnumerator ShockRoutine()
        {
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

        #region EMOTE SİSTEMİ (16+ HAREKET)
        /// <summary>
        /// Seçilen komik emote hareketini oynatır.
        /// </summary>
        public void PlayEmote(EmoteType emote)
        {
            if (activeEmoteRoutine != null) StopCoroutine(activeEmoteRoutine);
            activeEmoteRoutine = StartCoroutine(EmoteProcessRoutine(emote));
        }

        private IEnumerator EmoteProcessRoutine(EmoteType emote)
        {
            float elapsed = 0f;
            float duration = 1.8f;

            string comicText = "HA!";
            Color popupColor = Color.white;

            switch (emote)
            {
                case EmoteType.Kabadayi_ChestThump:
                    comicText = "GEL BURAYA!";
                    popupColor = Color.red;
                    break;
                case EmoteType.Kabadayi_BicepFlex:
                    comicText = "KAS GÜCÜ!";
                    popupColor = Color.yellow;
                    break;
                case EmoteType.Kabadayi_PointThreat:
                    comicText = "SIRADAKİ SENSİN!";
                    popupColor = new Color(1f, 0.4f, 0.1f);
                    break;
                case EmoteType.Kabadayi_BoomingLaugh:
                    comicText = "MUAHAHA!";
                    popupColor = Color.red;
                    break;

                case EmoteType.Korkak_TrembleBite:
                    comicText = "AYY ANNEM!";
                    popupColor = Color.cyan;
                    break;
                case EmoteType.Korkak_DuckTable:
                    comicText = "GÖRMÜYORUM!";
                    popupColor = Color.gray;
                    break;
                case EmoteType.Korkak_FakeCry:
                    comicText = "ÜHÜ ÜHÜ!";
                    popupColor = Color.blue;
                    break;
                case EmoteType.Korkak_ChickenMock:
                    comicText = "GIDIK GIDIK!";
                    popupColor = Color.yellow;
                    break;

                case EmoteType.Sinsi_FingerWag:
                    comicText = "CIK CIK CIK!";
                    popupColor = Color.green;
                    break;
                case EmoteType.Sinsi_SlowClap:
                    comicText = "BRAVO DAHA İYİSİ YOK!";
                    popupColor = Color.green;
                    break;
                case EmoteType.Sinsi_TongueOut:
                    comicText = "YAA YAA!";
                    popupColor = Color.magenta;
                    break;
                case EmoteType.Sinsi_HandInspect:
                    comicText = "ELİNE NE OLDU?";
                    popupColor = Color.green;
                    break;

                case EmoteType.Sovmen_HatBow:
                    comicText = "TEŞEKKÜRLER!";
                    popupColor = Color.yellow;
                    break;
                case EmoteType.Sovmen_JazzHands:
                    comicText = "ŞOV BAŞLASIN!";
                    popupColor = Color.cyan;
                    break;
                case EmoteType.Sovmen_ApplausePlease:
                    comicText = "ALKIŞŞŞ!";
                    popupColor = Color.white;
                    break;
                case EmoteType.Sovmen_VictoryPose:
                    comicText = "ZAFER BENİM!";
                    popupColor = Color.yellow;
                    break;
            }

            if (CartoonFXManager.Instance != null && headBone != null)
            {
                CartoonFXManager.Instance.SpawnComicPopup(headBone.position + Vector3.up * 0.4f, comicText, popupColor);
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                // Emote'a göre kemik animasyonları
                AnimateEmoteBones(emote, progress);

                yield return null;
            }

            ResetBonesToNeutral();
            activeEmoteRoutine = null;
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
                    if (torsoBone != null)
                    {
                        float bow = Mathf.Sin(progress * Mathf.PI) * 40f;
                        torsoBone.localRotation = initialTorsoRot * Quaternion.Euler(bow, 0, 0);
                    }
                    if (accessoryRoot != null)
                    {
                        accessoryRoot.localPosition = Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.15f);
                    }
                    break;

                default:
                    if (headBone != null) headBone.localRotation = initialHeadRot * Quaternion.Euler(0, wave * 20f, 0);
                    break;
            }
        }
        #endregion

        #region KASAP İNFAZ ELENME ANİMASYONLARI
        /// <summary>
        /// Kasabın seçtiği silaha özel komik çizgi film elenme animasyonunu oynatır.
        /// </summary>
        public void PlayEliminationByWeapon(int weaponIndex)
        {
            isEliminated = true;
            if (activeActionRoutine != null) StopCoroutine(activeActionRoutine);
            if (activeEmoteRoutine != null) StopCoroutine(activeEmoteRoutine);

            switch (weaponIndex)
            {
                case 0: // Dev Et Çekici -> Pancake gibi yassılaşma (Squash Flatten)
                    StartCoroutine(FlattenPancakeRoutine());
                    break;

                case 1: // Döküm Tava -> Yıldızlar eşliğinde sersemleyip arkaya devrilme
                    StartCoroutine(SpinAndKnockoutRoutine());
                    break;

                case 2: // Sosis Kırbacı -> Fırıldak gibi 720 derece dönme
                    StartCoroutine(WhirlwindSpinRoutine());
                    break;

                case 3: // Kremalı Pasta -> Pasta ile geriye kayarak düşme
                default:
                    StartCoroutine(CreamPieSlipRoutine());
                    break;
            }
        }

        private IEnumerator FlattenPancakeRoutine()
        {
            float elapsed = 0f;
            float dur = 0.35f;
            Vector3 initScale = transform.localScale;
            Vector3 pancakeScale = new Vector3(initScale.x * 1.7f, initScale.y * 0.15f, initScale.z * 1.7f);

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                transform.localScale = Vector3.Lerp(initScale, pancakeScale, t);
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
                transform.localRotation = initRot * Quaternion.Euler(-80f * t, t * 180f, 0);
                transform.localPosition += Vector3.down * (Time.deltaTime * 0.5f);
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

            transform.localRotation = Quaternion.Euler(-90f, 0, 0);
        }

        private IEnumerator CreamPieSlipRoutine()
        {
            float elapsed = 0f;
            float dur = 0.6f;
            Vector3 initPos = transform.position;

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                transform.position = initPos + Vector3.back * (t * 0.7f) + Vector3.down * (t * 0.4f);
                transform.localRotation = Quaternion.Euler(-45f * t, 0, 0);
                yield return null;
            }
        }
        #endregion

        private void ResetBonesToNeutral()
        {
            if (torsoBone != null)
            {
                torsoBone.localPosition = initialTorsoPos;
                torsoBone.localRotation = initialTorsoRot;
            }
            if (headBone != null)
            {
                headBone.localPosition = initialHeadPos;
                headBone.localRotation = initialHeadRot;
            }
            if (leftArmPivot != null) leftArmPivot.localRotation = initialLeftArmRot;
            if (rightArmPivot != null) rightArmPivot.localRotation = initialRightArmRot;
        }
    }
}

