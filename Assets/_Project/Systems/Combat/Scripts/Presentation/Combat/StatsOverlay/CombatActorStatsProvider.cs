using System.Collections.Generic;
using UnityEngine;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// The agnostic provider used in combat: adapts any <see cref="ICombatActor"/> (live
    /// stats / HP / effects) + its <see cref="CharacterData"/> (skills / passive / identity)
    /// + its pawn <see cref="Transform"/> into <see cref="ICharacterStatsProvider"/>.
    /// Player is the first consumer; an enemy can build the same provider from its own
    /// actor + a CharacterData-shaped source without the overlay changing.
    /// </summary>
    public sealed class CombatActorStatsProvider : ICharacterStatsProvider
    {
        private readonly ICombatActor  _actor;
        private readonly CharacterData _data;
        private readonly Transform     _focus;

        public CombatActorStatsProvider(ICombatActor actor, CharacterData data, Transform focus)
        {
            _actor = actor;
            _data  = data;
            _focus = focus;
        }

        public string         DisplayName    => _data != null ? _data.characterName : _actor?.Name;
        public Transform      FocusTarget     => _focus;
        public Sprite         Portrait        => _data != null ? _data.portrait : null;
        public ElementType    Element         => _data != null ? _data.element : (_actor?.Element ?? ElementType.Neutral);

        public CharacterStats BaseStats       => _actor?.Model?.Stats;
        public CharacterStats EffectiveStats  => _actor?.Model?.EffectiveStats;
        public float          CurrentHP       => _actor?.Model?.CurrentHP ?? 0f;
        public float          MaxHP           => _actor?.Model?.MaxHP ?? 0f;

        public IReadOnlyList<ActiveEffect> Effects => _actor?.Effects?.ActiveEffects;

        public SkillData         Skill1   => _data != null ? _data.skill1   : null;
        public SkillData         Skill2   => _data != null ? _data.skill2   : null;
        public UltimateData      Ultimate => _data != null ? _data.ultimate : null;
        public PassiveDefinition Passive  => _data != null ? _data.passive  : null;
    }
}
