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

        public CharacterData[] Party => _party;
    }
}
