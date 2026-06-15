using System.Collections.Generic;
using UnityEngine;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// The enemy counterpart of <see cref="CombatActorStatsProvider"/>: adapts any enemy
    /// <see cref="ICombatActor"/> (live stats / HP / effects) + its <see cref="EnemyData"/>
    /// (skills / identity) + its pawn <see cref="Transform"/> into
    /// <see cref="ICharacterStatsProvider"/>. The overlay renders it through the exact same
    /// path as the player — EnemyData carries no passive, so <see cref="Passive"/> is null and
    /// the overlay's passive panel simply omits itself.
    /// </summary>
    public sealed class EnemyStatsProvider : ICharacterStatsProvider
    {
        private readonly ICombatActor _actor;
        private readonly EnemyData    _data;
        private readonly Transform    _focus;

        public EnemyStatsProvider(ICombatActor actor, EnemyData data, Transform focus)
        {
            _actor = actor;
            _data  = data;
            _focus = focus;
        }

        public string         DisplayName    => _data != null ? _data.enemyName : _actor?.Name;
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
        public PassiveDefinition Passive  => null;   // EnemyData carries no passive
    }
}
