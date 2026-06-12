using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Manages the Win/Lose screens UI: instantiation, wiring buttons, and entrance/idle animations.
    /// </summary>
    public class CombatUIOutroPresenter
    {
        private readonly MonoBehaviour _runner;
        private readonly GameObject _winScreenPrefab;
        private readonly GameObject _loseScreenPrefab;

        private readonly float _fondoDuration;
        private readonly float _textDelay;
        private readonly float _textDuration;
        private readonly float _buttonDelay;
        private readonly float _buttonDuration;
        private readonly float _breathePeriod;
        private readonly float _breatheScale;
        private readonly float _scrimAlpha;
        private readonly float _defeatBounceDuration;
        private readonly float _defeatBounceStartScale;

        private GameObject _activeInstance;
        private Coroutine _breatheRoutine;
        private bool _isVictory;
        private bool _winSequencePlayed;   // guards the win entrance animation against double-fire

        // UI references
        private Canvas _canvas;
        private RectTransform _scrimRT;
        private Image _scrimImg;
        private RectTransform _containerRT;
        private RectTransform _fondoRT;
        private Image _fondoImg;
        private RectTransform _textoRT;
        private Image _textoImg;
        private RectTransform _botonContinuarRT;
        private Image _botonContinuarImg;
        private RectTransform _botonReintentarRT;
        private Image _botonReintentarImg;

        // Resting states
        private Vector2 _textoRestPos;
        private Vector2 _botonContinuarRestPos;
        private Vector2 _botonReintentarRestPos;
        private Vector3 _containerRestScale;
        private Vector3 _textoRestScale;

        public GameObject ActiveInstance => _activeInstance;

        public CombatUIOutroPresenter(
            MonoBehaviour runner,
            GameObject winScreenPrefab,
            GameObject loseScreenPrefab,
            float fondoDuration,
            float textDelay,
            float textDuration,
            float buttonDelay,
            float buttonDuration,
            float breathePeriod,
            float breatheScale,
            float scrimAlpha,
            float defeatBounceDuration,
            float defeatBounceStartScale)
        {
            _runner = runner;
            _winScreenPrefab = winScreenPrefab;
            _loseScreenPrefab = loseScreenPrefab;
            _fondoDuration = fondoDuration;
            _textDelay = textDelay;
            _textDuration = textDuration;
            _buttonDelay = buttonDelay;
            _buttonDuration = buttonDuration;
            _breathePeriod = breathePeriod;
            _breatheScale = breatheScale;
            _scrimAlpha = scrimAlpha;
            _defeatBounceDuration = defeatBounceDuration;
            _defeatBounceStartScale = defeatBounceStartScale;
        }

        /// <summary>Instantiates + wires the win screen but leaves it hidden and un-animated, so the
        /// outro timeline can bind to the instance. The actual reveal happens later via ShowWinScreen.</summary>
        public GameObject PrepareWinScreen(Action onContinue)
        {
            _isVictory = true;
            if (_activeInstance == null && _winScreenPrefab != null)
            {
                _activeInstance = UnityEngine.Object.Instantiate(_winScreenPrefab, _runner.transform);
                WireRefsFromInstance(_activeInstance, onContinue, null);
            }
            if (_activeInstance != null) _activeInstance.SetActive(false);
            _winSequencePlayed = false;
            return _activeInstance;
        }

        public void ShowWinScreen(Action onContinue)
        {
            _isVictory = true;
            if (_activeInstance == null)
            {
                if (_winScreenPrefab != null)
                {
                    _activeInstance = UnityEngine.Object.Instantiate(_winScreenPrefab, _runner.transform);
                    WireRefsFromInstance(_activeInstance, onContinue, null);
                }
                else
                {
                    Debug.LogError("[CombatUIOutroPresenter] _winScreenPrefab not assigned!");
                    onContinue?.Invoke();
                    return;
                }
            }

            _activeInstance.SetActive(true);
            // Reveal + animate ONCE. Multiple outro paths (beat, timeline-stopped) call this; the guard
            // stops the entrance animation from re-playing and the screen from appearing to "fire twice".
            if (_winSequencePlayed) return;
            _winSequencePlayed = true;
            _runner.StartCoroutine(VictorySequenceRoutine(onContinue));
        }

        public void ShowLoseScreen(Action onContinue, Action onRetry)
        {
            _isVictory = false;
            if (_loseScreenPrefab != null)
            {
                _activeInstance = UnityEngine.Object.Instantiate(_loseScreenPrefab, _runner.transform);
                WireRefsFromInstance(_activeInstance, onContinue, onRetry);
            }
            else
            {
                Debug.LogError("[CombatUIOutroPresenter] _loseScreenPrefab not assigned!");
                onContinue?.Invoke();
                return;
            }

            _activeInstance.SetActive(true);
            _runner.StartCoroutine(LoseSequenceRoutine());
        }

        private IEnumerator LoseSequenceRoutine()
        {
            // Start hidden + shrunk so the screen bounces in instead of popping from nowhere.
            SetAlpha(_fondoImg, 0f);
            SetAlpha(_scrimImg, 0f);
            SetAlpha(_textoImg, 0f);
            SetAlpha(_botonContinuarImg, 0f);
            SetAlpha(_botonReintentarImg, 0f);
            if (_textoRT != null) _textoRT.anchoredPosition = _textoRestPos;
            if (_botonContinuarRT != null) _botonContinuarRT.anchoredPosition = _botonContinuarRestPos;
            if (_botonReintentarRT != null) _botonReintentarRT.anchoredPosition = _botonReintentarRestPos;
            if (_containerRT != null) _containerRT.localScale = _containerRestScale * _defeatBounceStartScale;

            // Unlock cursor so player can click buttons immediately.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Bounce in: container scales up with an overshoot (EaseOutBack), elements fade in together.
            float dur = Mathf.Max(0.01f, _defeatBounceDuration);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float n = Mathf.Clamp01(t / dur);
                float fade = EaseOut(n);
                if (_containerRT != null)
                    _containerRT.localScale = Vector3.LerpUnclamped(
                        _containerRestScale * _defeatBounceStartScale, _containerRestScale, EaseOutBack(n));
                SetAlpha(_fondoImg, fade);
                SetAlpha(_scrimImg, fade * _scrimAlpha);
                SetAlpha(_textoImg, Mathf.Min(1f, n * 2f));
                SetAlpha(_botonContinuarImg, Mathf.Min(1f, n * 2f));
                SetAlpha(_botonReintentarImg, Mathf.Min(1f, n * 2f));
                yield return null;
            }

            if (_containerRT != null) _containerRT.localScale = _containerRestScale;
            SetAlpha(_fondoImg, 1f);
            SetAlpha(_scrimImg, _scrimAlpha);
            SetAlpha(_textoImg, 1f);
            SetAlpha(_botonContinuarImg, 1f);
            SetAlpha(_botonReintentarImg, 1f);

            // Idle breath while waiting for input.
            if (_breatheRoutine != null) _runner.StopCoroutine(_breatheRoutine);
            _breatheRoutine = _runner.StartCoroutine(BreatheLoop());
        }

        public void Hide()
        {
            if (_breatheRoutine != null)
            {
                _runner.StopCoroutine(_breatheRoutine);
                _breatheRoutine = null;
            }
            if (_activeInstance != null)
            {
                UnityEngine.Object.Destroy(_activeInstance);
                _activeInstance = null;
            }
            _winSequencePlayed = false;
        }

        private void WireRefsFromInstance(GameObject instance, Action onContinue, Action onRetry)
        {
            _canvas = instance.GetComponent<Canvas>();
            _scrimRT = instance.transform.Find("Scrim")?.GetComponent<RectTransform>();
            _scrimImg = _scrimRT?.GetComponent<Image>();
            _containerRT = instance.transform.Find("Container")?.GetComponent<RectTransform>();

            _fondoRT = instance.transform.Find("Container/Fondo")?.GetComponent<RectTransform>();
            _fondoImg = _fondoRT?.GetComponent<Image>();

            string textPath = _isVictory ? "Container/TextoVictoria" : "Container/TextoDerrota";
            _textoRT = instance.transform.Find(textPath)?.GetComponent<RectTransform>();
            _textoImg = _textoRT?.GetComponent<Image>();

            _botonContinuarRT = instance.transform.Find("Container/BotonContinuar")?.GetComponent<RectTransform>();
            _botonContinuarImg = _botonContinuarRT?.GetComponent<Image>();

            _botonReintentarRT = instance.transform.Find("Container/BotonReintentar")?.GetComponent<RectTransform>();
            _botonReintentarImg = _botonReintentarRT?.GetComponent<Image>();

            _textoRestPos = _textoRT != null ? _textoRT.anchoredPosition : Vector2.zero;
            _botonContinuarRestPos = _botonContinuarRT != null ? _botonContinuarRT.anchoredPosition : Vector2.zero;
            _botonReintentarRestPos = _botonReintentarRT != null ? _botonReintentarRT.anchoredPosition : Vector2.zero;

            _containerRestScale = _containerRT != null ? _containerRT.localScale : Vector3.one;
            _textoRestScale = _textoRT != null ? _textoRT.localScale : Vector3.one;

            var btnContinue = _botonContinuarRT?.GetComponent<Button>();
            if (btnContinue != null)
            {
                btnContinue.onClick.RemoveAllListeners();
                btnContinue.onClick.AddListener(() =>
                {
                    Hide();
                    onContinue?.Invoke();
                });
            }

            var btnRetry = _botonReintentarRT?.GetComponent<Button>();
            if (btnRetry != null)
            {
                btnRetry.onClick.RemoveAllListeners();
                btnRetry.onClick.AddListener(() =>
                {
                    Hide();
                    onRetry?.Invoke();
                });
            }
        }

        private IEnumerator VictorySequenceRoutine(Action onContinue)
        {
            // Play light UI pop-up animation
            SetAlpha(_fondoImg, 0f);
            SetAlpha(_scrimImg, 0f);
            SetAlpha(_textoImg, 0f);
            SetAlpha(_botonContinuarImg, 0f);
            if (_containerRT != null) _containerRT.localScale = _containerRestScale * 1.08f;

            yield return _runner.StartCoroutine(AnimateFondo());

            _runner.StartCoroutine(AnimateTexto(_textDelay - _fondoDuration));
            _runner.StartCoroutine(AnimateButton(_buttonDelay - _fondoDuration));

            yield return new WaitForSecondsRealtime(_buttonDelay - _fondoDuration + _buttonDuration + 0.05f);
            _breatheRoutine = _runner.StartCoroutine(BreatheLoop());
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

            var startPos = _textoRestPos + new Vector2(0f, 20f);
            var scaleStart = _textoRestScale * 1.30f;
            _textoRT.anchoredPosition = startPos;
            _textoRT.localScale = scaleStart;

            float t = 0f;
            while (t < _textDuration)
            {
                t += Time.unscaledDeltaTime;
                float norm = Mathf.Clamp01(t / _textDuration);
                SetAlpha(_textoImg, Mathf.Min(1f, norm * 3f));
                _textoRT.localScale = Vector3.LerpUnclamped(scaleStart, _textoRestScale, EaseOutBack(norm));
                _textoRT.anchoredPosition = Vector2.Lerp(startPos, _textoRestPos, EaseOut(norm));
                yield return null;
            }
            SetAlpha(_textoImg, 1f);
            _textoRT.localScale = _textoRestScale;
            _textoRT.anchoredPosition = _textoRestPos;
        }

        private IEnumerator AnimateButton(float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (_botonContinuarRT == null) yield break;

            var startPos = _botonContinuarRestPos + new Vector2(0f, -15f);
            _botonContinuarRT.anchoredPosition = startPos;

            float t = 0f;
            while (t < _buttonDuration)
            {
                t += Time.unscaledDeltaTime;
                float norm = Mathf.Clamp01(t / _buttonDuration);
                SetAlpha(_botonContinuarImg, Mathf.Min(1f, norm * 2f));
                _botonContinuarRT.anchoredPosition = Vector2.Lerp(startPos, _botonContinuarRestPos, EaseOut(norm));
                yield return null;
            }
            SetAlpha(_botonContinuarImg, 1f);
            _botonContinuarRT.anchoredPosition = _botonContinuarRestPos;
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
