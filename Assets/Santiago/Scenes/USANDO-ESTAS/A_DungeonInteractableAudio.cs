using UnityEngine;

/// <summary>
/// A_DungeonInteractableAudio
/// Componente de audio para dungeons en el mapa mundo.
/// Adjuntar al mismo GameObject que DungeonInteractable.
/// DungeonInteractable lo busca en Awake y llama OnInteract() / OnEnter().
/// Toda la configuración de sonido vive acá.
/// </summary>
public class A_DungeonInteractableAudio : MonoBehaviour
{
    [Header("Sonido de interacción (abrir panel)")]
    [Tooltip("Clip que se reproduce al hacer click sobre el dungeon y abrir el panel.")]
    public AudioClip interactSound;

    [Tooltip("Si está activado, usa el volumen global de SFX del AudioManager.")]
    public bool interactUseGlobalVolume = true;

    [Tooltip("Volumen específico (solo si interactUseGlobalVolume está desactivado).")]
    [Range(0f, 1f)]
    public float interactSoundVolume = 1f;

    [Header("Sonido de entrar al dungeon (botón ENTRAR)")]
    [Tooltip("Clip que se reproduce al confirmar la entrada al dungeon.")]
    public AudioClip enterSound;

    [Tooltip("Si está activado, usa el volumen global de SFX del AudioManager.")]
    public bool enterUseGlobalVolume = true;

    [Tooltip("Volumen específico (solo si enterUseGlobalVolume está desactivado).")]
    [Range(0f, 1f)]
    public float enterSoundVolume = 1f;

    // ── API pública (llamada desde DungeonInteractable) ────────────────────────

    public void OnInteract() => PlaySFX(interactSound, interactUseGlobalVolume, interactSoundVolume);
    public void OnEnter() => PlaySFX(enterSound, enterUseGlobalVolume, enterSoundVolume);

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