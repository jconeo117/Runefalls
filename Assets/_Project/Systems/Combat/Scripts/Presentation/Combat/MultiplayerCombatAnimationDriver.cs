using System;
using System.Collections;
using UnityEngine;
using Runefall.Combat;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Specific animation driver for multiplayer that uses physical lunges 
    /// (moving transform towards the target, triggering impact, and returning)
    /// instead of depending on runtime animator controller clip overrides.
    /// </summary>
    public class MultiplayerCombatAnimationDriver : CombatAnimationDriver
    {
        private bool _isMultiplayerDraining = false;

        public override void PlayQueuedAnimations(Action onComplete, bool fadeSlots = false)
        {
            Debug.Log($"[MultiplayerCombatAnimationDriver] PlayQueuedAnimations called. _isMultiplayerDraining={_isMultiplayerDraining}, queueCount={_animQueue.Count}");
            if (_isMultiplayerDraining) return;
            StartCoroutine(DrainQueueMultiplayer(onComplete, fadeSlots));
        }

        private IEnumerator DrainQueueMultiplayer(Action onComplete, bool fadeSlots = false)
        {
            _isMultiplayerDraining = true;
            Debug.Log("[MultiplayerCombatAnimationDriver] DrainQueueMultiplayer started.");
            try
            {
                int slotIndex = 0;
                while (_animQueue.Count > 0)
                {
                    if (_ctx != null && _ctx.IsOver)
                    {
                        Debug.Log("[MultiplayerCombatAnimationDriver] Combat is over. Stopping queue drain.");
                        break;
                    }

                    var pending = _animQueue.Dequeue();
                    Debug.Log($"[MultiplayerCombatAnimationDriver] Dequeued pending action: Caster={(pending.Caster != null ? pending.Caster.Name : "null")}, Skill={(pending.Skill != null ? pending.Skill.skillName : "null")}");
                    yield return StartCoroutine(PlayPhysicalLunge(pending));

                    if (fadeSlots)
                    {
                        _presenter?.NotifyActionAnimationComplete(slotIndex++);
                    }

                    if (_ctx != null && _ctx.IsOver) break;
                }

                if (_ctx != null && _ctx.IsOver)
                {
                    yield return StartCoroutine(RunCombatEndDrama());
                }

                Debug.Log("[MultiplayerCombatAnimationDriver] Queue drain complete. Invoking onComplete callback.");
                onComplete?.Invoke();
            }
            finally
            {
                _isMultiplayerDraining = false;
            }
        }

        private IEnumerator PlayPhysicalLunge(PendingAction pending)
        {
            if (pending.Caster == null)
            {
                Debug.LogError("[MultiplayerCombatAnimationDriver] PlayPhysicalLunge aborted: pending.Caster is null.");
                yield break;
            }

            if (_actorPawns == null)
            {
                Debug.LogError("[MultiplayerCombatAnimationDriver] PlayPhysicalLunge aborted: _actorPawns dictionary is null.");
                yield break;
            }

            if (!_actorPawns.TryGetValue(pending.Caster, out var casterPawn))
            {
                Debug.LogError($"[MultiplayerCombatAnimationDriver] PlayPhysicalLunge aborted: Caster '{pending.Caster.Name}' not found in _actorPawns! Present keys count={_actorPawns.Count}");
                foreach (var key in _actorPawns.Keys)
                {
                    Debug.Log($"   - Key in _actorPawns: '{key.Name}' (Type: {key.GetType().Name})");
                }
                yield break;
            }

            Debug.Log($"[MultiplayerCombatAnimationDriver] PlayPhysicalLunge started for '{pending.Caster.Name}' towards '{(pending.Target != null ? pending.Target.Name : "null")}'");

            Transform targetPawn = null;
            Vector3 targetPosition;

            if (pending.Target != null && _actorPawns.TryGetValue(pending.Target, out targetPawn))
            {
                targetPosition = targetPawn.position;
            }
            else
            {
                targetPosition = casterPawn.position + casterPawn.forward * 2f;
                Debug.LogWarning($"[MultiplayerCombatAnimationDriver] Target not found in _actorPawns or null. Using forward fallback position: {targetPosition}");
            }

            Vector3 originPosition = casterPawn.position;
            Quaternion originRotation = casterPawn.rotation;

            Vector3 lungeTarget = targetPosition;
            if (targetPawn != null)
            {
                Vector3 toCaster = (originPosition - targetPosition).normalized;
                lungeTarget = targetPosition + toCaster * lungeStopDistance;
            }

            Debug.Log($"[MultiplayerCombatAnimationDriver] Moving from {originPosition} to lunge target {lungeTarget}");

            // 1. Lunge Forward (Approach)
            float lungeDuration = 0.25f;
            float elapsed = 0f;

            Vector3 lookDir = (targetPosition - originPosition).normalized;
            if (lookDir != Vector3.zero)
            {
                casterPawn.rotation = Quaternion.LookRotation(lookDir);
            }

            var casterAnim = casterPawn.GetComponentInChildren<CombatPawnAnimator>();
            casterAnim?.PlayApproach();

            while (elapsed < lungeDuration)
            {
                casterPawn.position = Vector3.Lerp(originPosition, lungeTarget, elapsed / lungeDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            casterPawn.position = lungeTarget;

            // 2. Impact Event / Resolve Hits
            Debug.Log("[MultiplayerCombatAnimationDriver] Raising impact hit.");
            RaiseImpactHit(pending, 0, 1);

            if (targetPawn != null)
            {
                targetPawn.GetComponentInChildren<CombatPawnAnimator>()?.PlayHit();
            }

            yield return new WaitForSeconds(0.2f);

            // 3. Return Home
            Debug.Log("[MultiplayerCombatAnimationDriver] Returning home.");
            elapsed = 0f;
            float returnDuration = 0.2f;

            Vector3 returnLook = (originPosition - lungeTarget).normalized;
            if (returnLook != Vector3.zero)
            {
                casterPawn.rotation = Quaternion.LookRotation(returnLook);
            }

            while (elapsed < returnDuration)
            {
                casterPawn.position = Vector3.Lerp(lungeTarget, originPosition, elapsed / returnDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            casterPawn.position = originPosition;
            casterPawn.rotation = originRotation;

            casterAnim?.PlayReturn();
            Debug.Log("[MultiplayerCombatAnimationDriver] PlayPhysicalLunge finished.");
        }
    }
}
