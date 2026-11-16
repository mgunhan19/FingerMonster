using System;
using UnityEngine;
using FingerMonster.Player;

namespace FingerMonster.AI
{
    /// <summary>
    /// Botun çizgi film tarzı kişilik türü. Karakter kişilikleriyle birebir eşleşir.
    /// </summary>
    public enum BotPersonality
    {
        Cesur,  // Feg: Hızlı karar verir (1.0-1.6s), risk almaktan çekinmez
        Urkek,  // Kenan: Tereddütlüdür (2.0-3.0s), kenar tuşları tercih eder, titrer
        Sinsi,  // Feyzi: Olasılıkları tartar (1.4-2.3s), stratejik bekler
        Sovmen  // MMG: Gösterişçi ve teatral (1.1-1.9s), şov amaçlı tuş seçer
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

        public static BotPersonality MapArchetypeToPersonality(CharacterArchetype archetype)
        {
            switch (archetype)
            {
                case CharacterArchetype.Kabadayi: return BotPersonality.Cesur;
                case CharacterArchetype.Korkak: return BotPersonality.Urkek;
                case CharacterArchetype.Sinsi: return BotPersonality.Sinsi;
                case CharacterArchetype.Sovmen:
                default: return BotPersonality.Sovmen;
            }
        }

        public static string GetPersonalityLabel(BotPersonality personality)
        {
            switch (personality)
            {
                case BotPersonality.Cesur: return "Cesur";
                case BotPersonality.Urkek: return "Ürkek";
                case BotPersonality.Sinsi: return "Sinsi";
                case BotPersonality.Sovmen:
                default: return "Şovmen";
            }
        }

        public static BotProfile ForCharacterSlot(int slotIndex, CharacterArchetype archetype, BotDifficulty diff = BotDifficulty.Normal)
        {
            var def = CharacterDefinition.GetDefinition(archetype);
            BotPersonality pers = MapArchetypeToPersonality(archetype);

            return new BotProfile
            {
                SlotIndex = slotIndex,
                BotName = $"{def.DisplayName} ({def.PersonalityTag})",
                Personality = pers,
                Difficulty = diff,
                AvatarColor = def.PrimaryColor
            };
        }

        public static BotProfile GenerateRandom(int slotIndex, BotDifficulty diff = BotDifficulty.Normal)
        {
            CharacterArchetype archetype = (CharacterArchetype)(Mathf.Abs(slotIndex) % 4);
            return ForCharacterSlot(slotIndex, archetype, diff);
        }
    }
}

