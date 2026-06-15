using UnityEngine;

namespace Runefall.Data
{
    /// <summary>
    /// Banco de voces de un combatiente: gritos de ataque, de golpe recibido y de muerte.
    /// Cada lista admite varios clips; se elige uno al azar para evitar repetición.
    /// Asignado en CharacterData.voiceSet / EnemyData.voiceSet.
    /// </summary>
    [CreateAssetMenu(menuName = "Runefall/Audio/Voice Set")]
    public class VoiceSetData : ScriptableObject
    {
        [Tooltip("Gritos al lanzar un ataque.")]
        public AudioClip[] attackClips;
        [Tooltip("Reacciones al recibir un golpe (sobrevive).")]
        public AudioClip[] hitClips;
        [Tooltip("Gritos al morir.")]
        public AudioClip[] deathClips;

        [Header("Mezcla")]
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Variación aleatoria de pitch (+/-) para que no suene idéntico cada vez.")]
        [Range(0f, 0.5f)] public float pitchJitter = 0.08f;

        public AudioClip RandomAttack() => Pick(attackClips);
        public AudioClip RandomHit()    => Pick(hitClips);
        public AudioClip RandomDeath()  => Pick(deathClips);

        /// <summary>Pitch base 1.0 con jitter aleatorio aplicado.</summary>
        public float RandomPitch() =>
            pitchJitter <= 0f ? 1f : 1f + Random.Range(-pitchJitter, pitchJitter);

        private static AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }
    }
}
