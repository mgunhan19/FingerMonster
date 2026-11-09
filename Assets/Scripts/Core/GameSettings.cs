using UnityEngine;

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
    }
}

