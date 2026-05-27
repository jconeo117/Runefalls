using System;

namespace Runefall.Combat
{
    /// <summary>
    /// Controls the precise moment damage resolves during a combat animation.
    /// Arm() starts trigger logic. HasFired becomes true exactly once.
    /// CombatAnimationDriver awaits both animation completion and HasFired.
    ///
    /// Implementations: AnimEventTrigger (melee), ProjectileTrigger (ranged), TimerTrigger (VFX/flexible).
    /// </summary>
    public interface IImpactTrigger
    {
        /// <summary>
        /// Arms the trigger. Callback fires once per hit: (hitIndex, totalHits, specificTarget).
        /// Single-hit: (0, 1, null). Multi-hit: (0..N-1, N, null).
        /// AoE projectile: (waveIdx, totalWaves, specificEnemy) — one call per projectile arrival.
        /// HasFired becomes true after the final hit.
        /// </summary>
        void Arm(Action<int, int, ICombatActor> onImpact);
        bool HasFired { get; }
    }
}
