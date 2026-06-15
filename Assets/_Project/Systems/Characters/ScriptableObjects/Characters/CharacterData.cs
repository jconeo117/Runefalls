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

        [Header("Exploration Animations")]
        [Tooltip("Animator controller for this character in the exploration scene (locomotion). Applied at runtime by ExplorationPlayer to the player model.")]
        public RuntimeAnimatorController explorationAnimatorController;

        [Header("Combat Animations")]
        [Tooltip("Base animator controller applied to this character's pawn at spawn. Required for non-T-pose.")]
        public RuntimeAnimatorController animatorController;
        public AnimationClip animApproach;
        public AnimationClip animGetHit;
        public AnimationClip animDeath;

        [Header("HP Bar")]
        [Tooltip("Frame sprite drawn over the HP bar (the character's themed border). Null = plain bar.")]
        public Sprite hpBarFrame;

        [Header("Gacha")]
        [Range(0f, 1f)] public float baseDropRate;
    }
}
