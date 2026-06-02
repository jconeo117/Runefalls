using UnityEngine;

namespace Runefall.Presentation.Network
{
    /// <summary>
    /// Placed on the huge boss room door trigger. Detects local player proximity,
    /// prompts them to interact, unlocks their mouse cursor, and toggles the Network Lobby UI Canvas.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BossRoomDoorTrigger : MonoBehaviour
    {
        [Header("UI Reference")]
        [Tooltip("The parent Canvas containing the LobbyView and LobbyPresenter components.")]
        [SerializeField] private GameObject lobbyUICanvas;

        [Header("Visual Prompts (Optional)")]
        [Tooltip("A local 3D floating text or prompt UI asking the player to interact.")]
        [SerializeField] private GameObject interactPrompt;

        private bool _playerInside = false;
        private SimpleCharacterController _localPlayerController;
        private bool _uiClosedManually = false;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true; // Ensure it behaves strictly as a trigger

            if (lobbyUICanvas != null)
            {
                lobbyUICanvas.SetActive(false);
            }

            if (interactPrompt != null)
            {
                interactPrompt.SetActive(false);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Verify if the object entering is the local player
            var controller = other.GetComponent<SimpleCharacterController>();
            if (controller != null)
            {
                _playerInside = true;
                _localPlayerController = controller;

                if (interactPrompt != null)
                {
                    interactPrompt.SetActive(true);
                }

                // Only auto-open if the player has not closed the UI manually during this proximity session
                if (!_uiClosedManually)
                {
                    OpenLobbyUI();
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var controller = other.GetComponent<SimpleCharacterController>();
            if (controller != null && controller == _localPlayerController)
            {
                // If the wait room is active/assembled, do not close the UI or clear the controller,
                // because the player was physically moved to the preparation slot outside this trigger collider.
                var assembler = FindFirstObjectByType<WaitRoomAssembler>();
                if (assembler == null)
                {
                    #pragma warning disable CS0618
                    assembler = FindObjectOfType<WaitRoomAssembler>();
                    #pragma warning restore CS0618
                }

                if (assembler != null && assembler.IsWaitRoomAssembled)
                {
                    return; // Retain active wait room lobby UI state
                }

                CloseLobbyUI();
                
                _playerInside = false;
                _localPlayerController = null;
                _uiClosedManually = false; // Reset state once player walks completely away from the door
            }
        }

        /// <summary>
        /// Call this when the UI is closed manually via the X button,
        /// ensuring the trigger does not auto-reopen the panel until the player exits and re-enters.
        /// </summary>
        public void NotifyUIClose()
        {
            _uiClosedManually = true;
        }

        private void OpenLobbyUI()
        {
            if (lobbyUICanvas != null)
            {
                lobbyUICanvas.SetActive(true);
            }

            if (interactPrompt != null)
            {
                interactPrompt.SetActive(false); // Hide prompt once UI is active
            }

            // Unlock mouse cursor for UI interaction
            if (_localPlayerController != null)
            {
                _localPlayerController.SetCursorState(locked: false);
            }
        }

        private void CloseLobbyUI()
        {
            if (lobbyUICanvas != null)
            {
                lobbyUICanvas.SetActive(false);
            }

            if (interactPrompt != null)
            {
                interactPrompt.SetActive(false);
            }

            // Re-lock mouse cursor to resume movement
            if (_localPlayerController != null)
            {
                _localPlayerController.SetCursorState(locked: true);
            }
        }
    }
}
