using System;
using UnityEngine;
using FingerMonster.Core;

namespace FingerMonster.Player
{
    /// <summary>
    /// Bir oyuncunun çalışma anındaki durumunu ve skorunu saklayan veri modeli.
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public int PlayerId;
        public string PlayerName;
        public Color PlayerColor;
        public HandState LeftHand;
        public HandState RightHand;
        public int Score;

        /// <summary>
        /// Oyuncunun en az bir eli sağlamsa hayattadır.
        /// </summary>
        public bool IsAlive => LeftHand == HandState.Intact || RightHand == HandState.Intact;

        /// <summary>
        /// Oyuncunun kalan sağlam el sayısı (0, 1 veya 2).
        /// </summary>
        public int RemainingHandsCount
        {
            get
            {
                int count = 0;
                if (LeftHand == HandState.Intact) count++;
                if (RightHand == HandState.Intact) count++;
                return count;
            }
        }

        public PlayerData(int id, string name, Color color)
        {
            PlayerId = id;
            PlayerName = name;
            PlayerColor = color;
            LeftHand = HandState.Intact;
            RightHand = HandState.Intact;
            Score = 0;
        }

        /// <summary>
        /// Sağlam olan ellerden rastgele birini koparır ve kopan eli döner.
        /// </summary>
        public PlayerHand SeverRandomHand()
        {
            if (!IsAlive) return PlayerHand.None;

            // Her iki el de sağlamsa %50 şansla biri kopar
            if (LeftHand == HandState.Intact && RightHand == HandState.Intact)
            {
                bool cutLeft = UnityEngine.Random.value < 0.5f;
                if (cutLeft)
                {
                    LeftHand = HandState.Lost;
                    return PlayerHand.Left;
                }
                else
                {
                    RightHand = HandState.Lost;
                    return PlayerHand.Right;
                }
            }

            // Sadece sol el kaldıysa
            if (LeftHand == HandState.Intact)
            {
                LeftHand = HandState.Lost;
                return PlayerHand.Left;
            }

            // Sadece sağ el kaldıysa
            if (RightHand == HandState.Intact)
            {
                RightHand = HandState.Lost;
                return PlayerHand.Right;
            }

            return PlayerHand.None;
        }

        /// <summary>
        /// Tur başında veya yeniden başlatmada elleri sıfırlar.
        /// </summary>
        public void ResetHands()
        {
            LeftHand = HandState.Intact;
            RightHand = HandState.Intact;
        }
    }
}

