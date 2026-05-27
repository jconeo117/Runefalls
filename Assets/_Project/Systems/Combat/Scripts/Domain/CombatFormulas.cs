using System;
using Runefall.Characters;

namespace Runefall.Combat
{
    public readonly struct DamageResult
    {
        public readonly float damage;
        public readonly bool  isCrit;
        public readonly float lifeSteal;

        public DamageResult(float damage, bool isCrit, float lifeSteal)
        {
            this.damage    = damage;
            this.isCrit    = isCrit;
            this.lifeSteal = lifeSteal;
        }
    }

    public static class CombatFormulas
    {
        private static readonly Random rng = new Random();

        public static DamageResult CalculateDamage(
            CharacterStats attacker,
            CharacterStats defender,
            ElementType attackElement,
            ElementType targetElement,
            bool  ignoreDefense        = false,
            float critDamageMultiplier = 1f,
            float critChanceMultiplier = 1f)
        {
            float ataque = attacker.ofensivas.ataque;

            float total;
            if (ignoreDefense)
            {
                total = ataque;
            }
            else
            {
                // Perforacion — fracción del daño que omite defensa
                float pen       = Math.Max(0f, attacker.ofensivas.perforacion);
                float penDmg    = ataque * pen;
                float normalDmg = ataque * (1f - pen);

                // Defensa reduce porción normal: formula 100/(100+def)
                float defense       = Math.Max(1f, defender.defensivas.defensa);
                float reducedNormal = normalDmg * (100f / (100f + defense));
                total = reducedNormal + penDmg;

                // Resistencia — reducción plana porcentual del total
                float resistencia = Math.Min(1f, Math.Max(0f, defender.defensivas.resistencia));
                total *= (1f - resistencia);
            }

            // Elemento
            total *= GetElementalMultiplier(attackElement, targetElement);

            // Critico — resistenciaCrit del defensor reduce prob de critico del atacante
            float rawCritChance       = attacker.ofensivas.critChance * critChanceMultiplier;
            float effectiveCritChance = Math.Max(0f, rawCritChance - defender.defensivas.resistenciaCrit);
            bool  isCrit              = (float)rng.NextDouble() < effectiveCritChance;

            if (isCrit)
            {
                // defensaCrit reduce el bonus de daño critico (no el daño base)
                float critBonus        = Math.Max(0f, attacker.ofensivas.critDaño * critDamageMultiplier - 1f);
                float reducedCritBonus = critBonus * Math.Max(0f, 1f - defender.defensivas.defensaCrit);
                total *= (1f + reducedCritBonus);
            }

            float lifeSteal = total * Math.Max(0f, attacker.vitales.roboDeVida);

            return new DamageResult(total, isCrit, lifeSteal);
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
