using System.Collections;
using UnityEngine;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Subscribes to SkillUsedEvent SO and drives CombatCameraController through cinematic sequences.
    ///
    /// Shot selection by rank:
    ///   Rank 1 → rankOneSequence   (Static — hold on current frame)
    ///   Rank 2 → rankTwoSequence   (PushIn — camera glides toward the target)
    ///   Rank 3 → rankThreeSequence (DynamicOrbit — camera orbits around field center)
    ///
    /// Call Init() from CombatBootstrapper after TurnManager is created.
    /// Assign SkillUsedEvent SO and optional CameraSequenceData assets in the Inspector.
    /// Sequences run in parallel with combat — they affect only the camera transform.
    /// </summary>
    public class CombatCameraDirector : MonoBehaviour
    {
        [Header("Event")]
        [SerializeField] private SkillUsedEvent skillUsedEvent;

        [Header("Sequences — assign CameraSequenceData assets")]
        [SerializeField] private CameraSequenceData rankOneSequence;   // Rank 1 — Static
        [SerializeField] private CameraSequenceData rankTwoSequence;   // Rank 2 — PushIn
        [SerializeField] private CameraSequenceData rankThreeSequence; // Rank 3 — DynamicOrbit

        private CombatCameraController _cam;
        private Vector3                _fieldCenter;
        private TurnManager            _tm;
        private bool                   _isEnemyPhase;
        private bool                   _sequencePlaying;

        // ── init ─────────────────────────────────────────────────────────────────

        public void Init(CombatCameraController cam, Vector3 fieldCenter, TurnManager tm)
        {
            _cam         = cam;
            _fieldCenter = fieldCenter;
            _tm          = tm;
            _tm.OnPlayerTurnStarted += OnPlayerPhase;
            _tm.OnEnemyTurnStarted  += OnEnemyPhase;
        }

        private void OnEnable()  => skillUsedEvent?.Subscribe(OnSkillUsed);
        private void OnDisable() => skillUsedEvent?.Unsubscribe(OnSkillUsed);

        private void OnDestroy()
        {
            if (_tm == null) return;
            _tm.OnPlayerTurnStarted -= OnPlayerPhase;
            _tm.OnEnemyTurnStarted  -= OnEnemyPhase;
        }

        private void OnPlayerPhase(int _) => _isEnemyPhase = false;
        private void OnEnemyPhase()       => _isEnemyPhase = true;

        // ── skill event handler ───────────────────────────────────────────────────

        private void OnSkillUsed(SkillUsedPayload payload)
        {
            if (_cam == null || _sequencePlaying) return;

            var seq = payload.Rank switch
            {
                1 => rankOneSequence,
                2 => rankTwoSequence,
                _ => rankThreeSequence
            };

            if (seq == null || seq.shots == null || seq.shots.Length == 0) return;
            StartCoroutine(RunSequence(seq, payload));
        }

        // ── sequence runner ───────────────────────────────────────────────────────

        private IEnumerator RunSequence(CameraSequenceData seq, SkillUsedPayload payload)
        {
            _sequencePlaying = true;

            foreach (var shot in seq.shots)
                yield return StartCoroutine(ApplyShot(shot, payload));

            RestorePhaseTarget();
            _sequencePlaying = false;
        }

        private IEnumerator ApplyShot(CameraShot shot, SkillUsedPayload _)
        {
            switch (shot.type)
            {
                case CameraShotType.PushIn:
                    yield return StartCoroutine(PushIn(shot));
                    break;

                case CameraShotType.DynamicOrbit:
                    yield return StartCoroutine(DynamicOrbit(shot));
                    break;

                default: // Static, OverShoulder, LowAngle — hold current frame
                    yield return new WaitForSeconds(shot.duration);
                    break;
            }
        }

        // ── shot implementations ──────────────────────────────────────────────────

        /// <summary>Glides camera forward 2 units toward the target, then holds.</summary>
        private IEnumerator PushIn(CameraShot shot)
        {
            var pushAnchor = new GameObject("CamSeq_PushIn").transform;
            pushAnchor.position = _cam.transform.position + _cam.transform.forward * 2f;
            pushAnchor.rotation = _cam.transform.rotation;

            _cam.SetTarget(pushAnchor);
            yield return new WaitForSeconds(shot.duration);

            Destroy(pushAnchor.gameObject);
        }

        /// <summary>Orbits 60° around the field center at the current camera radius.</summary>
        private IEnumerator DynamicOrbit(CameraShot shot)
        {
            var orbitAnchor = new GameObject("CamSeq_Orbit").transform;
            _cam.SetTarget(orbitAnchor);

            Vector3 startPos   = _cam.transform.position;
            Vector2 flatOffset = new Vector2(startPos.x - _fieldCenter.x, startPos.z - _fieldCenter.z);
            float   radius     = flatOffset.magnitude;
            float   startAngle = Mathf.Atan2(flatOffset.y, flatOffset.x);
            float   sweepRad   = 60f * Mathf.Deg2Rad;
            float   elapsed    = 0f;

            while (elapsed < shot.duration)
            {
                elapsed += Time.deltaTime;
                float angle = startAngle + (elapsed / shot.duration) * sweepRad;

                orbitAnchor.position = new Vector3(
                    _fieldCenter.x + Mathf.Cos(angle) * radius,
                    startPos.y,
                    _fieldCenter.z + Mathf.Sin(angle) * radius);

                orbitAnchor.LookAt(new Vector3(_fieldCenter.x, startPos.y, _fieldCenter.z));
                yield return null;
            }

            Destroy(orbitAnchor.gameObject);
        }

        // ── helpers ───────────────────────────────────────────────────────────────

        /// <summary>Returns camera control to the orbit controller after a sequence ends.</summary>
        private void RestorePhaseTarget()
        {
            if (_cam == null) return;
            if (_isEnemyPhase) _cam.OnEnemyTurnStarted();
            else               _cam.OnPlayerTurnStarted(0);
        }
    }
}
