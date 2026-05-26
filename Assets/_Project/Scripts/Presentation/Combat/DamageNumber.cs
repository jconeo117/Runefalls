using System.Collections;
using UnityEngine;
using TMPro;

namespace Runefall.Presentation.Combat
{
    [RequireComponent(typeof(TextMeshPro))]
    public class DamageNumber : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float _bounceDuration = 0.22f;
        [SerializeField] private float _holdDuration   = 0.88f;
        [SerializeField] private float _fadeDuration   = 0.18f;

        [Header("Bounce")]
        [SerializeField] private float _scalePeak = 1.55f;
        [SerializeField] private float _scaleRest = 1.00f;

        [Header("Float")]
        [SerializeField] private float _floatDistance = 1.8f;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _critColor   = new Color(1f, 0.82f, 0.15f);

        [Header("Crit Label")]
        [Tooltip("Optional child TMP for 'Critical' text shown on crits.")]
        [SerializeField] private TextMeshPro _critLabel;

        private TextMeshPro _tmp;
        private Camera      _cam;

        void Awake()
        {
            _tmp = GetComponent<TextMeshPro>();
            _cam = Camera.main;
        }

        void LateUpdate()
        {
            if (_cam != null)
                transform.forward = _cam.transform.forward;
        }

        public void Show(string value, float fontSize = 8f, bool isCrit = false)
        {
            if (_tmp == null) _tmp = GetComponent<TextMeshPro>();

            _tmp.text     = value;
            _tmp.fontSize = fontSize;
            _tmp.color    = isCrit ? _critColor : _normalColor;
            _tmp.alpha    = 0f;

            if (_critLabel != null)
            {
                _critLabel.gameObject.SetActive(isCrit);
                if (isCrit)
                {
                    _critLabel.color = _critColor;
                    _critLabel.alpha = 0f;
                }
            }

            transform.localScale = Vector3.zero;
            StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            float   total   = _bounceDuration + _holdDuration + _fadeDuration;
            float   elapsed = 0f;
            Vector3 origin  = transform.position;

            while (elapsed < total)
            {
                elapsed += Time.deltaTime;

                float floatT = Mathf.Clamp01(elapsed / (_bounceDuration + _holdDuration));
                transform.position = origin + Vector3.up * (_floatDistance * EaseOutQuad(floatT));

                float alpha;
                float scale;

                if (elapsed < _bounceDuration)
                {
                    float bt = elapsed / _bounceDuration;
                    scale = bt < 0.45f
                        ? Mathf.LerpUnclamped(0f, _scalePeak, EaseOutQuad(bt / 0.45f))
                        : Mathf.LerpUnclamped(_scalePeak, _scaleRest, EaseOutQuad((bt - 0.45f) / 0.55f));
                    alpha = Mathf.Clamp01(bt * 6f);
                    transform.localScale = Vector3.one * scale;
                }
                else
                {
                    transform.localScale = Vector3.one * _scaleRest;
                    float fadeStart = _bounceDuration + _holdDuration;
                    alpha = elapsed >= fadeStart
                        ? 1f - EaseInQuad(Mathf.Clamp01((elapsed - fadeStart) / _fadeDuration))
                        : 1f;
                }

                _tmp.alpha = alpha;
                if (_critLabel != null && _critLabel.gameObject.activeSelf)
                    _critLabel.alpha = alpha;

                yield return null;
            }

            Destroy(gameObject);
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseInQuad(float t)  => t * t;
    }
}
