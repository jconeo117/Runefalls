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
        [Tooltip("If true, dynamically places and overrides camera positions at runtime. If false, respects designer-placed camera anchors from the layout prefab.")]
        public bool useDynamicCameraAnchors = false;
        [Tooltip("Playback speed multiplier for the cinematic intro timeline. Lower values slow it down for a weightier feel.")]
        [Range(0.1f, 2f)]
        public float timelinePlaybackSpeed = 0.6f; // Slow down to 60% of normal speed for dramatic weighting

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
        private CombatBootstrapper _bootstrapper;
        private bool _directorFinished;
        private bool _cameraHandedOver;

        public void Initialize(CombatBootstrapper bootstrapper, CombatPresenterBase hudPresenter)
        {
            _bootstrapper = bootstrapper;
            _hudPresenter = hudPresenter;
        }

        public void Run(Action onComplete)
        {
            _onCompleteCallback = onComplete;
            _isCompleted = false;
            _cameraHandedOver = false;

            // 1. Locate and hide the active gameplay HUD presenter so it doesn't clutter the cinematic intro
            if (_hudPresenter == null)
            {
                _hudPresenter = FindFirstObjectByType<CombatPresenterBase>();
                if (_hudPresenter == null)
                {
                    #pragma warning disable CS0618
                    _hudPresenter = FindObjectOfType<CombatPresenterBase>();
                    #pragma warning restore CS0618
                }
            }

            if (_hudPresenter != null)
            {
                _hudPresenter.HideAllUI();
            }

            // Find Bootstrapper and look for an active PlayableDirector in the instantiated arena layout
            if (_bootstrapper == null)
            {
                _bootstrapper = FindFirstObjectByType<CombatBootstrapper>();
                if (_bootstrapper == null)
                {
                    #pragma warning disable CS0618
                    _bootstrapper = FindObjectOfType<CombatBootstrapper>();
                    #pragma warning restore CS0618
                }
            }

            PlayableDirector activeDirector = null;
            if (_bootstrapper != null && _bootstrapper.arenaAssembler != null)
            {
                activeDirector = _bootstrapper.arenaAssembler.GetLayoutDirector();
            }

            if (activeDirector == null)
            {
                activeDirector = playableDirector;
            }

            if (_bootstrapper != null && _bootstrapper.arenaAssembler != null && _bootstrapper.arenaAssembler.IsReady)
            {
                AlignAnchorsDynamically(_bootstrapper.arenaAssembler);
            }

            // 2. Play using the dynamic timeline if available, otherwise run the code-driven fallback camera sweep
            if (activeDirector != null && activeDirector.playableAsset != null)
            {
                playableDirector = activeDirector;
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
        private void PerformCameraHandoff()
        {
            if (_cameraHandedOver) return;
            _cameraHandedOver = true;

            var bootstrapper = _bootstrapper != null ? _bootstrapper : FindFirstObjectByType<CombatBootstrapper>();
            if (bootstrapper == null)
            {
                #pragma warning disable CS0618
                bootstrapper = FindObjectOfType<CombatBootstrapper>();
                #pragma warning restore CS0618
            }

            // A. Disable CinemachineBrain so the manual camera controller can drive the camera
            var brain = Camera.main != null ? Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
            if (brain != null)
            {
                brain.enabled = false;
            }

            // B. Enable and initialize the manual camera controller
            if (bootstrapper != null && bootstrapper.cameraController != null)
            {
                bootstrapper.cameraController.enabled = true;

                if (bootstrapper.arenaAssembler != null && bootstrapper.arenaAssembler.IsReady)
                {
                    if (bootstrapper.arenaAssembler.CameraGameplayAnchor != null)
                    {
                        // Match the Main Camera exactly to the CameraGameplay anchor to avoid a jarring snap,
                        // then initialize manual anchors from it
                        Camera.main.transform.position = bootstrapper.arenaAssembler.CameraGameplayAnchor.position;
                        Camera.main.transform.rotation = bootstrapper.arenaAssembler.CameraGameplayAnchor.rotation;

                        bootstrapper.cameraController.InitFromAnchors(
                            bootstrapper.arenaAssembler.CameraGameplayAnchor,
                            bootstrapper.arenaAssembler.FieldCenter
                        );
                    }
                    else
                    {
                        bootstrapper.cameraController.InitFromTeams(
                            bootstrapper.arenaAssembler.PlayerRoot,
                            bootstrapper.arenaAssembler.EnemyRoot
                        );
                    }
                }

                // Snap manually to the player side anchor (the gameplay camera target)
                bootstrapper.cameraController.SnapToAnchor(enemySide: false);
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

            // Restore animator speeds just in case they were left frozen
            var bootstrapper = _bootstrapper != null ? _bootstrapper : FindFirstObjectByType<CombatBootstrapper>();
            if (bootstrapper == null)
            {
                #pragma warning disable CS0618
                bootstrapper = FindObjectOfType<CombatBootstrapper>();
                #pragma warning restore CS0618
            }

            if (bootstrapper != null)
            {
                var anims = bootstrapper.GetComponentsInChildren<Animator>();
                foreach (var a in anims)
                {
                    if (a != null) a.speed = 1f;
                }
            }

            // Hand off control from Cinemachine to the manual CombatCameraController if not already done
            PerformCameraHandoff();

            _onCompleteCallback?.Invoke();
        }

        private IEnumerator PlayTimelineSequence()
        {
            // Enable CinemachineBrain so the Timeline Cinemachine tracks can control the camera!
            var brain = Camera.main != null ? Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
            if (brain != null)
            {
                brain.enabled = true;
            }

            // Disable manual camera controller so it does not fight/lock the camera during the timeline!
            var bootstrapper = _bootstrapper != null ? _bootstrapper : FindFirstObjectByType<CombatBootstrapper>();
            if (bootstrapper == null)
            {
                #pragma warning disable CS0618
                bootstrapper = FindObjectOfType<CombatBootstrapper>();
                #pragma warning restore CS0618
            }

            if (bootstrapper != null && bootstrapper.cameraController != null)
            {
                bootstrapper.cameraController.enabled = false;
            }

            // 1. Find Animators and set runtime generic bindings for dynamically spawned objects
            Animator playerAnimator = null;
            var enemyAnimators = new List<Animator>();


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
                            if (brain != null)
                            {
                                playableDirector.SetGenericBinding(output.sourceObject, brain);
                            }

                            // Bind the individual clips/shots on the CinemachineTrack dynamically!
                            if (output.sourceObject is Unity.Cinemachine.CinemachineTrack cinemachineTrack)
                            {
                                foreach (var clip in cinemachineTrack.GetClips())
                                {
                                    var shot = clip.asset as Unity.Cinemachine.CinemachineShot;
                                    if (shot != null)
                                    {
                                        string clipName = clip.displayName.ToLower();
                                        Transform targetAnchor = null;

                                        if (clipName.Contains("enemy") && bootstrapper != null && bootstrapper.arenaAssembler != null)
                                            targetAnchor = bootstrapper.arenaAssembler.IntroEnemyAnchor;
                                        else if (clipName.Contains("player") && bootstrapper != null && bootstrapper.arenaAssembler != null)
                                            targetAnchor = bootstrapper.arenaAssembler.IntroPlayerAnchor;
                                        else if ((clipName.Contains("settle") || clipName.Contains("gameplay") || clipName.Contains("wide")) && bootstrapper != null && bootstrapper.arenaAssembler != null)
                                            targetAnchor = bootstrapper.arenaAssembler.CameraGameplayAnchor;

                                        if (targetAnchor != null)
                                        {
                                            var vcam = targetAnchor.GetComponent<Unity.Cinemachine.CinemachineCamera>();
                                            if (vcam == null)
                                            {
                                                vcam = targetAnchor.gameObject.AddComponent<Unity.Cinemachine.CinemachineCamera>();
                                            }
                                            
                                            // Set standard CinemachineCamera defaults so it displays beautifully
                                            vcam.Priority = 10;
                                            
                                            playableDirector.SetReferenceValue(shot.VirtualCamera.exposedName, vcam);
                                        }
                                    }
                                }
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
            _directorFinished = false;
            
            // Set speed on the PlayableGraph to slow down the 3D cinematic movement
            if (playableDirector.playableGraph.IsValid() && playableDirector.playableGraph.GetRootPlayableCount() > 0)
            {
                playableDirector.playableGraph.GetRootPlayable(0).SetSpeed(timelinePlaybackSpeed);
            }
            
            playableDirector.stopped += OnDirectorStopped;
            playableDirector.Play();

            float elapsed = 0f;
            float targetCollisionTime = 4.3f; // Absolute collision moment matching the end of player shot track
            float safetyMargin = 0.8f; // 0.8 seconds for a juicy, well-paced post-climax handoff
            float totalDuration = targetCollisionTime + safetyMargin;

            // Cache UI RectTransforms and CanvasGroups for animation
            RectTransform topBar = _introUIInstance.transform.Find("TopBar") as RectTransform;
            RectTransform botBar = _introUIInstance.transform.Find("BottomBar") as RectTransform;
            RectTransform leftPlate = _introUIInstance.transform.Find("LeftPlate") as RectTransform;
            RectTransform rightPlate = _introUIInstance.transform.Find("RightPlate") as RectTransform;
            RectTransform clashTitle = _introUIInstance.transform.Find("RuneClash") as RectTransform;
            
            CanvasGroup leftCg = leftPlate.GetComponent<CanvasGroup>();
            CanvasGroup rightCg = rightPlate.GetComponent<CanvasGroup>();
            
            CanvasGroup VS_Cg = clashTitle.GetComponent<CanvasGroup>();
            if (VS_Cg == null)
            {
                VS_Cg = clashTitle.gameObject.AddComponent<CanvasGroup>();
            }
            VS_Cg.alpha = 0f; // Start hidden
            
            UnityEngine.UI.Image flashOverlay = _introUIInstance.transform.Find("FlashOverlay").GetComponent<UnityEngine.UI.Image>();
            CanvasGroup uiCg = _introUIInstance.GetComponent<CanvasGroup>();
            if (uiCg != null)
            {
                uiCg.alpha = 1f; // Explicitly ensure visible
            }

            bool enemiesFrozen = false;

            // Animate programmatic Cinematic UI in sync with the Timeline playback!
            while (elapsed < totalDuration && !_isCompleted)
            {
                if (playableDirector != null && playableDirector.state == PlayState.Playing && !_directorFinished)
                {
                    elapsed = (float)playableDirector.time;

                    // Trigger the impact handoff early exactly at targetCollisionTime (4.3s)
                    if (elapsed >= targetCollisionTime)
                    {
                        elapsed = targetCollisionTime;
                        _directorFinished = true;
                        
                        // Perform early camera snap and handoff under the solid white flash!
                        PerformCameraHandoff();

                        if (playableDirector.state == PlayState.Playing)
                        {
                            playableDirector.Stop();
                        }
                    }
                }
                else
                {
                    // Manually increment time once director completes or stops so the post-climax sequence finishes perfectly
                    elapsed += Time.deltaTime;
                }

                // A. Animate Top and Bottom letterbox bars slide-in (0.0s to 0.4s)
                if (elapsed < 0.4f)
                {
                    float t = Mathf.Clamp01(elapsed / 0.4f);
                    float easeT = EaseOutExpo(t);
                    topBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(150f, 0f, easeT));
                    botBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(-150f, 0f, easeT));
                }
                else if (elapsed < targetCollisionTime)
                {
                    topBar.anchoredPosition = new Vector2(0f, 0f);
                    botBar.anchoredPosition = new Vector2(0f, 0f);
                }

                // B. Animate Enemy Left Nameplate banner slide-in (0.4s to 0.9s, holds until 4.0s)
                if (elapsed >= 0.4f && elapsed < 0.9f)
                {
                    float t = Mathf.Clamp01((elapsed - 0.4f) / 0.5f);
                    float easeT = EaseOutExpo(t);
                    leftPlate.anchoredPosition = new Vector2(Mathf.Lerp(-600f, 0f, easeT), 0f);
                    leftCg.alpha = 1f;
                }
                else if (elapsed >= 0.9f && elapsed < 4.0f)
                {
                    leftPlate.anchoredPosition = new Vector2(0f, 0f);
                    leftCg.alpha = 1f;
                }

                // C. Beat de pausa — freeze frame enemigos (1.8s to 2.5s)
                if (elapsed >= 1.8f && elapsed < 2.5f)
                {
                    if (!enemiesFrozen)
                    {
                        foreach (var anim in enemyAnimators)
                        {
                            if (anim != null) anim.speed = 0f;
                        }
                        enemiesFrozen = true;
                    }
                }
                else
                {
                    if (enemiesFrozen)
                    {
                        foreach (var anim in enemyAnimators)
                        {
                            if (anim != null) anim.speed = 1f;
                        }
                        enemiesFrozen = false;
                    }
                }

                // D. Animate Player Right Nameplate banner slide-in (2.8s to 3.3s, holds until 4.0s)
                // Delayed to start when the player is fully framing in camera
                if (elapsed >= 2.8f && elapsed < 3.3f)
                {
                    float t = Mathf.Clamp01((elapsed - 2.8f) / 0.5f);
                    float easeT = EaseOutExpo(t);
                    rightPlate.anchoredPosition = new Vector2(Mathf.Lerp(600f, 0f, easeT), 0f);
                    rightCg.alpha = 1f;
                }
                else if (elapsed >= 3.3f && elapsed < 4.0f)
                {
                    rightPlate.anchoredPosition = new Vector2(0f, 0f);
                    rightCg.alpha = 1f;
                }

                // E. THE CHARGE! (4.0s to 4.3s)
                // Left and right banners charge towards each other to meet exactly in the center of the screen at 4.3s
                if (elapsed >= 4.0f && elapsed < targetCollisionTime)
                {
                    float chargeT = Mathf.Clamp01((elapsed - 4.0f) / 0.3f);
                    float easeT = Mathf.Pow(chargeT, 3f); // Slam cubic speed curve
                    
                    leftPlate.anchoredPosition = new Vector2(Mathf.Lerp(0f, 410f, easeT), 0f);
                    leftCg.alpha = 1f;

                    rightPlate.anchoredPosition = new Vector2(Mathf.Lerp(0f, -410f, easeT), 0f);
                    rightCg.alpha = 1f;
                }

                // F. THE COLLISION CLIMAX! (4.3s to totalDuration)
                if (elapsed >= targetCollisionTime)
                {
                    // 1. Recoil / Bounce back and fade out of banners upon impact (4.3s to 4.6s)
                    if (elapsed < 4.6f)
                    {
                        float recoilT = Mathf.Clamp01((elapsed - targetCollisionTime) / 0.3f);
                        float easeRecoil = EaseOutExpo(recoilT);
                        leftPlate.anchoredPosition = new Vector2(Mathf.Lerp(410f, 320f, easeRecoil), 0f);
                        leftCg.alpha = 1f - recoilT;

                        rightPlate.anchoredPosition = new Vector2(Mathf.Lerp(-410f, -320f, easeRecoil), 0f);
                        rightCg.alpha = 1f - recoilT;
                    }
                    else
                    {
                        leftCg.alpha = 0f;
                        rightCg.alpha = 0f;
                    }

                    // 2. Flash overlay: peak fully white instantly at 4.3s, stays solid until 4.4s, then fade out during safetyMargin
                    if (elapsed < 4.4f)
                    {
                        float flashT = Mathf.Clamp01((elapsed - targetCollisionTime) / 0.05f);
                        flashOverlay.color = new Color(1f, 1f, 1f, flashT);
                    }
                    else
                    {
                        // Fade out smoothly over the remainder of the safetyMargin
                        float postT = Mathf.Clamp01((elapsed - 4.4f) / (safetyMargin - 0.1f));
                        flashOverlay.color = new Color(1f, 1f, 1f, 1f - postT);
                    }

                    // 3. Camera shake during flash (4.3s to 4.6s)
                    if (elapsed < 4.6f && Camera.main != null)
                    {
                        Camera.main.transform.position += UnityEngine.Random.insideUnitSphere * 0.05f;
                    }

                    // 4. VS Elastic Scale Pop (starts at 4.3s, pop duration is 0.4s)
                    float vsElapsed = elapsed - targetCollisionTime;
                    if (vsElapsed >= 0f)
                    {
                        float vsPopDuration = 0.4f;
                        float vsT = Mathf.Clamp01(vsElapsed / vsPopDuration);
                        float scale = ElasticOut(vsT);
                        clashTitle.localScale = Vector3.one * scale;

                        // Fade out the VS text in the second half of the safetyMargin
                        float fadeStart = targetCollisionTime + safetyMargin * 0.4f;
                        if (elapsed < fadeStart)
                        {
                            VS_Cg.alpha = 1f;
                        }
                        else
                        {
                            float fadeT = Mathf.Clamp01((elapsed - fadeStart) / (safetyMargin * 0.6f));
                            VS_Cg.alpha = 1f - fadeT;
                        }
                    }

                    // 5. Letterbox bars slide-out (starts after 4.3s)
                    if (elapsed >= targetCollisionTime)
                    {
                        float postT = Mathf.Clamp01((elapsed - targetCollisionTime) / safetyMargin);
                        float easeT = EaseOutExpo(postT);
                        topBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 150f, easeT));
                        botBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, -150f, easeT));
                    }
                    else
                    {
                        topBar.anchoredPosition = new Vector2(0f, 0f);
                        botBar.anchoredPosition = new Vector2(0f, 0f);
                    }

                    // 6. Fade out whole UI canvas group in the second half of safetyMargin
                    if (elapsed >= targetCollisionTime + safetyMargin * 0.4f)
                    {
                        float fadeT = Mathf.Clamp01((elapsed - (targetCollisionTime + safetyMargin * 0.4f)) / (safetyMargin * 0.6f));
                        uiCg.alpha = 1f - fadeT;
                    }
                    else
                    {
                        uiCg.alpha = 1f;
                    }
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
            _directorFinished = true;
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

        private Font GetSafeDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return font;
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

            // Safe font reference
            Font safeFont = GetSafeDefaultFont();

            // Top letterbox bar (13% screen height = 140px at 1080p)
            var topBarGO = new GameObject("TopBar");
            topBarGO.transform.SetParent(_introUIInstance.transform, false);
            var topRt = topBarGO.AddComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            topRt.anchoredPosition = new Vector2(0f, 150f);
            topRt.sizeDelta = new Vector2(0f, 140f);
            var topImg = topBarGO.AddComponent<UnityEngine.UI.Image>();
            topImg.color = Color.black;

            // Bottom letterbox bar (13% screen height = 140px at 1080p)
            var botBarGO = new GameObject("BottomBar");
            botBarGO.transform.SetParent(_introUIInstance.transform, false);
            var botRt = botBarGO.AddComponent<RectTransform>();
            botRt.anchorMin = new Vector2(0f, 0f);
            botRt.anchorMax = new Vector2(1f, 0f);
            botRt.pivot = new Vector2(0.5f, 0f);
            botRt.anchoredPosition = new Vector2(0f, -150f);
            botRt.sizeDelta = new Vector2(0f, 140f);
            var botImg = botBarGO.AddComponent<UnityEngine.UI.Image>();
            botImg.color = Color.black;

            // Left nameplate (Enemy side)
            var leftPlateGO = new GameObject("LeftPlate");
            leftPlateGO.transform.SetParent(_introUIInstance.transform, false);
            var leftRt = leftPlateGO.AddComponent<RectTransform>();
            leftRt.anchorMin = new Vector2(0f, 0.5f);
            leftRt.anchorMax = new Vector2(0f, 0.5f);
            leftRt.pivot = new Vector2(0f, 0.5f);
            leftRt.anchoredPosition = new Vector2(-600f, 0f);
            leftRt.sizeDelta = new Vector2(550f, 120f);
            var leftImg = leftPlateGO.AddComponent<UnityEngine.UI.Image>();
            leftImg.color = new Color(0.35f, 0.05f, 0.08f, 0.85f); // Deep modern dark neon red/crimson
            leftPlateGO.AddComponent<CanvasGroup>(); // To animate alpha independently

            // Left Plate Neon Borders: top and bottom high-brightness neon lines
            var leftTopLine = new GameObject("TopBorder");
            leftTopLine.transform.SetParent(leftPlateGO.transform, false);
            var ltlRt = leftTopLine.AddComponent<RectTransform>();
            ltlRt.anchorMin = new Vector2(0f, 1f);
            ltlRt.anchorMax = new Vector2(1f, 1f);
            ltlRt.pivot = new Vector2(0.5f, 1f);
            ltlRt.anchoredPosition = Vector2.zero;
            ltlRt.sizeDelta = new Vector2(0f, 5f); // 5px neon border
            var ltlImg = leftTopLine.AddComponent<UnityEngine.UI.Image>();
            ltlImg.color = new Color(1.0f, 0.25f, 0.35f, 1.0f); // Bright neon red

            var leftBotLine = new GameObject("BottomBorder");
            leftBotLine.transform.SetParent(leftPlateGO.transform, false);
            var lblRt = leftBotLine.AddComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0f, 0f);
            lblRt.anchorMax = new Vector2(1f, 0f);
            lblRt.pivot = new Vector2(0.5f, 0f);
            lblRt.anchoredPosition = Vector2.zero;
            lblRt.sizeDelta = new Vector2(0f, 5f);
            var lblImg = leftBotLine.AddComponent<UnityEngine.UI.Image>();
            lblImg.color = new Color(1.0f, 0.25f, 0.35f, 1.0f); // Bright neon red

            var leftTextGO = new GameObject("Text");
            leftTextGO.transform.SetParent(leftPlateGO.transform, false);
            var leftTxtRt = leftTextGO.AddComponent<RectTransform>();
            leftTxtRt.anchorMin = Vector2.zero;
            leftTxtRt.anchorMax = Vector2.one;
            leftTxtRt.offsetMin = new Vector2(40f, 0f);
            leftTxtRt.offsetMax = Vector2.zero;
            var leftTxt = leftTextGO.AddComponent<UnityEngine.UI.Text>();
            leftTxt.font = safeFont;
            leftTxt.fontSize = 36;
            leftTxt.fontStyle = FontStyle.Bold;
            leftTxt.alignment = TextAnchor.MiddleLeft;
            leftTxt.color = new Color(1f, 0.45f, 0.5f); // Neon red text
            leftTxt.text = "ENEMY TEAM";

            // Right nameplate (Player side)
            var rightPlateGO = new GameObject("RightPlate");
            rightPlateGO.transform.SetParent(_introUIInstance.transform, false);
            var rightRt = rightPlateGO.AddComponent<RectTransform>();
            rightRt.anchorMin = new Vector2(1f, 0.5f);
            rightRt.anchorMax = new Vector2(1f, 0.5f);
            rightRt.pivot = new Vector2(1f, 0.5f);
            rightRt.anchoredPosition = new Vector2(600f, 0f);
            rightRt.sizeDelta = new Vector2(550f, 120f);
            var rightImg = rightPlateGO.AddComponent<UnityEngine.UI.Image>();
            rightImg.color = new Color(0.06f, 0.16f, 0.32f, 0.85f); // Deep modern dark neon blue/cyan
            rightPlateGO.AddComponent<CanvasGroup>(); // To animate alpha independently

            // Right Plate Neon Borders: top and bottom high-brightness neon lines
            var rightTopLine = new GameObject("TopBorder");
            rightTopLine.transform.SetParent(rightPlateGO.transform, false);
            var rtlRt = rightTopLine.AddComponent<RectTransform>();
            rtlRt.anchorMin = new Vector2(0f, 1f);
            rtlRt.anchorMax = new Vector2(1f, 1f);
            rtlRt.pivot = new Vector2(0.5f, 1f);
            rtlRt.anchoredPosition = Vector2.zero;
            rtlRt.sizeDelta = new Vector2(0f, 5f);
            var rtlImg = rightTopLine.AddComponent<UnityEngine.UI.Image>();
            rtlImg.color = new Color(0.2f, 0.85f, 1.0f, 1.0f); // Bright neon cyan

            var rightBotLine = new GameObject("BottomBorder");
            rightBotLine.transform.SetParent(rightPlateGO.transform, false);
            var rblRt = rightBotLine.AddComponent<RectTransform>();
            rblRt.anchorMin = new Vector2(0f, 0f);
            rblRt.anchorMax = new Vector2(1f, 0f);
            rblRt.pivot = new Vector2(0.5f, 0f);
            rblRt.anchoredPosition = Vector2.zero;
            rblRt.sizeDelta = new Vector2(0f, 5f);
            var rblImg = rightBotLine.AddComponent<UnityEngine.UI.Image>();
            rblImg.color = new Color(0.2f, 0.85f, 1.0f, 1.0f); // Bright neon cyan

            var rightTextGO = new GameObject("Text");
            rightTextGO.transform.SetParent(rightPlateGO.transform, false);
            var rightTxtRt = rightTextGO.AddComponent<RectTransform>();
            rightTxtRt.anchorMin = Vector2.zero;
            rightTxtRt.anchorMax = Vector2.one;
            rightTxtRt.offsetMin = Vector2.zero;
            rightTxtRt.offsetMax = new Vector2(-40f, 0f);
            var rightTxt = rightTextGO.AddComponent<UnityEngine.UI.Text>();
            rightTxt.font = safeFont;
            rightTxt.fontSize = 36;
            rightTxt.fontStyle = FontStyle.Bold;
            rightTxt.alignment = TextAnchor.MiddleRight;
            rightTxt.color = new Color(0.5f, 0.85f, 1f); // Neon blue text
            rightTxt.text = "PLAYER TEAM";

            // Flash overlay for climax juice (Created BEFORE RuneClash so it draws UNDER the VS text)
            var flashGO = new GameObject("FlashOverlay");
            flashGO.transform.SetParent(_introUIInstance.transform, false);
            var flashRt = flashGO.AddComponent<RectTransform>();
            flashRt.anchorMin = Vector2.zero;
            flashRt.anchorMax = Vector2.one;
            flashRt.offsetMin = flashRt.offsetMax = Vector2.zero;
            var flashImg = flashGO.AddComponent<UnityEngine.UI.Image>();
            flashImg.color = new Color(1f, 1f, 1f, 0f);

            // "VS" title banner (Created AFTER FlashOverlay so it renders ON TOP of the white flash)
            var clashGO = new GameObject("RuneClash");
            clashGO.transform.SetParent(_introUIInstance.transform, false);
            var clashRt = clashGO.AddComponent<RectTransform>();
            clashRt.anchorMin = new Vector2(0.5f, 0.5f);
            clashRt.anchorMax = new Vector2(0.5f, 0.5f);
            clashRt.pivot = new Vector2(0.5f, 0.5f);
            clashRt.anchoredPosition = Vector2.zero;
            clashRt.sizeDelta = new Vector2(600f, 300f); // Non-zero sizeDelta prevents text clipping
            clashRt.localScale = Vector3.zero;

            // Add Text first (Graphic)
            var clashTxt = clashGO.AddComponent<UnityEngine.UI.Text>();
            clashTxt.font = safeFont;
            clashTxt.fontSize = 140; // High-impact font size
            clashTxt.fontStyle = FontStyle.BoldAndItalic;
            clashTxt.alignment = TextAnchor.MiddleCenter;
            clashTxt.color = new Color(1.0f, 0.82f, 0.15f); // Golden metallic style
            clashTxt.horizontalOverflow = HorizontalWrapMode.Overflow; // Guarantees rendering
            clashTxt.verticalOverflow = VerticalWrapMode.Overflow; // Guarantees rendering
            clashTxt.text = "VS";

            // Add Outline and Shadow effects after Text to guarantee perfect graphics rendering
            var clashOutline = clashGO.AddComponent<UnityEngine.UI.Outline>();
            clashOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            clashOutline.effectDistance = new Vector2(6f, -6f);

            var clashShadow = clashGO.AddComponent<UnityEngine.UI.Shadow>();
            clashShadow.effectColor = new Color(0.2f, 0.1f, 0f, 0.5f);
            clashShadow.effectDistance = new Vector2(10f, -10f);

            clashGO.AddComponent<CanvasGroup>();

            // Dynamically query names to make it look exceptionally polished
            if (bootstrapper != null)
            {
                // Enemy Name (Left)
                if (bootstrapper.enemyTeam != null && bootstrapper.enemyTeam.childCount > 0)
                {
                    var slot = bootstrapper.enemyTeam.GetChild(0).GetComponent<EnemySlot>();
                    if (slot != null && slot.data != null)
                    {
                        leftTxt.text = slot.data.enemyName.ToUpper();
                        if (bootstrapper.enemyTeam.childCount > 1)
                        {
                            leftTxt.text += " GROUP";
                        }
                    }
                }
                
                // Player Name (Right)
                if (bootstrapper.playerTeam != null && bootstrapper.playerTeam.childCount > 0)
                {
                    var slot = bootstrapper.playerTeam.GetChild(0).GetComponent<CharacterSlot>();
                    if (slot != null && slot.data != null)
                    {
                        rightTxt.text = slot.data.characterName.ToUpper();
                    }
                }
            }
        }

        private void AlignAnchorsDynamically(CombatArenaAssembler assembler)
        {
            if (assembler == null || !assembler.IsReady) return;
            if (!useDynamicCameraAnchors) return;

            // 1. Determine Player position (world space)
            Vector3 playerWorldPos = Vector3.zero;
            bool foundPlayer = false;

            if (assembler.PlayerSlots != null && assembler.PlayerSlots.Count > 0)
            {
                var slot = assembler.PlayerSlots[0];
                if (slot != null)
                {
                    playerWorldPos = slot.childCount > 0 && slot.GetChild(0) != null
                        ? slot.GetChild(0).position
                        : slot.position;
                    foundPlayer = true;
                }
            }

            if (!foundPlayer)
            {
                playerWorldPos = assembler.PlayerRoot != null 
                    ? assembler.PlayerRoot.position 
                    : assembler.transform.position + Vector3.back * 5f;
            }

            // 2. Determine Enemy team center (world space)
            Vector3 enemyCenterWorld = Vector3.zero;
            int activeEnemies = 0;

            if (assembler.EnemySlots != null && assembler.EnemySlots.Count > 0)
            {
                foreach (var slot in assembler.EnemySlots)
                {
                    if (slot != null)
                    {
                        Vector3 pos = slot.childCount > 0 && slot.GetChild(0) != null
                            ? slot.GetChild(0).position
                            : slot.position;
                        enemyCenterWorld += pos;
                        activeEnemies++;
                    }
                }
            }

            if (activeEnemies > 0)
            {
                enemyCenterWorld /= activeEnemies;
            }
            else
            {
                enemyCenterWorld = assembler.EnemyRoot != null
                    ? assembler.EnemyRoot.position
                    : assembler.transform.position + Vector3.forward * 5f;
            }

            // Define center of interest heights (e.g. eye/chest level)
            Vector3 playerTarget = playerWorldPos + Vector3.up * 1.0f;
            Vector3 enemyTarget = enemyCenterWorld + Vector3.up * 1.0f;
            Vector3 fieldCenterTarget = (playerWorldPos + enemyCenterWorld) * 0.5f + Vector3.up * 1.0f;

            // Direction vector from player side to enemy side
            Vector3 facing = (enemyCenterWorld - playerWorldPos).normalized;
            if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
            Vector3 right = Vector3.Cross(facing, Vector3.up).normalized;

            // A. Dynamically position Player Intro Camera Anchor (close, dramatic view framing the player)
            if (assembler.IntroPlayerAnchor != null)
            {
                // Positioned from the enemy/side direction looking back at the player
                Vector3 camPos = playerWorldPos + facing * 2.8f + right * 1.2f + Vector3.up * 0.6f;
                assembler.IntroPlayerAnchor.position = camPos;
                assembler.IntroPlayerAnchor.rotation = Quaternion.LookRotation(playerTarget - camPos);
                
                // Update CinemachineCamera component properties if attached
                var vcam = assembler.IntroPlayerAnchor.GetComponent<Unity.Cinemachine.CinemachineCamera>();
                if (vcam != null)
                {
                    vcam.transform.position = camPos;
                    vcam.transform.rotation = Quaternion.LookRotation(playerTarget - camPos);
                }
            }

            // B. Dynamically position Enemy Intro Camera Anchor (medium view framing the enemy group)
            if (assembler.IntroEnemyAnchor != null)
            {
                // Positioned from the player/side direction looking at the enemy group
                Vector3 camPos = enemyCenterWorld - facing * 3.5f - right * 1.5f + Vector3.up * 0.8f;
                assembler.IntroEnemyAnchor.position = camPos;
                assembler.IntroEnemyAnchor.rotation = Quaternion.LookRotation(enemyTarget - camPos);

                var vcam = assembler.IntroEnemyAnchor.GetComponent<Unity.Cinemachine.CinemachineCamera>();
                if (vcam != null)
                {
                    vcam.transform.position = camPos;
                    vcam.transform.rotation = Quaternion.LookRotation(enemyTarget - camPos);
                }
            }

            // C. Dynamically position Gameplay Camera Anchor (angled wide combat view)
            if (assembler.CameraGameplayAnchor != null)
            {
                Vector3 camPos = fieldCenterTarget - facing * 6.5f - right * 4.5f + Vector3.up * 5.0f;
                assembler.CameraGameplayAnchor.position = camPos;
                assembler.CameraGameplayAnchor.rotation = Quaternion.LookRotation(fieldCenterTarget - camPos);

                var vcam = assembler.CameraGameplayAnchor.GetComponent<Unity.Cinemachine.CinemachineCamera>();
                if (vcam != null)
                {
                    vcam.transform.position = camPos;
                    vcam.transform.rotation = Quaternion.LookRotation(fieldCenterTarget - camPos);
                }
            }
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
