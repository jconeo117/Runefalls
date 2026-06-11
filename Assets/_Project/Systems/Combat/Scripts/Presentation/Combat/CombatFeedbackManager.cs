using UnityEngine;
using Runefall.Data;
using Runefall.Combat;

namespace Runefall.Presentation.Combat
{
    public class CombatFeedbackManager : IFeedbackPresenter
    {
        private readonly GameObject _damageNumberPrefab;
        private readonly CombatVFXPlayer _vfxPlayer;

        public CombatFeedbackManager(GameObject damageNumberPrefab, CombatVFXPlayer vfxPlayer)
        {
            _damageNumberPrefab = damageNumberPrefab;
            _vfxPlayer = vfxPlayer;
        }

        public void SpawnDamageNumber(Transform targetPawn, float damage, bool isCrit)
        {
            if (_damageNumberPrefab == null || targetPawn == null) return;

            Vector3 offset = new Vector3(
                Random.Range(-0.3f, 0.3f),
                1.8f + Random.Range(0f, 0.35f),
                Random.Range(-0.15f, 0.15f));

            var go = Object.Instantiate(_damageNumberPrefab, targetPawn.position + offset, Quaternion.identity);
            var dmg = go.GetComponent<DamageNumber>();

            string text = $"{damage:F0}";
            float fontSize = isCrit ? 16f : 12f;
            dmg?.Show(text, fontSize, isCrit);
        }

        public GameObject PlayOnStartVFX(SkillVFXConfig config, Transform casterPawn, Vector3 targetPos)
        {
            if (_vfxPlayer == null) return null;
            return _vfxPlayer.PlayOnStartVFX(config, casterPawn, targetPos);
        }

        public void PlayOnImpactVFX(ImpactEvent impactEvent, ImpactContext ctx)
        {
            if (impactEvent != null)
            {
                impactEvent.Raise(ctx);
            }
        }
    }
}
