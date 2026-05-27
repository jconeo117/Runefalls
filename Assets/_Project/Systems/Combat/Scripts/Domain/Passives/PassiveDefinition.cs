using UnityEngine;

namespace Runefall.Combat
{
    /// <summary>
    /// Base class for all passive abilities. Subclass with [CreateAssetMenu] for each concrete passive.
    ///
    /// Lifecycle (managed by CombatBootstrapper):
    ///   Activate   — called once at combat start; subscribe to TurnManager events here.
    ///   Deactivate — called on combat end or owner death; unsubscribe all events here.
    ///
    /// Contract:
    ///   - No MonoBehaviour dependencies. Pure domain logic only.
    ///   - Store no persistent state between combats (reset in Deactivate or use instance fields
    ///     that are guaranteed reset on each Activate call).
    ///   - Never call Activate/Deactivate directly — always through CombatBootstrapper.
    /// </summary>
    public abstract class PassiveDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string passiveName;
        [TextArea] public string description;

        /// <summary>
        /// Called once at combat start. Subscribe to TurnManager events here.
        /// owner   — the ICombatActor this passive belongs to.
        /// tm      — the running TurnManager; subscribe to OnPlayerTurnStarted, OnActionResolved, etc.
        /// ctx     — the full combat context: players, enemies, round state.
        /// </summary>
        public abstract void Activate(ICombatActor owner, TurnManager tm, CombatContext ctx);

        /// <summary>
        /// Called when combat ends (win, loss, or retreat). Unsubscribe all events and reset instance state.
        /// </summary>
        public abstract void Deactivate(ICombatActor owner, TurnManager tm);
    }
}
