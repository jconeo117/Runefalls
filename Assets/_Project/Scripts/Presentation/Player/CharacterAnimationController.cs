using UnityEngine;
using Runefall.Characters;

namespace Runefall.Presentation.Player
{
    /// <summary>
    /// Traduce estado de movimiento a parámetros del Animator.
    /// No sabe de lógica de juego — recibe comandos simples.
    /// Asignar Animator e PlayerController en el inspector.
    /// </summary>
    public class CharacterAnimationController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Animator         animator;
        [SerializeField] private PlayerController playerController;

        // ── Hashes precalculados (más rápido que strings en runtime) ─────────
        private static readonly int SpeedHash  = Animator.StringToHash("Speed");
        private static readonly int DodgeHash  = Animator.StringToHash("Dodge");
        private static readonly int AttackHash = Animator.StringToHash("AttackIndex");

        // ── Ciclo de vida ────────────────────────────────────────────────────

        private void Awake()
        {
            if (animator == null)
            {
                Debug.LogError("[CharacterAnimationController] Animator no asignado.", this);
                enabled = false;
                return;
            }
            if (playerController == null)
            {
                Debug.LogError("[CharacterAnimationController] PlayerController no asignado.", this);
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            // Damping 0.1s para transición suave entre estados
            animator.SetFloat(SpeedHash, playerController.CurrentSpeed, 0.1f, Time.deltaTime);
        }

        // ── API pública (sistemas de combate, Sprint 3+) ─────────────────────

        /// <summary>Dispara animación de dodge.</summary>
        public void PlayDodge() =>
            animator.SetTrigger(DodgeHash);

        /// <summary>Dispara animación de skill por índice (0=skill1, 1=skill2).</summary>
        public void PlaySkill(int skillIndex) =>
            animator.SetTrigger(AttackHash + skillIndex);

        /// <summary>
        /// Conecta eventos del modelo de personaje.
        /// Sprint 3: model.OnStatusApplied += HandleStatus;
        /// </summary>
        public void Init(CharacterModel model)
        {
            // Sprint 3 — descomentar cuando CharacterModel tenga OnStatusApplied:
            // model.OnStatusApplied += HandleStatus;
        }
    }
}
