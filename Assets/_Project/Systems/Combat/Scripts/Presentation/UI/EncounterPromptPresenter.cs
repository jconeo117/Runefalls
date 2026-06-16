using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using Runefall.Combat;
using Runefall.Core;
using Runefall.Data;
using Runefall.Presentation.Combat;
using Runefall.Presentation.Dungeon;
using Runefall.Presentation.Player;
using Runefall.Presentation.Enemies;

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

        [Header("Poster Buttons (shared sprites)")]
        [Tooltip("Sprite del botón ENFRENTAR (estado normal) para enemigos regulares.")]
        [SerializeField] private Sprite _enfrentarNormal;
        [Tooltip("Sprite del botón ENFRENTAR iluminado (hover/pressed) para enemigos regulares.")]
        [SerializeField] private Sprite _enfrentarHover;
        [Tooltip("Sprite del botón ENFRENTAR (estado normal) para jefes (BossEnemyData).")]
        [SerializeField] private Sprite _enfrentarBossNormal;
        [Tooltip("Sprite del botón ENFRENTAR iluminado (hover/pressed) para jefes.")]
        [SerializeField] private Sprite _enfrentarBossHover;

        [Header("Poster Layout")]
        [Tooltip("Altura objetivo del cartel en px. El ancho se calcula preservando el aspecto del sprite.")]
        [SerializeField] private float   _posterHeight  = 360f;
        [Tooltip("Posición normalizada del botón ENFRENTAR dentro del cartel (0-1).")]
        [SerializeField] private Vector2 _enfrentarPos  = new Vector2(0.5f, 0.24f);
        [Tooltip("Tamaño del botón ENFRENTAR en px.")]
        [SerializeField] private Vector2 _enfrentarSize = new Vector2(200f, 64f);
        [Tooltip("Posición normalizada del texto Combat Class dentro del cartel (0-1).")]
        [SerializeField] private Vector2 _ccPos         = new Vector2(0.5f, 0.46f);
        [SerializeField] private int     _ccFontSize    = 20;
        [Tooltip("Color del texto CC — tinta oscura para leerse sobre el pergamino.")]
        [SerializeField] private Color   _ccColor       = new Color(0.18f, 0.10f, 0.04f, 1f);
        [SerializeField] private Color   _backdropColor = new Color(0f, 0f, 0f, 0.55f);

        // ── Runtime ──────────────────────────────────────────────────────────────

        private EncounterData _pending;
        private Canvas        _canvas;
        private Image         _posterImage;
        private RectTransform _posterRect;
        private Image         _enfrentarImage;
        private Button        _enfrentarButton;
        private Text          _ccLabel;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void Awake()  => BuildPanel();

        private void OnEnable()  => _encounterReadyEvent?.Subscribe(OnEncounterReady);
        private void OnDisable() => _encounterReadyEvent?.Unsubscribe(OnEncounterReady);

        // ── Event handler ────────────────────────────────────────────────────────

        private void OnEncounterReady(EncounterData data)
        {
            _pending = data;
            var enemy = data.enemyData;
            bool isBoss = enemy is BossEnemyData;

            // Poster — custom per enemy (name baked into the art). Size to target height, keep aspect.
            var poster = enemy != null ? enemy.preCombatPoster : null;
            if (poster != null)
            {
                _posterImage.enabled = true;
                _posterImage.sprite  = poster;
                float aspect = poster.rect.height > 0f ? poster.rect.width / poster.rect.height : 1f;
                _posterRect.sizeDelta = new Vector2(_posterHeight * aspect, _posterHeight);
            }
            else
            {
                _posterImage.enabled = false;
                Debug.LogWarning($"[EncounterPrompt] '{enemy?.enemyName}' has no preCombatPoster assigned.", this);
            }

            // Combat Class over the parchment.
            _ccLabel.text = enemy != null ? $"CC  {enemy.combatClass:F0}" : "CC  —";

            // Enfrentar button — swap sprite set for boss vs regular.
            var normal = isBoss ? _enfrentarBossNormal : _enfrentarNormal;
            var hover  = isBoss ? _enfrentarBossHover  : _enfrentarHover;
            if (normal != null) _enfrentarImage.sprite = normal;
            var ss = _enfrentarButton.spriteState;
            ss.highlightedSprite = hover;
            ss.pressedSprite     = hover;
            ss.selectedSprite    = normal;
            _enfrentarButton.spriteState = ss;

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
            if (_pending != null && _pending.enemyTransform != null)
            {
                var enemyController = _pending.enemyTransform.GetComponent<EnemyController>();
                if (enemyController != null)
                {
                    enemyController.CoolDownAndResume(3f); // 3 seconds cooldown to allow player to move away
                }
            }

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

            // Darkened full-screen backdrop (blocks clicks behind panel; click empty area = dismiss)
            var backdrop = MakeImage(canvasGO.transform, "Backdrop",
                _backdropColor,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backdrop.gameObject.AddComponent<Button>().onClick.AddListener(OnDismiss);

            // Poster — custom per-enemy art, centered. Size + sprite set in OnEncounterReady.
            var posterGO = new GameObject("Poster");
            posterGO.transform.SetParent(canvasGO.transform, false);
            _posterRect            = posterGO.AddComponent<RectTransform>();
            _posterRect.anchorMin  = new Vector2(0.5f, 0.5f);
            _posterRect.anchorMax  = new Vector2(0.5f, 0.5f);
            _posterRect.pivot      = new Vector2(0.5f, 0.5f);
            _posterRect.sizeDelta  = new Vector2(_posterHeight, _posterHeight);
            _posterRect.anchoredPosition = Vector2.zero;
            _posterRect.localScale = Vector3.one * 1.65f;
            _posterImage           = posterGO.AddComponent<Image>();
            _posterImage.preserveAspect = true;
            _posterImage.raycastTarget  = true; // absorb clicks over the poster (don't dismiss)

            // Close [X] button — top-right of the poster
            MakeCloseButton(_posterRect, OnDismiss);

            // Combat Class — over the parchment (poster already carries the name)
            _ccLabel = MakeLabel(_posterRect, "CCLabel",
                anchorMin: _ccPos, anchorMax: _ccPos,
                fontSize: _ccFontSize, style: FontStyle.Bold, color: _ccColor);
            var ccRT = _ccLabel.rectTransform;
            ccRT.pivot     = new Vector2(0.5f, 0.5f);
            ccRT.sizeDelta = new Vector2(240f, 40f);

            // Enfrentar button — sprite-swap (normal/iluminado), set per encounter
            BuildEnfrentarButton(_posterRect);

            canvasGO.SetActive(false);
        }

        private void BuildEnfrentarButton(Transform parent)
        {
            var go = new GameObject("Btn_Enfrentar");
            go.transform.SetParent(parent, false);

            var rt       = go.AddComponent<RectTransform>();
            rt.anchorMin = _enfrentarPos;
            rt.anchorMax = _enfrentarPos;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = _enfrentarSize;
            rt.anchoredPosition = new Vector2(0f, 45f);
            rt.localScale = Vector3.one * 0.5f;

            _enfrentarImage = go.AddComponent<Image>();
            _enfrentarImage.preserveAspect = true;

            _enfrentarButton = go.AddComponent<Button>();
            _enfrentarButton.transition    = Selectable.Transition.SpriteSwap;
            _enfrentarButton.targetGraphic = _enfrentarImage;
            _enfrentarButton.onClick.AddListener(OnEnfrentar);
        }

        // ── Builder helpers ──────────────────────────────────────────────────────

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

        private static void MakeCloseButton(Transform panel, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_Close");
            go.transform.SetParent(panel, false);

            var rt       = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(30f, 30f);
            rt.anchoredPosition = new Vector2(-85f, -54f);

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
