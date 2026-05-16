using System;
using System.Collections;
using UnityEngine;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    [CreateAssetMenu(menuName = "Runefall/Combat/Triggers/Timer")]
    public class TimerTriggerData : ImpactTriggerData
    {
        [Tooltip("Seconds after Arm() before impact fires.")]
        public float delay = 0.4f;

        public override IImpactTrigger Create(Transform caster, Transform target, MonoBehaviour host, AnimationClip[] allClips = null)
            => new TimerTrigger(delay, host);
    }

    /// <summary>
    /// Fires impact after a fixed delay. Use for VFX spells, AoE effects,
    /// or any skill where timing is controlled by a designer-set value rather than animation.
    /// </summary>
    public sealed class TimerTrigger : IImpactTrigger
    {
        private Action _onImpact;
        private readonly float _delay;
        private readonly MonoBehaviour _host;
        public bool HasFired { get; private set; }

        public TimerTrigger(float delay, MonoBehaviour host)
        {
            _delay = delay; _host = host;
        }

        public void Arm(Action onImpact)
        {
            _onImpact = onImpact;
            _host.StartCoroutine(FireAfterDelay());
        }

        private IEnumerator FireAfterDelay()
        {
            yield return new WaitForSeconds(_delay);
            if (!HasFired) { HasFired = true; _onImpact?.Invoke(); }
        }
    }
}
