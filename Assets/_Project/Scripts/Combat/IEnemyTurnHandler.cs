namespace Runefall.Combat
{
    /// <summary>
    /// Implemented by EnemyAgent. TurnManager casts enemies to this interface
    /// during enemy phase — keeps TurnManager decoupled from EnemyAgent class.
    /// </summary>
    public interface IEnemyTurnHandler
    {
        CombatActionResult TakeTurn(CombatContext context, ICombatActor target);
    }
}
