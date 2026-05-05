using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach this to the Dungeon GameObject.
/// Handles hover detection, proximity check, and click → panel open.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DungeonInteractable : MonoBehaviour, IInteractable
{
    // ─────────────────────────────────────────────────────────────
    #region Inspector Fields

    [Header("Interaction Settings")]
    [Tooltip("Maximum distance from the player to allow entry")]
    [SerializeField] private float interactionRange = 5f;

    [Tooltip("Reference to the player Transform (auto-assigned if left empty)")]
    [SerializeField] private Transform playerTransform;

    [Header("Dungeon Data")]
    [SerializeField] private string dungeonName = "Ancient Dungeon";

    [TextArea(2, 4)]
    [SerializeField] private string dungeonDescription = "A long-forgotten dungeon of unspeakable terror.";

    [Header("Events")]
    [Tooltip("Fired when the player enters this dungeon (panel open)")]
    public UnityEvent onDungeonEnter;

    [Tooltip("Fired when the player is in hover range but not close enough")]
    public UnityEvent onOutOfRange;

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Private State

    private bool _isHovered;
    private static readonly int s_OutlineID = Shader.PropertyToID("_OutlineEnabled");

    // Cache the renderer for outline toggling (optional visual feedback)
    private Renderer _renderer;

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Properties

    public string DungeonName        => dungeonName;
    public string DungeonDescription => dungeonDescription;
    public bool   IsInRange          => playerTransform != null &&
                                        Vector3.Distance(transform.position, playerTransform.position) <= interactionRange;

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();

        // Auto-find player if not assigned
        if (playerTransform == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
                playerTransform = go.transform;
            else
                Debug.LogWarning($"[DungeonInteractable] '{name}': No Player tag found. Assign playerTransform manually.", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Visualise interaction range in Scene view
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.DrawSphere(transform.position, interactionRange);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region IInteractable

    public void OnHoverEnter()
    {
        _isHovered = true;
        SetOutline(true);
        CrosshairSystem.Instance?.SetInteractMode(true);
        WorldTooltipSystem.Instance?.ShowTooltip(dungeonName, IsInRange ? "Click to enter" : "Get closer", transform.position);
    }

    public void OnHoverStay()
    {
        // Update crosshair in case player walks in/out of range while hovering
        CrosshairSystem.Instance?.SetInteractMode(IsInRange);
        WorldTooltipSystem.Instance?.UpdatePosition(transform.position);
    }

    public void OnHoverExit()
    {
        _isHovered = false;
        SetOutline(false);
        CrosshairSystem.Instance?.SetInteractMode(false);
        WorldTooltipSystem.Instance?.HideTooltip();
    }

    public void OnInteract()
    {
        if (!IsInRange)
        {
            // Solo mostrar mensaje, nada más
            WorldTooltipSystem.Instance?.FlashMessage("Too far away!");
            return; // <-- corta acá, no toca nada del panel
        }

        DungeonUIPanel.Instance?.Open(this);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Private Helpers

    private void SetOutline(bool enabled)
    {
        if (_renderer == null) return;

        // Works with any shader that exposes "_OutlineEnabled"
        // Replace with your own outline solution (Quick Outline, Highlight Plus, etc.)
        _renderer.material.SetFloat(s_OutlineID, enabled ? 1f : 0f);
    }

    #endregion
}
