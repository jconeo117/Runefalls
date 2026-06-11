using System;
using UnityEngine;
using UnityEngine.Playables;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Enemies
{
    public class BossAgent : IMultiPhaseActor, IEnemyTurnHandler
    {
        private readonly BossEnemyData _bossData;
        private EnemyData _currentPhaseData;
        
        public int CurrentPhase { get; private set; } = 1;
        public BossEnemyData BossData => _bossData;
        public EnemyData CurrentPhaseData => _currentPhaseData;

        public string         Name        => _currentPhaseData.enemyName;
        
        // The boss is alive if they are in phase 1 or 2, OR if they are in phase 3 and their HP > 0.
        public bool           IsAlive     => CurrentPhase < 3 || Model.IsAlive;
        public CharacterModel Model       { get; }
        public ElementType    Element     => _currentPhaseData.element;
        public ActorEffects   Effects     { get; }
        public float          CombatClass => _currentPhaseData.combatClass;

        public event Action<int> OnPhaseTransitionStarted;
        public event Action<int> OnPhaseTransitionCompleted;

        private bool _isTransitioning = false;
        public bool IsTransitioning => _isTransitioning;

        public BossAgent(BossEnemyData bossData)
        {
            if (bossData == null) throw new ArgumentNullException(nameof(bossData));
            _bossData = bossData;
            
            // Start with Phase 1 data
            _currentPhaseData = _bossData.phase1Data != null ? _bossData.phase1Data : _bossData;
            
            Model   = new CharacterModel(_currentPhaseData.enemyName, _currentPhaseData.stats, _currentPhaseData.maxMP);
            Effects = new ActorEffects(this);
        }

        public PendingAction TakeTurn(CombatContext context, ICombatActor target)
        {
            if (!IsAlive || target == null || _isTransitioning)
                return default;

            // Use the skills assigned to the current phase
            return _currentPhaseData.behaviorTree switch
            {
                BehaviorTreeType.BossElite => BuildAttack(target),
                _                          => BuildAttack(target)
            };
        }

        private PendingAction BuildAttack(ICombatActor target)
        {
            var skill = _currentPhaseData.skill1;
            
            if (CurrentPhase == 2 && _currentPhaseData.skill2 != null)
                skill = _currentPhaseData.skill2;
            else if (CurrentPhase == 3 && _currentPhaseData.ultimate != null && UnityEngine.Random.value < 0.5f)
            {
                return new PendingAction(
                    caster:     this,
                    target:     target,
                    skill:      null,
                    ultimate:   _currentPhaseData.ultimate,
                    rank:       1,
                    targetType: _currentPhaseData.ultimate.targetType,
                    isUltimate: true);
            }

            return new PendingAction(
                caster:     this,
                target:     target,
                skill:      skill,
                ultimate:   null,
                rank:       1,
                targetType: skill != null ? skill.targetType : TargetType.SingleEnemy,
                isUltimate: false);
        }

        public void CheckPhaseTransition(Action onTransitionCinematicFinished)
        {
            if (_isTransitioning) return;
            
            if (!Model.IsAlive && CurrentPhase < 3)
            {
                StartTransition(onTransitionCinematicFinished);
            }
        }

        private void StartTransition(Action onTransitionCinematicFinished)
        {
            _isTransitioning = true;
            CurrentPhase++;
            
            // Notify presentation layer to start transition
            OnPhaseTransitionStarted?.Invoke(CurrentPhase);
            
            // Update current phase data
            if (CurrentPhase == 2)
                _currentPhaseData = _bossData.phase2Data != null ? _bossData.phase2Data : _bossData;
            else if (CurrentPhase == 3)
                _currentPhaseData = _bossData.phase3Data != null ? _bossData.phase3Data : _bossData;

            // This will be called by presentation when cinematic completes
            OnTransitionCinematicCompleted = () =>
            {
                // Update stats and reset HP
                Model.ResetStats(_currentPhaseData.stats);
                
                // Clear active status effects
                Effects.ClearAll();

                _isTransitioning = false;
                OnPhaseTransitionCompleted?.Invoke(CurrentPhase);
                onTransitionCinematicFinished?.Invoke();
            };
        }

        public Action OnTransitionCinematicCompleted { get; private set; }
        
        public void CompleteTransitionCinematic()
        {
            OnTransitionCinematicCompleted?.Invoke();
            OnTransitionCinematicCompleted = null;
        }

        public PlayableAsset GetTransitionTimeline(int nextPhase)
        {
            if (_bossData == null) return null;
            return (nextPhase == 2) 
                ? _bossData.phase1To2Timeline 
                : _bossData.phase2To3Timeline;
        }

        public GameObject GetPhasePrefab(int nextPhase)
        {
            if (nextPhase == 2 && _bossData.phase2Data != null)
                return _bossData.phase2Data.prefab;
            if (nextPhase == 3 && _bossData.phase3Data != null)
                return _bossData.phase3Data.prefab;
            return _bossData.prefab;
        }
    }
}
