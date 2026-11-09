using UnityEngine;

namespace FingerMonster.Player
{
    /// <summary>
    /// Kameranın belirlenen sabit açıda (X:0, Y:2.28, Z:-2.31, 27.956°) stabil kalmasını sağlar.
    /// Kamera asla gereksiz yere eksenlerde dönüp baş döndürücü döngülere girmez.
    /// </summary>
    public class PlayerCameraController : MonoBehaviour
    {
        [Header("Kullanıcı Tanımlı Sabit Kamera")]
        [SerializeField] private Vector3 fixedPosition = new Vector3(0f, 2.28f, -2.31f);
        [SerializeField] private Vector3 fixedEulerAngles = new Vector3(27.956f, 0f, 0f);
        [SerializeField] private bool lockToFixedTransform = true;

        [Header("Hareket Ayarları")]
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float rotateSpeed = 5.0f;

        [Header("Hedef")]
        [SerializeField] private Transform targetMount;

        private void Awake()
        {
            ApplyFixedTransform();
        }

        public void ApplyFixedTransform()
        {
            transform.position = fixedPosition;
            transform.rotation = Quaternion.Euler(fixedEulerAngles);

            Camera cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 1000f;
            }
        }

        private void LateUpdate()
        {
            if (lockToFixedTransform)
            {
                // X ekseninde döngüsel sallantı ve yer değiştirmeleri tamamen engelle
                transform.position = fixedPosition;
                transform.rotation = Quaternion.Euler(fixedEulerAngles);
                return;
            }

            if (targetMount == null) return;

            // Yumuşak pozisyon ve rotasyon geçişi
            transform.position = Vector3.Lerp(transform.position, targetMount.position, Time.deltaTime * moveSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetMount.rotation, Time.deltaTime * rotateSpeed);
        }

        /// <summary>
        /// Kameranın hedef bağlama noktasını değiştirir.
        /// </summary>
        public void SetTarget(Transform newTarget, bool snapImmediately = false)
        {
            targetMount = newTarget;

            if (lockToFixedTransform)
            {
                ApplyFixedTransform();
                return;
            }

            if (snapImmediately && targetMount != null)
            {
                transform.position = targetMount.position;
                transform.rotation = targetMount.rotation;
            }
        }

        public void SetLockToFixedTransform(bool isLocked)
        {
            lockToFixedTransform = isLocked;
            if (isLocked) ApplyFixedTransform();
        }
    }
}

