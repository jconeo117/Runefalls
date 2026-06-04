using UnityEngine;
using Unity.Netcode;
using Runefall.Combat;
using Runefall.Core;
using Runefall.Data;
using Runefall.Presentation.Combat;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Multiplayer composition root for combat encounter.
    /// Spawns and initializes MultiplayerTurnManager and TurnManagerBridge.
    /// Inherits from CombatBootstrapper to reuse presentation wiring, but replaces
    /// local TurnManager with a network-replicated MultiplayerTurnManager.
    /// </summary>
    public class MultiplayerCombatBootstrapper : CombatBootstrapper
    {
        [Header("Multiplayer Setup")]
        [SerializeField] private MultiplayerTurnManagerBridge turnManagerBridgePrefab;
        [SerializeField] private MultiplayerCombatRegistry multiplayerRegistry;

        private MultiplayerTurnManagerBridge _bridgeInstance;

        protected override void OnEnableImpl()
        {
            // Register registry in ServiceLocator for pawns and network translation
            if (multiplayerRegistry != null)
            {
                ServiceLocator.Register<MultiplayerCombatRegistry>(multiplayerRegistry);
                multiplayerRegistry.Initialize();
            }
            else
            {
                Debug.LogError("[MultiplayerCombatBootstrapper] MultiplayerCombatRegistry is not assigned!");
            }

            // Fallback to singleplayer logic if Netcode is not active
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                base.OnEnableImpl();
                return;
            }

            // Server spawns the bridge, clients locate it in the scene
            if (NetworkManager.Singleton.IsServer)
            {
                if (turnManagerBridgePrefab != null)
                {
                    var bridgeGo = Instantiate(turnManagerBridgePrefab.gameObject);
                    _bridgeInstance = bridgeGo.GetComponent<MultiplayerTurnManagerBridge>();
                    var netObj = bridgeGo.GetComponent<NetworkObject>();
                    netObj.Spawn(destroyWithScene: true);
                }
                else
                {
                    var netObj = GetComponent<NetworkObject>();
                    if (netObj != null)
                    {
                        _bridgeInstance = gameObject.AddComponent<MultiplayerTurnManagerBridge>();
                    }
                    else
                    {
                        Debug.LogError("[MultiplayerCombatBootstrapper] TurnManagerBridgePrefab not assigned and no NetworkObject on Bootstrapper!");
                    }
                }
            }
            else
            {
                StartCoroutine(WaitForBridgeClient());
            }

            if (NetworkManager.Singleton.IsServer)
            {
                InitializeMultiplayerSystems();
            }
        }

        private System.Collections.IEnumerator WaitForBridgeClient()
        {
            float timeout = 10f;
            while (_bridgeInstance == null && timeout > 0f)
            {
                _bridgeInstance = FindFirstObjectByType<MultiplayerTurnManagerBridge>();
                if (_bridgeInstance != null) break;
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (_bridgeInstance != null)
            {
                InitializeMultiplayerSystems();
            }
            else
            {
                Debug.LogError("[MultiplayerCombatBootstrapper] Timeout waiting for MultiplayerTurnManagerBridge on client!");
            }
        }

        private void InitializeMultiplayerSystems()
        {
            LogMissingRefsLocal();

            // Instantiate UI presenter
            if (presenter == null && _combatUIPrefab != null)
            {
                _combatUIInstance = Instantiate(_combatUIPrefab);
                presenter = _combatUIInstance.GetComponent<CombatPresenterBase>();
            }
            else if (presenter != null && !presenter.gameObject.activeSelf)
                presenter.gameObject.SetActive(true);

            var finisher = GetComponent<FinisherManager>();
            if (finisher != null)
            {
                if (_combatUIInstance != null)
                {
                    var hudGroup = _combatUIInstance.GetComponent<CanvasGroup>()
                                ?? _combatUIInstance.AddComponent<CanvasGroup>();
                    finisher.InjectHUDGroup(hudGroup);
                }
                if (presenter != null)
                    finisher.InjectPresenter(presenter);
            }

            _presenter = presenter;
            _mainCamera = Camera.main;

            // Spawn/assemble physical arena
            if (arenaAssembler != null && ServiceLocator.TryGet<EncounterState>(out var encounterState))
            {
                arenaAssembler.SpawnFromEncounterState(encounterState, encounterState.ArenaCenter);
            }
            else if (arenaAssembler != null)
            {
                int pc = CountValidSlotsPlayer();
                int ec = CountValidSlotsEnemy();
                arenaAssembler.Assemble(pc, ec);
            }

            // Build context
            var (context, fieldChars) = BuildFromSlots();
            _ctx = context;

            AutoInitCameraLocal();

            // Magic: Instantiate MultiplayerTurnManager instead of standard TurnManager
            var mpTm = new MultiplayerTurnManager(_bridgeInstance, animationDriver);
            _tm = mpTm;

            skillEventBridge?.Init(_tm);
            WireTurnManagerEventsLocal();

            _presenter?.Initialize(_tm, context);

            _markers = CreateEnemyMarkersLocal(context.Enemies.Count);
            if (_markers.Length > 0)
                _presenter?.RegisterEnemyMarkers(_markers);

            BindHPBars();

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

            if (arenaAssembler != null && arenaAssembler.IsReady && introSequencer != null)
            {
                if (arenaAssembler.IntroEnemyAnchor != null) introSequencer.introEnemyAnchor = arenaAssembler.IntroEnemyAnchor;
                if (arenaAssembler.IntroPlayerAnchor != null) introSequencer.introPlayerAnchor = arenaAssembler.IntroPlayerAnchor;
            }

            EnterCombatModeLocal();

            _pendingFieldChars = fieldChars;
        }

        // --- Helper overrides to map protected base variables/methods safely ---

        private int CountValidSlotsPlayer()
        {
            if (playerTeam == null) return 0;
            int count = 0;
            for (int i = 0; i < playerTeam.childCount; i++)
                if (playerTeam.GetChild(i).GetComponent<CharacterSlot>() != null) count++;
            return count;
        }

        private int CountValidSlotsEnemy()
        {
            if (enemyTeam == null) return 0;
            int count = 0;
            for (int i = 0; i < enemyTeam.childCount; i++)
                if (enemyTeam.GetChild(i).GetComponent<EnemySlot>() != null) count++;
            return count;
        }

        private void AutoInitCameraLocal()
        {
            if (cameraController == null && _mainCamera != null)
            {
                cameraController = _mainCamera.GetComponent<CombatCameraController>()
                                ?? _mainCamera.gameObject.AddComponent<CombatCameraController>();
            }

            if (cameraController == null) return;

            if (arenaAssembler != null && arenaAssembler.IsReady)
            {
                if (arenaAssembler.CameraGameplayAnchor != null)
                {
                    _mainCamera.transform.position = arenaAssembler.CameraGameplayAnchor.position;
                    _mainCamera.transform.rotation = arenaAssembler.CameraGameplayAnchor.rotation;
                    cameraController.InitFromAnchors(arenaAssembler.CameraGameplayAnchor, arenaAssembler.FieldCenter);
                }
                else
                {
                    var pRoot = arenaAssembler.PlayerRoot;
                    var eRoot = arenaAssembler.EnemyRoot;
                    if (pRoot != null && eRoot != null)
                    {
                        Vector3 playerDir = (pRoot.position - eRoot.position).normalized;
                        float span = Vector3.Distance(pRoot.position, eRoot.position);
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

        private void WireTurnManagerEventsLocal()
        {
            _tm.OnPlayerTurnStarted += OnPlayerTurnStartedLocal;
            _tm.OnPlayerTurnStarted += _ => _presenter?.OnPlayerTurnStarted(_);
            _onActionPendingHandler = pending => animationDriver?.Enqueue(pending);
            _tm.OnActionPending += _onActionPendingHandler;
            _tm.OnActionResolved += OnActionResolvedLocal;

            _tm.OnCombatEnded += won =>
            {
                _playerWon = won;
                _presenter?.OnCombatEnded(won);

                var finisher = GetComponent<FinisherManager>();
                if (finisher != null)
                {
                    finisher.ForceCleanupIfActive();
                }
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;

                if (!won) { EndCombat(); return; }

                if (_victorySequencer != null)
                    _victorySequencer.Play(EndCombat);
                else
                    EndCombat();
            };

            _tm.OnMergeOccurred += (name, rank) => _presenter?.OnCardMerged(name, rank);
            _tm.OnGaugeChanged += (actor, orbs) => _presenter?.OnGaugeChanged(actor, orbs);

            _tm.OnPlayerActionsExhausted += () =>
            {
                _presenter?.SetActionSlotsActive(false);
                animationDriver?.PlayQueuedAnimations(() => _tm.EndPlayerTurn(), fadeSlots: true);
            };

            if (cameraController != null)
            {
                _tm.OnPlayerTurnBegin += cameraController.OnPlayerTurnStarted;
                _tm.OnEnemyTurnStarted += cameraController.OnEnemyTurnStarted;
            }

            _tm.PlayerTurnStartHandler = fire => StartCoroutine(PlayerTurnSettleLocal(fire));
        }

        private void OnPlayerTurnStartedLocal(int _)
        {
            _selectedIndex = -1;
            for (int i = 0; i < _markers.Length; i++)
                _markers[i]?.SetSelected(false);
            _presenter?.SelectEnemy(-1);

            _presenter?.SetActionSlotsActive(true);
            for (int i = 0; i < _hpBars.Count; i++)
                _hpBars[i]?.ForceRefresh();
        }

        private void OnActionResolvedLocal(CombatActionResult result)
        {
            if (_selectedIndex >= 0 && _selectedIndex < _ctx.Enemies.Count && !_ctx.Enemies[_selectedIndex].IsAlive)
            {
                _selectedIndex = -1;
                for (int i = 0; i < _markers.Length; i++)
                    _markers[i]?.SetSelected(false);
                _presenter?.SelectEnemy(-1);
            }

            _presenter?.OnActionResolved(result);
        }

        private System.Collections.IEnumerator PlayerTurnSettleLocal(System.Action fire)
        {
            if (_playerTurnCameraDelay > 0f)
                yield return new WaitForSeconds(_playerTurnCameraDelay);
            fire();
        }

        private EnemyTargetMarker[] CreateEnemyMarkersLocal(int enemyCount)
        {
            if (_enemySlots == null || _enemySlots.Length == 0)
                return System.Array.Empty<EnemyTargetMarker>();

            int count = Mathf.Min(enemyCount, _enemySlots.Length);
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

                var disc = CreateIndicatorDiscLocal(_enemySlots[i]);
                marker.Init(i, disc);
                markers[i] = marker;
            }

            return markers;
        }

        private GameObject CreateIndicatorDiscLocal(Transform parent)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "TargetIndicator";
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            disc.transform.localScale = new Vector3(2.4f, 0.02f, 2.4f);

            Destroy(disc.GetComponent<Collider>());

            var rend = disc.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Unlit/Color")
                      ?? Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = new Color(1f, 0.78f, 0.05f, 1f);
            rend.sharedMaterial = mat;

            disc.SetActive(false);
            return disc;
        }

        private void EnterCombatModeLocal()
        {
            if (_explorationBrain != null) _explorationBrain.enabled = false;
            if (_explorationVcam != null) _explorationVcam.SetActive(false);
            if (cameraController != null) cameraController.enabled = true;
            if (_cameraWallAvoidance != null) _cameraWallAvoidance.enabled = false;
            if (_playerController != null)
            {
                _playerController.enabled = false;
                _playerController.gameObject.SetActive(false);
            }
            if (_cameraInput != null) _cameraInput.enabled = false;

            if (ServiceLocator.TryGet<EncounterState>(out var es) && es.Encounter.enemyTransform != null)
                es.Encounter.enemyTransform.gameObject.SetActive(false);
        }

        private void LogMissingRefsLocal()
        {
            if (arenaAssembler == null)
                Debug.LogError("[MultiplayerCombatBootstrapper] arenaAssembler not assigned — pawns won't spawn from EncounterState.", this);
            if (presenter == null && _combatUIPrefab == null)
                Debug.LogWarning("[MultiplayerCombatBootstrapper] No combat UI: assign _combatUIPrefab (runtime) or presenter (blockout).", this);
            if (animationDriver == null)
                Debug.LogWarning("[MultiplayerCombatBootstrapper] animationDriver not assigned — no combat animations.", this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (ServiceLocator.TryGet<MultiplayerCombatRegistry>(out _))
            {
                ServiceLocator.Unregister<MultiplayerCombatRegistry>();
            }
        }
    }
}
