using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Handles visual effects (flashes, post-processing volume weights) and combat UI deactivation at closing.
    /// </summary>
    public class CombatOutroVfxPlayer
    {
        private readonly MonoBehaviour _runner;
        private GameObject _flashOverlayInstance;
        private Volume _volume;

        private Volume VolumeInstance
        {
            get
            {
                if (_volume == null)
                {
                    _volume = UnityEngine.Object.FindFirstObjectByType<Volume>();
                    if (_volume == null)
                    {
                        #pragma warning disable CS0618
                        _volume = UnityEngine.Object.FindObjectOfType<Volume>();
                        #pragma warning restore CS0618
                    }
                }
                return _volume;
            }
        }

        public GameObject FlashOverlayInstance => _flashOverlayInstance;

        public CombatOutroVfxPlayer(MonoBehaviour runner)
        {
            _runner = runner;
        }

        public void CreateFlashOverlay()
        {
            CleanupFlash();

            _flashOverlayInstance = new GameObject("VictoryOutro_FlashCanvas");
            _flashOverlayInstance.transform.SetParent(_runner.transform, false);

            var canvas = _flashOverlayInstance.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 998; // Under the win screen canvas (999)

            var scaler = _flashOverlayInstance.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var flashGO = new GameObject("FlashOverlay");
            flashGO.transform.SetParent(_flashOverlayInstance.transform, false);
            var rt = flashGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = flashGO.AddComponent<Image>();
            img.color = Color.white;

            flashGO.SetActive(false);
        }

        public void TriggerFlash()
        {
            if (_flashOverlayInstance == null) return;
            var flashGO = _flashOverlayInstance.transform.Find("FlashOverlay")?.gameObject;
            if (flashGO == null) return;

            var img = flashGO.GetComponent<Image>();
            if (img != null) img.color = Color.white;
            flashGO.SetActive(true);
            _runner.StartCoroutine(FlashFadeRoutine(img));
        }

        public void SetPostProcessingWeight(float weight)
        {
            var volume = VolumeInstance;
            if (volume != null)
            {
                volume.weight = Mathf.Clamp01(weight);
            }
        }

        public void HideAllCombatUI(CombatPresenterBase hudPresenter)
        {
            if (hudPresenter != null)
            {
                hudPresenter.HideAllUI();
            }

            // World-space HP bars are separate billboards — deactivate each.
            var bars = UnityEngine.Object.FindObjectsByType<HPBarPresenter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var bar in bars)
            {
                if (bar != null)
                {
                    bar.gameObject.SetActive(false);
                }
            }
        }

        public void CleanupFlash()
        {
            if (_flashOverlayInstance != null)
            {
                UnityEngine.Object.Destroy(_flashOverlayInstance);
                _flashOverlayInstance = null;
            }
        }

        private IEnumerator FlashFadeRoutine(Image img)
        {
            if (img == null) yield break;

            const float dur = 0.45f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float a = 1f - Mathf.Clamp01(t / dur);
                var c = img.color; c.a = a; img.color = c;
                yield return null;
            }
            var cc = img.color; cc.a = 0f; img.color = cc;
            if (img != null && img.gameObject != null)
            {
                img.gameObject.SetActive(false);
            }
        }
    }
}
