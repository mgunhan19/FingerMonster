using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace FingerMonster.Networking
{
    /// <summary>
    /// Masadaki 4 koltuktan birine oturan insan veya bot oyuncunun senkronize ağ verisi.
    /// INetworkSerializable arabirimi ile Netcode üzerinden verimli bayt serileştirmesi sağlar.
    /// </summary>
    public struct NetworkPlayerSlot : INetworkSerializable, IEquatable<NetworkPlayerSlot>
    {
        public ulong ClientId;
        public int SeatIndex;
        public int ArchetypeIndex;
        public FixedString64Bytes PlayerName;
        public bool IsBot;
        public bool IsReady;
        public bool IsAlive;
        public bool LeftHandIntact;
        public bool RightHandIntact;
        public int RemainingHands;
        public int Score;
        public Color AvatarColor;
        public int AbilityCardCount;
        public bool HasActiveShield;
        public bool IsFogged;
        public bool IsGhost;

        public NetworkPlayerSlot(ulong clientId, int seatIndex, string name, bool isBot, Color color, int archetypeIndex = -1)
        {
            ClientId = clientId;
            SeatIndex = seatIndex;
            ArchetypeIndex = archetypeIndex >= 0 ? archetypeIndex : (seatIndex % 4);
            PlayerName = new FixedString64Bytes(name);
            IsBot = isBot;
            IsReady = isBot; // Botlar varsayılan olarak hazırdır
            IsAlive = true;
            LeftHandIntact = true;
            RightHandIntact = true;
            RemainingHands = 2;
            Score = 0;
            AvatarColor = color;
            AbilityCardCount = 0;
            HasActiveShield = false;
            IsFogged = false;
            IsGhost = false;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref SeatIndex);
            serializer.SerializeValue(ref ArchetypeIndex);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref IsBot);
            serializer.SerializeValue(ref IsReady);
            serializer.SerializeValue(ref IsAlive);
            serializer.SerializeValue(ref LeftHandIntact);
            serializer.SerializeValue(ref RightHandIntact);
            serializer.SerializeValue(ref RemainingHands);
            serializer.SerializeValue(ref Score);
            serializer.SerializeValue(ref AvatarColor);
            serializer.SerializeValue(ref AbilityCardCount);
            serializer.SerializeValue(ref HasActiveShield);
            serializer.SerializeValue(ref IsFogged);
            serializer.SerializeValue(ref IsGhost);
        }

        public bool Equals(NetworkPlayerSlot other)
        {
            return ClientId == other.ClientId &&
                   SeatIndex == other.SeatIndex &&
                   ArchetypeIndex == other.ArchetypeIndex &&
                   PlayerName.Equals(other.PlayerName) &&
                   IsBot == other.IsBot &&
                   IsReady == other.IsReady &&
                   IsAlive == other.IsAlive &&
                   LeftHandIntact == other.LeftHandIntact &&
                   RightHandIntact == other.RightHandIntact &&
                   RemainingHands == other.RemainingHands &&
                   Score == other.Score &&
                   AvatarColor == other.AvatarColor &&
                   AbilityCardCount == other.AbilityCardCount &&
                   HasActiveShield == other.HasActiveShield &&
                   IsFogged == other.IsFogged &&
                   IsGhost == other.IsGhost;
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkPlayerSlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + ClientId.GetHashCode();
                hash = hash * 31 + SeatIndex.GetHashCode();
                hash = hash * 31 + ArchetypeIndex.GetHashCode();
                hash = hash * 31 + PlayerName.GetHashCode();
                hash = hash * 31 + IsBot.GetHashCode();
                hash = hash * 31 + IsReady.GetHashCode();
                hash = hash * 31 + IsAlive.GetHashCode();
                hash = hash * 31 + LeftHandIntact.GetHashCode();
                hash = hash * 31 + RightHandIntact.GetHashCode();
                hash = hash * 31 + RemainingHands.GetHashCode();
                hash = hash * 31 + Score.GetHashCode();
                hash = hash * 31 + AbilityCardCount.GetHashCode();
                hash = hash * 31 + HasActiveShield.GetHashCode();
                hash = hash * 31 + IsFogged.GetHashCode();
                hash = hash * 31 + IsGhost.GetHashCode();
                return hash;
            }
        }
    }
}

