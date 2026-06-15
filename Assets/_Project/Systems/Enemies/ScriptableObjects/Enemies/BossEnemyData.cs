using UnityEngine;
using UnityEngine.Playables;
using Runefall.Combat;

namespace Runefall.Data
{
    [CreateAssetMenu(menuName = "Runefall/Boss Enemy Data")]
    public class BossEnemyData : EnemyData
    {
        [Header("Boss Passive")]
        [Tooltip("Phase-driven passive (e.g. BossPhasesPassive). Activated for the boss agent at combat " +
                 "start. EnemyData has no passive field — only bosses carry one.")]
        public PassiveDefinition phasesPassive;

        [Header("Boss Multi-Phase Setup")]
        [Tooltip("Data for Phase 1. If null, falls back to this BossEnemyData asset's stats/skills.")]
        public EnemyData phase1Data;
        [Tooltip("Data for Phase 2. Must be assigned.")]
        public EnemyData phase2Data;
        [Tooltip("Data for Phase 3. Must be assigned.")]
        public EnemyData phase3Data;

        [Header("Phase Transition Timelines")]
        [Tooltip("Timeline to play when transitioning from Phase 1 to Phase 2.")]
        public PlayableAsset phase1To2Timeline;
        [Tooltip("Timeline to play when transitioning from Phase 2 to Phase 3.")]
        public PlayableAsset phase2To3Timeline;
    }
}
