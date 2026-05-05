using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DungeonUIPanel : MonoBehaviour
{
    public static DungeonUIPanel Instance { get; private set; }

    [Header("Panel Root")]
    [SerializeField] private CanvasGroup panelGroup;

    [Header("Content")]
    [SerializeField] private TMP_Text dungeonNameText;
    [SerializeField] private TMP_Text dungeonDescriptionText;

    [Header("Buttons")]
    [SerializeField] private Button enterButton;
    [SerializeField] private Button cancelButton;

    [Header("Animation")]
    [SerializeField] private float animSpeed = 8f;
    [SerializeField] private float scaleFrom = 0.88f;

    [Header("Game Pause")]
    [SerializeField] private bool pauseTimeScale = false;

    private DungeonInteractable _activeDungeon;
    private RectTransform _rectTransform;
    private bool _isOpen;
    private float _targetAlpha;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _rectTransform = panelGroup.GetComponent<RectTransform>();
        enterButton?.onClick.AddListener(OnEnterClicked);
        cancelButton?.onClick.AddListener(Close);

        _targetAlpha = 0f;
        panelGroup.alpha = 0f;
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;

        if (_rectTransform != null)
            _rectTransform.localScale = Vector3.one * scaleFrom;
    }

    private void Update()
    {
        // Animar alpha
        panelGroup.alpha = Mathf.Lerp(panelGroup.alpha, _targetAlpha, Time.unscaledDeltaTime * animSpeed);

        // Animar scale
        if (_rectTransform != null)
        {
            float targetScale = _isOpen ? 1f : scaleFrom;
            _rectTransform.localScale = Vector3.Lerp(
                _rectTransform.localScale,
                Vector3.one * targetScale,
                Time.unscaledDeltaTime * animSpeed
            );
        }

        // Escape para cerrar
        if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    public void Open(DungeonInteractable dungeon)
    {
        gameObject.SetActive(true);

        _activeDungeon = dungeon;

        if (dungeonNameText) dungeonNameText.text = dungeon.DungeonName;
        if (dungeonDescriptionText) dungeonDescriptionText.text = dungeon.DungeonDescription;

        _isOpen = true;
        _targetAlpha = 1f;
        panelGroup.interactable = true;
        panelGroup.blocksRaycasts = true;

        if (pauseTimeScale) Time.timeScale = 0f;
        CrosshairSystem.Instance?.ShowSystemCursor(true);
    }

    public void Close()
    {
        _isOpen = false;
        _targetAlpha = 0f;

        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;

        if (pauseTimeScale) Time.timeScale = 1f;
        CrosshairSystem.Instance?.ShowSystemCursor(false);
        CrosshairSystem.Instance?.SetInteractMode(false);
    }

    private void OnEnterClicked()
    {
        _activeDungeon?.onDungeonEnter?.Invoke();
        Close();
    }
}