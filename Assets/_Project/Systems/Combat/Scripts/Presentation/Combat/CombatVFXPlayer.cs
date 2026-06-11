using System.Collections.Generic;
using UnityEngine;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Spawns VFX prefabs at the correct moments during combat.
    ///
    /// onStartVFX  — called by CombatAnimationDriver when a skill animation begins.
    /// onImpactVFX — driven by ImpactEvent SO subscription (fires on each resolved hit).
    ///
    /// Init() must be called by CombatBootstrapper after the combat context is built.
    /// </summary>
    public class CombatVFXPlayer : MonoBehaviour
    {
        [SerializeField] private ImpactEvent _impactEvent;

        private IReadOnlyDictionary<ICombatActor, Transform> _actorPawns;
        private IReadOnlyDictionary<ICombatActor, EnemyData> _actorEnemyData;

        /// <summary>
        /// Call from CombatBootstrapper after the combat context is built.
        /// ImpactEvent subscription is handled automatically in Awake via the Inspector SO reference.
        /// </summary>
        public void Init(
            IReadOnlyDictionary<ICombatActor, Transform> actorPawns,
            IReadOnlyDictionary<ICombatActor, EnemyData> actorEnemyData)
        {
            _actorPawns     = actorPawns;
            _actorEnemyData = actorEnemyData;
        }

        private void Awake()
        {
            _impactEvent?.Subscribe(OnImpact);
        }

        private void OnDestroy()
        {
            _impactEvent?.Unsubscribe(OnImpact);
        }

        // ── Called by CombatAnimationDriver at skill animation start ──────────────

        public GameObject PlayOnStartVFX(SkillVFXConfig config, Transform casterPawn, Vector3 targetPos)
        {
            if (config?.onStartVFX == null) return null;

            if (config.spawnOnStartAtTarget)
            {
                Vector3 pos = targetPos + config.onStartOffset;
                return Spawn(config.onStartVFX, pos, Quaternion.identity, config.autoDestroyAfter);
            }

            var anim = casterPawn.GetComponentInChildren<CombatPawnAnimator>();
            Transform bone = anim != null ? (anim.bladeRoot != null ? anim.bladeRoot : anim.weaponBone) : null;

            if (bone != null)
            {
                var go = Spawn(config.onStartVFX, bone.position, bone.rotation, config.autoDestroyAfter);
                if (go != null)
                {
                    go.transform.SetParent(bone, worldPositionStays: false);
                    go.transform.localPosition = config.onStartOffset;
                    go.transform.localRotation = Quaternion.Euler(config.startRotationOffset);
                }
                return go;
            }
            else
            {
                Vector3    pos = casterPawn.position + config.onStartOffset;
                Quaternion rot = FaceToward(pos, targetPos);
                return Spawn(config.onStartVFX, pos, rot, config.autoDestroyAfter);
            }
        }

        // ── ImpactEvent subscriber ────────────────────────────────────────────────

        private void OnImpact(ImpactContext ctx)
        {
            var config = ResolveConfig(ctx);
            if (config?.onImpactVFX == null) return;

            Vector3 pos = ctx.HitPosition + config.onImpactOffset;

            Quaternion rot = Quaternion.identity;
            if (ctx.Attacker != null && _actorPawns != null
                && _actorPawns.TryGetValue(ctx.Attacker, out var attackerPawn))
                rot = FaceToward(attackerPawn.position, ctx.HitPosition);

            Spawn(config.onImpactVFX, pos, rot, config.autoDestroyAfter);
        }

        private SkillVFXConfig ResolveConfig(ImpactContext ctx)
        {
            if (ctx.Skill != null) return ctx.Skill.VfxConfig;
            return null;
        }

        private static Quaternion FaceToward(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
        }

        // Fallback lifetime for prefabs whose only ParticleSystems are looping
        // (CalcVFXDuration returns 0 for them, so without this they never get destroyed).
        private const float k_LoopFallback = 3f;

        private GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, float destroyAfter)
        {
            var go = Instantiate(prefab, pos, rot);

            // Force one-shot: disable loop on every PS regardless of prefab setting.
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var m  = ps.main;
                m.loop = false;
            }

            float duration = destroyAfter > 0f ? destroyAfter : CalcVFXDuration(go);
            if (duration <= 0f) duration = k_LoopFallback;
            Destroy(go, duration);
            return go;
        }

        private static float CalcVFXDuration(GameObject go)
        {
            float max = 0f;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
            {
                var m = ps.main;
                if (m.loop) continue;
                float end = m.duration + m.startLifetime.constantMax;
                if (end > max) max = end;
            }
            return max;
        }
    }
}
