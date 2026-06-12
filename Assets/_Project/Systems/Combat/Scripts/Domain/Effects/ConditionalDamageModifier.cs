using System;
using UnityEngine;

namespace Runefall.Combat
{
    /// <summary>What a conditional modifier does to a hit's damage math.</summary>
    public enum DamageModKind
    {
        MultiplyDamage,       // damageMult     *= value
        AddFlatDamage,        // flatBonus      += value
        IgnoreDefense,        // ignore the target's defense
        MultiplyCritDamage,   // critDamageMult *= value
        MultiplyCritChance    // critChanceMult *= value
    }

    /// <summary>
    /// The WHEN of a conditional damage modifier. Swap concrete subclasses to change the trigger
    /// (target frozen, target has effect X, caster has buff Y…). Pure domain, data-driven.
    /// </summary>
    public abstract class DamageCondition : ScriptableObject
    {
        public abstract bool Matches(ICombatActor caster, ICombatActor target);
    }

    /// <summary>
    /// One reusable rule: when <see cref="condition"/> holds (null = always), apply <see cref="kind"/>
    /// with <see cref="value"/> to the outgoing hit. This is the shared CONTAINER — build new behaviors
    /// ("x2 vs frozen", "x3 crit vs marked", "ignore def vs stunned") by swapping condition + kind + value,
    /// no new code. Evaluated deterministically inside the damage pipeline (DamageEffectDef).
    /// </summary>
    [Serializable]
    public class ConditionalDamageModifier
    {
        [Tooltip("When this modifier applies. Null = always.")]
        public DamageCondition condition;
        public DamageModKind   kind  = DamageModKind.MultiplyDamage;
        public float           value = 2f;
    }
}
