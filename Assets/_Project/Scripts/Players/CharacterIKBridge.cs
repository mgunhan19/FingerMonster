using UnityEngine;

namespace FingerMonster.Player
{
    /// <summary>
    /// Animator bileşeninin bulunduğu alt nesneden OnAnimatorIK geri çağrısını
    /// üstteki CharacterAnimatorDriver bileşenine ileten köprü bileşeni.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterIKBridge : MonoBehaviour
    {
        [SerializeField] private CharacterAnimatorDriver driver;

        private void Awake()
        {
            if (driver == null)
            {
                driver = GetComponentInParent<CharacterAnimatorDriver>();
            }
        }

        public void SetDriver(CharacterAnimatorDriver targetDriver)
        {
            driver = targetDriver;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (driver != null)
            {
                driver.HandleAnimatorIK(layerIndex);
            }
        }
    }
}

