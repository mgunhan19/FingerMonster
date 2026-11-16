using System;
using System.Collections.Generic;
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
        public CharacterArchetype Archetype;
        public string PersonalityTag;
        public Color PlayerColor;
        public HandState LeftHand;
        public HandState RightHand;
        public int Score;

        /// <summary>
        /// Oyuncunun elinde tuttuğu stratejik yetenek kartları (Maksimum 2).
        /// </summary>
        public List<AbilityCardType> AbilityCards { get; private set; } = new List<AbilityCardType>(2);

        /// <summary>
        /// Bu tur için aktif edilmiş Kalkan koruması var mı?
        /// </summary>
        public bool HasActiveShield { get; set; } = false;

        /// <summary>
        /// Rakip tarafından Sis kartı ile hedeflendi mi? (Bir sonraki 3 kart seçiminde geçerli)
        /// </summary>
        public bool IsFogged { get; set; } = false;

        /// <summary>
        /// Bir önceki kendi sırasını Sıra Atlat (SkipTurn) ile mi geçti? (Üst üste 2 kez kullanımı engeller)
        /// </summary>
        public bool UsedSkipLastTurn { get; set; } = false;

        /// <summary>
        /// Bu sıra içinde halihazırda bir yetenek kartı kullandı mı?
        /// </summary>
        public bool UsedAbilityThisTurn { get; set; } = false;

        /// <summary>
        /// Bu sıra içinde Sıra Atlat (SkipTurn) kartı kullandı mı?
        /// </summary>
        public bool UsedSkipThisTurn { get; set; } = false;

        /// <summary>
        /// Oyuncunun en az bir eli sağlamsa hayattadır.
        /// </summary>
        public bool IsAlive => LeftHand == HandState.Intact || RightHand == HandState.Intact;

        /// <summary>
        /// Oyuncu elenmişse hayalet (Kasabın Yardımcısı) durumundadır.
        /// </summary>
        public bool IsGhost => !IsAlive;

        /// <summary>
        /// Hayaletin lanet ve hayalet etkileri için kullanabileceği ruh enerjisi.
        /// </summary>
        public int SoulEnergy { get; set; } = 0;

        /// <summary>
        /// Hayaletin son atmosferik etkiyi tetiklediği zaman (cooldown kontrolü için).
        /// </summary>
        public float LastGhostEffectTime { get; set; } = -10f;

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

        public PlayerData(int id, string name, Color color, CharacterArchetype archetype = CharacterArchetype.Kabadayi, string personalityTag = "")
        {
            PlayerId = id;
            PlayerName = name;
            PlayerColor = color;
            Archetype = archetype;
            PersonalityTag = string.IsNullOrEmpty(personalityTag)
                ? CharacterDefinition.GetDefinition(archetype).PersonalityTag
                : personalityTag;
            LeftHand = HandState.Intact;
            RightHand = HandState.Intact;
            Score = 0;
            ClearAbilityCardsAndEffects();
        }

        /// <summary>
        /// Oyuncu hayattaysa ve elinde yer varsa yeni bir yetenek kartı ekler.
        /// </summary>
        public bool TryAddAbilityCard(AbilityCardType card, int maxHandSize = 2)
        {
            if (!IsAlive || card == AbilityCardType.None) return false;
            if (AbilityCards == null) AbilityCards = new List<AbilityCardType>(maxHandSize);
            if (AbilityCards.Count >= maxHandSize) return false;

            AbilityCards.Add(card);
            return true;
        }

        /// <summary>
        /// Belirtilen slottaki yetenek kartını elden çıkarır.
        /// </summary>
        public bool RemoveAbilityCardAt(int slotIndex)
        {
            if (AbilityCards == null || slotIndex < 0 || slotIndex >= AbilityCards.Count) return false;
            AbilityCards.RemoveAt(slotIndex);
            return true;
        }

        /// <summary>
        /// Eldeki belirtilen türdeki ilk yetenek kartını kaldırır (Örn: Pasif Kalkan tetiklendiğinde).
        /// </summary>
        public bool RemoveFirstAbilityCardOfType(AbilityCardType type)
        {
            if (AbilityCards == null) return false;
            int idx = AbilityCards.IndexOf(type);
            if (idx < 0) return false;
            AbilityCards.RemoveAt(idx);
            return true;
        }

        /// <summary>
        /// Oyuncunun elinde belirtilen yetenek kartından olup olmadığını kontrol eder.
        /// </summary>
        public bool HasAbilityCard(AbilityCardType type) => AbilityCards != null && AbilityCards.Contains(type);

        /// <summary>
        /// Oyuncunun sırası başladığında sıra içi yetenek kullanım bayraklarını sıfırlar.
        /// </summary>
        public void BeginTurnAbilityReset()
        {
            UsedAbilityThisTurn = false;
            UsedSkipThisTurn = false;
        }

        /// <summary>
        /// Oyuncunun sırası bittiğinde Sıra Atlat (SkipTurn) geçmişini günceller.
        /// </summary>
        public void EndTurnAbilityUpdate()
        {
            UsedSkipLastTurn = UsedSkipThisTurn;
            UsedSkipThisTurn = false;
        }

        /// <summary>
        /// Tüm yetenek kartlarını ve aktif durum etkilerini temizler (elenme veya oyun sıfırlamada).
        /// </summary>
        public void ClearAbilityCardsAndEffects()
        {
            if (AbilityCards == null) AbilityCards = new List<AbilityCardType>(2);
            else AbilityCards.Clear();

            HasActiveShield = false;
            IsFogged = false;
            UsedSkipLastTurn = false;
            UsedAbilityThisTurn = false;
            UsedSkipThisTurn = false;
            SoulEnergy = 0;
            LastGhostEffectTime = -10f;
        }

        /// <summary>
        /// Hayalet moduna geçerken başlangıç ruh enerjisini ayarlar.
        /// </summary>
        public void InitializeGhostEnergy(int startEnergy, int maxEnergy)
        {
            SoulEnergy = Mathf.Clamp(startEnergy, 0, maxEnergy);
        }

        /// <summary>
        /// Hayalete tur başı veya olay kaynaklı ruh enerjisi ekler.
        /// </summary>
        public void AddSoulEnergy(int amount, int maxEnergy)
        {
            SoulEnergy = Mathf.Clamp(SoulEnergy + amount, 0, maxEnergy);
        }

        /// <summary>
        /// Yeterli ruh enerjisi varsa belirtilen bedeli harcar ve true döner.
        /// </summary>
        public bool TrySpendSoulEnergy(int cost)
        {
            if (SoulEnergy < cost) return false;
            SoulEnergy -= cost;
            return true;
        }

        /// <summary>
        /// Sağlam olan ellerden rastgele birini koparır ve kopan eli döner.
        /// İkinci el de koptuğunda (oyuncu elendiğinde) eldeki tüm kartlar ve etkiler otomatik temizlenir.
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

            // Sadece sol el kaldıysa (son el kopuyor -> elenme)
            if (LeftHand == HandState.Intact)
            {
                LeftHand = HandState.Lost;
                if (!IsAlive) ClearAbilityCardsAndEffects();
                return PlayerHand.Left;
            }

            // Sadece sağ el kaldıysa (son el kopuyor -> elenme)
            if (RightHand == HandState.Intact)
            {
                RightHand = HandState.Lost;
                if (!IsAlive) ClearAbilityCardsAndEffects();
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

        /// <summary>
        /// Elleri sağlam durumuna geri döndürür (ResetHands ile eşdeğer).
        /// </summary>
        public void RestoreHands()
        {
            ResetHands();
        }
    }
}

