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

        private void Awake()
        {
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
            currentDocument = "";
            currentLineCharCount = 0;
            currentLines = 1;
            UpdateDisplay();
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

