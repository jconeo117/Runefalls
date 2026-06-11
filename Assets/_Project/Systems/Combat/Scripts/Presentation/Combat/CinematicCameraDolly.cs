using System.Collections;
using UnityEngine;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Handles the code-driven player camera dolly close-up at the end of combat.
    /// </summary>
    public class CinematicCameraDolly
    {
        private readonly MonoBehaviour _runner;
        private readonly float _outroPlayerFaceHeight;
        private readonly float _outroStartCamHeight;
        private readonly float _outroStartCamDistance;
        private readonly float _outroEndCamHeight;
        private readonly float _outroEndCamDistance;
        private readonly float _outroTravelDuration;
        private readonly float _outroCamFov;
        private readonly Transform _outroStartCamAnchor;
        private readonly Transform _outroEndCamAnchor;

        private Vector3 _camStartPos;
        private Vector3 _camEndPos;
        private Quaternion _camEndRot;
        private Vector3 _camFacePoint;
        private bool _hasCamPoses;
        private Coroutine _activeDollyRoutine;

        public bool HasCamPoses => _hasCamPoses;

        public Vector3 CamStartPos => _camStartPos;
        public Vector3 CamEndPos => _camEndPos;
        public Quaternion CamEndRot => _camEndRot;
        public Vector3 CamFacePoint => _camFacePoint;

        public CinematicCameraDolly(
            MonoBehaviour runner,
            float outroPlayerFaceHeight,
            float outroStartCamHeight,
            float outroStartCamDistance,
            float outroEndCamHeight,
            float outroEndCamDistance,
            float outroTravelDuration,
            float outroCamFov,
            Transform outroStartCamAnchor,
            Transform outroEndCamAnchor)
        {
            _runner = runner;
            _outroPlayerFaceHeight = outroPlayerFaceHeight;
            _outroStartCamHeight = outroStartCamHeight;
            _outroStartCamDistance = outroStartCamDistance;
            _outroEndCamHeight = outroEndCamHeight;
            _outroEndCamDistance = outroEndCamDistance;
            _outroTravelDuration = outroTravelDuration;
            _outroCamFov = outroCamFov;
            _outroStartCamAnchor = outroStartCamAnchor;
            _outroEndCamAnchor = outroEndCamAnchor;
        }

        public void CalculateCameraPoses(Transform playerPawn, Transform introAnchor)
        {
            if (playerPawn == null) return;

            Vector3 pawnPos = playerPawn.position;
            Vector3 pawnForward = playerPawn.forward;

            Vector3 facePoint = pawnPos + Vector3.up * _outroPlayerFaceHeight;
            Vector3 startPos = pawnPos + pawnForward * _outroStartCamDistance + Vector3.up * _outroStartCamHeight;
            Vector3 endPos = pawnPos + pawnForward * _outroEndCamDistance + Vector3.up * _outroEndCamHeight;
            Quaternion endRot = Quaternion.LookRotation(facePoint - endPos);

            if (_outroEndCamAnchor == null && introAnchor != null)
            {
                endPos = introAnchor.position;
                endRot = introAnchor.rotation;
            }

            _camStartPos = startPos;
            _camEndPos = endPos;
            _camEndRot = endRot;
            _camFacePoint = facePoint;
            _hasCamPoses = true;
        }

        public void StartDolly()
        {
            StopDolly();
            _activeDollyRoutine = _runner.StartCoroutine(DollyPlayerCameraRoutine());
        }

        public void StopDolly()
        {
            if (_activeDollyRoutine != null)
            {
                _runner.StopCoroutine(_activeDollyRoutine);
                _activeDollyRoutine = null;
            }
        }

        private IEnumerator DollyPlayerCameraRoutine()
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            if (!_hasCamPoses && _outroStartCamAnchor == null && _outroEndCamAnchor == null) yield break;

            var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null) brain.enabled = false; // keep manual control

            Vector3 startPos = _outroStartCamAnchor != null ? _outroStartCamAnchor.position : _camStartPos;
            Quaternion startRot = _outroStartCamAnchor != null
                ? _outroStartCamAnchor.rotation
                : Quaternion.LookRotation(_camFacePoint - startPos);

            Vector3 endPos = _outroEndCamAnchor != null ? _outroEndCamAnchor.position : _camEndPos;
            Quaternion endRot = _outroEndCamAnchor != null ? _outroEndCamAnchor.rotation : _camEndRot;

            float dur = Mathf.Max(0.01f, _outroTravelDuration);
            float startFov = cam.fieldOfView;
            float t = 0f;

            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                cam.transform.position = Vector3.Lerp(startPos, endPos, k);
                cam.transform.rotation = Quaternion.Slerp(startRot, endRot, k);
                cam.fieldOfView = Mathf.Lerp(startFov, _outroCamFov, k);
                yield return null;
            }

            cam.transform.position = endPos;
            cam.transform.rotation = endRot;
            cam.fieldOfView = _outroCamFov;
        }
    }
}
