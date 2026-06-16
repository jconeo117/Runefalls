using UnityEngine;

/// <summary>
/// A_CardDrawAudio
/// Componente de audio para CardDrawAnimation. Adjuntar al mismo GameObject.
/// CardDrawAnimation lo busca en Awake y llama OnButtonClick / OnShuffleTick / OnReveal.
/// Toda la configuración de sonido vive acá.
/// </summary>
public class A_CardDrawAudio : MonoBehaviour
{
    [Header("Clicks y reveal")]
    public AudioClip buttonClickSound;
    public AudioClip revealSound;
    public bool sfxUseGlobalVolume = true;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    [Header("Shuffle tipo ruleta (un tick por carta resaltada)")]
    public AudioClip shuffleTickSound;
    [Tooltip("Opcional: elige uno al azar en cada paso para más variedad.")]
    public AudioClip[] shuffleTickVariants;
    public bool shuffleUseGlobalVolume = true;
    [Range(0f, 1f)] public float shuffleTickVolume = 0.75f;
    [Tooltip("Pitch al inicio (pasos rápidos).")]
    public float shufflePitchStart = 1.35f;
    [Tooltip("Pitch al final (pasos lentos, como ruleta frenando).")]
    public float shufflePitchEnd = 0.85f;
    [Range(0f, 0.15f)] public float shufflePitchJitter = 0.04f;

    // ── API pública (llamada desde CardDrawAnimation) ───────────────────────

    public void OnButtonClick() => PlaySFX(buttonClickSound, sfxUseGlobalVolume, sfxVolume);

    public void OnReveal() => PlaySFX(revealSound, sfxUseGlobalVolume, sfxVolume);

    public void OnShuffleTick(int stepIndex, int totalSteps)
    {
        AudioClip clip = PickShuffleTickClip();
        if (clip == null) return;

        float progress = totalSteps > 1 ? (float)stepIndex / (totalSteps - 1) : 1f;
        float pitch = Mathf.Lerp(shufflePitchStart, shufflePitchEnd, progress);
        if (shufflePitchJitter > 0f)
            pitch += Random.Range(-shufflePitchJitter, shufflePitchJitter);
        pitch = Mathf.Clamp(pitch, 0.5f, 2f);

        float vol = shuffleUseGlobalVolume ? -1f : shuffleTickVolume;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFXWithPitch(clip, pitch, vol);
            return;
        }

        PlayClipWithPitchFallback(clip, pitch, vol >= 0f ? vol : 1f);
    }

    // ── Interno ───────────────────────────────────────────────────────────────

    private void PlaySFX(AudioClip clip, bool useGlobal, float volume)
    {
        if (clip == null) return;

        float vol = useGlobal ? -1f : volume;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clip, vol);
            return;
        }

        AudioSource.PlayClipAtPoint(clip,
            Camera.main != null ? Camera.main.transform.position : Vector3.zero,
            vol >= 0f ? vol : 1f);
    }

    private AudioClip PickShuffleTickClip()
    {
        if (shuffleTickVariants != null && shuffleTickVariants.Length > 0)
        {
            for (int attempt = 0; attempt < shuffleTickVariants.Length; attempt++)
            {
                AudioClip candidate = shuffleTickVariants[Random.Range(0, shuffleTickVariants.Length)];
                if (candidate != null) return candidate;
            }
        }

        return shuffleTickSound;
    }

    private void PlayClipWithPitchFallback(AudioClip clip, float pitch, float volume)
    {
        GameObject temp = new GameObject("CardDrawShuffle_Temp");
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
