using UnityEngine;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Keeps a World Space Canvas oriented toward the main camera each frame.
    /// Attach to the Canvas GO — not to the character root.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class BillboardCanvas : MonoBehaviour
    {
        private Camera _cam;

        private void Start()  => _cam = Camera.main;

        private void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam != null) transform.rotation = _cam.transform.rotation;
        }
    }
}
