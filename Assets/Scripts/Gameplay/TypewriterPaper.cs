using System.Collections;
using UnityEngine;
using TMPro;

namespace FingerMonster.Gameplay
{
    /// <summary>
    /// Daktilonun silindir kâğıt beslemesini temsil eder.
    /// Güvenli tuşlara basıldıkça daktilo gibi harfleri kâğıda döker.
    /// </summary>
    public class TypewriterPaper : MonoBehaviour
    {
        [Header("Metin Alanı")]
        [SerializeField] private TextMeshPro paperTextMesh;
        [SerializeField] private int maxLineLength = 12;
        [SerializeField] private int maxLines = 4;

        private string currentDocument = "";
        private int currentLineCharCount = 0;
        private int currentLines = 1;
        private Vector3 initialLocalPos;
        private Quaternion initialLocalRot = Quaternion.identity;
        private Coroutine flyAwayRoutine;

        private void Awake()
        {
            initialLocalPos = transform.localPosition;
            initialLocalRot = transform.localRotation;

            if (paperTextMesh == null)
            {
                paperTextMesh = GetComponentInChildren<TextMeshPro>();
            }
            ClearPaper();
        }

        /// <summary>
        /// Kâğıda yeni bir harf yazar ve satır sonu kontrolü yapar.
        /// </summary>
        public void AppendCharacter(char c)
        {
            if (currentLines >= maxLines && currentLineCharCount >= maxLineLength)
            {
                // Kâğıt dolduysa en eski satırı kaydır
                int firstNewline = currentDocument.IndexOf('\n');
                if (firstNewline >= 0)
                {
                    currentDocument = currentDocument.Substring(firstNewline + 1);
                    currentLines--;
                }
                else
                {
                    currentDocument = "";
                    currentLines = 1;
                }
            }

            if (currentLineCharCount >= maxLineLength)
            {
                currentDocument += "\n";
                currentLineCharCount = 0;
                currentLines++;
            }

            currentDocument += c;
            currentLineCharCount++;

            UpdateDisplay();
        }

        /// <summary>
        /// Kâğıdı temizler ve yeni tura hazırlar.
        /// </summary>
        public void ClearPaper()
        {
            if (flyAwayRoutine != null)
            {
                StopCoroutine(flyAwayRoutine);
                flyAwayRoutine = null;
            }
            transform.localPosition = initialLocalPos;
            transform.localRotation = initialLocalRot;

            currentDocument = "";
            currentLineCharCount = 0;
            currentLines = 1;
            UpdateDisplay();
        }

        /// <summary>
        /// Kâğıdı hafifçe yukarı ve yana savurup 1.2 saniye sonra yumuşakça yerine geri indirir.
        /// </summary>
        public void PlayPaperFlyAway()
        {
            if (flyAwayRoutine != null)
            {
                StopCoroutine(flyAwayRoutine);
                transform.localPosition = initialLocalPos;
                transform.localRotation = initialLocalRot;
            }
            flyAwayRoutine = StartCoroutine(PaperFlyAwayRoutine());
        }

        private IEnumerator PaperFlyAwayRoutine()
        {
            Vector3 peakPos = initialLocalPos + new Vector3(0.14f, 0.24f, -0.08f);
            Quaternion peakRot = initialLocalRot * Quaternion.Euler(-16f, 18f, -12f);

            float upDuration = 0.28f;
            float hoverDuration = 0.32f;
            float returnDuration = 0.60f;

            float elapsed = 0f;
            while (elapsed < upDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / upDuration));
                transform.localPosition = Vector3.Lerp(initialLocalPos, peakPos, t);
                transform.localRotation = Quaternion.Slerp(initialLocalRot, peakRot, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < hoverDuration)
            {
                elapsed += Time.deltaTime;
                float sway = Mathf.Sin(elapsed * 10f) * 0.015f;
                transform.localPosition = peakPos + new Vector3(sway, Mathf.Abs(sway) * 0.5f, 0f);
                yield return null;
            }

            Vector3 startReturnPos = transform.localPosition;
            Quaternion startReturnRot = transform.localRotation;
            elapsed = 0f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / returnDuration));
                transform.localPosition = Vector3.Lerp(startReturnPos, initialLocalPos, t);
                transform.localRotation = Quaternion.Slerp(startReturnRot, initialLocalRot, t);
                yield return null;
            }

            transform.localPosition = initialLocalPos;
            transform.localRotation = initialLocalRot;
            flyAwayRoutine = null;
        }

        private void UpdateDisplay()
        {
            if (paperTextMesh != null)
            {
                paperTextMesh.text = currentDocument;
            }
        }
    }
}

