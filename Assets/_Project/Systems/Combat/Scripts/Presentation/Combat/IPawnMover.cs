using System;
using System.Collections;
using UnityEngine;

namespace Runefall.Presentation.Combat
{
    public interface IPawnMover
    {
        IEnumerator LungeTo(Transform pawn, Vector3 targetPosition, float duration, Action onImpact);
        IEnumerator ReturnToOrigin(Transform pawn, Vector3 originPosition, float duration, float rotateDuration);
    }
}
