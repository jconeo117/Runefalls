using UnityEngine;

/// <summary>
/// A_WorldMapFootsteps
/// Componente de audio de pasos para el mapa mundo.
/// Adjuntar al mismo GameObject que WorldMapPlayerController.
/// WorldMapPlayerController lo busca en Awake y llama OnStep(distanceTravelled)
/// cada frame; este componente decide cuándo y cómo reproducir el sonido.
/// Toda la configuración de audio vive acá.
/// </summary>
public class A_WorldMapFootsteps : MonoBehaviour
{
    [Header("Sonido de paso")]
    [Tooltip("Clip del sonido de paso.")]
    public AudioClip footstepSound;

    [Tooltip("Cada cuántas unidades de distancia recorrida se reproduce un paso.")]
    public float footstepDistance = 0.6f;

    [Tooltip("Si está activado, usa el volumen global de SFX del AudioManager.")]
    public bool useGlobalVolume = true;

    [Tooltip("Volumen del paso (solo si useGlobalVolume está desactivado).")]
    [Range(0f, 1f)]
    public float footstepVolume = 1f;

    [Header("Variación de pitch")]
    [Tooltip("Pitch base del sonido de paso.")]
    [Range(0.5f, 2f)]
    public float pitchBase = 1f;

    [Tooltip("Variación aleatoria de pitch (±) para que los pasos no suenen todos iguales.")]
    [Range(0f, 0.5f)]
    public float pitchVariation = 0.1f;

    // ── Privado ───────────────────────────────────────────────────────────────
    private float _accumulator = 0f;

    // ── API pública (llamada desde WorldMapPlayerController) ──────────────────

    /// <summary>
    /// Llamar cada frame con la distancia recorrida en ese frame (metros).
    /// Internamente acumula y decide cuándo disparar el sonido.
    /// </summary>
    public void OnStep(float frameDist)
    {
        if (frameDist <= 0f)
        {
            _accumulator = 0f; // resetear al frenar para que el primer paso no salga demasiado pronto
            return;
        }

        _accumulator += frameDist;

        if (_accumulator >= footstepDistance)
        {
            _accumulator -= footstepDistance; // restar, no resetear, para mantener ritmo exacto
            PlayFootstep();
        }
    }

    // ── Interno ───────────────────────────────────────────────────────────────

    private void PlayFootstep()
    {
        if (footstepSound == null) return;

        float pitch = pitchBase + Random.Range(-pitchVariation, pitchVariation);
        float vol = useGlobalVolume ? -1f : footstepVolume;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFXWithPitch(footstepSound, pitch, vol);
        }
        else
        {
            PlayClipWithPitchFallback(footstepSound, pitch, vol >= 0f ? vol : 1f);
        }
    }

    /// Fallback sin AudioManager: crea un AudioSource temporal con pitch personalizado.
    private void PlayClipWithPitchFallback(AudioClip clip, float pitch, float volume)
    {
        GameObject temp = new GameObject("Footstep_Temp");
        temp.transform.position = transform.position;
        AudioSource src = temp.AddComponent<AudioSource>();
        src.clip = clip;
        src.pitch = pitch;
        src.volume = volume;
        src.spatialBlend = 0f;
        src.Play();
        Destroy(temp, clip.length / Mathf.Abs(pitch) + 0.1f);
    }
}