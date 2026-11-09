using System.Collections;
using UnityEngine;
using FingerMonster.Gameplay;
using FingerMonster.Audio;

namespace FingerMonster.Player
{
    /// <summary>
    /// Kopan elin çizgi film fiziğiyle (abartılı taklalar, sıçrama, boya/puf dumanı)
    /// havaya fırlamasını ve yere düştükten sonra komik bir şekilde seğirmesini sağlayan bileşen.
    /// </summary>
    public class SeveredHandPhysics : MonoBehaviour
    {
        [Header("Fizik Ayarları")]
        [SerializeField] private float launchUpwardForce = 5.5f;
        [SerializeField] private float launchHorizontalForce = 3.0f;
        [SerializeField] private float tumbleTorque = 16.0f;
        [SerializeField] private float twitchDuration = 5.0f;

        private Rigidbody rb;
        private Collider col;
        private bool hasLanded = false;
        private Coroutine twitchRoutine;
        private Color handColor = Color.white;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

            col = GetComponent<Collider>();
            if (col == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(0.25f, 0.2f, 0.35f);
            }

            rb.mass = 0.8f;
            rb.linearDamping = 0.3f;
            rb.angularDamping = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.isKinematic = true;
            rb.useGravity = false;
            if (col != null) col.enabled = false;
        }

        /// <summary>
        /// Eli belirtilen yön ve renkle havaya fırlatır.
        /// </summary>
        public void Launch(Vector3 launchDirection, Color playerColor)
        {
            handColor = playerColor;
            transform.SetParent(null, true);

            if (col != null) col.enabled = true;
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;

                Vector3 impulse = Vector3.up * launchUpwardForce + launchDirection.normalized * launchHorizontalForce;
                rb.AddForce(impulse, ForceMode.Impulse);

                Vector3 torque = new Vector3(
                    Random.Range(-tumbleTorque, tumbleTorque),
                    Random.Range(-tumbleTorque, tumbleTorque),
                    Random.Range(-tumbleTorque, tumbleTorque)
                );
                rb.AddTorque(torque, ForceMode.Impulse);
            }

            // Çizgi film yay fırlama ve şaplak sesi
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayTrapSting();
            }

            // Çizgi film puf dumanı ve boya sıçraması
            if (CartoonFXManager.Instance != null)
            {
                CartoonFXManager.Instance.PlayTrapSnapFX(transform.position, playerColor);
            }

            // 1.2 saniye sonra düşüş garantisiyle seğirmeye başla
            StartCoroutine(FallbackLandingRoutine());
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (hasLanded) return;
            hasLanded = true;

            // Küçük çizgi film "şop" etkisi
            if (CartoonFXManager.Instance != null)
            {
                CartoonFXManager.Instance.SpawnComicPopup(transform.position + Vector3.up * 0.2f, "ŞOP!", Color.yellow);
            }

            if (twitchRoutine != null) StopCoroutine(twitchRoutine);
            twitchRoutine = StartCoroutine(TwitchOnFloorRoutine());
        }

        private IEnumerator FallbackLandingRoutine()
        {
            yield return new WaitForSeconds(1.2f);
            if (!hasLanded)
            {
                hasLanded = true;
                if (twitchRoutine != null) StopCoroutine(twitchRoutine);
                twitchRoutine = StartCoroutine(TwitchOnFloorRoutine());
            }
        }

        /// <summary>
        /// Yerde komik bir şekilde parmaklarıyla seğiren el hareketi (Looney Tunes tarzı).
        /// </summary>
        private IEnumerator TwitchOnFloorRoutine()
        {
            float elapsed = 0f;
            Vector3 baseScale = transform.localScale;

            while (elapsed < twitchDuration)
            {
                elapsed += Time.deltaTime;

                // Ritmik seğirme ve minik zıplamalar
                float twitchFactor = Mathf.Sin(elapsed * 24f);
                if (twitchFactor > 0.85f && rb != null)
                {
                    rb.AddForce(Vector3.up * 0.8f + Random.insideUnitSphere * 0.3f, ForceMode.Impulse);
                    rb.AddTorque(Random.insideUnitSphere * 1.5f, ForceMode.Impulse);
                }

                // Squash & stretch deformasyonu
                float scaleSquash = 1f + Mathf.Sin(elapsed * 18f) * 0.12f;
                transform.localScale = new Vector3(baseScale.x * scaleSquash, baseScale.y / scaleSquash, baseScale.z * scaleSquash);

                yield return null;
            }

            // Seğirme bitince el komik bir dumanla puf diye kaybolur
            if (CartoonFXManager.Instance != null)
            {
                CartoonFXManager.Instance.PlayConfetti(transform.position, handColor);
            }
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Yeni turda veya sıfırlamada eli başlangıç konumuna ve durumuna geri getirir.
        /// </summary>
        public void ResetHand(Transform originalParent, Vector3 localPos, Quaternion localRot)
        {
            if (twitchRoutine != null)
            {
                StopCoroutine(twitchRoutine);
                twitchRoutine = null;
            }

            hasLanded = false;
            transform.SetParent(originalParent, false);
            transform.localPosition = localPos;
            transform.localRotation = localRot;
            gameObject.SetActive(true);

            if (col == null) col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }
    }
}

