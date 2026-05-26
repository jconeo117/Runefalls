using Unity.Cinemachine;
using UnityEngine;

namespace Runefall.Presentation.Player
{
    /// <summary>
    /// Único sistema que habla con Cinemachine.
    /// Gestiona tres modos de cámara: exploración, combate y lock-on.
    /// Asignar en un GameObject de la escena junto a CinemachineBrain.
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        [Header("Cámaras virtuales")]
        [SerializeField] private CinemachineCamera explorationCam; // + CinemachineOrbitalFollow + CinemachineDeoccluder
        [SerializeField] private CinemachineCamera combatCam;      // + CinemachineFollow
        [SerializeField] private CinemachineCamera lockOnCam;      // + CinemachineHardLookAt

        [Header("Prioridades")]
        [SerializeField] private int priorityActive   = 20;
        [SerializeField] private int priorityInactive = 0;
        [SerializeField] private int priorityLockOn   = 30;

        // ── Estado ──────────────────────────────────────────────────────────
        private CameraMode currentMode = CameraMode.Exploration;

        public enum CameraMode { Exploration, Combat, LockOn }

        // ── API pública ──────────────────────────────────────────────────────

        public void SwitchToExploration()
        {
            if (explorationCam != null) explorationCam.Priority = priorityActive;
            if (combatCam      != null) combatCam.Priority      = priorityInactive;
            if (lockOnCam      != null) lockOnCam.Priority      = priorityInactive;
            currentMode = CameraMode.Exploration;
        }

        public void SwitchToCombat()
        {
            if (combatCam      != null) combatCam.Priority      = priorityActive;
            if (explorationCam != null) explorationCam.Priority = priorityInactive;
            if (lockOnCam      != null) lockOnCam.Priority      = priorityInactive;
            currentMode = CameraMode.Combat;
        }

        public void SetLockOnTarget(Transform target)
        {
            if (target == null)
            {
                ClearLockOn();
                return;
            }

            if (lockOnCam == null) return;
            lockOnCam.LookAt        = target;
            lockOnCam.Priority      = priorityLockOn;
            if (explorationCam != null) explorationCam.Priority = priorityInactive;
            if (combatCam      != null) combatCam.Priority      = priorityInactive;
            currentMode = CameraMode.LockOn;
        }

        public void ClearLockOn()
        {
            if (lockOnCam != null) lockOnCam.Priority = priorityInactive;
            if (currentMode == CameraMode.LockOn)
                SwitchToExploration();
        }

        public CameraMode CurrentMode => currentMode;

        // ── Ciclo de vida ────────────────────────────────────────────────────

        private void Awake()
        {
            ValidateCameras();
            SwitchToExploration();
        }

        private void ValidateCameras()
        {
            if (explorationCam == null)
                Debug.LogError("[CameraManager] explorationCam no asignada.", this);
            if (combatCam == null)
                Debug.LogWarning("[CameraManager] combatCam no asignada — modo combate no disponible.", this);
            if (lockOnCam == null)
                Debug.LogWarning("[CameraManager] lockOnCam no asignada — lock-on no disponible.", this);
        }
    }
}
