using Unity.Netcode;
using Unity.Collections;
using Runefall.Combat;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Network-serializable wrapper for CombatActionResult.
    /// Maps ScriptableObjects to names and actors to NetworkActorRefs.
    /// </summary>
    public struct NetworkCombatActionResult : INetworkSerializable
    {
        public NetworkActorRef Caster;
        public NetworkActorRef Target;
        public FixedString32Bytes SkillName;
        public int Rank;
        public float DamageDealt;
        public bool IsCrit;
        public float LifeStealApplied;
        public float HealApplied;
        public bool IsAoe;

        public float TargetPostHP;
        public float TargetPostShield;

        public NetworkCombatActionResult(CombatActionResult result, CombatContext context)
        {
            Caster = new NetworkActorRef(result.Caster, context);
            Target = new NetworkActorRef(result.Target, context);
            SkillName = result.Skill != null ? (FixedString32Bytes)result.Skill.skillName : default;
            Rank = result.Rank;
            DamageDealt = result.DamageDealt;
            IsCrit = result.IsCrit;
            LifeStealApplied = result.LifeStealApplied;
            HealApplied = result.HealApplied;
            IsAoe = result.IsAoe;

            // Store post-action state of target
            TargetPostHP = result.Target != null ? result.Target.Model.CurrentHP : 0f;
            TargetPostShield = result.Target != null ? result.Target.Model.CurrentShield : 0f;
        }

        public CombatActionResult Resolve(CombatContext context, MultiplayerCombatRegistry registry)
        {
            var casterActor = Caster.Resolve(context);
            var targetActor = Target.Resolve(context);
            var skillData = registry.GetSkill(SkillName.ToString());

            return new CombatActionResult(
                casterActor,
                targetActor,
                skillData,
                Rank,
                DamageDealt,
                IsCrit,
                LifeStealApplied,
                HealApplied,
                IsAoe
            );
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Caster);
            serializer.SerializeValue(ref Target);
            serializer.SerializeValue(ref SkillName);
            serializer.SerializeValue(ref Rank);
            serializer.SerializeValue(ref DamageDealt);
            serializer.SerializeValue(ref IsCrit);
            serializer.SerializeValue(ref LifeStealApplied);
            serializer.SerializeValue(ref HealApplied);
            serializer.SerializeValue(ref IsAoe);
            serializer.SerializeValue(ref TargetPostHP);
            serializer.SerializeValue(ref TargetPostShield);
        }
    }
}
