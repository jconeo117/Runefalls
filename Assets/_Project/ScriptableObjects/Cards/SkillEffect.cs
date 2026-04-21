using UnityEngine;

namespace Runefall.Data
{
    [System.Serializable]
    public class SkillEffect
    {
        public float damageMultiplier = 1f;
        public float healPercent;
        public string animationTrigger;

        [Header("Camera")]
        public CameraSequenceData cameraSequence;
    }
}
