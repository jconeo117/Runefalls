using System;

namespace Runefall.Characters
{
    /// <summary>
    /// Estado de un personaje: HP, MP y stats base.
    /// Clase C# pura — sin MonoBehaviour, sin dependencias de UnityEngine.
    /// Sprint 3 añadirá CharacterData SO, CharacterStats, cartas y runas.
    /// </summary>
    public class CharacterModel
    {
        // ── Identidad ────────────────────────────────────────────────────────
        public string Name { get; }

        // ── HP ───────────────────────────────────────────────────────────────
        public float CurrentHP  { get; private set; }
        public float MaxHP      { get; }
        public bool  IsAlive    => CurrentHP > 0f;

        // ── MP ───────────────────────────────────────────────────────────────
        public float CurrentMP  { get; private set; }
        public float MaxMP      { get; }

        // ── Stats base (migrarán a CharacterStats en Sprint 3) ───────────────
        public float BaseAttack  { get; }
        public float BaseDefense { get; }

        // ── Eventos (Presentación suscribe, dominio nunca conoce al suscriptor)
        public event Action<float> OnHPChanged;
        public event Action<float> OnMPChanged;

        // ── Constructor ──────────────────────────────────────────────────────
        public CharacterModel(string name, float maxHP, float maxMP,
                              float baseAttack, float baseDefense)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new System.ArgumentException("Name cannot be null or empty.", nameof(name));
            if (maxHP <= 0f)
                throw new System.ArgumentOutOfRangeException(nameof(maxHP), "MaxHP must be greater than zero.");
            if (maxMP < 0f)
                throw new System.ArgumentOutOfRangeException(nameof(maxMP), "MaxMP cannot be negative.");

            Name        = name;
            MaxHP       = maxHP;
            MaxMP       = maxMP;
            BaseAttack  = baseAttack;
            BaseDefense = baseDefense;
            CurrentHP   = maxHP;
            CurrentMP   = maxMP;
        }

        // ── HP ───────────────────────────────────────────────────────────────

        /// <summary>Reduce el HP. Ignora valores menores o iguales a 0.</summary>
        public void TakeDamage(float amount)
        {
            if (amount <= 0f) return;
            CurrentHP = Clamp(CurrentHP - amount, 0f, MaxHP);
            OnHPChanged?.Invoke(CurrentHP);
        }

        /// <summary>Aumenta el HP. Ignora valores menores o iguales a 0.</summary>
        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            CurrentHP = Clamp(CurrentHP + amount, 0f, MaxHP);
            OnHPChanged?.Invoke(CurrentHP);
        }

        // ── MP ───────────────────────────────────────────────────────────────

        /// <summary>Reduce el MP. Ignora valores menores o iguales a 0.</summary>
        public void UseMP(float amount)
        {
            if (amount <= 0f) return;
            CurrentMP = Clamp(CurrentMP - amount, 0f, MaxMP);
            OnMPChanged?.Invoke(CurrentMP);
        }

        /// <summary>Aumenta el MP. Ignora valores menores o iguales a 0.</summary>
        public void RestoreMP(float amount)
        {
            if (amount <= 0f) return;
            CurrentMP = Clamp(CurrentMP + amount, 0f, MaxMP);
            OnMPChanged?.Invoke(CurrentMP);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Clamps a value between min and max. Compatible with .NET Standard 2.1.</summary>
        private static float Clamp(float value, float min, float max)
            => value < min ? min : value > max ? max : value;
    }
}
