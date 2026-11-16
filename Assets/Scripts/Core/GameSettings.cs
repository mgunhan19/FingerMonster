using System.Collections.Generic;
using UnityEngine;
using FingerMonster.Data;
using FingerMonster.Gameplay;

namespace FingerMonster.Core
{
    /// <summary>
    /// Oyun kuralları ve parametrelerini tutan ScriptableObject veri nesnesi.
    /// Inspector üzerinden kolayca dengelenebilir.
    /// </summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "FingerMonster/Game Settings")]
    public class GameSettings : ScriptableObject
    {
        [Header("Oyuncu ve Tuş Ayarları")]
        [Tooltip("Oyundaki toplam oyuncu sayısı (Aşama 1 için varsayılan 4)")]
        [Range(2, 4)]
        public int playerCount = 4;

        [Tooltip("Daktilo üzerindeki toplam basılabilir tuş sayısı")]
        [Range(10, 30)]
        public int totalKeys = 20;

        [Tooltip("Her turda daktiloya rastgele yerleştirilecek tuzak tuş sayısı")]
        [Range(1, 10)]
        public int trapCount = 6;

        [Header("Puanlama")]
        [Tooltip("Güvenli bir tuşa basıldığında kazanılan puan")]
        public int pointsPerSafeKey = 10;

        [Header("Zamanlamalar (Saniye)")]
        [Tooltip("Sıra geçişleri arasındaki bekleme")]
        public float turnTransitionDelay = 0.5f;

        [Tooltip("Tuş çözümlenme animasyon süresi")]
        public float keyResolveDelay = 0.4f;

        [Tooltip("El kaybetme dramatizasyonu süresi")]
        public float handLostDelay = 1.0f;

        [Tooltip("Kasap sekansı (ButcherSequence) süresi")]
        public float butcherSequenceDuration = 3.0f;

        [Tooltip("Kamera hareket lerp hızı")]
        public float cameraLerpSpeed = 4.0f;

        [Header("Daktilo Harfleri")]
        [Tooltip("Tuşlara atanacak alfabetik karakterler (en az 20 karakter)")]
        public string alphabetLetters = "QWERTYUIOPASDFGHJKLZ";

        [Header("Oyuncu Renkleri")]
        public Color[] playerColors = new Color[]
        {
            new Color(0.9f, 0.25f, 0.25f), // Kırmızı (P1)
            new Color(0.25f, 0.55f, 0.95f), // Mavi (P2)
            new Color(0.25f, 0.85f, 0.35f), // Yeşil (P3)
            new Color(0.95f, 0.75f, 0.2f)   // Sarı (P4)
        };

        [Header("Stratejik Yetenek Kartları (Ability Cards)")]
        [Tooltip("Bir oyuncunun elinde aynı anda tutabileceği maksimum yetenek kartı sayısı")]
        [Range(1, 4)]
        public int maxAbilityCardsInHand = 2;

        [Tooltip("Her yeni tur başında hayattaki oyunculara dağıtılan kart sayısı")]
        [Range(0, 2)]
        public int cardsDrawnPerRoundStart = 1;

        [Tooltip("3 kart seçiminde güvenli kart bulunup sıra atlandığında kazanılan kart sayısı")]
        [Range(0, 2)]
        public int cardsDrawnOnSafeCardPass = 1;

        [Tooltip("Turlar arasında oyuncuların elindeki yetenek kartları korunsun mu?")]
        public bool keepAbilityCardsBetweenRounds = true;

        [Tooltip("Yeni tur başladığında kopan eller geri gelsin mi? (Varsayılan: Hayır, eller kalıcıdır)")]
        public bool restoreHandsOnNewRound = false;

        [Header("Yetenek Kartı Çekiliş Ağırlıkları")]
        [Range(0, 100)] public int weightShield = 15;
        [Range(0, 100)] public int weightPossess = 22;
        [Range(0, 100)] public int weightXRay = 20;
        [Range(0, 100)] public int weightSkipTurn = 15;
        [Range(0, 100)] public int weightReverseDirection = 15;
        [Range(0, 100)] public int weightFog = 13;

