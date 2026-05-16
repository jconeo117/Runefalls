using UnityEngine;
using Runefall.Combat;

namespace Runefall.Data
{
    [System.Serializable]
    public class SkillEffect
    {
        [Header("Effect Pipeline")]
        [Tooltip("Effects execute in order. Earlier effects mutate context so later ones see updated state.")]
        public EffectDefinition[] effects;

        [Header("Legacy (fallback when effects[] is empty)")]
        public float  damageMultiplier = 1f;
        public float  healPercent;
        public string animationTrigger;

        [Header("Camera")]
        public CameraSequenceData cameraSequence;
    }
}
