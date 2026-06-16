using UnityEngine;
using Runefall.Characters;

namespace Runefall.Data
{
    public enum BehaviorTreeType { GoblinScout, OrcGuardian, ShadowMage, BossElite }

    [System.Serializable]
    public class EnemyReward
    {
        public int goldMin;
        public int goldMax;
        public RuneData[] possibleRunes;
        [Range(0f, 1f)] public float runeDropChance;
    }

    [CreateAssetMenu(menuName = "Runefall/Enemy")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string enemyName;
        public GameObject prefab;
        public Sprite portrait;
        public ElementType element;

        [Header("Stats")]
        public CharacterStats stats;
        public float maxMP;

        [Header("Combat Class")]
        [Tooltip("Valor asignado manualmente. Determina orden de turno.")]
        public float combatClass;

        [Tooltip("Multiplicador de escala del pawn en COMBATE, aplicado tras normalizar la altura. " +
                 "1 = sin cambio. >1 = más grande (p. ej. jefe ligeramente más imponente).")]
        public float combatScaleMultiplier = 1f;

        [Header("Exploration")]
        public float detectionRange  = 8f;
        [Tooltip("Radio del trigger que abre el panel de encuentro. Debe ser menor que detectionRange.")]
        public float encounterRange  = 1.8f;
        public float patrolRadius    = 4f;
        public bool  respawnsOnRoomExit = true;

        [Header("Skills")]
        [Tooltip("Definidas para uso futuro del BehaviorTree. El blockout usa BasicAttack con stats.")]
        public SkillData    skill1;
        public SkillData    skill2;
        public UltimateData ultimate;

        [Header("Combat Animations")]
        [Tooltip("Base animator controller applied to this enemy's pawn at spawn. Required for non-T-pose.")]
        public RuntimeAnimatorController animatorController;
        public AnimationClip animApproach;
        public AnimationClip animGetHit;
        public AnimationClip animDeath;


        [Header("Audio")]
        [Tooltip("Banco de voces (ataque/golpe/muerte) reproducido en combate por CombatAudioPlayer.")]
        public VoiceSetData voiceSet;

        [Header("HP Bar")]
        [Tooltip("Frame sprite drawn over the HP bar (difficulty-themed border). Null = plain bar.")]
        public Sprite hpBarFrame;

        [Header("Combat")]
        public BehaviorTreeType behaviorTree;
        public EnemyReward dropReward;
    }
}
