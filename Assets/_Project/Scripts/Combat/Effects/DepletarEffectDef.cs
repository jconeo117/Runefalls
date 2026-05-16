using UnityEngine;

namespace Runefall.Combat
{
    /// <summary>Stub — ultimate gauge defined in Sprint 5.6 (5 orbs). No-op until then.</summary>
    [CreateAssetMenu(menuName = "Runefall/Effects/DepletarEffect")]
    public class DepletarEffectDef : EffectDefinition
    {
        [Range(1, 3)] public int orbsToRemove = 1;

        public override void Execute(EffectExecutionContext ctx) { /* stub: wired in 5.6 */ }
    }
}
