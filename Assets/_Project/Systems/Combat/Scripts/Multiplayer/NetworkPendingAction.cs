using Unity.Netcode;
using Unity.Collections;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Network-serializable wrapper for PendingAction.
    /// Maps ScriptableObjects to names and actors to NetworkActorRefs.
    /// </summary>
    public struct NetworkPendingAction : INetworkSerializable
    {
        public NetworkActorRef Caster;
        public NetworkActorRef Target;
        public FixedString32Bytes SkillName;
        public FixedString32Bytes UltimateName;
        public int Rank;
        public TargetType TargetType;
        public bool IsUltimate;

        public NetworkPendingAction(PendingAction pending, CombatContext context)
        {
            Caster = new NetworkActorRef(pending.Caster, context);
            Target = new NetworkActorRef(pending.Target, context);
            SkillName = pending.Skill != null ? (FixedString32Bytes)pending.Skill.skillName : default;
            UltimateName = pending.Ultimate != null ? (FixedString32Bytes)pending.Ultimate.ultimateName : default;
            Rank = pending.Rank;
            TargetType = pending.TargetType;
            IsUltimate = pending.IsUltimate;
        }

        public PendingAction Resolve(CombatContext context, MultiplayerCombatRegistry registry)
        {
            var casterActor = Caster.Resolve(context);
            var targetActor = Target.Resolve(context);
            var skillData = !IsUltimate ? registry.GetSkill(SkillName.ToString()) : null;
            var ultimateData = IsUltimate ? registry.GetUltimate(UltimateName.ToString()) : null;

            return new PendingAction(
                casterActor,
                targetActor,
                skillData,
                ultimateData,
                Rank,
                TargetType,
                IsUltimate
            );
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Caster);
            serializer.SerializeValue(ref Target);
            serializer.SerializeValue(ref SkillName);
            serializer.SerializeValue(ref UltimateName);
            serializer.SerializeValue(ref Rank);
            serializer.SerializeValue(ref TargetType);
            serializer.SerializeValue(ref IsUltimate);
        }
    }
}
