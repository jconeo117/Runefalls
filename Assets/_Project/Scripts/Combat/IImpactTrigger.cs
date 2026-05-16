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
        void Arm(Action onImpact);
        bool HasFired { get; }
    }
}
