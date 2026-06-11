using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Manages victory and defeat screen sequences.
    /// In victory: plays a cinematic Timeline and then shows the WinScreen UI with a light animation.
    /// In defeat: instantly shows the LoseScreen UI ("en seco").
    /// </summary>
    public class VictorySequencer : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("Prefab for the Win Screen UI.")]
        [SerializeField] private GameObject _winScreenPrefab;
        [Tooltip("Prefab for the Lose Screen UI.")]
        [SerializeField] private GameObject _loseScreenPrefab;

        [Header("Timeline")]
        [Tooltip("PlayableDirector for the player character victory timeline.")]
        [SerializeField] private PlayableDirector _victoryTimelineDirector;

        [Header("Time Dilation / Slow-Mo")]
        [SerializeField] private float _victorySlowMoScale    = 0.30f;
        [SerializeField] private float _victorySlowMoDuration = 1.20f;

        [Header("Last Hit Drama (signal 1: Outro_LastHitDrama)")]
        [Tooltip("Time scale at the instant of the kill (hit-stop / freeze frame). Lower = harder freeze.")]
        [SerializeField] private float _lastHitFreezeScale = 0.06f;
        [Tooltip("FOV reduction applied to the gameplay camera to zoom in on the enemies during the drama beat.")]
        [SerializeField] private float _lastHitZoomFovDelta = 20f;
        [Tooltip("How long the hard hit-stop freeze holds before easing into slow-mo so the enemy death/fall plays out. The camera does NOT cut here (that is _beatCameraDelay).")]
        [SerializeField] private float _freezeHoldDuration = 0.30f;

        [Header("Outro Pacing (cinematic — real-time seconds)")]
        [Tooltip("Beat 2: how long the frozen last-hit holds (on the enemies) before the camera cuts to the outro shots.")]
        [SerializeField] private float _beatCameraDelay = 1.2f;
        [Tooltip("Beat 3: when the winning character plays the victory pose.")]
        [SerializeField] private float _beatVictoryAnimDelay = 2.4f;
        [Tooltip("Beat 4: when the victory screen appears (also restores normal time).")]
        [SerializeField] private float _beatVictoryScreenDelay = 6.0f;
        [Tooltip("Slow-mo time scale the hit-stop eases into after the freeze, held until the victory screen.")]
        [Range(0.05f, 1f)] [SerializeField] private float _outroSlowMoScale = 0.40f;
        [Tooltip("Seconds to ramp from the hit-stop freeze up to the slow-mo scale.")]
        [SerializeField] private float _timeRampDuration = 0.50f;

        [Header("Outro Camera Framing (player shots)")]
        [Tooltip("Height of the player's face — the look-at target for both player shots and the end-shot camera height.")]
        [SerializeField] private float _outroPlayerFaceHeight = 1.6f;
        [Tooltip("Start shot: camera height above the player's feet.")]
        [SerializeField] private float _outroStartCamHeight = 2.0f;
        [Tooltip("Start shot: camera distance in front of the player.")]
        [SerializeField] private float _outroStartCamDistance = 4.0f;
        [Tooltip("End shot: camera height. Set near the face height for a level close-up on the face.")]
        [SerializeField] private float _outroEndCamHeight = 1.6f;
        [Tooltip("End shot: camera distance in front of the player.")]
        [SerializeField] private float _outroEndCamDistance = 2.4f;
        [Tooltip("Duration (real seconds) of the smooth camera travel from the start pose to the face close-up.")]
        [SerializeField] private float _outroTravelDuration = 2.5f;
        [Tooltip("FOV the camera eases to during the outro dolly. The drama beat narrows it to 10; this opens it back up.")]
        [SerializeField] private float _outroCamFov = 22f;
        [Tooltip("Optional. If assigned, the dolly STARTS at this Transform's exact world pose instead of the computed start pose.")]
        [SerializeField] private Transform _outroStartCamAnchor;
        [Tooltip("Optional. If assigned, the dolly ENDS at this Transform's exact world pose (position + rotation) instead of the computed face close-up.")]
        [SerializeField] private Transform _outroEndCamAnchor;

        [Header("Timing (Victory UI animation)")]
        [SerializeField] private float _fondoDuration  = 0.70f;
        [SerializeField] private float _textDelay      = 0.85f;
        [SerializeField] private float _textDuration   = 0.55f;
        [SerializeField] private float _buttonDelay    = 1.15f;
        [SerializeField] private float _buttonDuration = 0.45f;
        [SerializeField] private float _breathePeriod  = 3.00f;
        [SerializeField] private float _breatheScale   = 0.015f;

        [Header("Defeat UI animation")]
        [Tooltip("Seconds the Lose screen bounces in (scale overshoot).")]
        [SerializeField] private float _defeatBounceDuration   = 0.55f;
        [Tooltip("Container scale the Lose screen starts at before bouncing up to full size.")]
        [SerializeField] private float _defeatBounceStartScale = 0.6f;

        [Header("Scrim")]
        [SerializeField] private float _scrimAlpha = 0.78f;

        // Runtime references populated from instantiated prefabs
        private GameObject    _activeInstance;
        private Canvas        _canvas;
        private RectTransform _containerRT;
        private RectTransform _fondoRT;
        private RectTransform _scrimRT;
        private RectTransform _textoRT;
        private RectTransform _botonContinuarRT;
        private RectTransform _botonReintentarRT;

        private Image         _fondoImg;
        private Image         _scrimImg;
        private Image         _textoImg;
        private Image         _botonContinuarImg;
        private Image         _botonReintentarImg;

        // Resting transforms captured from prefab before any animation
        private Vector2 _textoRestPos;
        private Vector2 _botonContinuarRestPos;
        private Vector2 _botonReintentarRestPos;
        private Vector3 _containerRestScale;
        private Vector3 _textoRestScale;

        private Action    _onContinue;
        private Action    _onRetry;
        private Coroutine _breatheRoutine;
        private bool      _isVictory;

        private GameObject _winScreenInstance;
        private GameObject _flashOverlayInstance;
        private System.Collections.Generic.List<Unity.Cinemachine.CinemachineCamera> _dynamicVcams = new();

        // Player camera dolly poses (captured when the runtime outro cameras are built).
        private Vector3    _camStartPos;
        private Vector3    _camEndPos;
        private Quaternion _camEndRot;
        private Vector3    _camFacePoint;
        private bool       _hasCamPoses;

        private CombatBootstrapper _bootstrapper;
        private CombatPresenterBase _hudPresenter;
        private Volume              _volume;

        public void Initialize(CombatBootstrapper bootstrapper, CombatPresenterBase hudPresenter)
        {
            _bootstrapper = bootstrapper;
            _hudPresenter = hudPresenter;
        }

        private CombatBootstrapper Bootstrapper
        {
            get
            {
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
                return _bootstrapper;
            }
        }

        private CombatPresenterBase HudPresenter
        {
            get
            {
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
                return _hudPresenter;
            }
        }

        private Volume VolumeInstance
        {
            get
            {
                if (_volume == null)
                {
                    _volume = FindFirstObjectByType<Volume>();
                    if (_volume == null)
                    {
                        #pragma warning disable CS0618
                        _volume = FindObjectOfType<Volume>();
                        #pragma warning restore CS0618
                    }
                }
                return _volume;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Legacy fallback to keep the compiler happy if other files call Play directly.
        /// </summary>
        public void Play(Action onContinue)
        {
            PlayOutro(true, onContinue);
        }

        /// <summary>
        /// Starts the appropriate outro sequence based on combat outcome.
        /// </summary>
        public void PlayOutro(bool won, Action onContinue, Action onRetry = null)
        {
            _onContinue = onContinue;
            _onRetry = onRetry ?? FallbackRetryAction;
            _isVictory = won;

            Debug.Log($"[VictorySequencer] PlayOutro: won={won}, hasTimeline={_victoryTimelineDirector != null}");

            if (_isVictory)
            {
                PlayVictoryOutro(onContinue, onRetry);
            }
            else
            {
                StartCoroutine(DefeatSequenceRoutine());
            }
        }

        /// <summary>
        /// Public entry point called by CombatBootstrapper on the exact last hit frame.
        /// </summary>
        public void PlayVictoryOutro(Action onContinue, Action onRetry = null)
        {
            _onContinue = onContinue;
            _onRetry = onRetry ?? FallbackRetryAction;
            _isVictory = true;

            Debug.Log($"[VictorySequencer] PlayVictoryOutro: hasTimeline={_victoryTimelineDirector != null}");

            StartCoroutine(PlayVictoryOutroTimelineCoroutine());
        }

        /// <summary>
        /// Coroutine triggered by PlayVictoryOutro on the Last Hit frame.
        /// Binds the dynamic tracks, applies time scale dilation, and plays the victory Timeline.
        /// </summary>
        public IEnumerator PlayVictoryOutroTimelineCoroutine()
        {
            Debug.Log("[VictorySequencer] Starting Victory Outro Timeline sequence.");

            var mainCamera = Camera.main;
            var brain = mainCamera != null ? mainCamera.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
            var camController = mainCamera != null ? mainCamera.GetComponent<CombatCameraController>() : null;

            // 1. Disable manual camera controller so it stops overriding Cinemachine
            if (camController != null) camController.enabled = false;

            // 2. Keep the CinemachineBrain DISABLED during the last-hit drama beat so the gameplay
            // camera stays live and EmitOutroStart's zoom-on-enemies is visible. The brain is
            // re-enabled at beat 3 (EmitOutroSkipToCameraAnchor), handing the camera to the
            // outro Cinemachine Track shots.
            if (brain != null) brain.enabled = false;

            // 3. Find winning player pawn and bind tracks
            Transform playerPawn = FindWinningPlayerPawn();
            var animator = playerPawn != null ? playerPawn.GetComponentInChildren<Animator>() : null;

            // 4. Instantiate the WinScreen UI (initially inactive, Timeline Activation Track will activate it)
            if (_winScreenPrefab != null)
            {
                _winScreenInstance = Instantiate(_winScreenPrefab, transform);
                _winScreenInstance.SetActive(false);
                WireRefsFromInstance(_winScreenInstance);
            }
            else
            {
                Debug.LogError("[VictorySequencer] _winScreenPrefab not assigned!");
            }

            // 5. Create the White Flash overlay programmatically so the Timeline can bind it
            CreateFlashOverlay();

            // 6. Create dynamic Virtual Cameras for camera blend/pan relative to player position
            CreateDynamicVirtualCameras(playerPawn);

            // 7. Bind all tracks dynamically (Cinemachine, Animator, HUD, PP, Flash, WinScreen)
            if (_victoryTimelineDirector != null)
            {
                _victoryTimelineDirector.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;

                // Configure runtime signal emitters and receiver callbacks
                SetupRuntimeSignals(_victoryTimelineDirector);

                BindVictoryOutroTimelineTracks(_victoryTimelineDirector, animator, brain);

                // The Cinemachine timeline is NOT played: its delayed-brain handoff skipped the
                // Start->End travel (camera jumped straight to the End shot). The player camera move is
                // the code dolly fired at the camera beat (EmitOutroSkipToCameraAnchor -> DollyPlayerCamera).

                // Let the scheduled beats run; the victory screen beat shows the UI.
                yield return new WaitForSecondsRealtime(_beatVictoryScreenDelay + 1.0f);
            }
            else
            {
                // Fallback if no timeline director is assigned
                Time.timeScale = _victorySlowMoScale;
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
                yield return new WaitForSecondsRealtime(_victorySlowMoDuration);
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;

                if (_winScreenInstance != null)
                {
                    _winScreenInstance.SetActive(true);
                    StartCoroutine(VictorySequenceRoutine());
                }
                else
                {
                    _onContinue?.Invoke();
                }
            }
        }

        private void CreateFlashOverlay()
        {
            if (_flashOverlayInstance != null) Destroy(_flashOverlayInstance);

            _flashOverlayInstance = new GameObject("VictoryOutro_FlashCanvas");
            _flashOverlayInstance.transform.SetParent(transform, false);

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

        private void CreateDynamicVirtualCameras(Transform playerPawn)
        {
            foreach (var cam in _dynamicVcams)
            {
                if (cam != null) Destroy(cam.gameObject);
            }
            _dynamicVcams.Clear();

            // 1. Create Enemy Camera looking at the defeated enemy
            Transform enemyPawn = FindDefeatedEnemyPawn();
            if (enemyPawn != null)
            {
                Vector3 enemyPos = enemyPawn.position;
                Vector3 enemyForward = enemyPawn.forward;
                Vector3 enemyCamPos = enemyPos + enemyForward * 3.0f + Vector3.up * 1.5f;

                var enemyGO = new GameObject("OutroCamera_Enemy");
                var enemyVcam = enemyGO.AddComponent<Unity.Cinemachine.CinemachineCamera>();
                enemyGO.transform.position = enemyCamPos;
                enemyGO.transform.LookAt(enemyPos + Vector3.up * 1.0f);
                enemyVcam.Priority = 10;
                enemyVcam.LookAt = enemyPawn;
                enemyVcam.Follow = enemyPawn;
                _dynamicVcams.Add(enemyVcam);
                enemyGO.transform.SetParent(transform);
            }

            if (playerPawn != null)
            {
                Vector3 pawnPos = playerPawn.position;
                Vector3 pawnForward = playerPawn.forward;

                // Look at the player's face for both shots so the framing centers on the face,
                // not the feet/chest. The end shot also sits at face height for a level close-up.
                Vector3 facePoint = pawnPos + Vector3.up * _outroPlayerFaceHeight;

                Vector3    startPos = pawnPos + pawnForward * _outroStartCamDistance + Vector3.up * _outroStartCamHeight;
                Vector3    endPos   = pawnPos + pawnForward * _outroEndCamDistance   + Vector3.up * _outroEndCamHeight;
                Quaternion endRot   = Quaternion.LookRotation(facePoint - endPos);

                // The end shot ideally sits where the intro framed the player team (a wider beauty shot,
                // not a tight face close-up). Use that anchor's exact pose when the arena exposes it.
                Transform introAnchor = ResolveIntroPlayerAnchor();
                if (_outroEndCamAnchor == null && introAnchor != null)
                {
                    endPos = introAnchor.position;
                    endRot = introAnchor.rotation;
                }

                // Capture poses for the code-driven dolly (EmitOutroSkipToCameraAnchor → DollyPlayerCamera).
                _camStartPos  = startPos;
                _camEndPos    = endPos;
                _camEndRot    = endRot;
                _camFacePoint = facePoint;
                _hasCamPoses  = true;

                var startGO = new GameObject("OutroCamera_Start");
                var startVcam = startGO.AddComponent<Unity.Cinemachine.CinemachineCamera>();
                startGO.transform.position = startPos;
                startGO.transform.LookAt(facePoint);
                startVcam.Priority = 10;
                startVcam.LookAt = playerPawn;
                startVcam.Follow = playerPawn;
                _dynamicVcams.Add(startVcam);
                startGO.transform.SetParent(transform);

                var endGO = new GameObject("OutroCamera_End");
                var endVcam = endGO.AddComponent<Unity.Cinemachine.CinemachineCamera>();
                endGO.transform.position = endPos;
                endGO.transform.rotation = endRot;
                endVcam.Priority = 10;
                endVcam.LookAt = playerPawn;
                endVcam.Follow = playerPawn;
                _dynamicVcams.Add(endVcam);
                endGO.transform.SetParent(transform);
            }
        }

        private Transform FindDefeatedEnemyPawn()
        {
            var bootstrapper = FindFirstObjectByType<CombatBootstrapper>();
            if (bootstrapper == null)
            {
                #pragma warning disable CS0618
                bootstrapper = FindObjectOfType<CombatBootstrapper>();
                #pragma warning restore CS0618
            }

            if (bootstrapper != null && bootstrapper.enemyTeam != null && bootstrapper.enemyTeam.childCount > 0)
            {
                return bootstrapper.enemyTeam.GetChild(0);
            }
            return null;
        }

        /// <summary>The intro's player-facing camera anchor (CamIntro_Player), resolved at runtime from
        /// the arena. Null until the arena is assembled or if the layout has no such anchor.</summary>
        private Transform ResolveIntroPlayerAnchor()
        {
            var bootstrapper = FindFirstObjectByType<CombatBootstrapper>();
            if (bootstrapper == null)
            {
                #pragma warning disable CS0618
                bootstrapper = FindObjectOfType<CombatBootstrapper>();
                #pragma warning restore CS0618
            }
            return bootstrapper != null && bootstrapper.arenaAssembler != null
                ? bootstrapper.arenaAssembler.IntroPlayerAnchor
                : null;
        }

        // ── Outro Signal Wiring ─────────────────────────────────────────────────────

        /// <summary>
        /// Wires the SignalReceiver to the emitters that are AUTHORED on the outro timeline's
        /// Signal Track. The emitters (and their .signal assets) live in the asset and define the
        /// ordered outro flow; this method only attaches the reaction callbacks. It does NOT create
        /// or delete markers — the timeline asset is the source of truth.
        /// Mapping (by SignalAsset name):
        ///   Outro_LastHitDrama       -> EmitOutroStart              (slow-mo, flash, PP, hide combat UI)
        ///   Outro_SkipToCameraAnchor -> EmitOutroSkipToCameraAnchor (cut to runtime outro camera anchor)
        ///   Outro_VictoryAnimation   -> EmitOutroCharacterReveal    (character victory pose)
        ///   Outro_VictoryScreen      -> EmitOutroVictoryScreen      (reveal victory screen UI)
        /// </summary>
        private void SetupRuntimeSignals(PlayableDirector director)
        {
            if (director == null || director.playableAsset == null) return;

            var timeline = director.playableAsset as TimelineAsset;
            if (timeline == null) return;

            // Locate the authored Signal Track. Do not create one — emitters are authored in the asset.
            SignalTrack signalTrack = null;
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is SignalTrack sigTrack)
                {
                    signalTrack = sigTrack;
                    break;
                }
            }

            if (signalTrack == null)
            {
                Debug.LogWarning("[VictorySequencer] No SignalTrack found on the outro timeline — outro emitters will not fire.");
                return;
            }

            // Read the authored emitters (time + signal name) and schedule each reaction directly in
            // real time. Timeline's SignalReceiver delivery is unreliable for a receiver added at
            // runtime (notifications never reach it), so we drive the authored beats ourselves. The
            // timeline asset is still the source of truth for the order/timing of the outro flow, and
            // its Cinemachine Track still drives the camera shots independently.
            int scheduled = 0;
            foreach (var marker in signalTrack.GetMarkers())
            {
                if (!(marker is SignalEmitter emitter) || emitter.asset == null) continue;

                UnityEngine.Events.UnityAction action = ResolveOutroSignalAction(emitter.asset.name);
                if (action == null)
                {
                    Debug.LogWarning($"[VictorySequencer] Unmapped outro signal '{emitter.asset.name}' — skipped.");
                    continue;
                }

                // Timing comes from the inspector pacing fields (cinematic + tunable), not the asset
                // marker times — the asset still defines which beats exist and in what order.
                StartCoroutine(FireOutroBeat(ResolveOutroBeatTime(emitter.asset.name), emitter.asset.name, action));
                scheduled++;
            }

            Debug.Log($"[VictorySequencer] Scheduled {scheduled} authored outro beat(s) from the Signal Track.");
        }

        /// <summary>Fires one authored outro beat at its real-time offset (matches the timeline's UnscaledGameTime clock).</summary>
        private IEnumerator FireOutroBeat(float delay, string signalName, UnityEngine.Events.UnityAction action)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            Debug.Log($"[VictorySequencer] Outro beat: {signalName} @ {delay:0.###}s");
            action.Invoke();
        }

        /// <summary>Maps an authored outro SignalAsset name to its reaction callback.</summary>
        private UnityEngine.Events.UnityAction ResolveOutroSignalAction(string signalName)
        {
            switch (signalName)
            {
                case "Outro_LastHitDrama":       return EmitOutroStart;
                case "Outro_SkipToCameraAnchor": return EmitOutroSkipToCameraAnchor;
                case "Outro_VictoryAnimation":   return EmitOutroCharacterReveal;
                case "Outro_VictoryScreen":      return EmitOutroVictoryScreen;
                default:                         return null;
            }
        }

        /// <summary>Real-time offset for each outro beat, from the inspector pacing fields.</summary>
        private float ResolveOutroBeatTime(string signalName)
        {
            switch (signalName)
            {
                case "Outro_LastHitDrama":       return 0f;
                case "Outro_SkipToCameraAnchor": return _beatCameraDelay;
                case "Outro_VictoryAnimation":   return _beatVictoryAnimDelay;
                case "Outro_VictoryScreen":      return _beatVictoryScreenDelay;
                default:                         return 0f;
            }
        }

        // ── Outro Signal Reactions (called in authored order) ───────────────────────

        // 1. Last hit drama: zoom the gameplay camera onto the enemies, partial-freeze the time
        // scale, punch in post-processing, fire the screen flash (and hide the combat UI).
        // The gameplay camera is still live here — the brain/Cinemachine handoff happens at beat 3
        // (EmitOutroSkipToCameraAnchor), so this zoom is visible before the outro shots take over.
        public void EmitOutroStart()
        {
            Debug.Log("[VictorySequencer] Signal: EmitOutroStart (zoom enemies, partial freeze, PP, flash, hide combat UI)");

            // Zoom the gameplay camera in on the enemies
            ZoomGameplayCameraOnEnemies();

            // Partial freeze
            Time.timeScale = _lastHitFreezeScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            // Deactivate ALL combat UI: HUD (cards, action slots, round, gauge) + world-space HP bars.
            HideAllCombatUI();

            // Screen flash
            TriggerFlash();

            // Post-processing punch-in
            SetPostProcessingWeight(1f);

            // Hold the hard freeze briefly, then ease into slow-mo so the enemy death/fall plays out
            // (and the pawn disappears) BEFORE the camera cuts to the player at _beatCameraDelay.
            StartCoroutine(FreezeThenSlowMo());
        }

        private IEnumerator FreezeThenSlowMo()
        {
            if (_freezeHoldDuration > 0f) yield return new WaitForSecondsRealtime(_freezeHoldDuration);
            yield return RampTimeScale(_lastHitFreezeScale, _outroSlowMoScale, _timeRampDuration);
        }

        /// <summary>
        /// Zooms the live gameplay camera (Camera.main) in on the enemy team and aims at it.
        /// Runs while the CinemachineBrain is still disabled (during the drama beat) so the direct
        /// transform/FOV override holds until the outro Cinemachine shots take over at beat 3.
        /// </summary>
        private void ZoomGameplayCameraOnEnemies()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 focus = GetEnemyFocusPoint();
            Vector3 dir = focus - cam.transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                cam.transform.rotation = Quaternion.LookRotation(dir);

            cam.fieldOfView = Mathf.Max(10f, cam.fieldOfView - _lastHitZoomFovDelta);
        }

        /// <summary>Centroid of the active enemy pawns (head height), with sensible fallbacks.</summary>
        private Vector3 GetEnemyFocusPoint()
        {
            var bootstrapper = FindFirstObjectByType<CombatBootstrapper>();
            if (bootstrapper == null)
            {
                #pragma warning disable CS0618
                bootstrapper = FindObjectOfType<CombatBootstrapper>();
                #pragma warning restore CS0618
            }

            if (bootstrapper != null && bootstrapper.enemyTeam != null && bootstrapper.enemyTeam.childCount > 0)
            {
                Vector3 sum = Vector3.zero;
                int n = 0;
                for (int i = 0; i < bootstrapper.enemyTeam.childCount; i++)
                {
                    var c = bootstrapper.enemyTeam.GetChild(i);
                    if (c != null) { sum += c.position; n++; }
                }
                if (n > 0) return sum / n + Vector3.up * 1.2f;
            }

            var enemy = FindDefeatedEnemyPawn();
            if (enemy != null) return enemy.position + Vector3.up * 1.2f;

            if (bootstrapper != null && bootstrapper.arenaAssembler != null && bootstrapper.arenaAssembler.IsReady)
                return bootstrapper.arenaAssembler.FieldCenter;

            var cam = Camera.main;
            return cam != null ? cam.transform.position + cam.transform.forward * 5f : Vector3.zero;
        }

        // 3. Camera handoff. The outro timeline's Cinemachine Track owns the camera via its 3
        // authored shots (enemy -> player_start -> player_end), bound at runtime by
        // BindVictoryOutroTimelineTracks. This beat only guarantees the brain is live so the track
        // can drive the camera; it must NOT bump vcam priority or override DefaultBlend, or it would
        // fight the timeline's authored camera sequence (the cause of the premature camera cut).
        public void EmitOutroSkipToCameraAnchor()
        {
            Debug.Log("[VictorySequencer] Signal: EmitOutroSkipToCameraAnchor (start player camera dolly)");
            // Cut to the start pose, then smoothly ease to the face close-up (end pose).
            StartCoroutine(DollyPlayerCamera());
        }

        /// <summary>
        /// Code-driven player camera move: cuts to the start pose then eases (smoothstep) to the end
        /// pose, both aimed at the player's face. Runs with the CinemachineBrain kept disabled so the
        /// direct transform writes hold — reliable, unlike the delayed-brain Timeline handoff.
        /// </summary>
        private IEnumerator DollyPlayerCamera()
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            if (!_hasCamPoses && _outroStartCamAnchor == null && _outroEndCamAnchor == null) yield break;

            var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null) brain.enabled = false; // keep manual control

            // Resolve start/end poses — an assigned anchor Transform overrides the computed pose.
            Vector3 startPos = _outroStartCamAnchor != null ? _outroStartCamAnchor.position : _camStartPos;
            Quaternion startRot = _outroStartCamAnchor != null
                ? _outroStartCamAnchor.rotation
                : Quaternion.LookRotation(_camFacePoint - startPos);

            Vector3 endPos = _outroEndCamAnchor != null ? _outroEndCamAnchor.position : _camEndPos;
            Quaternion endRot = _outroEndCamAnchor != null ? _outroEndCamAnchor.rotation : _camEndRot;

            float dur = Mathf.Max(0.01f, _outroTravelDuration);
            float startFov = cam.fieldOfView;   // narrowed to ~10 by the drama-beat zoom; open it back up
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                cam.transform.position = Vector3.Lerp(startPos, endPos, k);
                cam.transform.rotation = Quaternion.Slerp(startRot, endRot, k);
                cam.fieldOfView        = Mathf.Lerp(startFov, _outroCamFov, k);
                yield return null;
            }
            cam.transform.position = endPos;
            cam.transform.rotation = endRot;
            cam.fieldOfView        = _outroCamFov;
        }

        private IEnumerator RampTimeScale(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
                Time.timeScale = Mathf.Lerp(from, to, k);
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
                yield return null;
            }
            Time.timeScale = to;
            Time.fixedDeltaTime = 0.02f * to;
        }

        // 4. Character victory pose.
        public void EmitOutroCharacterReveal()
        {
            Debug.Log("[VictorySequencer] Signal: EmitOutroCharacterReveal (Victory Pose)");
            Transform playerPawn = FindWinningPlayerPawn();
            var animator = playerPawn != null ? playerPawn.GetComponentInChildren<Animator>() : null;
            if (animator != null)
            {
                animator.CrossFadeInFixedTime("Victory", 0.2f);
            }
        }

        // 5. Reveal the victory screen UI.
        public void EmitOutroVictoryScreen()
        {
            Debug.Log("[VictorySequencer] Signal: EmitOutroVictoryScreen (Show UI)");
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;

            // The camera shots are done playing under the screen — stop the director so it doesn't
            // keep crawling at the reduced playback speed.
            if (_victoryTimelineDirector != null && _victoryTimelineDirector.state == PlayState.Playing)
                _victoryTimelineDirector.Stop();

            if (_winScreenInstance != null)
            {
                _winScreenInstance.SetActive(true);
                WireRefsFromInstance(_winScreenInstance);
                StartCoroutine(VictorySequenceRoutine());
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ── Outro FX helpers ────────────────────────────────────────────────────────

        private void TriggerFlash()
        {
            if (_flashOverlayInstance == null) return;
            var flashGO = _flashOverlayInstance.transform.Find("FlashOverlay")?.gameObject;
            if (flashGO == null) return;

            var img = flashGO.GetComponent<Image>();
            if (img != null) img.color = Color.white;
            flashGO.SetActive(true);
            StartCoroutine(FlashFadeRoutine(img));
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
            if (img != null && img.gameObject != null) img.gameObject.SetActive(false);
        }

        private void SetPostProcessingWeight(float weight)
        {
            var volume = VolumeInstance;
            if (volume != null) volume.weight = Mathf.Clamp01(weight);
        }

        /// <summary>Fully hides all combat UI: the HUD CanvasGroup (cards, action slots, round, gauge) and the world-space HP bars.</summary>
        private void HideAllCombatUI()
        {
            var hud = HudPresenter;
            if (hud != null) hud.HideAllUI();

            // World-space HP bars are separate billboards (not in the HUD CanvasGroup) — deactivate each.
            var bars = FindObjectsByType<HPBarPresenter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var bar in bars)
                if (bar != null) bar.gameObject.SetActive(false);
        }

        private void OnVictoryTimelineStopped(PlayableDirector director)
        {
            if (_victoryTimelineDirector != null)
            {
                _victoryTimelineDirector.stopped -= OnVictoryTimelineStopped;
            }

            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;

            if (_winScreenInstance != null && !_winScreenInstance.activeSelf)
            {
                _winScreenInstance.SetActive(true);
                WireRefsFromInstance(_winScreenInstance);
                StartCoroutine(VictorySequenceRoutine());
            }
            else if (_winScreenInstance == null)
            {
                _onContinue?.Invoke();
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("[VictorySequencer] Victory Outro Timeline stopped and completed.");
        }

        public void Hide()
        {
            if (_breatheRoutine != null) { StopCoroutine(_breatheRoutine); _breatheRoutine = null; }
            if (_activeInstance != null) { Destroy(_activeInstance); _activeInstance = null; }
            if (_winScreenInstance != null) { Destroy(_winScreenInstance); _winScreenInstance = null; }
            if (_flashOverlayInstance != null) { Destroy(_flashOverlayInstance); _flashOverlayInstance = null; }

            foreach (var cam in _dynamicVcams)
            {
                if (cam != null) Destroy(cam.gameObject);
            }
            _dynamicVcams.Clear();
        }

        // ── Sequences ─────────────────────────────────────────────────────────────

        private IEnumerator VictorySequenceRoutine()
        {
            if (_winScreenInstance == null)
            {
                if (_winScreenPrefab != null)
                {
                    _winScreenInstance = Instantiate(_winScreenPrefab, transform);
                    WireRefsFromInstance(_winScreenInstance);
                }
                else
                {
                    Debug.LogError("[VictorySequencer] _winScreenPrefab not assigned!");
                    _onContinue?.Invoke();
                    yield break;
                }
            }

            _activeInstance = _winScreenInstance;
            _activeInstance.SetActive(true);

            // Play light UI pop-up animation
            SetAlpha(_fondoImg, 0f);
            SetAlpha(_scrimImg, 0f);
            SetAlpha(_textoImg, 0f);
            SetAlpha(_botonContinuarImg, 0f);
            if (_containerRT != null) _containerRT.localScale = _containerRestScale * 1.08f;

            yield return StartCoroutine(AnimateFondo());

            StartCoroutine(AnimateTexto(_textDelay - _fondoDuration));
            StartCoroutine(AnimateButton(_buttonDelay - _fondoDuration));

            yield return new WaitForSecondsRealtime(_buttonDelay - _fondoDuration + _buttonDuration + 0.05f);
            _breatheRoutine = StartCoroutine(BreatheLoop());
        }

        private IEnumerator DefeatSequenceRoutine()
        {
            // Ocultar el HUD
            var hud = HudPresenter;
            if (hud != null) hud.HideAllUI();

            // Instantiate Defeat UI Screen
            if (_loseScreenPrefab != null)
            {
                _activeInstance = Instantiate(_loseScreenPrefab, transform);
                WireRefsFromInstance(_activeInstance);
            }
            else
            {
                Debug.LogError("[VictorySequencer] _loseScreenPrefab not assigned!");
                _onContinue?.Invoke();
                yield break;
            }

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

            // Unlock cursor so player can click buttons immediately
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Bounce in: container scales up with an overshoot (EaseOutBack), elements fade in together.
            float dur = Mathf.Max(0.01f, _defeatBounceDuration);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float n    = Mathf.Clamp01(t / dur);
                float fade = EaseOut(n);
                if (_containerRT != null)
                    _containerRT.localScale = Vector3.LerpUnclamped(
                        _containerRestScale * _defeatBounceStartScale, _containerRestScale, EaseOutBack(n));
                SetAlpha(_fondoImg, fade);
                SetAlpha(_scrimImg, fade * _scrimAlpha);
                SetAlpha(_textoImg,           Mathf.Min(1f, n * 2f));
                SetAlpha(_botonContinuarImg,  Mathf.Min(1f, n * 2f));
                SetAlpha(_botonReintentarImg, Mathf.Min(1f, n * 2f));
                yield return null;
            }

            if (_containerRT != null) _containerRT.localScale = _containerRestScale;
            SetAlpha(_fondoImg, 1f);
            SetAlpha(_scrimImg, _scrimAlpha);
            SetAlpha(_textoImg, 1f);
            SetAlpha(_botonContinuarImg, 1f);
            SetAlpha(_botonReintentarImg, 1f);

            // Idle breath so the screen feels alive while waiting for input.
            if (_breatheRoutine != null) StopCoroutine(_breatheRoutine);
            _breatheRoutine = StartCoroutine(BreatheLoop());
        }

        // ── Track Bindings ────────────────────────────────────────────────────────

        private void BindVictoryOutroTimelineTracks(PlayableDirector director, Animator playerAnimator, Unity.Cinemachine.CinemachineBrain brain)
        {
            if (director == null || director.playableAsset == null) return;

            var timeline = director.playableAsset as TimelineAsset;
            if (timeline == null) return;

            var hudGO = HudPresenter?.gameObject;

            Unity.Cinemachine.CinemachineCamera enemyVcam = _dynamicVcams.Count > 0 ? _dynamicVcams[0] : null;
            Unity.Cinemachine.CinemachineCamera startVcam = _dynamicVcams.Count > 1 ? _dynamicVcams[1] : null;
            Unity.Cinemachine.CinemachineCamera endVcam = _dynamicVcams.Count > 2 ? _dynamicVcams[2] : null;

            GameObject flashOverlayGO = _flashOverlayInstance != null ? _flashOverlayInstance.transform.Find("FlashOverlay")?.gameObject : null;

            foreach (var output in timeline.outputs)
            {
                // A. Cinemachine Track
                if (output.outputTargetType == typeof(Unity.Cinemachine.CinemachineBrain))
                {
                    if (brain != null)
                    {
                        director.SetGenericBinding(output.sourceObject, brain);
                        Debug.Log($"[VictorySequencer] Bound CinemachineBrain to track: {output.streamName}");
                    }

                    if (output.sourceObject is Unity.Cinemachine.CinemachineTrack cinemachineTrack)
                    {
                        var clips = cinemachineTrack.GetClips();
                        int clipIdx = 0;
                        foreach (var clip in clips)
                        {
                            var shot = clip.asset as Unity.Cinemachine.CinemachineShot;
                            if (shot != null)
                            {
                                string clipName = clip.displayName.ToLower();
                                if (clipName.Contains("enemy"))
                                {
                                    if (enemyVcam != null)
                                        director.SetReferenceValue(shot.VirtualCamera.exposedName, enemyVcam);
                                }
                                else if (clipName.Contains("start") || clipName.Contains("1"))
                                {
                                    if (startVcam != null)
                                        director.SetReferenceValue(shot.VirtualCamera.exposedName, startVcam);
                                }
                                else if (clipName.Contains("end") || clipName.Contains("2"))
                                {
                                    if (endVcam != null)
                                        director.SetReferenceValue(shot.VirtualCamera.exposedName, endVcam);
                                }
                                else
                                {
                                    if (clipIdx == 0 && enemyVcam != null)
                                        director.SetReferenceValue(shot.VirtualCamera.exposedName, enemyVcam);
                                    else if (clipIdx == 1 && startVcam != null)
                                        director.SetReferenceValue(shot.VirtualCamera.exposedName, startVcam);
                                    else if (clipIdx == 2 && endVcam != null)
                                        director.SetReferenceValue(shot.VirtualCamera.exposedName, endVcam);
                                }
                            }
                            clipIdx++;
                        }
                    }
                }
                // B. Animator Track (Players)
                else if (output.outputTargetType == typeof(Animator))
                {
                    string name = output.streamName.ToLower();
                    if (name.Contains("player") || name.Contains("hero") || name.Contains("character"))
                    {
                        if (playerAnimator != null)
                        {
                            director.SetGenericBinding(output.sourceObject, playerAnimator);
                            Debug.Log($"[VictorySequencer] Bound Player Animator to track: {output.streamName}");
                        }
                    }
                }
                // C. Activation Track
                else if (output.outputTargetType == typeof(GameObject))
                {
                    string name = output.streamName.ToLower();
                    if (name.Contains("hud") || name.Contains("ui"))
                    {
                        if (hudGO != null)
                        {
                            director.SetGenericBinding(output.sourceObject, hudGO);
                            Debug.Log($"[VictorySequencer] Bound HUD GameObject to activation track: {output.streamName}");
                        }
                    }
                    else if (name.Contains("victory") || name.Contains("winscreen") || name.Contains("win"))
                    {
                        if (_winScreenInstance != null)
                        {
                            director.SetGenericBinding(output.sourceObject, _winScreenInstance);
                            Debug.Log($"[VictorySequencer] Bound WinScreen GameObject to activation track: {output.streamName}");
                        }
                    }
                    else if (name.Contains("flash") || name.Contains("white") || name.Contains("screenflash"))
                    {
                        if (flashOverlayGO != null)
                        {
                            director.SetGenericBinding(output.sourceObject, flashOverlayGO);
                            Debug.Log($"[VictorySequencer] Bound FlashOverlay GameObject to activation track: {output.streamName}");
                        }
                    }
                }
                // D. Volume Track (Post Processing)
                else if (output.outputTargetType == typeof(Volume))
                {
                    var volume = FindFirstObjectByType<Volume>();
                    if (volume == null)
                    {
                        #pragma warning disable CS0618
                        volume = FindObjectOfType<Volume>();
                        #pragma warning restore CS0618
                    }

                    if (volume != null)
                    {
                        director.SetGenericBinding(output.sourceObject, volume);
                        Debug.Log($"[VictorySequencer] Bound Post-Processing Volume to track: {output.streamName}");

                        var volGO = volume.gameObject;
                        if (volGO.GetComponent<Animator>() == null)
                        {
                            volGO.AddComponent<Animator>();
                        }
                    }
                }
            }
        }

        private Transform FindWinningPlayerPawn()
        {
            var bootstrapper = FindFirstObjectByType<CombatBootstrapper>();
            if (bootstrapper == null)
            {
                #pragma warning disable CS0618
                bootstrapper = FindObjectOfType<CombatBootstrapper>();
                #pragma warning restore CS0618
            }

            if (bootstrapper == null) return null;

            if (bootstrapper.arenaAssembler != null && bootstrapper.arenaAssembler.IsReady)
            {
                foreach (var slot in bootstrapper.arenaAssembler.PlayerSlots)
                {
                    if (slot.childCount > 0)
                    {
                        var child = slot.GetChild(0);
                        if (child.gameObject.activeInHierarchy)
                            return child;
                    }
                }
            }

            if (bootstrapper.playerTeam != null)
            {
                for (int i = 0; i < bootstrapper.playerTeam.childCount; i++)
                {
                    var child = bootstrapper.playerTeam.GetChild(i);
                    if (child.gameObject.activeInHierarchy)
                        return child;
                }
            }

            return null;
        }

        private void FallbackRetryAction()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ── UI Animation Helpers ──────────────────────────────────────────────────

        private void WireRefsFromInstance(GameObject instance)
        {
            _canvas      = instance.GetComponent<Canvas>();
            _scrimRT     = instance.transform.Find("Scrim")?.GetComponent<RectTransform>();
            _scrimImg    = _scrimRT?.GetComponent<Image>();
            _containerRT = instance.transform.Find("Container")?.GetComponent<RectTransform>();

            _fondoRT     = instance.transform.Find("Container/Fondo")?.GetComponent<RectTransform>();
            _fondoImg    = _fondoRT?.GetComponent<Image>();

            // Win or lose text
            string textPath = _isVictory ? "Container/TextoVictoria" : "Container/TextoDerrota";
            _textoRT     = instance.transform.Find(textPath)?.GetComponent<RectTransform>();
            _textoImg    = _textoRT?.GetComponent<Image>();

            // Buttons
            _botonContinuarRT  = instance.transform.Find("Container/BotonContinuar")?.GetComponent<RectTransform>();
            _botonContinuarImg = _botonContinuarRT?.GetComponent<Image>();

            _botonReintentarRT  = instance.transform.Find("Container/BotonReintentar")?.GetComponent<RectTransform>();
            _botonReintentarImg = _botonReintentarRT?.GetComponent<Image>();

            // Capture resting positions
            _textoRestPos       = _textoRT != null ? _textoRT.anchoredPosition : Vector2.zero;
            _botonContinuarRestPos = _botonContinuarRT != null ? _botonContinuarRT.anchoredPosition : Vector2.zero;
            _botonReintentarRestPos = _botonReintentarRT != null ? _botonReintentarRT.anchoredPosition : Vector2.zero;

            _containerRestScale = _containerRT != null ? _containerRT.localScale : Vector3.one;
            _textoRestScale    = _textoRT != null ? _textoRT.localScale : Vector3.one;

            // Wire Continue button
            var btnContinue = _botonContinuarRT?.GetComponent<Button>();
            if (btnContinue != null)
            {
                btnContinue.onClick.RemoveAllListeners();
                btnContinue.onClick.AddListener(() =>
                {
                    Hide();
                    _onContinue?.Invoke();
                });
            }

            // Wire Retry button
            var btnRetry = _botonReintentarRT?.GetComponent<Button>();
            if (btnRetry != null)
            {
                btnRetry.onClick.RemoveAllListeners();
                btnRetry.onClick.AddListener(() =>
                {
                    Hide();
                    _onRetry?.Invoke();
                });
            }
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
