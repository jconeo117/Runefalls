using System.Collections.Generic;
using UnityEngine;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Agnostic contract the character-stats overlay reads from. ANY subject that can
    /// describe itself — player, enemy, summon, dummy — provides one of these and the
    /// overlay renders it without knowing the concrete type. The live 3D pawn to frame
    /// is exposed via <see cref="FocusTarget"/>.
    /// </summary>
    public interface ICharacterStatsProvider
    {
        string         DisplayName    { get; }
        Transform      FocusTarget    { get; }   // pawn the camera frames from the front
        Sprite         Portrait       { get; }
        ElementType    Element        { get; }

        CharacterStats BaseStats      { get; }   // authored stats
        CharacterStats EffectiveStats { get; }   // base + active modifiers (for the green delta)
        float          CurrentHP      { get; }
        float          MaxHP          { get; }

        IReadOnlyList<ActiveEffect> Effects { get; }

        SkillData         Skill1   { get; }       // each renders as cards at ranks 1-3
        SkillData         Skill2   { get; }
        UltimateData      Ultimate { get; }
        PassiveDefinition Passive  { get; }
    }
}
