using System;
using System.Collections.Generic;

namespace Runefall.Characters
{
    public class CharacterModel
    {
        public string Name { get; }

        public float CurrentHP     { get; private set; }
        public float MaxHP         => Stats.vitales.ps;
        public bool  IsAlive       => CurrentHP > 0f;
        public float CurrentShield { get; private set; }

        public float CurrentMP { get; private set; }
        public float MaxMP     { get; }

        /// <summary>Set by ActorEffects when Infected is applied/removed. Heal() returns early when true.</summary>
        public bool IsHealBlocked { get; set; }

        public CharacterStats Stats { get; }

        /// <summary>Base stats + all active StatModifiers. Recomputed lazily on change.</summary>
        public CharacterStats EffectiveStats
        {
            get
            {
                if (!_statsDirty) return _effectiveStats;
                _effectiveStats = Stats;
                for (int i = 0; i < _statModifiers.Count; i++)
                    _effectiveStats = _effectiveStats + _statModifiers[i];
                _statsDirty = false;
                return _effectiveStats;
            }
        }

        public event Action<float> OnHPChanged;
        public event Action<float> OnMPChanged;

        private readonly List<StatModifier> _statModifiers = new();
        private CharacterStats _effectiveStats;
        private bool           _statsDirty = true;

        public CharacterModel(string name, CharacterStats stats, float maxMP = 0f)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be null or empty.", nameof(name));
            if (stats == null)
                throw new ArgumentNullException(nameof(stats));
            if (stats.vitales.ps <= 0f)
                throw new ArgumentOutOfRangeException(nameof(stats), "ps must be greater than zero.");
            if (maxMP < 0f)
                throw new ArgumentOutOfRangeException(nameof(maxMP), "MaxMP cannot be negative.");

            Name      = name;
            Stats     = stats;
            MaxMP     = maxMP;
            CurrentHP = MaxHP;
            CurrentMP = maxMP;
        }

        public void AddStatModifier(StatModifier mod)
        {
            _statModifiers.Add(mod);
            _statsDirty = true;
        }

        public void RemoveStatModifier(StatModifier mod)
        {
            _statModifiers.Remove(mod);
            _statsDirty = true;
        }

        public void AddShield(float amount)
        {
            if (amount <= 0f) return;
            CurrentShield += amount;
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f) return;
            if (CurrentShield > 0f)
            {
                float absorbed = Math.Min(CurrentShield, amount);
                CurrentShield -= absorbed;
                amount        -= absorbed;
            }
            if (amount <= 0f) return;
            CurrentHP = Clamp(CurrentHP - amount, 0f, MaxHP);
            OnHPChanged?.Invoke(CurrentHP);
        }

        // Heal amplificado por tasaRecuperacion del receptor. Bloqueado si IsHealBlocked.
        public void Heal(float amount)
        {
            if (IsHealBlocked || amount <= 0f) return;
            float effective = amount * (1f + EffectiveStats.vitales.tasaRecuperacion);
            CurrentHP = Clamp(CurrentHP + effective, 0f, MaxHP);
            OnHPChanged?.Invoke(CurrentHP);
        }

        // Llamar por TurnManager cada N turnos — recupera % de PS perdidos
        public void ApplyRegen()
        {
            if (EffectiveStats.vitales.tasaRegen <= 0f) return;
            float lost   = MaxHP - CurrentHP;
            float regain = lost * EffectiveStats.vitales.tasaRegen;
            if (regain <= 0f) return;
            CurrentHP = Clamp(CurrentHP + regain, 0f, MaxHP);
            OnHPChanged?.Invoke(CurrentHP);
        }

        public void UseMP(float amount)
        {
            if (amount <= 0f) return;
            CurrentMP = Clamp(CurrentMP - amount, 0f, MaxMP);
            OnMPChanged?.Invoke(CurrentMP);
        }

        public void RestoreMP(float amount)
        {
            if (amount <= 0f) return;
            CurrentMP = Clamp(CurrentMP + amount, 0f, MaxMP);
            OnMPChanged?.Invoke(CurrentMP);
        }

        private static float Clamp(float value, float min, float max)
            => value < min ? min : value > max ? max : value;
    }
}
