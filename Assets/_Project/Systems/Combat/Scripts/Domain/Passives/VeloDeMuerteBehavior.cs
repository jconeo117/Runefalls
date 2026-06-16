using System.Collections.Generic;
using UnityEngine;
using Runefall.Characters;

namespace Runefall.Combat
{
    /// <summary>
    /// Pasiva de Vorn "Velo de la Muerte": un DPS que expone al enemigo y castiga al marcado.
    ///
    /// Cláusulas (al inicio de cada turno del héroe):
    ///   1. Oscuridad Rota — a TODOS los enemigos (1 turno): reduce stats defensivos
    ///      (defensa, resistencia, def. crítica, res. crítica) en defenseReduction.
    ///   2. Marca de la Muerte — a UN enemigo aleatorio (1 turno): +markDamageTaken al daño recibido.
    ///   3. Recompensa — cuando Vorn da un crítico a un objetivo con Marca de la Muerte: gana
    ///      +critChancePerProc prob. crítico y +critDamagePerProc daño crítico (permanente, apila) y
    ///      recupera lifestealOnProc del daño hecho.
    ///
    /// Estado per-activación vive en locals capturados por las closures (Bind), nunca en campos del SO.
    /// </summary>
    [CreateAssetMenu(menuName = "Runefall/Passives/Behaviors/Velo de la Muerte")]
    public class VeloDeMuerteBehavior : PassiveBehavior
    {
        [Header("Oscuridad Rota — debuff defensivo a todos los enemigos (1 turno, recada turno)")]
        [Tooltip("Reducción a stats defensivos del enemigo. 0.20 = -20% (defensa multiplicativa; substats aditivos).")]
        public float defenseReduction = 0.20f;

        [Header("Marca de la Muerte — a un enemigo aleatorio (1 turno)")]
        [Tooltip("Daño recibido extra del marcado. 0.40 = +40%.")]
        public float markDamageTaken = 0.40f;

        [Header("Recompensa: crítico sobre el marcado")]
        [Tooltip("Prob. crítico permanente que gana Vorn por cada crítico sobre un marcado. 0.05 = +5%.")]
        public float critChancePerProc = 0.05f;
        [Tooltip("Daño crítico permanente por cada crítico sobre un marcado. 0.08 = +8%.")]
        public float critDamagePerProc = 0.08f;
        [Tooltip("Fracción del daño curada al critear a un marcado. 0.15 = 15%.")]
        public float lifestealOnProc = 0.15f;

        private const string OscuridadGroup = "oscuridad_rota";
        private const string MarcaGroup     = "marca_de_la_muerte";
        private const string CritBuildGroup = "velo_crit_buildup";

        public override void Bind(PassiveContext ctx)
        {
            // ── per-activation state (closures, no SO fields) ──────────────────────
            var          rng       = new System.Random();
            ICombatActor marked    = null;
            int          critProcs = 0;

            // Buildup permanente de crítico de Vorn (apila por cada crítico sobre marcado).
            void RecomputeCritBuildup()
            {
                ctx.Owner.Effects.RemoveByGroup(CritBuildGroup);
                if (critProcs <= 0) return;
                ctx.Owner.Effects.Apply(new ActiveEffect
                {
                    Source         = null,
                    Tag            = EffectTag.Neutral,
                    TurnsRemaining = -1,
                    Stacks         = 1,
                    StatMod        = new StatModifier
                    {
                        critChanceBonus = critChancePerProc * critProcs,
                        critDañoBonus   = critDamagePerProc * critProcs
                    },
                    GroupId        = CritBuildGroup
                });
            }

            // ── Cláusula 1+2: al inicio del turno del héroe ────────────────────────
            ctx.OnPlayerTurnStarted(_ =>
            {
                var enemies = ctx.Ctx.Enemies;

                // (1) Oscuridad Rota a todos los enemigos.
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (e == null || !e.IsAlive) continue;
                    e.Effects.RemoveByGroup(OscuridadGroup);
                    e.Effects.Apply(new ActiveEffect
                    {
                        Source         = null,
                        Tag            = EffectTag.Disadvantage,
                        TurnsRemaining = 1,
                        Stacks         = 1,
                        StatMod        = new StatModifier
                        {
                            defensaBonus         = -defenseReduction,   // multiplicativo (×0.8)
                            resistenciaBonus     = -defenseReduction,   // aditivo
                            defensaCritBonus     = -defenseReduction,
                            resistenciaCritBonus = -defenseReduction
                        },
                        GroupId        = OscuridadGroup
                    });
                }

                // (2) Marca de la Muerte a un enemigo aleatorio.
                marked = PickRandomAlive(enemies, rng);
                if (marked != null)
                {
                    marked.Effects.RemoveByGroup(MarcaGroup);
                    marked.Effects.Apply(new ActiveEffect
                    {
                        Source              = null,
                        Tag                 = EffectTag.Disadvantage,
                        TurnsRemaining      = 1,
                        Stacks              = 1,
                        IncomingDamageBonus = markDamageTaken,
                        GroupId             = MarcaGroup
                    });
                }
            });

            // ── Cláusula 3: crítico de Vorn sobre el marcado ───────────────────────
            ctx.OnActionResolved(result =>
            {
                if (result.Caster == ctx.Owner
                    && result.IsCrit
                    && result.Target != null
                    && ReferenceEquals(result.Target, marked))
                {
                    critProcs++;
                    RecomputeCritBuildup();
                    if (lifestealOnProc > 0f && result.DamageDealt > 0f)
                        ctx.Owner.Model.Heal(result.DamageDealt * lifestealOnProc);
                }
            });
        }

        // Reservoir sampling: elige uniformemente un actor vivo sin alojar listas.
        private static ICombatActor PickRandomAlive(IReadOnlyList<ICombatActor> list, System.Random rng)
        {
            int count = 0;
            ICombatActor chosen = null;
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a == null || !a.IsAlive) continue;
                count++;
                if (rng.Next(count) == 0) chosen = a;
            }
            return chosen;
        }
    }
}
