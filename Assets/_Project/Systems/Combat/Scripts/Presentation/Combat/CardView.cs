using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Runefall.Combat;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Displays a single card and handles drag input.
    /// Position and layout are driven frame-by-frame by the presenter.
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

        public BattleCard Card { get; private set; }
        public bool       IsDragging { get; private set; }
        public int        HandIndex;
        public System.Action<CardView> OnReorderRequested;
        [HideInInspector] public float targetScale = 1f;
        [HideInInspector] public float restScaleY  = 1f;   // absolute Y scale at rest (taller cards)

        private RectTransform _rt;
        private Canvas        _canvas;
        private CanvasGroup   _cg;
        private int           _originSibling;

        // Prefab-authored art rect (the frame's inner window). Cached so skill cards restore it
        // after an ultimate temporarily forced the art to full-bleed.
        private Vector2 _artAnchorMin, _artAnchorMax, _artOffsetMin, _artOffsetMax;

        void Awake()
        {
            _rt     = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            _cg     = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

            if (artBackground != null)
            {
                var a = artBackground.rectTransform;
                _artAnchorMin = a.anchorMin; _artAnchorMax = a.anchorMax;
                _artOffsetMin = a.offsetMin; _artOffsetMax = a.offsetMax;
            }
        }

        // ── Display ───────────────────────────────────────────────────────────

        public void Setup(BattleCard card, Color elementColor)
        {
            Card = card;
            bool isUlt = card.IsUltimate;

            if (rankFrame != null)
            {
                // Ultimates carry their own per-character frame baked into the card art, so the
                // generic gold rank frame is hidden for them.
                rankFrame.enabled = !isUlt;
                if (!isUlt)
                    rankFrame.sprite = card.Rank switch
                    {
                        1 => rankSprite1,
                        2 => rankSprite2,
                        _ => rankSprite3
                    };
            }

            Sprite art = isUlt ? card.Ultimate?.cardArt : card.Skill?.cardArt;
            if (artBackground != null)
            {
                // Ult art is a full card (art + frame) → fill the whole card; skill art sits
                // inside the frame's window.
                ApplyArtRect(fullBleed: isUlt);

                if (art != null)
                {
                    artBackground.sprite = art;
                    artBackground.color = Color.white;
                    if (artLabelText != null)
                        artLabelText.gameObject.SetActive(false);
                }
                else
                {
                    artBackground.sprite = null;
                    artBackground.color = isUlt ? new Color(1f, 0.82f, 0f) : elementColor;
                    if (artLabelText != null)
                    {
                        artLabelText.gameObject.SetActive(true);
                        artLabelText.text = isUlt ? "✶" : ElementInitial(elementColor);
                    }
                }
            }
            else
            {
                if (artLabelText != null)
                {
                    artLabelText.gameObject.SetActive(true);
                    artLabelText.text = isUlt ? "✶" : ElementInitial(elementColor);
                }
            }

            if (skillNameText != null)
                skillNameText.text = isUlt
                    ? (card.Ultimate?.ultimateName ?? "ULTIMATE")
                    : (card.Skill?.skillName.Replace("_", " ") ?? "?");
        }

        // Full-bleed art fills the whole card (ultimate sprite = art + frame); otherwise the art
        // returns to the prefab-authored frame window.
        private void ApplyArtRect(bool fullBleed)
        {
            var a = artBackground.rectTransform;
            if (fullBleed)
            {
                a.anchorMin = Vector2.zero; a.anchorMax = Vector2.one;
                a.offsetMin = Vector2.zero; a.offsetMax = Vector2.zero;
            }
            else
            {
                a.anchorMin = _artAnchorMin; a.anchorMax = _artAnchorMax;
                a.offsetMin = _artOffsetMin; a.offsetMax = _artOffsetMax;
            }
        }

        /// <summary>Scale only the art background image (not the whole card transform). Call after Setup().</summary>
        public void SetArtScale(float x, float y)
        {
            if (artBackground != null)
                artBackground.rectTransform.localScale = new Vector3(x, y, 1f);
        }

        // ── Animation API ─────────────────────────────────────────────────────

        // Resting scale: X/Z uniform, Y stretched independently (restScaleY). factor scales both
        // for pop-in / punch animations.
        public Vector3 RestScale(float uniform, float factor = 1f)
            => new Vector3(uniform * factor, restScaleY * factor, uniform * factor);

        public void StopAllAnimations()
        {
            StopAllCoroutines();
            transform.localScale = RestScale(targetScale);
            if (_cg != null) _cg.alpha = 1f;
        }

        public void PlayRankUpAnimation(Color elementColor, CardAnimationConfig cfg)
        {
            StopAllAnimations();
            StartCoroutine(AnimateRankUp(elementColor, cfg));
        }

        private IEnumerator AnimateRankUp(Color elementColor, CardAnimationConfig cfg)
        {
            Sprite art = Card.IsUltimate ? Card.Ultimate?.cardArt : Card.Skill?.cardArt;
            Color defaultColor = (art != null) ? Color.white : (Card.IsUltimate ? new Color(1f, 0.82f, 0f) : elementColor);
            Color baseColor = artBackground != null ? artBackground.color : defaultColor;
            float elapsed   = 0f;

            while (elapsed < cfg.mergeDuration)
            {
                elapsed += Time.deltaTime;
                float norm = Mathf.Clamp01(elapsed / cfg.mergeDuration);

                // Punch scale: scale up quickly, dip slightly, then settle at 1.0f
                float scale = 1f;
                if (norm < 0.3f)
                    scale = Mathf.Lerp(1f, cfg.mergeScalePeak, norm / 0.3f);
                else if (norm < 0.7f)
                    scale = Mathf.Lerp(cfg.mergeScalePeak, cfg.mergeScaleDip, (norm - 0.3f) / 0.4f);
                else
                    scale = Mathf.Lerp(cfg.mergeScaleDip, 1f, (norm - 0.7f) / 0.3f);

                transform.localScale = RestScale(targetScale, scale);

                // Flash white (blink)
                if (artBackground != null)
                {
                    artBackground.color = norm < 0.2f
                        ? Color.Lerp(baseColor, Color.white, norm / 0.2f)
                        : Color.Lerp(Color.white, defaultColor, (norm - 0.2f) / 0.8f);
                }

                yield return null;
            }

            transform.localScale = RestScale(targetScale);
            if (artBackground != null) artBackground.color = defaultColor;
        }

        // ── Drag ─────────────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!GetComponent<Button>().interactable) return;
            IsDragging     = true;
            _originSibling = _rt.GetSiblingIndex();
            _rt.SetAsLastSibling(); // Make sure it renders on top during dragging
            _cg.blocksRaycasts = false;
            _cg.alpha          = 0.80f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;

            // Project screen coordinate of the drag to the local space of the card hand container
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rt.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
            {
                // We keep Y fixed or let it drag slightly in Y for user freedom,
                // but local Y position should follow the user's drag.
                _rt.localPosition = new Vector3(localPoint.x, localPoint.y, 0f);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;
            IsDragging         = false;
            _cg.blocksRaycasts = true;
            _cg.alpha          = 1f;
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
