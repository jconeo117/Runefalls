using UnityEngine;
using UnityEngine.Playables;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Presentation.Combat;

namespace Runefall.Data
{
    /// <summary>
    /// A skill whose presentation (camera, animation, damage timing, VFX) is authored as a Timeline
    /// instead of a flat clip array — for complex choreographies. The Timeline is character-agnostic:
    /// the caster animator, target, camera and VFX are bound at runtime. Damage and VFX fire from
    /// named Signal emitters.
    ///
    /// Authoring contract (what the runtime looks for in the .playable):
    ///   - Animation Track  whose name contains "caster"  -> bound to the caster's Animator.
    ///   - Cinemachine Tracks whose names contain "bronze" / "silver" / "gold" -> only the ACTIVE
    ///     rank's track stays unmuted (dynamic camera per rank). Shots use these exposed names,
    ///     created at runtime around the caster/target:
    ///         "cam_caster"  -> behind the caster, framing the action
    ///         "cam_target"  -> in front of the target (impact close-up)
    ///         "cam_side"    -> side profile of the caster->target line
    ///   - Signal emitters (by SignalAsset name):
    ///         "Skill_Damage"   -> resolves damage + impact VFX (place hitCount of these).
    ///         "Skill_VFXStart" -> spawns the on-start VFX (swing / cast).
    ///
    /// Damage values still come from the inherited <see cref="SkillData.effectsByRank"/> (rank 1/2/3
    /// = bronze/silver/gold), so the numeric balance is shared with the rest of the skill system.
    /// </summary>
    [CreateAssetMenu(menuName = "Runefall/Cards/Timeline Skill")]
    public class TimelineSkillData : SkillData, ITimelineSkill
    {
        [Header("Timeline Presentation")]
        [Tooltip("The choreography Timeline: camera-per-rank, animation, Damage/VFX signals.")]
        public PlayableAsset skillTimeline;

        [Header("Multi-Hit")]
        [Tooltip("Total hits. Place this many 'Skill_Damage' emitters in the Timeline. Damage per hit = total / hitCount.")]
        public int hitCount = 1;

        [Header("VFX")]
        [Tooltip("VFX prefabs (onStart / onImpact). Null = no VFX.")]
        public SkillVFXConfig vfxConfig;

        [Header("VFX Choreography")]
        [Tooltip("Timed VFX cues. Cue at index N fires when the timeline signal 'Skill_VFX_N' triggers — " +
                 "place those signals in the Timeline to choreograph multiple effects (e.g. ground circle, then falling crystal).")]
        public SkillVFXCue[] vfxCues;

        [Header("SFX Choreography")]
        [Tooltip("Timed SFX cues. Cue at index N fires when the timeline signal 'Skill_SFX_N' triggers — " +
                 "same model as the VFX cues, played through the AudioManager.")]
        public SkillSFXCue[] sfxCues;

        public override void PlayPresentation(
            CombatAnimationDriver driver,
            ICombatActor caster,
            ICombatActor target,
            int rank,
            System.Action onComplete)
        {
            driver.PlayTimelineSkill(this, caster, target, rank, onComplete);
        }

        public override int HitCount => hitCount;
        public override SkillVFXConfig VfxConfig => vfxConfig;

        // ── ITimelineSkill ────────────────────────────────────────────────────────
        public PlayableAsset SkillTimeline => skillTimeline;
        public SkillVFXCue[] VfxCues       => vfxCues;
        public SkillSFXCue[] SfxCues       => sfxCues;
    }

    /// <summary>Where a VFX cue spawns. Pawn positions are at ground/feet level.</summary>
    public enum VFXAnchor { Caster, Target, EnemiesCenter, AllEnemies }

    /// <summary>
    /// One timed VFX in a skill's choreography. The driver spawns it when the matching timeline
    /// signal ("Skill_VFX_&lt;index&gt;") fires: the WHEN comes from the Timeline, the WHAT/WHERE
    /// from this cue. Reusable for any character — author the signals + fill this list.
    /// </summary>
    [System.Serializable]
    public class SkillVFXCue
    {
        [Tooltip("Editor note (e.g. 'Ground circle'). Fires from the timeline signal 'Skill_VFX_<this array index>'.")]
        public string label;
        public GameObject prefab;
        [Tooltip("Caster / Target / center of all alive enemies / one spawn per enemy.")]
        public VFXAnchor anchor = VFXAnchor.Target;
        [Tooltip("Offset added to the anchor (e.g. raise Y for a falling effect's start point).")]
        public Vector3 worldOffset;
        public Vector3 scale = Vector3.one;
        [Tooltip("Rotate the spawned VFX so its forward (+Z) points from the caster toward the target — " +
                 "for directional/projectile effects. The angle is whatever direction the target is in " +
                 "(slight if the target is mostly in front). Off = no rotation (e.g. ground circles).")]
        public bool faceTarget;
        [Tooltip("Extra rotation (euler) applied AFTER facing — to correct a prefab whose travel axis isn't +Z.")]
        public Vector3 rotationOffset;
        [Tooltip("Seconds before the spawned VFX auto-destroys. 0 = never.")]
        public float autoDestroyAfter = 4f;
    }

    /// <summary>
    /// One timed sound effect in a skill's choreography. The driver plays it (via IAudioService /
    /// AudioManager) when the matching timeline signal ("Skill_SFX_&lt;index&gt;") fires: the WHEN
    /// comes from the Timeline, the WHAT/WHERE from this cue. Mirrors <see cref="SkillVFXCue"/>.
    /// </summary>
    [System.Serializable]
    public class SkillSFXCue
    {
        [Tooltip("Editor note (e.g. 'Swing whoosh'). Fires from the timeline signal 'Skill_SFX_<this array index>'.")]
        public string label;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Variación aleatoria de pitch (+/-) para que no suene idéntico cada vez. 0 = sin variación.")]
        [Range(0f, 0.5f)] public float pitchJitter = 0f;
        [Tooltip("Posición de origen del sonido (3D depende del spatialBlend del AudioManager).")]
        public VFXAnchor anchor = VFXAnchor.Caster;
    }
}
