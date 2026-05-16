using UnityEngine;

namespace Runefall.Combat
{
    [CreateAssetMenu(menuName = "Runefall/Effects/LlamaradaEffect")]
    public class LlamaradaEffectDef : EffectDefinition
    {
        [Tooltip("Each stack adds +10% incoming damage. Stacks share duration; each application refreshes.")]
        public int[] durationByRank = { 2, 3, 4 };

        public override void Execute(EffectExecutionContext ctx)
        {
            if (ctx.Target == null) return;
            int durIdx = Mathf.Clamp(ctx.Rank - 1, 0, durationByRank.Length - 1);

            ctx.Target.Effects.Apply(new ActiveEffect
            {
                Source         = this,
                Tag            = EffectTag.Disadvantage,
                Flag           = EffectFlag.Llamarada,
                Applier        = ctx.Caster,
                TurnsRemaining = durationByRank[durIdx],
                Stacks         = 1
            });
        }
    }
}
