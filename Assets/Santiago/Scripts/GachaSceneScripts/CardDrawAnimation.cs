using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CardDrawAnimation : MonoBehaviour
{
    [Header("Cartas (las 3 imágenes de personajes)")]
    public RectTransform[] cards = new RectTransform[3];

    [Header("Overlays negros encima de cada carta")]
    public RawImage[] overlays = new RawImage[3];

    [Header("Botones")]
    public Button drawButton;
    public Button continueButton;

    [Header("Escena a cargar al tocar Continue")]
    public string dungeonSceneName = "";

    [Header("Nombres de los personajes (mismo orden: izq, medio, der)")]
    public string[] characterNames = { "Guerrero", "Mago", "Arquero" };

    [Header("=== SONIDOS ===")]
    public AudioSource sfxSource;       // para clicks y reveal (one-shot)
    public AudioSource loopSource;      // para el shuffling (loop)
    public AudioClip buttonClickSound;
    public AudioClip shufflingSound;
    public AudioClip revealSound;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    [Range(0f, 1f)] public float loopVolume = 0.6f;

    [Header("=== FADE IN / OUT ===")]
    public Image fadeOverlay;           // Image negro que cubre toda la pantalla
    public float fadeInDuration = 0.8f;   // al cargar la escena
    public float fadeOutDuration = 0.8f;  // al tocar continue

    [Header("Configuración de la animación")]
    public int totalSteps = 16;
    public float baseDelay = 0.07f;
    public float maxExtraDelay = 0.36f;
    public float highlightLiftY = 20f;
    public float winnerLiftY = 30f;
    public float overlayFadeDuration = 0.6f;

    [Header("Movimiento al centro (después del reveal)")]
    public float swapDuration = 0.8f;
    public float finalScale = 1.25f;
    public float scaleUpDuration = 0.35f;
    public float pauseBeforeSwap = 0.4f;

    [Header("Aparición del botón Continue")]
    public float continueButtonFadeDuration = 0.4f;
    public float pauseBeforeContinue = 0.3f;

    [Header("Opacidad de los overlays")]
    [Range(0f, 1f)] public float overlayAlpha = 0.996f;
    [Range(0f, 1f)] public float highlightAlpha = 0.55f;
    [Range(0f, 1f)] public float loserAlpha = 1f;

    private Color overlayBlack => new Color(0f, 0f, 0f, overlayAlpha);
    private Color overlayHighlight => new Color(0.21f, 0.54f, 0.86f, highlightAlpha);

    private Vector3[] originalWorldPositions;
    private bool isRunning = false;

    public static int SelectedCharacterIndex = -1;
    public static string SelectedCharacterName = "";

    void Start()
    {
        // posiciones originales
        originalWorldPositions = new Vector3[cards.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            originalWorldPositions[i] = cards[i].position;
            overlays[i].color = overlayBlack;
        }

        // botones
        if (drawButton != null)
            drawButton.onClick.AddListener(OnDrawButtonPressed);
        else
            Debug.LogWarning("CardDrawAnimation: no asignaste el botón draw.");

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueButtonPressed);
            continueButton.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("CardDrawAnimation: no asignaste el botón continue.");
        }

        // config inicial de audio
        if (sfxSource != null) sfxSource.volume = sfxVolume;
        if (loopSource != null)
        {
            loopSource.volume = loopVolume;
            loopSource.loop = true;
        }

        // 🎬 fade-in al arrancar la escena
        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.color = new Color(0f, 0f, 0f, 1f);
            StartCoroutine(FadeOverlay(1f, 0f, fadeInDuration, false));
        }
    }

    public void OnDrawButtonPressed()
    {
        if (isRunning) return;
        isRunning = true;

        PlaySfx(buttonClickSound);

        if (drawButton != null) drawButton.gameObject.SetActive(false);

        StartCoroutine(RunDraw());
    }

    public void OnContinueButtonPressed()
    {
        PlaySfx(buttonClickSound);

        // deshabilitamos el botón para evitar doble click durante el fade
        if (continueButton != null) continueButton.interactable = false;

        StartCoroutine(FadeOutAndLoadScene());
    }

    IEnumerator FadeOutAndLoadScene()
    {
        if (fadeOverlay != null)
        {
            yield return StartCoroutine(FadeOverlay(0f, 1f, fadeOutDuration, true));
        }

        if (!string.IsNullOrEmpty(dungeonSceneName))
        {
            SceneManager.LoadScene(dungeonSceneName);
        }
        else
        {
            Debug.LogWarning("CardDrawAnimation: no configuraste el nombre de la escena.");
        }
    }

    IEnumerator RunDraw()
    {
        yield return new WaitForSeconds(0.3f);

        // 🔊 arranca el loop de shuffling
        PlayLoop(shufflingSound);

        int winnerIdx = Random.Range(0, 3);

        int currentIdx = 0;
        for (int i = 0; i < totalSteps; i++)
        {
            ResetAllToBase();

            if (i == totalSteps - 1)
                currentIdx = winnerIdx;
            else
                currentIdx = (currentIdx + 1) % 3;

            HighlightCard(currentIdx);

            float t = (float)i / totalSteps;
            float delay = baseDelay + Mathf.Pow(t, 2.3f) * maxExtraDelay;
            yield return new WaitForSeconds(delay);
        }

        // 🔊 paramos el shuffling y sonamos el reveal
        StopLoop();
        PlaySfx(revealSound);

        yield return StartCoroutine(LockInWinner(winnerIdx));

        yield return StartCoroutine(MoveWinnerToCenter(winnerIdx));

        SelectedCharacterIndex = winnerIdx;
        SelectedCharacterName = characterNames[winnerIdx];
        Debug.Log("Personaje elegido: " + SelectedCharacterName);

        yield return StartCoroutine(ShowContinueButton());

        isRunning = false;
    }

    void ResetAllToBase()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].position = originalWorldPositions[i];
            cards[i].localScale = Vector3.one;
            overlays[i].color = overlayBlack;
        }
    }

    void HighlightCard(int idx)
    {
        cards[idx].position = originalWorldPositions[idx] + new Vector3(0, highlightLiftY, 0);
        cards[idx].localScale = Vector3.one * 1.06f;
        overlays[idx].color = overlayHighlight;
    }

    IEnumerator LockInWinner(int winnerIdx)
    {
        for (int i = 0; i < cards.Length; i++)
        {
            if (i == winnerIdx)
            {
                cards[i].position = originalWorldPositions[i] + new Vector3(0, winnerLiftY, 0);
                cards[i].localScale = Vector3.one * 1.1f;
            }
            else
            {
                cards[i].position = originalWorldPositions[i];
                cards[i].localScale = Vector3.one * 0.92f;
                overlays[i].color = new Color(0f, 0f, 0f, loserAlpha);
            }
        }

        yield return new WaitForSeconds(0.3f);

        float elapsed = 0f;
        Color startColor = overlays[winnerIdx].color;
        Color endColor = new Color(0f, 0f, 0f, 0f);

        while (elapsed < overlayFadeDuration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / overlayFadeDuration);
            overlays[winnerIdx].color = Color.Lerp(startColor, endColor, k);
            yield return null;
        }

        overlays[winnerIdx].color = endColor;
    }

    IEnumerator MoveWinnerToCenter(int winnerIdx)
    {
        yield return new WaitForSeconds(pauseBeforeSwap);

        int centerIdx = 1;

        if (winnerIdx == centerIdx)
        {
            yield return StartCoroutine(ScaleCard(cards[winnerIdx], cards[winnerIdx].localScale, Vector3.one * finalScale, scaleUpDuration));
            yield break;
        }

        Vector3 winnerStart = cards[winnerIdx].position;
        Vector3 winnerTarget = originalWorldPositions[centerIdx];
        Vector3 centerStart = cards[centerIdx].position;
        Vector3 centerTarget = originalWorldPositions[winnerIdx];

        Vector3 winnerScaleStart = cards[winnerIdx].localScale;
        Vector3 winnerScaleEnd = Vector3.one;

        float elapsed = 0f;
        while (elapsed < swapDuration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / swapDuration);
            float eased = k * k * (3f - 2f * k);

            cards[winnerIdx].position = Vector3.Lerp(winnerStart, winnerTarget, eased);
            cards[centerIdx].position = Vector3.Lerp(centerStart, centerTarget, eased);

            cards[winnerIdx].localScale = Vector3.Lerp(winnerScaleStart, winnerScaleEnd, eased);

            yield return null;
        }

        cards[winnerIdx].position = winnerTarget;
        cards[centerIdx].position = centerTarget;

        yield return StartCoroutine(ScaleCard(cards[winnerIdx], cards[winnerIdx].localScale, Vector3.one * finalScale, scaleUpDuration));
    }

    IEnumerator ScaleCard(RectTransform card, Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            card.localScale = Vector3.Lerp(from, to, eased);
            yield return null;
        }
        card.localScale = to;
    }

    IEnumerator ShowContinueButton()
    {
        if (continueButton == null) yield break;

        yield return new WaitForSeconds(pauseBeforeContinue);

        continueButton.gameObject.SetActive(true);

        CanvasGroup cg = continueButton.GetComponent<CanvasGroup>();
        if (cg == null) cg = continueButton.gameObject.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        float elapsed = 0f;
        while (elapsed < continueButtonFadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / continueButtonFadeDuration);
            yield return null;
        }

        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
    }

    // ============ HELPERS DE AUDIO ============

    void PlaySfx(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip, sfxVolume);
        }
    }

    void PlayLoop(AudioClip clip)
    {
        if (loopSource != null && clip != null)
        {
            loopSource.clip = clip;
            loopSource.volume = loopVolume;
            loopSource.loop = true;
            loopSource.Play();
        }
    }

    void StopLoop()
    {
        if (loopSource != null && loopSource.isPlaying)
        {
            loopSource.Stop();
        }
    }

    // ============ HELPER DE FADE ============

    IEnumerator FadeOverlay(float fromAlpha, float toAlpha, float duration, bool keepActiveAtEnd)
    {
        if (fadeOverlay == null) yield break;

        fadeOverlay.gameObject.SetActive(true);
        Color c = fadeOverlay.color;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            c.a = Mathf.Lerp(fromAlpha, toAlpha, k);
            fadeOverlay.color = c;
            yield return null;
        }

        c.a = toAlpha;
        fadeOverlay.color = c;

        // si terminamos en alpha 0, lo desactivamos para que no bloquee clicks
        if (!keepActiveAtEnd && toAlpha == 0f)
        {
            fadeOverlay.gameObject.SetActive(false);
        }
    }
}