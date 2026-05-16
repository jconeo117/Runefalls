using System;
using UnityEngine;
using Runefall.Characters;

namespace Runefall.Combat
{
    [CreateAssetMenu(menuName = "Runefall/Effects/ArrebatoEffect")]
    public class ArrebatoEffectDef : EffectDefinition
    {
        [Header("Stat Steal")]
        public StatId stat;
        [Tooltip("Fraction stolen (0.15 = 15%). Target loses X%, caster gains equivalent boost.")]
        public float[] percentByRank  = { 0.15f, 0.25f, 0.35f };
        public int[]   durationByRank = { 2, 3, 4 };

        public override void Execute(EffectExecutionContext ctx)
        {
            if (ctx.Target == null || ctx.Caster == null) return;

            int   idx      = Mathf.Clamp(ctx.Rank - 1, 0, percentByRank.Length - 1);
            int   durIdx   = Mathf.Clamp(ctx.Rank - 1, 0, durationByRank.Length - 1);
            float pct      = percentByRank[idx];
            int   duration = durationByRank[durIdx];

            // Compute actual amount stolen from target's effective stat
            float stolenFlat = GetFlatValue(ctx.Target.Model.EffectiveStats, stat) * pct;
            if (stolenFlat <= 0f) return;

            BuildPairedModifiers(stat, stolenFlat,
                ctx.Target.Model.EffectiveStats,
                ctx.Caster.Model.EffectiveStats,
                out StatModifier targetMod,
                out StatModifier casterMod);

            string groupId = Guid.NewGuid().ToString();

            ctx.Target.Effects.Apply(new ActiveEffect
            {
                Source         = this,
                Tag            = EffectTag.Disadvantage,
                Applier        = ctx.Caster,
                TurnsRemaining = duration,
                StoredValue    = stolenFlat,
                StatMod        = targetMod,
                GroupId        = groupId,
                LinkedActor    = ctx.Caster
            });

            ctx.Caster.Effects.Apply(new ActiveEffect
            {
                Source         = this,
                Tag            = EffectTag.Advantage,
                Applier        = ctx.Caster,
                TurnsRemaining = duration,
                StoredValue    = stolenFlat,
                StatMod        = casterMod,
                GroupId        = groupId,
                LinkedActor    = ctx.Target
            });
        }

        private static float GetFlatValue(CharacterStats s, StatId id) => id switch
        {
            StatId.Ataque           => s.ofensivas.ataque,
            StatId.Defensa          => s.defensivas.defensa,
            StatId.Perforacion      => s.ofensivas.perforacion,
            StatId.Resistencia      => s.defensivas.resistencia,
            StatId.CritChance       => s.ofensivas.critChance,
            StatId.CritDano         => s.ofensivas.critDaño,
            StatId.ResistenciaCrit  => s.defensivas.resistenciaCrit,
            StatId.DefensaCrit      => s.defensivas.defensaCrit,
            StatId.RoboDeVida       => s.vitales.roboDeVida,
            StatId.TasaRegen        => s.vitales.tasaRegen,
            StatId.TasaRecuperacion => s.vitales.tasaRecuperacion,
            _                       => 0f
        };

        // Multiplicative stats (ataque, defensa): express stolen flat as a fraction of each actor's own base.
        // Sub-stats: apply flat additive ±stolenFlat directly.
        private static void BuildPairedModifiers(StatId id, float stolenFlat,
            CharacterStats targetStats, CharacterStats casterStats,
            out StatModifier targetMod, out StatModifier casterMod)
        {
            switch (id)
            {
                case StatId.Ataque:
                    float targetAtk = Math.Max(1f, targetStats.ofensivas.ataque);
                    float casterAtk = Math.Max(1f, casterStats.ofensivas.ataque);
                    targetMod = new StatModifier { ataqueBonus = -(stolenFlat / targetAtk) };
                    casterMod = new StatModifier { ataqueBonus =  (stolenFlat / casterAtk) };
                    break;
                case StatId.Defensa:
                    float targetDef = Math.Max(1f, targetStats.defensivas.defensa);
                    float casterDef = Math.Max(1f, casterStats.defensivas.defensa);
                    targetMod = new StatModifier { defensaBonus = -(stolenFlat / targetDef) };
                    casterMod = new StatModifier { defensaBonus =  (stolenFlat / casterDef) };
                    break;
                default:
                    // Sub-stats: additive flat
                    targetMod = BuildSubStatMod(id, -stolenFlat);
                    casterMod = BuildSubStatMod(id, +stolenFlat);
                    break;
            }
        }

        private static StatModifier BuildSubStatMod(StatId id, float v) => id switch
        {
            StatId.Perforacion      => new StatModifier { perforacionBonus      = v },
            StatId.Resistencia      => new StatModifier { resistenciaBonus      = v },
            StatId.CritChance       => new StatModifier { critChanceBonus       = v },
            StatId.CritDano         => new StatModifier { critDañoBonus         = v },
            StatId.ResistenciaCrit  => new StatModifier { resistenciaCritBonus  = v },
            StatId.DefensaCrit      => new StatModifier { defensaCritBonus      = v },
            StatId.RoboDeVida       => new StatModifier { roboDeVidaBonus       = v },
            StatId.TasaRegen        => new StatModifier { tasaRegenBonus        = v },
            StatId.TasaRecuperacion => new StatModifier { tasaRecuperacionBonus = v },
            _                       => new StatModifier()
        };
    }
}
