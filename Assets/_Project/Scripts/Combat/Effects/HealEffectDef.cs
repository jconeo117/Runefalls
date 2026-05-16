using UnityEngine;

namespace Runefall.Combat
{
    [CreateAssetMenu(menuName = "Runefall/Effects/HealEffect")]
    public class HealEffectDef : EffectDefinition
    {
        [Header("Targeting")]
        public EffectTarget effectTarget = EffectTarget.Caster;
        public HealSource   source       = HealSource.MaxHP;

        [Tooltip("[0]=R1 [1]=R2 [2]=R3")]
        public float[] percentByRank = { 0.2f, 0.3f, 0.4f };

        public override void Execute(EffectExecutionContext ctx)
        {
            var recipient = effectTarget == EffectTarget.Caster ? ctx.Caster : ctx.Target;
            if (recipient == null || !recipient.IsAlive) return;

            int   idx  = Mathf.Clamp(ctx.Rank - 1, 0, percentByRank.Length - 1);
            float @base = source switch
            {
                HealSource.CurrentHP    => recipient.Model.CurrentHP,
                HealSource.CasterAttack => ctx.Caster.Model.EffectiveStats.ofensivas.ataque,
                HealSource.CasterMaxHP  => ctx.Caster.Model.MaxHP,
                _                       => recipient.Model.MaxHP
            };

            float amount = @base * percentByRank[idx];
            recipient.Model.Heal(amount);
            ctx.TotalHealApplied += amount;
        }
    }
}
