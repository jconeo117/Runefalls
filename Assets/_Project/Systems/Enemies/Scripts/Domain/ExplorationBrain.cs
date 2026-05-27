using System;

namespace Runefall.Enemies
{
    /// <summary>
    /// Pure state machine for enemy exploration behavior.
    /// No MonoBehaviour, no Unity dependencies — receives sensor data, outputs state.
    /// </summary>
    public class ExplorationBrain
    {
        public EnemyState CurrentState { get; private set; } = EnemyState.Patrol;

        public event Action<EnemyState, EnemyState> OnStateChanged; // (from, to)

        private readonly float _detectionRange;  // Patrol → Chase
        private readonly float _loseRange;       // Chase → Patrol (lost player)
        private readonly float _confrontRange;   // Chase → Confront (close enough to trigger combat)
        private readonly float _requireLosTime;  // seconds of LOS before switching to Chase

        private float _losTimer;
        private float _lostTimer;
        private const float LostGracePeriod = 3f; // seconds before giving up chase

        public ExplorationBrain(
            float detectionRange  = 10f,
            float loseRange       = 15f,
            float confrontRange   = 1.8f,
            float requireLosTime  = 0.3f)
        {
            _detectionRange  = detectionRange;
            _loseRange       = loseRange;
            _confrontRange   = confrontRange;
            _requireLosTime  = requireLosTime;
        }

        /// <summary>
        /// Drive the state machine each frame.
        /// All Unity-side physics queries happen in EnemyController; results pass in here.
        /// </summary>
        public void Tick(float distanceToPlayer, bool hasLineOfSight, float deltaTime)
        {
            switch (CurrentState)
            {
                case EnemyState.Patrol:
                    TickPatrol(distanceToPlayer, hasLineOfSight, deltaTime);
                    break;
                case EnemyState.Chase:
                    TickChase(distanceToPlayer, hasLineOfSight, deltaTime);
                    break;
                case EnemyState.Confront:
                    // Confront is terminal until combat resolves — no auto-exit here.
                    break;
            }
        }

        public void ForceState(EnemyState state) => Transition(state);

        private void TickPatrol(float dist, bool los, float dt)
        {
            if (dist <= _detectionRange && los)
            {
                _losTimer += dt;
                if (_losTimer >= _requireLosTime)
                {
                    _losTimer = 0f;
                    Transition(EnemyState.Chase);
                }
            }
            else
            {
                _losTimer = 0f;
            }
        }

        private void TickChase(float dist, bool los, float dt)
        {
            if (dist <= _confrontRange)
            {
                Transition(EnemyState.Confront);
                return;
            }

            if (!los || dist > _loseRange)
            {
                _lostTimer += dt;
                if (_lostTimer >= LostGracePeriod)
                {
                    _lostTimer = 0f;
                    Transition(EnemyState.Patrol);
                }
            }
            else
            {
                _lostTimer = 0f;
            }
        }

        private void Transition(EnemyState next)
        {
            if (next == CurrentState) return;
            var prev = CurrentState;
            CurrentState = next;
            OnStateChanged?.Invoke(prev, next);
        }
    }
}
