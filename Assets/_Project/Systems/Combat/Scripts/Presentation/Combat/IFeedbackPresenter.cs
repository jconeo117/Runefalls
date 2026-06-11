using UnityEngine;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    public interface IFeedbackPresenter
    {
        void SpawnDamageNumber(Transform targetPawn, float damage, bool isCrit);
        GameObject PlayOnStartVFX(SkillVFXConfig config, Transform casterPawn, Vector3 targetPos);
        void PlayOnImpactVFX(ImpactEvent impactEvent, ImpactContext ctx);
    }
}
