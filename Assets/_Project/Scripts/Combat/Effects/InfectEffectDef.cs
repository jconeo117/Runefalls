using UnityEngine;

namespace Runefall.Combat
{
    [CreateAssetMenu(menuName = "Runefall/Effects/InfectEffect")]
    public class InfectEffectDef : EffectDefinition
    {
        [Tooltip("[0]=R1 [1]=R2 [2]=R3 — rounds")]
        public int[] durationByRank = { 2, 3, 4 };

        public override void Execute(EffectExecutionContext ctx)
        {
            if (ctx.Target == null) return;
            int durIdx = Mathf.Clamp(ctx.Rank - 1, 0, durationByRank.Length - 1);

            ctx.Target.Effects.Apply(new ActiveEffect
            {
                Source         = this,
                Tag            = EffectTag.Disadvantage,
                Flag           = EffectFlag.Infected,
                Applier        = ctx.Caster,
                TurnsRemaining = durationByRank[durIdx],
                Stacks         = 1
            });
        }
    }
}
