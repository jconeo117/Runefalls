using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Instantiates _canvasPrefab at runtime and plays a 3-beat animated victory screen.
    ///
    /// Prefab hierarchy expected:
    ///   VictoryCanvas (Canvas + CanvasScaler + GraphicRaycaster)
    ///     Scrim      — full-screen dark overlay (direct child of canvas)
    ///     Container  — AspectRatioFitter keeps shield at correct ratio on any resolution
    ///       Fondo    — fills Container 100%
    ///       Texto    — percentage anchors within Container
    ///       Boton    — percentage anchors within Container (+ Button component)
    ///
    /// Edit Assets/_Project/Prefabs/UI/VictoryCanvas.prefab to adjust layout.
    /// </summary>
    public class VictorySequencer : MonoBehaviour
    {
        [Header("Canvas")]
        [Tooltip("Prefab with the VictoryCanvas hierarchy. Edit this prefab to change layout.")]
        [SerializeField] private GameObject _canvasPrefab;

        [Header("Timing — sequential: fondo → texto → botón")]
        [SerializeField] private float _fondoDuration  = 0.70f;
        [SerializeField] private float _textDelay      = 0.85f;
        [SerializeField] private float _textDuration   = 0.55f;
        [SerializeField] private float _buttonDelay    = 1.60f;
        [SerializeField] private float _buttonDuration = 0.45f;
        [SerializeField] private float _breathePeriod  = 3.00f;
        [SerializeField] private float _breatheScale   = 0.015f;

        [Header("Scrim")]
        [SerializeField] private float _scrimAlpha = 0.78f;

        // Runtime refs — populated from instantiated prefab
        private Canvas        _canvas;
        private RectTransform _containerRT;   // whole shield — animated for scale/breathe
        private RectTransform _fondoRT;
        private RectTransform _scrimRT;
        private RectTransform _textoRT;
        private RectTransform _botonRT;
        private Image         _fondoImg;
        private Image         _scrimImg;
        private Image         _textoImg;
        private Image         _botonImg;

        // Resting transforms captured from prefab before any animation
        private Vector2 _textoRestPos;
        private Vector2 _botonRestPos;
        private Vector3 _containerRestScale;
        private Vector3 _textoRestScale;

        private Action    _onContinue;
        private Coroutine _breatheRoutine;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_canvasPrefab != null)
            {
                var instance = Instantiate(_canvasPrefab, transform);
                WireRefsFromInstance(instance);
            }
            else
            {
                Debug.LogWarning("[VictorySequencer] _canvasPrefab not assigned.", this);
            }

            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        private void WireRefsFromInstance(GameObject instance)
        {
            _canvas      = instance.GetComponent<Canvas>();
            _scrimRT     = instance.transform.Find("Scrim")?.GetComponent<RectTransform>();
            _scrimImg    = _scrimRT?.GetComponent<Image>();
            _containerRT = instance.transform.Find("Container")?.GetComponent<RectTransform>();
            _fondoRT     = instance.transform.Find("Container/Fondo")?.GetComponent<RectTransform>();
            _fondoImg    = _fondoRT?.GetComponent<Image>();
            _textoRT     = instance.transform.Find("Container/Texto")?.GetComponent<RectTransform>();
            _textoImg    = _textoRT?.GetComponent<Image>();
            _botonRT     = instance.transform.Find("Container/Boton")?.GetComponent<RectTransform>();
            _botonImg    = _botonRT?.GetComponent<Image>();

            var btn = _botonRT?.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnContinue);
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void Play(Action onContinue)
        {
            if (_canvas == null)
            {
                Debug.LogError("[VictorySequencer] Canvas not ready — assign _canvasPrefab.", this);
                onContinue?.Invoke();
                return;
            }

            _onContinue        = onContinue;
            _textoRestPos      = _textoRT     != null ? _textoRT.anchoredPosition     : Vector2.zero;
            _botonRestPos      = _botonRT     != null ? _botonRT.anchoredPosition     : Vector2.zero;
            _containerRestScale = _containerRT != null ? _containerRT.localScale      : Vector3.one;
            _textoRestScale    = _textoRT     != null ? _textoRT.localScale           : Vector3.one;

            _canvas.gameObject.SetActive(true);
            StartCoroutine(Sequence());
        }

        public void Hide()
        {
            if (_breatheRoutine != null) { StopCoroutine(_breatheRoutine); _breatheRoutine = null; }
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        // ── Sequence ──────────────────────────────────────────────────────────────

        private IEnumerator Sequence()
        {
            SetAlpha(_fondoImg, 0f);
            SetAlpha(_scrimImg, 0f);
            SetAlpha(_textoImg, 0f);
            SetAlpha(_botonImg, 0f);
            if (_containerRT != null) _containerRT.localScale = _containerRestScale * 1.08f;

            yield return StartCoroutine(AnimateFondo());

            StartCoroutine(AnimateTexto(_textDelay - _fondoDuration));
            StartCoroutine(AnimateButton(_buttonDelay - _fondoDuration));

            yield return new WaitForSecondsRealtime(_buttonDelay - _fondoDuration + _buttonDuration + 0.05f);
            _breatheRoutine = StartCoroutine(BreatheLoop());
        }

        private IEnumerator AnimateFondo()
        {
            float t = 0f;
            while (t < _fondoDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = EaseOut(Mathf.Clamp01(t / _fondoDuration));
                SetAlpha(_fondoImg, p);
                SetAlpha(_scrimImg, p * _scrimAlpha);
                if (_containerRT != null)
                    _containerRT.localScale = Vector3.LerpUnclamped(_containerRestScale * 1.08f, _containerRestScale, p);
                yield return null;
            }
            SetAlpha(_fondoImg, 1f);
            SetAlpha(_scrimImg, _scrimAlpha);
            if (_containerRT != null) _containerRT.localScale = _containerRestScale;
        }

        private IEnumerator AnimateTexto(float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (_textoRT == null) yield break;

            var startPos   = _textoRestPos + new Vector2(0f, 20f);
            var scaleStart = _textoRestScale * 1.30f;
            _textoRT.anchoredPosition = startPos;
            _textoRT.localScale       = scaleStart;

            float t = 0f;
            while (t < _textDuration)
            {
                t += Time.unscaledDeltaTime;
                float norm = Mathf.Clamp01(t / _textDuration);
                SetAlpha(_textoImg, Mathf.Min(1f, norm * 3f));
                _textoRT.localScale       = Vector3.LerpUnclamped(scaleStart, _textoRestScale, EaseOutBack(norm));
                _textoRT.anchoredPosition = Vector2.Lerp(startPos, _textoRestPos, EaseOut(norm));
                yield return null;
            }
            SetAlpha(_textoImg, 1f);
            _textoRT.localScale       = _textoRestScale;
            _textoRT.anchoredPosition = _textoRestPos;
        }

        private IEnumerator AnimateButton(float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (_botonRT == null) yield break;

            var startPos = _botonRestPos + new Vector2(0f, -15f);
            _botonRT.anchoredPosition = startPos;

            float t = 0f;
            while (t < _buttonDuration)
            {
                t += Time.unscaledDeltaTime;
                float norm = Mathf.Clamp01(t / _buttonDuration);
                SetAlpha(_botonImg, Mathf.Min(1f, norm * 2f));
                _botonRT.anchoredPosition = Vector2.Lerp(startPos, _botonRestPos, EaseOut(norm));
                yield return null;
            }
            SetAlpha(_botonImg, 1f);
            _botonRT.anchoredPosition = _botonRestPos;
        }

        private IEnumerator BreatheLoop()
        {
            float t = 0f;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                float s = 1f + _breatheScale * Mathf.Sin(t * (2f * Mathf.PI / _breathePeriod));
                if (_containerRT != null) _containerRT.localScale = _containerRestScale * s;
                yield return null;
            }
        }

        private void OnContinue()
        {
            Hide();
            _onContinue?.Invoke();
        }

        // ── Easing ────────────────────────────────────────────────────────────────

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private static void SetAlpha(Graphic g, float a)
        {
            if (g == null) return;
            var c = g.color; c.a = a; g.color = c;
        }
    }
}
