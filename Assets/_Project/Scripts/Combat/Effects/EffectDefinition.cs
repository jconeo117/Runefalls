using UnityEngine;

namespace Runefall.Combat
{
    /// <summary>
    /// Base for all skill effects. Subclass with [CreateAssetMenu] for each effect type.
    /// Effects in SkillEffect.effects[] execute in order — earlier effects mutate ctx
    /// so later effects (e.g. DamageEffectDef after a debuff) see the updated actor state.
    /// </summary>
    public abstract class EffectDefinition : ScriptableObject
    {
        [Header("Classification")]
        [Tooltip("Advantage = buff. Disadvantage = debuff. Used by ruptura, punto debil, etc.")]
        public EffectTag tag = EffectTag.Neutral;

        public abstract void Execute(EffectExecutionContext ctx);
    }
}
