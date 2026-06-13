using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// ButtonEffect
/// Al pasar el mouse por encima del botón, se agranda un poco;
/// al sacar el mouse, vuelve a su tamaño de forma suave (lerp).
///
/// Adjuntar a: el mismo GameObject del botón.
/// Requiere: un EventSystem en la escena (se crea solo al hacer un Canvas)
/// y que el Graphic del botón tenga "Raycast Target" activado (viene activado por defecto).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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

    private Vector3 _baseScale;
    private Vector3 _targetScale;

    private void Awake()
    {
        if (target == null) target = GetComponent<RectTransform>();
        _baseScale = target.localScale;
        _targetScale = _baseScale;
    }

    private void OnEnable()
    {
        // Por si el botón se reactiva, arrancar siempre desde la escala base
        _targetScale = _baseScale;
    }

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        target.localScale = Vector3.Lerp(target.localScale, _targetScale, dt * lerpSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _targetScale = _baseScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _targetScale = _baseScale;
    }

    private void OnDisable()
    {
        // Evitar que quede "agrandado" si se desactiva mientras el mouse estaba encima
        if (target != null) target.localScale = _baseScale;
        _targetScale = _baseScale;
    }
}