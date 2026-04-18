using UnityEngine;

namespace Runefall.Presentation.Player
{
    /// <summary>
    /// Almacena y modifica el HP del jugador.
    /// No dispara eventos ni tiene lógica de muerte — eso va en CharacterModel (Sprint 2.3+).
    /// Asignar en el mismo GameObject que PlayerController.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Salud")]
        [SerializeField] private float maxHP = 100f;

        private float currentHP;

        public float CurrentHP => currentHP;
        public float MaxHP     => maxHP;
        public bool  IsAlive   => currentHP > 0f;

        private void Awake()
        {
            currentHP = maxHP;
        }

        /// <summary>Reduce el HP. No baja de 0.</summary>
        public void TakeDamage(float amount)
        {
            currentHP = Mathf.Clamp(currentHP - amount, 0f, maxHP);
        }

        /// <summary>Aumenta el HP. No supera maxHP.</summary>
        public void Heal(float amount)
        {
            currentHP = Mathf.Clamp(currentHP + amount, 0f, maxHP);
        }
    }
}
