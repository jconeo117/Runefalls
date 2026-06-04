using UnityEngine;

namespace Runefall.Multiplayer.Lobby
{
    /// <summary>
    /// Simple third-person orbit camera for the lobby.
    /// Hold right mouse button to orbit. Auto-follows player.
    /// </summary>
    public class LobbyCamera : MonoBehaviour
    {
        [SerializeField] public  Transform target;
        [SerializeField] private Vector3   offset      = new Vector3(0f, 2f, -5f);
        [SerializeField] private float     smoothSpeed = 8f;
        [SerializeField] private float     sensitivity = 3f;
        [SerializeField] private float     minPitch    = -15f;
        [SerializeField] private float     maxPitch    =  50f;

        private float _yaw;
        private float _pitch = 15f;

        private void Start()
        {
            if (target != null) _yaw = target.eulerAngles.y;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (Input.GetMouseButton(1))
            {
                _yaw   += Input.GetAxis("Mouse X") * sensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * sensitivity;
                _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }

            var rotation   = Quaternion.Euler(_pitch, _yaw, 0f);
            var desired    = target.position + rotation * offset;

            transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
            transform.LookAt(target.position + Vector3.up * 1.2f);
        }
    }
}
