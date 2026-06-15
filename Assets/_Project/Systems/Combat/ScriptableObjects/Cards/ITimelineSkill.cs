using UnityEngine.Playables;

namespace Runefall.Data
{
    /// <summary>
    /// Implemented by any <see cref="SkillData"/> whose presentation is authored as a Timeline
    /// (normal skills AND ultimates). Lets <c>CombatAnimationDriver.PlayTimelineSkill</c> drive both
    /// through the same code path without depending on the concrete type.
    ///
    /// HitCount and VfxConfig are already exposed by SkillData; this only adds the Timeline-specific
    /// pieces (the choreography asset + the timed VFX cues fired from "Skill_VFX_N" signals).
    /// </summary>
    public interface ITimelineSkill
    {
        /// <summary>The choreography Timeline (animation + Damage/VFX signals). Null = use anim-sequence fallback.</summary>
        PlayableAsset SkillTimeline { get; }

        /// <summary>Timed VFX cues fired from the Timeline's "Skill_VFX_&lt;index&gt;" signals.</summary>
        SkillVFXCue[] VfxCues { get; }

        /// <summary>Timed SFX cues fired from the Timeline's "Skill_SFX_&lt;index&gt;" signals.</summary>
        SkillSFXCue[] SfxCues { get; }
    }
}
