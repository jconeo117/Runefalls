using System;
using System.Collections.Generic;

namespace Runefall.Combat
{
    /// <summary>
    /// Presentation-side animator for the enemy phase.
    /// Pass to TurnManager constructor; if null, enemy turns resolve synchronously.
    /// TurnManager calls RunEnemyPhase and passes its own callbacks so the animator
    /// never imports TurnManager — the dependency arrow stays Presentation → Domain.
    /// </summary>
    public interface IEnemyPhaseAnimator
    {
        /// <summary>
        /// Animate each enemy's turn in sequence.
        /// Call executeTurn(enemy) to resolve each enemy's action — this fires
        /// TurnManager.OnActionResolved. Call onComplete when all animations finish;
        /// that triggers EndOfRound inside TurnManager.
        /// </summary>
        void RunEnemyPhase(
            IReadOnlyList<ICombatActor> enemies,
            Action<ICombatActor>        executeTurn,
            Action                      onComplete);
    }
}
