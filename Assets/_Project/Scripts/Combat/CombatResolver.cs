using System;
using System.Collections.Generic;
using Runefall.Data;

namespace Runefall.Combat
{
    public readonly struct CombatActionResult
    {
        public readonly ICombatActor Caster;
        public readonly ICombatActor Target;
        public readonly SkillData    Skill;
        public readonly int          Rank;
        public readonly float        DamageDealt;
        public readonly bool         IsCrit;
        public readonly float        LifeStealApplied;
        public readonly float        HealApplied;
        /// <summary>True when this result is one of multiple hits from an AoE skill.</summary>
        public readonly bool         IsAoe;

        public CombatActionResult(
            ICombatActor caster, ICombatActor target,
            SkillData skill, int rank,
            float damageDealt, bool isCrit,
            float lifeStealApplied, float healApplied,
            bool isAoe = false)
        {
            Caster            = caster;
            Target            = target;
            Skill             = skill;
            Rank              = rank;
            DamageDealt       = damageDealt;
            IsCrit            = isCrit;
            LifeStealApplied  = lifeStealApplied;
            HealApplied       = healApplied;
            IsAoe             = isAoe;
        }
    }

    public static class CombatResolver
    {
        /// <summary>
        /// Applies a skill at the given rank from caster to target.
        /// rank: 1–3. Clamped to effectsByRank bounds.
        /// For AoE skills use ExecuteAll; this always hits a single target regardless of targetType.
        /// </summary>
        public static CombatActionResult Execute(
            SkillData skill, int rank,
            ICombatActor caster, ICombatActor target)
        {
            if (skill  == null) throw new ArgumentNullException(nameof(skill));
            if (caster == null) throw new ArgumentNullException(nameof(caster));
            if (target == null) throw new ArgumentNullException(nameof(target));

            if (skill.effectsByRank == null || skill.effectsByRank.Length == 0)
                throw new InvalidOperationException($"SkillData '{skill.skillName}' has no effectsByRank.");

            int idx    = Math.Max(0, Math.Min(rank - 1, skill.effectsByRank.Length - 1));
            var (dmg, crit, lifeSteal, heal) = ApplyEffect(skill.effectsByRank[idx], caster, target, rank);
            return new CombatActionResult(caster, target, skill, rank, dmg, crit, lifeSteal, heal);
        }

        /// <summary>
        /// AoE path: applies skill to all alive targets in the list.
        /// Returns one result per target hit. Caller (TurnManager) fires OnActionResolved per result.
        /// </summary>
        public static CombatActionResult[] ExecuteAll(
            SkillData skill, int rank,
            ICombatActor caster, IReadOnlyList<ICombatActor> targets)
        {
            if (skill    == null) throw new ArgumentNullException(nameof(skill));
            if (caster   == null) throw new ArgumentNullException(nameof(caster));
            if (targets  == null) throw new ArgumentNullException(nameof(targets));

            if (skill.effectsByRank == null || skill.effectsByRank.Length == 0)
                throw new InvalidOperationException($"SkillData '{skill.skillName}' has no effectsByRank.");

            int idx     = Math.Max(0, Math.Min(rank - 1, skill.effectsByRank.Length - 1));
            var effect  = skill.effectsByRank[idx];
            var results = new List<CombatActionResult>();

            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (!t.IsAlive) continue;
                var (dmg, crit, lifeSteal, heal) = ApplyEffect(effect, caster, t, rank);
                results.Add(new CombatActionResult(caster, t, skill, rank, dmg, crit, lifeSteal, heal, isAoe: true));
            }

            return results.ToArray();
        }

