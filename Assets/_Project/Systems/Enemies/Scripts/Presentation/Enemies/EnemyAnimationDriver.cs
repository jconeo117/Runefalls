using UnityEngine;
using UnityEngine.AI;
using Runefall.Enemies;

namespace Runefall.Presentation.Enemies
{
    [RequireComponent(typeof(Animator))]
    public class EnemyAnimationDriver : MonoBehaviour
    {
        [Header("Animation Settings")]
        [Tooltip("Speed multiplier for the animator during chase phase. Set to 0.8f for 20% slower run.")]
        [SerializeField] private float _chaseAnimSpeedScale = 0.8f;

        private Animator _animator;
        private NavMeshAgent _agent;
        private EnemyController _controller;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private void Awake()
        {
            _animator   = GetComponent<Animator>();
            _agent      = GetComponentInParent<NavMeshAgent>();
            _controller = GetComponentInParent<EnemyController>();
        }

        private void Update()
        {
            if (_agent == null)
            {
                _animator.SetFloat(SpeedHash, 0f);
                _animator.speed = 1f;
                return;
            }

            float speed = 0f;
            if (_controller != null && _controller.UseRootMotion)
            {
                // When using root motion, the agent is not moving the transform,
                // so actual velocity is zero. We use desiredVelocity magnitude.
                speed = _agent.desiredVelocity.magnitude;
            }
            else
            {
                // Fallback classic movement: drive by actual velocity
                speed = _agent.velocity.magnitude;
            }

            float normSpeed = 0f;
            float animSpeedMultiplier = 1f;

            if (_controller != null)
            {
                EnemyState state = _controller.CurrentState;
                if (state == EnemyState.Patrol)
                {
                    float maxPatrol = _controller.PatrolSpeed > 0.01f ? _controller.PatrolSpeed : 2.5f;
                    normSpeed = Mathf.Clamp01(speed / maxPatrol) * 0.5f; // Cap to Walk (0.5f)
                    animSpeedMultiplier = 1f; // Patrol is fine at default speed
                }
                else if (state == EnemyState.Chase)
                {
                    float maxChase = _controller.ChaseSpeed > 0.01f ? _controller.ChaseSpeed : 5f;
                    normSpeed = Mathf.Clamp01(speed / maxChase); // Max out to Run (1.0f)
                    animSpeedMultiplier = _chaseAnimSpeedScale; // Scale animation speed for chase
                }
                else
                {
                    normSpeed = 0f;
                    animSpeedMultiplier = 1f;
                }
            }
            else
            {
                normSpeed = speed; // Fallback
                animSpeedMultiplier = 1f;
            }

            _animator.speed = animSpeedMultiplier;
            // Damping 0.1s for smooth transitions between Idle, Walk, and Run
            _animator.SetFloat(SpeedHash, normSpeed, 0.1f, Time.deltaTime);
        }

        private void OnAnimatorMove()
        {
            if (_controller != null && _controller.UseRootMotion)
            {
                _controller.ApplyRootMotion(_animator.deltaPosition, _animator.deltaRotation);
            }
        }
    }
}
