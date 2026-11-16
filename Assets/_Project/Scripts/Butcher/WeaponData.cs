using UnityEngine;
using FingerMonster.Gameplay;

namespace FingerMonster.Data
{
    /// <summary>
    /// Kasabın infaz sekansında kullandığı silahların görsel, animasyon ve efekt verilerini tutan ScriptableObject.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "FingerMonster/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [Header("Temel Kimlik")]
        public ExecutionWeaponType weaponType;
        public string displayName;
        public GameObject weaponPrefab;

        [Header("El Yerleşim Ofsetleri")]
        public Vector3 localPositionOffset;
        public Vector3 localEulerOffset;
        public Vector3 localScale = Vector3.one;

        [Header("Animasyon ve Ses")]
        public string attackTriggerName = "Attack";
        public float attackSpeedMultiplier = 1f;
        public AudioClip hitSfx;

        [Header("Görsel Efekt ve Tepki")]
        public Color vfxColor = Color.white;
        public string victimReactionType;
        public string funnyHitMessage;

        /// <summary>
        /// Asset atanmadığı durumlarda çalışma zamanında varsayılan WeaponData örneği üretir.
        /// </summary>
        public static WeaponData CreateRuntimeDefault(ExecutionWeaponType type)
        {
            WeaponData data = CreateInstance<WeaponData>();
            data.weaponType = type;
            data.attackTriggerName = "Attack";
            data.localScale = Vector3.one;

            switch (type)
            {
                case ExecutionWeaponType.GiantMeatMallet:
                    data.name = "WeaponData_GiantMeatMallet";
                    data.displayName = "Tokmak";
                    data.localPositionOffset = new Vector3(0f, 0.05f, 0.1f);
                    data.localEulerOffset = Vector3.zero;
                    data.attackSpeedMultiplier = 1.0f;
                    data.vfxColor = new Color(0.95f, 0.2f, 0.2f);
                    data.victimReactionType = "Pancake";
                    data.funnyHitMessage = "BOONNNK!";
                    break;

                case ExecutionWeaponType.FlyingCastIronPan:
                    data.name = "WeaponData_FlyingCastIronPan";
                    data.displayName = "Tava";
                    data.localPositionOffset = new Vector3(0f, 0.04f, 0.08f);
                    data.localEulerOffset = new Vector3(0f, 90f, 0f);
                    data.attackSpeedMultiplier = 1.15f;
                    data.vfxColor = Color.yellow;
                    data.victimReactionType = "DizzyStars";
                    data.funnyHitMessage = "DÖNNNKKK!";
                    break;

                case ExecutionWeaponType.SausageWhip:
                    data.name = "WeaponData_SausageWhip";
                    data.displayName = "Sosis Kırbacı";
                    data.localPositionOffset = new Vector3(0f, 0.02f, 0.12f);
                    data.localEulerOffset = Vector3.zero;
                    data.attackSpeedMultiplier = 1.25f;
                    data.vfxColor = new Color(1f, 0.45f, 0.1f);
                    data.victimReactionType = "Spin";
                    data.funnyHitMessage = "ŞŞŞRAAAK!";
                    break;

                case ExecutionWeaponType.CreamPieLauncher:
                default:
                    data.name = "WeaponData_CreamPieLauncher";
                    data.displayName = "Kremalı Pasta";
                    data.localPositionOffset = new Vector3(0f, 0.06f, 0.15f);
                    data.localEulerOffset = Vector3.zero;
                    data.attackSpeedMultiplier = 1.1f;
                    data.vfxColor = new Color(1f, 0.96f, 0.85f);
                    data.victimReactionType = "SlipSplat";
                    data.funnyHitMessage = "SPLATTT!";
                    break;
            }

            return data;
        }
    }
}
