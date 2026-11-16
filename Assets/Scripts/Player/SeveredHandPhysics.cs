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
        [SerializeField] private float launchUpwardForce = 2.0f;
        [SerializeField] private float launchHorizontalForce = 1.5f;
        [SerializeField] private float tumbleTorque = 6.5f;
        [SerializeField] private float twitchDuration = 4.0f;

        private Rigidbody rb;
        private Collider col;
        private bool hasLanded = false;
        private Coroutine twitchRoutine;
        private Color handColor = new Color(0.32f, 0.015f, 0.015f);

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

            col = GetComponent<Collider>();
            if (col == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(0.14f, 0.08f, 0.20f);
                col = box;
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
        /// Eli belirtilen yönde gerçekçi kesik el ağırlığıyla fırlatır.
        /// </summary>
        public void Launch(Vector3 launchDirection, Color playerColor)
        {
            handColor = new Color(0.32f, 0.015f, 0.015f);
            transform.SetParent(null, true);

            if (col != null) col.enabled = true;
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;

                float effectiveUpForce = Mathf.Min(launchUpwardForce, 2.0f);
                float effectiveHorizForce = Mathf.Min(launchHorizontalForce, 1.5f);
                Vector3 impulse = Vector3.up * effectiveUpForce + launchDirection.normalized * effectiveHorizForce;
                rb.AddForce(impulse, ForceMode.Impulse);

                Vector3 torque = new Vector3(
                    Random.Range(-tumbleTorque, tumbleTorque),
                    Random.Range(-tumbleTorque, tumbleTorque),
                    Random.Range(-tumbleTorque, tumbleTorque)
                );
                rb.AddTorque(torque, ForceMode.Impulse);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayTrapSting();
            }

            if (CartoonFXManager.Instance != null)
            {
                CartoonFXManager.Instance.PlayTrapSnapFX(transform.position, handColor);
            }

            // 1.2 saniye sonra düşüş garantisiyle seğirmeye başla
            StartCoroutine(FallbackLandingRoutine());
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (hasLanded) return;
            hasLanded = true;

            if (CartoonFXManager.Instance != null)
            {
                Vector3 splatPoint = (collision.contactCount > 0) ? collision.GetContact(0).point : transform.position;
                CartoonFXManager.Instance.SpawnPaintSplat(splatPoint, new Color(0.32f, 0.015f, 0.015f));
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
        /// Yerde gerçekçi, hafif sinir seğirmesi.
        /// </summary>
        private IEnumerator TwitchOnFloorRoutine()
        {
            float elapsed = 0f;
            Vector3 baseScale = transform.localScale;

            while (elapsed < twitchDuration)
            {
                elapsed += Time.deltaTime;

                float twitchFactor = Mathf.Sin(elapsed * 20f);
                if (twitchFactor > 0.90f && rb != null)
                {
                    rb.AddForce(Random.insideUnitSphere * 0.15f, ForceMode.Impulse);
                    rb.AddTorque(Random.insideUnitSphere * 0.45f, ForceMode.Impulse);
                }

                transform.localScale = baseScale;

                yield return null;
            }

            transform.localScale = baseScale;
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

