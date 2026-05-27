using UnityEngine;

namespace Runefall.Presentation.Player
{
    /// Attach to Main Camera (alongside CinemachineBrain).
    /// Runs after Cinemachine positions the camera — if a wall blocks the line
    /// pivot→camera, slides the camera closer along the same ray so the player
    /// is always visible. Rotation is untouched; HardLookAt already set it.
    [DefaultExecutionOrder(1000)]
    public class CameraWallAvoidance : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float     pivotHeight  = 1.5f;   // match OrbitalFollow TargetOffset.y
        [SerializeField] private float     probeRadius  = 0.25f;
        [SerializeField] private float     minDistance  = 0.8f;
        [SerializeField] private LayerMask obstacleMask = ~0;     // exclude Player layer in Inspector

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            Vector3 toCam = transform.position - pivot;
            float   dist  = toCam.magnitude;

            if (dist < 0.01f) return;

            Vector3 dir = toCam / dist;

            if (Physics.SphereCast(pivot, probeRadius, dir, out RaycastHit hit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
                transform.position = pivot + dir * Mathf.Max(hit.distance - probeRadius, minDistance);
        }
    }
}
