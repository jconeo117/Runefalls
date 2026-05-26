using UnityEngine;
using Runefall.Characters;
using Runefall.Combat;

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
        public PassiveDefinition passive;

        [Header("Combat Animations")]
        [Tooltip("Base animator controller applied to this character's pawn at spawn. Required for non-T-pose.")]
        public RuntimeAnimatorController animatorController;
        public AnimationClip animApproach;
        public AnimationClip animGetHit;
        public AnimationClip animDeath;

        [Header("Gacha")]
        [Range(0f, 1f)] public float baseDropRate;
    }
}
