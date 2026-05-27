using System;
using System.Collections.Generic;
using Runefall.Data;

namespace Runefall.Combat
{
    /// <summary>
    /// Infinite skill pool sourced from field characters.
    /// Each draw is random with replacement — pool never depletes.
    /// </summary>
    public class CardPool
    {
        private readonly List<SkillData> _skills;
        private readonly Random          _rng;

        public int SkillCount => _skills.Count;

        public CardPool(IReadOnlyList<CharacterData> fieldChars, Random rng = null)
        {
            if (fieldChars == null || fieldChars.Count == 0)
                throw new ArgumentException("At least 1 field character required.", nameof(fieldChars));

            _rng    = rng ?? new Random();
            _skills = new List<SkillData>(fieldChars.Count * 2);

            foreach (var c in fieldChars)
                AddCharacter(c);

            if (_skills.Count == 0)
                throw new InvalidOperationException("No skills found in field characters.");
        }

        /// <summary>Returns random skill. Never removes it from pool.</summary>
        public SkillData Draw() => _skills[_rng.Next(_skills.Count)];

        public void AddCharacter(CharacterData character)
        {
            if (character == null) return;
            if (character.skill1 != null) _skills.Add(character.skill1);
            if (character.skill2 != null) _skills.Add(character.skill2);
        }

        /// <summary>Removes first occurrence of each skill. Call when character leaves field.</summary>
        public void RemoveCharacter(CharacterData character)
        {
            if (character == null) return;
            if (character.skill1 != null) _skills.Remove(character.skill1);
            if (character.skill2 != null) _skills.Remove(character.skill2);
        }
    }
}
