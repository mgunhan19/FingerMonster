using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using FingerMonster.Core;

namespace FingerMonster.Gameplay
{
    /// <summary>
    /// Daktilo üzerindeki tek bir harf tuşunun fiziksel etkileşimi, görseli ve durumunu yönetir.
    /// Hem EventSystem (IPointer...), hem Unity fizik (OnMouse...), hem de doğrudan raycast'i destekler.
    /// Üzerine gelindiğinde belirgin sarı neon ışıması ve büyüme efekti verir.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TypewriterKey : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Tuş Bilgileri")]
        [SerializeField] private int keyIndex = 0;
        [SerializeField] private char keyChar = 'A';
        public int KeyIndex => keyIndex;
        public char KeyChar => keyChar;
        public bool IsPressed { get; private set; } = false;

        [Header("Görsel Bileşenler")]
        [SerializeField] private Renderer keyRenderer;
        [SerializeField] private TextMeshPro labelText;
        [SerializeField] private Transform movingCap;

        [Header("Renkler")]
        [SerializeField] private Color normalColor = new Color(0.92f, 0.92f, 0.90f);
        [SerializeField] private Color hoverColor = new Color(1.0f, 0.95f, 0.15f); // Parlak Sarı
        [SerializeField] private Color safePressedColor = new Color(0.3f, 0.85f, 0.35f); // Yeşil
        [SerializeField] private Color trapPressedColor = new Color(0.95f, 0.2f, 0.2f); // Kırmızı

        private Material keyMaterial;
        private Vector3 initialCapLocalPos;
        private Vector3 initialLocalScale;
        private bool isHovered = false;

        private void Awake()
        {
            if (movingCap == null) movingCap = transform;
            if (initialCapLocalPos == Vector3.zero) initialCapLocalPos = movingCap.localPosition;
            if (initialLocalScale == Vector3.zero) initialLocalScale = transform.localScale;

            EnsureMaterial();

            if (labelText == null)
            {
                labelText = GetComponentInChildren<TextMeshPro>();
            }
        }

        private void EnsureMaterial()
        {
            if (keyRenderer == null) keyRenderer = GetComponent<Renderer>();
            if (keyMaterial == null && keyRenderer != null)
            {
                // Material kopyası oluştur ve ata
                keyMaterial = new Material(keyRenderer.sharedMaterial);
                keyRenderer.material = keyMaterial;
                SetMaterialVisuals(normalColor, isEmissive: false);
            }
        }

        private void SetMaterialVisuals(Color baseCol, bool isEmissive, Color? emissiveCol = null)
        {
            if (keyMaterial == null) EnsureMaterial();
            if (keyMaterial == null) return;

            keyMaterial.color = baseCol;

            if (keyMaterial.HasProperty("_BaseColor"))
            {
                keyMaterial.SetColor("_BaseColor", baseCol);
            }

            if (keyMaterial.HasProperty("_EmissionColor"))
            {
                if (isEmissive)
                {
                    keyMaterial.EnableKeyword("_EMISSION");
                    Color emit = emissiveCol ?? (baseCol * 1.8f);
                    keyMaterial.SetColor("_EmissionColor", emit);
                }
                else
                {
                    keyMaterial.DisableKeyword("_EMISSION");
                    keyMaterial.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        public void Initialize(int index, char character)
        {
            keyIndex = index;
            keyChar = character;
            IsPressed = false;

            if (movingCap == null) movingCap = transform;
            // Başlangıç transform değerlerini güvenle kaydet
            initialCapLocalPos = movingCap.localPosition;
            initialLocalScale = transform.localScale;

            EnsureMaterial();

            if (labelText == null)
            {
                labelText = GetComponentInChildren<TextMeshPro>();
            }

            if (labelText != null)
            {
                labelText.text = character.ToString();
            }

            ResetKey();
        }

        /// <summary>
        /// Fare üzerine geldiğinde veya gamepad ile odaklandığında tuşu vurgular.
        /// </summary>
        public void SetHovered(bool hovered)
        {
            if (IsPressed) return;
            isHovered = hovered;

            if (hovered)
            {
                // Parlak sarı + neon sarı ışıma + belirgin büyüme
                SetMaterialVisuals(hoverColor, isEmissive: true, emissiveCol: Color.yellow * 2.0f);
                if (initialLocalScale != Vector3.zero)
                {
                    transform.localScale = initialLocalScale * 1.15f;
                }
            }
            else
            {
                SetMaterialVisuals(normalColor, isEmissive: false);
                if (initialLocalScale != Vector3.zero)
                {
                    transform.localScale = initialLocalScale;
                }
            }
        }

        #region EVENT SYSTEM & MOUSE FALLBACKS
        public void OnPointerEnter(PointerEventData eventData) => HandleHover(true);
        public void OnPointerExit(PointerEventData eventData) => HandleHover(false);
        public void OnPointerClick(PointerEventData eventData) => HandleClick();

        private void OnMouseEnter() => HandleHover(true);
        private void OnMouseExit() => HandleHover(false);
        private void OnMouseDown() => HandleClick();

        public void HandleHover(bool hovered)
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.PlayerTurn)
            {
                if (!hovered) SetHovered(false);
                return;
            }
            SetHovered(hovered);
        }

        public void HandleClick()
        {
            if (IsPressed) return;
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.PlayerTurn)
            {
                return;
            }

            int activePlayer = GameManager.Instance.ActivePlayerIndex;
            if (GameManager.Instance.IsBotPlayer(activePlayer)) return;

            PressKeyCommand command = new PressKeyCommand(activePlayer, keyIndex);
            GameManager.Instance.ProcessCommand(command);
        }
        #endregion

        /// <summary>
        /// Tuşa basıldığında fiziksel çöküş ve renk değişimini gerçekleştirir.
        /// </summary>
        public void AnimatePress(bool isTrap)
        {
            IsPressed = true;
            isHovered = false;

            transform.localScale = initialLocalScale;
            Color pressedCol = isTrap ? trapPressedColor : safePressedColor;
            SetMaterialVisuals(pressedCol, isEmissive: true, emissiveCol: pressedCol * 1.4f);

            StopAllCoroutines();
            StartCoroutine(PressRoutine());
        }

        private IEnumerator PressRoutine()
        {
            Vector3 targetPos = initialCapLocalPos + new Vector3(0, -0.05f, 0);
            float elapsed = 0f;
            float duration = 0.08f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                movingCap.localPosition = Vector3.Lerp(initialCapLocalPos, targetPos, elapsed / duration);
                yield return null;
            }

            movingCap.localPosition = targetPos;
        }

        /// <summary>
        /// Yeni turda tuşun durumunu eski orijinal konumuna ve rengine sıfırlar.
        /// </summary>
        public void ResetKey()
        {
            IsPressed = false;
            isHovered = false;

            if (movingCap != null && initialCapLocalPos != Vector3.zero)
            {
                movingCap.localPosition = initialCapLocalPos;
            }

            if (initialLocalScale != Vector3.zero)
            {
                transform.localScale = initialLocalScale;
            }
            SetMaterialVisuals(normalColor, isEmissive: false);
        }

        private void OnDestroy()
        {
            if (keyMaterial != null)
            {
                Destroy(keyMaterial);
            }
        }
    }
}
