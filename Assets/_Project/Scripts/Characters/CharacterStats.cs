using UnityEngine;

namespace Runefall.Characters
{
    [System.Serializable]
    public class CharacterStats
    {
        public float maxHP;
        public float attack;
        public float defense;
        public float speed;
        [Range(0f, 1f)] public float critChance;
        public float critMultiplier;

        public CharacterStats Clone() => (CharacterStats)MemberwiseClone();

        public static CharacterStats operator +(CharacterStats a, StatModifier m)
        {
            var result = a.Clone();
            result.attack    *= (1f + m.attackBonus);
            result.defense   *= (1f + m.defenseBonus);
            result.critChance = Mathf.Clamp01(result.critChance + m.critBonus);
            return result;
        }
    }

    [System.Serializable]
    public class StatModifier
    {
        public float attackBonus;
        public float defenseBonus;
        public float critBonus;

        public static readonly StatModifier Zero = new StatModifier();
    }
}
