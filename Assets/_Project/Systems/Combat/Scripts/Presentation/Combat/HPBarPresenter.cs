using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Runefall.Combat;

namespace Runefall.Presentation.Combat
{
    public class HPBarPresenter : MonoBehaviour
    {
        [Header("Wired in the HP bar prefab")]
        [SerializeField] private RectTransform _fillRT;
        [SerializeField] private RectTransform _secondaryFillRT;
        [SerializeField] private Image         _frameImage;

        private ICombatActor  _actor;
        private float         _displayedHP;
        private Transform     _followTarget;
        private Vector3       _worldOffset;
        private RectTransform _iconRow;
        private CanvasGroup   _canvasGroup;
        private Coroutine     _fadeRoutine;

        private static readonly Color k_AdvantageColor    = new Color(0.25f, 0.55f, 1.00f);
        private static readonly Color k_DisadvantageColor = new Color(1.00f, 0.25f, 0.25f);

        // ── public API ────────────────────────────────────────────────────────────

        public void Bind(ICombatActor actor)
        {
            _actor       = actor;
            _displayedHP = actor.Model.CurrentHP;

            BuildIconRow();
            actor.Effects.OnEffectsChanged += RebuildEffectIcons;
            actor.Effects.OnEffectApplied  += OnEffectApplied;

            SetSecondary(0f);   // ultimate gauge starts empty; fills via OnGaugeChanged
            Refresh();
        }

        /// <summary>Per-character frame sprite (wired Image in the prefab). Null hides it.</summary>
        public void SetFrame(Sprite frame)
        {
            if (_frameImage == null) return;
            _frameImage.sprite  = frame;
            _frameImage.enabled = frame != null;
        }

        public void SetFollow(Transform target, Vector3 worldOffset)
        {
            _followTarget = target;
            _worldOffset  = worldOffset;
        }

        /// <summary>Fade the bar in (used when the gameplay camera resumes after a skill close-up,
        /// so it doesn't pop back from nothing).</summary>
        public void Show(float duration = 0.3f)
        {
            EnsureCanvasGroup();
            gameObject.SetActive(true);
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            _fadeRoutine = StartCoroutine(FadeIn(duration));
        }

        /// <summary>Hide the bar instantly (while the skill camera frames a close-up).</summary>
        public void Hide()
        {
            if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
            gameObject.SetActive(false);
        }

        private void EnsureCanvasGroup()
        {
            if (_canvasGroup != null) return;
            if (!TryGetComponent(out _canvasGroup))
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private IEnumerator FadeIn(float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Clamp01(t / duration);
                yield return null;
            }
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            _fadeRoutine = null;
        }

        /// <summary>Sets the secondary bar fill fraction (0..1). Wire to a shield/charge/etc.</summary>
        public void SetSecondary(float pct)
        {
            if (_secondaryFillRT == null) return;
            var am = _secondaryFillRT.anchorMax;
            am.x = Mathf.Clamp01(pct);   // preserve Y so the bottom-region layout stays intact
            _secondaryFillRT.anchorMax = am;
        }

        public void ApplyVisualDamage(float amount)
        {
            _displayedHP = Mathf.Max(0f, _displayedHP - amount);
            Refresh();
        }

        public void ApplyVisualHeal(float amount)
        {
            _displayedHP = Mathf.Min(_actor.Model.MaxHP, _displayedHP + amount);
            Refresh();
        }

        public void ForceRefresh()
        {
            if (_actor == null) return;
            _displayedHP = _actor.Model.CurrentHP;
            Refresh();
        }

        /// <summary>MP: update bar directly from server-authoritative HP values (no actor needed).</summary>
        public void ForceSetHP(float current, float max)
        {
            _displayedHP = current;
            if (_fillRT == null) return;
            float pct     = max > 0f ? current / max : 0f;
            _fillRT.anchorMax = new Vector2(pct, 1f);
        }

        // ── lifecycle ─────────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            if (_actor == null) return;
            _actor.Effects.OnEffectsChanged -= RebuildEffectIcons;
            _actor.Effects.OnEffectApplied  -= OnEffectApplied;
        }

        private void LateUpdate()
        {
            // No follow target = fixed local transform (set at spawn); leave it alone.
            if (_followTarget == null) return;
            transform.position = _followTarget.position + _worldOffset;
            if (Camera.main != null)
                transform.rotation = Camera.main.transform.rotation;
        }

        // ── icon row ──────────────────────────────────────────────────────────────

        private void BuildIconRow()
        {
            var rowGO = new GameObject("EffectIcons");
            rowGO.transform.SetParent(transform, false);
            _iconRow = rowGO.AddComponent<RectTransform>();
            _iconRow.sizeDelta        = new Vector2(200f, 20f);
            _iconRow.anchoredPosition = new Vector2(0f, 24f); // above 28px HP bar
        }

        private void RebuildEffectIcons()
        {
            if (_iconRow == null) return;

            for (int i = _iconRow.childCount - 1; i >= 0; i--)
                Destroy(_iconRow.GetChild(i).gameObject);

            var effects = _actor.Effects.ActiveEffects;

            int count = 0;
            for (int i = 0; i < effects.Count; i++)
                if (effects[i].Tag == EffectTag.Advantage || effects[i].Tag == EffectTag.Disadvantage)
                    count++;

            if (count == 0) return;

            const float size = 18f;
            const float gap  = 3f;
            float totalW = count * size + (count - 1) * gap;
            float startX = -totalW * 0.5f + size * 0.5f;

            int idx = 0;
            for (int i = 0; i < effects.Count; i++)
            {
                var e = effects[i];
                if (e.Tag != EffectTag.Advantage && e.Tag != EffectTag.Disadvantage) continue;
                Color col = e.Tag == EffectTag.Advantage ? k_AdvantageColor : k_DisadvantageColor;
                SpawnIcon(startX + idx * (size + gap), col, e);
                idx++;
            }
        }

