using UnityEngine;
using FingerMonster.Player;

namespace FingerMonster.Core
{
    /// <summary>
    /// Ana Menü ile Game sahnesi arasında seçilen oyun modu, zorluk derecesi,
    /// seçili karakter ve bot/insan yapılandırmasını taşıyan statik oturum verisi.
    /// </summary>
    public static class GameSessionData
    {
        public enum GameMode
        {
            SoloVsBots,    // 1 İnsan Oyuncu vs 3 Bot
            HotseatParty   // 4 İnsan Oyuncu (Aynı PC'de Sırayla)
        }

        public enum Difficulty
        {
            Easy = 4,      // 4 Tuzak
            Normal = 6,    // 6 Tuzak
            Nightmare = 8  // 8 Tuzak
        }

        public static GameMode SelectedMode = GameMode.SoloVsBots;
        public static Difficulty SelectedDifficulty = Difficulty.Normal;

        // Oyuncunun Ana Menüde seçtiği karakter (varsayılan 0 = Feg)
        public static int SelectedCharacterIndex = 0;

        // Her koltuğun bot olup olmadığı
        private static bool[] botSlots = new bool[4] { false, true, true, true };

        public static void SetMode(GameMode mode)
        {
            SelectedMode = mode;
            if (mode == GameMode.SoloVsBots)
            {
                // P1 insan, P2, P3, P4 bot
                botSlots[0] = false;
                botSlots[1] = true;
                botSlots[2] = true;
                botSlots[3] = true;
            }
            else if (mode == GameMode.HotseatParty)
            {
                // 4 koltuk da insan
                botSlots[0] = false;
                botSlots[1] = false;
                botSlots[2] = false;
                botSlots[3] = false;
            }
        }

        public static void SetSelectedCharacter(int index)
        {
            SelectedCharacterIndex = Mathf.Clamp(index, 0, 3);
        }

        /// <summary>
        /// Verilen koltuğa hangi karakter arketipinin oturacağını döner.
        /// Koltuk 0 (ön koltuk) seçili karakteri alır; diğer koltuklar kalan 3 karakteri sırayla alır.
        /// </summary>
        public static CharacterArchetype GetSeatArchetype(int seatIndex)
        {
            int chosen = Mathf.Clamp(SelectedCharacterIndex, 0, 3);
            if (seatIndex == 0) return (CharacterArchetype)chosen;

            int order = 0;
            for (int c = 0; c < 4; c++)
            {
                if (c == chosen) continue;
                order++;
                if (order == seatIndex)
                {
                    return (CharacterArchetype)c;
                }
            }
            return (CharacterArchetype)(Mathf.Abs(seatIndex) % 4);
        }

        public static bool IsSlotBot(int index)
        {
            if (index >= 0 && index < botSlots.Length)
            {
                return botSlots[index];
            }
            return false;
        }

        public static int GetTrapCount()
        {
            return (int)SelectedDifficulty;
        }
    }
}

