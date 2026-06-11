using System;
using System.Collections;
using UnityEngine;

namespace Runefall.Presentation.Combat
{
    public class PawnMovementChoreographer : IPawnMover
    {
        private readonly float _lungeStopDistance;

        public PawnMovementChoreographer(float lungeStopDistance)
        {
            _lungeStopDistance = lungeStopDistance;
        }

        public Vector3 ComputeLungeTarget(Transform casterPawn, Vector3 targetPos)
        {
            if (targetPos == Vector3.zero) return casterPawn.position;
            Vector3 dir = (casterPawn.position - targetPos).normalized;
            return targetPos + dir * _lungeStopDistance;
        }

        public void RotateCasterToward(Transform caster, Vector3 target)
        {
            if (target == Vector3.zero) return;
            Vector3 dir = target - caster.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                caster.rotation = Quaternion.LookRotation(dir);
        }

        public IEnumerator LungeTo(Transform pawn, Vector3 targetPosition, float duration, Action onImpact)
        {
            if (duration <= 0f)
            {
                pawn.position = targetPosition;
                onImpact?.Invoke();
                yield break;
            }

            Vector3 origin = pawn.position;
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / duration);
                pawn.position = Vector3.Lerp(origin, targetPosition, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            onImpact?.Invoke();
        }

        public IEnumerator ReturnToOrigin(Transform pawn, Vector3 originPosition, float duration, float rotateDuration)
        {
            // First smoothly rotate back to face original direction (looking toward origin position)
            yield return SmoothRotateTo(pawn, originPosition, 0f, rotateDuration);
            
            // Lunge back to origin
            yield return LungeTo(pawn, originPosition, duration, null);
            pawn.position = originPosition;
        }

        public IEnumerator SmoothRotateTo(Transform pawn, Vector3 lookTarget, float delay, float duration)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (pawn == null) yield break;

            Vector3 dir = lookTarget - pawn.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) yield break;

            Quaternion from = pawn.rotation;
            Quaternion to = Quaternion.LookRotation(dir);

            if (duration <= 0f)
            {
                pawn.rotation = to;
                yield break;
            }

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (pawn == null) yield break;
                pawn.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            if (pawn != null) pawn.rotation = to;
        }

        public IEnumerator DelayedLungeTo(
            Transform pawn, Vector3 target, float delay, float duration, bool snapOnEnd = false)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            yield return LungeTo(pawn, target, duration, null);
            if (snapOnEnd && pawn != null) pawn.position = target;
        }
    }
}
