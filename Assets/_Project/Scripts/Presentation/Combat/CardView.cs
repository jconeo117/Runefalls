using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Runefall.Combat;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Displays a single card and executes animations on command.
    /// No business logic — display and motion only.
    /// Animation timing/values come from CardAnimationConfig passed by the caller.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Frame")]
        public Image  rankFrame;
        public Sprite rankSprite1;
        public Sprite rankSprite2;
        public Sprite rankSprite3;

        [Header("Content")]
        public Image artBackground;
        public Text  artLabelText;
        public Text  skillNameText;

        public int HandIndex;
        public System.Action<CardView> OnReorderRequested;

        private RectTransform _rt;
        private Canvas        _canvas;
        private CanvasGroup   _cg;
        private Transform     _originParent;
        private int           _originSibling;
        private bool          _isDragging;

        void Awake()
        {
            _rt     = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            _cg     = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
        }

        // ── Display ───────────────────────────────────────────────────────────

        public void Setup(BattleCard card, Color elementColor)
        {
            bool isUlt = card.IsUltimate;

            if (rankFrame != null)
                rankFrame.sprite = card.Rank switch
                {
                    1 => rankSprite1,
                    2 => rankSprite2,
                    _ => rankSprite3
                };

            if (artBackground != null)
                artBackground.color = isUlt ? new Color(1f, 0.82f, 0f) : elementColor;

            if (artLabelText != null)
                artLabelText.text = isUlt ? "✶" : ElementInitial(elementColor);

            if (skillNameText != null)
                skillNameText.text = isUlt
                    ? (card.Ultimate?.ultimateName ?? "ULTIMATE")
                    : (card.Skill?.skillName.Replace("_", " ") ?? "?");
        }

        // ── Animation API ─────────────────────────────────────────────────────

        /// <summary>Stop all running animation coroutines and reset visual state to idle.</summary>
        public void StopAllAnimations()
        {
            StopAllCoroutines();
            transform.localScale = Vector3.one;
            if (_cg != null) _cg.alpha = 1f;
        }

        public void PlayDrawAnimation(float delay, CardAnimationConfig cfg)
            => StartCoroutine(AnimateIn(delay, cfg));

        public void PlayMergeAnimation(Color elementColor, float delay, CardAnimationConfig cfg)
            => StartCoroutine(AnimateMerge(elementColor, delay, cfg));

        public void PlaySlideAnimation(Vector3 worldFrom, CardAnimationConfig cfg)
            => StartCoroutine(AnimateSlide(worldFrom, cfg));

        /// <summary>
        /// Ghost-merge animation: this card is a temporary copy of the consumed source card.
        /// Slides from its current world position to <paramref name="mergeTarget"/>'s position,
        /// triggers the merge punch on the target on arrival, then destroys itself.
        /// </summary>
        public void PlayGhostMerge(CardView mergeTarget, Color elementColor, CardAnimationConfig cfg)
            => StartCoroutine(AnimateGhostMerge(mergeTarget, elementColor, cfg));

        // ── Coroutines ────────────────────────────────────────────────────────

        private IEnumerator AnimateIn(float delay, CardAnimationConfig cfg)
        {
            Vector3 worldTarget = transform.position;
            Vector3 worldStart  = new Vector3(worldTarget.x - Screen.width, worldTarget.y, worldTarget.z);

            _cg.alpha          = 0f;
            transform.position = worldStart;

            if (delay > 0f) yield return new WaitForSeconds(delay);

            float t = 0f;
            while (t < cfg.drawDuration)
            {
                t += Time.deltaTime;
                float norm         = Mathf.Clamp01(t / cfg.drawDuration);
                transform.position = Vector3.LerpUnclamped(worldStart, worldTarget, EaseOutBack(norm));
                _cg.alpha          = Mathf.Clamp01(t / cfg.drawAlphaRise);
                yield return null;
            }

            transform.position = worldTarget;
            _cg.alpha          = 1f;
        }

        private IEnumerator AnimateMerge(Color elementColor, float delay, CardAnimationConfig cfg)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            Color baseColor = artBackground != null ? artBackground.color : elementColor;
            float elapsed   = 0f;

            while (elapsed < cfg.mergeDuration)
            {
                elapsed += Time.deltaTime;

                float scale;
                if (elapsed < cfg.mergePhase1End)
                    scale = Mathf.LerpUnclamped(1f, cfg.mergeScalePeak,
                                EaseOutQuad(elapsed / cfg.mergePhase1End));
                else if (elapsed < cfg.mergePhase2End)
                    scale = Mathf.LerpUnclamped(cfg.mergeScalePeak, cfg.mergeScaleDip,
                                EaseInQuad((elapsed - cfg.mergePhase1End) /
                                           (cfg.mergePhase2End - cfg.mergePhase1End)));
                else
                    scale = Mathf.LerpUnclamped(cfg.mergeScaleDip, 1f,
                                EaseOutQuad((elapsed - cfg.mergePhase2End) /
                                            (cfg.mergeDuration  - cfg.mergePhase2End)));

                transform.localScale = Vector3.one * scale;

                if (artBackground != null)
                    artBackground.color = elapsed < cfg.mergeFlashDuration
                        ? Color.Lerp(baseColor, Color.white,
                              elapsed / cfg.mergeFlashDuration)
                        : Color.Lerp(Color.white, elementColor,
                              (elapsed - cfg.mergeFlashDuration) /
                              (cfg.mergeDuration - cfg.mergeFlashDuration));

                yield return null;
            }

            transform.localScale = Vector3.one;
            if (artBackground != null) artBackground.color = elementColor;
        }

        private IEnumerator AnimateGhostMerge(CardView mergeTarget, Color elementColor, CardAnimationConfig cfg)
        {
            Vector3 worldFrom   = transform.position;
            Vector3 worldTarget = mergeTarget != null ? mergeTarget.transform.position : worldFrom;

            float t = 0f;
            while (t < cfg.slideDuration)
            {
                t += Time.deltaTime;
                float norm         = Mathf.Clamp01(t / cfg.slideDuration);
                transform.position = Vector3.LerpUnclamped(worldFrom, worldTarget, EaseOutQuad(norm));
                if (_cg != null) _cg.alpha = 1f - norm * 0.5f; // subtle fade toward target
                yield return null;
            }

            if (mergeTarget != null)
                mergeTarget.PlayMergeAnimation(elementColor, 0f, cfg);

            Destroy(gameObject);
        }

        private IEnumerator AnimateSlide(Vector3 worldFrom, CardAnimationConfig cfg)
        {
            Vector3 worldTarget = transform.position;
            transform.position  = worldFrom;

            float t = 0f;
            while (t < cfg.slideDuration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.LerpUnclamped(worldFrom, worldTarget,
                    EaseOutQuad(Mathf.Clamp01(t / cfg.slideDuration)));
                yield return null;
            }

            transform.position = worldTarget;
        }

        // ── Easing ────────────────────────────────────────────────────────────

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseInQuad(float t)  => t * t;

        // ── Drag ─────────────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!GetComponent<Button>().interactable) return;
            _isDragging    = true;
            _originParent  = _rt.parent;
            _originSibling = _rt.GetSiblingIndex();
            if (_canvas != null) _rt.SetParent(_canvas.transform, true);
            _cg.blocksRaycasts = false;
            _cg.alpha          = 0.80f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            float scale = (_canvas != null && _canvas.scaleFactor > 0f) ? _canvas.scaleFactor : 1f;
            _rt.anchoredPosition += eventData.delta / scale;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            _isDragging        = false;
            _cg.blocksRaycasts = true;
            _cg.alpha          = 1f;
            _rt.SetParent(_originParent, true);
            _rt.SetSiblingIndex(_originSibling);
            OnReorderRequested?.Invoke(this);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string ElementInitial(Color c)
        {
            if (c.r > 0.6f && c.g < 0.4f && c.b < 0.3f) return "F";
            if (c.b > 0.5f && c.r < 0.4f)               return "I";
            if (c.r > 0.3f && c.b > 0.3f && c.g < 0.3f) return "S";
            if (c.r > 0.7f && c.g > 0.7f && c.b < 0.5f) return "L";
            if (c.g > 0.4f && c.r < 0.4f && c.b < 0.3f) return "E";
            return "N";
        }
    }
}
