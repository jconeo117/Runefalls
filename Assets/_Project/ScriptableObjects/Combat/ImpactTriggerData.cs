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
        public abstract IImpactTrigger Create(Transform caster, Transform target, MonoBehaviour host, AnimationClip[] allClips = null);
    }
}
