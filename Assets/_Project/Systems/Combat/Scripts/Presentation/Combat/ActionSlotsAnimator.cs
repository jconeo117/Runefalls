using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Animation mechanics for the action-slot strip: a resolved slot fading out and
    /// the survivors rolling left to fill the gap, plus the strip miniaturizing to the
    /// corner during resolution and expanding back on the player's turn.
    ///
    /// Stateless about turn flow — it only moves transforms. The presenter owns the
    /// slot list (shared by reference) and decides when to start these coroutines, so
    /// it can keep the handles it needs to stop them.
    /// </summary>
    public sealed class ActionSlotsAnimator
    {
        private readonly Transform        _container;
        private readonly List<Transform>  _activeSlots;   // shared ref with the presenter
        private readonly float            _miniScale;
        private readonly Vector2          _miniMargin;
        private readonly float            _animDuration;
        private readonly Vector3          _origLocalPos;
        private readonly Vector3          _origScale;

        public ActionSlotsAnimator(
            Transform container,
            List<Transform> activeSlots,
            float miniScale,
            Vector2 miniMargin,
            float animDuration)
        {
            _container    = container;
            _activeSlots  = activeSlots;
            _miniScale    = miniScale;
            _miniMargin   = miniMargin;
            _animDuration = animDuration;
            if (_container != null)
            {
                _origLocalPos = _container.localPosition;
                _origScale    = _container.localScale;
            }
        }

        /// <summary>Fade a resolved slot out, then slide the slots to its right left to fill the gap.</summary>
        public IEnumerator FadeOutSlot(int actionIndex)
        {
            if (actionIndex < 0 || actionIndex >= _activeSlots.Count) yield break;
            var slot = _activeSlots[actionIndex];
            if (slot == null) yield break;

            // Fade
            var cg = slot.GetComponent<CanvasGroup>();
            if (cg == null) { slot.gameObject.SetActive(false); yield break; }
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.2f)
            {
                if (slot == null) yield break;
                cg.alpha = 1f - Mathf.Clamp01(t);
                yield return null;
            }
            if (slot == null) yield break;
            cg.alpha = 0f;

            // Snapshot world positions of slots to the right BEFORE deactivating
            var remaining = new List<(Transform t, Vector3 from)>();
            for (int i = actionIndex + 1; i < _activeSlots.Count; i++)
            {
                var s = _activeSlots[i];
                if (s != null && s.gameObject.activeSelf)
                    remaining.Add((s, s.position));
            }

            slot.gameObject.SetActive(false);

            if (remaining.Count == 0) yield break;

            // Force HLG to compute new positions, then snapshot targets
            var crt = _container as RectTransform;
            if (crt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(crt);

            var to = new Vector3[remaining.Count];
            for (int i = 0; i < remaining.Count; i++)
                to[i] = remaining[i].t.position;

            // Slide: disable HLG, restore old pos, animate, re-enable
            var hlg = _container != null ? _container.GetComponent<HorizontalLayoutGroup>() : null;

            if (hlg != null) hlg.enabled = false;
            for (int i = 0; i < remaining.Count; i++)
                remaining[i].t.position = remaining[i].from;

            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.2f)
            {
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                for (int i = 0; i < remaining.Count; i++)
                {
                    if (remaining[i].t == null) continue;
                    remaining[i].t.position = Vector3.Lerp(remaining[i].from, to[i], s);
                }
                yield return null;
            }
            for (int i = 0; i < remaining.Count; i++)
                if (remaining[i].t != null) remaining[i].t.position = to[i];

            if (hlg != null) hlg.enabled = true;
        }

        /// <summary>Miniaturize the strip and dock it to the bottom-left corner.</summary>
        public IEnumerator Shrink()
        {
            var rt       = _container as RectTransform;
            var parentRt = rt != null ? rt.parent as RectTransform : null;

            Vector3 targetPos;
            if (rt != null && parentRt != null)
            {
                float halfW   = parentRt.rect.width  * 0.5f;
                float halfH   = parentRt.rect.height * 0.5f;
                float scaledW = rt.rect.width  * _miniScale;
                float scaledH = rt.rect.height * _miniScale;
                targetPos = new Vector3(
                    -halfW + _miniMargin.x + scaledW * rt.pivot.x,
                    -halfH + _miniMargin.y + scaledH * rt.pivot.y,
                    0f);
            }
            else
            {
                targetPos = _container.localPosition;
            }

            yield return AnimateTo(targetPos, _origScale * _miniScale);
        }

        /// <summary>Expand the strip back to its authored position and scale.</summary>
        public IEnumerator Expand() => AnimateTo(_origLocalPos, _origScale);

        private IEnumerator AnimateTo(Vector3 targetPos, Vector3 targetScale)
        {
            Vector3 startPos   = _container.localPosition;
            Vector3 startScale = _container.localScale;
            float   elapsed    = 0f;

            while (elapsed < _animDuration)
            {
                elapsed += Time.deltaTime;
                float st = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _animDuration));
                _container.localPosition = Vector3.Lerp(startPos,   targetPos,   st);
                _container.localScale    = Vector3.Lerp(startScale, targetScale, st);
                yield return null;
            }

            _container.localPosition = targetPos;
            _container.localScale    = targetScale;
        }
    }
}
