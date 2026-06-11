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
    public interface IHudVisibilityPresenter
    {
        /// <summary>Instantly hides ALL combat UI (cards, slots, HUD). Called by FinisherManager.</summary>
        void HideAllUI();
        /// <summary>Restores ALL combat UI visibility after finisher / on turn start.</summary>
        void ShowAllUI();
    }

    public interface ICombatAnimationNotifier
    {
        /// <summary>
        /// Called after each player action animation completes.
        /// Presenter starts its own coroutine — no yield required from caller.
        /// </summary>
        void NotifyActionAnimationComplete(int actionIndex);
    }

    public interface ICardPresenter
    {
        void OnCardMerged(string skillName, int newRank);
        void SetActionSlotsActive(bool active);
    }

    public interface IGaugePresenter
    {
        void OnGaugeChanged(ICombatActor actor, int orbs);
    }

    public interface IEnemySelectionPresenter
    {
        void RegisterEnemyMarkers(EnemyTargetMarker[] markers);
        void SelectEnemy(int index);
    }

    /// <summary>
    /// Contract for any MonoBehaviour that presents combat state to the player.
    /// CombatBootstrapper depends on this interface so concrete presenter classes
    /// (blockout, production) can be swapped without touching the Composition Root.
    /// Serialized Inspector fields still use CombatPresenterBase (Unity cannot serialize interfaces);
    /// code always references ICombatPresenter.
    /// </summary>
    public interface ICombatPresenter : 
        IHudVisibilityPresenter, 
        ICombatAnimationNotifier, 
        ICardPresenter, 
        IGaugePresenter, 
        IEnemySelectionPresenter
    {
        void Initialize(TurnManager tm, CombatContext ctx);
        void OnPlayerTurnStarted(int round);
        void OnActionResolved(CombatActionResult result);
        void OnCombatEnded(bool playerWon);
    }
}
