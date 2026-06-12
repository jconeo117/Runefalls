using System;
using System.Collections;
using UnityEngine;

namespace Runefall.Presentation.Combat
{
    public class SkillCameraDirector : ICameraDirector
    {
        private readonly CameraConfig _config;
        private readonly Camera _camera;
        private readonly Func<bool> _hasTriggeredOutroClimax;

        public SkillCameraDirector(CameraConfig config, Func<bool> hasTriggeredOutroClimax)
        {
            _config = config;
            _camera = Camera.main;
            _hasTriggeredOutroClimax = hasTriggeredOutroClimax;
        }

        public IEnumerator PerformCameraShake(float duration, float magnitude)
        {
            if (_camera == null) yield break;
            Vector3 originalPos = _camera.transform.position;
            float elapsed = 0.0f;
            
            while (elapsed < duration)
            {
                float x = UnityEngine.Random.Range(-1f, 1f) * magnitude;
                float y = UnityEngine.Random.Range(-1f, 1f) * magnitude;
                
                float percent = elapsed / duration;
                float damper = 1.0f - percent;
                
                _camera.transform.position = originalPos + new Vector3(x, y, 0f) * damper;
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            _camera.transform.position = originalPos;
        }

        public IEnumerator BlendToPose(CameraPose pose, Transform casterAnchor, float duration)
        {
            // Handled through the per-rank camera posing routines.
            yield break;
        }

        public IEnumerator SkillCameraRoutine(int rank, Transform casterPawn, Vector3 targetPos, float duration)
        {
            if (_camera == null || casterPawn == null) yield break;

            var brain = _camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            var camController = _camera.GetComponent<CombatCameraController>();
            if (brain != null) brain.enabled = false;
            if (camController != null) camController.enabled = false;

            Vector3 casterPos = casterPawn.position;
            Vector3 fwd = targetPos - casterPos; fwd.y = 0f;
            if (targetPos == Vector3.zero || fwd.sqrMagnitude < 1.0f) fwd = casterPawn.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            Vector3 shoulderPos  = casterPos + Vector3.up * _config.bronzeHeight - fwd * _config.bronzeBack + right * _config.bronzeRight;
            Vector3 shoulderLook = casterPos + fwd * 3f + Vector3.up * 1.4f;
            Vector3 facePos      = casterPos + fwd * _config.faceDist + Vector3.up * _config.faceHeight;
            Vector3 faceLook     = casterPos + Vector3.up * _config.faceHeight;

            if (rank <= 1)
                yield return BronzeCam(shoulderPos, shoulderLook, duration);
            else if (rank == 2)
                yield return SilverCam(facePos, faceLook, shoulderPos, shoulderLook, duration);
            else
                yield return GoldCam(casterPos, targetPos, facePos, faceLook, duration);

            if (!_hasTriggeredOutroClimax() && camController != null)
                camController.enabled = true;
        }

        private IEnumerator BronzeCam(Vector3 pos, Vector3 look, float duration)
        {
            yield return BlendCamMove(_camera.transform.position, _camera.transform.position + _camera.transform.forward, pos, look, _config.camBlendIn);
            float t = 0f;
            while (t < duration && !_hasTriggeredOutroClimax()) 
            { 
                t += Time.deltaTime; 
                ApplyCam(pos, look); 
                yield return null; 
            }
        }

        private IEnumerator SilverCam(Vector3 facePos, Vector3 faceLook, Vector3 shPos, Vector3 shLook, float duration)
        {
            ApplyCam(facePos, faceLook);
            float t = 0f;
            while (t < _config.silverFaceHold && !_hasTriggeredOutroClimax()) 
            { 
                t += Time.deltaTime; 
                ApplyCam(facePos, faceLook); 
                yield return null; 
            }
            
            yield return BlendCamMove(facePos, faceLook, shPos, shLook, _config.silverReturnDur);
            float t2 = 0f;
            while (t2 < duration && !_hasTriggeredOutroClimax()) 
            { 
                t2 += Time.deltaTime; 
                ApplyCam(shPos, shLook); 
                yield return null; 
            }
        }

        // Cinematic CUT sequence: 3 hard cuts (no panning) — low-hero anticipation on the caster,
        // tight wind-up close-up, then the impact shot with a push-in and a decaying shake.
        private IEnumerator GoldCam(Vector3 casterPos, Vector3 targetPos, Vector3 facePos, Vector3 faceLook, float duration)
        {
            if (_camera == null) yield break;

            Vector3 fwd = targetPos - casterPos; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = _camera.transform.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            Vector3 casterHead = casterPos + Vector3.up * _config.faceHeight;

            // CUT 1 — low hero angle on the caster (anticipation).
            Vector3 c1pos  = casterPos + fwd * (_config.faceDist * 1.3f) + right * 0.5f + Vector3.up * 0.45f;
            Vector3 c1look = casterHead + Vector3.up * 0.25f;
            yield return HoldCut(c1pos, c1look, _config.goldCut1Hold);

            // CUT 2 — tight wind-up: close on the caster's chest/hands, slight side.
            Vector3 c2pos  = casterPos + fwd * (_config.faceDist * 0.7f) + right * (_config.bronzeRight + 0.35f)
                             + Vector3.up * (_config.faceHeight * 0.78f);
            Vector3 c2look = casterPos + Vector3.up * (_config.faceHeight * 0.72f) + fwd * 0.4f;
            yield return HoldCut(c2pos, c2look, _config.goldCut2Hold);

            // CUT 3 — WIDE impact shot: pull back from the action center (caster<->target) and frame the
            // whole clash, not a tight zoom on the enemy. Gentle push-in + decaying shake.
            Vector3 actionCenter = (casterPos + targetPos) * 0.5f + Vector3.up * (_config.faceHeight * 0.9f);
            Vector3 c3pos = actionCenter - fwd * _config.goldEndBack + right * _config.goldEndRight
                            + Vector3.up * _config.goldEndHeight;
            ApplyCam(c3pos, actionCenter);

            float   rest    = Mathf.Max(0.1f, duration - _config.goldCut1Hold - _config.goldCut2Hold);
            Vector3 pushDir = actionCenter - c3pos; pushDir.y *= 0.4f;
            if (pushDir.sqrMagnitude > 0.0001f) pushDir.Normalize();

            float ot = 0f;
            while (ot < rest && !_hasTriggeredOutroClimax())
            {
                ot += Time.deltaTime;
                float k = Mathf.Clamp01(ot / rest);
                Vector3 basePos = c3pos + pushDir * (k * 0.4f);   // gentle push — stays wide/panoramic
                Vector3 shake   = UnityEngine.Random.insideUnitSphere * (_config.goldShakeMagnitude * (1f - k));
                shake.z *= 0.5f;
                _camera.transform.position = basePos + shake;
                Vector3 ld = actionCenter - basePos;
                if (ld.sqrMagnitude > 0.0001f) _camera.transform.rotation = Quaternion.LookRotation(ld);
                yield return null;
            }
            ApplyCam(c3pos + pushDir * 0.4f, actionCenter);
        }

        /// <summary>Hard cut to a static pose and hold it for `hold` seconds (no drift).</summary>
        private IEnumerator HoldCut(Vector3 pos, Vector3 look, float hold)
        {
            ApplyCam(pos, look);
            float t = 0f;
            while (t < hold && !_hasTriggeredOutroClimax())
            {
                t += Time.deltaTime;
                ApplyCam(pos, look);
                yield return null;
            }
        }

        private void ApplyCam(Vector3 pos, Vector3 look)
        {
            if (_camera == null) return;
            _camera.transform.position = pos;
            Vector3 dir = look - pos;
            if (dir.sqrMagnitude > 0.0001f) _camera.transform.rotation = Quaternion.LookRotation(dir);
        }

        private IEnumerator BlendCamMove(Vector3 fromPos, Vector3 fromLook, Vector3 toPos, Vector3 toLook, float dur)
        {
            if (_camera == null) yield break;
            Quaternion fromRot = SafeLook(fromLook - fromPos, _camera.transform.rotation);
            Quaternion toRot = SafeLook(toLook - toPos, _camera.transform.rotation);
            float t = 0f;
            while (t < dur && !_hasTriggeredOutroClimax())
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, dur > 0f ? Mathf.Clamp01(t / dur) : 1f);
                _camera.transform.position = Vector3.Lerp(fromPos, toPos, k);
                _camera.transform.rotation = Quaternion.Slerp(fromRot, toRot, k);
                yield return null;
            }
            ApplyCam(toPos, toLook);
        }

        private static Quaternion SafeLook(Vector3 dir, Quaternion fallback)
            => dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : fallback;

        public IEnumerator StartOrbit(Transform focusPoint, float speed)
        {
            if (_camera == null || focusPoint == null) yield break;
            Vector3 center = focusPoint.position;
            Vector3 off = _camera.transform.position - center;
            float radius = new Vector2(off.x, off.z).magnitude;
            float ang = Mathf.Atan2(off.z, off.x);

            while (true)
            {
                ang += Time.deltaTime * speed;
                _camera.transform.position = center + new Vector3(Mathf.Cos(ang) * radius, off.y, Mathf.Sin(ang) * radius);
                _camera.transform.LookAt(center);
                yield return null;
            }
        }

        public void StopOrbit()
        {
            // Optional cleanup
        }
    }
}
