using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays a world-space tooltip that follows the mouse pointer.
/// Shows the dungeon/location name and a short action hint.
/// Also supports a temporary flash message ("Too far away!" etc.)
///
/// Attach to a Canvas child containing a panel with two TMP_Text fields.
/// </summary>
public class WorldTooltipSystem : MonoBehaviour
{
    public static WorldTooltipSystem Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────
    #region Inspector Fields

    [Header("References")]
    [SerializeField] private CanvasGroup tooltipGroup;
    [SerializeField] private RectTransform tooltipRect;
    [SerializeField] private TMP_Text     titleText;
    [SerializeField] private TMP_Text     hintText;

    [Header("Layout")]
    [Tooltip("Pixel offset from the cursor position")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(18f, -18f);

    [Header("Animation")]
    [SerializeField] private float fadeSpeed     = 10f;
    [SerializeField] private float flashDuration = 1.5f;

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Private State

    private Camera      _cam;
    private bool        _visible;
    private float       _targetAlpha;
    private Vector3     _worldTarget;
    private Coroutine   _flashCoroutine;

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _cam = Camera.main;
        tooltipGroup.alpha          = 0f;
        tooltipGroup.interactable   = false;
        tooltipGroup.blocksRaycasts = false;
    }

    private void LateUpdate()
    {
        // Smooth alpha
        tooltipGroup.alpha = Mathf.Lerp(tooltipGroup.alpha, _targetAlpha, Time.unscaledDeltaTime * fadeSpeed);

        // Follow mouse in screen-space
        if (_visible && tooltipRect != null)
        {
            Vector2 screenPos = Input.mousePosition;
            screenPos += cursorOffset;
            tooltipRect.position = screenPos;
            ClampToScreen();
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Public API

    public void ShowTooltip(string title, string hint, Vector3 worldPosition)
    {
        titleText.text = title;
        hintText.text  = hint;
        _worldTarget   = worldPosition;
        _visible       = true;
        _targetAlpha   = 1f;
    }

    public void UpdatePosition(Vector3 worldPosition) => _worldTarget = worldPosition;

    public void HideTooltip()
    {
        _visible     = false;
        _targetAlpha = 0f;
    }

    public void FlashMessage(string message)
    {
        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(FlashRoutine(message));
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Private

    private void ClampToScreen()
    {
        if (tooltipRect == null) return;

        var corners = new Vector3[4];
        tooltipRect.GetWorldCorners(corners);

        float   screenW  = Screen.width;
        float   screenH  = Screen.height;
        Vector3 pos      = tooltipRect.position;

        float minX = corners[0].x;
        float maxX = corners[2].x;
        float minY = corners[0].y;
        float maxY = corners[1].y;

        if (maxX > screenW) pos.x -= (maxX - screenW);
        if (minX < 0)       pos.x -= minX;
        if (maxY > screenH) pos.y -= (maxY - screenH);
        if (minY < 0)       pos.y -= minY;

        tooltipRect.position = pos;
    }

    private IEnumerator FlashRoutine(string message)
    {
        string prevHint = hintText.text;
        hintText.text   = message;
        _targetAlpha    = 1f;
        _visible        = true;

        yield return new WaitForSecondsRealtime(flashDuration);

        hintText.text = prevHint;
        if (!_visible) _targetAlpha = 0f;
    }

    #endregion
}
