using System.Collections;
using UnityEngine;
using FingerMonster.Audio;
using FingerMonster.Player;

namespace FingerMonster.Gameplay
{
    /// <summary>
    /// İki elini kaybeden oyuncunun arkasında beliren önlüklü, komik kasap karakteri.
    /// 4 absürt çizgi film silahından (Dev Tokmak, Döküm Tava, Sosis Kırbacı, Kremalı Pasta)
    /// birini kuşanarak silaha özel darbe ve elenme sekansını yönetir.
    /// </summary>
    public class ButcherPlaceholder : MonoBehaviour
    {
        [Header("Görsel Parçalar")]
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Transform weaponArmPivot;

        [Header("Absürt İnfaz Silahları (4 Adet)")]
        [SerializeField] private GameObject[] weaponModels;

        private Coroutine activeRoutine;
        private int currentWeaponIndex = 0;

        public int CurrentWeaponIndex => currentWeaponIndex;

        private void Awake()
        {
            if (visualRoot == null) visualRoot = gameObject;
            Hide();
        }

        /// <summary>
        /// Kasabı kurbanın arkasında oluşturur, silahı seçer ve infaz sekansını başlatır.
        /// </summary>
        public void AppearAt(Transform spawnAnchor, int victimSeatIndex = 0, int weaponIndex = -1, System.Action onStrikeCallback = null)
        {
            if (spawnAnchor != null)
            {
                transform.position = spawnAnchor.position;
                transform.rotation = spawnAnchor.rotation;
            }

            // Silah seçimi (rastgele veya belirtilen)
            currentWeaponIndex = (weaponIndex >= 0 && weaponIndex < 4) ? weaponIndex : Random.Range(0, 4);
            SelectWeapon(currentWeaponIndex);

            if (visualRoot != null)
            {
                visualRoot.SetActive(true);
            }

            // Karanlık ve yansıma sistemini tetikle
            if (ExecutionReflectionSystem.Instance != null)
            {
                // Kurban yerel oyuncu mu?
                bool isLocalVictim = false;
                if (Networking.NetworkGameManager.Instance != null)
                {
                    isLocalVictim = (Networking.NetworkGameManager.Instance.LocalSeatIndex == victimSeatIndex);
                }
                else if (Core.GameManager.Instance != null)
                {
                    isLocalVictim = (victimSeatIndex == 0 && !Core.GameManager.Instance.IsBotPlayer(0));
                }

                ExecutionReflectionSystem.Instance.StartExecutionSequence(victimSeatIndex, isLocalVictim);
            }

            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(SmashAnimationRoutine(currentWeaponIndex, onStrikeCallback));
        }

        private void SelectWeapon(int index)
        {
            if (weaponModels == null || weaponModels.Length == 0) return;

            for (int i = 0; i < weaponModels.Length; i++)
            {
                if (weaponModels[i] != null)
                {
                    weaponModels[i].SetActive(i == index);
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

            if (visualRoot != null)
            {
                visualRoot.SetActive(false);
            }

            if (ExecutionReflectionSystem.Instance != null)
            {
                ExecutionReflectionSystem.Instance.EndExecutionSequence();
            }
        }

        private IEnumerator SmashAnimationRoutine(int weaponIdx, System.Action onStrikeCallback)
        {
            ExecutionWeaponData weaponData = ExecutionWeaponData.GetData((ExecutionWeaponType)weaponIdx);

            if (weaponArmPivot == null) yield break;

            Quaternion initialArmRot = Quaternion.identity;
            Quaternion windupRot = Quaternion.Euler(weaponData.WindupAngle);
            Quaternion smashRot = Quaternion.Euler(weaponData.StrikeAngle);

            // 1. AŞAMA: Silahı havaya kaldırıp tereddütle titretme (Wind-up & Antics)
            float windupElapsed = 0f;
            float windupDuration = weaponData.WindupDuration;

            // Kasap kısık bir kıkırdama sesi çıkarır
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayButcherGiggle();
            }

            while (windupElapsed < windupDuration)
            {
                windupElapsed += Time.deltaTime;
                float t = windupElapsed / windupDuration;
                float shake = Mathf.Sin(windupElapsed * 40f) * 4.5f;

                weaponArmPivot.localRotation = Quaternion.Euler(
                    weaponData.WindupAngle.x * t + shake,
                    weaponData.WindupAngle.y * t,
                    weaponData.WindupAngle.z * t
                );
                yield return null;
            }

            // 2. AŞAMA: Ani ve abartılı çizgi film vuruşu (Fast smash)
            float smashElapsed = 0f;
            float smashDuration = weaponData.SmashDuration;

            while (smashElapsed < smashDuration)
            {
                smashElapsed += Time.deltaTime;
                float t = smashElapsed / smashDuration;
                weaponArmPivot.localRotation = Quaternion.Slerp(windupRot, smashRot, t * t);
                yield return null;
            }

            weaponArmPivot.localRotation = smashRot;

            // DARBE ANI: Silaha özel ses, efekt ve kurbanın silaha özel elenme animasyonu!
            PlayWeaponImpactSound(weaponData.WeaponType);

            if (CartoonFXManager.Instance != null)
            {
                Vector3 impactPos = transform.position + transform.forward * 0.7f + Vector3.up * 0.5f;
                CartoonFXManager.Instance.PlayButcherStrikeFX(impactPos);
                CartoonFXManager.Instance.SpawnComicPopup(impactPos + Vector3.up * 0.3f, weaponData.ComicHitText, weaponData.ComicColor);
            }

            // Kurbanın silaha özel tepki vermesi
            onStrikeCallback?.Invoke();

            // 3. AŞAMA: Geri çekilme ve bitiş
            yield return new WaitForSeconds(0.8f);

            float retractElapsed = 0f;
            float retractDur = 0.5f;
            while (retractElapsed < retractDur)
            {
                retractElapsed += Time.deltaTime;
                float t = retractElapsed / retractDur;
                weaponArmPivot.localRotation = Quaternion.Slerp(smashRot, initialArmRot, t);
                yield return null;
            }

            weaponArmPivot.localRotation = initialArmRot;
            Hide();
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
            weaponArmPivot = pivot;
            weaponModels = weapons;
        }
    }
}
