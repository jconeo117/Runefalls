using UnityEngine;
using UnityEngine.Timeline;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Presentation.Combat;

namespace Runefall.Data
{
    [CreateAssetMenu(menuName = "Runefall/Cards/Custom/Hero1_SaberSlash")]
    public class Hero1_SaberSlash : SkillData
    {
        [Header("Cinematic Timelines by Rank")]
        public TimelineAsset rank1Timeline; // E.g., Slight zoom focus
        public TimelineAsset rank2Timeline; // E.g., Close-up profile -> sweep slash
        public TimelineAsset rank3Timeline; // E.g., Full cutscene, custom camera cuts

        public override void PlayPresentation(
            CombatAnimationDriver driver, 
            ICombatActor caster, 
            ICombatActor target, 
            int rank, 
            System.Action onComplete)
        {
            TimelineAsset activeTimeline = rank switch
            {
                1 => rank1Timeline,
                2 => rank2Timeline,
                _ => rank3Timeline
            };

            if (activeTimeline != null)
            {
                driver.PlayTimelineCinematic(activeTimeline, caster, target, onComplete);
            }
            else
            {
                Debug.LogWarning($"[Hero1_SaberSlash] No timeline configured for Rank {rank}. Presentation skipped.");
                onComplete?.Invoke();
            }
        }

        public override (float dmg, bool crit, float lifeSteal, float heal) ExecuteGameplayEffect(
            ICombatActor caster, ICombatActor target, int rank, float hitFraction = 1f)
        {
            // Custom gameplay logic can be added here.
            // E.g. base standard calculations from effectsByRank:
            return base.ExecuteGameplayEffect(caster, target, rank, hitFraction);
        }
    }
}
