using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TextScroller : MonoBehaviour
{
    [Header("La hoja (RawImage) que sube")]
    public GameObject Lines;

    [Header("Velocidad de subida")]
    public float speed = 300f;

    [Header("Dónde se frena en Y (0 = centro del canvas si está anclada al centro)")]
    public float centerY = 0f;

    [Header("Hover (solo después de llegar al medio)")]
    public float hoverScale = 1.1f;        // cuánto se agranda al pasar el mouse
    public float scaleLerpSpeed = 10f;     // qué tan rápido lerpea el tamaño

    [Header("Escena a cargar al hacer click")]
    public string sceneToLoad = "";

    [Header("Fade a negro antes de cambiar de escena")]
    public Image fadeOverlay;              // Image negro que cubre toda la pantalla (empieza transparente)
    public float fadeDuration = 0.88f;

    private RectTransform rect;
    private Camera canvasCam;
    private bool arrived = false;          // true cuando ya se ancló en el medio
    private bool loading = false;          // true cuando ya se tocó (evita doble click)
    private Vector3 baseScale;

    void Start()
    {
        if (Lines == null) return;

        rect = Lines.GetComponent<RectTransform>();
        if (rect == null) return;

        baseScale = rect.localScale;

        // si el canvas NO es Screen Space - Overlay, necesitamos su cámara para detectar el mouse
        Canvas canvas = Lines.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            canvasCam = canvas.worldCamera;
    }

    void Update()
    {
        if (rect == null) return;

        // ----- sube hasta el medio -----
        if (!arrived)
        {
            Vector2 p = rect.anchoredPosition;
            p.y += speed * Time.deltaTime;

            if (p.y >= centerY)
            {
                p.y = centerY;
                arrived = true;   // a partir de acá se habilita el hover y el click
            }

            rect.anchoredPosition = p;
            return;
        }

        // si ya estamos cargando, no hacemos nada más
        if (loading) return;

        // ----- ya está anclada en el medio: hover + click -----

        bool mouseOver = RectTransformUtility.RectangleContainsScreenPoint(
            rect, Input.mousePosition, canvasCam);

        // se agranda suave si el mouse está encima, vuelve a su tamaño si no
        Vector3 targetScale = mouseOver ? baseScale * hoverScale : baseScale;
        rect.localScale = Vector3.Lerp(rect.localScale, targetScale, scaleLerpSpeed * Time.deltaTime);

        // click sobre la carta -> fade a negro y cambia de escena
        if (mouseOver && Input.GetMouseButtonDown(0))
        {
            if (!string.IsNullOrEmpty(sceneToLoad))
                StartCoroutine(FadeAndLoad());
            else
                Debug.LogWarning("TextScroller: no asignaste la escena a cargar.");
        }
    }

    IEnumerator FadeAndLoad()
    {
        loading = true;

        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);

            Color c = fadeOverlay.color;
            c.a = 0f;                         // arranca transparente
            fadeOverlay.color = c;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Clamp01(elapsed / fadeDuration);   // de 0 a 1 (se pone negro)
                fadeOverlay.color = c;
                yield return null;
            }

            c.a = 1f;
            fadeOverlay.color = c;
        }

        SceneManager.LoadScene(sceneToLoad);
    }
}