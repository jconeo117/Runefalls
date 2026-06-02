using UnityEngine;

namespace Runefall.Presentation.Network
{
    /// <summary>
    /// Establishes the physical layout and cinematic camera for the cooperatively integrated Wait Room.
    /// Handles deactivating local room decorations, positioning the local player on the right slot,
    /// placing the remote companion on the left slot (local mirror layout), and framing both against the massive Boss Door.
    /// </summary>
    public class WaitRoomAssembler : MonoBehaviour
    {
        [Header("Lobby Environment Elements")]
        [Tooltip("The central runic altar platform GameObject that will be deactivated during preparation.")]
        [SerializeField] private GameObject altarCenter;

        [Tooltip("The mystic floating crystal GameObject that will be deactivated during preparation.")]
        [SerializeField] private GameObject floatingCrystal;

        [Header("Player Physical Slots")]
        [Tooltip("The physical slot (Transform) on the left side (from camera view facing the door). Usually for the remote ally.")]
        [SerializeField] private Transform slotLeft;

        [Tooltip("The physical slot (Transform) on the right side (from camera view facing the door). Always for the local player.")]
        [SerializeField] private Transform slotRight;

        [Header("Cinematic Camera Framing")]
        [Tooltip("The low angle, slightly tilted upward camera anchor positioned behind the players looking at the boss door.")]
        [SerializeField] private Transform prepCameraAnchor;

        [Header("Slot Visual Platforms")]
        [Tooltip("The visual platform/indicator for the left slot (deactivated by default).")]
        [SerializeField] private GameObject slotLeftVisual;

        [Tooltip("The visual platform/indicator for the right slot (deactivated by default).")]
        [SerializeField] private GameObject slotRightVisual;

        private Vector3 _originalPlayerPosition;
        private Quaternion _originalPlayerRotation;
        private bool _isWaitRoomAssembled = false;

        public Transform SlotLeft => slotLeft;
        public Transform SlotRight => slotRight;
        public Transform PrepCameraAnchor => prepCameraAnchor;
        public bool IsWaitRoomAssembled => _isWaitRoomAssembled;

        private void Awake()
        {
            // Verify slots and anchors exist
            if (slotLeft == null || slotRight == null || prepCameraAnchor == null)
            {
                Debug.LogWarning("[WaitRoomAssembler] Warning: Some player slots or camera anchors are not assigned in the inspector.");
            }

            // Ensure slot visual platforms are hidden during exploration phase
            if (slotLeftVisual != null) slotLeftVisual.SetActive(false);
            if (slotRightVisual != null) slotRightVisual.SetActive(false);
        }

        /// <summary>
        /// Dynamically hides central decorations, snaps the local player to the right slot,
        /// and positions the camera at a premium low cinematic angle style 7DS Grand Cross.
        /// </summary>
        public void AssembleWaitRoom(SimpleCharacterController localPlayer)
        {
            if (_isWaitRoomAssembled) return;

            Debug.Log("[WaitRoomAssembler] Assembling physical waiting room...");

            // 1. Hide central decorations to clear the arena area
            if (altarCenter != null) altarCenter.SetActive(false);
            if (floatingCrystal != null) floatingCrystal.SetActive(false);

            // Activate visual slot platforms/indicators on the ground
            if (slotLeftVisual != null) slotLeftVisual.SetActive(true);
            if (slotRightVisual != null) slotRightVisual.SetActive(true);

            // 2. Cache player's pre-preparation position to restore it if they cancel
            if (localPlayer != null)
            {
                _originalPlayerPosition = localPlayer.transform.position;
                _originalPlayerRotation = localPlayer.transform.rotation;

                // Freeze player controls and unlock cursor for UI interaction
                localPlayer.InputBlocked = true;
                localPlayer.SetCursorState(locked: false);

                // Position the local player always on the right side slot (local mirror effect)
                if (slotRight != null)
                {
                    localPlayer.transform.position = slotRight.position;
                    localPlayer.transform.rotation = slotRight.rotation;
                }
            }

            // 3. Move camera to the cinematic prep camera anchor
            PositionCameraCinematic(localPlayer, active: true);

            _isWaitRoomAssembled = true;
        }

        /// <summary>
        /// Restores the 3D room to its initial Individual Exploration state,
        /// reactivating decorations, releasing the camera, and restoring the player's original position.
        /// </summary>
        public void TeardownWaitRoom(SimpleCharacterController localPlayer)
        {
            if (!_isWaitRoomAssembled) return;

            Debug.Log("[WaitRoomAssembler] Tearing down physical waiting room, restoring individual exploration...");

            // 1. Re-enable central decorations
            if (altarCenter != null) altarCenter.SetActive(true);
            if (floatingCrystal != null) floatingCrystal.SetActive(true);

            // Hide visual slot platforms/indicators again
            if (slotLeftVisual != null) slotLeftVisual.SetActive(false);
            if (slotRightVisual != null) slotRightVisual.SetActive(false);

            // 2. Restore camera control back to the player's third-person controller orbit
            PositionCameraCinematic(localPlayer, active: false);

            // 3. Return player to their original position prior to entering preparation phase
            if (localPlayer != null)
            {
                // Restore controls and re-lock mouse cursor
                localPlayer.InputBlocked = false;
                localPlayer.SetCursorState(locked: true);

                if (_originalPlayerPosition != Vector3.zero)
                {
                    localPlayer.transform.position = _originalPlayerPosition;
                    localPlayer.transform.rotation = _originalPlayerRotation;
                }
            }

            _isWaitRoomAssembled = false;
        }

        /// <summary>
        /// Snaps a remote client's avatar visual representation to the left slot.
        /// </summary>
        public void PositionRemotePlayer(GameObject remotePlayerVisual)
        {
            if (remotePlayerVisual != null && slotLeft != null)
            {
                remotePlayerVisual.transform.position = slotLeft.position;
                remotePlayerVisual.transform.rotation = slotLeft.rotation;
                Debug.Log($"[WaitRoomAssembler] Positioned remote player visual at left slot: {remotePlayerVisual.name}");
            }
        }

        /// <summary>
        /// Handles swapping the camera between cinematic lobby view and normal free third-person orbit.
        /// </summary>
        private void PositionCameraCinematic(SimpleCharacterController localPlayer, bool active)
        {
            if (localPlayer == null) return;

            // Find the camera transform inside the player controller or the scene
            var playerCameraField = typeof(SimpleCharacterController).GetField("playerCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var camTransform = playerCameraField?.GetValue(localPlayer) as Transform;

            if (camTransform == null)
            {
                camTransform = Camera.main != null ? Camera.main.transform : null;
            }

            if (camTransform != null)
            {
                if (active && prepCameraAnchor != null)
                {
                    // Unparent camera so that it's completely independent of player rotation/movement
                    camTransform.SetParent(null);
                    
                    // Snap camera to the static low cinematic angle
                    camTransform.position = prepCameraAnchor.position;
                    camTransform.rotation = prepCameraAnchor.rotation;
                }
                else
                {
                    // Reparent back to player and snap back to standard third person local offset
                    camTransform.SetParent(localPlayer.transform);
                    camTransform.localPosition = new Vector3(0f, 1.8f, -3.5f);
                    camTransform.localRotation = Quaternion.identity;
                    Debug.Log("[WaitRoomAssembler] Restored and reparented third-person camera orbit.");
                }
            }
        }
    }
}
