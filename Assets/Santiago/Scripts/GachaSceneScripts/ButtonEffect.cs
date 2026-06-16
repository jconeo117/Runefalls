using UnityEngine;
using UnityEngine.EventSystems;


/// <summary>
/// ButtonEffect
/// Al pasar el mouse por encima del botón, se agranda un poco;
/// al sacar el mouse, vuelve a su tamaño de forma suave (lerp).
/// Al hacer click izquierdo, notifica al componente de audio.
///
/// Audio: adjuntar A_ButtonAudio al mismo GameObject para configurar los sonidos.
/// Adjuntar a: el mismo GameObject del botón.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    [Header("Objetivo (opcional)")]
    [Tooltip("Qué se agranda. Si lo dejás vacío, usa este mismo objeto (el botón).")]
    public RectTransform target;


    [Header("Escala")]
    [Tooltip("Cuánto se agranda al pasar el mouse. 1.1 = 10% más grande.")]
    public float hoverScale = 1.1f;


    [Tooltip("Qué tan rápido y suave interpola (mayor = más rápido).")]
    public float lerpSpeed = 12f;


    [Tooltip("Usar tiempo sin escala (sirve si el menú aparece con el juego en pausa / timeScale 0).")]
    public bool useUnscaledTime = true;


    // ── Privado ───────────────────────────────────────────────────────────────
    private Vector3 _baseScale;
    private Vector3 _targetScale;
    private A_ButtonAudio _audio; // opcional; si no está en el GO, simplemente no suena


    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (target == null) target = GetComponent<RectTransform>();
        _baseScale = target.localScale;
        _targetScale = _baseScale;


        _audio = GetComponent<A_ButtonAudio>(); // null si no está adjunto, y está bien
    }


    private void OnEnable()
    {
        _targetScale = _baseScale;
    }


    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        target.localScale = Vector3.Lerp(target.localScale, _targetScale, dt * lerpSpeed);
    }


    // ── Pointer events ────────────────────────────────────────────────────────
    public void OnPointerEnter(PointerEventData eventData)
    {
        _targetScale = _baseScale * hoverScale;
        _audio?.OnHover();
    }


    public void OnPointerExit(PointerEventData eventData)
    {
        _targetScale = _baseScale;
    }


    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        _audio?.OnClick();
    }


    private void OnDisable()
    {
        if (target != null) target.localScale = _baseScale;
        _targetScale = _baseScale;
    }
}
