using System.Collections.Generic;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Core
{
    // Registrado en ServiceLocator antes de la transición de escena exploración→combate
    public class EncounterState
    {
        public EncounterData Encounter       { get; set; }
        public CharacterData SelectedCharacter { get; set; }
        public WeaponData    EquippedWeapon  { get; set; }
        public List<RuneData> EquippedRunes  { get; set; } = new List<RuneData>();
    }
}
