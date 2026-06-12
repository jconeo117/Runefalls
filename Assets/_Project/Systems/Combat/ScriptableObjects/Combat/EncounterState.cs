using System.Collections.Generic;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Core
{
    // Registrado en ServiceLocator antes de la transición de escena exploración→combate
    public class EncounterState
    {
        public EncounterData   Encounter          { get; set; }
        public CharacterData   SelectedCharacter  { get; set; }
        public CharacterData[] PlayerParty        { get; set; }
        public UnityEngine.Vector3 ArenaCenter    { get; set; }
        public WeaponData      EquippedWeapon     { get; set; }
        public List<RuneData>  EquippedRunes      { get; set; } = new List<RuneData>();

        /// <summary>
        /// Enemy count rolled on the FIRST arena assembly of this encounter, kept so Retry rebuilds the
        /// exact same arena (same number of enemies) instead of re-rolling the difficulty. -1 = not rolled
        /// yet. A brand-new encounter gets a fresh EncounterState (default -1) → fresh roll; Retry reuses
        /// the same EncounterState instance → same count.
        /// </summary>
        public int LockedEnemyCount { get; set; } = -1;

        // Resolves the party array: prefers PlayerParty, falls back to SelectedCharacter.
        public CharacterData[] ResolvedParty =>
            PlayerParty is { Length: > 0 }
                ? PlayerParty
                : SelectedCharacter != null
                    ? new[] { SelectedCharacter }
                    : System.Array.Empty<CharacterData>();
    }
}
