using UnityEngine;
using UnityEngine.InputSystem;

namespace Runefall.Presentation.Network
{
    /// <summary>
    /// A clean Third-Person Character Controller for the local prep room lobby.
    /// Follows the player from behind, allowing mouse orbiting and WASD direction movement.
    /// Fully compatible with Unity's New Input System using direct hardware polling.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SimpleCharacterController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5.0f;
        [SerializeField] private float gravity = 9.81f;
        [SerializeField] private float rotationSpeed = 10.0f;

        [Header("Camera Third Person Settings")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] private float lookSpeed = 0.12f;
        [SerializeField] private float cameraDistance = 3.5f;
        [SerializeField] private float cameraHeight = 1.8f;
        [SerializeField] private float lookXLimitMin = -15.0f;
        [SerializeField] private float lookXLimitMax = 55.0f;

        public bool InputBlocked { get; set; } = false;

        private CharacterController _characterController;
        private Vector3 _moveDirection = Vector3.zero;
        private float _rotationX = 15f; // Start with a slight angle looking down
        private float _rotationY = 0f;

        private void Start()
        {
            _characterController = GetComponent<CharacterController>();

            // Lock and hide cursor for a premium third-person immersion inside the lobby
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (playerCamera == null)
            {
                playerCamera = Camera.main != null ? Camera.main.transform : GetComponentInChildren<Camera>()?.transform;
            }

            _rotationY = transform.eulerAngles.y;
        }

        private void Update()
        {
            if (InputBlocked)
            {
                _moveDirection = Vector3.zero;
                return;
            }

            // Do not process movement if cursor is unlocked (meaning player is interacting with UI panels)
            if (Cursor.lockState == CursorLockMode.None)
            {
                _moveDirection = Vector3.zero;
                if (!_characterController.isGrounded)
                {
                    _moveDirection.y -= gravity * Time.deltaTime;
                    _characterController.Move(_moveDirection * Time.deltaTime);
                }
                return;
            }

            // Retrieve hardware input states dynamically
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            float moveForward = 0f;
            float moveRight = 0f;
            float mouseDeltaX = 0f;
            float mouseDeltaY = 0f;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveForward = 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveForward = -1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveRight = 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveRight = -1f;
            }

            if (mouse != null)
            {
                var delta = mouse.delta.ReadValue();
                mouseDeltaX = delta.x;
                mouseDeltaY = delta.y;
            }

            // A. Handle Camera Orbit (Rotation)
            _rotationY += mouseDeltaX * lookSpeed;
            _rotationX -= mouseDeltaY * lookSpeed;
            _rotationX = Mathf.Clamp(_rotationX, lookXLimitMin, lookXLimitMax);

            // B. Rotate Player to align with camera horizontal view when moving
            if (moveForward != 0 || moveRight != 0)
            {
                float targetAngle = _rotationY;
                if (moveForward < 0) targetAngle += 180f;
                if (moveRight > 0) targetAngle += (moveForward > 0 ? 45f : (moveForward < 0 ? -45f : 90f));
                if (moveRight < 0) targetAngle -= (moveForward > 0 ? 45f : (moveForward < 0 ? -45f : 90f));
                
                Quaternion targetRotation = Quaternion.Euler(0, targetAngle, 0);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // C. Calculate Camera Position in Third Person
            if (playerCamera != null)
            {
                Quaternion cameraRotation = Quaternion.Euler(_rotationX, _rotationY, 0);
                Vector3 cameraOffset = cameraRotation * new Vector3(0, 0, -cameraDistance);
                
                // Focus camera slightly above player pivot (hips/shoulders level)
                Vector3 targetCameraPos = transform.position + new Vector3(0, cameraHeight, 0) + cameraOffset;
                
                playerCamera.position = targetCameraPos;
                playerCamera.rotation = Quaternion.LookRotation((transform.position + Vector3.up * (cameraHeight * 0.7f)) - playerCamera.position);
            }

            // D. Handle Movement relative to camera's horizontal view
            Vector3 camForward = playerCamera != null ? playerCamera.forward : transform.forward;
            Vector3 camRight = playerCamera != null ? playerCamera.right : transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 desiredDirection = camForward * moveForward + camRight * moveRight;
            float movementDirectionY = _moveDirection.y;

            if (desiredDirection.sqrMagnitude > 0.001f)
            {
                desiredDirection.Normalize();
                _moveDirection = desiredDirection * moveSpeed;
            }
            else
            {
                _moveDirection = Vector3.zero;
            }

            // Apply gravity
            if (!_characterController.isGrounded)
            {
                _moveDirection.y = movementDirectionY - gravity * Time.deltaTime;
            }
            else
            {
                _moveDirection.y = -0.5f;
            }

            // Move the controller
            _characterController.Move(_moveDirection * Time.deltaTime);
        }

        /// <summary>
        /// Helper to unlock the cursor when interacting with UI panels.
        /// </summary>
        public void SetCursorState(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
