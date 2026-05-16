using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Runefall.Combat;
using Runefall.Data;
using Runefall.Enemies;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Composition Root for a combat encounter.
    /// Reads CharacterSlot/EnemySlot components from playerTeam/enemyTeam children to build
    /// CombatContext, then wires TurnManager, presenter, animation driver, and camera director.
    ///
    /// Responsibilities:
    ///   - Build runtime combat state from scene slots
    ///   - Instantiate and connect all combat systems
    ///   - Handle enemy target selection (mouse click)
    ///   - Manage HP bar creation and per-turn refresh
    ///
    /// Production path: read EncounterState from ServiceLocator instead of building from slots.
    /// </summary>
    public class CombatBootstrapper : MonoBehaviour
    {
        [Header("Wiring")]
        public CombatPresenterBase    presenter;          // serialized field — Inspector keeps scene ref
        public CombatCameraController cameraController;
        public CombatIntroSequencer                     introSequencer;
        public SkillEventBridge                         skillEventBridge;
        public CombatAnimationDriver                    animationDriver;
        public CombatCameraDirector                     cameraDirector;
        public CombatVFXPlayer                          vfxPlayer;

        [Header("Teams")]
        [Tooltip("Root of player pawns. Children must have CharacterSlot. Order = card order.")]
        public Transform playerTeam;
        [Tooltip("Root of enemy pawns. Children must have EnemySlot. Order = turn/target order.")]
        public Transform enemyTeam;

        // Runtime state
        private TurnManager   _tm;
        private CombatContext _ctx;
        private ICombatPresenter _presenter;
        private Camera           _mainCamera;

        private Transform[]         _enemySlots = System.Array.Empty<Transform>();
        private EnemyTargetMarker[] _markers    = System.Array.Empty<EnemyTargetMarker>();
        private int                 _selectedIndex = -1;

        private readonly List<HPBarPresenter>                     _hpBars         = new();
        private readonly Dictionary<ICombatActor, HPBarPresenter>  _actorHPBars    = new();
        private readonly Dictionary<ICombatActor, Transform>       _actorPawns     = new();
        private readonly Dictionary<ICombatActor, CharacterData>   _actorCharData  = new();
        private readonly Dictionary<ICombatActor, EnemyData>       _actorEnemyData = new();

        // ── lifecycle ─────────────────────────────────────────────────────────────

        private void Start()
        {
            _presenter  = presenter;
            _mainCamera = Camera.main;

            var (context, fieldChars) = BuildFromSlots();
            _ctx = context;

            AutoInitCamera();

            _tm = new TurnManager(animationDriver);
            skillEventBridge?.Init(_tm);

            WireTurnManagerEvents();

            _presenter?.Initialize(_tm, context);

            _markers = CreateEnemyMarkers(context.Enemies.Count);
            if (_markers.Length > 0)
                _presenter?.RegisterEnemyMarkers(_markers);

            BindHPBars();

            animationDriver?.Init(
                _ctx, _actorPawns, _actorCharData, _actorEnemyData, _actorHPBars, _presenter);

            vfxPlayer?.Init(_actorPawns, _actorEnemyData);

            if (cameraDirector != null && cameraController != null && playerTeam != null && enemyTeam != null)
            {
                Vector3 fieldCenter = (playerTeam.position + enemyTeam.position) * 0.5f;
                cameraDirector.Init(cameraController, fieldCenter, _tm);
            }

            void BeginCombat() => _tm.StartCombat(context, fieldChars, hasBench: false);
            if (introSequencer != null)
                introSequencer.Run(BeginCombat);
            else
                BeginCombat();
        }

        private void WireTurnManagerEvents()
        {
            _tm.OnPlayerTurnStarted += OnPlayerTurnStarted;
            _tm.OnPlayerTurnStarted += _ => _presenter?.OnPlayerTurnStarted(_);
            _tm.OnActionResolved    += OnActionResolved;
            _tm.OnCombatEnded       += won => _presenter?.OnCombatEnded(won);
            _tm.OnMergeOccurred     += (name, rank) => _presenter?.OnCardMerged(name, rank);

            _tm.OnGaugeChanged += (actor, orbs) => _presenter?.OnGaugeChanged(actor, orbs);

            _tm.OnPlayerActionsExhausted += () =>
            {
                _presenter?.SetActionSlotsActive(false);
                animationDriver?.PlayQueuedAnimations(() => _tm.EndPlayerTurn(), fadeSlots: true);
            };

            if (cameraController != null)
            {
                _tm.OnPlayerTurnStarted += cameraController.OnPlayerTurnStarted;
                _tm.OnEnemyTurnStarted  += cameraController.OnEnemyTurnStarted;
            }
        }

        // ── enemy selection ───────────────────────────────────────────────────────

        private void Update()
        {
            if (_tm?.Phase != CombatPhase.PlayerTurn) return;
            if (!Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (_mainCamera == null) return;

            var ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f)) return;

            var marker = hit.collider.GetComponentInParent<EnemyTargetMarker>();
            if (marker == null) return;
            if (marker.Index >= _ctx.Enemies.Count) return;
            if (!_ctx.Enemies[marker.Index].IsAlive) return;

            if (marker.Index == _selectedIndex)
                ClearSelection();
            else
                SetSelectedEnemy(marker.Index);
        }

        private void SetSelectedEnemy(int index)
        {
            _selectedIndex = index;
            for (int i = 0; i < _markers.Length; i++)
                _markers[i]?.SetSelected(i == index);
            _presenter?.SelectEnemy(index);
        }

        private void ClearSelection()
        {
            _selectedIndex = -1;
            for (int i = 0; i < _markers.Length; i++)
                _markers[i]?.SetSelected(false);
            _presenter?.SelectEnemy(-1);
        }

        // ── event handlers ────────────────────────────────────────────────────────

        private void OnPlayerTurnStarted(int _)
        {
            ClearSelection();
            _presenter?.SetActionSlotsActive(true);
            for (int i = 0; i < _hpBars.Count; i++)
                _hpBars[i]?.ForceRefresh();
        }

        private void OnActionResolved(CombatActionResult result)
        {
            if (_selectedIndex >= 0
                && _selectedIndex < _ctx.Enemies.Count
                && !_ctx.Enemies[_selectedIndex].IsAlive)
                ClearSelection();

            _presenter?.OnActionResolved(result);
            animationDriver?.Enqueue(result);
        }

        // ── slot-driven encounter ─────────────────────────────────────────────────

        private (CombatContext, List<CharacterData>) BuildFromSlots()
        {
            var fieldChars   = new List<CharacterData>();
            var playerActors = new List<ICombatActor>();

            if (playerTeam != null)
            {
                // Reverse order: rightmost child in hierarchy → leftmost card slots.
                for (int i = playerTeam.childCount - 1; i >= 0; i--)
                {
                    var child = playerTeam.GetChild(i);
                    var slot  = child.GetComponent<CharacterSlot>();
                    if (slot?.data == null) continue;
                    var actor = new PlayerActor(slot.data);
                    fieldChars.Add(slot.data);
                    playerActors.Add(actor);
                    _actorPawns[actor]    = child;
                    _actorCharData[actor] = slot.data;
                }
            }

            var enemySlotList = new List<Transform>();
            var enemyActors   = new List<ICombatActor>();

            if (enemyTeam != null)
            {
                for (int i = 0; i < enemyTeam.childCount; i++)
                {
                    var child = enemyTeam.GetChild(i);
                    var slot  = child.GetComponent<EnemySlot>();
                    if (slot?.data == null) continue;
                    var agent = new EnemyAgent(slot.data);
                    enemyActors.Add(agent);
                    enemySlotList.Add(child);
                    _actorPawns[agent]      = child;
                    _actorEnemyData[agent]  = slot.data;
                }
            }

            _enemySlots = enemySlotList.ToArray();
            return (new CombatContext(playerActors, enemyActors), fieldChars);
        }

        // ── HP bars ───────────────────────────────────────────────────────────────

        private void BindHPBars()
        {
            for (int i = 0; i < _ctx.Players.Count; i++)
            {
                var actor = _ctx.Players[i];
                if (!_actorPawns.TryGetValue(actor, out var pawn)) continue;
                var slot = pawn.GetComponent<CharacterSlot>();
                var bar  = AttachHPBar(pawn, actor,
                    slot != null ? slot.hpBarOffset    : 3.5f,
                    slot != null ? slot.headBoneOffset : 0.25f,
                    slot != null ? slot.headBone       : null);
                _hpBars.Add(bar);
                _actorHPBars[actor] = bar;
            }

            for (int i = 0; i < _enemySlots.Length && i < _ctx.Enemies.Count; i++)
            {
                if (_enemySlots[i] == null) continue;
                var actor = _ctx.Enemies[i];
                var slot  = _enemySlots[i].GetComponent<EnemySlot>();
                var bar   = AttachHPBar(_enemySlots[i], actor,
                    slot != null ? slot.hpBarOffset    : 3.5f,
                    slot != null ? slot.headBoneOffset : 0.25f,
                    slot != null ? slot.headBone       : null);
                _hpBars.Add(bar);
                _actorHPBars[actor] = bar;
            }
        }

        private static HPBarPresenter AttachHPBar(
            Transform pawn, ICombatActor actor,
            float yOffset, float headBoneOffset = 0.25f, Transform headBoneOverride = null)
        {
            Transform attachPoint = pawn;
            float     offset      = yOffset;

            if (headBoneOverride != null)
            {
                attachPoint = headBoneOverride;
                offset      = headBoneOffset;
            }
            else
            {
                var anim = pawn.GetComponentInChildren<Animator>();
                if (anim != null && anim.isHuman)
                {
                    var head = anim.GetBoneTransform(HumanBodyBones.Head);
                    if (head != null) { attachPoint = head; offset = headBoneOffset; }
                }
            }

            var barGO = new GameObject("HPBar");
            barGO.transform.SetParent(pawn, false);
            barGO.transform.localPosition = Vector3.zero;
            barGO.transform.localScale    = new Vector3(0.01f, 0.01f, 0.01f);

            var canvas = barGO.AddComponent<Canvas>();
            canvas.renderMode      = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder    = 100;
            barGO.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 28f);

            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(barGO.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            bgGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.12f, 0.04f, 0.04f, 0.9f);

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(barGO.transform, false);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
            fillGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.18f, 0.80f, 0.22f, 0.95f);

            var hp = barGO.AddComponent<HPBarPresenter>();
            hp.Bind(actor, fillRT);
            hp.SetFollow(attachPoint, Vector3.up * offset);
            return hp;
        }

        // ── enemy markers ─────────────────────────────────────────────────────────

        private EnemyTargetMarker[] CreateEnemyMarkers(int enemyCount)
        {
            if (_enemySlots == null || _enemySlots.Length == 0)
                return System.Array.Empty<EnemyTargetMarker>();

            int count   = Mathf.Min(enemyCount, _enemySlots.Length);
            var markers = new EnemyTargetMarker[count];

            for (int i = 0; i < count; i++)
            {
                if (_enemySlots[i] == null) continue;

                for (int c = _enemySlots[i].childCount - 1; c >= 0; c--)
                {
                    var child = _enemySlots[i].GetChild(c);
                    if (child.name == "TargetIndicator")
                        DestroyImmediate(child.gameObject);
                }

                var marker = _enemySlots[i].GetComponent<EnemyTargetMarker>()
                             ?? _enemySlots[i].gameObject.AddComponent<EnemyTargetMarker>();

                var disc = CreateIndicatorDisc(_enemySlots[i]);
                marker.Init(i, disc);
                markers[i] = marker;
            }

            return markers;
        }

        // ── camera auto-init ──────────────────────────────────────────────────────

        private void AutoInitCamera()
        {
            if (cameraController == null && _mainCamera != null)
            {
                cameraController = _mainCamera.GetComponent<CombatCameraController>()
                                ?? _mainCamera.gameObject.AddComponent<CombatCameraController>();
            }

            if (cameraController != null && playerTeam != null && enemyTeam != null)
                cameraController.InitFromTeams(playerTeam, enemyTeam);
        }

        // ── factory helpers ───────────────────────────────────────────────────────

        private static GameObject CreateIndicatorDisc(Transform parent)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "TargetIndicator";
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            disc.transform.localScale    = new Vector3(2.4f, 0.02f, 2.4f);

            Object.Destroy(disc.GetComponent<Collider>());

            var rend   = disc.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Unlit/Color")
                      ?? Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Standard");
            var mat    = new Material(shader);
            mat.color  = new Color(1f, 0.78f, 0.05f, 1f);
            rend.sharedMaterial = mat;

            disc.SetActive(false);
            return disc;
        }
    }
}
