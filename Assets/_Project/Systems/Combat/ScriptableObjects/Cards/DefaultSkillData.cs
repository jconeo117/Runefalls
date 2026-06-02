using UnityEngine;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Presentation.Combat;

namespace Runefall.Data
{
    [CreateAssetMenu(menuName = "Runefall/Cards/Skill")]
    public class DefaultSkillData : SkillData
    {
        [Header("Default Presentation Details")]
        [Tooltip("Index of the clip after which impact (damage visuals) is applied.\n" +
                 "-1 = before any clip | 0 = after clip 0 | 1 = after clip 1, etc.")]
        public int impactAfterClipIndex = 0;
        [Tooltip("Index of the clip during which the return movement runs.\n" +
                 "-1 = use last clip in sequence (default).")]
        public int returnAtClipIndex = -1;

        [Header("Multi-Hit")]
        [Tooltip("Total hits this skill delivers. Each hit = totalDamage / hitCount.\n" +
                 "Must match the number of impact AEs (ImpactFrame or shoot) across all clips.\n" +
                 "1 = single hit (default). 2+ = multi-hit with overkill floating numbers.")]
        public int hitCount = 1;

        [Header("Impact Trigger")]
        [Tooltip("Precise moment damage resolves. Null = legacy clip-index fallback.\n" +
                 "AnimEvent = contact frame | Projectile = arrow arrival | Timer = fixed delay.")]
        public ImpactTriggerData impactTrigger;

        [Header("VFX")]
        [Tooltip("VFX prefabs for this skill. Null = no visual effects.")]
        public SkillVFXConfig vfxConfig;

        [Header("Animations")]
        public AnimationClip[] animSequence;

        public override void PlayPresentation(
            CombatAnimationDriver driver, 
            ICombatActor caster, 
            ICombatActor target, 
            int rank, 
            System.Action onComplete)
        {
            driver.PlayDefaultAnimationSequence(this, caster, target, rank, onComplete);
        }
    }
}
