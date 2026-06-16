using UnityEngine;
using Runefall.Characters;

namespace Runefall.Combat
{
    /// <summary>
    /// Pasiva de Kael "Corazón del Fénix": un tanque de aguante cuyo daño escala mientras sobrevive,
    /// con un renacer explosivo al borde de la muerte.
    ///
    /// Cláusulas:
    ///   1. Corazón del Fénix — al inicio de cada turno del héroe, sus stats ofensivas (ataque,
    ///      perforación, prob. crítico, daño crítico) suben +perTurnBonus por turno transcurrido
    ///      (aura permanente recalculada cada turno).
    ///   2. Renacer — si los PS del héroe caen bajo lowHpThreshold (1 vez por combate si oncePerCombat):
    ///      cura total, borra las acumulaciones de Corazón del Fénix, y aplica Berserker Infernal.
    ///   3. Berserker Infernal (1 turno jugable) — +berserkerBonus a stats ofensivas y crítico
    ///      garantizado (critChance ≥ 100%).
    ///
    /// Estado per-activación vive en locals capturados por las closures (Bind), nunca en campos del SO.
    /// </summary>
    [CreateAssetMenu(menuName = "Runefall/Passives/Behaviors/Corazon del Fenix")]
    public class CorazonDelFenixBehavior : PassiveBehavior
    {
        [Header("Corazón del Fénix — escalado por turno")]
        [Tooltip("Bonus a stats ofensivas por cada turno transcurrido. 0.02 = +2%/turno (ataque ×, substats +).")]
        public float perTurnBonus = 0.02f;

        [Header("Renacer — trigger a bajo PS")]
        [Range(0f, 1f)]
        [Tooltip("Umbral de PS que dispara el renacer (cura total + Berserker).")]
        public float lowHpThreshold = 0.40f;
        [Tooltip("True = el renacer ocurre UNA sola vez por combate (fénix). False = re-armable cada vez que cae bajo el umbral.")]
        public bool oncePerCombat = true;

        [Header("Berserker Infernal")]
        [Tooltip("Bonus a stats ofensivas durante el Berserker. 0.75 = +75%.")]
        public float berserkerBonus = 0.75f;
        [Tooltip("Duración interna del Berserker. 2 = un turno jugable completo (el Tick de fin de ronda decrementa una vez antes del turno del jugador).")]
        public int berserkerTurns = 2;

        private const string FenixGroup     = "corazon_del_fenix";
        private const string BerserkerGroup = "berserker_infernal";

        public override void Bind(PassiveContext ctx)
        {
            // ── per-activation state (closures, no SO fields) ──────────────────────
            int  turns       = 0;
            bool phoenixUsed = false;

            // Aura "Corazón del Fénix": stats ofensivas +perTurnBonus por turno. Se regenera cada turno.
            void RecomputeFenixAura()
            {
                ctx.Owner.Effects.RemoveByGroup(FenixGroup);
                float b = perTurnBonus * turns;
                if (b <= 0f) return;

                ctx.Owner.Effects.Apply(new ActiveEffect
                {
                    Source         = null,           // aura interna (sin chip de UI ni conteo de ventaja)
                    Tag            = EffectTag.Neutral,
                    TurnsRemaining = -1,             // permanente; gestionada por recálculo, no por Tick
                    Stacks         = 1,
                    StatMod        = new StatModifier
                    {
                        ataqueBonus      = b,        // multiplicativo
                        perforacionBonus = b,        // aditivo (substat)
                        critChanceBonus  = b,
                        critDañoBonus    = b
                    },
                    GroupId        = FenixGroup
                });
            }

            void TriggerRebirth()
            {
                phoenixUsed = true;

                // Cura total.
                ctx.Owner.Model.Heal(ctx.Owner.Model.MaxHP);

                // Borra las acumulaciones de Corazón del Fénix.
                turns = 0;
                ctx.Owner.Effects.RemoveByGroup(FenixGroup);

                // Berserker Infernal: +berserkerBonus ofensivo + crítico garantizado, 1 turno jugable.
                ctx.Owner.Effects.RemoveByGroup(BerserkerGroup);
                ctx.Owner.Effects.Apply(new ActiveEffect
                {
                    Source         = null,
                    Tag            = EffectTag.Neutral,
                    TurnsRemaining = Mathf.Max(1, berserkerTurns),
                    Stacks         = 1,
                    StatMod        = new StatModifier
                    {
                        ataqueBonus      = berserkerBonus,
                        perforacionBonus = berserkerBonus,
                        critChanceBonus  = 1f,       // ≥100% → crítico garantizado
                        critDañoBonus    = berserkerBonus
                    },
                    GroupId        = BerserkerGroup
                });
            }

            // ── Cláusula 1: escalado al inicio del turno del héroe ─────────────────
            ctx.OnPlayerTurnStarted(_ =>
            {
                turns++;
                RecomputeFenixAura();
            });

            // ── Cláusula 2: renacer cuando los PS caen bajo el umbral ──────────────
            ctx.OnActionResolved(result =>
            {
                bool canRevive = !(oncePerCombat && phoenixUsed);
                if (canRevive
                    && result.Target == ctx.Owner
                    && ctx.Owner.IsAlive
                    && ctx.Owner.Model.CurrentHP < ctx.Owner.Model.MaxHP * lowHpThreshold)
                {
                    TriggerRebirth();
                }
            });
        }
    }
}
