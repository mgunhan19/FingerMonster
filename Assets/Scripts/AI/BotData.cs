using System;
using UnityEngine;

namespace FingerMonster.AI
{
    /// <summary>
    /// Botun çizgi film tarzı kişilik türü. Aşama 3'te animasyon ve ses tepkilerini de belirler.
    /// </summary>
    public enum BotPersonality
    {
        Cesur,  // Cesur: Hızlı karar verir (0.8-1.5s), risk almaktan çekinmez
        Urkek,  // Ürkek: Tereddütlüdür (2.0-3.2s), kenar tuşları tercih eder, titrer
        Sinsi   // Sinsi: Olasılıkları tartar (1.4-2.2s), rakiplerin durumuna göre stratejik bekler
    }

    /// <summary>
    /// Bot yapay zekâsının karar verme zorluk seviyesi.
    /// </summary>
    public enum BotDifficulty
    {
        Kolay,   // Tamamen rastgele, olasılık hesaplamaz
        Normal,  // Basılmış tuşları filtreler, dengeli insansı risk alır
        Zor      // Kalan tuzak/güvenli oranını matematiksel olarak tartar (P_tuzak = T_kalan / N_kalan)
    }

    /// <summary>
    /// Her bir bot oyuncunun profil ve kişilik verilerini saklar.
    /// </summary>
    [Serializable]
    public class BotProfile
    {
        public int SlotIndex;
        public string BotName;
        public BotPersonality Personality;
        public BotDifficulty Difficulty;
        public Color AvatarColor;

        public static BotProfile GenerateRandom(int slotIndex, BotDifficulty diff = BotDifficulty.Normal)
        {
            string[] names = new string[] { "Demir Parmak", "Tereddütlü Toni", "Korkusuz Kaan", "Şanslı Sam", "Gölge Gizem", "Daktilo Dede" };
            BotPersonality[] personalities = (BotPersonality[])Enum.GetValues(typeof(BotPersonality));

            string name = names[slotIndex % names.Length];
            BotPersonality pers = personalities[UnityEngine.Random.Range(0, personalities.Length)];

            return new BotProfile
            {
                SlotIndex = slotIndex,
                BotName = $"{name} ({pers})",
                Personality = pers,
                Difficulty = diff,
                AvatarColor = GetDefaultSlotColor(slotIndex)
            };
        }

        private static Color GetDefaultSlotColor(int slot)
        {
            switch (slot)
            {
                case 0: return new Color(0.9f, 0.25f, 0.25f);
                case 1: return new Color(0.25f, 0.55f, 0.95f);
                case 2: return new Color(0.25f, 0.85f, 0.35f);
                case 3: return new Color(0.95f, 0.75f, 0.2f);
                default: return Color.gray;
            }
        }
    }
}

