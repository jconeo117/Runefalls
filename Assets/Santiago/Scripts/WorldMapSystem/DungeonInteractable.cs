using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    [Header("Scene Transition")]
    [Tooltip("Name of the scene to load when entering this dungeon. Must be added in Build Settings.")]
    [SerializeField] private string dungeonSceneName = "";

    [Tooltip("Black image that covers the screen for the fade out. Must be a UI Image inside a Canvas.")]
    [SerializeField] private Image fadeOverlay;

    [Tooltip("How long the fade out takes before loading the scene")]
    [SerializeField] private float fadeOutDuration = 0.8f;

    [Header("Events")]
    [Tooltip("Fired when the player enters this dungeon (panel open)")]
    public UnityEvent onDungeonEnter;

    [Tooltip("Fired when the player is in hover range but not close enough")]
    public UnityEvent onOutOfRange;

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Private State

    private bool _isHovered;
    private bool _isLoadingScene;
    private static readonly int s_OutlineID = Shader.PropertyToID("_OutlineEnabled");

    // Cache the renderer for outline toggling (optional visual feedback)
    private Renderer _renderer;

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Properties

    public string DungeonName => dungeonName;
    public string DungeonDescription => dungeonDescription;
    public string DungeonSceneName => dungeonSceneName;
    public bool IsInRange => playerTransform != null &&
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

        // Asegurar que el overlay arranque transparente e invisible
        if (fadeOverlay != null)
        {
            Color c = fadeOverlay.color;
            c.a = 0f;
            fadeOverlay.color = c;
            fadeOverlay.gameObject.SetActive(false);
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
    #region Public API

    /// <summary>
    /// Loads the scene configured in the Inspector (dungeonSceneName) with a fade out.
    /// Can be called from UnityEvents (e.g. onDungeonEnter) or other scripts.
    /// </summary>
    public void LoadDungeonScene()
    {
        if (string.IsNullOrEmpty(dungeonSceneName))
        {
            Debug.LogWarning($"[DungeonInteractable] '{name}': No scene name configured in the Inspector.", this);
            return;
        }

        LoadSceneByName(dungeonSceneName);
    }

    /// <summary>
    /// Loads any scene by name with a fade out. Useful when you want to call it from a UnityEvent
    /// and pick the scene from the Inspector without changing the dungeonSceneName field.
    /// </summary>
    public void LoadSceneByName(string sceneName)
    {
        if (_isLoadingScene) return; // evita disparar dos veces

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning($"[DungeonInteractable] '{name}': Tried to load an empty scene name.", this);
            return;
        }

        _isLoadingScene = true;
        StartCoroutine(FadeOutAndLoad(sceneName));
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Scene Transition

    private IEnumerator FadeOutAndLoad(string sceneName)
    {
        // si no hay overlay asignado, cargamos directo
        if (fadeOverlay == null)
        {
            Debug.LogWarning($"[DungeonInteractable] '{name}': No fadeOverlay assigned, loading scene without fade.", this);
            SceneManager.LoadScene(sceneName);
            yield break;
        }

        fadeOverlay.gameObject.SetActive(true);
        fadeOverlay.raycastTarget = true; // bloquea clicks durante el fade

        Color c = fadeOverlay.color;
        float elapsed = 0f;
        float startAlpha = c.a;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(startAlpha, 1f, Mathf.Clamp01(elapsed / fadeOutDuration));
            fadeOverlay.color = c;
            yield return null;
        }

        c.a = 1f;
        fadeOverlay.color = c;

        SceneManager.LoadScene(sceneName);
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