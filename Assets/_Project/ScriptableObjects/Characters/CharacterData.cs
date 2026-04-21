using UnityEngine;
using Runefall.Characters;

namespace Runefall.Data
{
    [CreateAssetMenu(menuName = "Runefall/Character")]
    public class CharacterData : ScriptableObject
    {
        [Header("Identity")]
        public string characterName;
        public Sprite portrait;
        public GameObject prefab;
        public RarityType rarity;
        public ElementType element;

        [Header("Stats")]
        public CharacterStats baseStats;

        [Header("Skills")]
        public SkillData skill1;
        public SkillData skill2;
        public UltimateData ultimate;
        public PassiveData passive;

        [Header("Gacha")]
        [Range(0f, 1f)] public float baseDropRate;
    }
}
