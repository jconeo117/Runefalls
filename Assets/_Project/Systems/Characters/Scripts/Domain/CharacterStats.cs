using System;
using UnityEngine;

namespace Runefall.Characters
{
    [Serializable]
    public class OffensiveStats
    {
        public float ataque;
        public float perforacion;
        public float critChance;
        public float critDaño;
    }

    [Serializable]
    public class DefensiveStats
    {
        public float defensa;
        public float resistencia;
        public float defensaCrit;
        public float resistenciaCrit;
    }

    [Serializable]
    public class VitalStats
    {
        public float ps;
        public float roboDeVida;
        public float tasaRegen;
        public float tasaRecuperacion;
    }

    [Serializable]
    public class CharacterStats
    {
        public OffensiveStats ofensivas  = new OffensiveStats();
        public DefensiveStats defensivas = new DefensiveStats();
        public VitalStats     vitales    = new VitalStats();

        // Stats principales: ps (0.45) > ataque (0.35) > defensa (0.20)
        // Substats ofensivas (0.08), defensivas (0.05), vitales (0.04)
        public float ClaseDeCombate =>
            vitales.ps       * 0.45f
            + ofensivas.ataque  * 0.35f
            + defensivas.defensa * 0.20f
            + (ofensivas.perforacion  + ofensivas.critChance   + ofensivas.critDaño)         * 0.08f
            + (defensivas.resistencia + defensivas.defensaCrit + defensivas.resistenciaCrit) * 0.05f
            + (vitales.roboDeVida     + vitales.tasaRegen      + vitales.tasaRecuperacion)   * 0.04f;

        public CharacterStats Clone() => new CharacterStats
        {
            ofensivas = new OffensiveStats
            {
                ataque      = ofensivas.ataque,
                perforacion = ofensivas.perforacion,
                critChance  = ofensivas.critChance,
                critDaño    = ofensivas.critDaño
            },
            defensivas = new DefensiveStats
            {
                defensa          = defensivas.defensa,
                resistencia      = defensivas.resistencia,
                defensaCrit      = defensivas.defensaCrit,
                resistenciaCrit  = defensivas.resistenciaCrit
            },
            vitales = new VitalStats
            {
                ps                = vitales.ps,
                roboDeVida        = vitales.roboDeVida,
                tasaRegen         = vitales.tasaRegen,
                tasaRecuperacion  = vitales.tasaRecuperacion
            }
        };

        // Principales: bonus multiplicativo. Substats: bonus aditivo.
        public static CharacterStats operator +(CharacterStats a, StatModifier m)
        {
            var r = a.Clone();
            r.ofensivas.ataque           *= 1f + m.ataqueBonus;
            r.ofensivas.perforacion      += m.perforacionBonus;
            r.ofensivas.critChance       += m.critChanceBonus;
            r.ofensivas.critDaño         += m.critDañoBonus;
            r.defensivas.defensa         *= 1f + m.defensaBonus;
            r.defensivas.resistencia     += m.resistenciaBonus;
            r.defensivas.defensaCrit     += m.defensaCritBonus;
            r.defensivas.resistenciaCrit += m.resistenciaCritBonus;
            r.vitales.roboDeVida         += m.roboDeVidaBonus;
            r.vitales.tasaRegen          += m.tasaRegenBonus;
            r.vitales.tasaRecuperacion   += m.tasaRecuperacionBonus;
            return r;
        }
    }

    [Serializable]
    public class StatModifier
    {
        public float ataqueBonus;
        public float perforacionBonus;
        public float critChanceBonus;
        public float critDañoBonus;
        public float defensaBonus;
        public float resistenciaBonus;
        public float defensaCritBonus;
        public float resistenciaCritBonus;
        public float roboDeVidaBonus;
        public float tasaRegenBonus;
        public float tasaRecuperacionBonus;

        public static readonly StatModifier Zero = new StatModifier();
    }
}