        private void SpawnIcon(float localX, Color tint, ActiveEffect e)
        {
            const float size = 20f;

            var iconGO = new GameObject("EffectIcon");
            iconGO.transform.SetParent(_iconRow, false);
            var iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.sizeDelta        = new Vector2(size, size);
            iconRT.anchoredPosition = new Vector2(localX, 0f);

            var img = iconGO.AddComponent<Image>();
            if (e.Source?.icon != null)
            {
                img.sprite = e.Source.icon;
                img.color  = Color.white;
            }
            else
            {
                // No custom sprite → styled procedural chip: rounded fill + darker outline + initial glyph.
                img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
                img.type   = Image.Type.Sliced;
                img.color  = tint;

                var outline = iconGO.AddComponent<Outline>();
                outline.effectColor    = new Color(tint.r * 0.4f, tint.g * 0.4f, tint.b * 0.4f, 1f);
                outline.effectDistance = new Vector2(1.4f, -1.4f);

                string glyph = !string.IsNullOrEmpty(e.Source?.effectName)
                    ? e.Source.effectName.Substring(0, 1).ToUpperInvariant()
                    : "+";

                var glyphGO = new GameObject("Glyph");
                glyphGO.transform.SetParent(iconGO.transform, false);
                var glyphRT       = glyphGO.AddComponent<RectTransform>();
                glyphRT.anchorMin = Vector2.zero;
                glyphRT.anchorMax = Vector2.one;
                glyphRT.offsetMin = glyphRT.offsetMax = Vector2.zero;

                var sh = glyphGO.AddComponent<Shadow>();
                sh.effectColor    = new Color(0f, 0f, 0f, 0.55f);
                sh.effectDistance = new Vector2(1f, -1f);

                var glyphTxt      = glyphGO.AddComponent<Text>();
                glyphTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                glyphTxt.text      = glyph;
                glyphTxt.fontSize  = 12;
                glyphTxt.fontStyle = FontStyle.Bold;
                glyphTxt.color     = Color.white;
                glyphTxt.alignment = TextAnchor.MiddleCenter;
            }

            // Stack count — bottom-right corner, only when actually stacked
            if (e.Stacks > 1)
            {
                var stackGO = new GameObject("Stacks");
                stackGO.transform.SetParent(iconGO.transform, false);
                var stackRT       = stackGO.AddComponent<RectTransform>();
                stackRT.anchorMin = new Vector2(0.45f, 0f);
                stackRT.anchorMax = new Vector2(1f, 0.55f);
                stackRT.offsetMin = stackRT.offsetMax = Vector2.zero;
                var stackTxt      = stackGO.AddComponent<Text>();
                stackTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                stackTxt.text      = e.Stacks.ToString();
                stackTxt.fontSize  = 9;
                stackTxt.fontStyle = FontStyle.Bold;
                stackTxt.color     = Color.white;
                stackTxt.alignment = TextAnchor.LowerRight;
            }
        }

        // ── effect popup ──────────────────────────────────────────────────────────

        private void OnEffectApplied(ActiveEffect effect)
        {
            if (effect.Source == null) return;
            if (string.IsNullOrEmpty(effect.Source.effectName)) return;
            if (effect.Tag != EffectTag.Advantage && effect.Tag != EffectTag.Disadvantage) return;

            Color col = effect.Tag == EffectTag.Advantage ? k_AdvantageColor : k_DisadvantageColor;
            StartCoroutine(FloatPopup(effect.Source.effectName, col));
        }

        private IEnumerator FloatPopup(string label, Color color)
        {
            var popupGO = new GameObject("EffectPopup");
            popupGO.transform.SetParent(transform, false);

            var rt              = popupGO.AddComponent<RectTransform>();
            rt.sizeDelta        = new Vector2(200f, 28f);
            rt.anchoredPosition = new Vector2(0f, 50f);

            var shadow      = popupGO.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            var txt        = popupGO.AddComponent<Text>();
            txt.font       = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text       = label;
            txt.fontSize   = 15;
            txt.fontStyle  = FontStyle.Bold;
            txt.alignment  = TextAnchor.MiddleCenter;
            txt.color      = color;

            const float duration  = 1.82f;   // 1.4 × 1.3
            const float rise      = 44f;
            const float holdFrac  = 0.40f;   // first 40% — full opacity, no fade
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float pct           = t / duration;
                rt.anchoredPosition = new Vector2(0f, 50f + rise * pct);

                float fadePct = pct < holdFrac ? 0f : (pct - holdFrac) / (1f - holdFrac);
                var c         = txt.color;
                c.a           = 1f - fadePct;
                txt.color     = c;
                yield return null;
            }

            Destroy(popupGO);
        }

        // ── HP bar ────────────────────────────────────────────────────────────────

        private void Refresh()
        {
            if (_fillRT == null || _actor == null) return;
            float pct     = _actor.Model.MaxHP > 0f ? _displayedHP / _actor.Model.MaxHP : 0f;
            _fillRT.anchorMax = new Vector2(pct, 1f);
        }
    }
}