        [Header("Hayalet (Kasabın Yardımcısı) Ayarları")]
        [Range(0, 3)] public int ghostStartSoulEnergy = 1;
        [Range(0, 3)] public int ghostEnergyPerRound = 1;
        [Range(1, 5)] public int ghostMaxSoulEnergy = 3;
        [Range(1, 5)] public int ghostCurseCost = 2;
        [Range(0, 3)] public int ghostEffectCost = 1;
        [Range(1f, 10f)] public float ghostWindowDuration = 4.0f;
        [Range(0.5f, 10f)] public float ghostEffectCooldown = 3.0f;
        [Range(1, 3)] public int maxCursesPerRound = 1;
        [Range(4f, 30f)] public float ghostOrbitSpeed = 12.0f;

        [Header("Hayalet Serbest Hareket")]
        [Range(1f, 12f)] public float ghostMoveSpeed = 4.2f;
        [Range(1f, 3f)] public float ghostSprintMultiplier = 1.65f;
        [Range(0.05f, 1f)] public float ghostLookSensitivity = 0.18f;
        [Range(0.2f, 4f)] public float ghostFadeDistance = 1.0f;
        [Range(0.05f, 0.8f)] public float ghostMinAlphaNearLiving = 0.15f;

        [Header("Hayalet Korkutma (\"Buuu!\")")]
        [Range(0, 3)] public int ghostScareCost = 1;
        [Range(1f, 20f)] public float ghostScareCooldown = 6f;
        [Range(1f, 10f)] public float ghostScareRange = 3.0f;
        [Range(1f, 15f)] public float ghostScareTargetImmunity = 5.0f;
        [Range(0.1f, 3f)] public float ghostScareEffectDuration = 0.6f;

        [Header("Hayalet Fırlatma")]
        [Range(0, 3)] public int ghostThrowCost = 1;
        [Range(1f, 20f)] public float ghostThrowCooldown = 5f;
        [Range(3f, 25f)] public float ghostThrowMaxRange = 12f;
        [Range(0.5f, 10f)] public float ghostSplatDuration = 3.0f;
        [Range(1f, 20f)] public float ghostDecalDuration = 8.0f;
        public List<ThrowableItemData> throwableItems = new List<ThrowableItemData>();

        [Header("Kasap Silahları")]
        public List<WeaponData> butcherWeapons = new List<WeaponData>();

        /// <summary>
        /// Fırlatılabilir nesne veya kasap silahı listeleri boşsa çalışma zamanı varsayılanlarıyla doldurur.
        /// </summary>
        public void EnsureRuntimeDefaults()
        {
            if (throwableItems == null)
            {
                throwableItems = new List<ThrowableItemData>();
            }
            if (throwableItems.Count == 0)
            {
                throwableItems.AddRange(ThrowableItemData.CreateRuntimeDefaultList());
            }

            if (butcherWeapons == null)
            {
                butcherWeapons = new List<WeaponData>();
            }
            if (butcherWeapons.Count == 0)
            {
                butcherWeapons.Add(WeaponData.CreateRuntimeDefault(ExecutionWeaponType.GiantMeatMallet));
                butcherWeapons.Add(WeaponData.CreateRuntimeDefault(ExecutionWeaponType.FlyingCastIronPan));
                butcherWeapons.Add(WeaponData.CreateRuntimeDefault(ExecutionWeaponType.SausageWhip));
                butcherWeapons.Add(WeaponData.CreateRuntimeDefault(ExecutionWeaponType.CreamPieLauncher));
            }
        }

        /// <summary>
        /// Ayarlanan ağırlıklara göre rastgele bir stratejik yetenek kartı türü seçer.
        /// </summary>
        public AbilityCardType DrawWeightedRandomAbilityCard()
        {
            int wShield = Mathf.Max(0, weightShield);
            int wPossess = Mathf.Max(0, weightPossess);
            int wXRay = Mathf.Max(0, weightXRay);
            int wSkip = Mathf.Max(0, weightSkipTurn);
            int wReverse = Mathf.Max(0, weightReverseDirection);
            int wFog = Mathf.Max(0, weightFog);

            int totalWeight = wShield + wPossess + wXRay + wSkip + wReverse + wFog;
            if (totalWeight <= 0)
            {
                return (AbilityCardType)Random.Range(0, 6);
            }

            int roll = Random.Range(0, totalWeight);

            if (roll < wShield) return AbilityCardType.Shield;
            roll -= wShield;

            if (roll < wPossess) return AbilityCardType.Possess;
            roll -= wPossess;

            if (roll < wXRay) return AbilityCardType.XRay;
            roll -= wXRay;

            if (roll < wSkip) return AbilityCardType.SkipTurn;
            roll -= wSkip;

            if (roll < wReverse) return AbilityCardType.ReverseDirection;
            return AbilityCardType.Fog;
        }
    }
}

