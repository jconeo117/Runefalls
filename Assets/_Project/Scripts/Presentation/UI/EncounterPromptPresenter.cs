using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using Runefall.Combat;
using Runefall.Core;
using Runefall.Data;
using Runefall.Presentation.Combat;
using Runefall.Presentation.Dungeon;
using Runefall.Presentation.Player;

namespace Runefall.Presentation.UI
{
    /// <summary>
    /// Listens to EncounterReadyEvent, shows a confirmation panel with enemy info.
    /// "Enfrentar" raises OnEncounterAccepted (wire to SceneTransitionSystem in 6.2).
    /// Panel is built procedurally — no prefab needed.
    /// </summary>
    public class EncounterPromptPresenter : MonoBehaviour
    {
        [Header("Event Bus")]
        [SerializeField] private EncounterReadyEvent    _encounterReadyEvent;
        [SerializeField] private EncounterAcceptedEvent _onEncounterAccepted;

        [Header("Arena")]
        [SerializeField] private RoomRegistry           _roomRegistry;
        [SerializeField] private CombatArenaAssembler   _arenaAssembler;
        [SerializeField] private CombatBootstrapper     _combatBootstrapper;
        [SerializeField] private CombatTransitionScreen _transitionScreen;

        [Header("Party")]
        [Tooltip("Fallback party if ExplorationPlayer component not found on the player GO (1–3 assets).")]
        [SerializeField] private CharacterData[] _defaultParty;

        [Header("Player Systems")]
        [SerializeField] private PlayerController              _playerController;
        [SerializeField] private CursorController              _cursorController;
        [SerializeField] private CinemachineInputAxisController _cameraInput;

        [Header("Visual")]
        [SerializeField] private Color _panelBg       = new Color(0.06f, 0.06f, 0.10f, 0.92f);
        [SerializeField] private Color _accentColor   = new Color(0.80f, 0.30f, 0.20f, 1f);
        [SerializeField] private Color _buttonHover   = new Color(0.95f, 0.40f, 0.20f, 1f);

        // ── Runtime ──────────────────────────────────────────────────────────────

        private EncounterData _pending;
        private Canvas        _canvas;
        private Text          _nameLabel;
        private Text          _ccLabel;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void Awake()  => BuildPanel();

        private void OnEnable()  => _encounterReadyEvent?.Subscribe(OnEncounterReady);
        private void OnDisable() => _encounterReadyEvent?.Unsubscribe(OnEncounterReady);

        // ── Event handler ────────────────────────────────────────────────────────

        private void OnEncounterReady(EncounterData data)
        {
            _pending        = data;
            _nameLabel.text = data.enemyData != null ? data.enemyData.enemyName : "???";
            _ccLabel.text   = data.enemyData != null
                ? $"CC  {data.enemyData.combatClass:F0}"
                : "CC  —";

            SetBlocking(true);
            _canvas.gameObject.SetActive(true);
        }

        // ── Button callbacks ─────────────────────────────────────────────────────

        private void OnEnfrentar()
        {
            if (_pending == null) { ClosePanel(); return; }

            Vector3 confrontPoint = _pending.enemyTransform != null
                ? _pending.enemyTransform.position
                : Vector3.zero;

            var room        = _roomRegistry?.FindNearest(confrontPoint);
            var arenaCenter = room != null ? room.transform.position : confrontPoint;

            // Prefer party from the player GO itself; fall back to Inspector array.
            var explorationPlayer = _playerController != null
                ? _playerController.GetComponent<Runefall.Presentation.Player.ExplorationPlayer>()
                : null;
            var rawParty = explorationPlayer?.Party is { Length: > 0 }
                ? explorationPlayer.Party
                : _defaultParty;
            int n     = rawParty != null ? Mathf.Clamp(rawParty.Length, 0, 3) : 0;
            var party = n > 0 ? rawParty[..n] : System.Array.Empty<CharacterData>();

            ServiceLocator.Register<EncounterState>(new EncounterState
            {
                Encounter   = _pending,
                ArenaCenter = arenaCenter,
                PlayerParty = party
            });

            _onEncounterAccepted?.Raise(_pending);

            // Hide panel; player stays locked — coroutine owns the rest of the transition.
            _canvas.gameObject.SetActive(false);
            _pending = null;

            StartCoroutine(TransitionToCombat(room));
        }

        private System.Collections.IEnumerator TransitionToCombat(RoomVolume room)
        {
            // Fade to black — long enough to hide the swap.
            if (_transitionScreen != null)
                yield return StartCoroutine(_transitionScreen.FadeToBlack());

            // Init combat systems while screen is black.
            if (_arenaAssembler != null) _arenaAssembler.PendingRoom = room;
            if (_combatBootstrapper != null)
            {
                _combatBootstrapper.OverrideExplorationRefs(_playerController, _cameraInput);
                _combatBootstrapper.gameObject.SetActive(true);
            }

            // Safety frames: OnEnable + camera reposition must finish before reveal.
            yield return null;
            yield return null;

            // Reveal the arena.
            if (_transitionScreen != null)
                yield return StartCoroutine(_transitionScreen.FadeFromBlack());

            // Trigger intro sequencer (if wired) then start the turn loop.
            _combatBootstrapper?.BeginCombat();
        }

        private void OnDismiss()
        {
            ClosePanel();
        }

