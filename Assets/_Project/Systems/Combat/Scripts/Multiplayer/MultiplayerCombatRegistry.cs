using System.Collections.Generic;
using UnityEngine;
using Runefall.Data;

namespace Runefall.Multiplayer
{
    [CreateAssetMenu(menuName = "Runefall/Multiplayer/Registry")]
    public class MultiplayerCombatRegistry : ScriptableObject
    {
        [Header("Characters")]
        public List<CharacterData> characters = new();

        [Header("Enemies")]
        public List<EnemyData> enemies = new();

        [Header("Skills")]
        public List<SkillData> skills = new();

        [Header("Ultimates")]
        public List<UltimateData> ultimates = new();

        private Dictionary<string, CharacterData> _charByName;
        private Dictionary<string, EnemyData> _enemyByName;
        private Dictionary<string, SkillData> _skillByName;
        private Dictionary<string, UltimateData> _ultimateByName;

        public void Initialize()
        {
            _charByName = new Dictionary<string, CharacterData>();
            foreach (var c in characters)
            {
                if (c != null && !string.IsNullOrEmpty(c.characterName))
                    _charByName[c.characterName] = c;
            }

            _enemyByName = new Dictionary<string, EnemyData>();
            foreach (var e in enemies)
            {
                if (e != null && !string.IsNullOrEmpty(e.enemyName))
                    _enemyByName[e.enemyName] = e;
            }

            _skillByName    = new Dictionary<string, SkillData>();
            _ultimateByName = new Dictionary<string, UltimateData>();

            // Explicit lists first.
            foreach (var s in skills)
                if (s != null && !string.IsNullOrEmpty(s.skillName))
                    _skillByName[s.skillName] = s;
            foreach (var u in ultimates)
                if (u != null && !string.IsNullOrEmpty(u.ultimateName))
                    _ultimateByName[u.ultimateName] = u;

            // Auto-index skills/ultimates carried by each character and enemy, so remote
            // clients can resolve any card's SkillData (for art/name) even when the explicit
            // skills/ultimates lists are left empty. Characters/enemies are the source of truth.
            foreach (var c in characters)
            {
                if (c == null) continue;
                IndexSkill(c.skill1);
                IndexSkill(c.skill2);
                IndexUltimate(c.ultimate);
            }
            foreach (var e in enemies)
            {
                if (e == null) continue;
                IndexSkill(e.skill1);
                IndexSkill(e.skill2);
                IndexUltimate(e.ultimate);
            }
        }

        private void IndexSkill(SkillData s)
        {
            if (s != null && !string.IsNullOrEmpty(s.skillName) && !_skillByName.ContainsKey(s.skillName))
                _skillByName[s.skillName] = s;
        }

        private void IndexUltimate(UltimateData u)
        {
            if (u != null && !string.IsNullOrEmpty(u.ultimateName) && !_ultimateByName.ContainsKey(u.ultimateName))
                _ultimateByName[u.ultimateName] = u;
        }

        public CharacterData GetCharacter(string name)
        {
            if (_charByName == null) Initialize();
            return _charByName.TryGetValue(name, out var val) ? val : null;
        }

        public EnemyData GetEnemy(string name)
        {
            if (_enemyByName == null) Initialize();
            return _enemyByName.TryGetValue(name, out var val) ? val : null;
        }

        public SkillData GetSkill(string name)
        {
            if (_skillByName == null) Initialize();
            return _skillByName.TryGetValue(name, out var val) ? val : null;
        }

        public UltimateData GetUltimate(string name)
        {
            if (_ultimateByName == null) Initialize();
            return _ultimateByName.TryGetValue(name, out var val) ? val : null;
        }
    }
}
