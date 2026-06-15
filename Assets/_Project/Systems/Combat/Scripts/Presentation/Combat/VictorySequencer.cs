using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.SceneManagement;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Manages victory and defeat screen sequences.
    /// Delegates specific responsibilities to specialized sub-systems (SRP).
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
        [SerializeField] private float _victorySlowMoScale = 0.30f;
        [SerializeField] private float _victorySlowMoDuration = 1.20f;

        [Header("Last Hit Drama (signal 1: Outro_LastHitDrama)")]
        [Tooltip("Time scale at the instant of the kill (hit-stop / freeze frame).")]
        [SerializeField] private float _lastHitFreezeScale = 0.06f;
        [Tooltip("Fallback FOV reduction used only when no enemy pawns are found.")]
        [SerializeField] private float _lastHitZoomFovDelta = 12f;
        [Tooltip("Tightest FOV the dynamic framing is allowed to reach (single enemy). Higher = less aggressive zoom.")]
        [SerializeField] private float _lastHitZoomMinFov = 24f;
        [Tooltip("Widest FOV the dynamic framing may open to so several spread enemies all fit on screen.")]
        [SerializeField] private float _lastHitZoomMaxFov = 55f;
        [Tooltip("Extra metres of breathing room added around the enemy group when fitting the FOV.")]
        [SerializeField] private float _lastHitFramingPadding = 1.5f;
        [Tooltip("How long the hard hit-stop freeze holds.")]
        [SerializeField] private float _freezeHoldDuration = 0.30f;

        [Header("Outro Pacing (cinematic — real-time seconds)")]
        [SerializeField] private float _beatCameraDelay = 1.2f;
        [SerializeField] private float _beatVictoryAnimDelay = 2.4f;
        [SerializeField] private float _beatVictoryScreenDelay = 6.0f;
        [Range(0.05f, 1f)] [SerializeField] private float _outroSlowMoScale = 0.40f;
        [SerializeField] private float _timeRampDuration = 0.50f;

        [Header("Outro Camera Framing")]
        [SerializeField] private float _outroPlayerFaceHeight = 1.6f;
        [SerializeField] private float _outroStartCamHeight = 2.0f;
        [SerializeField] private float _outroStartCamDistance = 4.0f;
        [SerializeField] private float _outroEndCamHeight = 1.6f;
        [SerializeField] private float _outroEndCamDistance = 2.4f;
        [SerializeField] private float _outroTravelDuration = 2.5f;
        [SerializeField] private float _outroCamFov = 22f;
        [SerializeField] private Transform _outroStartCamAnchor;
        [SerializeField] private Transform _outroEndCamAnchor;

        [Header("Timing (Victory UI animation)")]
        [SerializeField] private float _fondoDuration = 0.70f;
        [SerializeField] private float _textDelay = 0.85f;
        [SerializeField] private float _textDuration = 0.55f;
        [SerializeField] private float _buttonDelay = 1.15f;
        [SerializeField] private float _buttonDuration = 0.45f;
        [SerializeField] private float _breathePeriod = 3.00f;
        [SerializeField] private float _breatheScale = 0.015f;

        [Header("Defeat UI animation")]
        [Tooltip("Seconds the Lose screen bounces in (scale overshoot).")]
        [SerializeField] private float _defeatBounceDuration = 0.55f;
        [Tooltip("Container scale the Lose screen starts at before bouncing up to full size.")]
        [SerializeField] private float _defeatBounceStartScale = 0.6f;

        [Header("Scrim")]
        [SerializeField] private float _scrimAlpha = 0.78f;

        // Delegated components (SRP)
        private CombatTimeDilationManager _timeManager;
        private CombatUIOutroPresenter _uiPresenter;
        private CinematicCameraDolly _cameraDolly;
        private CombatOutroTimelineBinder _timelineBinder;
        private CombatOutroVfxPlayer _vfxPlayer;

        // Runtime references and state
        private Action _onContinue;
        private Action _onRetry;
        private bool _isVictory;
        private CombatBootstrapper _bootstrapper;
        private CombatPresenterBase _hudPresenter;
        private readonly List<Unity.Cinemachine.CinemachineCamera> _dynamicVcams = new();

        private void Awake()
        {
            _timeManager = new CombatTimeDilationManager(this);
            _uiPresenter = new CombatUIOutroPresenter(
                this, _winScreenPrefab, _loseScreenPrefab,
                _fondoDuration, _textDelay, _textDuration, _buttonDelay, _buttonDuration,
                _breathePeriod, _breatheScale, _scrimAlpha,
                _defeatBounceDuration, _defeatBounceStartScale);
            _cameraDolly = new CinematicCameraDolly(
                this, _outroPlayerFaceHeight, _outroStartCamHeight, _outroStartCamDistance,
                _outroEndCamHeight, _outroEndCamDistance, _outroTravelDuration, _outroCamFov,
                _outroStartCamAnchor, _outroEndCamAnchor);
            _timelineBinder = new CombatOutroTimelineBinder(this);
            _vfxPlayer = new CombatOutroVfxPlayer(this);
        }

        public void Initialize(CombatBootstrapper bootstrapper, CombatPresenterBase hudPresenter)
        {
            _bootstrapper = bootstrapper;
            _hudPresenter = hudPresenter;
        }

        private CombatBootstrapper Bootstrapper => _bootstrapper ??= FindFirstObjectByType<CombatBootstrapper>();
        private CombatPresenterBase HudPresenter => _hudPresenter ??= FindFirstObjectByType<CombatPresenterBase>();

        // ── Public API ────────────────────────────────────────────────────────────

        public void Play(Action onContinue) => PlayOutro(true, onContinue);

        public void PlayOutro(bool won, Action onContinue, Action onRetry = null)
        {
            _isVictory = won;

            if (won) PlayVictoryOutro(onContinue, onRetry);
            else
            {
                SetOutroCallbacks(onContinue, onRetry);
                _vfxPlayer.HideAllCombatUI(HudPresenter);
                _uiPresenter.ShowLoseScreen(_onContinue, _onRetry);
            }
        }

        public void PlayVictoryOutro(Action onContinue, Action onRetry = null)
        {
            SetOutroCallbacks(onContinue, onRetry);
            _isVictory = true;
            StartCoroutine(PlayVictoryOutroTimelineCoroutine());
        }

        /// <summary>
        /// Wraps Continue/Retry so dismissing a result screen first tears down the WHOLE outro: stops
        /// pending beat coroutines + the victory timeline and resets time scale. Without this, a still-
        /// pending Outro_VictoryScreen beat re-instantiates the victory screen after the player already
        /// continued, and a stale freeze beat leaves the game frozen.
        /// </summary>
        private void SetOutroCallbacks(Action onContinue, Action onRetry)
        {
            Action realContinue = onContinue;
            Action realRetry    = onRetry ?? FallbackRetryAction;
            _onContinue = () => { CleanupOutro(); realContinue?.Invoke(); };
            _onRetry    = () => { CleanupOutro(); realRetry?.Invoke(); };
        }

        private void CleanupOutro()
        {
            StopAllCoroutines();   // kills pending FireOutroBeat + the victory outro coroutine
            if (_victoryTimelineDirector != null)
            {
                _victoryTimelineDirector.stopped -= OnVictoryTimelineStopped;
                if (_victoryTimelineDirector.state == PlayState.Playing) _victoryTimelineDirector.Stop();
            }
            _timeManager.ResetTimeScale();
            Hide();
        }

        public IEnumerator PlayVictoryOutroTimelineCoroutine()
        {
            var mainCamera = Camera.main;
            var brain = mainCamera != null ? mainCamera.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
            var camController = mainCamera != null ? mainCamera.GetComponent<CombatCameraController>() : null;

            if (camController != null) camController.enabled = false;
            if (brain != null) brain.enabled = false;

            Transform playerPawn = FindWinningPlayerPawn();

            _uiPresenter.PrepareWinScreen(_onContinue);   // create + wire, hidden + un-animated (no double reveal)

            _vfxPlayer.CreateFlashOverlay();
            CreateDynamicVirtualCameras(playerPawn);

            if (_victoryTimelineDirector != null)
            {
                _victoryTimelineDirector.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
                _timelineBinder.SetupRuntimeSignals(_victoryTimelineDirector, ResolveOutroSignalAction, ResolveOutroBeatTime);
                _timelineBinder.BindTracks(_victoryTimelineDirector, playerPawn?.GetComponentInChildren<Animator>(), brain, _uiPresenter.ActiveInstance, _vfxPlayer.FlashOverlayInstance, _dynamicVcams, HudPresenter);

                _victoryTimelineDirector.stopped += OnVictoryTimelineStopped;
                _victoryTimelineDirector.Play();
                yield return new WaitForSecondsRealtime(_beatVictoryScreenDelay + 1.0f);
            }
            else
            {
                _timeManager.ApplySlowMo(_victorySlowMoScale);
                yield return new WaitForSecondsRealtime(_victorySlowMoDuration);
                _timeManager.ResetTimeScale();
                if (_uiPresenter.ActiveInstance != null) _uiPresenter.ActiveInstance.SetActive(true);
                else _onContinue?.Invoke();
            }
        }

        private void OnVictoryTimelineStopped(PlayableDirector director)
        {
            if (_victoryTimelineDirector != null) _victoryTimelineDirector.stopped -= OnVictoryTimelineStopped;
            _timeManager.ResetTimeScale();
            if (_uiPresenter.ActiveInstance != null && !_uiPresenter.ActiveInstance.activeSelf) _uiPresenter.ShowWinScreen(_onContinue);
            else if (_uiPresenter.ActiveInstance == null) _onContinue?.Invoke();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        public void Hide()
        {
            _uiPresenter.Hide();
            _vfxPlayer.CleanupFlash();
            _cameraDolly.StopDolly();
            foreach (var cam in _dynamicVcams) if (cam != null) Destroy(cam.gameObject);
            _dynamicVcams.Clear();
        }

        // ── Outro Signal Reactions ──────────────────────────────────────────────────

        public void EmitOutroStart()
        {
            ZoomGameplayCameraOnEnemies();
            _timeManager.StartFreezeThenSlowMo(_lastHitFreezeScale, _outroSlowMoScale, _freezeHoldDuration, _timeRampDuration);
            _vfxPlayer.HideAllCombatUI(HudPresenter);
            _vfxPlayer.TriggerFlash();
            _vfxPlayer.SetPostProcessingWeight(1f);
        }

        public void EmitOutroSkipToCameraAnchor() => _cameraDolly.StartDolly();

        public void EmitOutroCharacterReveal() => FindWinningPlayerPawn()?.GetComponentInChildren<Animator>()?.CrossFadeInFixedTime("Victory", 0.2f);

        public void EmitOutroVictoryScreen()
        {
            _timeManager.ResetTimeScale();
            if (_victoryTimelineDirector != null)
            {
                // Unsubscribe BEFORE Stop: Stop() fires `stopped` synchronously, and OnVictoryTimelineStopped
                // would call ShowWinScreen a second time right before we do below.
                _victoryTimelineDirector.stopped -= OnVictoryTimelineStopped;
                if (_victoryTimelineDirector.state == PlayState.Playing) _victoryTimelineDirector.Stop();
            }
            _uiPresenter.ShowWinScreen(_onContinue);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        private UnityEngine.Events.UnityAction ResolveOutroSignalAction(string signalName) => signalName switch
        {
            "Outro_LastHitDrama" => EmitOutroStart,
            "Outro_SkipToCameraAnchor" => EmitOutroSkipToCameraAnchor,
            "Outro_VictoryAnimation" => EmitOutroCharacterReveal,
            "Outro_VictoryScreen" => EmitOutroVictoryScreen,
            _ => null
        };

        private float ResolveOutroBeatTime(string signalName) => signalName switch
        {
            "Outro_LastHitDrama" => 0f,
            "Outro_SkipToCameraAnchor" => _beatCameraDelay,
            "Outro_VictoryAnimation" => _beatVictoryAnimDelay,
            "Outro_VictoryScreen" => _beatVictoryScreenDelay,
            _ => 0f
        };

        private void ZoomGameplayCameraOnEnemies()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var enemies = GetEnemyPawnPositions();
            if (enemies.Count == 0)
            {
                // No pawns located — degrade to the old fixed-delta zoom on the fallback focus.
                Vector3 fb = GetEnemyFocusPoint();
                AimCameraAt(cam, fb);
                cam.fieldOfView = Mathf.Clamp(cam.fieldOfView - _lastHitZoomFovDelta, _lastHitZoomMinFov, _lastHitZoomMaxFov);
                return;
            }

            // Focus = centroid of every enemy pawn, raised to chest height.
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < enemies.Count; i++) centroid += enemies[i];
            centroid /= enemies.Count;
            Vector3 focus = centroid + Vector3.up * 1.2f;

            // Aim first so the fit distance is measured along the final view direction.
            AimCameraAt(cam, focus);

            // Bounding radius over the whole group (feet + head height so tall/spread pawns fit).
            float radius = 0f;
            for (int i = 0; i < enemies.Count; i++)
            {
                radius = Mathf.Max(radius, Vector3.Distance(focus, enemies[i]));
                radius = Mathf.Max(radius, Vector3.Distance(focus, enemies[i] + Vector3.up * 1.8f));
            }
            radius += _lastHitFramingPadding;

            float dist = Vector3.Distance(cam.transform.position, focus);
            if (dist < 0.01f) dist = 0.01f;

            // Vertical FOV that fits the bounding sphere. Aspect makes the horizontal FOV wider, so
            // the vertical axis is the binding constraint — fit it and everything fits.
            float requiredFov = 2f * Mathf.Atan(radius / dist) * Mathf.Rad2Deg;
            cam.fieldOfView = Mathf.Clamp(requiredFov, _lastHitZoomMinFov, _lastHitZoomMaxFov);
        }

        private static void AimCameraAt(Camera cam, Vector3 focus)
        {
            Vector3 dir = focus - cam.transform.position;
            if (dir.sqrMagnitude > 0.0001f) cam.transform.rotation = Quaternion.LookRotation(dir);
        }

        /// <summary>All enemy pawn world positions still on the field (slots preferred, team fallback).</summary>
        private List<Vector3> GetEnemyPawnPositions()
        {
            var list = new List<Vector3>();
            var b = Bootstrapper;
            if (b == null) return list;

            if (b.arenaAssembler != null && b.arenaAssembler.IsReady)
            {
                foreach (var slot in b.arenaAssembler.EnemySlots)
                    if (slot != null && slot.childCount > 0)
                        list.Add(slot.GetChild(0).position);
                if (list.Count > 0) return list;
            }

            if (b.enemyTeam != null)
                for (int i = 0; i < b.enemyTeam.childCount; i++)
                    list.Add(b.enemyTeam.GetChild(i).position);

            return list;
        }

        private Vector3 GetEnemyFocusPoint()
        {
            var positions = GetEnemyPawnPositions();
            if (positions.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                for (int i = 0; i < positions.Count; i++) sum += positions[i];
                return sum / positions.Count + Vector3.up * 1.2f;
            }
            var c = Camera.main;
            return c != null ? c.transform.position + c.transform.forward * 5f : Vector3.zero;
        }

        private void CreateDynamicVirtualCameras(Transform playerPawn)
        {
            foreach (var cam in _dynamicVcams) if (cam != null) Destroy(cam.gameObject);
            _dynamicVcams.Clear();

            Transform enemyPawn = FindDefeatedEnemyPawn();
            if (enemyPawn != null)
            {
                var go = new GameObject("OutroCamera_Enemy");
                var vcam = go.AddComponent<Unity.Cinemachine.CinemachineCamera>();
                go.transform.position = enemyPawn.position + enemyPawn.forward * 3.0f + Vector3.up * 1.5f;
                go.transform.LookAt(enemyPawn.position + Vector3.up * 1.0f);
                vcam.Priority = 10;
                _dynamicVcams.Add(vcam);
                go.transform.SetParent(transform);
            }

            if (playerPawn != null)
            {
                _cameraDolly.CalculateCameraPoses(playerPawn, ResolveIntroPlayerAnchor());
                var sGo = new GameObject("OutroCamera_Start");
                var sVcam = sGo.AddComponent<Unity.Cinemachine.CinemachineCamera>();
                sGo.transform.position = _cameraDolly.CamStartPos;
                sGo.transform.LookAt(_cameraDolly.CamFacePoint);
                sVcam.Priority = 10;
                _dynamicVcams.Add(sVcam);
                sGo.transform.SetParent(transform);
                var eGo = new GameObject("OutroCamera_End");
                var eVcam = eGo.AddComponent<Unity.Cinemachine.CinemachineCamera>();
                eGo.transform.position = _cameraDolly.CamEndPos;
                eGo.transform.rotation = _cameraDolly.CamEndRot;
                eVcam.Priority = 10;
                _dynamicVcams.Add(eVcam);
                eGo.transform.SetParent(transform);
            }
        }

        // ── Helper Resolvers ────────────────────────────────────────────────────────

        private Transform FindWinningPlayerPawn()
        {
            var b = Bootstrapper;
            if (b == null) return null;

            if (b.arenaAssembler != null && b.arenaAssembler.IsReady)
            {
                foreach (var slot in b.arenaAssembler.PlayerSlots)
                {
                    if (slot.childCount > 0)
                    {
                        var child = slot.GetChild(0);
                        if (child.gameObject.activeInHierarchy)
                            return child;
                    }
                }
            }

            if (b.playerTeam != null)
            {
                for (int i = 0; i < b.playerTeam.childCount; i++)
                {
                    var child = b.playerTeam.GetChild(i);
                    if (child.gameObject.activeInHierarchy)
                        return child;
                }
            }

            return null;
        }

        private Transform FindDefeatedEnemyPawn()
        {
            var b = Bootstrapper;
            if (b == null) return null;

            if (b.arenaAssembler != null && b.arenaAssembler.IsReady)
            {
                foreach (var slot in b.arenaAssembler.EnemySlots)
                {
                    if (slot.childCount > 0)
                    {
                        var child = slot.GetChild(0);
                        return child;
                    }
                }
            }

            if (b.enemyTeam != null && b.enemyTeam.childCount > 0)
            {
                return b.enemyTeam.GetChild(0);
            }

            return null;
        }

        private Transform ResolveIntroPlayerAnchor()
        {
            var b = Bootstrapper;
            if (b != null && b.introSequencer != null)
            {
                return b.introSequencer.introPlayerAnchor;
            }
            return null;
        }

        private void FallbackRetryAction()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
