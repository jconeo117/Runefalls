using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Runefall.Combat;
using Runefall.Enemies;

namespace Runefall.Presentation.Combat
{
    public class CombatQueueManager
    {
        private readonly MonoBehaviour _coroutineRunner;
        private readonly Queue<PendingAction> _animQueue = new();
        private readonly Func<PendingAction, IEnumerator> _playActionGroup;
        private readonly Func<IEnumerator> _runCombatEndDrama;
        private readonly ICombatAnimationNotifier _notifier;
        private readonly Func<bool> _isCombatOver;
        private readonly float _postPlayerTurnDelay;
        private readonly float _betweenSkillsDelay;
        private readonly BossTransitionPresenter _bossTransitionPresenter;
        private readonly Func<CombatContext> _getContext;
        private readonly Action _onBossPhaseTransitioned;

        private bool _isDraining;

        public bool IsDraining => _isDraining;
        public Queue<PendingAction> Queue => _animQueue;

        public CombatQueueManager(
            MonoBehaviour coroutineRunner,
            Func<PendingAction, IEnumerator> playActionGroup,
            Func<IEnumerator> runCombatEndDrama,
            ICombatAnimationNotifier notifier,
            Func<bool> isCombatOver,
            float postPlayerTurnDelay,
            float betweenSkillsDelay,
            BossTransitionPresenter bossTransitionPresenter,
            Func<CombatContext> getContext,
            Action onBossPhaseTransitioned = null)
        {
            _coroutineRunner = coroutineRunner;
            _playActionGroup = playActionGroup;
            _runCombatEndDrama = runCombatEndDrama;
            _notifier = notifier;
            _isCombatOver = isCombatOver;
            _postPlayerTurnDelay = postPlayerTurnDelay;
            _betweenSkillsDelay = betweenSkillsDelay;
            _bossTransitionPresenter = bossTransitionPresenter;
            _getContext = getContext;
            _onBossPhaseTransitioned = onBossPhaseTransitioned;
            _isDraining = false;
        }

        public void Enqueue(PendingAction pending)
        {
            _animQueue.Enqueue(pending);
        }

        public void ClearQueue()
        {
            _animQueue.Clear();
        }

        public void PlayQueuedAnimations(Action onComplete, bool fadeSlots = false)
        {
            if (_isDraining) return;
            _coroutineRunner.StartCoroutine(DrainQueue(onComplete, fadeSlots));
        }

        public IEnumerator DrainQueue(Action onComplete, bool fadeSlots = false)
        {
            _isDraining = true;
            try
            {
                int slotIndex = 0;
                while (_animQueue.Count > 0)
                {
                    if (_isCombatOver()) break;
                    
                    var pending = _animQueue.Dequeue();
                    yield return _coroutineRunner.StartCoroutine(_playActionGroup(pending));
                    
                    if (fadeSlots && _notifier != null)
                        _notifier.NotifyActionAnimationComplete(slotIndex++);
                    
                    // Check if a boss needs to transition phase
                    if (TryGetMultiPhaseActor(out var boss) && !boss.Model.IsAlive && boss.CurrentPhase < 3)
                    {
                        _animQueue.Clear();
                        if (_bossTransitionPresenter != null)
                        {
                            yield return _coroutineRunner.StartCoroutine(_bossTransitionPresenter.RunBossPhaseTransition(boss));
                        }
                        _onBossPhaseTransitioned?.Invoke();   // transition = boss's turn → skip its enemy phase
                        break;
                    }

                    if (_isCombatOver()) break;

                    if (_animQueue.Count > 0 && _betweenSkillsDelay > 0f)
                        yield return new WaitForSeconds(_betweenSkillsDelay);
                }

                // Check if a boss needs to transition phase (in case they died on the last action)
                if (TryGetMultiPhaseActor(out var lastBoss) && !lastBoss.Model.IsAlive && lastBoss.CurrentPhase < 3)
                {
                    if (_bossTransitionPresenter != null)
                    {
                        yield return _coroutineRunner.StartCoroutine(_bossTransitionPresenter.RunBossPhaseTransition(lastBoss));
                    }
                    _onBossPhaseTransitioned?.Invoke();   // transition = boss's turn → skip its enemy phase
                }

                if (_isCombatOver())
                {
                    yield return _coroutineRunner.StartCoroutine(_runCombatEndDrama());
                }

                if (onComplete != null && fadeSlots && _postPlayerTurnDelay > 0f)
                    yield return new WaitForSecondsRealtime(_postPlayerTurnDelay);

                onComplete?.Invoke();
            }
            finally
            {
                _isDraining = false;
            }
        }

        private bool TryGetMultiPhaseActor(out IMultiPhaseActor boss)
        {
            boss = null;
            var ctx = _getContext();
            if (ctx == null || ctx.Enemies == null) return false;
            for (int i = 0; i < ctx.Enemies.Count; i++)
            {
                if (ctx.Enemies[i] is IMultiPhaseActor mpActor)
                {
                    boss = mpActor;
                    return true;
                }
            }
            return false;
        }
    }
}
