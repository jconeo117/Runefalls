using System;
using UnityEngine;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    [CreateAssetMenu(menuName = "Runefall/Combat/Triggers/AnimationEvent")]
    public class AnimEventTriggerData : ImpactTriggerData
    {
        [Tooltip("Name of the Animation Event method on the clip that signals the impact frame. " +
                 "Melee/generic: 'ImpactFrame' | Archer: 'shoot' | Must match a method on CombatPawnAnimator.")]
        public string eventName = "ImpactFrame";

        [Tooltip("Safety-net only: fires if not all impact events arrive before total clip duration + 0.3s. " +
                 "Used as the sole delay for clips that have NO matching event set.")]
        public float fallbackDelay = 0.5f;

        public override IImpactTrigger Create(Transform caster, Transform target, MonoBehaviour host, AnimationClip[] allClips = null)
        {
            int   expectedHits = CountExpectedHits(allClips, eventName);
            float safetyDelay  = ComputeSafetyDelay(allClips, fallbackDelay);
            return new AnimEventTrigger(caster, host, expectedHits, safetyDelay);
        }

        // One expected hit per clip-slot that contains the configured event name.
        // Same clip referenced twice in the array counts as two expected hits.
        private static int CountExpectedHits(AnimationClip[] clips, string evName)
        {
            if (clips == null || clips.Length == 0) return 1;
            int count = 0;
            foreach (var c in clips)
            {
                if (c == null) continue;
                foreach (var ev in c.events)
                    if (ev.functionName == evName) { count++; break; }
            }
            return count > 0 ? count : 1;
        }

        // Safety fires after full sequence + buffer so it never races with real events.
        private static float ComputeSafetyDelay(AnimationClip[] clips, float fallback)
        {
            if (clips == null || clips.Length == 0) return fallback;
            float total = 0f;
            foreach (var c in clips)
                if (c != null) total += c.length;
            return total > 0f ? total + 0.3f : fallback;
        }
    }

    /// <summary>
    /// Fires impact callback once for every ImpactFrame Animation Event received,
    /// up to the expected hit count derived from the skill's clip array.
    /// HasFired becomes true only after ALL expected hits are received (or safety timer fires).
    /// </summary>
    public sealed class AnimEventTrigger : IImpactTrigger
    {
        private Action _onImpact;
        private readonly CombatPawnAnimator _anim;
        private readonly float              _safetyDelay;
        private readonly MonoBehaviour      _host;
        private readonly int                _expectedHits;
        private int                         _hitsReceived;
        public bool HasFired { get; private set; }

        public AnimEventTrigger(Transform caster, MonoBehaviour host, int expectedHits, float safetyDelay)
        {
            _anim         = caster != null ? caster.GetComponentInChildren<CombatPawnAnimator>() : null;
            _host         = host;
            _expectedHits = expectedHits;
            _safetyDelay  = safetyDelay;
        }

        public void Arm(Action onImpact)
        {
            _onImpact = onImpact;

            if (_anim != null)
            {
                _anim.OnImpactFrame += OnHit;
                Debug.Log($"[AnimEventTrigger] Armed on '{_anim.gameObject.name}'. " +
                          $"expectedHits={_expectedHits} safetyDelay={_safetyDelay:F3}s");
                if (_host != null && _safetyDelay > 0f)
                    _host.StartCoroutine(SafetyTimer());
            }
            else
            {
                Debug.LogWarning("[AnimEventTrigger] No CombatPawnAnimator found — firing impact immediately.");
                Complete();
            }
        }

        private void OnHit()
        {
            if (HasFired) return;
            _hitsReceived++;
            Debug.Log($"[AnimEventTrigger] ImpactFrame hit {_hitsReceived}/{_expectedHits}.");
            _onImpact?.Invoke();

            if (_hitsReceived >= _expectedHits)
                Complete();
        }

        private System.Collections.IEnumerator SafetyTimer()
        {
            yield return new UnityEngine.WaitForSeconds(_safetyDelay);
            if (!HasFired)
            {
                Debug.LogWarning($"[AnimEventTrigger] Safety timer fired on '{_anim?.gameObject.name}' " +
                                 $"after {_safetyDelay:F3}s — only {_hitsReceived}/{_expectedHits} ImpactFrame events received.");
                Complete();
            }
        }

        private void Complete()
        {
            if (HasFired) return;
            HasFired = true;
            if (_anim != null) _anim.OnImpactFrame -= OnHit;
        }
    }
}
