using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Screen-space running total of the damage dealt by a single skill (single- or multi-target).
    /// Subscribes to ImpactEvent and sums every hit that lands within a short window; a longer gap
    /// starts a fresh total. Holds the final value, then fades. Self-builds a stylized UI
    /// ("TOTAL" caption + big crimson number with outline + glow + pop) so it only needs ImpactEvent.
    /// </summary>
    public class TotalDamagePresenter : MonoBehaviour
    {
        [SerializeField] private ImpactEvent _impactEvent;

        [Header("Grouping / timing (unscaled seconds)")]
        [Tooltip("Hits within this gap belong to the same skill. A longer gap starts a new total.")]
        [SerializeField] private float _groupWindow  = 0.8f;
        [Tooltip("How long the final total stays fully visible after the last hit.")]
        [SerializeField] private float _holdDuration = 1.1f;
        [SerializeField] private float _fadeDuration = 0.45f;

        [Header("Placement")]
        [Tooltip("Offset from the screen's right-center anchor. +X = left from right edge, +Y = up.")]
        [SerializeField] private Vector2 _anchoredOffset = new Vector2(-90f, 110f);
        [SerializeField] private float _numberFontSize  = 78f;
        [SerializeField] private float _captionFontSize = 30f;

        [Header("Style")]
        [SerializeField] private Color _faceColor    = new Color(0.85f, 0.10f, 0.13f);   // crimson
        [SerializeField] private Color _outlineColor = new Color(1f, 0.95f, 0.85f);      // cream
        [SerializeField] private Color _glowColor    = new Color(0.55f, 0.0f, 0.07f, 1f);// red glow
        [SerializeField] private Color _captionColor = new Color(1f, 0.93f, 0.78f);
        [Range(0f, 0.5f)] [SerializeField] private float _outlineWidth = 0.18f;
        [SerializeField] private float _popScale = 1.22f;
        [SerializeField] private float _popDuration = 0.16f;
        [Tooltip("Fuente TMP para el número y el caption (Playfair Display SDF).")]
        [SerializeField] private TMP_FontAsset _font;

        private CanvasGroup     _group;
        private RectTransform   _container;
        private TextMeshProUGUI _number;
        private TextMeshProUGUI _caption;

        private float _accum;
        private float _lastHitTime;
        private float _popTime = -10f;

        private void Awake()
        {
            BuildUI();
            if (_group != null) _group.alpha = 0f;
        }

        private void OnEnable()  => _impactEvent?.Subscribe(OnImpact);
        private void OnDisable() => _impactEvent?.Unsubscribe(OnImpact);

        private void OnImpact(ImpactContext ctx)
        {
            if (_number == null) return;
            float dmg = ctx.DamageDealt;
            if (dmg <= 0f) return;                 // ignore heals / zero hits

            float now = Time.unscaledTime;
            if (now - _lastHitTime > _groupWindow) _accum = 0f;   // gap → new skill total
            _accum      += dmg;
            _lastHitTime = now;
            _popTime     = now;

            _number.text  = Mathf.RoundToInt(_accum).ToString("N0");
            _group.alpha  = 1f;
        }

        private void Update()
        {
            if (_group == null) return;

            float now   = Time.unscaledTime;
            float since = now - _lastHitTime;

            // Fade after the hold window.
            if (since <= _holdDuration)      _group.alpha = 1f;
            else
            {
                float t = (since - _holdDuration) / Mathf.Max(0.0001f, _fadeDuration);
                _group.alpha = Mathf.Clamp01(1f - t);
            }

            // Pop on each new hit (scale punch on the container).
            float p = Mathf.Clamp01((now - _popTime) / Mathf.Max(0.0001f, _popDuration));
            float s = Mathf.Lerp(_popScale, 1f, p);
            if (_container != null) _container.localScale = new Vector3(s, s, 1f);
        }

        // ── UI construction ─────────────────────────────────────────────────────────

        private void BuildUI()
        {
            var canvasGO = new GameObject("TotalDamageCanvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            _group = canvasGO.AddComponent<CanvasGroup>();
            _group.interactable   = false;
            _group.blocksRaycasts = false;

            // Container (right-center anchored), scaled for the pop.
            var containerGO = new GameObject("Container", typeof(RectTransform));
            containerGO.transform.SetParent(canvasGO.transform, false);
            _container = containerGO.GetComponent<RectTransform>();
            _container.anchorMin        = new Vector2(1f, 0.5f);
            _container.anchorMax        = new Vector2(1f, 0.5f);
            _container.pivot            = new Vector2(1f, 0.5f);
            _container.sizeDelta        = new Vector2(540f, 180f);
            _container.anchoredPosition = _anchoredOffset;

            _caption = MakeLabel("Caption", _captionFontSize, _captionColor,
                                 new Vector2(0f, 56f), new Vector2(540f, 50f));
            _caption.text         = "TOTAL";
            _caption.characterSpacing = 8f;
            StyleOutlineGlow(_caption, 0.12f);

            _number = MakeLabel("Number", _numberFontSize, Color.white,
                                new Vector2(0f, -18f), new Vector2(540f, 130f));
            _number.text = "0";
            StyleFace(_number);
        }

        private TextMeshProUGUI MakeLabel(string name, float size, Color vertexColor,
                                          Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_container, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;     // Playfair Display SDF (set before styling)
            t.alignment           = TextAlignmentOptions.MidlineRight;
            t.fontSize            = size;
            t.fontStyle           = FontStyles.Bold;
            t.color               = vertexColor;
            t.raycastTarget       = false;
            t.enableWordWrapping  = false;

            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPos;
            return t;
        }

        // Big number: crimson face + cream outline + red glow underlay.
        private void StyleFace(TextMeshProUGUI t)
        {
            var m = t.fontMaterial;   // per-instance material
            if (m == null) return;
            m.SetColor(ShaderUtilities.ID_FaceColor,    _faceColor);
            m.SetColor(ShaderUtilities.ID_OutlineColor, _outlineColor);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, _outlineWidth);
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor,    _glowColor);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.55f);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate,   0.30f);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX,  0f);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY,  0f);
        }

        // Caption: just an outline for legibility.
        private void StyleOutlineGlow(TextMeshProUGUI t, float outlineWidth)
        {
            var m = t.fontMaterial;
            if (m == null) return;
            m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.25f, 0.02f, 0.04f));
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
        }
    }
}
