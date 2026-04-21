using System;
using Runefall.Characters;

namespace Runefall.Combat
{
    public static class CombatFormulas
    {
        private static readonly Random rng = new Random();

        public static float CalculateDamage(
            float rawAttack, float targetDefense,
            ElementType attackElement, ElementType targetElement,
            float critChance, float critMultiplier)
        {
            float elementalMult = GetElementalMultiplier(attackElement, targetElement);
            bool isCrit = (float)rng.NextDouble() < critChance;
            float critFactor = isCrit ? critMultiplier : 1f;
            float defense = Math.Max(1f, targetDefense);

            return rawAttack * elementalMult * critFactor * (100f / (100f + defense));
        }

        public static float GetElementalMultiplier(ElementType atk, ElementType def) =>
            (atk, def) switch
            {
                (ElementType.Fire,   ElementType.Ice)    => 1.5f,
                (ElementType.Ice,    ElementType.Shadow) => 1.5f,
                (ElementType.Shadow, ElementType.Fire)   => 1.5f,
                (ElementType.Fire,   ElementType.Fire)   => 0.5f,
                (ElementType.Ice,    ElementType.Ice)    => 0.5f,
                (ElementType.Shadow, ElementType.Shadow) => 0.5f,
                _ => 1.0f
            };
    }
}
