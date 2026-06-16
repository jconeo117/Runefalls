using System;
using System.Collections.Generic;
using UnityEngine;
using Runefall.Characters;

namespace Runefall.Combat
{
    /// <summary>
    /// Pasiva del jefe (Tharok). Combina rasgos SIEMPRE activos con rasgos por fase.
    ///
    /// Siempre activos (todas las fases):
    ///   A. Escalado total — al inicio de cada turno, TODAS las estadísticas del jefe suben
    ///      +allStatsPerTurn por cada turno transcurrido (aura recalculada cada turno).
    ///   B. Limpieza — al comienzo del turno del jefe (fase enemiga) se quita TODO efecto negativo
    ///      (Tag = Disadvantage) que tenga encima.
    ///   C. +2 acciones — el jefe actúa 3 veces por turno. Esto es estructural
    ///      (BossEnemyData.actionsPerTurn → ICombatActor.ActionsPerTurn → TurnManager), no se gestiona aquí.
    ///
    /// Por fase (las stats/efectos se limpian en cada transición; se reaplican según la fase nueva):
    ///   Fase 1: reduce el daño recibido en phase1DamageReduction (efecto IncomingDamageBonus negativo).
    ///   Fase 2: +phase2AttackPerCrit de ataque por cada crítico que asesta el jefe (apila);
    ///           y reduce la resistencia crítica de TODOS los enemigos en
    ///           phase2EnemyCritResistPerTurn por cada turno transcurrido en la fase (acumulativo).
    ///   Fase 3: si los PS llegan a 0, revive UNA vez: se cura phase3ReviveHealPercent de la vida máxima
    ///           y gana +phase3ReviveStatBonus a TODAS las estadísticas (permanente).
    ///
    /// NOTA: BossPhasesPassive es monolítica (un único jefe por combate). Todo el estado de runtime se
    /// resetea en Activate; el SO no se comparte entre varios actores como las pasivas componibles.
    /// </summary>
    [CreateAssetMenu(menuName = "Runefall/Passives/BossPhasesPassive")]
    public class BossPhasesPassive : PassiveDefinition
    {
        [Header("Siempre activo — escalado total por turno")]
        [Tooltip("Bonus a TODAS las stats por cada turno transcurrido. 0.01 = +1%/turno.")]
        public float allStatsPerTurn = 0.01f;

        [Header("Fase 1 — reducción de daño recibido")]
        [Range(0f, 1f)]
        [Tooltip("Fracción de daño recibido que se anula. 0.20 = -20% daño recibido.")]
        public float phase1DamageReduction = 0.20f;

        [Header("Fase 2 — furia por crítico y exposición")]
        [Tooltip("Ataque ganado por cada crítico del jefe (apila). 0.01 = +1% por crítico.")]
        public float phase2AttackPerCrit = 0.01f;
        [Tooltip("Resistencia crítica que pierden los enemigos por cada turno de la fase 2. 0.02 = -2%/turno.")]
        public float phase2EnemyCritResistPerTurn = 0.02f;

        [Header("Fase 3 — renacer")]
        [Range(0f, 1f)]
        [Tooltip("Vida máxima recuperada al revivir. 0.25 = 25%.")]
        public float phase3ReviveHealPercent = 0.25f;
        [Range(0f, 2f)]
        [Tooltip("Bonus a TODAS las stats al revivir (permanente). 0.15 = +15%.")]
        public float phase3ReviveStatBonus = 0.15f;

        // ── group ids (efectos aplicados vía ActorEffects) ─────────────────────────
        private const string Phase1DRGroup     = "boss_p1_damage_reduction";
        private const string Phase2CritResGroup = "boss_p2_enemy_critres";

        // ── runtime state (un solo jefe; reseteado en Activate) ────────────────────
        private ICombatActor  _owner;
        private TurnManager   _tm;
        private CombatContext _ctx;

        private int _turns;          // turnos totales transcurridos (aura A)
        private int _phase2Turns;    // turnos transcurridos dentro de la fase 2
        private int _phase2Crits;    // críticos del jefe en fase 2
        private bool _revived;       // fase 3: el renacer ya ocurrió

        // stat modifiers directos sobre el Model del jefe (sobreviven a ResetStats; los gestionamos a mano)
        private StatModifier _auraMod;
        private StatModifier _p2CritAtkMod;
        private StatModifier _p3ReviveMod;

