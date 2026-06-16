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

        [Tooltip("Actions the boss takes per enemy phase. 3 = the boss attacks three times (passive: +2 acciones). " +
                 "TurnManager repeats the boss in the enemy turn order this many times.")]
        public int actionsPerTurn = 3;

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

        [Header("Transition VFX (spawned as a CHILD of the boss)")]
        [Tooltip("Effect prefab (e.g. magic circle) instanced as a child of the boss when the transition " +
                 "timeline fires a 'Boss_VFX' signal. Child = always centered on the boss + scalable here.")]
        public GameObject transitionVfxPrefab;
        [Tooltip("Uniform scale of the transition VFX. Make it big/imposing.")]
        public float transitionVfxScale = 1f;
        [Tooltip("Local offset from the boss pivot (e.g. raise Y a touch off the ground).")]
        public Vector3 transitionVfxLocalOffset;
        [Tooltip("Seconds before the spawned VFX auto-destroys. 0 = lives until the pawn is replaced.")]
        public float transitionVfxAutoDestroy = 4f;
    }
}
