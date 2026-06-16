using System.Collections;
using UnityEngine;

namespace Runefall.Presentation.Dungeon
{
    /// <summary>
    /// A_EnemyEncounterMusic
    /// Música de tensión/encuentro al activarse EnemyEncounterTrigger.
    /// Adjuntar al mismo GameObject que EnemyEncounterTrigger.
    /// EnemyEncounterTrigger lo busca en Awake y llama OnEncounterActivated() / OnEncounterReset().
    /// </summary>
    public class A_EnemyEncounterMusic : MonoBehaviour
    {
        [Header("Música de encuentro")]
        [Tooltip("Pista que suena cuando el jugador entra en el trigger de encuentro.")]
        [SerializeField] private AudioClip _encounterMusic;

        [Tooltip("Duración del fade in al activarse el encuentro.")]
        [SerializeField] private float _fadeInDuration = 0.8f;

        [Tooltip("Duración del fade out al resetear el trigger (post-combate).")]
        [SerializeField] private float _fadeOutDuration = 1f;

        [Range(0f, 1f)]
        [SerializeField] private float _musicVolume = 0.65f;

        [Header("Comportamiento")]
        [Tooltip("Si está activado, detiene la música con fade out cuando se llama ResetTrigger().")]
        [SerializeField] private bool _stopOnReset = true;

        private AudioSource _musicSource;
        private Coroutine  _fadeCoroutine;

        private void Awake()
        {
            _musicSource = GetComponent<AudioSource>();
            if (_musicSource == null)
                _musicSource = gameObject.AddComponent<AudioSource>();

            _musicSource.playOnAwake = false;
            _musicSource.loop        = true;
            _musicSource.spatialBlend  = 0f;
            _musicSource.volume        = 0f;
        }

        /// <summary>Llamado por EnemyEncounterTrigger al detectar al jugador.</summary>
        public void OnEncounterActivated()
        {
            if (_encounterMusic == null) return;

            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _musicSource.clip = _encounterMusic;

            if (!_musicSource.isPlaying)
                _musicSource.Play();

            _fadeCoroutine = StartCoroutine(FadeVolume(_musicSource.volume, _musicVolume, _fadeInDuration));
        }

        /// <summary>Llamado por EnemyEncounterTrigger.ResetTrigger() tras el combate.</summary>
        public void OnEncounterReset()
        {
            if (!_stopOnReset || !_musicSource.isPlaying) return;

            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeOutAndStop(_fadeOutDuration));
        }

        private IEnumerator FadeVolume(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                _musicSource.volume = to;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _musicSource.volume = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }

            _musicSource.volume = to;
            _fadeCoroutine      = null;
        }

        private IEnumerator FadeOutAndStop(float duration)
        {
            float startVol = _musicSource.volume;

            if (duration <= 0f)
            {
                _musicSource.Stop();
                _musicSource.volume = 0f;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _musicSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
                yield return null;
            }

            _musicSource.Stop();
            _musicSource.volume = 0f;
            _fadeCoroutine      = null;
        }
    }
}
