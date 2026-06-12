using System.Collections.Generic;
using UnityEngine;

namespace Runefall.Combat
{
    /// <summary>
    /// A passive built by COMPOSING reusable <see cref="PassiveBehavior"/> clauses (strategy pattern,
    /// like <see cref="EffectDefinition"/>). The asset is pure data; all runtime state lives in a
    /// per-owner <see cref="PassiveContext"/>, so the same passive asset can serve multiple actors at
    /// once without clashing. A complex passive = a list of independent behaviors.
    /// </summary>
    [CreateAssetMenu(menuName = "Runefall/Passives/Composable Passive")]
    public class ComposablePassive : PassiveDefinition
    {
        [Header("Composition")]
        [Tooltip("The clauses that make up this passive. They are bound in order.")]
        public List<PassiveBehavior> behaviors = new();

        // Per-owner live contexts (transient; cleared on Deactivate). Keyed by owner so one passive asset
        // can run on several actors simultaneously without shared-state clashes.
        private readonly Dictionary<ICombatActor, PassiveContext> _live = new();

        public override void Activate(ICombatActor owner, TurnManager tm, CombatContext ctx)
        {
            if (owner == null || tm == null || ctx == null) return;
            if (_live.ContainsKey(owner)) Deactivate(owner, tm);   // guard against double-activate

            var pctx = new PassiveContext(owner, tm, ctx);
            for (int i = 0; i < behaviors.Count; i++)
                behaviors[i]?.Bind(pctx);
            _live[owner] = pctx;
        }

        public override void Deactivate(ICombatActor owner, TurnManager tm)
        {
            if (owner == null) return;
            if (_live.TryGetValue(owner, out var pctx))
            {
                pctx.UnbindAll();
                _live.Remove(owner);
            }
        }
    }
}
