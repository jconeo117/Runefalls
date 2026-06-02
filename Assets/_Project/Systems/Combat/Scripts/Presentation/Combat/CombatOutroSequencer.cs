using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Programmatic and Cinematic Outro Sequencer for combat outcomes.
    /// Handles victory and defeat states, slow-motion hits, freeze frames,
    /// dynamic sprite/text UI canvas generation, victory poses, camera orbit sweeps,
    /// and clean transition fade-out handoffs to explore or rewards.
    /// </summary>
    public class CombatOutroSequencer : MonoBehaviour
    {
        [Header("Outro Timeline (Optional)")]
        [Tooltip("Optional. PlayableDirector that plays the outro timeline.")]
        public PlayableDirector outroDirector;

        [Header("Post-Processing (Optional)")]
        [Tooltip("Optional. Post-Processing Volume to control dynamically during the outro sequence.")]
        public Volume outroPPVolume;
        [Tooltip("Target weight of the PP volume during the slow-motion visual climax.")]
        [Range(0f, 1f)] public float slowMoPPWeight = 0.8f;
        [Tooltip("Target weight of the PP volume during the victory pose & camera orbit.")]
        [Range(0f, 1f)] public float victoryPPWeight = 1.0f;

        [Header("Sprites (Shared/UI)")]
        [Tooltip("Optional sprite for Victory Text.")]
        public Sprite victoryTextSprite;
        [Tooltip("Optional sprite for Victory Background Shield.")]
        public Sprite victoryBgSprite;
        [Tooltip("Optional sprite for Defeat Text.")]
        public Sprite defeatTextSprite;
        [Tooltip("Optional sprite for Defeat Background Shield.")]
        public Sprite defeatBgSprite;
        [Tooltip("Optional sprite for Continue Button.")]
        public Sprite continueButtonSprite;

        [Header("Timing Configuration")]
        [Tooltip("Duration of the slow-motion death frame impact in seconds.")]
        public float slowMoDuration = 1.2f;
        [Tooltip("Time scale during the slow-motion beat.")]
        public float slowMoTimeScale = 0.3f;
        [Tooltip("Freeze frame total pause duration in seconds.")]
        public float freezeFrameDuration = 0.4f;
        [Tooltip("Safety margin fade out duration in seconds.")]
        public float safetyMargin = 0.8f;

        private Action _onCompleteCallback;
        private GameObject _outroUIInstance;
        private bool _outroFinished;
        private CombatBootstrapper _bootstrapper;
        private bool _won;

        // Captured UI elements for animation
        private RectTransform _scrimRt;
        private RectTransform _fondoRt;
        private RectTransform _textoRt;
        private RectTransform _botonRt;
        
        private Image _scrimImg;
        private Image _fondoImg;
        private Image _textoImg;
        private Image _botonImg;
        
        private CanvasGroup _uiCg;
        private Image _flashOverlay;

        // Captures
        private Vector2 _textoRestPos;
        private Vector2 _botonRestPos;
        private Vector3 _textoRestScale = Vector3.one;

        private float _scrimAlpha = 0.78f;
        private bool _cameraHandedOver = false;

        public void PlayOutro(bool won, Action onComplete)
        {
            _won = won;
            _onCompleteCallback = onComplete;
            _outroFinished = false;
            _cameraHandedOver = false;

            // Locate bootstrapper
            _bootstrapper = FindFirstObjectByType<CombatBootstrapper>();
            if (_bootstrapper == null)
            {
                #pragma warning disable CS0618
                _bootstrapper = FindObjectOfType<CombatBootstrapper>();
                #pragma warning restore CS0618
            }

            StartCoroutine(PlayOutroSequence());
        }

        private IEnumerator PlayOutroSequence()
        {
            // 1. Slow-Motion Death Frame Climax (0.0s – 1.2s)
            if (outroPPVolume != null)
            {
                outroPPVolume.weight = 0f;
                outroPPVolume.enabled = true;
                StartCoroutine(AnimatePPVolumeWeight(slowMoPPWeight, 0.4f, true));
            }

            Time.timeScale = slowMoTimeScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            
            // Wait in real-world seconds so the slow-mo takes exactly slowMoDuration seconds
            yield return new WaitForSecondsRealtime(slowMoDuration);

            // 2. Freeze Frame + White Flash Climax (1.2s – 1.6s)
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;

            // Freeze animators in place
            FreezeAnimators(true);

            // Programmatically build the beautiful Outro Canvas (zero scene dependencies!)
            CreateOutroUI();

            // Detonate screen-clearing white flash overlay
            _flashOverlay.color = Color.white;

            // Wait in real-world seconds during freeze frame
            yield return new WaitForSecondsRealtime(freezeFrameDuration);

            // Unfreeze animators and restore speed
            FreezeAnimators(false);

            // Disable Cinemachine and handoff to exploration camera / snap early under cover of the white flash
            PerformOutroCameraHandoff();

            // 3. Victory Pose & Camera Orbit Fallback (1.6s – 3.2s)
            if (outroPPVolume != null)
            {
                StartCoroutine(AnimatePPVolumeWeight(victoryPPWeight, 1.0f, true));
            }

            if (_won)
            {
                TriggerPlayerVictoryPose();
            }

            // Hide active combat HUD (cards, round count, player stats) to clean the presentation
            var hud = FindFirstObjectByType<CombatPresenterBase>();
            if (hud == null)
            {
                #pragma warning disable CS0618
                hud = FindObjectOfType<CombatPresenterBase>();
                #pragma warning restore CS0618
            }
            if (hud != null)
            {
                hud.HideAllUI();
            }

            // Start timeline director if available, otherwise run dynamic camera orbit around hero
            Coroutine cameraOrbitRoutine = null;
            if (outroDirector != null && outroDirector.playableAsset != null)
            {
                outroDirector.time = 0;
                outroDirector.Evaluate();
                outroDirector.Play();
            }
            else
            {
                cameraOrbitRoutine = StartCoroutine(ProgrammaticCameraOrbitRoutine());
            }

            // 4. Entrance of VICTORIA / DERROTA Shield & Banner text (2.8s – 3.8s)
            // Parallel coroutines to animate background fade, text scale-in and button slide-up
            StartCoroutine(AnimateFondoFade());
            StartCoroutine(AnimateTextoElasticPop());
            StartCoroutine(AnimateBotonSlideUp());

            // Wait until continue button is pressed or hold completes
            yield return new WaitUntil(() => _outroFinished);

            // Stop camera orbit if running
            if (cameraOrbitRoutine != null)
            {
                StopCoroutine(cameraOrbitRoutine);
            }

            // 5. Outro Transition Fade-Out to negro (3.8s – 5.0s)
            if (outroPPVolume != null)
            {
                StartCoroutine(AnimatePPVolumeWeight(0f, 0.5f, true));
            }

            yield return StartCoroutine(FadeOutToBlackRoutine());

            // Outro complete! Call GameManager handoff
            if (_outroUIInstance != null)
            {
                Destroy(_outroUIInstance);
                _outroUIInstance = null;
            }

            _onCompleteCallback?.Invoke();
        }

        private void FreezeAnimators(bool freeze)
        {
            if (_bootstrapper == null) return;
            var anims = _bootstrapper.GetComponentsInChildren<Animator>();
            foreach (var a in anims)
            {
                if (a != null)
                {
                    a.speed = freeze ? 0f : 1f;
                }
            }
        }

        private void TriggerPlayerVictoryPose()
        {
            if (_bootstrapper == null || _bootstrapper.arenaAssembler == null) return;
            
            // Find player character animators
            foreach (var slot in _bootstrapper.arenaAssembler.PlayerSlots)
            {
                var anim = slot.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    // Cross fade into victory state smoothly
                    anim.CrossFadeInFixedTime("Victory", 0.2f);
                }
            }
        }

        private void PerformOutroCameraHandoff()
        {
            if (_cameraHandedOver) return;
            _cameraHandedOver = true;

            // A. Disable CinemachineBrain so the manual camera controller can drive the camera
            var brain = Camera.main != null ? Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
            if (brain != null)
            {
                brain.enabled = false;
            }

            // B. Disable exploration camera references if active
            if (_bootstrapper != null && _bootstrapper.cameraController != null)
            {
                _bootstrapper.cameraController.enabled = false;
            }
        }

        private IEnumerator ProgrammaticCameraOrbitRoutine()
        {
            Vector3 focusPoint = Vector3.zero;
            
            // Determine player team center for camera focus
            if (_bootstrapper != null && _bootstrapper.arenaAssembler != null && _bootstrapper.arenaAssembler.IsReady)
            {
                focusPoint = _bootstrapper.arenaAssembler.FieldCenter;
                
                // Focus slightly on player side center
                if (_bootstrapper.arenaAssembler.PlayerSlots != null && _bootstrapper.arenaAssembler.PlayerSlots.Count > 0)
                {
                    var slot = _bootstrapper.arenaAssembler.PlayerSlots[0];
                    if (slot != null) focusPoint = slot.position;
                }
            }

            Vector3 offset = Camera.main.transform.position - focusPoint;
            float radius = offset.magnitude;
            float angle = Mathf.Atan2(offset.z, offset.x);
            float speed = 0.15f; // Slow dramatic camera rotation

            while (true)
            {
                angle += Time.deltaTime * speed;
                Vector3 targetPos = focusPoint + new Vector3(Mathf.Cos(angle) * radius, offset.y * 0.95f, Mathf.Sin(angle) * radius);
                
                Camera.main.transform.position = targetPos;
                Camera.main.transform.LookAt(focusPoint + Vector3.up * 1f);
                yield return null;
            }
        }

        private void CreateOutroUI()
        {
            if (_outroUIInstance != null) Destroy(_outroUIInstance);

            // Create Canvas
            _outroUIInstance = new GameObject("CombatOutro_CinematicUI");
            var canvas = _outroUIInstance.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            
            var scaler = _outroUIInstance.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            _outroUIInstance.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            _uiCg = _outroUIInstance.AddComponent<CanvasGroup>();

            // Scrim (dark screen overlay)
            var scrimGO = new GameObject("Scrim");
            scrimGO.transform.SetParent(_outroUIInstance.transform, false);
            _scrimRt = scrimGO.AddComponent<RectTransform>();
            _scrimRt.anchorMin = Vector2.zero;
            _scrimRt.anchorMax = Vector2.one;
            _scrimRt.offsetMin = _scrimRt.offsetMax = Vector2.zero;
            _scrimImg = scrimGO.AddComponent<UnityEngine.UI.Image>();
            _scrimImg.color = new Color(0f, 0f, 0f, 0f); // Starts transparent

            // Container for layout
            var containerGO = new GameObject("Container");
            containerGO.transform.SetParent(_outroUIInstance.transform, false);
            var containerRt = containerGO.AddComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(0.5f, 0.5f);
            containerRt.anchorMax = new Vector2(0.5f, 0.5f);
            containerRt.pivot = new Vector2(0.5f, 0.5f);
            containerRt.anchoredPosition = Vector2.zero;
            containerRt.sizeDelta = new Vector2(1920f, 1080f);

            // Background shield image
            var fondoGO = new GameObject("Fondo");
            fondoGO.transform.SetParent(containerGO.transform, false);
            _fondoRt = fondoGO.AddComponent<RectTransform>();
            _fondoRt.anchorMin = new Vector2(0.5f, 0.5f);
            _fondoRt.anchorMax = new Vector2(0.5f, 0.5f);
            _fondoRt.pivot = new Vector2(0.5f, 0.5f);
            _fondoRt.anchoredPosition = new Vector2(0f, 60f);
            _fondoRt.sizeDelta = new Vector2(750f, 500f);
            _fondoImg = fondoGO.AddComponent<UnityEngine.UI.Image>();
            _fondoImg.color = new Color(1f, 1f, 1f, 0f);

            Sprite currentBgSprite = _won ? victoryBgSprite : defeatBgSprite;
            if (currentBgSprite != null)
            {
                _fondoImg.sprite = currentBgSprite;
            }
            else
            {
                // Fallback beautifully stylized background panel
                _fondoImg.color = _won 
                    ? new Color(0.06f, 0.20f, 0.12f, 0.0f) // Soft emerald dark green
                    : new Color(0.20f, 0.04f, 0.06f, 0.0f); // Soft obsidian crimson
                
                var outline = fondoGO.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = _won ? new Color(0.2f, 1f, 0.4f, 0.8f) : new Color(1f, 0.2f, 0.2f, 0.8f);
                outline.effectDistance = new Vector2(4f, -4f);
            }

            // Outcome Text (VICTORIA / DERROTA)
            var textoGO = new GameObject("Texto");
            textoGO.transform.SetParent(containerGO.transform, false);
            _textoRt = textoGO.AddComponent<RectTransform>();
            _textoRt.anchorMin = new Vector2(0.5f, 0.5f);
            _textoRt.anchorMax = new Vector2(0.5f, 0.5f);
            _textoRt.pivot = new Vector2(0.5f, 0.5f);
            _textoRestPos = new Vector2(0f, 120f);
            _textoRt.anchoredPosition = _textoRestPos + new Vector2(0f, 50f); // Starts higher up
            _textoRt.localScale = Vector3.one * 1.5f; // Starts scaled up

            Sprite currentTextSprite = _won ? victoryTextSprite : defeatTextSprite;
            if (currentTextSprite != null)
            {
                _textoImg = textoGO.AddComponent<UnityEngine.UI.Image>();
                _textoImg.sprite = currentTextSprite;
                _textoImg.color = new Color(1f, 1f, 1f, 0f);
                _textoRt.sizeDelta = new Vector2(600f, 180f);
            }
            else
            {
                // Fallback metallic gold / red programmatic text
                _textoRt.sizeDelta = new Vector2(700f, 200f);
                var txt = textoGO.AddComponent<UnityEngine.UI.Text>();
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") 
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                txt.fontSize = 120;
                txt.fontStyle = FontStyle.BoldAndItalic;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                txt.text = _won ? "VICTORIA" : "DERROTA";
                
                // Neon glow outline and drop shadow
                txt.color = _won ? new Color(1.0f, 0.82f, 0.15f, 0.0f) : new Color(0.9f, 0.1f, 0.15f, 0.0f);
                
                var outline = textoGO.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
                outline.effectDistance = new Vector2(5f, -5f);

                var shadow = textoGO.AddComponent<UnityEngine.UI.Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
                shadow.effectDistance = new Vector2(8f, -8f);

                _textoImg = null; // Mark as text-only fallback
            }

            // Continue / Action Button
            var botonGO = new GameObject("Boton");
            botonGO.transform.SetParent(containerGO.transform, false);
            _botonRt = botonGO.AddComponent<RectTransform>();
            _botonRt.anchorMin = new Vector2(0.5f, 0.5f);
            _botonRt.anchorMax = new Vector2(0.5f, 0.5f);
            _botonRt.pivot = new Vector2(0.5f, 0.5f);
            _botonRestPos = new Vector2(0f, -140f);
            _botonRt.anchoredPosition = _botonRestPos + new Vector2(0f, -50f); // Starts lower down
            _botonRt.sizeDelta = new Vector2(300f, 85f);
            
            _botonImg = botonGO.AddComponent<UnityEngine.UI.Image>();
            _botonImg.color = new Color(1f, 1f, 1f, 0f);

            if (continueButtonSprite != null)
            {
                _botonImg.sprite = continueButtonSprite;
            }
            else
            {
                // Fallback flat color buttons
                _botonImg.color = _won 
                    ? new Color(0.12f, 0.35f, 0.22f, 0.0f) // Emerald green button
                    : new Color(0.38f, 0.1f, 0.12f, 0.0f); // Crimson button
                
                var outline = botonGO.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                outline.effectDistance = new Vector2(2f, -2f);

                // Button label
                var labelGO = new GameObject("Text");
                labelGO.transform.SetParent(botonGO.transform, false);
                var labelRt = labelGO.AddComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;
                
                var labelTxt = labelGO.AddComponent<UnityEngine.UI.Text>();
                labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") 
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                labelTxt.fontSize = 28;
                labelTxt.fontStyle = FontStyle.Bold;
                labelTxt.alignment = TextAnchor.MiddleCenter;
                labelTxt.color = new Color(1f, 1f, 1f, 0f);
                labelTxt.text = _won ? "CONTINUAR" : "REINTENTAR";
            }

            // Wire button click logic
            var btn = botonGO.AddComponent<Button>();
            btn.onClick.AddListener(OnContinueButtonPressed);

            // Screen Flash overlay (placed at the end of the canvas hierarchy so it plays ON TOP of all elements)
            var flashGO = new GameObject("FlashOverlay");
            flashGO.transform.SetParent(_outroUIInstance.transform, false);
            var flashRt = flashGO.AddComponent<RectTransform>();
            flashRt.anchorMin = Vector2.zero;
            flashRt.anchorMax = Vector2.one;
            flashRt.offsetMin = flashRt.offsetMax = Vector2.zero;
            _flashOverlay = flashGO.AddComponent<UnityEngine.UI.Image>();
            _flashOverlay.color = new Color(1f, 1f, 1f, 0f);
        }

        private void OnContinueButtonPressed()
        {
            _outroFinished = true;
        }

        private IEnumerator AnimateFondoFade()
        {
            float t = 0f;
            float duration = 0.6f;
            
            // Wait slightly after flash completes
            yield return new WaitForSecondsRealtime(0.1f);

            while (t < duration)
            {
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / duration);
                float easeT = EaseOutExpo(norm);

                // Fade scrim alpha
                _scrimImg.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, _scrimAlpha, easeT));
                
                // Fade fondo background shield
                if (_fondoImg != null)
                {
                    Color c = _fondoImg.color;
                    c.a = easeT;
                    _fondoImg.color = c;
                }

                yield return null;
            }

            _scrimImg.color = new Color(0f, 0f, 0f, _scrimAlpha);
            if (_fondoImg != null)
            {
                Color c = _fondoImg.color;
                c.a = 1f;
                _fondoImg.color = c;
            }
        }

        private IEnumerator AnimateTextoElasticPop()
        {
            float t = 0f;
            float duration = 0.7f;
            
            // Wait slightly after flash completes
            yield return new WaitForSecondsRealtime(0.15f);

            while (t < duration)
            {
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / duration);
                float easePop = ElasticOut(norm);
                float easePos = EaseOutExpo(norm);

                _textoRt.localScale = Vector3.one * Mathf.LerpUnclamped(1.5f, 1.0f, easePop);
                _textoRt.anchoredPosition = Vector2.Lerp(_textoRestPos + new Vector2(0f, 50f), _textoRestPos, easePos);

                // Animate text transparency
                if (_textoImg != null)
                {
                    Color c = _textoImg.color;
                    c.a = norm;
                    _textoImg.color = c;
                }
                else
                {
                    var txt = _textoRt.GetComponent<UnityEngine.UI.Text>();
                    if (txt != null)
                    {
                        Color c = txt.color;
                        c.a = norm;
                        txt.color = c;
                    }
                }

                yield return null;
            }

            _textoRt.localScale = Vector3.one;
            _textoRt.anchoredPosition = _textoRestPos;
            
            if (_textoImg != null)
            {
                Color c = _textoImg.color;
                c.a = 1f;
                _textoImg.color = c;
            }
            else
            {
                var txt = _textoRt.GetComponent<UnityEngine.UI.Text>();
                if (txt != null)
                {
                    Color c = txt.color;
                    c.a = 1f;
                    txt.color = c;
                }
            }
        }

        private IEnumerator AnimateBotonSlideUp()
        {
            float t = 0f;
            float duration = 0.5f;

            // Delayed so the text pop gets primary visual attention
            yield return new WaitForSecondsRealtime(0.45f);

            while (t < duration)
            {
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / duration);
                float easePos = EaseOutExpo(norm);

                _botonRt.anchoredPosition = Vector2.Lerp(_botonRestPos + new Vector2(0f, -50f), _botonRestPos, easePos);

                // Fade button graphic and text label
                if (_botonImg != null)
                {
                    Color c = _botonImg.color;
                    c.a = norm;
                    _botonImg.color = c;
                }

                var label = _botonRt.transform.Find("Text")?.GetComponent<UnityEngine.UI.Text>();
                if (label != null)
                {
                    Color c = label.color;
                    c.a = norm;
                    label.color = c;
                }

                yield return null;
            }

            _botonRt.anchoredPosition = _botonRestPos;
            if (_botonImg != null)
            {
                Color c = _botonImg.color;
                c.a = 1f;
                _botonImg.color = c;
            }

            var finalLabel = _botonRt.transform.Find("Text")?.GetComponent<UnityEngine.UI.Text>();
            if (finalLabel != null)
            {
                Color c = finalLabel.color;
                c.a = 1f;
                finalLabel.color = c;
            }
        }

        private IEnumerator FadeOutToBlackRoutine()
        {
            // First, trigger a beautiful quick flash fade out of elements
            float t = 0f;
            float duration = 0.5f;

            // Fade out the continue button and main shield elements
            while (t < duration)
            {
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / duration);
                _uiCg.alpha = 1f - norm;
                yield return null;
            }

            _uiCg.alpha = 0f;

            // Trigger active black screen transition (respecting bootstrapper transition screen if available)
            if (_bootstrapper != null && _bootstrapper.cameraController != null)
            {
                // Give GameManager or transition screen 0.2s of absolute padding
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }

                private IEnumerator AnimatePPVolumeWeight(float targetWeight, float duration, bool useUnscaledTime)
        {
            if (outroPPVolume == null) yield break;

            float startWeight = outroPPVolume.weight;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                outroPPVolume.weight = Mathf.Lerp(startWeight, targetWeight, EaseOutExpo(t));
                yield return null;
            }
            outroPPVolume.weight = targetWeight;
        }

        private static float EaseOutExpo(float t)
        {
            return t == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
        }

        private static float ElasticOut(float t)
        {
            if (t == 0f || t == 1f) return t;
            float p = 0.3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - p / 4f) * (2f * Mathf.PI) / p) + 1f;
        }

        private void Update()
        {
            // Flash overlay post-trigger fade out to clear screen
            if (_flashOverlay != null && _flashOverlay.color.a > 0.001f)
            {
                float postT = Time.unscaledDeltaTime * 2.5f; // Fast flash fade out
                Color c = _flashOverlay.color;
                c.a = Mathf.Max(0f, c.a - postT);
                _flashOverlay.color = c;
            }
        }

        private void OnDestroy()
        {
            if (_outroUIInstance != null)
            {
                Destroy(_outroUIInstance);
            }
        }
    }
}
