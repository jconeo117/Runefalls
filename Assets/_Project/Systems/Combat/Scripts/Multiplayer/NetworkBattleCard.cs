using Unity.Netcode;
using Unity.Collections;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Network-serializable representation of a BattleCard.
    /// ScriptableObjects cannot be serialized directly, so this references them by name/ID.
    /// Element is stored directly so visual display works even if registry lookup fails.
    /// </summary>
    public struct NetworkBattleCard : INetworkSerializable
    {
        public int                CardId;
        public FixedString32Bytes SkillName;
        public FixedString32Bytes UltimateName;
        public int                Rank;
        public bool               IsUltimate;
        public int                Element;   // ElementType cast to int — avoids registry dependency for color

        public BattleCard Resolve(MultiplayerCombatRegistry registry)
        {
            var skillData    = !IsUltimate ? registry.GetSkill(SkillName.ToString())         : null;
            var ultimateData =  IsUltimate ? registry.GetUltimate(UltimateName.ToString())   : null;
            return new BattleCard(CardId, skillData, ultimateData, Rank, IsUltimate);
        }

        public ElementType ElementType => (ElementType)Element;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref CardId);
            serializer.SerializeValue(ref SkillName);
            serializer.SerializeValue(ref UltimateName);
            serializer.SerializeValue(ref Rank);
            serializer.SerializeValue(ref IsUltimate);
            serializer.SerializeValue(ref Element);
        }
    }
}
