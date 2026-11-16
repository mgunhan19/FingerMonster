using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using FingerMonster.Core;

namespace FingerMonster.Player
{
    /// <summary>
    /// Oyuncunun kamerasını her zaman kendi seçtiği / atandığı koltukta sabit tutar.
    /// Sıra başka oyuncuya (bot veya insan) geçse bile kamera asla sıradaki oyuncuyu takip etmez.
    /// Sadece yerel oyuncu yanındaki rakibe emote yaparken kamera yumuşakça o tarafa doğru (35-45° yaw)
    /// bakar, emote bitince eski daktilo açısına geri döner.
    /// </summary>
    public class PlayerCameraController : MonoBehaviour
    {
        public static PlayerCameraController Instance { get; private set; }

        [Header("Yerel Koltuk Yapılandırması")]
        [SerializeField] private int localSeatIndex = 0;
        [SerializeField] private float fixedFieldOfView = 68f;

        [Header("Geçiş Hızları")]
        [SerializeField] private float panSpeed = 6.0f;

        [Header("Hayalet İzleyici Kamera")]
        [SerializeField] private float ghostDistanceFromTable = 2.95f;
        [SerializeField] private float ghostCameraHeight = 2.25f;
        [SerializeField] private float ghostTransitionDuration = 0.4f;
        [SerializeField] private float ghostSphereCastRadius = 0.25f;
        [SerializeField] private LayerMask ghostCollisionMask = ~0;

        [Header("Hayalet Serbest Uçuş")]
        [SerializeField] private float ghostMoveSpeed = 3.5f;
        [SerializeField] private float ghostSprintMultiplier = 2.0f;
        [SerializeField] private float ghostLookSensitivity = 0.18f;
        [SerializeField] private float ghostMinPitch = -70f;
        [SerializeField] private float ghostMaxPitch = 75f;

        [Header("Durum")]
        [SerializeField] private bool isEmotePanActive = false;
        public bool IsEmotePanActive => isEmotePanActive;
        public bool IsGhostSpectatorMode { get; private set; } = false;
        public int GhostSpectatingSeatIndex { get; private set; } = 0;
        public GhostMovementMode CurrentGhostMode { get; private set; } = GhostMovementMode.FreeFlight;
        public event Action<GhostMovementMode> OnGhostModeChanged;
        public Vector3 CurrentGhostWorldPosition => transform.position;
        public Quaternion CurrentGhostWorldRotation => transform.rotation;

        private Vector3 seatDefaultPosition;
        private Quaternion seatDefaultRotation;
        private Quaternion currentTargetRotation;
        private Coroutine activeEmotePanRoutine;
        private Coroutine activeScareShakeRoutine;
        private Vector3 scareShakeOffset = Vector3.zero;
        private Transform externalOverrideTarget;
        private float ghostOrbitAngle = 0f;
        private float ghostOrbitSpeed = 12f;
        private float ghostTransitionStartAngle = 0f;
        private float ghostTargetBaseAngle = 0f;
        private float ghostTransitionTimer = 0f;
        private Vector3 ghostFreeFlightPos;
        private float ghostYaw = 0f;
        private float ghostPitch = 20f;
        private Camera cachedCamera;

        private static readonly Bounds GhostRoomBounds = new Bounds(
            new Vector3(0f, 2.20f, 0f),
            new Vector3(6.8f, 1.30f, 6.8f));

        private static readonly Vector3 TableCenterTarget = new Vector3(0f, 0.85f, 0f);

        private static readonly Vector3[] StandardSeatPositions = new Vector3[]
        {
            new Vector3(0f, 0f, -2.05f),
            new Vector3(0f, 0f, 1.48f),
            new Vector3(-1.68f, 0f, 0f),
            new Vector3(1.68f, 0f, 0f)
        };