        private Action<int>                  _onTurnStarted;
        private Action                       _onEnemyTurnStarted;
        private Action<CombatActionResult>   _onActionResolved;
        private Action<int>                  _onPhaseTransitioned;

        public override void Activate(ICombatActor owner, TurnManager tm, CombatContext ctx)
        {
            _owner = owner;
            _tm    = tm;
            _ctx   = ctx;

            _turns = 0;
            _phase2Turns = 0;
            _phase2Crits = 0;
            _revived = false;
            _auraMod = null;
            _p2CritAtkMod = null;
            _p3ReviveMod = null;

            _onTurnStarted      = OnPlayerTurnStarted;
            _onEnemyTurnStarted = OnEnemyTurnStarted;
            _onActionResolved   = OnActionResolved;

            _tm.OnPlayerTurnStarted += _onTurnStarted;
            _tm.OnEnemyTurnStarted  += _onEnemyTurnStarted;
            _tm.OnActionResolved    += _onActionResolved;

            if (_owner is IMultiPhaseActor boss)
            {
                _onPhaseTransitioned = OnPhaseTransitioned;
                boss.OnPhaseTransitionCompleted += _onPhaseTransitioned;
            }

            ApplyPhasePassive(CurrentPhase());
        }

        public override void Deactivate(ICombatActor owner, TurnManager tm)
        {
            if (_onTurnStarted      != null) tm.OnPlayerTurnStarted -= _onTurnStarted;
            if (_onEnemyTurnStarted != null) tm.OnEnemyTurnStarted  -= _onEnemyTurnStarted;
            if (_onActionResolved   != null) tm.OnActionResolved    -= _onActionResolved;

            if (_owner is IMultiPhaseActor boss && _onPhaseTransitioned != null)
                boss.OnPhaseTransitionCompleted -= _onPhaseTransitioned;

            RemovePhaseModifiers();
            RemoveStat(ref _auraMod);

            _owner = null;
            _tm = null;
            _ctx = null;
            _onTurnStarted = null;
            _onEnemyTurnStarted = null;
            _onActionResolved = null;
            _onPhaseTransitioned = null;
        }

        // ── ciclo de turno ─────────────────────────────────────────────────────────

        private void OnPlayerTurnStarted(int round)
        {
            if (_owner == null || !_owner.IsAlive) return;

            // (A) Escalado total: +allStatsPerTurn por turno transcurrido. Recalculado cada turno.
            _turns++;
            RemoveStat(ref _auraMod);
            float aura = allStatsPerTurn * _turns;
            if (aura > 0f)
            {
                _auraMod = AllStatsMod(aura);
                _owner.Model.AddStatModifier(_auraMod);
            }

            // (Fase 2) Exposición: -resistencia crítica a todos los enemigos, acumulativo por turno.
            if (CurrentPhase() == 2)
            {
                _phase2Turns++;
                float critResDown = phase2EnemyCritResistPerTurn * _phase2Turns;
                ApplyEnemyCritResistDebuff(critResDown);
            }
        }

        // Comienzo del turno del jefe (fase enemiga): se quita todo efecto negativo encima del jefe.
        private void OnEnemyTurnStarted()
        {
            if (_owner == null || !_owner.IsAlive) return;
            _owner.Effects.RemoveByTag(EffectTag.Disadvantage);
        }

        private void OnActionResolved(CombatActionResult result)
        {
            if (_owner == null) return;
            int phase = CurrentPhase();

            // (Fase 2) +ataque por cada crítico que asesta el jefe.
            if (phase == 2 && ReferenceEquals(result.Caster, _owner) && result.IsCrit)
            {
                _phase2Crits++;
                RemoveStat(ref _p2CritAtkMod);
                _p2CritAtkMod = new StatModifier { ataqueBonus = phase2AttackPerCrit * _phase2Crits };
                _owner.Model.AddStatModifier(_p2CritAtkMod);
            }

            // (Fase 3) Renacer: si el jefe cae a 0 PS, revive una vez.
            if (phase == 3 && !_revived
                && ReferenceEquals(result.Target, _owner)
                && !_owner.Model.IsAlive)
            {
                _revived = true;
                _owner.Model.SetHPDirectly(_owner.Model.MaxHP * phase3ReviveHealPercent);
                RemoveStat(ref _p3ReviveMod);
                _p3ReviveMod = AllStatsMod(phase3ReviveStatBonus);
                _owner.Model.AddStatModifier(_p3ReviveMod);
            }
        }

