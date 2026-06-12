using UnityEngine;
using Runefall.Characters;

namespace Runefall.Combat
{
    /// <summary>
    /// Hipotermia — debuff central de la pasiva de Hielo "Dominio Glacial".
    /// Reduce las estadísticas relacionadas con la defensa del objetivo (el stat multiplicativo `defensa`).
    /// Es una clase dedicada (no un StatModEffectDef genérico) para poder CONTARLA y FILTRARLA por tipo:
    /// el orquestador escala el buff del héroe por cada Hipotermia en campo y congela a los enemigos que
    /// la tengan (HasEffect&lt;HipotermiaEffectDef&gt;()).
    ///
    /// Configurar en el asset:
    ///   effectName    = "Hipotermia"
    ///   tag           = Disadvantage
    ///   stackBySource = true   (una por enemigo)
    ///   stackable     = false  (reaplicar solo refresca la duración)
    /// </summary>
    [CreateAssetMenu(menuName = "Runefall/Effects/HipotermiaEffect")]
    public class HipotermiaEffectDef : EffectDefinition
    {
        [Header("Defense Penalty")]
        [Tooltip("Penalización multiplicativa a la defensa. -0.20 = -20%.")]
        public float defensePenalty = -0.20f;

        [Header("Duration")]
        [Tooltip("Turnos que dura. -1 = permanente. [0]=R1 [1]=R2 [2]=R3")]
        public int[] durationByRank = { 1, 1, 1 };

        public override void Execute(EffectExecutionContext ctx)
        {
            if (ctx.Target == null) return;

            int durIdx = Mathf.Clamp(ctx.Rank - 1, 0, durationByRank.Length - 1);

            ctx.Target.Effects.Apply(new ActiveEffect
            {
                Source         = this,
                Tag            = tag,
                Applier        = ctx.Caster,
                TurnsRemaining = durationByRank[durIdx],
                Stacks         = 1,
                StatMod        = new StatModifier { defensaBonus = defensePenalty }
            });
        }
    }
}
