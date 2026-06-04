using System;
using Unity.Netcode;

namespace Runefall.Multiplayer
{
    public struct NetworkSlotState : INetworkSerializable, IEquatable<NetworkSlotState>
    {
        public bool   IsOccupied;
        public ulong  OwnerClientId;
        public NetworkBattleCard Card;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref IsOccupied);
            serializer.SerializeValue(ref OwnerClientId);
            Card.NetworkSerialize(serializer);
        }

        public bool Equals(NetworkSlotState other) =>
            IsOccupied == other.IsOccupied &&
            OwnerClientId == other.OwnerClientId &&
            Card.CardId == other.Card.CardId &&
            Card.Rank   == other.Card.Rank;
    }
}
