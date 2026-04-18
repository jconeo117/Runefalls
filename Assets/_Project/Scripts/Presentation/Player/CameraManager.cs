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
            explorationCam.Priority = priorityActive;
            combatCam.Priority      = priorityInactive;
            lockOnCam.Priority      = priorityInactive;
            currentMode = CameraMode.Exploration;
        }

        public void SwitchToCombat()
        {
            combatCam.Priority      = priorityActive;
            explorationCam.Priority = priorityInactive;
            lockOnCam.Priority      = priorityInactive;
            currentMode = CameraMode.Combat;
        }

        public void SetLockOnTarget(Transform target)
        {
            if (target == null)
            {
                ClearLockOn();
                return;
            }

            // Asignar target al LookAt del lockOnCam
            lockOnCam.LookAt = target;
            lockOnCam.Priority      = priorityLockOn;
            explorationCam.Priority = priorityInactive;
            combatCam.Priority      = priorityInactive;
            currentMode = CameraMode.LockOn;
        }

        public void ClearLockOn()
        {
            lockOnCam.Priority = priorityInactive;
            // Volver al modo anterior
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
                Debug.LogError("[CameraManager] combatCam no asignada.", this);
            if (lockOnCam == null)
                Debug.LogError("[CameraManager] lockOnCam no asignada.", this);
        }
    }
}
