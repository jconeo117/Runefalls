using UnityEngine;
using Runefall.Characters;

namespace Runefall.Data
{
    public enum BehaviorTreeType { GoblinScout, OrcGuardian, ShadowMage, BossElite }

    [System.Serializable]
    public class EnemyStats
    {
        public float maxHP;
        public float attack;
        public float defense;
        public float speed;
    }

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
        public EnemyStats stats;
        public int approximatePower;

        [Header("Exploration")]
        public float detectionRange = 8f;
        public float patrolRadius = 4f;
        public bool respawnsOnRoomExit = true;

        [Header("Combat")]
        public BehaviorTreeType behaviorTree;
        public EnemyReward dropReward;
    }
}
