using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Full-screen black overlay for exploration→combat transitions.
    /// Attach to any persistent GameObject. Builds its own canvas procedurally.
    /// Call FadeToBlack() before enabling combat, FadeFromBlack() once arena is ready.
    /// </summary>
    public class CombatTransitionScreen : MonoBehaviour
    {
        [Range(0.1f, 2f)] public float fadeOutDuration = 0.60f;
        [Range(0.1f, 2f)] public float fadeInDuration  = 0.45f;

        private CanvasGroup _group;

        private void Awake() => BuildOverlay();

        private void BuildOverlay()
        {
            var canvasGO = new GameObject("CombatTransitionOverlay");
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode    = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder  = 999;
            canvasGO.AddComponent<CanvasScaler>();

            var panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGO.transform, false);
            var rt       = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = Color.black;

            _group               = canvasGO.AddComponent<CanvasGroup>();
            _group.alpha         = 0f;
            _group.blocksRaycasts = false;
            _group.interactable  = false;
        }

        public IEnumerator FadeToBlack()
        {
            _group.blocksRaycasts = true;
            yield return Fade(0f, 1f, fadeOutDuration);
        }

        public IEnumerator FadeFromBlack()
        {
            yield return Fade(1f, 0f, fadeInDuration);
            _group.blocksRaycasts = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t           += Time.deltaTime;
                _group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            _group.alpha = to;
        }
    }
}
