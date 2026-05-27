using UnityEngine;

namespace Runefall.Combat
{
    [CreateAssetMenu(menuName = "Runefall/Effects/LifestealEffect")]
    public class LifestealEffectDef : EffectDefinition
    {
        [Tooltip("Fraction of TotalDamageDealt this turn healed back to caster. [0]=R1 [1]=R2 [2]=R3")]
        public float[] percentByRank = { 0.15f, 0.25f, 0.35f };

        public override void Execute(EffectExecutionContext ctx)
        {
            if (ctx.Caster == null || ctx.TotalDamageDealt <= 0f) return;

            int   idx  = Mathf.Clamp(ctx.Rank - 1, 0, percentByRank.Length - 1);
            float heal = ctx.TotalDamageDealt * percentByRank[idx];

            ctx.Caster.Model.Heal(heal);
            ctx.TotalHealApplied += heal;
        }
    }
}
