using UnityEngine;
using Runefall.Characters;

namespace Runefall.Combat
{
    /// <summary>
    /// Effect that, while active on an actor, modifies that actor's OUTGOING damage through conditional
    /// rules (e.g. Monarca del Hielo: x2 vs frozen). Optionally also applies a flat stat buff (e.g. +50%
    /// ATK), so a single asset can be the whole "Monarca" effect.
    ///
    /// The modifiers are evaluated DETERMINISTICALLY in the damage pipeline (DamageEffectDef gathers the
    /// caster's active OutgoingDamageModifiers before computing) — never as post-hoc re-damage, so the
    /// bonus passes correctly through crit, defense and DamageReceivedMultiplier.
    /// </summary>
    [CreateAssetMenu(menuName = "Runefall/Effects/OutgoingModifierEffect")]
    public class OutgoingModifierEffectDef : EffectDefinition
    {
        [Header("Targeting / Duration")]
        public EffectTarget effectTarget = EffectTarget.Caster;
        [Tooltip("Turnos que dura. -1 = permanente. [0]=R1 [1]=R2 [2]=R3")]
        public int[] durationByRank = { 1, 1, 1 };

        [Header("Optional stat buff (ej. Monarca +50% ATK)")]
        public bool   applyStat;
        public StatId stat;
        [Tooltip("Ataque/Defensa: fraccional (0.5 = +50%). Sub-stats: aditivo plano.")]
        public float  statValue;

        [Header("Conditional outgoing-damage modifiers")]
        public ConditionalDamageModifier[] modifiers;

        public override void Execute(EffectExecutionContext ctx)
        {
            var recipient = effectTarget == EffectTarget.Caster ? ctx.Caster : ctx.Target;
            if (recipient == null) return;

            int durIdx = Mathf.Clamp(ctx.Rank - 1, 0, durationByRank.Length - 1);

            recipient.Effects.Apply(new ActiveEffect
            {
                Source                  = this,
                Tag                     = tag,
                Applier                 = ctx.Caster,
                TurnsRemaining          = durationByRank[durIdx],
                Stacks                  = 1,
                StatMod                 = applyStat ? BuildModifier(stat, statValue) : null,
                OutgoingDamageModifiers = modifiers
            });
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
