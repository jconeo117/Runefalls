using System;
using System.Collections.Generic;

namespace Runefall.Combat
{
    /// <summary>
    /// Manages active effects (buffs, debuffs, DoTs, stances) on an ICombatActor.
    /// TurnManager calls Tick() at end of each round.
    ///
    /// Stacking rules (driven by EffectDefinition fields, not a flag enum):
    ///   stackBySource=false → Apply() always adds a new entry
    ///   stackBySource=true  → matches existing entry by Source SO reference
    ///     stackable=true  → increments Stacks
    ///     stackable=false → refreshes TurnsRemaining only
    /// </summary>
    public class ActorEffects
    {
        private readonly ICombatActor       _actor;
        private readonly List<ActiveEffect> _effects = new();

        /// <summary>Fires whenever effects are added, stacked, refreshed, or removed. Subscribe to rebuild full UI.</summary>
        public event Action OnEffectsChanged;
        /// <summary>Fires only when an effect is applied or gains a new stack — use to trigger name popups.</summary>
        public event Action<ActiveEffect> OnEffectApplied;

        /// <summary>Read-only view of all active effects — for UI display only, do not mutate.</summary>
        public IReadOnlyList<ActiveEffect> ActiveEffects => _effects;

        public ActorEffects(ICombatActor actor) => _actor = actor;

        public int  AdvantageCount    => CountTag(EffectTag.Advantage);
        public int  DisadvantageCount => CountTag(EffectTag.Disadvantage);

        // ── Queries ───────────────────────────────────────────────────────────────

        /// <summary>True if any active effect has the given mechanical behaviour.</summary>
        public bool HasBehavior(EffectBehavior b)
        {
            for (int i = 0; i < _effects.Count; i++)
                if (_effects[i].Source != null && (_effects[i].Source.behavior & b) != 0) return true;
            return false;
        }

        /// <summary>True if any active effect was applied by an EffectDefinition of type T.</summary>
        public bool HasEffect<T>() where T : EffectDefinition
        {
            for (int i = 0; i < _effects.Count; i++)
                if (_effects[i].Source is T) return true;
            return false;
        }

        /// <summary>Total stacks of effects whose Source is of type T.</summary>
        public int GetStacks<T>() where T : EffectDefinition
        {
            int total = 0;
            for (int i = 0; i < _effects.Count; i++)
                if (_effects[i].Source is T) total += _effects[i].Stacks;
            return total;
        }

        /// <summary>Multiplier applied to all incoming damage. Sums IncomingDamageBonus * Stacks across all effects.</summary>
        public float DamageReceivedMultiplier()
        {
            float bonus = 0f;
            for (int i = 0; i < _effects.Count; i++)
                bonus += _effects[i].IncomingDamageBonus * _effects[i].Stacks;
            return 1f + bonus;
        }

        // ── Mutation ──────────────────────────────────────────────────────────────

        public void Apply(ActiveEffect effect)
        {
            // stackBySource=true: find existing entry by Source SO and stack or refresh
            if (effect.Source != null && effect.Source.stackBySource)
            {
                for (int i = 0; i < _effects.Count; i++)
                {
                    if (_effects[i].Source == effect.Source)
                    {
                        if (effect.Source.stackable) _effects[i].Stacks++;
                        _effects[i].TurnsRemaining = effect.TurnsRemaining;
                        OnEffectApplied?.Invoke(effect);
                        OnEffectsChanged?.Invoke();
                        return;
                    }
                }
            }

            _effects.Add(effect);
            if (effect.StatMod != null)
                _actor.Model.AddStatModifier(effect.StatMod);
            if (effect.Source != null && (effect.Source.behavior & EffectBehavior.BlockHeal) != 0)
                _actor.Model.IsHealBlocked = true;

            OnEffectApplied?.Invoke(effect);
            OnEffectsChanged?.Invoke();
        }

        /// <summary>End-of-round tick: apply DoT, decrement durations, remove expired. TurnsRemaining=-1 = permanent.</summary>
        public void Tick()
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var e = _effects[i];
                if (e.TickDamage && e.StoredValue > 0f && _actor.IsAlive)
                    _actor.Model.TakeDamage(e.StoredValue);

                if (e.TurnsRemaining != -1)
                {
                    e.TurnsRemaining--;
                    if (e.TurnsRemaining <= 0)
                        RemoveAt(i, e);
                }
            }

            OnEffectsChanged?.Invoke();
        }

        /// <summary>Remove all effects with the given tag (limpieza mechanic).</summary>
        public void RemoveByTag(EffectTag tag)
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
                if (_effects[i].Tag == tag) RemoveAt(i, _effects[i]);
        }

        /// <summary>Remove all effects sharing a groupId (arrebato expiry on paired actor).</summary>
        public void RemoveByGroup(string groupId)
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
                if (_effects[i].GroupId == groupId) RemoveAt(i, _effects[i]);
        }

        private void RemoveAt(int index, ActiveEffect e)
        {
            _effects.RemoveAt(index);

            if (e.StatMod != null)
                _actor.Model.RemoveStatModifier(e.StatMod);

            // Recompute BlockHeal: only clear if no remaining effect still applies it
            if (e.Source != null && (e.Source.behavior & EffectBehavior.BlockHeal) != 0)
                if (!HasBehavior(EffectBehavior.BlockHeal))
                    _actor.Model.IsHealBlocked = false;

            // Arrebato: remove the paired effect on the linked actor
            if (!string.IsNullOrEmpty(e.GroupId) && e.LinkedActor != null)
                e.LinkedActor.Effects.RemoveByGroup(e.GroupId);

            OnEffectsChanged?.Invoke();
        }

        private int CountTag(EffectTag tag)
        {
            int n = 0;
            for (int i = 0; i < _effects.Count; i++)
                if (_effects[i].Tag == tag) n++;
            return n;
        }
    }
}
