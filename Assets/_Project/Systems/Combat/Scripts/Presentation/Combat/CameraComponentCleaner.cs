using UnityEngine;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Tiny helper component to automatically destroy standard Camera components 
    /// on virtual cameras at runtime to prevent rendering conflicts, while preserving
    /// the virtual camera GameObject itself.
    /// </summary>
    [DefaultExecutionOrder(-99)]
    public class CameraComponentCleaner : MonoBehaviour
    {
        private void Awake()
        {
            var cam = GetComponent<Camera>();
            if (cam != null)
            {
                Destroy(cam);
            }
            // Destroy this helper script too to keep the hierarchy pristine
            Destroy(this);
        }
    }
}
