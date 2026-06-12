using UnityEngine;

namespace Runefall.Combat
{
    /// <summary>True when the target carries a specific effect (matched by the EffectDefinition asset).</summary>
    [CreateAssetMenu(menuName = "Runefall/Conditions/Target Has Effect")]
    public class TargetHasEffectCondition : DamageCondition
    {
        [Tooltip("The effect asset to look for on the target.")]
        public EffectDefinition effect;

        public override bool Matches(ICombatActor caster, ICombatActor target)
        {
            if (target == null || effect == null) return false;
            var fx = target.Effects.ActiveEffects;
            for (int i = 0; i < fx.Count; i++)
                if (fx[i].Source == effect) return true;
            return false;
        }
    }
}
