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

        [Header("Exploration")]
        public float detectionRange  = 8f;
        public float patrolRadius    = 4f;
        public bool  respawnsOnRoomExit = true;

        [Header("Skills")]
        [Tooltip("Definidas para uso futuro del BehaviorTree. El blockout usa BasicAttack con stats.")]
        public SkillData    skill1;
        public SkillData    skill2;
        public UltimateData ultimate;

        [Header("Combat Animations")]
        public AnimationClip animApproach;
        public AnimationClip animGetHit;
        public AnimationClip animDeath;


        [Header("Combat")]
        public BehaviorTreeType behaviorTree;
        public EnemyReward dropReward;
    }
}
