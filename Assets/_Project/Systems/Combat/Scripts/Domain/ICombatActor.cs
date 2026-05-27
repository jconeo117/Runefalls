using Runefall.Characters;

namespace Runefall.Combat
{
    public interface ICombatActor
    {
        string        Name        { get; }
        bool          IsAlive     { get; }
        CharacterModel Model      { get; }
        ElementType   Element     { get; }
        ActorEffects  Effects     { get; }

        // Players: CharacterStats.ClaseDeCombate. Enemies: EnemyData.combatClass (manual).
        float CombatClass { get; }
    }
}
