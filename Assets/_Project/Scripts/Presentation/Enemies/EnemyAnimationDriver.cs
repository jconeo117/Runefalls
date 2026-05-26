using UnityEngine;
using UnityEngine.AI;

namespace Runefall.Presentation.Enemies
{
    [RequireComponent(typeof(Animator))]
    public class EnemyAnimationDriver : MonoBehaviour
    {
        private Animator _animator;
        private NavMeshAgent _agent;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _agent    = GetComponentInParent<NavMeshAgent>();
        }

        private void Update()
        {
            float speed = _agent != null ? _agent.velocity.magnitude : 0f;
            _animator.SetFloat(SpeedHash, speed);
        }
    }
}
