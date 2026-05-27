using UnityEngine;
using UnityEngine.AI;

namespace Runefall.Presentation.Enemies
{
    [RequireComponent(typeof(Animator))]
    public class EnemyAnimationDriver : MonoBehaviour
    {
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

            _animator.SetFloat(SpeedHash, speed);
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
