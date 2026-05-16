using UnityEngine;

/// <summary>
/// Casts a ray from the camera through the mouse position each frame.
/// Detects IInteractable objects and dispatches hover / click events.
///
/// Designed for isometric cameras (orthographic or perspective).
/// Attach to the Player or a dedicated GameManager GameObject.
/// </summary>
public class PlayerInteractionRaycaster : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    #region Inspector Fields

    [Header("Raycast Settings")]
    [Tooltip("Camera used for ray projection (uses Camera.main if left empty)")]
    [SerializeField] private Camera gameCamera;

    [Tooltip("Maximum raycast distance")]
    [SerializeField] private float maxRayDistance = 200f;

    [Tooltip("LayerMask for interactable objects (set to your Dungeon / POI layer)")]
    [SerializeField] private LayerMask interactableLayer = ~0;

    [Header("Input")]
    [Tooltip("Mouse button index: 0 = Left, 1 = Right, 2 = Middle")]
    [SerializeField] private int interactMouseButton = 0;

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Private State

    private IInteractable _currentHover;
    private Camera        _cam;

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        _cam = gameCamera != null ? gameCamera : Camera.main;

        if (_cam == null)
            Debug.LogError("[PlayerInteractionRaycaster] No camera found. Assign one in the Inspector.", this);
    }

    private void Update()
    {
        if (_cam == null) return;

        ProcessHover();

        if (Input.GetMouseButtonDown(interactMouseButton))
            ProcessClick();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Private Logic

    private void ProcessHover()
    {
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, interactableLayer);

        if (hitSomething && hit.collider.TryGetComponent(out IInteractable interactable))
        {
            if (_currentHover != interactable)
            {
                // Transition: exit old, enter new
                _currentHover?.OnHoverExit();
                _currentHover = interactable;
                _currentHover.OnHoverEnter();
            }
            else
            {
                _currentHover.OnHoverStay();
            }
        }
        else
        {
            // Moved off any interactable
            if (_currentHover != null)
            {
                _currentHover.OnHoverExit();
                _currentHover = null;
            }
        }
    }

    private void ProcessClick()
    {
        _currentHover?.OnInteract();
    }

    #endregion
}
