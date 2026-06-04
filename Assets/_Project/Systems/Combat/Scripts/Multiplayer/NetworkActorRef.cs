using Unity.Netcode;
using Runefall.Combat;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Network-serializable reference to a combat actor within the current CombatContext.
    /// Resolves actors unambiguously using team boolean and list index.
    /// </summary>
    public struct NetworkActorRef : INetworkSerializable
    {
        public bool IsPlayer;
        public int Index;

        public NetworkActorRef(ICombatActor actor, CombatContext context)
        {
            IsPlayer = false;
            Index = -1;

            if (actor == null || context == null) return;

            for (int i = 0; i < context.Players.Count; i++)
            {
                if (context.Players[i] == actor)
                {
                    IsPlayer = true;
                    Index = i;
                    return;
                }
            }

            for (int i = 0; i < context.Enemies.Count; i++)
            {
                if (context.Enemies[i] == actor)
                {
                    IsPlayer = false;
                    Index = i;
                    return;
                }
            }
        }

        public ICombatActor Resolve(CombatContext context)
        {
            if (context == null || Index < 0) return null;

            if (IsPlayer)
            {
                return Index < context.Players.Count ? context.Players[Index] : null;
            }
            else
            {
                return Index < context.Enemies.Count ? context.Enemies[Index] : null;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref IsPlayer);
            serializer.SerializeValue(ref Index);
        }
    }
}