        // ── transición de fase ───────────────────────────────────────────────────
        // BossAgent hace ResetStats + Effects.ClearAll en la transición: los efectos de fase
        // (DR fase 1, debuffs en jugadores) se borran. Reaplicamos lo de la fase nueva.
        private void OnPhaseTransitioned(int nextPhase)
        {
            RemovePhaseModifiers();
            _phase2Turns = 0;
            _phase2Crits = 0;

            // El aura total la borra Effects.ClearAll/ResetStats indirectamente? No: es un stat mod
            // directo, sobrevive. La reaplicamos por seguridad con el valor acumulado actual.
            RemoveStat(ref _auraMod);
            float aura = allStatsPerTurn * _turns;
            if (aura > 0f)
            {
                _auraMod = AllStatsMod(aura);
                _owner.Model.AddStatModifier(_auraMod);
            }

            ApplyPhasePassive(nextPhase);
        }

        private void ApplyPhasePassive(int phase)
        {
            if (_owner == null) return;

            // Fase 1: reducción de daño recibido (efecto Neutral → no lo borra la limpieza del propio jefe).
            if (phase == 1)
            {
                _owner.Effects.RemoveByGroup(Phase1DRGroup);
                _owner.Effects.Apply(new ActiveEffect
                {
                    Source              = null,
                    Tag                 = EffectTag.Neutral,
                    TurnsRemaining      = -1,
                    Stacks              = 1,
                    IncomingDamageBonus = -phase1DamageReduction,   // 1 + (-0.2) = x0.8 daño recibido
                    GroupId             = Phase1DRGroup
                });
            }
            // Fase 2 y 3 reaccionan a eventos (crítico, turno, muerte); no hay setup inmediato.
        }

        private void RemovePhaseModifiers()
        {
            if (_owner == null) return;

            // Fase 1
            _owner.Effects.RemoveByGroup(Phase1DRGroup);
            // Fase 2 — buff de ataque del jefe + debuff de crit-res en jugadores
            RemoveStat(ref _p2CritAtkMod);
            RemoveEnemyCritResistDebuff();
            // Fase 3 — el renacer es permanente; NO lo quitamos aquí (no hay fase posterior).
            // _p3ReviveMod sólo se limpia en Deactivate.
        }

        // ── helpers ────────────────────────────────────────────────────────────────

        private int CurrentPhase() => _owner is IMultiPhaseActor boss ? boss.CurrentPhase : 1;

        private void RemoveStat(ref StatModifier mod)
        {
            if (mod != null && _owner?.Model != null) _owner.Model.RemoveStatModifier(mod);
            mod = null;
        }

        // Reaplica el debuff de resistencia crítica a todos los enemigos (jugadores) con la magnitud dada.
        private void ApplyEnemyCritResistDebuff(float amount)
        {
            if (_ctx == null) return;
            var players = _ctx.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsAlive) continue;
                p.Effects.RemoveByGroup(Phase2CritResGroup);
                p.Effects.Apply(new ActiveEffect
                {
                    Source         = null,
                    Tag            = EffectTag.Disadvantage,
                    TurnsRemaining = -1,
                    Stacks         = 1,
                    StatMod        = new StatModifier { resistenciaCritBonus = -amount },
                    GroupId        = Phase2CritResGroup
                });
            }
        }

        private void RemoveEnemyCritResistDebuff()
        {
            if (_ctx == null) return;
            var players = _ctx.Players;
            for (int i = 0; i < players.Count; i++)
                players[i]?.Effects.RemoveByGroup(Phase2CritResGroup);
        }

        // StatModifier con el mismo bonus en TODAS las estadísticas (principales multiplicativas, substats aditivas).
        // ps no tiene campo en StatModifier → la vida no escala por aquí.
        private static StatModifier AllStatsMod(float v) => new StatModifier
        {
            ataqueBonus           = v,
            perforacionBonus      = v,
            critChanceBonus       = v,
            critDañoBonus         = v,
            defensaBonus          = v,
            resistenciaBonus      = v,
            defensaCritBonus      = v,
            resistenciaCritBonus  = v,
            roboDeVidaBonus       = v,
            tasaRegenBonus        = v,
            tasaRecuperacionBonus = v
        };
    }
}
