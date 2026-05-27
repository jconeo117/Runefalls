using System.Collections.Generic;
using UnityEngine;
using Runefall.Combat;

namespace Runefall.Data
{
    /// <summary>
    /// Strategy SO that creates the IImpactTrigger for a skill or enemy attack.
    /// Assign to SkillData.impactTrigger or EnemyData.attackTrigger in the Inspector.
    /// Concrete types: AnimEventTriggerData, ProjectileTriggerData, TimerTriggerData.
    /// </summary>
    public abstract class ImpactTriggerData : ScriptableObject
    {
        /// <param name="expectedHits">
        /// When > 0 overrides internal hit-count detection. Pass SkillData.hitCount.
        /// 0 = trigger auto-detects from clips/AEs.
        /// </param>
        /// <param name="aoeTargets">
        /// When non-null, ProjectileTrigger spawns one projectile per target per shoot AE.
        /// Null for single-target skills or AoE melee/timer triggers.
        /// </param>
        public abstract IImpactTrigger Create(
            Transform caster, Transform target, MonoBehaviour host,
            AnimationClip[] allClips = null, int expectedHits = 0,
            IReadOnlyList<(ICombatActor actor, Transform pawn)> aoeTargets = null);
    }
}
