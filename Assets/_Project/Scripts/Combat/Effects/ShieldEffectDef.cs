using UnityEngine;

namespace Runefall.Combat
{
    public enum ShieldMode { Flat, PercentMaxHP }

    [CreateAssetMenu(menuName = "Runefall/Effects/ShieldEffect")]
    public class ShieldEffectDef : EffectDefinition
    {
        public EffectTarget effectTarget = EffectTarget.Caster;
        public ShieldMode   shieldMode   = ShieldMode.PercentMaxHP;

        [Tooltip("[0]=R1 [1]=R2 [2]=R3")]
        public float[] valueByRank = { 0.15f, 0.2f, 0.3f };

        public override void Execute(EffectExecutionContext ctx)
        {
            var recipient = effectTarget == EffectTarget.Caster ? ctx.Caster : ctx.Target;
            if (recipient == null || !recipient.IsAlive) return;

            int   idx    = Mathf.Clamp(ctx.Rank - 1, 0, valueByRank.Length - 1);
            float amount = shieldMode == ShieldMode.Flat
                ? valueByRank[idx]
                : recipient.Model.MaxHP * valueByRank[idx];

            recipient.Model.AddShield(amount);

            // Track as an advantage so AdvantageCount, ruptura, and limpieza see it
            recipient.Effects.Apply(new ActiveEffect
            {
                Source         = this,
                Tag            = EffectTag.Advantage,
                Flag           = EffectFlag.None,
                Applier        = ctx.Caster,
                TurnsRemaining = 99,  // Shield lasts until consumed or cleansed
                Stacks         = 1,
                StoredValue    = amount
            });
        }
    }
}
