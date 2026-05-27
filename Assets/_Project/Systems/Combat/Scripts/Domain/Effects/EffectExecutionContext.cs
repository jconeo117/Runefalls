namespace Runefall.Combat
{
    /// <summary>
    /// Mutable context passed through the EffectDefinition pipeline for one skill use.
    /// Earlier effects set modifier flags; later effects read them.
    /// </summary>
    public class EffectExecutionContext
    {
        public ICombatActor Caster;
        public ICombatActor Target;
        public int          Rank;

        // Multi-hit fraction: 1/N for N-hit skills. Applied by DamageEffectDef and HealEffectDef.
        public float HitFraction = 1f;

        // Damage modifiers — set by modifier effects earlier in the array
        public bool  IgnoreDefense;
        public float CritDamageMultiplier = 1f;
        public float CritChanceMultiplier = 1f;
        public float DamageMultiplier     = 1f;
        public float FlatBonusDamage;

        // Accumulated output — read by reactive effects (LifestealEffectDef, DoTEffectDef)
        public float TotalDamageDealt;
        public float TotalHealApplied;
        public bool  IsCrit;
    }
}
