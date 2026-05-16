using System;
using UnityEngine;

namespace Runefall.Combat
{
    [Serializable]
    public struct DamageModifierEntry
    {
        public DamageModifierType type;
        [Tooltip("Used by Amplificar (% per advantage) and Ruina (% per disadvantage)")]
        public float value;
    }

    [CreateAssetMenu(menuName = "Runefall/Effects/DamageEffect")]
    public class DamageEffectDef : EffectDefinition
    {
        [Header("Base Damage")]
        [Tooltip("Which caster stat drives damage before the multiplier.")]
        public StatSource statSource = StatSource.Attack;
        [Tooltip("Damage multiplier per rank. [0]=R1 [1]=R2 [2]=R3")]
        public float[] multiplierByRank = { 1f, 1.2f, 1.5f };

        [Header("Damage Modifiers")]
        public DamageModifierEntry[] modifiers;

        public override void Execute(EffectExecutionContext ctx)
        {
            if (ctx.Target == null || !ctx.Target.IsAlive) return;

            int idx = Mathf.Clamp(ctx.Rank - 1, 0, multiplierByRank.Length - 1);

            var castStats   = ctx.Caster.Model.EffectiveStats;
            var targetStats = ctx.Target.Model.EffectiveStats;

            // Copy modifier state from context (set by earlier pipeline steps)
            bool  ignoreDefense   = ctx.IgnoreDefense;
            float critDamageMult  = ctx.CritDamageMultiplier;
            float critChanceMult  = ctx.CritChanceMultiplier;
            float damageMult      = ctx.DamageMultiplier;
            float flatBonus       = ctx.FlatBonusDamage;

            // Apply this effect's own modifiers
            if (modifiers != null)
            {
                foreach (var mod in modifiers)
                {
                    switch (mod.type)
                    {
                        case DamageModifierType.Puncion:
                            critDamageMult *= 2f;
                            break;
                        case DamageModifierType.Destello:
                            critChanceMult *= 3f;
                            break;
                        case DamageModifierType.Carga:
                            ignoreDefense = true;
                            break;
                        case DamageModifierType.GolpeDePoder:
                            flatBonus += targetStats.defensivas.resistencia;
                            break;
                        case DamageModifierType.Inundacion:
                            float hpPct = ctx.Caster.Model.MaxHP > 0f
                                ? ctx.Caster.Model.CurrentHP / ctx.Caster.Model.MaxHP : 0f;
                            damageMult += hpPct * 0.8f;
                            break;
                        case DamageModifierType.Amplificar:
                            damageMult += ctx.Caster.Effects.AdvantageCount * mod.value;
                            break;
                        case DamageModifierType.Ruina:
                            damageMult += ctx.Target.Effects.DisadvantageCount * mod.value;
                            break;
                        case DamageModifierType.Ruptura:
                            if (ctx.Target.Effects.AdvantageCount > 0) damageMult *= 2f;
                            break;
                        case DamageModifierType.PuntoDebil:
                            if (ctx.Target.Effects.DisadvantageCount > 0) damageMult *= 2f;
                            break;
                    }
                }
            }

            // Override ataque in a cloned stat block so the formula uses the scaled value
            var modifiedAttacker          = castStats.Clone();
            float baseStat = statSource switch
            {
                StatSource.Defense => castStats.defensivas.defensa,
                StatSource.HP      => castStats.vitales.ps,
                _                  => castStats.ofensivas.ataque
            };
            modifiedAttacker.ofensivas.ataque = baseStat * multiplierByRank[idx];

            var dr     = CombatFormulas.CalculateDamage(
                modifiedAttacker, targetStats,
                ctx.Caster.Element, ctx.Target.Element,
                ignoreDefense, critDamageMult, critChanceMult);

            float damage = (dr.damage * damageMult) + flatBonus;
            damage      *= ctx.Target.Effects.DamageReceivedMultiplier();
            damage       = Mathf.Max(0f, damage);

            ctx.Target.Model.TakeDamage(damage);
            ctx.TotalDamageDealt += damage;
            ctx.IsCrit            = dr.isCrit;
        }
    }
}
