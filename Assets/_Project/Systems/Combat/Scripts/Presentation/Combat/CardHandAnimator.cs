using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Owns every card-hand animation: a used card sliding up, a consumed card
    /// merging into its survivor, and the sequential draw-in (with cascading
    /// rank-up merges). The presenter decides <em>what</em> the hand contains;
    /// this decides <em>how</em> the cards move.
    ///
    /// State the presenter and this animator share (live card order, which cards
    /// are mid-draw, where a slot sits on screen) is passed in by reference so the
    /// motion stays in lock-step with the presenter's layout pass.
    /// </summary>
    public sealed class CardHandAnimator
    {
        private readonly MonoBehaviour            _runner;
        private readonly Transform                _container;
        private readonly CardView                 _prefab;
        private readonly CardAnimationConfig      _config;
        private readonly float                    _cardScale;
        private readonly List<CardView>           _cardViews;          // shared ref — live left-to-right order
        private readonly Func<int, int, float, Vector3> _cardTargetLocalPos; // (visualIdx, total, cardWidth)
        private readonly Func<CardView, int>      _mergeFinalRank;     // landed rank for a merge target

        private readonly HashSet<Transform>       _drawing = new();    // cards currently animating their draw-in

        public CardHandAnimator(
            MonoBehaviour runner,
            Transform container,
            CardView prefab,
            CardAnimationConfig config,
            float cardScale,
            List<CardView> liveCardViews,
            Func<int, int, float, Vector3> cardTargetLocalPos,
            Func<CardView, int> mergeFinalRank)
        {
            _runner             = runner;
            _container          = container;
            _prefab             = prefab;
            _config             = config;
            _cardScale          = cardScale;
            _cardViews          = liveCardViews;
            _cardTargetLocalPos = cardTargetLocalPos;
            _mergeFinalRank     = mergeFinalRank;
        }

        /// <summary>True while a card is sliding into the hand (presenter skips it in its layout pass).</summary>
        public bool IsAnimatingDraw(Transform t) => t != null && _drawing.Contains(t);

        // ── public triggers ──────────────────────────────────────────────────

        public void PlayMergeSlide(CardView oldCv, CardView targetCv, Color elemColor)
            => _runner.StartCoroutine(MergeSlide(oldCv, targetCv, elemColor));

        public void PlayUsedSlide(CardView oldCv)
            => _runner.StartCoroutine(UsedSlide(oldCv));

        public void PlayDrawSequence(List<(CardView cv, int finalRank, Color elemColor)> cards)
            => _runner.StartCoroutine(DrawSequence(cards));

        // ── coroutines ───────────────────────────────────────────────────────

        private IEnumerator MergeSlide(CardView oldCv, CardView targetCv, Color elemColor)
        {
            if (oldCv == null) yield break;

            var cg = oldCv.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = false;

            float elapsed  = 0f;
            float duration = _config != null ? _config.slideDuration : 0.22f;

            Vector3 startPos = oldCv.transform.localPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float norm = Mathf.Clamp01(elapsed / duration);

                if (oldCv == null) yield break;
                if (targetCv == null)
                {
                    UnityEngine.Object.Destroy(oldCv.gameObject);
                    yield break;
                }

                oldCv.transform.localPosition = Vector3.Lerp(startPos, targetCv.transform.localPosition, norm);
                if (cg != null) cg.alpha = 1f - norm;

                yield return null;
            }

            if (oldCv != null) UnityEngine.Object.Destroy(oldCv.gameObject);

            if (targetCv != null)
            {
                int finalRank = _mergeFinalRank(targetCv);
                targetCv.Setup(targetCv.Card.WithRank(finalRank), elemColor);
                targetCv.PlayRankUpAnimation(elemColor, _config);
            }
        }

        private IEnumerator UsedSlide(CardView oldCv)
        {
            if (oldCv == null) yield break;

            var cg = oldCv.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = false;

            float elapsed  = 0f;
            float duration = 0.25f;

            Vector3 startPos  = oldCv.transform.localPosition;
            Vector3 targetPos = startPos + new Vector3(0f, 300f, 0f); // slide up toward action slots

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float norm = Mathf.Clamp01(elapsed / duration);

                if (oldCv == null) yield break;

                oldCv.transform.localPosition = Vector3.Lerp(startPos, targetPos, norm);
                if (cg != null) cg.alpha = 1f - norm;

                yield return null;
            }

            if (oldCv != null) UnityEngine.Object.Destroy(oldCv.gameObject);
        }

        private IEnumerator DrawSequence(List<(CardView cv, int finalRank, Color elemColor)> cards)
        {
            foreach (var item in cards)
            {
                if (item.cv == null) continue;

                var t = item.cv.transform;
                _drawing.Add(t);

                yield return _runner.StartCoroutine(SingleCardDraw(item.cv, item.finalRank, item.elemColor));

                _drawing.Remove(t);
            }
        }

        private IEnumerator SingleCardDraw(CardView cv, int finalRank, Color elemColor)
        {
            if (cv == null) yield break;

            float elapsed  = 0f;
            float duration = 0.35f; // duration of the slide-in per card

            var cardRt = cv.GetComponent<RectTransform>();
            float cardWidth = cardRt != null ? cardRt.rect.width : 145f;

            // 1. Draw the main card as Rank 1
            int visualIdx = _cardViews.IndexOf(cv);
            if (visualIdx < 0) visualIdx = _cardViews.Count; // fallback

            Vector3 targetPos = _cardTargetLocalPos(visualIdx, _cardViews.Count, cardWidth);
            Vector3 startPos  = new Vector3(targetPos.x - 400f, targetPos.y, 0f); // start 400 units to the left

            cv.transform.localPosition = startPos;
            cv.transform.localScale = cv.RestScale(_cardScale, 0.5f); // start smaller for a nice pop-in effect
            var cg = cv.GetComponent<CanvasGroup>() ?? cv.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float norm = Mathf.Clamp01(elapsed / duration);

                // Ease-out cubic curve
                float t = 1f - Mathf.Pow(1f - norm, 3f);

                if (cv == null) yield break;

                // Re-calculate targetPos dynamically in case other cards shifting
                visualIdx = _cardViews.IndexOf(cv);
                if (visualIdx >= 0)
                {
                    targetPos = _cardTargetLocalPos(visualIdx, _cardViews.Count, cardWidth);
                    startPos = new Vector3(targetPos.x - 400f, targetPos.y, 0f);
                }

                cv.transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                cv.transform.localScale = Vector3.Lerp(cv.RestScale(_cardScale, 0.5f), cv.RestScale(_cardScale), t);
                cg.alpha = norm;

                yield return null;
            }

            if (cv != null)
            {
                visualIdx = _cardViews.IndexOf(cv);
                if (visualIdx >= 0)
                    cv.transform.localPosition = _cardTargetLocalPos(visualIdx, _cardViews.Count, cardWidth);
                cv.transform.localScale = cv.RestScale(_cardScale);
                cg.alpha = 1f;
            }

            // 2. If the final rank is greater than 1, draw temporary cards and merge them sequentially!
            for (int r = 2; r <= finalRank; r++)
            {
                if (cv == null) yield break;

                // Instantiate a temporary card representing the merging card
                var tempCv = UnityEngine.Object.Instantiate(_prefab, _container);
                tempCv.Setup(cv.Card.WithRank(1), elemColor); // starts as Rank 1!

                // Hide initially until slide starts
                var tempCg = tempCv.GetComponent<CanvasGroup>() ?? tempCv.gameObject.AddComponent<CanvasGroup>();
                tempCg.alpha = 0f;
                tempCv.transform.localScale = Vector3.zero;

                float tempElapsed  = 0f;
                float tempDuration = 0.35f;

                Vector3 tempTarget = cv.transform.localPosition;
                Vector3 tempStart  = new Vector3(tempTarget.x - 400f, tempTarget.y, 0f);

                tempCv.transform.localPosition = tempStart;
                tempCv.transform.localScale = tempCv.RestScale(_cardScale, 0.5f);

                while (tempElapsed < tempDuration)
                {
                    tempElapsed += Time.deltaTime;
                    float norm = Mathf.Clamp01(tempElapsed / tempDuration);
                    float t = 1f - Mathf.Pow(1f - norm, 3f);

                    if (tempCv == null) yield break;
                    if (cv == null)
                    {
                        UnityEngine.Object.Destroy(tempCv.gameObject);
                        yield break;
                    }

                    tempTarget = cv.transform.localPosition;
                    tempStart = new Vector3(tempTarget.x - 400f, tempTarget.y, 0f);

                    tempCv.transform.localPosition = Vector3.Lerp(tempStart, tempTarget, t);
                    tempCv.transform.localScale = Vector3.Lerp(tempCv.RestScale(_cardScale, 0.5f), tempCv.RestScale(_cardScale), t);
                    tempCg.alpha = norm;

                    yield return null;
                }

                // Temporary card has landed on the main card!
                if (tempCv != null) UnityEngine.Object.Destroy(tempCv.gameObject);

                if (cv != null)
                {
                    // Upgrade cv to the rank reached so far and play rank-up blink!
                    cv.Setup(cv.Card.WithRank(r), elemColor);
                    cv.PlayRankUpAnimation(elemColor, _config);

                    // Small delay to let the rank-up animation breathe before the next draw
                    yield return new WaitForSeconds(0.12f);
                }
            }
        }
    }
}
