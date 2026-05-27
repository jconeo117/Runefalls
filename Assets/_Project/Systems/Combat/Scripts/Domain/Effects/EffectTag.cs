using System;

namespace Runefall.Combat
{
    public enum EffectTag    { Neutral, Advantage, Disadvantage, Stance }
    public enum EffectTarget { Target, Caster }
    public enum StatSource   { Attack, Defense, HP }

    /// <summary>
    /// Mechanical overrides declared on EffectDefinition.
    /// Each flag activates a specific game-engine behaviour — independent of how many effects exist.
    /// New passive effects reuse these flags; new flags are added only when a NEW mechanic is needed.
    /// </summary>
    [Flags]
    public enum EffectBehavior
    {
        None         = 0,
        SkipTurn     = 1 << 0,   // actor does not act on its turn (frozen, stunned, etc.)
        BlockHeal    = 1 << 1,   // actor cannot receive healing
        ForceTarget  = 1 << 2,   // taunt — forces enemies to target this actor
        Untargetable = 1 << 3,   // cannot be selected as a target
        Invulnerable = 1 << 4,   // takes no damage
        Silenced     = 1 << 5,   // cannot use skills
    }

    public enum StatId
    {
        Ataque, Defensa, Perforacion, Resistencia,
        CritChance, CritDano, ResistenciaCrit, DefensaCrit,
        RoboDeVida, TasaRegen, TasaRecuperacion
    }

    public enum DamageModifierType
    {
        Puncion,       // x2 crit damage
        Destello,      // x3 crit chance
        Ruptura,       // x2 if target has advantages
        PuntoDebil,    // x2 if target has disadvantages
        Carga,         // ignore defense
        GolpeDePoder,  // +flat = target.resistencia
        Inundacion,    // +0.8% damage per % caster HP remaining
        Amplificar,    // +value% per advantage on self
        Ruina          // +value% per disadvantage on target
    }

    public enum HealSource { MaxHP, CurrentHP, CasterAttack, CasterMaxHP }
    public enum DoTMode    { PercentDamageDealt, PercentMaxHP, PercentCurrentHP }
}
