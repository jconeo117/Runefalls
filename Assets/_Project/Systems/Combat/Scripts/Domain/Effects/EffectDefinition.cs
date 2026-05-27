using UnityEngine;

namespace Runefall.Combat
{
    /// <summary>
    /// Base for all skill and passive effects. Subclass with [CreateAssetMenu] for each mechanic type.
    ///
    /// Two roles:
    ///   1. Mechanic — the C# subclass defines HOW the effect works (DamageEffectDef, StatModEffectDef…)
    ///   2. Named effect — the .asset instance IS the named effect (Hipotermia.asset, ArmorVolcanica.asset…)
    ///      Set effectName/description/icon on the asset for effects that appear in the player UI.
    ///      Leave effectName empty for internal/unnamed mechanics (Llamarada stacks, DoT ticks…).
    ///
    /// Stacking on the same actor:
    ///   stackBySource=false (default) → each Apply() call adds a new independent entry.
    ///   stackBySource=true            → reapplying the same SO matches the existing entry.
    ///     stackable=true  → increments Stacks (Llamarada: each application adds a stack)
    ///     stackable=false → refreshes TurnsRemaining only, Stacks stays at 1 (Hipotermia: just renew)
    /// </summary>
    public abstract class EffectDefinition : ScriptableObject
    {
        [Header("Display")]
        [Tooltip("Player-visible name. Empty = internal effect, hidden from UI.")]
        public string effectName;
        [TextArea]
        public string description;
        public Sprite icon;

        [Header("Classification")]
        [Tooltip("Advantage = buff. Disadvantage = debuff. Used by ruptura, punto debil, limpieza, etc.")]
        public EffectTag tag = EffectTag.Neutral;

        [Header("Mechanical Behaviours")]
        [Tooltip("Engine-level overrides this effect activates while present on an actor.")]
        public EffectBehavior behavior = EffectBehavior.None;

        [Header("Stacking")]
        [Tooltip("True = reapplying this SO matches the existing entry (stack or refresh). False = always new entry.")]
        public bool stackBySource = false;
        [Tooltip("Only used when stackBySource=true. True = increment Stacks. False = refresh duration only.")]
        public bool stackable = true;

        public abstract void Execute(EffectExecutionContext ctx);
    }
}