        private void ClosePanel()
        {
            _canvas.gameObject.SetActive(false);
            SetBlocking(false);
            _pending = null;
        }

        // ── Input blocking ────────────────────────────────────────────────────────

        private void SetBlocking(bool block)
        {
            // Cursor
            Cursor.lockState = block ? CursorLockMode.None   : CursorLockMode.Locked;
            Cursor.visible   = block;

            // Disable CursorController so Escape doesn't fight us while panel is open
            if (_cursorController != null) _cursorController.enabled = !block;

            // Stop player movement
            if (_playerController != null) _playerController.enabled = !block;

            // Stop camera orbit input
            if (_cameraInput != null) _cameraInput.enabled = !block;
        }

        // ── Procedural UI build ──────────────────────────────────────────────────

        private void BuildPanel()
        {
            // Root canvas — screen-space overlay, on top of everything
            var canvasGO = new GameObject("EncounterPromptCanvas");
            canvasGO.transform.SetParent(transform, false);
            _canvas            = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Darkened full-screen backdrop (blocks clicks behind panel)
            var backdrop = MakeImage(canvasGO.transform, "Backdrop",
                new Color(0f, 0f, 0f, 0.45f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backdrop.gameObject.AddComponent<Button>().onClick.AddListener(OnDismiss);

            // Centered panel — 320 × 220 px
            var panel = MakePanel(canvasGO.transform, new Vector2(320f, 220f));

            // Close [X] button — top-right corner
            MakeCloseButton(panel, OnDismiss);

            // Enemy name label
            _nameLabel = MakeLabel(panel, "EnemyName",
                anchorMin: new Vector2(0f, 0.62f), anchorMax: new Vector2(1f, 0.90f),
                fontSize: 22, style: FontStyle.Bold, color: Color.white);

            // CC label
            _ccLabel = MakeLabel(panel, "CCLabel",
                anchorMin: new Vector2(0f, 0.40f), anchorMax: new Vector2(1f, 0.62f),
                fontSize: 16, style: FontStyle.Normal, color: new Color(0.85f, 0.75f, 0.40f));

            // Divider line
            MakeImage(panel, "Divider", new Color(1f, 1f, 1f, 0.10f),
                new Vector2(0.05f, 0.36f), new Vector2(0.95f, 0.37f),
                Vector2.zero, Vector2.zero);

            // Enfrentar button
            MakeActionButton(panel, "ENFRENTAR", _accentColor, _buttonHover,
                new Vector2(0.10f, 0.06f), new Vector2(0.90f, 0.32f),
                OnEnfrentar);

            canvasGO.SetActive(false);
        }

        // ── Builder helpers ──────────────────────────────────────────────────────

        private Transform MakePanel(Transform parent, Vector2 size)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);

            var rt         = go.AddComponent<RectTransform>();
            rt.anchorMin   = new Vector2(0.5f, 0.5f);
            rt.anchorMax   = new Vector2(0.5f, 0.5f);
            rt.pivot       = new Vector2(0.5f, 0.5f);
            rt.sizeDelta   = size;
            rt.anchoredPosition = Vector2.zero;

            go.AddComponent<Image>().color = _panelBg;
            return go.transform;
        }

        private static Image MakeImage(Transform parent, string goName, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            var rt        = go.AddComponent<RectTransform>();
            rt.anchorMin  = anchorMin;
            rt.anchorMax  = anchorMax;
            rt.offsetMin  = offsetMin;
            rt.offsetMax  = offsetMax;
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Text MakeLabel(Transform parent, string goName,
            Vector2 anchorMin, Vector2 anchorMax,
            int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            var rt       = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(12f, 0f);
            rt.offsetMax = new Vector2(-12f, 0f);

            var txt           = go.AddComponent<Text>();
            txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize      = fontSize;
            txt.fontStyle     = style;
            txt.color         = color;
            txt.alignment     = TextAnchor.MiddleCenter;
            txt.text          = string.Empty;
            return txt;
        }

        private void MakeActionButton(Transform parent, string label,
            Color normalColor, Color highlightColor,
            Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);

            var rt       = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img   = go.AddComponent<Image>();
            img.color = normalColor;

            var btn = go.AddComponent<Button>();
            var colors          = btn.colors;
            colors.normalColor  = normalColor;
            colors.highlightedColor = highlightColor;
            colors.pressedColor = normalColor * 0.7f;
            btn.colors          = colors;
            btn.onClick.AddListener(onClick);

            var txtGO  = new GameObject("Label");
            txtGO.transform.SetParent(go.transform, false);
            var txtRT      = txtGO.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;
            var txt           = txtGO.AddComponent<Text>();
            txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text          = label;
            txt.fontSize      = 15;
            txt.fontStyle     = FontStyle.Bold;
            txt.color         = Color.white;
            txt.alignment     = TextAnchor.MiddleCenter;
        }

        private static void MakeCloseButton(Transform panel, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_Close");
            go.transform.SetParent(panel, false);

            var rt       = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(30f, 30f);
            rt.anchoredPosition = new Vector2(-4f, -4f);

            go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(go.transform, false);
            var txtRT      = txtGO.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;
            var txt       = txtGO.AddComponent<Text>();
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text      = "✕";
            txt.fontSize  = 14;
            txt.color     = new Color(1f, 1f, 1f, 0.6f);
            txt.alignment = TextAnchor.MiddleCenter;
        }
    }
}
