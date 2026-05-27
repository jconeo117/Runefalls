using UnityEngine;
using UnityEngine.Events;

namespace Runefall.Presentation
{
    [System.Serializable]
    public class AnimatorMoveEvent : UnityEvent<Vector3, Quaternion> { }

    /// <summary>
    /// Bridges Animation Events baked into ExplosiveLLC clips to C# UnityEvents.
    /// Attach to the same GameObject as the Animator (child of each combat pawn).
    /// Animation clips call Hit(), Shoot(), FootL(), FootR(), Land() by name.
    /// </summary>
    public class RPGCharacterAnimatorEvents : MonoBehaviour
    {
        public UnityEvent OnHit      = new UnityEvent();
        public UnityEvent OnShoot    = new UnityEvent();
        public UnityEvent OnFootR    = new UnityEvent();
        public UnityEvent OnFootL    = new UnityEvent();
        public UnityEvent OnLand     = new UnityEvent();
        public UnityEvent OnWeaponSwitch = new UnityEvent();
        public AnimatorMoveEvent OnMove  = new AnimatorMoveEvent();

        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public void Hit()          => OnHit.Invoke();
        public void Shoot()        => OnShoot.Invoke();
        public void FootR()        => OnFootR.Invoke();
        public void FootL()        => OnFootL.Invoke();
        public void Land()         => OnLand.Invoke();
        public void WeaponSwitch() => OnWeaponSwitch.Invoke();

        private void OnAnimatorMove()
        {
            if (!_animator) return;
            OnMove.Invoke(_animator.deltaPosition, _animator.rootRotation);
        }
    }
}
