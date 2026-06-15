using UnityEngine;
using Runefall.Data;

namespace Runefall.Presentation.Player
{
    /// <summary>
    /// Carries the party CharacterData for the player GO in the exploration scene.
    /// EncounterPromptPresenter reads this instead of its own hardcoded _defaultParty array.
    /// </summary>
    public class ExplorationPlayer : MonoBehaviour
    {
        [Tooltip("Active characters in the party (1–3). Slot 0 = primary / leftmost card in combat.")]
        [SerializeField] private CharacterData[] _party;

        [Tooltip("Optional explicit model Animator. Empty = first Animator found in children.")]
        [SerializeField] private Animator _modelAnimator;

        public CharacterData[] Party => _party;
        public CharacterData Primary => (_party != null && _party.Length > 0) ? _party[0] : null;

        private void Start() => ApplyExplorationController();

        /// <summary>Sets the player model's Animator to the primary character's exploration controller.
        /// Call again after swapping the model at runtime (selection-driven loader).</summary>
        public void ApplyExplorationController()
        {
            var primary = Primary;
            if (primary == null || primary.explorationAnimatorController == null) return;

            var anim = _modelAnimator != null ? _modelAnimator : GetComponentInChildren<Animator>(true);
            if (anim != null)
                anim.runtimeAnimatorController = primary.explorationAnimatorController;
        }
    }
}
