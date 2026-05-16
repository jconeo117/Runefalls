using System.Collections.Generic;

namespace Runefall.Combat
{
    /// <summary>
    /// Manages active effects (buffs, debuffs, DoTs, stances) on an ICombatActor.
    /// TurnManager calls Tick() at end of each round.
    /// </summary>
    public class ActorEffects
    {
        private readonly ICombatActor     _actor;
        private readonly List<ActiveEffect> _effects = new();

        public ActorEffects(ICombatActor actor) => _actor = actor;

        public int  AdvantageCount    => CountTag(EffectTag.Advantage);
        public int  DisadvantageCount => CountTag(EffectTag.Disadvantage);
        public bool HasFlag(EffectFlag flag)
        {
            for (int i = 0; i < _effects.Count; i++)
                if (_effects[i].Flag == flag) return true;
            return false;
        }

        public int GetStacks(EffectFlag flag)
        {
            int total = 0;
            for (int i = 0; i < _effects.Count; i++)
                if (_effects[i].Flag == flag) total += _effects[i].Stacks;
            return total;
        }

        /// <summary>Multiplier applied to all incoming damage (e.g. llamarada stacks).</summary>
        public float DamageReceivedMultiplier()
        {
            int stacks = GetStacks(EffectFlag.Llamarada);
            return 1f + stacks * 0.1f;
        }

        public void Apply(ActiveEffect effect)
        {
            // Stacking flag effects: increment stacks on the existing entry
            if (effect.Flag != EffectFlag.None)
            {
                for (int i = 0; i < _effects.Count; i++)
                {
                    if (_effects[i].Flag == effect.Flag && _effects[i].Source == effect.Source)
                    {
                        _effects[i].Stacks++;
                        _effects[i].TurnsRemaining = effect.TurnsRemaining;
                        return;
                    }
                }
            }

            _effects.Add(effect);
            if (effect.StatMod != null)
                _actor.Model.AddStatModifier(effect.StatMod);
            if (effect.Flag == EffectFlag.Infected)
                _actor.Model.IsHealBlocked = true;
        }

        /// <summary>End-of-round tick: apply DoT, decrement durations, remove expired.</summary>
        public void Tick()
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var e = _effects[i];
                if (e.TickDamage && e.StoredValue > 0f && _actor.IsAlive)
                    _actor.Model.TakeDamage(e.StoredValue);

                e.TurnsRemaining--;
                if (e.TurnsRemaining <= 0)
                    RemoveAt(i, e);
            }
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

            if (e.Flag == EffectFlag.Infected && !HasFlag(EffectFlag.Infected))
                _actor.Model.IsHealBlocked = false;

            // Arrebato: remove the paired effect on the linked actor
            if (!string.IsNullOrEmpty(e.GroupId) && e.LinkedActor != null)
                e.LinkedActor.Effects.RemoveByGroup(e.GroupId);
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
