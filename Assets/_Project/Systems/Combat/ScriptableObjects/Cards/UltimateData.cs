using UnityEngine;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Presentation.Combat;

namespace Runefall.Data
{
    [CreateAssetMenu(menuName = "Runefall/Cards/Ultimate")]
    public class UltimateData : SkillData
    {
        public string ultimateName;
        public SkillEffect effect;

        [Header("Animations")]
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
            driver.PlayDefaultAnimationSequence(this, caster, target, rank, onComplete);
        }

        public override AnimationClip[] AnimSequence => animSequence;
        public override int ImpactAfterClipIndex => 0;
        public override int ReturnLungeClipIndex => -1;
        public override int HitCount => 1;
        public override ImpactTriggerData ImpactTrigger => null;
        public override SkillVFXConfig VfxConfig => null;
    }
}
