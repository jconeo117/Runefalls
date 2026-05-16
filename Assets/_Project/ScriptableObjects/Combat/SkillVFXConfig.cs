using UnityEngine;

namespace Runefall.Data
{
    /// <summary>
    /// VFX prefabs for a skill or enemy basic attack.
    /// Assign to SkillData.vfxConfig or EnemyData.basicAttackVFXConfig.
    ///
    /// onStartVFX  — spawns at weapon bone when attack animation begins (slash arc, cast circle, charge).
    /// onImpactVFX — spawns at hit position when ImpactEvent fires (sparks, explosion, hit flash).
    /// </summary>
    [CreateAssetMenu(menuName = "Runefall/Combat/SkillVFXConfig")]
    public class SkillVFXConfig : ScriptableObject
    {
        [Header("Attack Start")]
        [Tooltip("Spawns at weapon bone (or caster root) when attack animation begins.")]
        public GameObject onStartVFX;
        [Tooltip("Positional offset from weapon bone / caster root.")]
        public Vector3 onStartOffset = new Vector3(0f, 1f, 0.5f);
        [Tooltip("Local rotation offset (euler) applied to the slash VFX relative to the weapon bone. Dial in per-skill to align with the blade arc.")]
        public Vector3 startRotationOffset = Vector3.zero;

        [Header("Impact")]
        [Tooltip("Spawns at hit position when ImpactEvent fires.")]
        public GameObject onImpactVFX;
        [Tooltip("Positional offset applied to HitPosition.")]
        public Vector3 onImpactOffset = Vector3.zero;

        [Header("Timing")]
        [Tooltip("Auto-destroys spawned VFX after this many seconds. 0 = let the prefab self-destroy.")]
        public float autoDestroyAfter = 3f;
    }
}
