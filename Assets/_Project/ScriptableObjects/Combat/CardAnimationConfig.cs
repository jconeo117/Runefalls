using UnityEngine;

namespace Runefall.Presentation.Combat
{
    [CreateAssetMenu(fileName = "CardAnimationConfig",
                     menuName  = "Runefall/Combat/Card Animation Config")]
    public class CardAnimationConfig : ScriptableObject
    {
        [Header("Draw")]
        [Tooltip("Duration of the slide-in per card.")]
        public float drawDuration      = 0.35f;
        [Tooltip("Delay between consecutive cards in the stagger.")]
        public float drawStagger       = 0.06f;
        [Tooltip("Duration of the alpha fade at the start of a draw.")]
        public float drawAlphaRise     = 0.08f;

        [Header("Merge Punch")]
        [Tooltip("Total duration of the scale punch.")]
        public float mergeDuration      = 0.20f;
        [Tooltip("Scale peak at the start of the punch.")]
        public float mergeScalePeak     = 1.35f;
        [Tooltip("Scale dip after the peak.")]
        public float mergeScaleDip      = 0.90f;
        [Tooltip("Time (within mergeDuration) to reach peak scale.")]
        public float mergePhase1End     = 0.08f;
        [Tooltip("Time (within mergeDuration) at which dip is complete.")]
        public float mergePhase2End     = 0.14f;
        [Tooltip("Duration of the white flash at merge start.")]
        public float mergeFlashDuration = 0.05f;
        [Tooltip("Gap between a card finishing its draw animation and its merge punch starting.")]
        public float mergeAfterDrawGap  = 0.05f;
        [Tooltip("Gap between a card finishing its slide and its merge punch starting.")]
        public float mergeAfterSlideGap = 0.03f;

        [Header("Slide")]
        [Tooltip("Duration of the card reposition slide.")]
        public float slideDuration     = 0.22f;
        [Tooltip("Minimum world-space distance (pixels) required to trigger a slide.")]
        public float slideMinDistance  = 2f;
    }
}
