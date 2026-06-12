using System;
using System.Collections.Generic;

namespace Runefall.Combat
{
    /// <summary>
    /// Runtime facade for a passive's behaviors during ONE combat. Created per owner on Activate and
    /// torn down on Deactivate. It centralizes every TurnManager event subscription so behaviors can
    /// never leak handlers, and exposes a small, safe helper API.
    ///
    /// IMPORTANT: a behavior keeps its per-activation state in locals captured by the handler closures it
    /// registers here — NEVER in fields on the (shared) ScriptableObject. That is what makes the same
    /// passive/behavior asset safe to run on several actors at once.
    /// </summary>
    public class PassiveContext
    {
        public ICombatActor  Owner { get; }
        public TurnManager   Tm    { get; }
        public CombatContext Ctx   { get; }

        private readonly List<Action> _unsubscribers = new();

        public PassiveContext(ICombatActor owner, TurnManager tm, CombatContext ctx)
        {
            Owner = owner;
            Tm    = tm;
            Ctx   = ctx;
        }

        // ── event subscriptions (auto-tracked for clean teardown) ──────────────────

        public void OnPlayerTurnStarted(Action<int> handler)
        {
            Tm.OnPlayerTurnStarted += handler;
            _unsubscribers.Add(() => Tm.OnPlayerTurnStarted -= handler);
        }

        public void OnPlayerTurnBegin(Action<int> handler)
        {
            Tm.OnPlayerTurnBegin += handler;
            _unsubscribers.Add(() => Tm.OnPlayerTurnBegin -= handler);
        }

        public void OnEnemyTurnStarted(Action handler)
        {
            Tm.OnEnemyTurnStarted += handler;
            _unsubscribers.Add(() => Tm.OnEnemyTurnStarted -= handler);
        }

        public void OnActionResolved(Action<CombatActionResult> handler)
        {
            Tm.OnActionResolved += handler;
            _unsubscribers.Add(() => Tm.OnActionResolved -= handler);
        }

        /// <summary>Unsubscribe every handler this context registered. Idempotent.</summary>
        public void UnbindAll()
        {
            for (int i = 0; i < _unsubscribers.Count; i++) _unsubscribers[i]?.Invoke();
            _unsubscribers.Clear();
        }

        // ── helpers ────────────────────────────────────────────────────────────────

        /// <summary>Apply an effect to a single target as if cast by the owner.</summary>
        public void ApplyEffect(EffectDefinition effect, ICombatActor target, int rank = 1)
        {
            if (effect == null || target == null) return;
            effect.Execute(new EffectExecutionContext { Caster = Owner, Target = target, Rank = rank });
        }

        /// <summary>Apply an effect to every alive actor on a side (helper for "apply to all enemies/allies").</summary>
        public void ApplyEffectToAll(EffectDefinition effect, IReadOnlyList<ICombatActor> side, int rank = 1)
        {
            if (effect == null || side == null) return;
            for (int i = 0; i < side.Count; i++)
            {
                var a = side[i];
                if (a != null && a.IsAlive) ApplyEffect(effect, a, rank);
            }
        }
    }
}
