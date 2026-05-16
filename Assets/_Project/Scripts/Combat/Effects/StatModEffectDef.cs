using UnityEngine;
using Runefall.Characters;

namespace Runefall.Combat
{
    [CreateAssetMenu(menuName = "Runefall/Effects/StatModEffect")]
    public class StatModEffectDef : EffectDefinition
    {
        [Header("Targeting")]
        public EffectTarget effectTarget = EffectTarget.Target;

        [Header("Stat")]
        public StatId stat;
        [Tooltip("Signed value. Ataque/Defensa: fractional (0.2 = +20%). Sub-stats: additive flat.")]
        public float[] valueByRank    = { 0f, 0f, 0f };
        [Tooltip("Duration in rounds. Use large value (99) for permanent-until-cleansed.")]
        public int[]   durationByRank = { 2, 3, 4 };

        public override void Execute(EffectExecutionContext ctx)
        {
            var recipient = effectTarget == EffectTarget.Caster ? ctx.Caster : ctx.Target;
            if (recipient == null) return;

            int   idx      = Mathf.Clamp(ctx.Rank - 1, 0, valueByRank.Length - 1);
            int   durIdx   = Mathf.Clamp(ctx.Rank - 1, 0, durationByRank.Length - 1);
            float value    = valueByRank[idx];
            int   duration = durationByRank[durIdx];

            var mod = BuildModifier(stat, value);

            var effect = new ActiveEffect
            {
                Source         = this,
                Tag            = tag,
                Flag           = EffectFlag.None,
                Applier        = ctx.Caster,
                TurnsRemaining = duration,
                Stacks         = 1,
                StatMod        = mod
            };

            recipient.Effects.Apply(effect);
        }

        private static StatModifier BuildModifier(StatId id, float value) => id switch
        {
            StatId.Ataque           => new StatModifier { ataqueBonus           = value },
            StatId.Defensa          => new StatModifier { defensaBonus          = value },
            StatId.Perforacion      => new StatModifier { perforacionBonus      = value },
            StatId.Resistencia      => new StatModifier { resistenciaBonus      = value },
            StatId.CritChance       => new StatModifier { critChanceBonus       = value },
            StatId.CritDano         => new StatModifier { critDañoBonus         = value },
            StatId.ResistenciaCrit  => new StatModifier { resistenciaCritBonus  = value },
            StatId.DefensaCrit      => new StatModifier { defensaCritBonus      = value },
            StatId.RoboDeVida       => new StatModifier { roboDeVidaBonus       = value },
            StatId.TasaRegen        => new StatModifier { tasaRegenBonus        = value },
            StatId.TasaRecuperacion => new StatModifier { tasaRecuperacionBonus = value },
            _                       => new StatModifier()
        };
    }
}
