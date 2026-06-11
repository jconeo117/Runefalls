using System;
using UnityEngine;
using Runefall.Characters;


namespace Runefall.Combat
{
    [CreateAssetMenu(menuName = "Runefall/Passives/BossPhasesPassive")]
    public class BossPhasesPassive : PassiveDefinition
    {
        [Header("Tuning — Phase 1")]
        [Range(0f, 1f)] public float phase1DamageReduction = 0.3f;
        [Range(0f, 0.2f)] public float phase1AttackBuffPerHit = 0.05f;
        public int phase1MaxAttackStacks = 5;

        [Header("Tuning — Phase 2")]
        [Range(0f, 1f)] public float phase2AttackBonus = 0.2f;
        [Range(0f, 0.2f)] public float phase2BurnPercent = 0.05f;

        [Header("Tuning — Phase 3")]
        [Range(0f, 1f)] public float phase3LifeSteal = 0.3f;
        [Range(0f, 1f)] public float phase3CritChanceBonus = 0.3f;
        [Range(0f, 1f)] public float phase3LowHPCritDamageBonus = 0.5f;

        // Runtime state (reset on Activate)
        private ICombatActor _owner;
        private TurnManager _tm;
        private CombatContext _ctx;

        // Modifiers applied
        private StatModifier _p1BaseMod;
        private StatModifier _p1StackMod;
        private int _p1AttackStacks;

        private StatModifier _p2BaseMod;

        private StatModifier _p3BaseMod;
        private StatModifier _p3LowHPMod;
        private bool _p3LowHPActive;

        private Action<int> _onTurnStarted;
        private Action<CombatActionResult> _onActionResolved;
        private Action<int> _onPhaseTransitioned;

        public override void Activate(ICombatActor owner, TurnManager tm, CombatContext ctx)
        {
            _owner = owner;
            _tm = tm;
            _ctx = ctx;

            _p1AttackStacks = 0;
            _p3LowHPActive = false;

            _onTurnStarted = OnTurnStarted;
            _onActionResolved = OnActionResolved;

            _tm.OnPlayerTurnStarted += _onTurnStarted;
            _tm.OnActionResolved += _onActionResolved;

            if (_owner is IMultiPhaseActor boss)
            {
                _onPhaseTransitioned = OnPhaseTransitioned;
                boss.OnPhaseTransitionCompleted += _onPhaseTransitioned;
            }

            ApplyInitialPhasePassive();
        }

        public override void Deactivate(ICombatActor owner, TurnManager tm)
        {
            if (_onTurnStarted != null) tm.OnPlayerTurnStarted -= _onTurnStarted;
            if (_onActionResolved != null) tm.OnActionResolved -= _onActionResolved;

            if (_owner is IMultiPhaseActor boss && _onPhaseTransitioned != null)
            {
                boss.OnPhaseTransitionCompleted -= _onPhaseTransitioned;
            }

            RemoveAllModifiers();

            _owner = null;
            _tm = null;
            _ctx = null;
            _onTurnStarted = null;
            _onActionResolved = null;
            _onPhaseTransitioned = null;
        }

        private void ApplyInitialPhasePassive()
        {
            if (_owner == null) return;

            int phase = 1;
            if (_owner is IMultiPhaseActor boss)
            {
                phase = boss.CurrentPhase;
            }

            ApplyPassiveForPhase(phase);
        }

        private void OnPhaseTransitioned(int nextPhase)
        {
            // Remove previous phase modifiers
            RemoveAllModifiers();
            
            // Reset state
            _p1AttackStacks = 0;
            _p3LowHPActive = false;

            // Apply new phase modifiers
            ApplyPassiveForPhase(nextPhase);
        }

        private void ApplyPassiveForPhase(int phase)
        {
            if (_owner == null) return;

            if (phase == 1)
            {
                _p1BaseMod = new StatModifier { resistenciaBonus = phase1DamageReduction };
                _owner.Model.AddStatModifier(_p1BaseMod);
            }
            else if (phase == 2)
            {
                _p2BaseMod = new StatModifier { ataqueBonus = phase2AttackBonus };
                _owner.Model.AddStatModifier(_p2BaseMod);
            }
            else if (phase == 3)
            {
                _p3BaseMod = new StatModifier 
                { 
                    roboDeVidaBonus = phase3LifeSteal, 
                    critChanceBonus = phase3CritChanceBonus 
                };
                _owner.Model.AddStatModifier(_p3BaseMod);
            }
        }

        private void RemoveAllModifiers()
        {
            if (_owner == null || _owner.Model == null) return;

            if (_p1BaseMod != null) { _owner.Model.RemoveStatModifier(_p1BaseMod); _p1BaseMod = null; }
            if (_p1StackMod != null) { _owner.Model.RemoveStatModifier(_p1StackMod); _p1StackMod = null; }
            if (_p2BaseMod != null) { _owner.Model.RemoveStatModifier(_p2BaseMod); _p2BaseMod = null; }
            if (_p3BaseMod != null) { _owner.Model.RemoveStatModifier(_p3BaseMod); _p3BaseMod = null; }
            if (_p3LowHPMod != null) { _owner.Model.RemoveStatModifier(_p3LowHPMod); _p3LowHPMod = null; }
        }

        private void OnTurnStarted(int round)
        {
            if (_owner == null || !_owner.IsAlive) return;

            int phase = 1;
            if (_owner is IMultiPhaseActor boss)
            {
                phase = boss.CurrentPhase;
            }

            // Phase 2 logic: Deal fire burn damage to all players at the start of Player turn
            if (phase == 2 && _ctx != null)
            {
                for (int i = 0; i < _ctx.Players.Count; i++)
                {
                    var player = _ctx.Players[i];
                    if (player != null && player.IsAlive)
                    {
                        float burnDamage = player.Model.MaxHP * phase2BurnPercent;
                        player.Model.TakeDamage(burnDamage);
                    }
                }
            }
        }

        private void OnActionResolved(CombatActionResult result)
        {
            if (_owner == null || !_owner.IsAlive) return;

            int phase = 1;
            if (_owner is IMultiPhaseActor boss)
            {
                phase = boss.CurrentPhase;
            }

            // Phase 1 logic: Gain stacking attack buff when hit
            if (phase == 1 && result.Target == _owner && result.DamageDealt > 0f)
            {
                if (_p1AttackStacks < phase1MaxAttackStacks)
                {
                    _p1AttackStacks++;
                    if (_p1StackMod != null)
                    {
                        _owner.Model.RemoveStatModifier(_p1StackMod);
                    }
                    _p1StackMod = new StatModifier { ataqueBonus = phase1AttackBuffPerHit * _p1AttackStacks };
                    _owner.Model.AddStatModifier(_p1StackMod);
                }
            }

            // Phase 3 logic: Check low HP crit damage bonus trigger
            if (phase == 3)
            {
                float hpPercent = _owner.Model.CurrentHP / _owner.Model.MaxHP;
                if (hpPercent < 0.3f && !_p3LowHPActive)
                {
                    _p3LowHPActive = true;
                    _p3LowHPMod = new StatModifier { critDañoBonus = phase3LowHPCritDamageBonus };
                    _owner.Model.AddStatModifier(_p3LowHPMod);
                }
                else if (hpPercent >= 0.3f && _p3LowHPActive)
                {
                    _p3LowHPActive = false;
                    if (_p3LowHPMod != null)
                    {
                        _owner.Model.RemoveStatModifier(_p3LowHPMod);
                        _p3LowHPMod = null;
                    }
                }
            }
        }
    }
}
