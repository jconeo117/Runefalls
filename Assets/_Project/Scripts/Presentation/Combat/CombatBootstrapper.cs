using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Cinemachine;
using Runefall.Combat;
using Runefall.Core;
using Runefall.Data;
using Runefall.Enemies;
using Runefall.Presentation.Dungeon;
using Runefall.Presentation.Player;

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
        [Header("Combat UI")]
        [Tooltip("Prefab with CombatPresenterBase. Instantiated at combat start, destroyed on exit. " +
                 "Leave null and assign presenter directly for blockout/editor testing.")]
        [SerializeField] private GameObject _combatUIPrefab;

        [Header("Wiring")]
        public CombatPresenterBase    presenter;          // auto-populated from _combatUIPrefab, or set manually for blockout
        public CombatCameraController cameraController;
        public CombatIntroSequencer                     introSequencer;
        public SkillEventBridge                         skillEventBridge;
        public CombatAnimationDriver                    animationDriver;
        public CombatCameraDirector                     cameraDirector;
        public CombatVFXPlayer                          vfxPlayer;

        [Header("Timing")]
        [Tooltip("Seconds to wait after camera starts moving before firing OnPlayerTurnStarted (passives, UI, cards). Match to camera lerpSpeed settle time.")]
        [SerializeField] private float _playerTurnCameraDelay = 0.6f;

        [Header("Post-Combat")]
        [SerializeField] private VictorySequencer       _victorySequencer;
        [SerializeField] private CombatTransitionScreen _exitTransition;

        [Header("Arena")]
        [Tooltip("Optional. When assigned, controls slot positions and camera anchors.")]
        public CombatArenaAssembler arenaAssembler;

        [Header("Teams")]
        [Tooltip("Root of player pawns. Children must have CharacterSlot. Order = card order.")]
        public Transform playerTeam;
        [Tooltip("Root of enemy pawns. Children must have EnemySlot. Order = turn/target order.")]
        public Transform enemyTeam;

        [Header("Exploration (disabled during combat, restored on end)")]
        [SerializeField] private CinemachineBrain                  _explorationBrain;
        [SerializeField] private GameObject                         _explorationVcam;
        [SerializeField] private PlayerController                   _playerController;
        [SerializeField] private CinemachineInputAxisController     _cameraInput;
        [SerializeField] private Runefall.Presentation.Player.CameraWallAvoidance _cameraWallAvoidance;

        // Runtime state
        private GameObject                    _combatUIInstance;
        private TurnManager                   _tm;
        private CombatContext                 _ctx;
        private ICombatPresenter              _presenter;
        private Camera                        _mainCamera;
        private List<CharacterData>           _pendingFieldChars;   // held until BeginCombat() is called
        private bool                          _playerWon;
        private System.Action<PendingAction>  _onActionPendingHandler;

        private Transform[]         _enemySlots = System.Array.Empty<Transform>();
        private EnemyTargetMarker[] _markers    = System.Array.Empty<EnemyTargetMarker>();
        private int                 _selectedIndex = -1;

        private readonly List<HPBarPresenter>                     _hpBars         = new();
        private readonly Dictionary<ICombatActor, HPBarPresenter>  _actorHPBars    = new();
        private readonly Dictionary<ICombatActor, Transform>       _actorPawns     = new();
        private readonly Dictionary<ICombatActor, CharacterData>   _actorCharData  = new();
        private readonly Dictionary<ICombatActor, EnemyData>       _actorEnemyData = new();

        // ── lifecycle ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by EncounterPromptPresenter before SetActive so ExitCombatMode can restore movement.
        /// Overrides serialized fields only when non-null, preserving any Inspector-set values.
        /// </summary>
        public void OverrideExplorationRefs(PlayerController pc, CinemachineInputAxisController ci)
        {
            if (pc != null) _playerController = pc;
            if (ci != null) _cameraInput      = ci;
        }

        // OnEnable fires every time the GO is activated — required for re-use across encounters.
        // Start() fires only once per lifetime and would miss any encounter after the first.
        private void OnEnable()
        {
            try { OnEnableImpl(); }
            catch (Exception e) { Debug.LogError($"[CombatBootstrapper] OnEnable FAILED: {e}", this); }
        }

        private void OnEnableImpl()
        {
            LogMissingRefs();

            // Instantiate combat UI from prefab, or activate if it's a scene reference.
            if (presenter == null && _combatUIPrefab != null)
            {
                _combatUIInstance = Instantiate(_combatUIPrefab);
                presenter = _combatUIInstance.GetComponent<CombatPresenterBase>();
            }
            else if (presenter != null && !presenter.gameObject.activeSelf)
                presenter.gameObject.SetActive(true);

            _presenter  = presenter;
            _mainCamera = Camera.main;

            Debug.Log("[CombatBootstrapper] Step 1 — checking EncounterState");

            // EncounterState path (production): spawn pawns from runtime data.
            // Fallback: use manually placed CharacterSlot/EnemySlot children (blockout/editor).
            if (arenaAssembler != null && ServiceLocator.TryGet<EncounterState>(out var encounterState))
            {
                Debug.Log("[CombatBootstrapper] Step 2 — SpawnFromEncounterState");
                // Pawns spawn directly into arena slots — no temp team GOs needed.
                arenaAssembler.SpawnFromEncounterState(encounterState, encounterState.ArenaCenter);
            }
            else if (arenaAssembler != null)
            {
                Debug.Log("[CombatBootstrapper] Step 2 — Assemble (no EncounterState)");
                int pc = CountValidSlots<CharacterSlot>(playerTeam);
                int ec = CountValidSlots<EnemySlot>(enemyTeam);
                arenaAssembler.Assemble(pc, ec);
            }
            else
            {
                Debug.LogWarning("[CombatBootstrapper] Step 2 — arenaAssembler null, skipping");
            }

            Debug.Log("[CombatBootstrapper] Step 3 — BuildFromSlots");
            var (context, fieldChars) = BuildFromSlots();
            _ctx = context;
            Debug.Log($"[CombatBootstrapper] Step 3 done — players={context.Players.Count} enemies={context.Enemies.Count}");

            Debug.Log("[CombatBootstrapper] Step 4 — AutoInitCamera");
            AutoInitCamera();

            Debug.Log("[CombatBootstrapper] Step 5 — TurnManager + wire events");
            _tm = new TurnManager(animationDriver);
            skillEventBridge?.Init(_tm);
            WireTurnManagerEvents();

            Debug.Log("[CombatBootstrapper] Step 6 — Initialize presenter");
            _presenter?.Initialize(_tm, context);

            _markers = CreateEnemyMarkers(context.Enemies.Count);
            if (_markers.Length > 0)
                _presenter?.RegisterEnemyMarkers(_markers);

            Debug.Log("[CombatBootstrapper] Step 7 — BindHPBars");
            BindHPBars();

            Debug.Log("[CombatBootstrapper] Step 8 — animationDriver.Init");
            animationDriver?.Init(
                _ctx, _tm, _actorPawns, _actorCharData, _actorEnemyData, _actorHPBars, _presenter);

            vfxPlayer?.Init(_actorPawns, _actorEnemyData);

            if (cameraDirector != null && cameraController != null)
            {
                Vector3 fieldCenter = arenaAssembler != null && arenaAssembler.IsReady
                    ? arenaAssembler.FieldCenter
                    : playerTeam != null && enemyTeam != null
                        ? (playerTeam.position + enemyTeam.position) * 0.5f
                        : Vector3.zero;
                cameraDirector.Init(cameraController, fieldCenter, _tm);
            }

            // Wire intro anchors from layout prefab if available.
            if (arenaAssembler != null && arenaAssembler.IsReady && introSequencer != null)
            {
                if (arenaAssembler.IntroEnemyAnchor  != null) introSequencer.introEnemyAnchor  = arenaAssembler.IntroEnemyAnchor;
                if (arenaAssembler.IntroPlayerAnchor != null) introSequencer.introPlayerAnchor = arenaAssembler.IntroPlayerAnchor;
            }

            Debug.Log("[CombatBootstrapper] Step 9 — EnterCombatMode");
            EnterCombatMode();

            // Store until BeginCombat() is called externally (after intro fade-in).
            _pendingFieldChars = fieldChars;

            Debug.Log("[CombatBootstrapper] OnEnable complete — waiting for BeginCombat()");
        }

        private void WireTurnManagerEvents()
        {
            _tm.OnPlayerTurnStarted += OnPlayerTurnStarted;
            _tm.OnPlayerTurnStarted += _ => _presenter?.OnPlayerTurnStarted(_);
            _onActionPendingHandler  = pending => animationDriver?.Enqueue(pending);
            _tm.OnActionPending     += _onActionPendingHandler;
            _tm.OnActionResolved    += OnActionResolved;
            _tm.OnCombatEnded       += won =>
            {
                _playerWon = won;
                _presenter?.OnCombatEnded(won);
                string enemyName = ServiceLocator.TryGet<EncounterState>(out var es)
                                   && es.Encounter.enemyData != null
                                   ? es.Encounter.enemyData.enemyName
                                   : "Enemigo";
                if (won && _victorySequencer != null)
                    _victorySequencer.Play(EndCombat);
                else
                    EndCombat();
            };
            _tm.OnMergeOccurred     += (name, rank) => _presenter?.OnCardMerged(name, rank);

            _tm.OnGaugeChanged += (actor, orbs) => _presenter?.OnGaugeChanged(actor, orbs);

            _tm.OnPlayerActionsExhausted += () =>
            {
                _presenter?.SetActionSlotsActive(false);
                animationDriver?.PlayQueuedAnimations(() => _tm.EndPlayerTurn(), fadeSlots: true);
            };

            if (cameraController != null)
            {
                _tm.OnPlayerTurnBegin  += cameraController.OnPlayerTurnStarted;  // camera moves immediately
                _tm.OnEnemyTurnStarted += cameraController.OnEnemyTurnStarted;
            }

            _tm.PlayerTurnStartHandler = fire => StartCoroutine(PlayerTurnSettle(fire));
        }

        /// <summary>
        /// Called by EncounterPromptPresenter after the fade-in completes.
        /// Runs the intro sequencer (if present) then starts the turn loop.
        /// </summary>
        public void BeginCombat()
        {
            if (_tm == null || _pendingFieldChars == null)
            {
                Debug.LogError("[CombatBootstrapper] BeginCombat called before OnEnable completed.", this);
                return;
            }

            var fieldChars = _pendingFieldChars;
            _pendingFieldChars = null;

            void StartLoop()
            {
                _tm.StartCombat(_ctx, fieldChars, hasBench: false);
                ActivatePassives(fieldChars);
            }

            if (introSequencer != null)
                introSequencer.Run(StartLoop);
            else
                StartLoop();
        }

        private void ActivatePassives(System.Collections.Generic.List<CharacterData> fieldChars)
        {
            for (int i = 0; i < fieldChars.Count && i < _ctx.Players.Count; i++)
            {
                var cd = fieldChars[i];
                if (cd.passive == null) continue;
                cd.passive.Activate(_ctx.Players[i], _tm, _ctx);
            }
        }

        private void DeactivatePassives()
        {
            if (_tm == null || _ctx == null) return;
            foreach (var kvp in _actorCharData)
            {
                var cd = kvp.Value;
                if (cd.passive == null) continue;
                cd.passive.Deactivate(kvp.Key, _tm);
            }
        }

        private void OnDisable()
        {
            DeactivatePassives();
            DestroyUIInstance();

            if (_tm != null)
            {
                _tm.OnPlayerTurnStarted -= OnPlayerTurnStarted;
                _tm.OnActionPending     -= _onActionPendingHandler;
                _tm.OnActionResolved    -= OnActionResolved;
                if (cameraController != null)
                {
                    _tm.OnPlayerTurnBegin  -= cameraController.OnPlayerTurnStarted;
                    _tm.OnEnemyTurnStarted -= cameraController.OnEnemyTurnStarted;
                }
                _tm.PlayerTurnStartHandler = null;
            }

            _tm                      = null;
            _ctx                     = null;
            _presenter               = null;
            _pendingFieldChars       = null;
            _playerWon               = false;
            _onActionPendingHandler  = null;
            _hpBars.Clear();
            _actorHPBars.Clear();
            _actorPawns.Clear();
            _actorCharData.Clear();
            _actorEnemyData.Clear();
            _enemySlots    = System.Array.Empty<Transform>();
            _markers       = System.Array.Empty<EnemyTargetMarker>();
            _selectedIndex = -1;
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
            // Animation enqueue now wired to OnActionPending — no Enqueue() here.
        }

        // ── slot-driven encounter ─────────────────────────────────────────────────

        private (CombatContext, List<CharacterData>) BuildFromSlots()
        {
            var fieldChars    = new List<CharacterData>();
            var playerActors  = new List<ICombatActor>();
            bool hasAssembler = arenaAssembler != null && arenaAssembler.IsReady;

            if (hasAssembler)
            {
                // EncounterState path: pawns already spawned directly into arena slots.
                for (int s = 0; s < arenaAssembler.PlayerSlots.Count; s++)
                {
                    var slotT = arenaAssembler.PlayerSlots[s];
                    for (int c = 0; c < slotT.childCount; c++)
                    {
                        var child = slotT.GetChild(c);
                        var slot  = child.GetComponent<CharacterSlot>();
                        if (slot?.data == null) continue;
                        var actor = new PlayerActor(slot.data);
                        fieldChars.Add(slot.data);
                        playerActors.Add(actor);
                        _actorPawns[actor]    = child;
                        _actorCharData[actor] = slot.data;
                    }
                }
            }
            else if (playerTeam != null)
            {
                // Blockout path: manually-placed CharacterSlot children in scene.
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

            if (hasAssembler)
            {
                // EncounterState path: read enemy pawns from arena slots.
                for (int s = 0; s < arenaAssembler.EnemySlots.Count; s++)
                {
                    var slotT = arenaAssembler.EnemySlots[s];
                    for (int c = 0; c < slotT.childCount; c++)
                    {
                        var child = slotT.GetChild(c);
                        var slot  = child.GetComponent<EnemySlot>();
                        if (slot?.data == null) continue;
                        var agent = new EnemyAgent(slot.data);
                        enemyActors.Add(agent);
                        enemySlotList.Add(child);
                        _actorPawns[agent]     = child;
                        _actorEnemyData[agent] = slot.data;
                    }
                }
            }
            else if (enemyTeam != null)
            {
                // Blockout path: manually-placed EnemySlot children in scene.
                for (int i = 0; i < enemyTeam.childCount; i++)
                {
                    var child = enemyTeam.GetChild(i);
                    var slot  = child.GetComponent<EnemySlot>();
                    if (slot?.data == null) continue;
                    var agent = new EnemyAgent(slot.data);
                    enemyActors.Add(agent);
                    enemySlotList.Add(child);
                    _actorPawns[agent]     = child;
                    _actorEnemyData[agent] = slot.data;
                }
            }

            _enemySlots = enemySlotList.ToArray();
            return (new CombatContext(playerActors, enemyActors), fieldChars);
        }

        private static void PlacePawnAtSlot(Transform pawn, Transform slot)
        {
            pawn.SetParent(slot, false);
            pawn.localPosition = Vector3.zero;
            pawn.localRotation = Quaternion.identity;
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
                    slot != null ? slot.headBoneOffset : 0.5f,
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
                    slot != null ? slot.headBoneOffset : 0.5f,
                    slot != null ? slot.headBone       : null);
                _hpBars.Add(bar);
                _actorHPBars[actor] = bar;
            }
        }

        private static HPBarPresenter AttachHPBar(
            Transform pawn, ICombatActor actor,
            float yOffset, float headBoneOffset = 0.5f, Transform headBoneOverride = null)
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

        // ── post-combat ───────────────────────────────────────────────────────────

        public void EndCombat()
        {
            ResetEnemyTrigger();
            StartCoroutine(EndCombatRoutine());
        }

        private IEnumerator EndCombatRoutine()
        {
            if (_exitTransition != null)
                yield return StartCoroutine(_exitTransition.FadeToBlack());

            // Restore exploration systems (everything except deactivating this GO)
            RestoreExplorationMode();

            // Hand off FadeFromBlack to the transition screen before we deactivate —
            // coroutines on this GO stop when SetActive(false) is called.
            if (_exitTransition != null)
                _exitTransition.StartCoroutine(_exitTransition.FadeFromBlack());

            gameObject.SetActive(false);
        }

        private void ResetEnemyTrigger()
        {
            if (!ServiceLocator.TryGet<EncounterState>(out var es)) return;
            var t = es.Encounter.enemyTransform;
            if (t == null) return;

            if (_playerWon)
                return; // enemy stays deactivated after defeat

            // Defeat path: respawn enemy so player can retry
            t.GetComponent<EnemyEncounterTrigger>()?.ResetTrigger();
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
        }

        // ── exploration ↔ combat camera swap ─────────────────────────────────────

        private void EnterCombatMode()
        {
            if (_explorationBrain      != null) _explorationBrain.enabled      = false;
            if (_explorationVcam       != null) _explorationVcam.SetActive(false);
            if (cameraController       != null) cameraController.enabled       = true;
            if (_cameraWallAvoidance   != null) _cameraWallAvoidance.enabled   = false;
            if (_playerController != null)
            {
                _playerController.enabled = false;
                _playerController.gameObject.SetActive(false);
            }
            if (_cameraInput != null) _cameraInput.enabled = false;

            // Hide dungeon enemy that triggered the encounter
            if (ServiceLocator.TryGet<EncounterState>(out var es)
                && es.Encounter.enemyTransform != null)
                es.Encounter.enemyTransform.gameObject.SetActive(false);
        }

        private void RestoreExplorationMode()
        {
            if (_explorationBrain    != null) _explorationBrain.enabled    = true;
            if (_explorationVcam     != null) _explorationVcam.SetActive(true);
            if (cameraController     != null) cameraController.enabled     = false;
            if (_cameraWallAvoidance != null) _cameraWallAvoidance.enabled = true;
            if (_playerController != null)
            {
                _playerController.gameObject.SetActive(true);
                _playerController.enabled = true;
            }
            if (_cameraInput != null) _cameraInput.enabled = true;
            arenaAssembler?.Teardown();
            DestroyUIInstance();
        }

        private void ExitCombatMode()
        {
            RestoreExplorationMode();
            gameObject.SetActive(false);
        }

        private void DestroyUIInstance()
        {
            if (_combatUIInstance == null) return;
            Destroy(_combatUIInstance);
            _combatUIInstance = null;
            presenter         = null;
        }

        // ── camera auto-init ──────────────────────────────────────────────────────

        private void AutoInitCamera()
        {
            if (cameraController == null && _mainCamera != null)
            {
                cameraController = _mainCamera.GetComponent<CombatCameraController>()
                                ?? _mainCamera.gameObject.AddComponent<CombatCameraController>();
            }

            if (cameraController == null) return;

            // Kill Cinemachine BEFORE repositioning camera.
            if (_explorationBrain != null) _explorationBrain.enabled = false;
            if (_explorationVcam  != null) _explorationVcam.SetActive(false);

            if (arenaAssembler != null && arenaAssembler.IsReady)
            {
                if (arenaAssembler.CameraGameplayAnchor != null)
                {
                    // Designer-tuned position from layout prefab — snap and use as player anchor.
                    _mainCamera.transform.position = arenaAssembler.CameraGameplayAnchor.position;
                    _mainCamera.transform.rotation = arenaAssembler.CameraGameplayAnchor.rotation;
                    cameraController.InitFromAnchors(arenaAssembler.CameraGameplayAnchor, arenaAssembler.FieldCenter);
                }
                else
                {
                    // Fallback: heuristic position (no layout prefab assigned).
                    var pRoot = arenaAssembler.PlayerRoot;
                    var eRoot = arenaAssembler.EnemyRoot;
                    if (pRoot != null && eRoot != null)
                    {
                        Vector3 playerDir = (pRoot.position - eRoot.position).normalized;
                        float   span      = Vector3.Distance(pRoot.position, eRoot.position);
                        _mainCamera.transform.position = pRoot.position + playerDir * Mathf.Max(span * 0.8f, 3f) + Vector3.up * 3.5f;
                        _mainCamera.transform.LookAt(arenaAssembler.FieldCenter);
                    }
                    cameraController.InitFromTeams(arenaAssembler.PlayerRoot, arenaAssembler.EnemyRoot);
                }
            }
            else if (playerTeam != null && enemyTeam != null)
                cameraController.InitFromTeams(playerTeam, enemyTeam);

            cameraController.enabled = true;
        }

        // ── factory helpers ───────────────────────────────────────────────────────

        private static void ClearTeamChildren(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
                DestroyImmediate(root.GetChild(i).gameObject);
        }

        private static int CountValidSlots<T>(Transform root) where T : Component
        {
            if (root == null) return 0;
            int count = 0;
            for (int i = 0; i < root.childCount; i++)
                if (root.GetChild(i).GetComponent<T>() != null) count++;
            return count;
        }

        private static GameObject CreateIndicatorDisc(Transform parent)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "TargetIndicator";
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            disc.transform.localScale    = new Vector3(2.4f, 0.02f, 2.4f);

            UnityEngine.Object.Destroy(disc.GetComponent<Collider>());

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

        private System.Collections.IEnumerator PlayerTurnSettle(Action fire)
        {
            if (_playerTurnCameraDelay > 0f)
                yield return new WaitForSeconds(_playerTurnCameraDelay);
            fire();
        }

        private void LogMissingRefs()
        {
            if (arenaAssembler == null)
                Debug.LogError("[CombatBootstrapper] arenaAssembler not assigned — pawns won't spawn from EncounterState.", this);
            if (presenter == null && _combatUIPrefab == null)
                Debug.LogWarning("[CombatBootstrapper] No combat UI: assign _combatUIPrefab (runtime) or presenter (blockout).", this);
            if (animationDriver == null)
                Debug.LogWarning("[CombatBootstrapper] animationDriver not assigned — no combat animations.", this);
        }
    }
}
