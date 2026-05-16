using Runefall.Combat;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Contract for any MonoBehaviour that presents combat state to the player.
    /// CombatBootstrapper depends on this interface so concrete presenter classes
    /// (blockout, production) can be swapped without touching the Composition Root.
    /// Serialized Inspector fields still use CombatPresenterBase (Unity cannot serialize interfaces);
    /// code always references ICombatPresenter.
    /// </summary>
    public interface ICombatPresenter
    {
        void Initialize(TurnManager tm, CombatContext ctx);
        void OnPlayerTurnStarted(int round);
        void OnActionResolved(CombatActionResult result);
        void OnCombatEnded(bool playerWon);
        void RegisterEnemyMarkers(EnemyTargetMarker[] markers);
        void SelectEnemy(int index);
        void OnCardMerged(string skillName, int newRank);
        void SetActionSlotsActive(bool active);
        void NotifyActionAnimationComplete(int actionIndex);
        void OnGaugeChanged(ICombatActor actor, int orbs);
    }
}