        /// <summary>
        /// Applies an ultimate ability from caster to target.
        /// Ultimate has a single SkillEffect (no rank variation).
        /// result.Skill is null — callers can check CardSlot.IsUltimate for context.
        /// </summary>
        public static CombatActionResult ExecuteUltimate(
            UltimateData ultimate, ICombatActor caster, ICombatActor target)
        {
            if (ultimate == null) throw new ArgumentNullException(nameof(ultimate));
            if (caster   == null) throw new ArgumentNullException(nameof(caster));
            if (target   == null) throw new ArgumentNullException(nameof(target));

            if (ultimate.effect == null)
                return new CombatActionResult(caster, target, null, 3, 0f, false, 0f, 0f);

            var (dmg, crit, lifeSteal, heal) = ApplyEffect(ultimate.effect, caster, target, 3);
            return new CombatActionResult(caster, target, null, 3, dmg, crit, lifeSteal, heal);
        }

        /// <summary>
        /// AoE ultimate path. Same as ExecuteAll but for UltimateData.
        /// </summary>
        public static CombatActionResult[] ExecuteUltimateAll(
            UltimateData ultimate, ICombatActor caster, IReadOnlyList<ICombatActor> targets)
        {
            if (ultimate == null) throw new ArgumentNullException(nameof(ultimate));
            if (caster   == null) throw new ArgumentNullException(nameof(caster));
            if (targets  == null) throw new ArgumentNullException(nameof(targets));

            if (ultimate.effect == null) return Array.Empty<CombatActionResult>();

            var results = new List<CombatActionResult>();
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (!t.IsAlive) continue;
                var (dmg, crit, lifeSteal, heal) = ApplyEffect(ultimate.effect, caster, t, 3);
                results.Add(new CombatActionResult(caster, t, null, 3, dmg, crit, lifeSteal, heal, isAoe: true));
            }
            return results.ToArray();
        }

        // Shared effect application — runs the new pipeline if effects[] is populated,
        // otherwise falls back to the legacy damageMultiplier/healPercent fields.
        private static (float dmg, bool crit, float lifeSteal, float heal) ApplyEffect(
            SkillEffect effect, ICombatActor caster, ICombatActor target, int rank = 1)
        {
            if (effect.effects is { Length: > 0 })
                return RunPipeline(effect.effects, caster, target, rank);

            return LegacyApplyEffect(effect, caster, target);
        }

        private static (float dmg, bool crit, float lifeSteal, float heal) RunPipeline(
            EffectDefinition[] effects, ICombatActor caster, ICombatActor target, int rank)
        {
            var ctx = new EffectExecutionContext
            {
                Caster = caster,
                Target = target,
                Rank   = rank
            };

            for (int i = 0; i < effects.Length; i++)
                effects[i]?.Execute(ctx);

            // Lifesteal from EffectiveStats (roboDeVida) is handled by DamageEffectDef internally.
            // Return accumulated totals so CombatActionResult stays consistent.
            return (ctx.TotalDamageDealt, ctx.IsCrit, 0f, ctx.TotalHealApplied);
        }

        private static (float dmg, bool crit, float lifeSteal, float heal) LegacyApplyEffect(
            SkillEffect effect, ICombatActor caster, ICombatActor target)
        {
            float damageDealt      = 0f;
            bool  isCrit           = false;
            float lifeStealApplied = 0f;
            float healApplied      = 0f;

            if (effect.damageMultiplier > 0f && target.IsAlive)
            {
                var dr = CombatFormulas.CalculateDamage(
                    caster.Model.EffectiveStats, target.Model.EffectiveStats,
                    caster.Element, target.Element);

                damageDealt = dr.damage * effect.damageMultiplier;
                isCrit      = dr.isCrit;
                target.Model.TakeDamage(damageDealt);

                if (dr.lifeSteal > 0f)
                {
                    lifeStealApplied = dr.lifeSteal * effect.damageMultiplier;
                    caster.Model.Heal(lifeStealApplied);
                }
            }

            if (effect.healPercent > 0f)
            {
                healApplied = target.Model.MaxHP * effect.healPercent;
                target.Model.Heal(healApplied);
            }

            return (damageDealt, isCrit, lifeStealApplied, healApplied);
        }
    }
}
