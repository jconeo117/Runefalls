using UnityEngine;
using Runefall.Combat;

namespace Runefall.Presentation.Combat
{
    public abstract class CombatPresenterBase : MonoBehaviour, ICombatPresenter
    {
        public abstract void Initialize(TurnManager tm, CombatContext ctx);
        public abstract void OnPlayerTurnStarted(int round);
        public abstract void OnActionResolved(CombatActionResult result);
        public abstract void OnCombatEnded(bool playerWon);

        public virtual void RegisterEnemyMarkers(EnemyTargetMarker[] markers) { }
        public virtual void SelectEnemy(int index) { }
        public virtual void OnCardMerged(string skillName, int newRank) { }

        /// <summary>Show or hide action slots. Called by bootstrapper around lunge animations.</summary>
        public virtual void SetActionSlotsActive(bool active) { }

        /// <summary>
        /// Called after each player action animation completes.
        /// Presenter starts its own coroutine — no yield required from caller.
        /// </summary>
        public virtual void NotifyActionAnimationComplete(int actionIndex) { }

        /// <summary>Called when a player's ultimate gauge changes. orbs: 0–7.</summary>
        public virtual void OnGaugeChanged(ICombatActor actor, int orbs) { }
    }
}
