using UnityEngine;

namespace Runefall.Combat
{
    [CreateAssetMenu(menuName = "Runefall/Passives/DefaultPassive")]
    public class DefaultPassiveDefinition : PassiveDefinition
    {
        [Header("Default Combat Start Effects")]
        [Tooltip("Efectos del pipeline aplicados automáticamente al propietario al iniciar el combate.")]
        public EffectDefinition[] combatStartEffects;

        public override void Activate(ICombatActor owner, TurnManager tm, CombatContext ctx)
        {
            if (combatStartEffects == null || combatStartEffects.Length == 0) return;

            var execCtx = new EffectExecutionContext
            {
                Caster = owner,
                Target = owner,
                Rank   = 1
            };

            for (int i = 0; i < combatStartEffects.Length; i++)
            {
                if (combatStartEffects[i] != null)
                {
                    combatStartEffects[i].Execute(execCtx);
                }
            }
        }

        public override void Deactivate(ICombatActor owner, TurnManager tm)
        {
            // Simple start-of-combat effects do not subscribe to events, so no deactivation cleanup is needed.
        }
    }
}