        // 4 Koltuğun Dünya Konumları ve Daktiloya Bakan Açıları
        // P1: Güney (0, 0, -2.05), P2: Kuzey (0, 0, 1.48), P3: Batı (-1.68, 0, 0), P4: Doğu (1.68, 0, 0)
        // KameraMount local offset: (0, 2.45, -1.20) ve 26° eğim
        private static readonly Vector3[] StandardCamPositions = new Vector3[]
        {
            new Vector3(0f, 2.45f, -3.25f),     // P1: Güneyden Kuzeye (Daktiloya) bakar
            new Vector3(0f, 2.45f, 2.68f),      // P2: Kuzeyden Güneye bakar
            new Vector3(-2.88f, 2.45f, 0f),     // P3: Batıdan Doğuya bakar
            new Vector3(2.88f, 2.45f, 0f)       // P4: Doğudan Batıya bakar
        };

        private static readonly Vector3[] StandardCamEulerAngles = new Vector3[]
        {
            new Vector3(26f, 0f, 0f),           // P1: Yaw 0°
            new Vector3(26f, 180f, 0f),         // P2: Yaw 180°
            new Vector3(26f, 90f, 0f),          // P3: Yaw 90°
            new Vector3(26f, -90f, 0f)          // P4: Yaw -90° (270°)
        };

        private void Awake()
        {
            Instance = this;
            cachedCamera = GetComponent<Camera>();
            ApplyFixedTransform();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            IsGhostSpectatorMode = false;
            CurrentGhostMode = GhostMovementMode.FreeFlight;
            externalOverrideTarget = null;
            activeEmotePanRoutine = null;
            activeScareShakeRoutine = null;
            scareShakeOffset = Vector3.zero;
        }

        public void ApplyFixedTransform()
        {
            SetLocalSeat(localSeatIndex, snapImmediately: true);
        }

        /// <summary>
        /// Oyuncunun yerel koltuğunu belirler ve kamerayı o koltuğa sabitler.
        /// </summary>
        public void SetLocalSeat(int seatIndex, bool snapImmediately = false)
        {
            localSeatIndex = Mathf.Clamp(seatIndex, 0, 3);

            if (localSeatIndex >= 0 && localSeatIndex < StandardCamPositions.Length)
            {
                seatDefaultPosition = StandardCamPositions[localSeatIndex];
                seatDefaultRotation = Quaternion.Euler(StandardCamEulerAngles[localSeatIndex]);
            }
            else
            {
                seatDefaultPosition = transform.position;
                seatDefaultRotation = transform.rotation;
            }

            currentTargetRotation = seatDefaultRotation;

            Camera cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.fieldOfView = fixedFieldOfView;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 1000f;
            }

            if (snapImmediately)
            {
                transform.position = seatDefaultPosition;
                transform.rotation = seatDefaultRotation;
            }
        }

