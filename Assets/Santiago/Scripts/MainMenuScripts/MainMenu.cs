using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string nombreEscena = "Game";

    [Header("Fade")]
    [SerializeField] private Image fadeOverlay;          // Image negro que cubre toda la pantalla
    [SerializeField] private float fadeDuration = 0.88f;

    [Header("RawImage con hover + click (estilo botón)")]
    [SerializeField] private RawImage hoverImage;        // la RawImage que se agranda y al clickear cambia de escena
    [SerializeField] private float hoverScale = 1.1f;    // cuánto se agranda al pasar el mouse
    [SerializeField] private float scaleLerpSpeed = 10f; // qué tan rápido lerpea el tamaño

    [Header("Sonido de click")]
    [SerializeField] private AudioSource sfxSource;      // AudioSource para reproducir el click
    [SerializeField] private AudioClip clickSound;       // el sonidito al tocar jugar
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.8f;

    private RectTransform hoverRect;
    private Camera canvasCam;
    private Vector3 hoverBaseScale;
    private bool loading = false;                         // evita doble click durante el fade

    void Start()
    {
        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            // fade de entrada: arranca en negro (1) y se va a transparente (0)
            StartCoroutine(Fade(1f, 0f, false));
        }

        // preparamos la Image que tiene hover/click
        if (hoverImage != null)
        {
            hoverRect = hoverImage.rectTransform;
            hoverBaseScale = hoverRect.localScale;

            // si el canvas NO es Screen Space - Overlay, necesitamos su cámara para detectar el mouse
            Canvas canvas = hoverImage.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                canvasCam = canvas.worldCamera;
        }
    }

    void Update()
    {
        if (hoverRect == null || loading) return;

        bool mouseOver = RectTransformUtility.RectangleContainsScreenPoint(
            hoverRect, Input.mousePosition, canvasCam);

        // se agranda suave si el mouse está encima, vuelve a su tamaño si no
        Vector3 targetScale = mouseOver ? hoverBaseScale * hoverScale : hoverBaseScale;
        hoverRect.localScale = Vector3.Lerp(hoverRect.localScale, targetScale, scaleLerpSpeed * Time.deltaTime);

        // click sobre la Image -> misma lógica del fade + cambio de escena
        if (mouseOver && Input.GetMouseButtonDown(0))
            StartGame();
    }

    public void StartGame()
    {
        if (loading) return;
        loading = true;

        // 🔊 sonidito al tocar jugar
        if (sfxSource != null && clickSound != null)
            sfxSource.PlayOneShot(clickSound, sfxVolume);

        StartCoroutine(FadeAndLoadScene());
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    IEnumerator FadeAndLoadScene()
    {
        // fade de salida: de transparente (0) a negro (1) antes de cambiar
        yield return StartCoroutine(Fade(0f, 1f, true));
        SceneManager.LoadScene(nombreEscena);
    }

    IEnumerator Fade(float from, float to, bool keepBlackAtEnd)
    {
        if (fadeOverlay == null) yield break;

        fadeOverlay.gameObject.SetActive(true);
        Color c = fadeOverlay.color;
        c.a = from;
        fadeOverlay.color = c;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
            fadeOverlay.color = c;
            yield return null;
        }

        c.a = to;
        fadeOverlay.color = c;

        // si terminó transparente, lo apagamos para que no bloquee los botones del menú
        if (!keepBlackAtEnd && to == 0f)
            fadeOverlay.gameObject.SetActive(false);
    }
}