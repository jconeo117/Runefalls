using UnityEngine;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    public class EnemySlot : MonoBehaviour
    {
        public EnemyData data;
        [Tooltip("Assign the head bone Transform directly. Overrides auto-detection.")]
        public Transform headBone;
        [Tooltip("Fallback Y offset above pawn pivot (used when model has no humanoid rig).")]
        public float hpBarOffset = 3.5f;
        [Tooltip("Y offset above the head bone.")]
        public float headBoneOffset = 0.25f;
    }
}
