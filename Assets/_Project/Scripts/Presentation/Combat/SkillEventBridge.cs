using Runefall.Combat;
using Runefall.Data;
using UnityEngine;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Forwards TurnManager.OnActionResolved (C# event) → SkillUsedEvent SO (GameEvent<T>).
    /// Assign SkillUsedEvent SO in Inspector. Call Init(tm) from CombatBootstrapper after
    /// TurnManager is created. Subscribers (CombatCameraDirector, VFX, etc.) wire to the SO.
    /// </summary>
    public class SkillEventBridge : MonoBehaviour
    {
        [SerializeField] private SkillUsedEvent skillUsedEvent;

        private TurnManager _tm;

        public void Init(TurnManager tm)
        {
            _tm = tm;
            _tm.OnActionResolved += Forward;
        }

        private void OnDestroy()
        {
            if (_tm != null)
                _tm.OnActionResolved -= Forward;
        }

        private void Forward(CombatActionResult result)
        {
            skillUsedEvent?.Raise(SkillUsedPayload.FromResult(result));
        }
    }
}
