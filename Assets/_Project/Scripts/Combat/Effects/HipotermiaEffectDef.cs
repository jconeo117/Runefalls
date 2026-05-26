using UnityEngine;
using Runefall.Characters;

namespace Runefall.Combat
{
    /// <summary>
    /// Applies a combined DEF-reduction + incoming-damage-bonus effect to the target.
    /// Designed for "Hipotermia" and any future effect that debuffs defense while marking
    /// the target for amplified incoming damage.
    ///
    /// Configure in Inspector:
    ///   effectName      = "Hipotermia"
    ///   tag             = Disadvantage
    ///   stackBySource   = true
    ///   stackable       = false   (reapply refreshes duration, does not add stacks)
    /// </summary>
    [CreateAssetMenu(menuName = "Runefall/Effects/HipotermiaEffect")]
    public class HipotermiaEffectDef : EffectDefinition
    {
        [Header("Defense Reduction")]
        [Tooltip("Multiplicative penalty to defensa. -0.20 = -20% DEF.")]
        public float defensaPenalty = -0.20f;

        [Header("Incoming Damage Amplification")]
        [Tooltip("Added to IncomingDamageBonus. 0.30 = target receives 30% more damage while debuff is active.")]
        public float incomingDamageBonus = 0.30f;

        [Header("Duration")]
        [Tooltip("Rounds this effect lasts. -1 = permanent. [0]=R1 [1]=R2 [2]=R3")]
        public int[] durationByRank = { 2, 2, 2 };

        public override void Execute(EffectExecutionContext ctx)
        {
            if (ctx.Target == null) return;

            int durIdx   = Mathf.Clamp(ctx.Rank - 1, 0, durationByRank.Length - 1);
            var statMod  = new StatModifier { defensaBonus = defensaPenalty };

            ctx.Target.Effects.Apply(new ActiveEffect
            {
                Source              = this,
                Tag                 = tag,
                Applier             = ctx.Caster,
                TurnsRemaining      = durationByRank[durIdx],
                Stacks              = 1,
                StatMod             = statMod,
                IncomingDamageBonus = incomingDamageBonus
            });
        }
    }
}
