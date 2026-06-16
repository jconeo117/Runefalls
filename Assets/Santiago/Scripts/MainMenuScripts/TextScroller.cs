using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;


/// <summary>
/// TextScroller
/// Sube una hoja (RawImage) hasta el centro del canvas y habilita hover + click.
/// Al hacer click izquierdo, hace fade a negro y carga una escena.
///
/// Audio: adjuntar A_TextScrollerAudio al mismo GameObject para configurar los sonidos.
/// </summary>
public class TextScroller : MonoBehaviour
{
    [Header("La hoja (RawImage) que sube")]
    public GameObject Lines;


    [Header("Velocidad de subida")]
    public float speed = 300f;


    [Header("Dónde se frena en Y (0 = centro del canvas si está anclada al centro)")]
    public float centerY = 0f;


    [Header("Hover (solo después de llegar al medio)")]
    public float hoverScale = 1.1f;
    public float scaleLerpSpeed = 10f;


    [Header("Escena a cargar al hacer click")]
    public string sceneToLoad = "";


    [Header("Fade a negro antes de cambiar de escena")]
    public Image fadeOverlay;
    public float fadeDuration = 0.88f;


    // ── Privado ───────────────────────────────────────────────────────────────
    private RectTransform _rect;
    private Camera _canvasCam;
    private bool _arrived = false;
    private bool _loading = false;
    private Vector3 _baseScale;
    private bool _wasMouseOver = false;
    private A_TextScrollerAudio _audio;   // opcional; si no está en el GO, no suena


    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Start()
    {
        if (Lines == null) return;


        _rect = Lines.GetComponent<RectTransform>();
        if (_rect == null) return;


        _baseScale = _rect.localScale;


        Canvas canvas = Lines.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            _canvasCam = canvas.worldCamera;


        _audio = GetComponent<A_TextScrollerAudio>();
    }


    private void Update()
    {
        if (_rect == null) return;


        // Sube hasta el centro
        if (!_arrived)
        {
            Vector2 p = _rect.anchoredPosition;
            p.y += speed * Time.deltaTime;


            if (p.y >= centerY)
            {
                p.y = centerY;
                _arrived = true;
            }


            _rect.anchoredPosition = p;
            return;
        }


        if (_loading) return;


        // Hover + click
        bool mouseOver = RectTransformUtility.RectangleContainsScreenPoint(
            _rect, Input.mousePosition, _canvasCam);


        // Flanco de entrada: solo la primera vez que entra el mouse
        if (mouseOver && !_wasMouseOver)
            _audio?.OnHover();


        _wasMouseOver = mouseOver;


        // Escala suave
        Vector3 targetScale = mouseOver ? _baseScale * hoverScale : _baseScale;
        _rect.localScale = Vector3.Lerp(_rect.localScale, targetScale, scaleLerpSpeed * Time.deltaTime);


        // Click izquierdo
        if (mouseOver && Input.GetMouseButtonDown(0))
        {
            _audio?.OnClick();


            if (!string.IsNullOrEmpty(sceneToLoad))
                StartCoroutine(FadeAndLoad());
            else
                Debug.LogWarning("TextScroller: no asignaste la escena a cargar.");
        }
    }


    // ── Fade y carga ──────────────────────────────────────────────────────────
    private IEnumerator FadeAndLoad()
    {
        _loading = true;


        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);


            Color c = fadeOverlay.color;
            c.a = 0f;
            fadeOverlay.color = c;


            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Clamp01(elapsed / fadeDuration);
                fadeOverlay.color = c;
                yield return null;
            }


            c.a = 1f;
            fadeOverlay.color = c;
        }


        SceneManager.LoadScene(sceneToLoad);
    }
}
