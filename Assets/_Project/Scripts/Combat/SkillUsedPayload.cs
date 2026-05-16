using Runefall.Data;

namespace Runefall.Combat
{
    /// <summary>
    /// Payload raised by SkillUsedEvent when TurnManager resolves a player or enemy action.
    /// Lives in domain so SkillUsedEvent SO (Runefall.Data) can reference it without depending
    /// on Runefall.Presentation — Data → Domain is a legal dependency direction.
    /// </summary>
    public readonly struct SkillUsedPayload
    {
        public readonly ICombatActor Caster;
        public readonly ICombatActor Target;
        public readonly SkillData    Skill;
        public readonly int          Rank;
        public readonly bool         IsUltimate;
        public readonly bool         IsAoe;

        public SkillUsedPayload(
            ICombatActor caster, ICombatActor target,
            SkillData skill, int rank, bool isUltimate, bool isAoe)
        {
            Caster     = caster;
            Target     = target;
            Skill      = skill;
            Rank       = rank;
            IsUltimate = isUltimate;
            IsAoe      = isAoe;
        }

        public static SkillUsedPayload FromResult(CombatActionResult r) =>
            new(r.Caster, r.Target, r.Skill, r.Rank,
                isUltimate: r.Skill == null,
                isAoe:      r.IsAoe);
    }
}
