using System.Collections;
using UnityEngine;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Handles time scale manipulations (slow-mo, freeze frame, hit-stop) during combat outro sequences.
    /// </summary>
    public class CombatTimeDilationManager
    {
        private readonly MonoBehaviour _runner;
        private Coroutine _activeRampRoutine;

        public CombatTimeDilationManager(MonoBehaviour runner)
        {
            _runner = runner;
        }

        public void ApplySlowMo(float scale, float fixedDeltaTimeDefault = 0.02f)
        {
            Time.timeScale = scale;
            Time.fixedDeltaTime = fixedDeltaTimeDefault * scale;
        }

        public void ResetTimeScale()
        {
            StopActiveRamp();
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        public void StartFreezeThenSlowMo(float freezeScale, float slowMoScale, float freezeDuration, float rampDuration)
        {
            StopActiveRamp();
            _activeRampRoutine = _runner.StartCoroutine(FreezeThenSlowMoRoutine(freezeScale, slowMoScale, freezeDuration, rampDuration));
        }

        public void StartRamp(float from, float to, float duration)
        {
            StopActiveRamp();
            _activeRampRoutine = _runner.StartCoroutine(RampTimeScaleRoutine(from, to, duration));
        }

        public void StopActiveRamp()
        {
            if (_activeRampRoutine != null)
            {
                _runner.StopCoroutine(_activeRampRoutine);
                _activeRampRoutine = null;
            }
        }

        private IEnumerator FreezeThenSlowMoRoutine(float freezeScale, float slowMoScale, float freezeDuration, float rampDuration)
        {
            Time.timeScale = freezeScale;
            Time.fixedDeltaTime = 0.02f * freezeScale;

            if (freezeDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(freezeDuration);
            }

            yield return RampTimeScaleRoutine(freezeScale, slowMoScale, rampDuration);
        }

        private IEnumerator RampTimeScaleRoutine(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
                Time.timeScale = Mathf.Lerp(from, to, k);
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
                yield return null;
            }
            Time.timeScale = to;
            Time.fixedDeltaTime = 0.02f * to;
        }
    }
}
