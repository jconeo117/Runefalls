using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Sequences card hand animations: draw, merge punch, slide.
    ///
    /// Responsibilities:
    ///   - Compute per-card animation delays from CardAnimationConfig.
    ///   - Disable HLG/CSF while positional animations are running.
    ///   - Delegate execution to CardView — no visual logic here.
    ///
    /// Does NOT own a MonoBehaviour; CardView.StartCoroutine drives coroutines.
    /// </summary>
    public sealed class CardHandAnimator
    {
        private readonly CardAnimationConfig _cfg;

        public CardHandAnimator(CardAnimationConfig config)
            => _cfg = config != null ? config : ScriptableObject.CreateInstance<CardAnimationConfig>();

        // Stop all animations on a card — call before reparenting or destroying.
        public void CancelCard(CardView cv) => cv?.StopAllAnimations();

        /// <summary>
        /// Execute a complete hand-refresh animation pass.
        /// </summary>
        /// <param name="drawSequence">
        ///   Cards that slide in from off-screen. Index 0 = first to arrive (delay 0).
        ///   Caller builds this ordered list; animator handles stagger timing.
        /// </param>
        /// <param name="mergeTargets">
        ///   Cards that fire a scale punch directly (no ghost). Delay is derived from
        ///   the card's own positional animation finish time.
        /// </param>
        /// <param name="mergeGhosts">
        ///   Ghost-driven merges: each ghost is a temporary card already positioned at
        ///   the consumed source location. It slides to the result card and triggers the
        ///   punch on arrival, then self-destructs.
        /// </param>
        /// <param name="slideFrom">
        ///   Cards that slide from a previous world position to their current one.
        /// </param>
        public void PlayRefresh(
            IReadOnlyList<CardView>                                    drawSequence,
            IReadOnlyList<(CardView cv, Color color)>                  mergeTargets,
            IReadOnlyList<(CardView ghost, CardView result, Color color)> mergeGhosts,
            IReadOnlyDictionary<CardView, Vector3>                     slideFrom,
            HorizontalLayoutGroup hlg,
            ContentSizeFitter     csf)
        {
            bool hasPositional = drawSequence.Count > 0 || slideFrom.Count > 0 || mergeGhosts.Count > 0;
            if (!hasPositional && mergeTargets.Count == 0) return;

            if (hasPositional) SetLayout(hlg, csf, false);

            // Track finish time per card so direct merge punches fire right after positional anim.
            var finishAt = new Dictionary<CardView, float>(
                drawSequence.Count + slideFrom.Count);

            // Draw: staggered slide-in. Index 0 = first to arrive = delay 0.
            for (int k = 0; k < drawSequence.Count; k++)
            {
                float delay = k * _cfg.drawStagger;
                drawSequence[k].PlayDrawAnimation(delay, _cfg);
                finishAt[drawSequence[k]] = delay + _cfg.drawDuration;
            }

            // Slide: all fire immediately, each finishes at slideDuration.
            foreach (var kvp in slideFrom)
            {
                kvp.Key.PlaySlideAnimation(kvp.Value, _cfg);
                finishAt[kvp.Key] = _cfg.slideDuration;
            }

            // Direct merge punches: after each card's own positional animation.
            foreach (var (cv, color) in mergeTargets)
            {
                float mergeDelay = 0f;
                if (finishAt.TryGetValue(cv, out float finishTime))
                {
                    float gap = slideFrom.ContainsKey(cv)
                        ? _cfg.mergeAfterSlideGap
                        : _cfg.mergeAfterDrawGap;
                    mergeDelay = finishTime + gap;
                }
                cv.PlayMergeAnimation(color, mergeDelay, _cfg);
            }

            // Ghost merges: ghost slides from its current position to the result card,
            // triggers the punch on arrival, then self-destructs.
            foreach (var (ghost, result, color) in mergeGhosts)
                ghost.PlayGhostMerge(result, color, _cfg);
        }

        private static void SetLayout(HorizontalLayoutGroup hlg, ContentSizeFitter csf, bool on)
        {
            if (hlg != null) hlg.enabled = on;
            if (csf != null) csf.enabled = on;
        }
    }
}
