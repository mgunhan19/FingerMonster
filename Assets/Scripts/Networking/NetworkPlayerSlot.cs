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
        public FixedString64Bytes PlayerName;
        public bool IsBot;
        public bool IsReady;
        public bool IsAlive;
        public int RemainingHands;
        public int Score;
        public Color AvatarColor;

        public NetworkPlayerSlot(ulong clientId, int seatIndex, string name, bool isBot, Color color)
        {
            ClientId = clientId;
            SeatIndex = seatIndex;
            PlayerName = new FixedString64Bytes(name);
            IsBot = isBot;
            IsReady = isBot; // Botlar varsayılan olarak hazırdır
            IsAlive = true;
            RemainingHands = 2;
            Score = 0;
            AvatarColor = color;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref SeatIndex);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref IsBot);
            serializer.SerializeValue(ref IsReady);
            serializer.SerializeValue(ref IsAlive);
            serializer.SerializeValue(ref RemainingHands);
            serializer.SerializeValue(ref Score);
            serializer.SerializeValue(ref AvatarColor);
        }

        public bool Equals(NetworkPlayerSlot other)
        {
            return ClientId == other.ClientId && SeatIndex == other.SeatIndex;
        }
    }
}

