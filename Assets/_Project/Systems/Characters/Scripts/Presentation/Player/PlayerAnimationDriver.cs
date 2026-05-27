using UnityEngine;

namespace Runefall.Presentation.Player
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimationDriver : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;

        private Animator _animator;
        private static readonly int SpeedHash     = Animator.StringToHash("Speed");
        private static readonly int IsDashingHash = Animator.StringToHash("IsDashing");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (controller == null)
                controller = GetComponentInParent<PlayerController>();
        }

        private void Update()
        {
            if (controller == null) return;
            _animator.SetFloat(SpeedHash,     controller.CurrentSpeed);
            _animator.SetBool(IsDashingHash,  controller.IsDashing);
        }
    }
}
