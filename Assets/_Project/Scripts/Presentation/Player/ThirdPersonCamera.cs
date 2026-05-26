using UnityEngine;
using UnityEngine.InputSystem;

namespace Runefall.Presentation.Player
{
    [RequireComponent(typeof(Camera))]
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("Distance")]
        [SerializeField] private float distance     = 5f;
        [SerializeField] private float heightOffset = 1.5f;

        [Header("Mouse sensitivity")]
        [SerializeField] private float sensitivityX = 0.2f;
        [SerializeField] private float sensitivityY = 0.15f;

        [Header("Vertical clamp")]
        [SerializeField] private float minPitch = -10f;
        [SerializeField] private float maxPitch =  40f;

        private float _yaw;
        private float _pitch = 12f;

        private void LateUpdate()
        {
            if (target == null) return;

            var delta = Mouse.current.delta.ReadValue();
            _yaw   += delta.x * sensitivityX;
            _pitch -= delta.y * sensitivityY;
            _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);

            var rot   = Quaternion.Euler(_pitch, _yaw, 0f);
            var pivot = target.position + Vector3.up * heightOffset;

            transform.position = pivot - rot * Vector3.forward * distance;
            transform.LookAt(pivot);
        }
    }
}
