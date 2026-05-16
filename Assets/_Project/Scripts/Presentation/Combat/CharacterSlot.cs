using UnityEngine;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    public class CharacterSlot : MonoBehaviour
    {
        public CharacterData data;
        [Tooltip("Assign the head bone Transform directly. Overrides auto-detection.")]
        public Transform headBone;
        [Tooltip("Fallback Y offset above pawn pivot (used when model has no humanoid rig).")]
        public float hpBarOffset = 3.5f;
        [Tooltip("Y offset above the head bone.")]
        public float headBoneOffset = 0.25f;
    }
}
