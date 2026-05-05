using UnityEngine;
using UnityEngine.UI;

public class CrosshairSystem : MonoBehaviour
{
    public static CrosshairSystem Instance { get; private set; }

    [Header("Crosshair Images")]
    [SerializeField] private Image crosshairDefault;
    [SerializeField] private Image crosshairInteract;

    [Header("Transition")]
    [SerializeField] private float fadeSpeed = 10f;

    [Header("Cursor")]
    [SerializeField] private bool hideSystemCursor = true;

    [Header("Screen Clamp")]
    [Tooltip("Margen en píxeles desde el borde de la pantalla")]
    [SerializeField] private float screenMargin = 0f;

    private RectTransform _defaultRect;
    private RectTransform _interactRect;
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private bool _isInteractMode;
    private float _interactAlpha;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (crosshairDefault == null || crosshairInteract == null)
        {
            Debug.LogError("[CrosshairSystem] Faltan referencias de Image en el Inspector.", this);
            return;
        }

        _defaultRect = crosshairDefault.GetComponent<RectTransform>();
        _interactRect = crosshairInteract.GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _canvasRect = _canvas?.GetComponent<RectTransform>();

        if (_canvas == null)
            Debug.LogError("[CrosshairSystem] No se encontró Canvas padre.", this);

        if (hideSystemCursor)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.None;
        }

        crosshairDefault.color = crosshairDefault.color.WithAlpha(1f);
        crosshairInteract.color = crosshairInteract.color.WithAlpha(0f);
        _interactAlpha = 0f;
    }

    private void Update()
    {
        MoveCrosshairsToMouse();
        AnimateFade();
    }

    private void OnDestroy()
    {
        if (hideSystemCursor)
            Cursor.visible = true;
    }

    // ─── Public API ──────────────────────────────────────────────

    public void SetInteractMode(bool interactable) => _isInteractMode = interactable;

    public void ShowSystemCursor(bool show)
    {
        Cursor.visible = show;
        crosshairDefault.enabled = !show;
        crosshairInteract.enabled = !show;
    }

    // ─── Private ─────────────────────────────────────────────────

    private void MoveCrosshairsToMouse()
    {
        if (_canvas == null || _canvasRect == null) return;

        // Convertir posición del mouse a espacio local del canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect,
            Input.mousePosition,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
            out Vector2 localPoint
        );

        // Límites del canvas en espacio local
        // El canvas tiene pivot (0.5, 0.5) por defecto, así que va de -halfW a +halfW
        Vector2 canvasSize = _canvasRect.rect.size;
        float halfW = canvasSize.x * 0.5f - screenMargin;
        float halfH = canvasSize.y * 0.5f - screenMargin;

        // Clampear para que no salga de los bordes
        localPoint.x = Mathf.Clamp(localPoint.x, -halfW, halfW);
        localPoint.y = Mathf.Clamp(localPoint.y, -halfH, halfH);

        _defaultRect.localPosition = localPoint;
        _interactRect.localPosition = localPoint;
    }

    private void AnimateFade()
    {
        float target = _isInteractMode ? 1f : 0f;
        _interactAlpha = Mathf.Lerp(_interactAlpha, target, Time.unscaledDeltaTime * fadeSpeed);

        crosshairDefault.color = crosshairDefault.color.WithAlpha(1f - _interactAlpha);
        crosshairInteract.color = crosshairInteract.color.WithAlpha(_interactAlpha);
    }
}