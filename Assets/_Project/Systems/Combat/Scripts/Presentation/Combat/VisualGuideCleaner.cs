using UnityEngine;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Tiny helper component to automatically destroy visual guides/placeholders 
    /// at runtime so they don't collide with or block the actual spawned character models.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class VisualGuideCleaner : MonoBehaviour
    {
        private void Awake()
        {
            // Destroy the guide immediately on awake so it is clean for runtime spawning
            Destroy(gameObject);
        }
    }
}
