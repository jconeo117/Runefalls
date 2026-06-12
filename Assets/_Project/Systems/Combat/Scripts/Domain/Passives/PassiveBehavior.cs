using UnityEngine;

namespace Runefall.Combat
{
    /// <summary>
    /// One reusable CLAUSE of a passive — e.g. "apply an effect at the start of the player's turn",
    /// "heal on crit vs a marked target", "double damage vs frozen". A ScriptableObject so passives are
    /// composed in the Inspector (strategy pattern, mirroring <see cref="EffectDefinition"/>).
    ///
    /// Contract:
    ///   - Bind() registers the behavior's reactions on the PassiveContext, which auto-tracks them for a
    ///     clean teardown — never subscribe to TurnManager directly.
    ///   - Keep ALL per-activation state in locals captured by the handler closures created in Bind().
    ///     This SO asset is shared across actors/combats; storing runtime state in fields would clash.
    ///   - Pure domain logic. No MonoBehaviour / Presentation dependencies.
    /// </summary>
    public abstract class PassiveBehavior : ScriptableObject
    {
        [Tooltip("Editor note only — what this clause does. Does not affect runtime.")]
        [TextArea] public string note;

        public abstract void Bind(PassiveContext ctx);
    }
}
