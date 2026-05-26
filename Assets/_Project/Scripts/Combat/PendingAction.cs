using Runefall.Data;

namespace Runefall.Combat
{
    /// <summary>
    /// Captures a combat action's intent without resolving it.
    /// TurnManager builds one per SubmitSkill / enemy turn.
    /// CombatAnimationDriver calls TurnManager.ResolveAction() at the animation impact frame.
    /// </summary>
    public readonly struct PendingAction
    {
        public readonly ICombatActor Caster;
        public readonly ICombatActor Target;      // null for AoE
        public readonly SkillData    Skill;       // null if ultimate
        public readonly UltimateData Ultimate;    // null if skill
        public readonly int          Rank;
        public readonly TargetType   TargetType;
        public readonly bool         IsUltimate;

        public PendingAction(
            ICombatActor caster,
            ICombatActor target,
            SkillData    skill,
            UltimateData ultimate,
            int          rank,
            TargetType   targetType,
            bool         isUltimate)
        {
            Caster     = caster;
            Target     = target;
            Skill      = skill;
            Ultimate   = ultimate;
            Rank       = rank;
            TargetType = targetType;
            IsUltimate = isUltimate;
        }
    }
}
