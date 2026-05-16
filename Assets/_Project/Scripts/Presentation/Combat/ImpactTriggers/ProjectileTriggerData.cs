using System;
using System.Collections;
using UnityEngine;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    [CreateAssetMenu(menuName = "Runefall/Combat/Triggers/Projectile")]
    public class ProjectileTriggerData : ImpactTriggerData
    {
        [Tooltip("Prefab that travels from caster to target.")]
        public GameObject projectilePrefab;
        [Tooltip("Travel speed in world units/second.")]
        public float speed = 15f;
        [Tooltip("Y-offset above pawn pivot for spawn and arrival positions.")]
        public float heightOffset = 1.2f;
        [Tooltip("Distance at which projectile is considered arrived.")]
        public float arrivalThreshold = 0.15f;

        [Header("Spawn Timing")]
        [Tooltip("Animation Event that triggers projectile spawn. " +
                 "Archer clips use 'shoot'. Leave empty to use Release Delay instead.")]
        public string spawnEventName = "shoot";
        [Tooltip("Fallback seconds from Arm() before spawning, used only when Spawn Event Name is empty.")]
        public float releaseDelay = 0f;

        public override IImpactTrigger Create(Transform caster, Transform target, MonoBehaviour host, AnimationClip[] allClips = null)
            => new ProjectileTrigger(this, caster, target, host);
    }

    /// <summary>
    /// Waits for a "shoot" Animation Event (or releaseDelay fallback), then spawns a projectile
    /// and moves it toward the target. Impact fires on arrival — not on release.
    /// </summary>
    public sealed class ProjectileTrigger : IImpactTrigger
    {
        private Action _onImpact;
        private readonly ProjectileTriggerData _data;
        private readonly Transform             _caster;
        private readonly Transform             _target;
        private readonly MonoBehaviour         _host;
        private readonly CombatPawnAnimator    _anim;
        public bool HasFired { get; private set; }

        public ProjectileTrigger(ProjectileTriggerData data, Transform caster, Transform target, MonoBehaviour host)
        {
            _data   = data;
            _caster = caster;
            _target = target;
            _host   = host;
            _anim   = caster != null ? caster.GetComponentInChildren<CombatPawnAnimator>() : null;
        }

        public void Arm(Action onImpact)
        {
            _onImpact = onImpact;

            bool useAnimEvent = !string.IsNullOrEmpty(_data.spawnEventName) && _anim != null;

            if (useAnimEvent)
            {
                Debug.Log($"[ProjectileTrigger] Armed on '{_anim.gameObject.name}'. Waiting for '{_data.spawnEventName}' event to spawn projectile.");
                _anim.OnShoot += OnShootEvent;
            }
            else
            {
                _host.StartCoroutine(Travel(_data.releaseDelay));
            }
        }

        private void OnShootEvent()
        {
            _anim.OnShoot -= OnShootEvent;
            Debug.Log($"[ProjectileTrigger] '{_data.spawnEventName}' received — spawning projectile.");
            _host.StartCoroutine(Travel(0f));
        }

        private IEnumerator Travel(float spawnDelay)
        {
            if (spawnDelay > 0f)
                yield return new WaitForSeconds(spawnDelay);

            if (_data.projectilePrefab == null || _target == null) { Fire(); yield break; }

            Vector3 spawnPos = (_caster != null ? _caster.position : Vector3.zero)
                               + Vector3.up * _data.heightOffset;
            var go = UnityEngine.Object.Instantiate(_data.projectilePrefab, spawnPos, Quaternion.identity);

            while (go != null)
            {
                Vector3 dest = _target.position + Vector3.up * _data.heightOffset;
                if (Vector3.Distance(go.transform.position, dest) <= _data.arrivalThreshold)
                {
                    UnityEngine.Object.Destroy(go);
                    Fire();
                    yield break;
                }
                Vector3 dir = (dest - go.transform.position).normalized;
                go.transform.position += dir * _data.speed * Time.deltaTime;
                if (dir != Vector3.zero) go.transform.rotation = Quaternion.LookRotation(dir);
                yield return null;
            }
            Fire();
        }

        private void Fire()
        {
            if (HasFired) return;
            HasFired = true;
            Debug.Log("[ProjectileTrigger] Projectile arrived — impact resolved.");
            _onImpact?.Invoke();
        }
    }
}
