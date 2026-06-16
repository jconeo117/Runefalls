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

        // How many actions this actor takes in one enemy phase. 1 for players/regular enemies;
        // bosses can act multiple times (TurnManager repeats the actor in the turn order).
        int ActionsPerTurn { get; }
    }
}
