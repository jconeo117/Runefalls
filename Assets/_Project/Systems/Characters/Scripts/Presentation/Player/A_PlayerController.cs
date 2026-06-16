using UnityEngine;
using Runefall.Audio;

/// <summary>
/// A_PlayerFootsteps
/// Componente de audio de pasos para exploración 3D.
/// Adjuntar al mismo GameObject que PlayerController.
/// PlayerController lo busca en Awake y llama OnStep(frameDist) cada frame.
/// </summary>
public class A_PlayerFootsteps : MonoBehaviour
{
    public enum FootstepTimingMode
    {
        Distance,
        Time
    }

    [Header("Modo de timing")]
    public FootstepTimingMode timingMode = FootstepTimingMode.Distance;

    [Tooltip("Cada cuántas unidades de distancia se reproduce un paso (modo Distance).")]
    public float footstepDistance = 0.55f;

    [Tooltip("Cada cuántos segundos se reproduce un paso (modo Time).")]
    public float footstepInterval = 0.45f;

    [Header("Sonido de paso")]
    public AudioClip footstepSound;

    [Tooltip("Si está activado, usa el volumen master del AudioManager de Runefall.")]
    public bool useGlobalVolume = true;

    [Range(0f, 1f)]
    public float footstepVolume = 1f;

    [Header("Espacialización")]
    [Tooltip("Si está activado, el paso suena en la posición del jugador (3D).")]
    public bool use3DPosition = true;

    [Header("Variación de pitch")]
    [Range(0.5f, 2f)]
    public float pitchBase = 1f;

    [Range(0f, 0.5f)]
    public float pitchVariation = 0.12f;

    private float _distanceAccumulator;
    private float _timeAccumulator;

    public void OnStep(float frameDist)
    {
        if (frameDist <= 0f)
        {
            _distanceAccumulator = 0f;
            _timeAccumulator = 0f;
            return;
        }

        if (timingMode == FootstepTimingMode.Distance)
            HandleDistance(frameDist);
        else
            HandleTime();
    }

    private void HandleDistance(float frameDist)
    {
        _distanceAccumulator += frameDist;

        if (_distanceAccumulator >= footstepDistance)
        {
            _distanceAccumulator -= footstepDistance;
            PlayFootstep();
        }
    }

    private void HandleTime()
    {
        _timeAccumulator += Time.deltaTime;

        if (_timeAccumulator >= footstepInterval)
        {
            _timeAccumulator -= footstepInterval;
            PlayFootstep();
        }
    }

    private void PlayFootstep()
    {
        if (footstepSound == null) return;

        float pitch = pitchBase + Random.Range(-pitchVariation, pitchVariation);
        float vol = useGlobalVolume ? 1f : footstepVolume;

        IAudioService audio = AudioManager.GetOrCreate();
        if (audio != null)
        {
            if (use3DPosition)
                audio.PlayAt(footstepSound, transform.position, vol, pitch);
            else
                audio.Play2D(footstepSound, vol, pitch);

            return;
        }

        PlayClipWithPitchFallback(footstepSound, pitch, vol);
    }

    private void PlayClipWithPitchFallback(AudioClip clip, float pitch, float volume)
    {
        GameObject temp = new GameObject("Footstep_Temp");
        temp.transform.position = transform.position;
        AudioSource src = temp.AddComponent<AudioSource>();
        src.clip = clip;
        src.pitch = pitch;
        src.volume = volume;
        src.spatialBlend = use3DPosition ? 1f : 0f;
        src.Play();
        Destroy(temp, clip.length / Mathf.Abs(pitch) + 0.1f);
    }
}