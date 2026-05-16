namespace Runefall.Combat
{
    public enum EffectTag    { Neutral, Advantage, Disadvantage, Stance }
    public enum EffectFlag   { None, Infected, Llamarada, Taunted }
    public enum EffectTarget { Target, Caster }

    public enum StatSource { Attack, Defense, HP }

    public enum StatId
    {
        Ataque, Defensa, Perforacion, Resistencia,
        CritChance, CritDano, ResistenciaCrit, DefensaCrit,
        RoboDeVida, TasaRegen, TasaRecuperacion
    }

    public enum DamageModifierType
    {
        Puncion,      // x2 crit damage
        Destello,     // x3 crit chance
        Ruptura,      // x2 if target has advantages
        PuntoDebil,   // x2 if target has disadvantages
        Carga,        // ignore defense
        GolpeDePoder, // +flat = target.resistencia
        Inundacion,   // +0.8% damage per % caster HP remaining
        Amplificar,   // +value% per advantage on self
        Ruina         // +value% per disadvantage on target
    }

    public enum HealSource { MaxHP, CurrentHP, CasterAttack, CasterMaxHP }

    public enum DoTMode { PercentDamageDealt, PercentMaxHP, PercentCurrentHP }
}
