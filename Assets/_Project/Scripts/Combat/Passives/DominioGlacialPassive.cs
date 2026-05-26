using System;
using UnityEngine;

namespace Runefall.Combat
{
    /// <summary>
    /// Dominio Glacial — pasiva del personaje de Hielo.
    ///
    /// Cláusulas:
    ///   1. Inicio de turno del jugador → aplica Hipotermia a todos los enemigos vivos (2 turnos).
    ///   2. Atacar enemigo con Hipotermia → +30% daño recibido (automático vía IncomingDamageBonus).
    ///   3. Golpe crítico vs enemigo con Hipotermia → cura al héroe un % del daño infligido.
    ///   4. Recibir daño > umbral% del HP máximo en un solo golpe (una vez) →
    ///        · Congela a todos los enemigos vivos (Congelado, permanente).
    ///        · Aplica Dominio del Hielo al héroe (+15% ATK, permanente).
    ///   5. Con Dominio del Hielo activo, atacar a un enemigo Congelado → daño doble
    ///        (aplica daño extra igual al daño original directamente sobre el objetivo).
    ///
    /// Nota: cláusula 2 no requiere código en esta clase. HipotermiaEffectDef.IncomingDamageBonus
    /// alimenta ActorEffects.DamageReceivedMultiplier(), que DamageEffectDef aplica automáticamente.
    /// Solo funciona con skills que usan el pipeline effects[] (no legacy damageMultiplier).
    /// </summary>
    [CreateAssetMenu(menuName = "Runefall/Passives/DominioGlacial")]
    public class DominioGlacialPassive : PassiveDefinition
    {
        [Header("Effects — assign in Inspector")]
        public HipotermiaEffectDef hipotermia;
        [Tooltip("MarkerEffectDef con behavior=SkipTurn y durationByRank=[-1,-1,-1].")]
        public MarkerEffectDef     congelado;
        [Tooltip("StatModEffectDef con stat=Ataque, value=+0.15, durationByRank=[-1,-1,-1], effectTarget=Caster.")]
        public EffectDefinition    dominioDelHielo;

        [Header("Tuning")]
        [Range(0f, 1f)]
        [Tooltip("Fracción del daño infligido curada al héroe en crítico vs Hipotermia.")]
        public float critHealPercent  = 0.50f;
        [Range(0f, 1f)]
        [Tooltip("Fracción del HP máximo del héroe que dispara el trigger de congelación masiva.")]
        public float triggerThreshold = 0.60f;

        // ── runtime state (reset on every Activate) ───────────────────────────────
        private ICombatActor _owner;
        private CombatContext _ctx;
        private bool _triggered;    // damage trigger fires only once per combat
        private bool _dominioActive;// true after DominioDelHielo is applied

        private Action<int>                _onTurnStarted;
        private Action<CombatActionResult> _onActionResolved;

        // ── PassiveDefinition ─────────────────────────────────────────────────────

        public override void Activate(ICombatActor owner, TurnManager tm, CombatContext ctx)
        {
            _owner         = owner;
            _ctx           = ctx;
            _triggered     = false;
            _dominioActive = false;

            _onTurnStarted    = _ => OnPlayerTurnStarted();
            _onActionResolved = OnActionResolved;

            tm.OnPlayerTurnStarted += _onTurnStarted;
            tm.OnActionResolved    += _onActionResolved;
        }

        public override void Deactivate(ICombatActor owner, TurnManager tm)
        {
            if (_onTurnStarted    != null) tm.OnPlayerTurnStarted -= _onTurnStarted;
            if (_onActionResolved != null) tm.OnActionResolved    -= _onActionResolved;

            _owner            = null;
            _ctx              = null;
            _triggered        = false;
            _dominioActive    = false;
            _onTurnStarted    = null;
            _onActionResolved = null;
        }

        // ── handlers ─────────────────────────────────────────────────────────────

        private void OnPlayerTurnStarted()
        {
            if (hipotermia == null || _ctx == null) return;

            var execCtx = new EffectExecutionContext { Caster = _owner, Rank = 1 };
            for (int i = 0; i < _ctx.Enemies.Count; i++)
            {
                var enemy = _ctx.Enemies[i];
                if (!enemy.IsAlive) continue;
                execCtx.Target = enemy;
                hipotermia.Execute(execCtx);
            }
        }

        private void OnActionResolved(CombatActionResult result)
        {
            CritHealCheck(result);
            DamageTriggerCheck(result);
            DominioFrozenBonusCheck(result);
        }

        // Cláusula 3: crítico vs enemigo con Hipotermia → cura héroe
        private void CritHealCheck(CombatActionResult result)
        {
            if (result.Caster != _owner) return;
            if (!result.IsCrit) return;
            if (result.DamageDealt <= 0f) return;
            if (result.Target == null) return;
            if (!result.Target.Effects.HasEffect<HipotermiaEffectDef>()) return;

            _owner.Model.Heal(result.DamageDealt * critHealPercent);
        }

        // Cláusula 4: recibir daño > umbral → congelar todos + Dominio del Hielo
        private void DamageTriggerCheck(CombatActionResult result)
        {
            if (_triggered) return;
            if (result.Target != _owner) return;
            if (result.DamageDealt <= _owner.Model.MaxHP * triggerThreshold) return;

            _triggered = true;
            ApplyMassFreezeAndDominio();
        }

        // Cláusula 5: héroe con Dominio del Hielo ataca a Congelado → daño extra igual al original
        private void DominioFrozenBonusCheck(CombatActionResult result)
        {
            if (!_dominioActive) return;
            if (result.Caster != _owner) return;
            if (result.DamageDealt <= 0f) return;
            if (result.Target == null || !result.Target.IsAlive) return;
            if (!result.Target.Effects.HasBehavior(EffectBehavior.SkipTurn)) return;

            result.Target.Model.TakeDamage(result.DamageDealt);
        }

        // ── helpers ───────────────────────────────────────────────────────────────

        private void ApplyMassFreezeAndDominio()
        {
            if (_ctx == null) return;

            // Congelar todos los enemigos vivos
            if (congelado != null)
            {
                var freezeCtx = new EffectExecutionContext { Caster = _owner, Rank = 1 };
                for (int i = 0; i < _ctx.Enemies.Count; i++)
                {
                    var enemy = _ctx.Enemies[i];
                    if (!enemy.IsAlive) continue;
                    freezeCtx.Target = enemy;
                    congelado.Execute(freezeCtx);
                }
            }

            // Aplicar Dominio del Hielo al héroe
            if (dominioDelHielo != null)
            {
                dominioDelHielo.Execute(new EffectExecutionContext
                {
                    Caster = _owner,
                    Target = _owner,
                    Rank   = 1
                });
                _dominioActive = true;
            }
        }
    }
}
