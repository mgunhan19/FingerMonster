using System.Collections.Generic;
using UnityEngine;

namespace FingerMonster.Player
{
    /// <summary>
    /// Sıra tabanlı dönüşümlü oyuncu sırasını yöneten kontrolcü.
    /// Elenmiş oyuncuları atlar ve hayattaki oyuncuları sırayla sunar.
    /// </summary>
    public class TurnController : MonoBehaviour
    {
        public int ActivePlayerIndex { get; private set; } = 0;

        /// <summary>
        /// Sırayı ilk oyuncuya sıfırlar.
        /// </summary>
        public void ResetTurn(int startingIndex = 0)
        {
            ActivePlayerIndex = startingIndex;
        }

        /// <summary>
        /// Sıradaki hayatta olan oyuncuyu bulur ve sırayı ona geçirir.
        /// Eğer hayatta oyuncu kalmadıysa veya tek kişi kaldıysa uygun dönüşü yapar.
        /// </summary>
        public int AdvanceToNextAlivePlayer(IReadOnlyList<PlayerData> players)
        {
            if (players == null || players.Count == 0) return -1;

            int total = players.Count;
            for (int i = 1; i <= total; i++)
            {
                int candidate = (ActivePlayerIndex + i) % total;
                if (players[candidate].IsAlive)
                {
                    ActivePlayerIndex = candidate;
                    return ActivePlayerIndex;
                }
            }

            // Kimse hayatta değil
            return -1;
        }

        /// <summary>
        /// Mevcut hayatta olan oyuncu sayısını hesaplar.
        /// </summary>
        public int GetAlivePlayersCount(IReadOnlyList<PlayerData> players)
        {
            if (players == null) return 0;
            int count = 0;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].IsAlive) count++;
            }
            return count;
        }

        /// <summary>
        /// Hayatta kalan son tek oyuncuyu bulur. (Yalnızca 1 kişi kaldığında çağrılır)
        /// </summary>
        public PlayerData GetSoleSurvivor(IReadOnlyList<PlayerData> players)
        {
            if (players == null) return null;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].IsAlive) return players[i];
            }
            return null;
        }

        /// <summary>
        /// En yüksek skora sahip oyuncuyu belirler.
        /// </summary>
        public PlayerData GetHighestScorePlayer(IReadOnlyList<PlayerData> players)
        {
            if (players == null || players.Count == 0) return null;

            PlayerData best = null;
            int highest = -1;

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].IsAlive && players[i].Score > highest)
                {
                    highest = players[i].Score;
                    best = players[i];
                }
            }

            // Eğer hayatta kimse kalmadıysa genel skora bakar
            if (best == null)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].Score > highest)
                    {
                        highest = players[i].Score;
                        best = players[i];
                    }
                }
            }

            return best ?? players[0];
        }
    }
}

