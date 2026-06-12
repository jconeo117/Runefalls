using UnityEngine;

namespace Runefall.Combat
{
    /// <summary>True when the target carries a given EffectBehavior (e.g. SkipTurn = frozen/stunned).</summary>
    [CreateAssetMenu(menuName = "Runefall/Conditions/Target Has Behavior")]
    public class TargetHasBehaviorCondition : DamageCondition
    {
        public EffectBehavior behavior = EffectBehavior.SkipTurn;

        public override bool Matches(ICombatActor caster, ICombatActor target)
            => target != null && target.Effects.HasBehavior(behavior);
    }
}
