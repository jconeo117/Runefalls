using UnityEngine;

/// <summary>
/// A_ButtonAudio
/// Componente de audio para botones. Adjuntar al mismo GameObject que ButtonEffect.
/// ButtonEffect lo busca en Awake y llama OnHover() / OnClick() cuando corresponde.
/// Toda la configuración de sonido vive acá, ButtonEffect no sabe nada de audio.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class A_ButtonAudio : MonoBehaviour
{
    [Header("Sonido de hover")]
    [Tooltip("Clip que se reproduce al entrar el mouse sobre el botón.")]
    public AudioClip hoverSound;

    [Tooltip("Si está activado, usa el volumen global de SFX del AudioManager.")]
    public bool hoverUseGlobalVolume = true;

    [Tooltip("Volumen específico para este botón (solo si hoverUseGlobalVolume está desactivado).")]
    [Range(0f, 1f)]
    public float hoverVolume = 1f;

    [Header("Sonido de click")]
    [Tooltip("Clip que se reproduce al hacer click izquierdo sobre el botón.")]
    public AudioClip clickSound;

    [Tooltip("Si está activado, usa el volumen global de SFX del AudioManager.")]
    public bool clickUseGlobalVolume = true;

    [Tooltip("Volumen específico para este botón (solo si clickUseGlobalVolume está desactivado).")]
    [Range(0f, 1f)]
    public float clickVolume = 1f;

    // ── API pública (llamada desde ButtonEffect) ───────────────────────────────

    public void OnHover() => PlaySFX(hoverSound, hoverUseGlobalVolume, hoverVolume);
    public void OnClick() => PlaySFX(clickSound, clickUseGlobalVolume, clickVolume);

    // ── Interno ───────────────────────────────────────────────────────────────

    private void PlaySFX(AudioClip clip, bool useGlobal, float volume)
    {
        if (clip == null) return;

        float vol = useGlobal ? -1f : volume;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clip, vol);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip,
                Camera.main != null ? Camera.main.transform.position : Vector3.zero,
                vol >= 0f ? vol : 1f);
        }
    }
}