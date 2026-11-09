using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FingerMonster.Audio;

namespace FingerMonster.Gameplay
{
    /// <summary>
    /// Masanın ortasındaki daktilo makinesini, tuşları ve kâğıt kızağının (carriage)
    /// mekanik kayma ve satır başı geri dönüş animasyonunu yöneten sınıf.
    /// </summary>
    public class TypewriterMachine : MonoBehaviour
    {
        [Header("Bileşen Referansları")]
        [SerializeField] private TypewriterPaper paper;
        [SerializeField] private Transform carriageTransform;
        [SerializeField] private List<TypewriterKey> keys = new List<TypewriterKey>();

        public TypewriterPaper Paper => paper;
        public IReadOnlyList<TypewriterKey> Keys => keys;

        private Vector3 initialCarriagePos;
        private int carriageStep = 0;
        private const int MaxCarriageSteps = 10;
        private Coroutine carriageZipRoutine;

        private void Awake()
        {
            if (carriageTransform != null)
            {
                initialCarriagePos = carriageTransform.localPosition;
            }
        }

        /// <summary>
        /// Tuş dizisini harflerle hazırlar, eski engelleyici objeleri temizler ve tuş dizilimini doğrular.
        /// </summary>
        public void SetupKeys(string alphabetLetters)
        {
            // Varsa eski engelleyici KeyboardDeck objesini temizle
            Transform oldDeck = transform.Find("KeyboardDeck");
            if (oldDeck != null)
            {
                Destroy(oldDeck.gameObject);
            }

            if (keys == null || keys.Count == 0)
            {
                keys = new List<TypewriterKey>(GetComponentsInChildren<TypewriterKey>());
            }

            // Tuşların doğru, kademeli (staggered) ve masada belirgin pozisyonlarda olmasını garanti et
            float stepX = 0.094f;
            float topStartX = -4.5f * stepX;
            float bottomStartX = topStartX + (stepX * 0.5f);
            float[] rowZ = new float[] { -0.07f, -0.22f };
            float[] rowY = new float[] { 0.155f, 0.125f };

            for (int i = 0; i < keys.Count; i++)
            {
                char letter = (i < alphabetLetters.Length) ? alphabetLetters[i] : (char)('A' + i);

                int row = (i < 10) ? 0 : 1;
                int col = (i < 10) ? i : (i - 10);
                float rowStartX = (row == 0) ? topStartX : bottomStartX;
                Vector3 properPos = new Vector3(rowStartX + (col * stepX), rowY[row], rowZ[row]);

                // Eğer tuş (0,0,0)'a çökmüşse veya yanlış konumdaysa doğru koordinatına taşı
                if (keys[i].transform.localPosition.sqrMagnitude < 0.01f || Mathf.Abs(keys[i].transform.localPosition.z) < 0.02f)
                {
                    keys[i].transform.localPosition = properPos;
                    keys[i].transform.localRotation = Quaternion.Euler(16f, 0f, 0f);
                    keys[i].transform.localScale = new Vector3(0.082f, 0.038f, 0.082f);
                }

                keys[i].Initialize(i, letter);
            }

            ResetCarriage();

            if (paper != null)
            {
                paper.ClearPaper();
            }
        }

        /// <summary>
        /// Belirli bir tuşa basılmasını simüle eder.
        /// </summary>
        public void PressKey(int keyIndex, bool isTrap)
        {
            if (keyIndex < 0 || keyIndex >= keys.Count) return;

            TypewriterKey key = keys[keyIndex];
            key.AnimatePress(isTrap);

            if (!isTrap)
            {
                if (paper != null)
                {
                    paper.AppendCharacter(key.KeyChar);
                }
                AdvanceCarriage();
            }
        }

        private void AdvanceCarriage()
        {
            if (carriageTransform == null) return;

            carriageStep++;
            if (carriageStep >= MaxCarriageSteps)
            {
                // Satır sonu zili ve kızağın geri dönmesi
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBell();
                }
                ResetCarriage();
            }
            else
            {
                // Her harfte sola hafif mekanik kayma
                Vector3 targetPos = initialCarriagePos + new Vector3(-0.035f * carriageStep, 0, 0);
                carriageTransform.localPosition = targetPos;
            }
        }

        private void ResetCarriage()
        {
            carriageStep = 0;
            if (carriageTransform != null)
            {
                if (carriageZipRoutine != null) StopCoroutine(carriageZipRoutine);
                carriageZipRoutine = StartCoroutine(CarriageZipRoutine());
            }
        }

        private IEnumerator CarriageZipRoutine()
        {
            Vector3 current = carriageTransform.localPosition;
            float elapsed = 0f;
            float duration = 0.15f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                carriageTransform.localPosition = Vector3.Lerp(current, initialCarriagePos, elapsed / duration);
                yield return null;
            }

            carriageTransform.localPosition = initialCarriagePos;
        }

        /// <summary>
        /// Yeni turda tüm tuşları ve kâğıt kızağını başlangıç konumuna döndürür.
        /// </summary>
        public void ResetAllKeys()
        {
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] != null)
                {
                    keys[i].ResetKey();
                }
            }

            ResetCarriage();

            if (paper != null)
            {
                paper.ClearPaper();
            }
        }

        public TypewriterKey GetKey(int index)
        {
            if (index >= 0 && index < keys.Count) return keys[index];
            return null;
        }

        public void SetKeysList(List<TypewriterKey> newKeys)
        {
            keys = newKeys;
        }

        public void SetPaper(TypewriterPaper newPaper)
        {
            paper = newPaper;
        }

        public void SetCarriage(Transform carriage)
        {
            carriageTransform = carriage;
            if (carriageTransform != null)
            {
                initialCarriagePos = carriageTransform.localPosition;
            }
        }
    }
}
