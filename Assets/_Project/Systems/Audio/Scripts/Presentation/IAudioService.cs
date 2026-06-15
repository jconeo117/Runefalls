using UnityEngine;

namespace Runefall.Audio
{
    /// <summary>
    /// Servicio global de reproducción de sonido. Registrado en ServiceLocator por AudioManager.
    /// Consumido por sistemas de presentación (combate, UI) para emitir SFX/voces sin acoplarse
    /// a la implementación concreta del pool de AudioSources.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>Reproduce un clip one-shot en una posición del mundo.</summary>
        void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f);

        /// <summary>Reproduce un clip one-shot sin posición (2D, siempre audible).</summary>
        void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f);
    }
}
