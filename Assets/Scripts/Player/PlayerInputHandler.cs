using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using FingerMonster.Core;
using FingerMonster.Gameplay;
using FingerMonster.Networking;

namespace FingerMonster.Player
{
    /// <summary>
    /// Yeni Input System ile fare (raycast), klavye ve gamepad girdilerini işleyen
    /// ve geçerli hamleleri PressKeyCommand olarak GameManager'a ileten girdi kontrolcüsü.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Etkileşim Ayarları")]
        [SerializeField] private LayerMask keyLayerMask = ~0;
        [SerializeField] private float raycastDistance = 20.0f;

        private Camera mainCamera;
        private TypewriterKey currentHoveredKey;
        private int gamepadSelectedIndex = 0;
        private bool isGamepadMode = false;
        private float stickCooldown = 0f;

        private void Start()
        {
            mainCamera = Camera.main;
        }

        private void Update()
        {
            // Pause kontrolü (ESC veya Gamepad Start)
            HandlePauseInput();

            bool isNetwork = NetworkGameManager.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

            if (isNetwork)
            {
                // Ağ oyununda sıra yerel oyuncuda değilse veya tur bekleme durumunda değilse girdiyi engelle
                if (!NetworkGameManager.Instance.IsLocalPlayerTurn ||
                    NetworkGameManager.Instance.CurrentNetworkState.Value != GameState.PlayerTurn)
                {
                    ClearHover();
                    return;
                }
            }
            else
            {
                // Yerel oyunda oyun şu an oyuncunun tuş seçmesini beklemiyorsa veya sıra bir botta ise insan girdisini engelle
                if (GameManager.Instance == null || 
                    GameManager.Instance.CurrentState != GameState.PlayerTurn ||
                    GameManager.Instance.IsBotPlayer(GameManager.Instance.ActivePlayerIndex))
                {
                    ClearHover();
                    return;
                }
            }

            // Girdi kontrolleri
            HandleMouseInput();
            HandleGamepadAndKeyboardNavigation();
        }

        private void HandlePauseInput()
        {
            bool pausePressed = false;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                pausePressed = true;
            }

            if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
            {
                pausePressed = true;
            }

            if (pausePressed && GameManager.Instance != null)
            {
                GameManager.Instance.TogglePause();
            }
        }

        private void HandleMouseInput()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            Vector2 mousePos;
            bool leftClicked = false;

            if (Mouse.current != null)
            {
                mousePos = Mouse.current.position.ReadValue();
                leftClicked = Mouse.current.leftButton.wasPressedThisFrame;
            }
            else
            {
                mousePos = UnityEngine.Input.mousePosition;
                leftClicked = UnityEngine.Input.GetMouseButtonDown(0);
            }

            Ray ray = mainCamera.ScreenPointToRay(mousePos);

            LayerMask mask = (keyLayerMask.value == 0) ? (LayerMask)(~0) : keyLayerMask;
            RaycastHit[] hits = Physics.RaycastAll(ray, raycastDistance, mask);
            TypewriterKey closestKey = null;
            float minDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                TypewriterKey key = hits[i].collider.GetComponentInParent<TypewriterKey>();
                if (key != null && !key.IsPressed)
                {
                    if (hits[i].distance < minDistance)
                    {
                        minDistance = hits[i].distance;
                        closestKey = key;
                    }
                }
            }

            if (closestKey != null)
            {
                isGamepadMode = false;
                SetHoveredKey(closestKey);

                if (leftClicked)
                {
                    TryPressKey(closestKey.KeyIndex);
                }
                return;
            }

            if (!isGamepadMode)
            {
                ClearHover();
            }
        }

        private void HandleGamepadAndKeyboardNavigation()
        {
            // Gamepad / Klavye ok tuşları ile tuşlar arası gezinme
            Vector2 nav = Vector2.zero;

            if (Gamepad.current != null)
            {
                nav = Gamepad.current.dpad.ReadValue();
                if (nav.sqrMagnitude < 0.1f)
                {
                    nav = Gamepad.current.leftStick.ReadValue();
                }
            }

            if (Keyboard.current != null && nav.sqrMagnitude < 0.1f)
            {
                if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame) nav.x = -1f;
                else if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame) nav.x = 1f;
                else if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame) nav.y = 1f;
                else if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame) nav.y = -1f;
            }

            if (stickCooldown > 0f)
            {
                stickCooldown -= Time.deltaTime;
            }
            else if (nav.sqrMagnitude > 0.4f)
            {
                isGamepadMode = true;
                stickCooldown = 0.2f;

                int totalKeys = GameManager.Instance.Settings.totalKeys;
                if (Mathf.Abs(nav.x) > Mathf.Abs(nav.y))
                {
                    gamepadSelectedIndex += (nav.x > 0) ? 1 : -1;
                }
                else
                {
                    // İki satırlı düzende alt/üst satır geçişi (örnek 10 tuşluk satırlar)
                    gamepadSelectedIndex += (nav.y > 0) ? -10 : 10;
                }

                if (gamepadSelectedIndex < 0) gamepadSelectedIndex += totalKeys;
                gamepadSelectedIndex %= totalKeys;

                TypewriterMachine machine = GameManager.Instance.Typewriter;
                if (machine != null)
                {
                    TypewriterKey key = machine.GetKey(gamepadSelectedIndex);
                    if (key != null)
                    {
                        SetHoveredKey(key);
                    }
                }
            }

            // Gamepad A / X veya Klavyede Boşluk / Enter ile basma
            bool submitPressed = false;
            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                submitPressed = true;
            }
            if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
            {
                submitPressed = true;
            }

            if (submitPressed && currentHoveredKey != null && !currentHoveredKey.IsPressed)
            {
                TryPressKey(currentHoveredKey.KeyIndex);
            }
        }

        private void SetHoveredKey(TypewriterKey key)
        {
            if (currentHoveredKey == key) return;

            ClearHover();
            currentHoveredKey = key;
            if (currentHoveredKey != null)
            {
                currentHoveredKey.SetHovered(true);
            }
        }

        private void ClearHover()
        {
            if (currentHoveredKey != null)
            {
                currentHoveredKey.SetHovered(false);
                currentHoveredKey = null;
            }
        }

        private void TryPressKey(int keyIndex)
        {
            bool isNetwork = NetworkGameManager.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            if (isNetwork)
            {
                NetworkGameManager.Instance.LocalRequestPressKey(keyIndex);
            }
            else if (GameManager.Instance != null)
            {
                int activePlayer = GameManager.Instance.ActivePlayerIndex;
                PressKeyCommand command = new PressKeyCommand(activePlayer, keyIndex);
                GameManager.Instance.ProcessCommand(command);
            }
            ClearHover();
        }
    }
}