        private void LateUpdate()
        {
            // Eğer harici bir sekans (kasap, infaz kamerası vb.) override hedefi verdiyse
            if (externalOverrideTarget != null && externalOverrideTarget.gameObject.activeInHierarchy)
            {
                Vector3 tPos = externalOverrideTarget.position;
                Quaternion tRot = externalOverrideTarget.rotation;
                if (float.IsFinite(tPos.x) && float.IsFinite(tPos.y) && float.IsFinite(tPos.z))
                {
                    transform.position = Vector3.Lerp(transform.position, tPos, Time.deltaTime * 5f);
                }
                if (float.IsFinite(tRot.x) && float.IsFinite(tRot.y) && float.IsFinite(tRot.z) && float.IsFinite(tRot.w) &&
                    (tRot.x != 0 || tRot.y != 0 || tRot.z != 0 || tRot.w != 0))
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, tRot, Time.deltaTime * 5f);
                }
                return;
            }
            else
            {
                externalOverrideTarget = null;
            }

            if (IsGhostSpectatorMode)
            {
                if (cachedCamera == null) cachedCamera = GetComponent<Camera>();
                if (cachedCamera != null && !Mathf.Approximately(cachedCamera.nearClipPlane, 0.15f))
                {
                    cachedCamera.nearClipPlane = 0.15f;
                }

                if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    ToggleGhostMovementMode();
                }

                if (CurrentGhostMode == GhostMovementMode.FreeFlight)
                {
                    UpdateGhostFreeFlight();
                    return;
                }

                UpdateGhostSpectatorOrbit();
                return;
            }

            // Kamera konumu her zaman yerel oyuncunun koltuğunda SABİTTİR (varsa yumuşak korkutma sarsıntısı eklenir)
            if (float.IsFinite(seatDefaultPosition.x) && float.IsFinite(seatDefaultPosition.y) && float.IsFinite(seatDefaultPosition.z))
            {
                transform.position = seatDefaultPosition + scareShakeOffset;
            }

            // Kamera rotasyonu: Emote sırasında yumuşakça yana döner, bitince daktiloya döner
            if (float.IsFinite(currentTargetRotation.x) && float.IsFinite(currentTargetRotation.y) &&
                float.IsFinite(currentTargetRotation.z) && float.IsFinite(currentTargetRotation.w) &&
                (currentTargetRotation.x != 0 || currentTargetRotation.y != 0 || currentTargetRotation.z != 0 || currentTargetRotation.w != 0))
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, currentTargetRotation, Time.deltaTime * panSpeed);
            }
        }

        /// <summary>
        /// Yerel oyuncu bir hedefe emote yaptığında kamerayı o hedefin yönüne (Sol / Sağ)
        /// yaklaşık 40° çevirir. Karşıdaki koltuğa emote yapılıyorsa kamera sabit kalır.
        /// Emote süresi bitince yumuşakça merkeze döner.
        /// </summary>
        public void TriggerEmotePan(int targetSeatIndex, float duration = 1.8f)
        {
            if (IsGhostSpectatorMode) return;
            if (targetSeatIndex < 0 || targetSeatIndex == localSeatIndex) return;

            // Hedefin yerel koltuğa göre göreceli açısı:
            // 0: Ön (Güney), 1: Karşı (Kuzey), 2: Sol (Batı), 3: Sağ (Doğu)
            float yawOffset = CalculateRelativeYawOffset(localSeatIndex, targetSeatIndex);

            if (Mathf.Abs(yawOffset) < 5f)
            {
                // Karşıdaki oyuncuysa kamera zaten düz bakıyor, sabit kalır!
                return;
            }

            if (activeEmotePanRoutine != null) StopCoroutine(activeEmotePanRoutine);
            activeEmotePanRoutine = StartCoroutine(EmotePanRoutine(yawOffset, duration));
        }

        private IEnumerator EmotePanRoutine(float yawOffset, float duration)
        {
            isEmotePanActive = true;

            // Koltuğun temel bakışına Sol (-yaw) veya Sağ (+yaw) açıyı ekle
            Quaternion targetRot = seatDefaultRotation * Quaternion.Euler(0f, yawOffset, 0f);
            currentTargetRotation = targetRot;

            yield return new WaitForSeconds(duration);

            // Eski konuma yumuşakça geri dön
            currentTargetRotation = seatDefaultRotation;
            yield return new WaitForSeconds(0.4f);

            isEmotePanActive = false;
            activeEmotePanRoutine = null;
        }

        /// <summary>
        /// 4 koltuk (0: Güney, 1: Kuzey, 2: Batı, 3: Doğu) arasındaki göreceli yönü hesaplar.
        /// Karşı: 0°, Sol: -42°, Sağ: +42°
        /// </summary>
        public static float CalculateRelativeYawOffset(int fromSeat, int toSeat)
        {
            // Koltukların dünya yaw açıları: 0 -> 0°, 1 -> 180°, 2 -> 90°, 3 -> 270°
            float[] seatWorldAngles = new float[] { 0f, 180f, 90f, 270f };

            if (fromSeat < 0 || fromSeat >= 4 || toSeat < 0 || toSeat >= 4) return 0f;

            // Masanın ortasından bakıldığında göreceli koltuk ilişkileri
            // P1 (Güney, Yaw 0°):
            //   P2 (Kuzey): Karşı -> 0°
            //   P3 (Batı):  Sol -> -40°
            //   P4 (Doğu):  Sağ -> +40°
            // P2 (Kuzey, Yaw 180°):
            //   P1 (Güney): Karşı -> 0°
            //   P4 (Doğu):  Sol -> -40°
            //   P3 (Batı):  Sağ -> +40°
            // P3 (Batı, Yaw 90°):
            //   P4 (Doğu):  Karşı -> 0°
            //   P1 (Güney): Sağ -> +40°
            //   P2 (Kuzey): Sol -> -40°
            // P4 (Doğu, Yaw 270°):
            //   P3 (Batı):  Karşı -> 0°
            //   P1 (Güney): Sol -> -40°
            //   P2 (Kuzey): Sağ -> +40°

            switch (fromSeat)
            {
                case 0: // Güney
                    if (toSeat == 1) return 0f;    // Karşı
                    if (toSeat == 2) return -40f;  // Sol (Batı)
                    if (toSeat == 3) return 40f;   // Sağ (Doğu)
                    break;

                case 1: // Kuzey
                    if (toSeat == 0) return 0f;    // Karşı
                    if (toSeat == 3) return -40f;  // Sol (Doğu)
                    if (toSeat == 2) return 40f;   // Sağ (Batı)
                    break;

                case 2: // Batı
                    if (toSeat == 3) return 0f;    // Karşı
                    if (toSeat == 1) return -40f;  // Sol (Kuzey)
                    if (toSeat == 0) return 40f;   // Sağ (Güney)
                    break;

                case 3: // Doğu
                    if (toSeat == 2) return 0f;    // Karşı
                    if (toSeat == 0) return -40f;  // Sol (Güney)
                    if (toSeat == 1) return 40f;   // Sağ (Kuzey)
                    break;
            }

            return 0f;
        }

        /// <summary>
        /// Eski GameManager / kasap uyumluluğu için:
        /// Eğer harici bir sekans özel mount verirse o hedefe bakar, null verilirse yerel koltuğa döner.
        /// </summary>
        public void SetTarget(Transform newTarget, bool snapImmediately = false)
        {
            // Tur değişimlerinde gelen sıradaki oyuncu mount'larını yok say!
            // Sadece özel sekanslar (örn. kasap vb.) doğrudan çağırırsa veya hedef sıfırlanırsa dinle.
        }

        public void SetExternalOverride(Transform overrideTarget)
        {
            externalOverrideTarget = overrideTarget;
        }

        public void ClearExternalOverride()
        {
            externalOverrideTarget = null;
            currentTargetRotation = seatDefaultRotation;
        }

        /// <summary>
        /// Hayalet izleyici kamera modunu açar veya kapatır.
        /// Açıldığında varsayılan olarak FreeFlight modunda başlar; kapatıldığında yerel koltuk kamerasına döner.
        /// </summary>
        public void SetGhostSpectatorMode(bool active, int startSeat = 0, float orbitSpeed = 12f)
        {
            IsGhostSpectatorMode = active;
            GhostSpectatingSeatIndex = Mathf.Clamp(startSeat, 0, 3);
            ghostOrbitSpeed = orbitSpeed;
            ghostTransitionTimer = 0f;

            if (activeScareShakeRoutine != null)
            {
                StopCoroutine(activeScareShakeRoutine);
                activeScareShakeRoutine = null;
            }
            scareShakeOffset = Vector3.zero;

            Camera cam = cachedCamera != null ? cachedCamera : GetComponent<Camera>();
            if (active)
            {
                CurrentGhostMode = GhostMovementMode.FreeFlight;
                ghostOrbitAngle = GetSeatBaseOrbitAngle(GhostSpectatingSeatIndex);
                ghostTargetBaseAngle = ghostOrbitAngle;

                int initSeat = Mathf.Clamp(localSeatIndex, 0, StandardCamPositions.Length - 1);
                ghostFreeFlightPos = ClampToGhostBounds(StandardCamPositions[initSeat] + new Vector3(0f, 0.25f, 0f));

                Vector3 lookDir = (TableCenterTarget - ghostFreeFlightPos);
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    Vector3 euler = Quaternion.LookRotation(lookDir.normalized, Vector3.up).eulerAngles;
                    ghostYaw = euler.y;
                    ghostPitch = ClampGhostPitch(NormalizeAngle(euler.x));
                }
                else
                {
                    ghostYaw = StandardCamEulerAngles[initSeat].y;
                    ghostPitch = ClampGhostPitch(StandardCamEulerAngles[initSeat].x);
                }

                transform.position = ghostFreeFlightPos;
                transform.rotation = Quaternion.Euler(ghostPitch, ghostYaw, 0f);

                if (cam != null)
                {
                    cam.nearClipPlane = 0.15f;
                }
                OnGhostModeChanged?.Invoke(CurrentGhostMode);
                LogGhostCameraDiagnostics("EnterGhostSpectatorMode(FreeFlight)");
            }
            else
            {
                CurrentGhostMode = GhostMovementMode.FreeFlight;
                ghostFreeFlightPos = seatDefaultPosition;
                ApplyFixedTransform();
            }
        }

        private void ToggleGhostMovementMode()
        {
            if (CurrentGhostMode == GhostMovementMode.FreeFlight)
            {
                CurrentGhostMode = GhostMovementMode.SpectatorOrbit;
                ghostOrbitAngle = Mathf.Atan2(transform.position.z, transform.position.x) * Mathf.Rad2Deg;
                ghostTargetBaseAngle = ghostOrbitAngle;
                ghostTransitionTimer = 0f;
            }
            else
            {
                CurrentGhostMode = GhostMovementMode.FreeFlight;
                ghostFreeFlightPos = ClampToGhostBounds(transform.position);
                Vector3 euler = transform.rotation.eulerAngles;
                ghostYaw = euler.y;
                ghostPitch = ClampGhostPitch(NormalizeAngle(euler.x));
            }

            OnGhostModeChanged?.Invoke(CurrentGhostMode);
            LogGhostCameraDiagnostics($"ToggleGhostMode({CurrentGhostMode})");
        }

        private void UpdateGhostFreeFlight()
        {
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                ghostYaw += delta.x * ghostLookSensitivity;
                ghostPitch = ClampGhostPitch(ghostPitch - delta.y * ghostLookSensitivity);
            }

            Quaternion lookRot = Quaternion.Euler(ghostPitch, ghostYaw, 0f);
            transform.rotation = lookRot;

            Vector3 moveInput = Vector3.zero;
            bool isSprinting = false;

            if (Keyboard.current != null)
            {
                Vector3 forward = lookRot * Vector3.forward;
                Vector3 right = lookRot * Vector3.right;

                if (Keyboard.current.wKey.isPressed) moveInput += forward;
                if (Keyboard.current.sKey.isPressed) moveInput -= forward;
                if (Keyboard.current.dKey.isPressed) moveInput += right;
                if (Keyboard.current.aKey.isPressed) moveInput -= right;
                if (Keyboard.current.spaceKey.isPressed) moveInput += Vector3.up;
                if (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed) moveInput += Vector3.down;
                isSprinting = Keyboard.current.leftShiftKey.isPressed;
            }

            if (moveInput.sqrMagnitude > 1f)
            {
                moveInput.Normalize();
            }

            float speed = ghostMoveSpeed * (isSprinting ? ghostSprintMultiplier : 1f);
            Vector3 moveDelta = moveInput * (speed * Time.deltaTime);

            ghostFreeFlightPos = ResolveGhostFreeFlightMove(ghostFreeFlightPos, moveDelta);
            transform.position = ghostFreeFlightPos;
        }

        private Vector3 ResolveGhostFreeFlightMove(Vector3 currentPos, Vector3 moveDelta)
        {
            if (moveDelta.sqrMagnitude <= 1e-6f)
            {
                return ClampToGhostBounds(currentPos);
            }

            float dist = moveDelta.magnitude;
            Vector3 dir = moveDelta / dist;

            if (Physics.SphereCast(currentPos, ghostSphereCastRadius, dir, out RaycastHit hit, dist + 0.05f, ghostCollisionMask, QueryTriggerInteraction.Ignore))
            {
                if (!IsIgnoredGhostCollider(hit.collider))
                {
                    float safeDist = Mathf.Max(0f, hit.distance - 0.05f);
                    Vector3 slideDelta = Vector3.ProjectOnPlane(moveDelta, hit.normal);
                    Vector3 candidate = currentPos + dir * safeDist + slideDelta * 0.5f;
                    return ClampToGhostBounds(candidate);
                }
            }

            return ClampToGhostBounds(currentPos + moveDelta);
        }

        private void UpdateGhostSpectatorOrbit()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.qKey.wasPressedThisFrame)
                {
                    CycleGhostSpectatorTarget(-1);
                }
                else if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    CycleGhostSpectatorTarget(1);
                }
            }

            if (ghostTransitionTimer > 0f)
            {
                ghostTransitionTimer = Mathf.Max(0f, ghostTransitionTimer - Time.deltaTime);
                float duration = Mathf.Max(0.01f, ghostTransitionDuration);
                float t = 1f - (ghostTransitionTimer / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                ghostTargetBaseAngle += Time.deltaTime * ghostOrbitSpeed;
                ghostOrbitAngle = Mathf.LerpAngle(ghostTransitionStartAngle, ghostTargetBaseAngle, smoothT);
            }
            else
            {
                ghostOrbitAngle += Time.deltaTime * ghostOrbitSpeed;
            }

            float rad = ghostOrbitAngle * Mathf.Deg2Rad;
            Vector3 desiredPos = new Vector3(
                Mathf.Cos(rad) * ghostDistanceFromTable,
                ghostCameraHeight,
                Mathf.Sin(rad) * ghostDistanceFromTable);

            desiredPos = ClampToGhostBounds(desiredPos);

            Vector3 rayOrigin = new Vector3(0f, 1.35f, 0f);
            Vector3 toDesired = desiredPos - rayOrigin;
            float dist = toDesired.magnitude;
            if (dist > 0.05f)
            {
                Vector3 dir = toDesired / dist;
                if (Physics.SphereCast(rayOrigin, ghostSphereCastRadius, dir, out RaycastHit hit, dist, ghostCollisionMask, QueryTriggerInteraction.Ignore))
                {
                    if (!IsIgnoredGhostCollider(hit.collider))
                    {
                        desiredPos = rayOrigin + dir * Mathf.Max(1.4f, hit.distance - 0.35f);
                        desiredPos = ClampToGhostBounds(desiredPos);
                    }
                }
            }

            transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * 6f);

            int seatIdx = Mathf.Clamp(GhostSpectatingSeatIndex, 0, StandardSeatPositions.Length - 1);
            Vector3 seatPos = StandardSeatPositions[seatIdx];
            Vector3 lookTarget = Vector3.Lerp(TableCenterTarget, seatPos + Vector3.up * 0.85f, 0.2f);
            Vector3 lookDir = lookTarget - transform.position;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime * 7f);
            }
        }

        /// <summary>
        /// Yaşayan oyuncular için ışığa duyarlı dostu, düşük frekanslı yumuşak korkutma kamera sarsıntısı uygular.
        /// </summary>
        public void TriggerGentleScareShake(float duration = 0.6f, float intensity = 0.045f)
        {
            if (IsGhostSpectatorMode) return;

            if (activeScareShakeRoutine != null)
            {
                StopCoroutine(activeScareShakeRoutine);
            }
            activeScareShakeRoutine = StartCoroutine(ScareShakeRoutine(Mathf.Max(0.1f, duration), Mathf.Clamp(intensity, 0f, 0.12f)));
        }

        private IEnumerator ScareShakeRoutine(float duration, float intensity)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (IsGhostSpectatorMode)
                {
                    scareShakeOffset = Vector3.zero;
                    activeScareShakeRoutine = null;
                    yield break;
                }

                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float envelope = Mathf.Sin(progress * Mathf.PI);
                float x = Mathf.Sin(elapsed * 22f) * intensity * envelope;
                float y = Mathf.Cos(elapsed * 18f) * (intensity * 0.6f) * envelope;
                scareShakeOffset = new Vector3(x, y, 0f);
                yield return null;
            }

            scareShakeOffset = Vector3.zero;
            activeScareShakeRoutine = null;
        }

        private float ClampGhostPitch(float pitch)
        {
            return Mathf.Clamp(pitch, ghostMinPitch, ghostMaxPitch);
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }

        /// <summary>
        /// Hayalet izleyici kamerasının odaklandığı koltuğu Q/E yönünde tüm 4 koltuk arasında değiştirir.
        /// Seçilen koltuğun omzunun gerisinden masaya bakan açıya yumuşak geçiş başlatır.
        /// </summary>
        public void CycleGhostSpectatorTarget(int direction, Func<int, bool> isSeatAlive = null)
        {
            int step = direction >= 0 ? 1 : -1;
            GhostSpectatingSeatIndex = ((GhostSpectatingSeatIndex + step) % 4 + 4) % 4;
            ghostTransitionStartAngle = ghostOrbitAngle;
            ghostTargetBaseAngle = GetSeatBaseOrbitAngle(GhostSpectatingSeatIndex);
            ghostTransitionTimer = Mathf.Max(0.01f, ghostTransitionDuration);
            LogGhostCameraDiagnostics($"CycleGhostTarget({step})");
        }

        private static float GetSeatBaseOrbitAngle(int seatIndex)
        {
            int idx = Mathf.Clamp(seatIndex, 0, StandardSeatPositions.Length - 1);
            Vector3 seatPos = StandardSeatPositions[idx];
            return Mathf.Atan2(seatPos.z, seatPos.x) * Mathf.Rad2Deg;
        }

        private static Vector3 ClampToGhostBounds(Vector3 pos)
        {
            Vector3 min = GhostRoomBounds.min;
            Vector3 max = GhostRoomBounds.max;
            return new Vector3(
                Mathf.Clamp(pos.x, min.x, max.x),
                Mathf.Clamp(pos.y, min.y, max.y),
                Mathf.Clamp(pos.z, min.z, max.z));
        }

        private static bool IsIgnoredGhostCollider(Collider col)
        {
            if (col == null) return true;

            Transform t = col.transform;
            while (t != null)
            {
                string n = t.name;
                if (n.IndexOf("Typewriter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Table", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("PlayerSeat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("PlayerVisualAvatar", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
                t = t.parent;
            }
            return false;
        }

        private void LogGhostCameraDiagnostics(string context)
        {
            Debug.Log($"[PlayerCameraController] {context} | Seat={GhostSpectatingSeatIndex} | Pos={transform.position} | Rot={transform.rotation.eulerAngles} | Pivot={TableCenterTarget} | Radius={ghostDistanceFromTable:F2}m | Height={ghostCameraHeight:F2}m | Angle={ghostOrbitAngle:F1}°");
        }
    }
}
