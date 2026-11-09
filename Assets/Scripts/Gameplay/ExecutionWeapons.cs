using System;
using UnityEngine;

namespace FingerMonster.Gameplay
{
    /// <summary>
    /// Kasabın kullandığı absürt çizgi film infaz silahlarının türleri.
    /// </summary>
    public enum ExecutionWeaponType
    {
        GiantMeatMallet = 0,    // Dev Et Çekici / Tokmak (BONK -> Pancake Yassılaşma)
        FlyingCastIronPan = 1,  // Döküm Tava (DÖNKK -> Yıldızlar ve Devrilme)
        SausageWhip = 2,        // Sosis Kırbacı (ŞRAAK -> Fırıldak Dönüşü)
        CreamPieLauncher = 3    // Kremalı Pasta (SPLAT -> Kayıp Düşme)
    }

    /// <summary>
    /// İnfaz silahlarının animasyon parametreleri, sesleri ve görsel efekt verilerini tanımlar.
    /// </summary>
    [Serializable]
    public class ExecutionWeaponData
    {
        public ExecutionWeaponType WeaponType;
        public string WeaponName;
        public string ComicHitText;
        public Color ComicColor;
        public float WindupDuration;
        public float SmashDuration;
        public Vector3 WindupAngle;
        public Vector3 StrikeAngle;

        public static ExecutionWeaponData GetData(ExecutionWeaponType type)
        {
            switch (type)
            {
                case ExecutionWeaponType.GiantMeatMallet:
                    return new ExecutionWeaponData
                    {
                        WeaponType = ExecutionWeaponType.GiantMeatMallet,
                        WeaponName = "Dev Çizgi Film Tokmağı",
                        ComicHitText = "BOONNNK!",
                        ComicColor = new Color(0.95f, 0.2f, 0.2f),
                        WindupDuration = 0.9f,
                        SmashDuration = 0.12f,
                        WindupAngle = new Vector3(-65f, 0, 0),
                        StrikeAngle = new Vector3(80f, 0, 0)
                    };

                case ExecutionWeaponType.FlyingCastIronPan:
                    return new ExecutionWeaponData
                    {
                        WeaponType = ExecutionWeaponType.FlyingCastIronPan,
                        WeaponName = "Uçan Döküm Tava",
                        ComicHitText = "DÖNNNKKK!",
                        ComicColor = Color.yellow,
                        WindupDuration = 0.75f,
                        SmashDuration = 0.10f,
                        WindupAngle = new Vector3(-45f, 40f, 0),
                        StrikeAngle = new Vector3(60f, -30f, 0)
                    };

                case ExecutionWeaponType.SausageWhip:
                    return new ExecutionWeaponData
                    {
                        WeaponType = ExecutionWeaponType.SausageWhip,
                        WeaponName = "Acılı Sosis Kırbacı",
                        ComicHitText = "ŞŞŞRAAAK!",
                        ComicColor = new Color(1f, 0.45f, 0.1f),
                        WindupDuration = 0.85f,
                        SmashDuration = 0.14f,
                        WindupAngle = new Vector3(-50f, -35f, 15f),
                        StrikeAngle = new Vector3(70f, 25f, -10f)
                    };

                case ExecutionWeaponType.CreamPieLauncher:
                default:
                    return new ExecutionWeaponData
                    {
                        WeaponType = ExecutionWeaponType.CreamPieLauncher,
                        WeaponName = "Kremalı Çizgi Film Pastası",
                        ComicHitText = "SSSPLLLAATTT!",
                        ComicColor = new Color(0.9f, 0.4f, 0.8f),
                        WindupDuration = 0.7f,
                        SmashDuration = 0.15f,
                        WindupAngle = new Vector3(-30f, 0, 0),
                        StrikeAngle = new Vector3(45f, 0, 0)
                    };
            }
        }
    }
}

