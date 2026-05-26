namespace Runefall.Combat
{
    /// <summary>
    /// Implemented by EnemyAgent. TurnManager casts enemies to this interface
    /// during enemy phase — keeps TurnManager decoupled from EnemyAgent class.
    /// Returns a PendingAction; actual damage is applied by TurnManager.ResolveAction() at impact frame.
    /// </summary>
    public interface IEnemyTurnHandler
    {
        PendingAction TakeTurn(CombatContext context, ICombatActor target);
    }
}
