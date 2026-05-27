using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Runefall.Presentation.Combat
{
    public sealed class FinisherManager : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private CinemachineCamera _combatCamera;
        [SerializeField] private Camera _directCamera;
        [Tooltip("Camera controller disabled during the finisher so it stops overriding look-at. Re-enabled on cleanup.")]
        [SerializeField] private MonoBehaviour _cameraController;
        [SerializeField] private CinemachineImpulseSource _impulseSource;
        [SerializeField] private float _fovPunchDelta = -18f;

        [Header("Post-Processing")]
        [SerializeField] private Volume _ppVolume;
        [SerializeField] [Range(0f, 1f)] private float _chromaticAberrationMax = 1.00f;
        [SerializeField] [Range(0f, 1f)] private float _vignetteIntensityMax   = 0.50f;
        [SerializeField] [Range(0f, 1f)] private float _vignetteSmoothness     = 0.50f;

        [Header("Audio")]
        [SerializeField] private AudioMixerSnapshot _snapFinisher;
        [SerializeField] private AudioMixerSnapshot _snapDefault;
        [SerializeField] private float _duckTransitionIn  = 0.05f;
        [SerializeField] private float _duckTransitionOut = 0.80f;

        [Header("UI")]
        [SerializeField] private CanvasGroup _combatHUDGroup;

        [Header("Timing — all values in real (unscaled) seconds")]
        [SerializeField] private float _hitStopScale        = 0.05f;
        [SerializeField] private float _hitStopHoldDuration = 0.12f;
        [SerializeField] private float _rampToMidDuration   = 0.30f;
        [SerializeField] private float _midScale            = 0.30f;
        [SerializeField] private float _midHoldDuration     = 0.80f;
        [SerializeField] private float _rampToOneDuration   = 0.425f;

        private const float kFixedBase = 0.02f;

        public event Action OnSequenceComplete;

        public void InjectHUDGroup(CanvasGroup group) => _combatHUDGroup = group;
        public void InjectPresenter(ICombatPresenter p) => _presenter = p;

        /// <summary>
        /// Called by CombatBootstrapper from OnCombatEnded as a safety net.
        /// No-op if the sequence already completed normally.
        /// </summary>
        public void ForceCleanupIfActive()
        {
            if (!_sequenceActive) return;
            Debug.Log("[FinisherManager] ForceCleanupIfActive — sequence still active, forcing cleanup");
            StopAllCoroutines();
            ForceRestoreAll(restoreCamera: false);
        }

        private ICombatPresenter    _presenter;
        private ChromaticAberration _ca;
        private Vignette            _vignette;
        private float               _originalVolumeWeight;
        private float               _originalFov;
        private Transform           _runtimeLookAtPivot;
        private Transform           _originalLookAt;
        private Quaternion          _originalDirectCamRotation;
        private bool                _sequenceActive;

        // ── lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            Debug.Log("[FinisherManager] Awake — " +
                $"combatCamera={(_combatCamera != null ? _combatCamera.name : "NULL")} " +
                $"directCamera={(_directCamera != null ? _directCamera.name : "NULL")} " +
                $"ppVolume={(_ppVolume != null ? _ppVolume.name : "NULL")} " +
                $"hud={(_combatHUDGroup != null ? _combatHUDGroup.name : "NULL")} " +
                $"impulse={(_impulseSource != null ? _impulseSource.name : "NULL")}");

            CloneAndCacheVolumeProfile();

            var pivotGO = new GameObject("[FinisherLookAt]") { hideFlags = HideFlags.HideAndDontSave };
            pivotGO.transform.SetParent(transform);
            _runtimeLookAtPivot = pivotGO.transform;
        }

        private void OnDisable()
        {
            Debug.Log($"[FinisherManager] OnDisable — sequenceActive={_sequenceActive} timeScale={Time.timeScale}");
            if (!_sequenceActive) return;
            Debug.Log("[FinisherManager] OnDisable SAFETY NET fired — restoring state");
            ForceRestoreAll(restoreCamera: true);
        }

        private void OnDestroy()
        {
            Debug.Log($"[FinisherManager] OnDestroy — sequenceActive={_sequenceActive} timeScale={Time.timeScale}");
            if (_sequenceActive)
            {
                Debug.Log("[FinisherManager] OnDestroy SAFETY NET — restoring timeScale");
                RestoreTimeScale();
            }
        }

        // ── public API ────────────────────────────────────────────────────────────

        public Coroutine Play(Vector3 attackerPos, Vector3 targetPos)
        {
            Debug.Log($"[FinisherManager] Play() called — attacker={attackerPos} target={targetPos} active={isActiveAndEnabled}");
            return StartCoroutine(RunSequence(attackerPos, targetPos));
        }

        // ── sequence ──────────────────────────────────────────────────────────────

        private IEnumerator RunSequence(Vector3 attackerPos, Vector3 targetPos)
        {
            _sequenceActive = true;
            Debug.Log($"[FinisherManager] RunSequence START — timeScale={Time.timeScale}");

            OverrideLookAt(targetPos);

            // ── Frame 0 ──────────────────────────────────────────────────────────
            ApplyHitStop();
            HideHUD();
            FireImpulse();
            PunchFOV();
            PeakPostProcessing();
            _snapFinisher?.TransitionTo(_duckTransitionIn);

            // ── Phase 1: hit-stop hold (freeze frame on impact) ───────────────────
            float elapsed = 0f;
            while (elapsed < _hitStopHoldDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // ── Phase 2: ramp hitStop → mid — PP stays at peak ────────────────────
            // FOV stays punched. PP keeps its intensity to preserve the dramatic feel
            // through the transition into slow-mo.
            elapsed = 0f;
            while (elapsed < _rampToMidDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t      = Mathf.Clamp01(elapsed / _rampToMidDuration);
                float smooth = Mathf.SmoothStep(0f, 1f, t);
                Time.timeScale      = Mathf.Lerp(_hitStopScale, _midScale, smooth);
                Time.fixedDeltaTime = kFixedBase * Time.timeScale;
                yield return null;
            }
            Time.timeScale      = _midScale;
            Time.fixedDeltaTime = kFixedBase * _midScale;
            Debug.Log($"[FinisherManager] Phase2 done — timeScale={Time.timeScale}");

            // ── Phase 3: hold at mid, PP fades out while enemy falls ──────────────
            // PP fades from peak to 0 across the full hold duration so the slow-mo
            // and the post-processing effect retire together.
            elapsed = 0f;
            while (elapsed < _midHoldDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _midHoldDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                SetPostProcessingNormalized(1f - smoothT);
                yield return null;
            }
            DisablePostProcessing();
            Debug.Log($"[FinisherManager] Phase3 done");

            // ── Phase 4: ramp mid → 1.0, lerp FOV back ───────────────────────────
            elapsed = 0f;
            while (elapsed < _rampToOneDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _rampToOneDuration);
                float smooth = Mathf.SmoothStep(0f, 1f, t);
                Time.timeScale      = Mathf.Lerp(_midScale, 1f, smooth);
                Time.fixedDeltaTime = kFixedBase * Time.timeScale;
                
                // Interpolar suavemente el zoom del FOV en sincronía con el tiempo
                LerpFOVToOriginal(smooth);
                
                yield return null;
            }
            SnapFOVToOriginal();
            Debug.Log($"[FinisherManager] Phase4 done — timeScale={Time.timeScale}");

            // ── Cleanup ───────────────────────────────────────────────────────────
            // restoreCamera: false — win screen covers camera; leaving controller
            // disabled prevents the lerp-back-to-player-side visual artifact.
            ForceRestoreAll(restoreCamera: false, restoreHUD: false);
            Debug.Log("[FinisherManager] RunSequence COMPLETE");

            OnSequenceComplete?.Invoke();
        }

        private void ForceRestoreAll(bool restoreCamera = true, bool restoreHUD = true)
        {
            RestoreTimeScale();
            RestorePostProcessing();
            RestoreLookAt(restoreCamera);
            if (restoreHUD)
                RestoreHUD();
            _snapDefault?.TransitionTo(_duckTransitionOut);
            _sequenceActive = false;
        }

        // ── time ──────────────────────────────────────────────────────────────────

        private void ApplyHitStop()
        {
            Time.timeScale      = _hitStopScale;
            Time.fixedDeltaTime = kFixedBase * _hitStopScale;
        }

        private void RestoreTimeScale()
        {
            Debug.Log($"[FinisherManager] RestoreTimeScale — was {Time.timeScale} → setting 1.0");
            Time.timeScale      = 1f;
            Time.fixedDeltaTime = kFixedBase;
        }

        // ── HUD ───────────────────────────────────────────────────────────────────

        private void HideHUD()
        {
            // Root CanvasGroup covers anything that was injected via InjectHUDGroup.
            if (_combatHUDGroup != null)
            {
                _combatHUDGroup.alpha          = 0f;
                _combatHUDGroup.blocksRaycasts = false;
            }
            // HideAllUI covers the case where no HUDGroup was injected (direct presenter ref)
            // and instantly alpha=0's cards/slots without any scaled-time animation.
            _presenter?.HideAllUI();
        }

        private void RestoreHUD()
        {
            if (_combatHUDGroup != null)
            {
                _combatHUDGroup.alpha          = 1f;
                _combatHUDGroup.blocksRaycasts = true;
            }
            _presenter?.ShowAllUI();
        }

        // ── camera look-at ────────────────────────────────────────────────────────

        private void OverrideLookAt(Vector3 targetPos)
        {
            // Disable the camera controller so it stops overriding transform every frame.
            if (_cameraController != null) _cameraController.enabled = false;

            if (_combatCamera != null)
            {
                _originalLookAt = _combatCamera.LookAt;
                _runtimeLookAtPivot.position = targetPos + Vector3.up * 1.2f;
                _combatCamera.LookAt = _runtimeLookAtPivot;
                Debug.Log($"[FinisherManager] OverrideLookAt (vcam) — pivot={_runtimeLookAtPivot.position} " +
                          $"originalLookAt={(_originalLookAt != null ? _originalLookAt.name : "NULL")}");
            }
            else if (_directCamera != null)
            {
                _originalDirectCamRotation = _directCamera.transform.rotation;
                var dir = (targetPos + Vector3.up * 1.2f) - _directCamera.transform.position;
                if (dir.sqrMagnitude > 0.001f)
                    _directCamera.transform.rotation = Quaternion.LookRotation(dir);
                Debug.Log($"[FinisherManager] OverrideLookAt (direct) — target={targetPos} dir={dir.normalized}");
            }
            else
            {
                Debug.LogWarning("[FinisherManager] OverrideLookAt — no camera assigned (combatCamera + directCamera both NULL)");
            }
        }

        private void RestoreLookAt(bool restoreCamera = true)
        {
            if (_combatCamera != null)
                _combatCamera.LookAt = _originalLookAt;
            else if (_directCamera != null)
                _directCamera.transform.rotation = _originalDirectCamRotation;

            // Only re-enable when requested (safety net). Normal completion leaves controller
            // disabled — the win screen covers the camera and re-enabling would cause a
            // visible lerp back to the player-side anchor.
            if (restoreCamera && _cameraController != null)
                _cameraController.enabled = true;
        }

        // ── camera shake ─────────────────────────────────────────────────────────

        private void FireImpulse()
            => _impulseSource?.GenerateImpulse(Vector3.up);

        // ── FOV ───────────────────────────────────────────────────────────────────

        private bool HasFOVTarget => _combatCamera != null || _directCamera != null;

        private void PunchFOV()
        {
            if (!HasFOVTarget) { Debug.LogWarning("[FinisherManager] PunchFOV — no camera assigned"); return; }
            _originalFov = _combatCamera != null ? _combatCamera.Lens.FieldOfView : _directCamera.fieldOfView;
            SetFOV(_originalFov + _fovPunchDelta);
            Debug.Log($"[FinisherManager] PunchFOV — original={_originalFov} delta={_fovPunchDelta} new={_originalFov + _fovPunchDelta}");
        }

        private void LerpFOVToOriginal(float t)
        {
            if (!HasFOVTarget) return;
            SetFOV(Mathf.Lerp(_originalFov + _fovPunchDelta, _originalFov, t));
        }

        private void SnapFOVToOriginal()
        {
            if (!HasFOVTarget) return;
            SetFOV(_originalFov);
        }

        private void SetFOV(float fov)
        {
            if (_combatCamera != null)
            {
                var lens         = _combatCamera.Lens;
                lens.FieldOfView = fov;
                _combatCamera.Lens = lens;
            }
            else if (_directCamera != null)
            {
                _directCamera.fieldOfView = fov;
            }
        }

        // ── post-processing ───────────────────────────────────────────────────────

        private void PeakPostProcessing()
        {
            if (_ppVolume == null)
            {
                Debug.LogWarning("[FinisherManager] PeakPostProcessing — _ppVolume is NULL");
                return;
            }
            _originalVolumeWeight = _ppVolume.weight;
            _ppVolume.weight = 1f;
            Debug.Log($"[FinisherManager] PeakPostProcessing — volume weight: {_originalVolumeWeight}→1 " +
                      $"isGlobal={_ppVolume.isGlobal} enabled={_ppVolume.enabled} " +
                      $"ca={_ca != null} vignette={_vignette != null}");
            if (_ca != null)
            {
                _ca.active = true;
                _ca.intensity.Override(_chromaticAberrationMax);
            }
            if (_vignette != null)
            {
                _vignette.active = true;
                _vignette.intensity.Override(_vignetteIntensityMax);
                _vignette.smoothness.Override(_vignetteSmoothness);
            }
        }

        private void SetPostProcessingNormalized(float t)
        {
            if (_ca      != null) _ca.intensity.Override(_chromaticAberrationMax * t);
            if (_vignette != null) _vignette.intensity.Override(_vignetteIntensityMax * t);
        }

        private void DisablePostProcessing()
        {
            SetPostProcessingNormalized(0f);
            if (_ca      != null) _ca.active      = false;
            if (_vignette != null) _vignette.active = false;
        }

        private void RestorePostProcessing()
        {
            DisablePostProcessing();
            if (_ppVolume != null) _ppVolume.weight = _originalVolumeWeight;
        }

        private void CloneAndCacheVolumeProfile()
        {
            if (_ppVolume == null)
            {
                Debug.LogWarning("[FinisherManager] CloneAndCacheVolumeProfile — _ppVolume not assigned in Inspector");
                return;
            }
            _ppVolume.profile = Instantiate(_ppVolume.profile);
            bool gotCA      = _ppVolume.profile.TryGet(out _ca);
            bool gotVignette = _ppVolume.profile.TryGet(out _vignette);
            Debug.Log($"[FinisherManager] VolumeProfile cloned — gotCA={gotCA} gotVignette={gotVignette}");
        }
    }
}
