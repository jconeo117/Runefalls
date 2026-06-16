using UnityEngine;

/// <summary>
/// A_SkillsInfoAudio
/// Componente de audio para SkillsInfo. Adjuntar al mismo GameObject que SkillsInfo.
/// SkillsInfo lo busca en Start y llama OnHover() al pasar el mouse sobre un icono de skill.
/// Toda la configuración de sonido vive acá.
/// </summary>
public class A_SkillsInfoAudio : MonoBehaviour
{
    [Header("Sonido de hover")]
    [Tooltip("Clip que se reproduce al pasar el mouse sobre un icono de skill.")]
    public AudioClip hoverSound;

    [Tooltip("Si está activado, usa el volumen global de SFX del AudioManager.")]
    public bool hoverUseGlobalVolume = true;

    [Tooltip("Volumen específico (solo si hoverUseGlobalVolume está desactivado).")]
    [Range(0f, 1f)]
    public float hoverVolume = 1f;

    // ── API pública (llamada desde SkillsInfo) ───────────────────────────────

    public void OnHover() => PlaySFX(hoverSound, hoverUseGlobalVolume, hoverVolume);

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