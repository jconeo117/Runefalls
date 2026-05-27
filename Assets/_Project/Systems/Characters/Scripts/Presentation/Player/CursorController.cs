using UnityEngine;

namespace Runefall.Presentation.Player
{
    /// <summary>
    /// Locks cursor for exploration mode. Escape toggles lock.
    /// </summary>
    public class CursorController : MonoBehaviour
    {
        private void Awake() => LockCursor();

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                    UnlockCursor();
                else
                    LockCursor();
            }
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
    }
}
