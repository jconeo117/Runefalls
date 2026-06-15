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

        [Header("Audio")]
        [Tooltip("Banco de voces (ataque/golpe/muerte) reproducido en combate por CombatAudioPlayer.")]
        public VoiceSetData voiceSet;

        [Header("Combat Scale")]
        [Tooltip("Multiplicador extra sobre la escala normalizada del pawn en combate (1 = altura objetivo estándar; <1 = más pequeño). Afecta también su HP bar (hija del pawn).")]
        public float combatScaleMultiplier = 1f;

        [Header("HP Bar")]
        [Tooltip("Frame sprite drawn over the HP bar (the character's themed border). Null = plain bar.")]
        public Sprite hpBarFrame;

        [Header("Gacha")]
        [Range(0f, 1f)] public float baseDropRate;
    }
}
