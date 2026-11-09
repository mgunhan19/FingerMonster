namespace FingerMonster.Core
{
    /// <summary>
    /// Ana Menü ile Game sahnesi arasında seçilen oyun modu, zorluk derecesi ve
    /// bot/insan yapılandırmasını taşıyan statik oturum verisi.
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

