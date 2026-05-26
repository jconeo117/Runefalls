using UnityEngine;
using UnityEngine.AI;
using Runefall.Core;
using Runefall.Combat;
using Runefall.Enemies;
using Runefall.Data;

namespace Runefall.Presentation.Enemies
{
    /// <summary>
    /// Presentation layer for enemy exploration AI.
    /// Drives ExplorationBrain with physics sensor data.
    /// Moves via NavMeshAgent. Raises EncounterReadyEvent on Confront.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private EnemyData _enemyData;

        [Header("Detection")]
        [SerializeField] private float _detectionRange  = 10f;
        [SerializeField] private float _loseRange       = 15f;
        [SerializeField] private float _confrontRange   = 1.8f;
        [SerializeField] private LayerMask _playerMask  = ~0;
        [SerializeField] private Transform _eyePoint;       // LOS raycast origin; defaults to this transform

        [Header("Patrol")]
        [SerializeField] private Transform[] _waypoints;
        [SerializeField] private float _waypointTolerance = 0.5f;
        [SerializeField] private float _waypointWaitTime  = 1.5f;

        [Header("Speed")]
        [SerializeField] private float _patrolSpeed = 2.5f;
        [SerializeField] private float _chaseSpeed  = 5f;

        [Header("Events")]
        [Tooltip("Raised when Confront triggers — connect to scene loader or combat system.")]
        [SerializeField] private EncounterReadyEvent _encounterReadyEvent;

        // ── Runtime state ────────────────────────────────────────────────────────

        private NavMeshAgent   _agent;
        private ExplorationBrain _brain;
        private Transform      _playerTransform;

        private int   _patrolIndex;
        private float _waitTimer;
        private bool  _waiting;
        private bool  _confrontFired;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _brain = new ExplorationBrain(_detectionRange, _loseRange, _confrontRange);
            _brain.OnStateChanged += HandleStateChanged;
        }

        private void Start()
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
                _playerTransform = playerGO.transform;
            else
                Debug.LogWarning($"[EnemyController] '{name}': no GameObject tagged 'Player' found.", this);

            SetPatrolSpeed();
            AdvanceToNextWaypoint();
        }

        private void Update()
        {
            if (_playerTransform == null) return;

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            bool  los  = HasLineOfSight(dist);

            _brain.Tick(dist, los, Time.deltaTime);

            switch (_brain.CurrentState)
            {
                case EnemyState.Patrol:   TickPatrol();         break;
                case EnemyState.Chase:    TickChase();          break;
                case EnemyState.Confront: TickConfront();       break;
            }
        }

        // ── State ticks ──────────────────────────────────────────────────────────

        private void TickPatrol()
        {
            if (_waypoints == null || _waypoints.Length == 0) return;

            if (_waiting)
            {
                _waitTimer -= Time.deltaTime;
                if (_waitTimer <= 0f)
                {
                    _waiting = false;
                    AdvanceToNextWaypoint();
                }
                return;
            }

            if (!_agent.pathPending && _agent.remainingDistance <= _waypointTolerance)
            {
                _waiting   = true;
                _waitTimer = _waypointWaitTime;
            }
        }

        private void TickChase()
        {
            _agent.SetDestination(_playerTransform.position);
        }

        private void TickConfront()
        {
            _agent.ResetPath();
            transform.LookAt(_playerTransform.position);

            if (!_confrontFired)
            {
                _confrontFired = true;
                FireEncounterEvent();
            }
        }

        // ── Sensor ───────────────────────────────────────────────────────────────

        private bool HasLineOfSight(float dist)
        {
            if (dist > _detectionRange) return false;

            Vector3 origin = _eyePoint != null ? _eyePoint.position : transform.position + Vector3.up * 1.5f;
            Vector3 dir    = (_playerTransform.position - origin).normalized;

            return Physics.Raycast(origin, dir, dist + 0.1f, _playerMask) == false
                || HitsPlayer(origin, dir, dist);
        }

        private bool HitsPlayer(Vector3 origin, Vector3 dir, float dist)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, dist + 0.1f))
                return hit.transform == _playerTransform || hit.transform.IsChildOf(_playerTransform);
            return false;
        }

        // ── Events ───────────────────────────────────────────────────────────────

        private void HandleStateChanged(EnemyState from, EnemyState to)
        {
            switch (to)
            {
                case EnemyState.Chase:
                    _agent.speed = _chaseSpeed;
                    _waiting     = false;
                    break;
                case EnemyState.Patrol:
                    SetPatrolSpeed();
                    AdvanceToNextWaypoint();
                    break;
            }
#if UNITY_EDITOR
            Debug.Log($"[EnemyController] '{name}' {from} → {to}");
#endif
        }

        private void FireEncounterEvent()
        {
            if (_encounterReadyEvent == null || _enemyData == null) return;

            var data = new EncounterData
            {
                enemyData      = _enemyData,
                enemyLevel     = 1,
                enemyTransform = transform
            };
            _encounterReadyEvent.Raise(data);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private void SetPatrolSpeed() => _agent.speed = _patrolSpeed;

        private void AdvanceToNextWaypoint()
        {
            if (_waypoints == null || _waypoints.Length == 0) return;
            _patrolIndex = (_patrolIndex + 1) % _waypoints.Length;
            _agent.SetDestination(_waypoints[_patrolIndex].position);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 origin = _eyePoint != null ? _eyePoint.position : transform.position + Vector3.up * 1.5f;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _detectionRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _confrontRange);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _loseRange);

            if (_waypoints != null)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < _waypoints.Length; i++)
                {
                    if (_waypoints[i] == null) continue;
                    Gizmos.DrawSphere(_waypoints[i].position, 0.2f);
                    if (i + 1 < _waypoints.Length && _waypoints[i + 1] != null)
                        Gizmos.DrawLine(_waypoints[i].position, _waypoints[i + 1].position);
                }
            }
        }
#endif
    }
}
