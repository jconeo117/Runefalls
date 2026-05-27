using System;
using System.Collections;
using System.Collections.Generic;
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

        public override IImpactTrigger Create(Transform caster, Transform target, MonoBehaviour host,
            AnimationClip[] allClips = null, int expectedHits = 0,
            IReadOnlyList<(ICombatActor actor, Transform pawn)> aoeTargets = null)
        {
            int hits = expectedHits > 0 ? expectedHits : CountExpectedHits(allClips, spawnEventName);
            return new ProjectileTrigger(this, caster, target, host, hits, aoeTargets);
        }

        private static int CountExpectedHits(AnimationClip[] clips, string evName)
        {
            if (string.IsNullOrEmpty(evName) || clips == null || clips.Length == 0) return 1;
            int count = 0;
            foreach (var c in clips)
            {
                if (c == null) continue;
                foreach (var ev in c.events)
                    if (ev.functionName == evName) { count++; break; }
            }
            return count > 0 ? count : 1;
        }
    }

    /// <summary>
    /// Spawns projectiles per shoot AE. Two modes:
    ///   Single-target: one projectile per AE, all travel to _singleTarget.
    ///   AoE: one projectile per alive enemy per AE. Each arrival fires for its specific target.
    /// Callback: (waveIdx, totalWaves, specificActor). specificActor is null for single-target.
    /// HasFired = true after the last projectile of the last wave lands.
    /// </summary>
    public sealed class ProjectileTrigger : IImpactTrigger
    {
        private Action<int, int, ICombatActor>  _onImpact;
        private readonly ProjectileTriggerData  _data;
        private readonly Transform              _caster;
        private readonly Transform              _singleTarget;
        private readonly MonoBehaviour          _host;
        private readonly CombatPawnAnimator     _anim;
        private readonly int                    _expectedHits;
        private readonly IReadOnlyList<(ICombatActor actor, Transform pawn)> _aoeTargets;
        private readonly int                    _totalExpectedFires;
        private int                             _shotsReceived;
        private int                             _hitsFired;
        public bool HasFired { get; private set; }

        public ProjectileTrigger(
            ProjectileTriggerData data,
            Transform caster,
            Transform singleTarget,
            MonoBehaviour host,
            int expectedHits,
            IReadOnlyList<(ICombatActor actor, Transform pawn)> aoeTargets = null)
        {
            _data               = data;
            _caster             = caster;
            _singleTarget       = singleTarget;
            _host               = host;
            _expectedHits       = Mathf.Max(1, expectedHits);
            _aoeTargets         = aoeTargets != null && aoeTargets.Count > 0 ? aoeTargets : null;
            _anim               = caster != null ? caster.GetComponentInChildren<CombatPawnAnimator>() : null;
            _totalExpectedFires = _aoeTargets != null
                ? _expectedHits * _aoeTargets.Count
                : _expectedHits;
        }

        public void Arm(Action<int, int, ICombatActor> onImpact)
        {
            _onImpact = onImpact;
            bool useAnimEvent = !string.IsNullOrEmpty(_data.spawnEventName) && _anim != null;
            if (useAnimEvent)
            {
                _anim.OnShoot += OnShootEvent;
            }
            else
            {
                _host.StartCoroutine(SpawnWaveDelayed(_data.releaseDelay, 0));
            }
        }

        private void OnShootEvent()
        {
            int wave = _shotsReceived;
            _shotsReceived++;
            if (_shotsReceived >= _expectedHits)
                _anim.OnShoot -= OnShootEvent;
            SpawnWave(wave);
        }

        private IEnumerator SpawnWaveDelayed(float delay, int waveIdx)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            SpawnWave(waveIdx);
        }

        private void SpawnWave(int waveIdx)
        {
            if (_aoeTargets != null)
            {
                foreach (var (actor, pawn) in _aoeTargets)
                {
                    var cap = (actor, pawn);
                    _host.StartCoroutine(TravelTo(cap.pawn, waveIdx, cap.actor));
                }
            }
            else
            {
                _host.StartCoroutine(TravelTo(_singleTarget, waveIdx, null));
            }
        }

        private IEnumerator TravelTo(Transform target, int waveIdx, ICombatActor specificActor)
        {
            if (_data.projectilePrefab == null || target == null)
            {
                OnArrival(waveIdx, specificActor);
                yield break;
            }

            Vector3 spawnPos = (_caster != null ? _caster.position : Vector3.zero)
                               + Vector3.up * _data.heightOffset;
            var go = UnityEngine.Object.Instantiate(_data.projectilePrefab, spawnPos, Quaternion.identity);

            while (go != null)
            {
                Vector3 dest = target.position + Vector3.up * _data.heightOffset;
                if (Vector3.Distance(go.transform.position, dest) <= _data.arrivalThreshold)
                {
                    UnityEngine.Object.Destroy(go);
                    OnArrival(waveIdx, specificActor);
                    yield break;
                }
                Vector3 dir = (dest - go.transform.position).normalized;
                go.transform.position += dir * _data.speed * Time.deltaTime;
                if (dir != Vector3.zero) go.transform.rotation = Quaternion.LookRotation(dir);
                yield return null;
            }
            OnArrival(waveIdx, specificActor);
        }

        private void OnArrival(int waveIdx, ICombatActor specificActor)
        {
            _hitsFired++;
            _onImpact?.Invoke(waveIdx, _expectedHits, specificActor);
            if (_hitsFired >= _totalExpectedFires)
                HasFired = true;
        }
    }
}
