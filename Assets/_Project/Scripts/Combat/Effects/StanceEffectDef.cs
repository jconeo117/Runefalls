using UnityEngine;

namespace Runefall.Combat
{
    /// <summary>
    /// Shell — architecture placeholder for stance mechanics.
    /// Applies a Stance-tagged ActiveEffect so AdvantageCount/DisadvantageCount and
    /// limpieza mechanics see it. No card-filtering or counter-attack logic yet.
    /// </summary>
    [CreateAssetMenu(menuName = "Runefall/Effects/StanceEffect")]
    public class StanceEffectDef : EffectDefinition
    {
        public string stanceId;
        [Tooltip("[0]=R1 [1]=R2 [2]=R3 — rounds. 0 = until replaced.")]
        public int[] durationByRank = { 2, 3, 4 };

        public override void Execute(EffectExecutionContext ctx)
        {
            if (ctx.Caster == null) return;
            int durIdx = Mathf.Clamp(ctx.Rank - 1, 0, durationByRank.Length - 1);
            int dur    = durationByRank[durIdx] == 0 ? 99 : durationByRank[durIdx];

            // Remove any existing stance first (only one active at a time)
            ctx.Caster.Effects.RemoveByTag(EffectTag.Stance);

            ctx.Caster.Effects.Apply(new ActiveEffect
            {
                Source         = this,
                Tag            = EffectTag.Stance,
                Flag           = EffectFlag.None,
                Applier        = ctx.Caster,
                TurnsRemaining = dur,
                Stacks         = 1
            });
        }
    }
}
