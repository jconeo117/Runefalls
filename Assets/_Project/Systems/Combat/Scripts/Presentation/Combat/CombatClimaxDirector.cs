using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Runefall.Combat;

namespace Runefall.Presentation.Combat
{
    public class CombatClimaxDirector
    {
        private readonly MonoBehaviour _coroutineRunner;
        private readonly FinisherManager _finisherManager;
        private readonly VictorySequencer _victorySequencer;
        private readonly float _slowMoScale;
        private readonly float _slowMoDuration;
        private readonly float _pauseDuration;

        private bool _hasTriggeredOutroClimax;
        private Vector3 _killingBlowAttackerPos;
        private Vector3 _killingBlowTargetPos;

        public bool HasTriggeredOutroClimax => _hasTriggeredOutroClimax;
        public Action OnVictoryOutroTriggered;

        public CombatClimaxDirector(
            MonoBehaviour coroutineRunner,
            FinisherManager finisherManager,
            VictorySequencer victorySequencer,
            float slowMoScale,
            float slowMoDuration,
            float pauseDuration)
        {
            _coroutineRunner = coroutineRunner;
            _finisherManager = finisherManager;
            _victorySequencer = victorySequencer;
            _slowMoScale = slowMoScale;
            _slowMoDuration = slowMoDuration;
            _pauseDuration = pauseDuration;
            _hasTriggeredOutroClimax = false;
        }

        public void Reset()
        {
            _hasTriggeredOutroClimax = false;
            _killingBlowAttackerPos = Vector3.zero;
            _killingBlowTargetPos = Vector3.zero;
        }

        public void SetTriggered()
        {
            _hasTriggeredOutroClimax = true;
        }

        public void TryTriggerOutroClimax(CombatContext ctx, Action triggerEvent)
        {
            if (ctx != null && ctx.IsOver && ctx.PlayerWon && !_hasTriggeredOutroClimax)
            {
                _hasTriggeredOutroClimax = true;
                triggerEvent?.Invoke();
                OnVictoryOutroTriggered?.Invoke();
            }
        }

        public IEnumerator RunCombatEndDrama(CombatContext ctx)
        {
            if (_hasTriggeredOutroClimax)
                yield break;

            if (ctx == null || !ctx.PlayerWon)
                yield break;

            _hasTriggeredOutroClimax = true;

            if (_victorySequencer != null)
            {
                OnVictoryOutroTriggered?.Invoke();
            }
            else if (_finisherManager != null)
            {
                yield return _finisherManager.Play(_killingBlowAttackerPos, _killingBlowTargetPos);
            }
            else
            {
                yield return _coroutineRunner.StartCoroutine(FallbackLastHitDrama());
            }
        }

        private IEnumerator FallbackLastHitDrama()
        {
            Time.timeScale = _slowMoScale;
            yield return new WaitForSecondsRealtime(_slowMoDuration);
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(_pauseDuration);
        }

        public void TryCaptureKillingBlowPositions(CombatContext ctx, CombatActionResult[] results, IReadOnlyDictionary<ICombatActor, Transform> actorPawns)
        {
            if (ctx == null || !ctx.IsOver || !ctx.PlayerWon) return;
            for (int i = 0; i < results.Length; i++)
            {
                var r = results[i];
                if (r.Target == null || r.Target.IsAlive) continue;
                _killingBlowAttackerPos = actorPawns.TryGetValue(r.Caster, out var ap) ? ap.position : Vector3.zero;
                _killingBlowTargetPos = actorPawns.TryGetValue(r.Target, out var tp) ? tp.position : Vector3.zero;
                return;
            }
        }
    }
}
