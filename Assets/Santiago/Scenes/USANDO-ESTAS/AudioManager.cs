using UnityEngine;

/// <summary>
/// AudioManager — Singleton global de audio (Unity built-in).
///
/// Maneja dos canales separados:
///   · SFX  : efectos cortos (hover, clicks, pasos, gacha, etc.)
///   · Music : música de fondo con fade in/out suave.
///
/// Uso desde cualquier script:
///   AudioManager.Instance.PlaySFX(miClip);
///   AudioManager.Instance.PlaySFXWithPitch(miClip, 1.1f);
///   AudioManager.Instance.PlayMusic(musicaDeInicio, fadeTime: 1f);
///   AudioManager.Instance.StopMusic(fadeTime: 1f);
///
/// Setup en escena:
///   · Crear un GameObject vacío llamado "AudioManager" y adjuntar este script.
///   · (Opcional) Llenar sfxSource y musicSource en el Inspector, o se crean solos.
/// </summary>
public class AudioManager : UnityEngine.MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────────
    public static AudioManager Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────────────────
    [UnityEngine.Header("Sources (se crean automáticamente si están vacías)")]
    public UnityEngine.AudioSource sfxSource;
    public UnityEngine.AudioSource musicSource;

    [UnityEngine.Header("Volúmenes iniciales")]
    [UnityEngine.Range(0f, 1f)] public float sfxVolume = 1f;
    [UnityEngine.Range(0f, 1f)] public float musicVolume = 0.6f;

    // ── Privado ────────────────────────────────────────────────────────────────
    private System.Collections.IEnumerator _musicFadeCoroutine_handle;

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitSources();
    }

    // ── Inicialización ────────────────────────────────────────────────────────
    private void InitSources()
    {
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<UnityEngine.AudioSource>();
            sfxSource.playOnAwake = false;
        }
        sfxSource.volume = sfxVolume;

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<UnityEngine.AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
        }
        musicSource.volume = musicVolume;
    }

    // ── API pública: SFX ──────────────────────────────────────────────────────

    /// <summary>Reproduce un clip de efecto de sonido (fire-and-forget).</summary>
    public void PlaySFX(UnityEngine.AudioClip clip, float volumeOverride = -1f)
    {
        if (clip == null) return;
        float vol = volumeOverride >= 0f ? volumeOverride : sfxVolume;
        sfxSource.PlayOneShot(clip, vol);
    }

    /// <summary>
    /// Reproduce un clip con pitch personalizado.
    /// Crea un AudioSource temporal para no afectar el pitch global de sfxSource.
    /// Ideal para pasos, impactos y cualquier sonido que necesite variación de tono.
    /// </summary>
    public void PlaySFXWithPitch(UnityEngine.AudioClip clip, float pitch, float volumeOverride = -1f)
    {
        if (clip == null) return;
        float vol = volumeOverride >= 0f ? volumeOverride : sfxVolume;
        StartCoroutine(PlayWithPitchCoroutine(clip, pitch, vol));
    }

    private System.Collections.IEnumerator PlayWithPitchCoroutine(
        UnityEngine.AudioClip clip, float pitch, float volume)
    {
        // Fuente temporal independiente para no tocar el pitch global de sfxSource
        var temp = gameObject.AddComponent<UnityEngine.AudioSource>();
        temp.clip = clip;
        temp.pitch = UnityEngine.Mathf.Clamp(pitch, 0.1f, 3f);
        temp.volume = volume;
        temp.spatialBlend = 0f; // 2D
        temp.playOnAwake = false;
        temp.Play();

        yield return new UnityEngine.WaitForSecondsRealtime(
            clip.length / UnityEngine.Mathf.Abs(temp.pitch) + 0.05f);

        Destroy(temp);
    }

    // ── API pública: Música ───────────────────────────────────────────────────

    /// <summary>
    /// Reproduce una pista de música.
    /// Si ya hay música sonando, hace fade out → swap → fade in.
    /// </summary>
    public void PlayMusic(UnityEngine.AudioClip clip, float fadeTime = 0.5f)
    {
        if (clip == null) return;
        if (_musicFadeCoroutine_handle != null) StopCoroutine(_musicFadeCoroutine_handle);
        _musicFadeCoroutine_handle = CrossfadeMusic(clip, fadeTime);
        StartCoroutine(_musicFadeCoroutine_handle);
    }

    /// <summary>Para la música con un fade out suave.</summary>
    public void StopMusic(float fadeTime = 0.5f)
    {
        if (_musicFadeCoroutine_handle != null) StopCoroutine(_musicFadeCoroutine_handle);
        _musicFadeCoroutine_handle = FadeOut(fadeTime);
        StartCoroutine(_musicFadeCoroutine_handle);
    }

    /// <summary>Ajusta el volumen global de SFX en tiempo real.</summary>
    public void SetSFXVolume(float value)
    {
        sfxVolume = UnityEngine.Mathf.Clamp01(value);
        sfxSource.volume = sfxVolume;
    }

    /// <summary>Ajusta el volumen global de música en tiempo real.</summary>
    public void SetMusicVolume(float value)
    {
        musicVolume = UnityEngine.Mathf.Clamp01(value);
        musicSource.volume = musicVolume;
    }

    // ── Coroutines internas ───────────────────────────────────────────────────
    private System.Collections.IEnumerator CrossfadeMusic(UnityEngine.AudioClip newClip, float fadeTime)
    {
        if (musicSource.isPlaying && fadeTime > 0f)
        {
            float startVol = musicSource.volume;
            float t = 0f;
            while (t < fadeTime)
            {
                t += UnityEngine.Time.unscaledDeltaTime;
                musicSource.volume = UnityEngine.Mathf.Lerp(startVol, 0f, t / fadeTime);
                yield return null;
            }
        }

        musicSource.Stop();
        musicSource.clip = newClip;
        musicSource.Play();

        if (fadeTime > 0f)
        {
            float t = 0f;
            while (t < fadeTime)
            {
                t += UnityEngine.Time.unscaledDeltaTime;
                musicSource.volume = UnityEngine.Mathf.Lerp(0f, musicVolume, t / fadeTime);
                yield return null;
            }
        }

        musicSource.volume = musicVolume;
    }

    private System.Collections.IEnumerator FadeOut(float fadeTime)
    {
        float startVol = musicSource.volume;
        float t = 0f;
        while (t < fadeTime)
        {
            t += UnityEngine.Time.unscaledDeltaTime;
            musicSource.volume = UnityEngine.Mathf.Lerp(startVol, 0f, t / fadeTime);
            yield return null;
        }
        musicSource.Stop();
        musicSource.volume = musicVolume;
    }
}