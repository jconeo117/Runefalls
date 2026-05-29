using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Plays the combat intro before TurnManager.StartCombat is called.
    /// Hides the gameplay HUD, plays either a Timeline/Cinemachine sequence or a logical camera lerp,
    /// and triggers onComplete to start active gameplay.
    /// Supports fully automatic track binding and cinematic UI generation at runtime.
    /// </summary>
    public class CombatIntroSequencer : MonoBehaviour
    {
        [Header("Timeline (Dynamic/Cinematic)")]
        [Tooltip("Optional. PlayableDirector that plays the cinematic intro timeline.")]
        public PlayableDirector playableDirector;

        [Header("Camera (Logical Fallback)")]
        public CombatCameraController cameraController;

        [Header("Intro Anchors (Logical Fallback)")]
        [Tooltip("Empty GO placed in scene facing the enemy team. If null, falls back to gameplay player anchor.")]
        public Transform introEnemyAnchor;
        [Tooltip("Empty GO placed in scene facing the player team. If null, falls back to gameplay enemy anchor.")]
        public Transform introPlayerAnchor;

        [Header("Camera Animation (Logical Fallback)")]
        [Tooltip("Units above anchor the camera starts — LateUpdate lerps it down during reveal.")]
        public float dropHeight = 1.0f;
        [Tooltip("Lerp speed during intro settle. Lower = slower/weightier. Restored after intro.")]
        public float introLerpSpeed = 2.0f;

        [Header("Timing (Logical Fallback)")]
        [Tooltip("Seconds the camera holds on the enemy team.")]
        public float enemyRevealDuration = 1.2f;
        [Tooltip("Seconds after camera snaps to player side before combat begins.")]
        public float playerRevealDuration = 1.0f;

        private Action _onCompleteCallback;
        private CombatPresenterBase _hudPresenter;
        private GameObject _introUIInstance;
        private bool _isCompleted;

        public void Run(Action onComplete)
        {
            _onCompleteCallback = onComplete;
            _isCompleted = false;

            // 1. Locate and hide the active gameplay HUD presenter so it doesn't clutter the cinematic intro
            _hudPresenter = FindFirstObjectByType<CombatPresenterBase>();
            if (_hudPresenter == null)
            {
                #pragma warning disable CS0618
                _hudPresenter = FindObjectOfType<CombatPresenterBase>();
                #pragma warning restore CS0618
            }

            if (_hudPresenter != null)
            {
                _hudPresenter.HideAllUI();
            }

            // 2. Play using the dynamic timeline if available, otherwise run the code-driven fallback camera sweep
            if (playableDirector != null && playableDirector.playableAsset != null)
            {
                StartCoroutine(PlayTimelineSequence());
            }
            else
            {
                StartCoroutine(PlayFallbackSequence());
            }
        }

        /// <summary>
        /// Public entry point for Timeline Signals or script markers to finish the intro sequence exactly when desired.
        /// </summary>
        public void CompleteIntro()
        {
            if (_isCompleted) return;
            _isCompleted = true;

            // Stop the director if it is still playing
            if (playableDirector != null && playableDirector.state == PlayState.Playing)
            {
                playableDirector.Stop();
            }

            // Destroy the runtime cinematic UI
            if (_introUIInstance != null)
            {
                Destroy(_introUIInstance);
                _introUIInstance = null;
            }

            // Restore the gameplay HUD
            if (_hudPresenter != null)
            {
                _hudPresenter.ShowAllUI();
            }

            _onCompleteCallback?.Invoke();
        }

        private IEnumerator PlayTimelineSequence()
        {
            // 1. Find Animators and set runtime generic bindings for dynamically spawned objects
            Animator playerAnimator = null;
            var enemyAnimators = new List<Animator>();

            var bootstrapper = FindFirstObjectByType<CombatBootstrapper>();
            if (bootstrapper == null)
            {
                #pragma warning disable CS0618
                bootstrapper = FindObjectOfType<CombatBootstrapper>();
                #pragma warning restore CS0618
            }

            if (bootstrapper != null)
            {
                // Find Player Animator under active arena slots or explore nodes
                if (bootstrapper.arenaAssembler != null && bootstrapper.arenaAssembler.IsReady)
                {
                    foreach (var slot in bootstrapper.arenaAssembler.PlayerSlots)
                    {
                        var anim = slot.GetComponentInChildren<Animator>();
                        if (anim != null)
                        {
                            playerAnimator = anim;
                            break;
                        }
                    }
                }
                else if (bootstrapper.playerTeam != null)
                {
                    playerAnimator = bootstrapper.playerTeam.GetComponentInChildren<Animator>();
                }

                // Find Enemy Animators under active arena slots or explore nodes
                if (bootstrapper.arenaAssembler != null && bootstrapper.arenaAssembler.IsReady)
                {
                    foreach (var slot in bootstrapper.arenaAssembler.EnemySlots)
                    {
                        var anim = slot.GetComponentInChildren<Animator>();
                        if (anim != null) enemyAnimators.Add(anim);
                    }
                }
                else if (bootstrapper.enemyTeam != null)
                {
                    var anims = bootstrapper.enemyTeam.GetComponentsInChildren<Animator>();
                    if (anims != null) enemyAnimators.AddRange(anims);
                }
            }

            // Bind Timeline Generic Outputs dynamically to allow runtime spawned players/enemies/cameras
            if (playableDirector != null && playableDirector.playableAsset != null)
            {
                var timeline = playableDirector.playableAsset as TimelineAsset;
                if (timeline != null)
                {
                    foreach (var output in timeline.outputs)
                    {
                        // A. Cinemachine Track: bind the runtime camera's Cinemachine Brain
                        if (output.outputTargetType == typeof(Unity.Cinemachine.CinemachineBrain))
                        {
                            var brain = Camera.main != null ? Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
                            if (brain != null)
                            {
                                playableDirector.SetGenericBinding(output.sourceObject, brain);
                            }
                        }

                        // B. Animator Tracks: match by track names (e.g. "Player" or "Enemy 1", "Enemy 2")
                        if (output.outputTargetType == typeof(Animator))
                        {
                            string name = output.streamName.ToLower();
                            if (name.Contains("player") || name.Contains("hero"))
                            {
                                if (playerAnimator != null)
                                {
                                    playableDirector.SetGenericBinding(output.sourceObject, playerAnimator);
                                }
                            }
                            else if (name.Contains("enemy"))
                            {
                                int index = 0;
                                string numberStr = System.Text.RegularExpressions.Regex.Match(name, @"\d+").Value;
                                if (!string.IsNullOrEmpty(numberStr))
                                {
                                    index = int.Parse(numberStr) - 1;
                                }

                                if (index >= 0 && index < enemyAnimators.Count)
                                {
                                    playableDirector.SetGenericBinding(output.sourceObject, enemyAnimators[index]);
                                }
                                else if (enemyAnimators.Count > 0)
                                {
                                    playableDirector.SetGenericBinding(output.sourceObject, enemyAnimators[0]);
                                }
                            }
                        }
                    }
                }
            }

            // 2. Create the Cinematic UI programmatically at runtime (zero scene dependencies!)
            CreateCinematicUI(bootstrapper);

            // 3. Reset and Play Timeline
            playableDirector.time = 0;
            playableDirector.Evaluate();
            playableDirector.stopped += OnDirectorStopped;
            playableDirector.Play();

            float duration = (float)playableDirector.duration;
            float elapsed = 0f;
            float safetyMargin = 0.5f;

            // Cache UI RectTransforms for animation
            RectTransform topBar = _introUIInstance.transform.Find("TopBar") as RectTransform;
            RectTransform botBar = _introUIInstance.transform.Find("BottomBar") as RectTransform;
            RectTransform leftPlate = _introUIInstance.transform.Find("LeftPlate") as RectTransform;
            RectTransform rightPlate = _introUIInstance.transform.Find("RightPlate") as RectTransform;
            RectTransform clashTitle = _introUIInstance.transform.Find("RuneClash") as RectTransform;
            UnityEngine.UI.Image flashOverlay = _introUIInstance.transform.Find("FlashOverlay").GetComponent<UnityEngine.UI.Image>();
            CanvasGroup uiCg = _introUIInstance.GetComponent<CanvasGroup>();

            // Animate programmatic Cinematic UI in sync with the Timeline playback!
            while (elapsed < duration + safetyMargin && !_isCompleted)
            {
                elapsed = (float)playableDirector.time;

                // Animate Top and Bottom letterbox bars (0.0s to 0.4s)
                if (elapsed < 0.4f)
                {
                    float t = Mathf.Clamp01(elapsed / 0.4f);
                    topBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(150f, 0f, t));
                    botBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(-150f, 0f, t));
                }
                else
                {
                    topBar.anchoredPosition = new Vector2(0f, 0f);
                    botBar.anchoredPosition = new Vector2(0f, 0f);
                }

                // Animate Left and Right nameplate banners (2.5s to 2.8s)
                if (elapsed >= 2.5f && elapsed < 2.8f)
                {
                    float t = Mathf.Clamp01((elapsed - 2.5f) / 0.3f);
                    leftPlate.anchoredPosition = new Vector2(Mathf.Lerp(-600f, 0f, t), 0f);
                    rightPlate.anchoredPosition = new Vector2(Mathf.Lerp(600f, 0f, t), 0f);
                }
                else if (elapsed >= 2.8f)
                {
                    leftPlate.anchoredPosition = new Vector2(0f, 0f);
                    rightPlate.anchoredPosition = new Vector2(0f, 0f);
                }

                // Animate "RUNE CLASH!" title pop-in with elastic pop (2.8s to 3.1s)
                if (elapsed >= 2.8f && elapsed < 3.1f)
                {
                    float t = Mathf.Clamp01((elapsed - 2.8f) / 0.3f);
                    float scale = Mathf.Sin(t * Mathf.PI * 1.5f) * 0.3f + t; // Elastic scale pop
                    clashTitle.localScale = Vector3.one * scale;
                }
                else if (elapsed >= 3.1f)
                {
                    clashTitle.localScale = Vector3.one;
                }

                // Climax flash, camera shake, and slide out (3.5s to 3.8s)
                if (elapsed >= 3.5f)
                {
                    float t = Mathf.Clamp01((elapsed - 3.5f) / 0.3f);
                    
                    // Flash overlay: quick peak, fade out
                    if (elapsed < 3.8f)
                    {
                        float flashT = Mathf.Clamp01((elapsed - 3.5f) / 0.1f);
                        if (elapsed < 3.6f)
                            flashOverlay.color = new Color(1f, 1f, 1f, flashT);
                        else
                            flashOverlay.color = new Color(1f, 1f, 1f, 1f - (elapsed - 3.6f) / 0.2f);

                        // Trigger slight camera shake during the flash
                        if (Camera.main != null)
                        {
                            Camera.main.transform.position += UnityEngine.Random.insideUnitSphere * 0.05f;
                        }
                    }
                    else
                    {
                        flashOverlay.color = new Color(1f, 1f, 1f, 0f);
                    }

                    // Slide out Top and Bottom bars
                    topBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 150f, t));
                    botBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, -150f, t));

                    // Fade out banners
                    uiCg.alpha = 1f - t;
                }

                yield return null;
            }

            if (!_isCompleted)
            {
                CompleteIntro();
            }
        }

        private void OnDirectorStopped(PlayableDirector director)
        {
            if (playableDirector != null)
            {
                playableDirector.stopped -= OnDirectorStopped;
            }
            CompleteIntro();
        }

        private IEnumerator PlayFallbackSequence()
        {
            if (cameraController != null)
            {
                float originalLerpSpeed = cameraController.lerpSpeed;
                cameraController.lerpSpeed = introLerpSpeed;

                var drop = Vector3.up * dropHeight;

                var enemyAnchor = introEnemyAnchor;
                var playerAnchor = introPlayerAnchor;

                if (enemyAnchor != null)
                    cameraController.SnapToWithOffset(enemyAnchor, drop);
                else
                    cameraController.SnapToAnchor(enemySide: false);

                yield return new WaitForSeconds(enemyRevealDuration);

                if (playerAnchor != null)
                    cameraController.SnapToWithOffset(playerAnchor, drop);
                else
                    cameraController.SnapToAnchor(enemySide: true);

                yield return new WaitForSeconds(playerRevealDuration);

                cameraController.lerpSpeed = originalLerpSpeed;
                cameraController.SnapToAnchorWithOffset(enemySide: false, drop);
            }

            CompleteIntro();
        }

        private void CreateCinematicUI(CombatBootstrapper bootstrapper)
        {
            if (_introUIInstance != null) Destroy(_introUIInstance);

            // Create Canvas
            _introUIInstance = new GameObject("CombatIntro_CinematicUI");
            var canvas = _introUIInstance.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            
            var scaler = _introUIInstance.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            _introUIInstance.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var rootCg = _introUIInstance.AddComponent<CanvasGroup>();

            // Top letterbox bar
            var topBarGO = new GameObject("TopBar");
            topBarGO.transform.SetParent(_introUIInstance.transform, false);
            var topRt = topBarGO.AddComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            topRt.anchoredPosition = new Vector2(0f, 150f);
            topRt.sizeDelta = new Vector2(0f, 120f);
            var topImg = topBarGO.AddComponent<UnityEngine.UI.Image>();
            topImg.color = Color.black;

            // Bottom letterbox bar
            var botBarGO = new GameObject("BottomBar");
            botBarGO.transform.SetParent(_introUIInstance.transform, false);
            var botRt = botBarGO.AddComponent<RectTransform>();
            botRt.anchorMin = new Vector2(0f, 0f);
            botRt.anchorMax = new Vector2(1f, 0f);
            botRt.pivot = new Vector2(0.5f, 0f);
            botRt.anchoredPosition = new Vector2(0f, -150f);
            botRt.sizeDelta = new Vector2(0f, 120f);
            var botImg = botBarGO.AddComponent<UnityEngine.UI.Image>();
            botImg.color = Color.black;

            // Left nameplate (Player side)
            var leftPlateGO = new GameObject("LeftPlate");
            leftPlateGO.transform.SetParent(_introUIInstance.transform, false);
            var leftRt = leftPlateGO.AddComponent<RectTransform>();
            leftRt.anchorMin = new Vector2(0f, 0.5f);
            leftRt.anchorMax = new Vector2(0f, 0.5f);
            leftRt.pivot = new Vector2(0f, 0.5f);
            leftRt.anchoredPosition = new Vector2(-600f, 0f);
            leftRt.sizeDelta = new Vector2(500f, 100f);
            var leftImg = leftPlateGO.AddComponent<UnityEngine.UI.Image>();
            leftImg.color = new Color(0.06f, 0.16f, 0.32f, 0.85f); // Deep modern dark neon blue

            var leftTextGO = new GameObject("Text");
            leftTextGO.transform.SetParent(leftPlateGO.transform, false);
            var leftTxtRt = leftTextGO.AddComponent<RectTransform>();
            leftTxtRt.anchorMin = Vector2.zero;
            leftTxtRt.anchorMax = Vector2.one;
            leftTxtRt.offsetMin = new Vector2(40f, 0f);
            leftTxtRt.offsetMax = Vector2.zero;
            var leftTxt = leftTextGO.AddComponent<UnityEngine.UI.Text>();
            leftTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            leftTxt.fontSize = 32;
            leftTxt.fontStyle = FontStyle.Bold;
            leftTxt.alignment = TextAnchor.MiddleLeft;
            leftTxt.color = new Color(0.5f, 0.85f, 1f);
            leftTxt.text = "PLAYER TEAM";

            // Right nameplate (Enemy side)
            var rightPlateGO = new GameObject("RightPlate");
            rightPlateGO.transform.SetParent(_introUIInstance.transform, false);
            var rightRt = rightPlateGO.AddComponent<RectTransform>();
            rightRt.anchorMin = new Vector2(1f, 0.5f);
            rightRt.anchorMax = new Vector2(1f, 0.5f);
            rightRt.pivot = new Vector2(1f, 0.5f);
            rightRt.anchoredPosition = new Vector2(600f, 0f);
            rightRt.sizeDelta = new Vector2(500f, 100f);
            var rightImg = rightPlateGO.AddComponent<UnityEngine.UI.Image>();
            rightImg.color = new Color(0.35f, 0.05f, 0.08f, 0.85f); // Deep modern dark neon red

            var rightTextGO = new GameObject("Text");
            rightTextGO.transform.SetParent(rightPlateGO.transform, false);
            var rightTxtRt = rightTextGO.AddComponent<RectTransform>();
            rightTxtRt.anchorMin = Vector2.zero;
            rightTxtRt.anchorMax = Vector2.one;
            rightTxtRt.offsetMin = Vector2.zero;
            rightTxtRt.offsetMax = new Vector2(-40f, 0f);
            var rightTxt = rightTextGO.AddComponent<UnityEngine.UI.Text>();
            rightTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rightTxt.fontSize = 32;
            rightTxt.fontStyle = FontStyle.Bold;
            rightTxt.alignment = TextAnchor.MiddleRight;
            rightTxt.color = new Color(1f, 0.45f, 0.5f);
            rightTxt.text = "ENEMY TEAM";

            // "RUNE CLASH!" title banner
            var clashGO = new GameObject("RuneClash");
            clashGO.transform.SetParent(_introUIInstance.transform, false);
            var clashRt = clashGO.AddComponent<RectTransform>();
            clashRt.anchorMin = new Vector2(0.5f, 0.5f);
            clashRt.anchorMax = new Vector2(0.5f, 0.5f);
            clashRt.pivot = new Vector2(0.5f, 0.5f);
            clashRt.anchoredPosition = Vector2.zero;
            clashRt.localScale = Vector3.zero;

            var clashOutline = clashGO.AddComponent<UnityEngine.UI.Outline>();
            clashOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            clashOutline.effectDistance = new Vector2(4f, -4f);

            var clashTxt = clashGO.AddComponent<UnityEngine.UI.Text>();
            clashTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            clashTxt.fontSize = 72;
            clashTxt.fontStyle = FontStyle.BoldAndItalic;
            clashTxt.alignment = TextAnchor.MiddleCenter;
            clashTxt.color = new Color(1.0f, 0.82f, 0.15f);
            clashTxt.text = "RUNE CLASH!";

            // Flash overlay for climax juice
            var flashGO = new GameObject("FlashOverlay");
            flashGO.transform.SetParent(_introUIInstance.transform, false);
            var flashRt = flashGO.AddComponent<RectTransform>();
            flashRt.anchorMin = Vector2.zero;
            flashRt.anchorMax = Vector2.one;
            flashRt.offsetMin = flashRt.offsetMax = Vector2.zero;
            var flashImg = flashGO.AddComponent<UnityEngine.UI.Image>();
            flashImg.color = new Color(1f, 1f, 1f, 0f);

            // Dynamically query names to make it look exceptionally polished
            if (bootstrapper != null)
            {
                // Player Name
                if (bootstrapper.playerTeam != null && bootstrapper.playerTeam.childCount > 0)
                {
                    var slot = bootstrapper.playerTeam.GetChild(0).GetComponent<CharacterSlot>();
                    if (slot != null && slot.data != null)
                    {
                        leftTxt.text = slot.data.characterName.ToUpper();
                    }
                }
                
                // Enemy Name
                if (bootstrapper.enemyTeam != null && bootstrapper.enemyTeam.childCount > 0)
                {
                    var slot = bootstrapper.enemyTeam.GetChild(0).GetComponent<EnemySlot>();
                    if (slot != null && slot.data != null)
                    {
                        rightTxt.text = slot.data.enemyName.ToUpper();
                        if (bootstrapper.enemyTeam.childCount > 1)
                        {
                            rightTxt.text += " GROUP";
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (playableDirector != null)
            {
                playableDirector.stopped -= OnDirectorStopped;
            }
            if (_introUIInstance != null)
            {
                Destroy(_introUIInstance);
            }
        }
    }
}
