using UnityEngine;
using Runefall.Characters;

namespace Runefall.Data
{
    public enum WeaponType { Sword, Staff, Dagger, Hammer }

    [System.Serializable]
    public class WeaponStats
    {
        public float attack;
        public float defense;
        public float speed;
        [Range(0f, 1f)] public float critChance;
        public float critMultiplier;
    }

    [System.Serializable]
    public class ConditionalBonus
    {
        [TextArea] public string conditionDescription;
        public StatModifier modifier;
    }

    [CreateAssetMenu(menuName = "Runefall/Weapon")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        public string weaponName;
        public Sprite weaponArt;
        public GameObject weaponPrefab;
        public WeaponType weaponType;
        public ElementType element;
        public RarityType rarity;

        [Header("Stats")]
        public WeaponStats stats;

        [Header("Conditional Bonus")]
        public ConditionalBonus conditionalBonus;

        [Header("Gacha")]
        [Range(0f, 1f)] public float baseDropRate;
    }
}
