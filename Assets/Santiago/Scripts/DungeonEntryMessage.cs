using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DungeonEntryMessage : MonoBehaviour
{
    [Header("Primera RawImage (solo se le toca el alpha)")]
    public RawImage message;

    [Header("Segunda RawImage (aparece cuando se va la primera)")]
    public RawImage message2;

    [Header("Tiempos (en segundos)")]
    public float delayBeforeShow = 1.22f;    // espera antes de que aparezca la primera
    public float fadeInDuration = 0.5f;       // cuánto tarda en aparecer (las dos)
    public float fadeOutDuration = 0.5f;      // cuánto tarda en irse (las dos)
    public float visibleDuration = 3f;        // cuánto se queda la primera
    public float visibleDuration2 = 3.22f;    // cuánto se queda la segunda

    [Header("Sonido")]
    public AudioSource sfxSource;             // AudioSource para reproducir los sonidos
    public AudioClip showSound;               // sonido al aparecer la primera
    public AudioClip showSound2;              // sonido al aparecer la segunda (opcional)
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    void Start()
    {
        // las dos arrancan transparentes (solo alpha, no tocamos posición ni escala)
        SetAlpha(message, 0f);
        SetAlpha(message2, 0f);

        StartCoroutine(ShowMessagesRoutine());
    }

    IEnumerator ShowMessagesRoutine()
    {
        // espera inicial
        yield return new WaitForSeconds(delayBeforeShow);

        // ----- PRIMERA IMAGEN -----
        if (message != null)
        {
            if (sfxSource != null && showSound != null)
                sfxSource.PlayOneShot(showSound, sfxVolume);

            yield return StartCoroutine(Fade(message, 0f, 1f, fadeInDuration));
            yield return new WaitForSeconds(visibleDuration);
            yield return StartCoroutine(Fade(message, 1f, 0f, fadeOutDuration));
        }

        // ----- SEGUNDA IMAGEN -----
        if (message2 != null)
        {
            if (sfxSource != null && showSound2 != null)
                sfxSource.PlayOneShot(showSound2, sfxVolume);

            yield return StartCoroutine(Fade(message2, 0f, 1f, fadeInDuration));
            yield return new WaitForSeconds(visibleDuration2);
            yield return StartCoroutine(Fade(message2, 1f, 0f, fadeOutDuration));
        }
    }

    IEnumerator Fade(RawImage img, float from, float to, float duration)
    {
        if (img == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            SetAlpha(img, a);
            yield return null;
        }
        SetAlpha(img, to);
    }

    void SetAlpha(RawImage img, float a)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = a;             // SOLO el alpha; posición y escala quedan intactas
        img.color = c;
    }
}