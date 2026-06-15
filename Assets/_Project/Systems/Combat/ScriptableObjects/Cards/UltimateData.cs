using UnityEngine;
using UnityEngine.Playables;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Presentation.Combat;

namespace Runefall.Data
{
    [CreateAssetMenu(menuName = "Runefall/Cards/Ultimate")]
    public class UltimateData : SkillData, ITimelineSkill
    {
        public string ultimateName;
        public SkillEffect effect;

        [Header("Timeline Presentation")]
        [Tooltip("Choreography Timeline (animation + Damage/VFX signals). Assigned = uses the Timeline " +
                 "pipeline like skills; null = falls back to the flat animSequence below.")]
        public PlayableAsset ultimateTimeline;
        [Tooltip("Total hits. Place this many 'Skill_Damage' emitters in the Timeline.")]
        public int hitCount = 1;
        [Tooltip("VFX prefabs (onStart / onImpact). Null = no VFX.")]
        public SkillVFXConfig vfxConfig;
        [Tooltip("Timed VFX cues fired from the Timeline's 'Skill_VFX_<index>' signals.")]
        public SkillVFXCue[] vfxCues;
        [Tooltip("Timed SFX cues fired from the Timeline's 'Skill_SFX_<index>' signals.")]
        public SkillSFXCue[] sfxCues;

        [Header("Animations (fallback when no Timeline)")]
        public AnimationClip[] animSequence;

        private void OnValidate()
        {
            skillName = ultimateName;
        }

        public override (float dmg, bool crit, float lifeSteal, float heal) ExecuteGameplayEffect(
            ICombatActor caster, ICombatActor target, int rank, float hitFraction = 1f)
        {
            if (effect == null) return (0f, false, 0f, 0f);
            return CombatResolver.ApplyEffect(effect, caster, target, 3, hitFraction);
        }

        public override void PlayPresentation(
            CombatAnimationDriver driver,
            ICombatActor caster,
            ICombatActor target,
            int rank,
            System.Action onComplete)
        {
            // Same two pipelines as skills: Timeline if authored, else the flat clip sequence.
            if (ultimateTimeline != null)
                driver.PlayTimelineSkill(this, caster, target, rank, onComplete);
            else
                driver.PlayDefaultAnimationSequence(this, caster, target, rank, onComplete);
        }

        public override AnimationClip[] AnimSequence => animSequence;
        public override int ImpactAfterClipIndex => 0;
        public override int ReturnLungeClipIndex => -1;
        public override int HitCount => Mathf.Max(1, hitCount);
        public override ImpactTriggerData ImpactTrigger => null;
        public override SkillVFXConfig VfxConfig => vfxConfig;

        // ── ITimelineSkill ────────────────────────────────────────────────────────
        public PlayableAsset SkillTimeline => ultimateTimeline;
        public SkillVFXCue[] VfxCues       => vfxCues;
        public SkillSFXCue[] SfxCues       => sfxCues;
    }
}
