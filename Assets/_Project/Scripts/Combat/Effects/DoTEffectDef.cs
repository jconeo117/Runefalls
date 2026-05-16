using UnityEngine;

namespace Runefall.Combat
{
    [CreateAssetMenu(menuName = "Runefall/Effects/DoTEffect")]
    public class DoTEffectDef : EffectDefinition
    {
        [Header("Damage per Tick")]
        [Tooltip("How tick damage is calculated at application time.")]
        public DoTMode doTMode = DoTMode.PercentDamageDealt;

        [Tooltip("[0]=R1 [1]=R2 [2]=R3  — fraction (0.4 = 40%)")]
        public float[] percentByRank  = { 0.4f, 0.5f, 0.6f };
        public int[]   durationByRank = { 2, 3, 4 };

        public override void Execute(EffectExecutionContext ctx)
        {
            if (ctx.Target == null || !ctx.Target.IsAlive) return;

            int   idx      = Mathf.Clamp(ctx.Rank - 1, 0, percentByRank.Length - 1);
            int   durIdx   = Mathf.Clamp(ctx.Rank - 1, 0, durationByRank.Length - 1);
            float percent  = percentByRank[idx];

            float tickDamage = doTMode switch
            {
                DoTMode.PercentMaxHP     => ctx.Target.Model.MaxHP * percent,
                DoTMode.PercentCurrentHP => ctx.Target.Model.CurrentHP * percent,
                _                        => ctx.TotalDamageDealt * percent   // PercentDamageDealt
            };

            if (tickDamage <= 0f) return;

            ctx.Target.Effects.Apply(new ActiveEffect
            {
                Source         = this,
                Tag            = EffectTag.Disadvantage,
                Flag           = EffectFlag.None,
                Applier        = ctx.Caster,
                TurnsRemaining = durationByRank[durIdx],
                Stacks         = 1,
                StoredValue    = tickDamage,
                TickDamage     = true
            });
        }
    }
}
