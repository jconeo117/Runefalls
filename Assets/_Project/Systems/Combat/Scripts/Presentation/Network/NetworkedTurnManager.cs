using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Runefall.Combat;
using Runefall.Data;
using Runefall.Core;
using Runefall.Enemies;
using Runefall.Presentation.Combat;

namespace Runefall.Presentation.Network
{
    /// <summary>
    /// Programmatic wrapper that bridges Unity Netcode for GameObjects with the domain TurnManager.
    /// Authorities turn loop, sessional countdowns, action submissions, and broadcasts visual triggers.
    /// Only the Host (Server) runs the actual domain simulation.
    /// </summary>
    public class NetworkedTurnManager : NetworkBehaviour
    {
        public static NetworkedTurnManager Instance { get; private set; }

        [Header("Domain ScriptableObjects")]
        [SerializeField] private CharacterData playerCharacterData;
        [SerializeField] private CharacterData clientCharacterData;
        [SerializeField] private EnemyData bossEnemyData;

        public CharacterData PlayerCharacterData => playerCharacterData;
        public CharacterData ClientCharacterData => clientCharacterData;
        public EnemyData BossEnemyData => bossEnemyData;

        [Header("Sessional Network Variables")]
        public NetworkVariable<CombatPhase> CurrentPhase = new(
            CombatPhase.Idle, 
            NetworkVariableReadPermission.Everyone, 
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> RoundNumber = new(
            0, 
            NetworkVariableReadPermission.Everyone, 
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<float> TurnTimer = new(
            30f, 
            NetworkVariableReadPermission.Everyone, 
            NetworkVariableWritePermission.Server
        );

        [Header("Actor Health Sync (Max / Current)")]
        public NetworkVariable<float> HostHP = new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> HostMaxHP = new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        
        public NetworkVariable<float> ClientHP = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> ClientMaxHP = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<float> BossHP = new(1000f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> BossMaxHP = new(1000f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> ActionsRemaining = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Header("Synchronised Shared HandState")]
        public NetworkVariable<NetworkedHand> HandState = new(
            new NetworkedHand(), 
            NetworkVariableReadPermission.Everyone, 
            NetworkVariableWritePermission.Server
        );

        [Header("Replicated Global Played Actions")]
        public NetworkVariable<NetworkedActionState> ReplicatedActions = new(
            new NetworkedActionState(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private struct PendingServerAction
        {
            public ICombatActor Caster;
            public ICombatActor Target;
            public SkillData Skill;
            public UltimateData Ultimate;
            public int Rank;
            public TargetType TargetType;
            public bool IsUltimate;
            public int SkillType;
        }

        private TurnManager _authoritativeTurnManager;
        private TurnManager _domainTurnManager; // Local visual proxy on Host/Client
        private bool _isTimerActive = false;
        private List<CharacterData> _fieldChars = new();
        private List<ICombatActor> _playerActors = new();
        private List<ICombatActor> _enemyActors = new();
        private HashSet<ulong> _playerFinishedTurn = new(); // 0 for Host, 1 for Client
        private List<PendingServerAction> _queuedActions = new();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[NetworkedTurnManager] Initialising Networked Turn Manager. IsServer={IsServer}");

            if (IsServer)
            {
                _authoritativeTurnManager = new TurnManager();
                _domainTurnManager = new TurnManager();

                // Register authoritative domain events to propagate state updates via RPCs
                _authoritativeTurnManager.OnPlayerTurnBegin += HandlePlayerTurnBegin;
                _authoritativeTurnManager.OnEnemyTurnStarted += HandleEnemyTurnStarted;
                _authoritativeTurnManager.OnGaugeChanged += HandleDomainGaugeChanged;
                _authoritativeTurnManager.OnCombatEnded += HandleDomainCombatEnded;

                StartCoroutine(StartCombatDelayed());
            }
            else
            {
                _domainTurnManager = new TurnManager();

                // Reactive clients subscribe to network state variables
                CurrentPhase.OnValueChanged += HandlePhaseChangedOnClient;
                RoundNumber.OnValueChanged += HandleRoundChangedOnClient;
                HandState.OnValueChanged += HandleHandStateChangedOnClient;
                ActionsRemaining.OnValueChanged += HandleActionsRemainingChangedOnClient;

                HostHP.OnValueChanged += HandleHostHPChanged;
                ClientHP.OnValueChanged += HandleClientHPChanged;
                BossHP.OnValueChanged += HandleBossHPChanged;

                ReplicatedActions.OnValueChanged += HandleReplicatedActionsChanged;

                StartCoroutine(BootClientPresentationDelayed());
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                if (_authoritativeTurnManager != null)
                {
                    _authoritativeTurnManager.OnPlayerTurnBegin -= HandlePlayerTurnBegin;
                    _authoritativeTurnManager.OnEnemyTurnStarted -= HandleEnemyTurnStarted;
                    _authoritativeTurnManager.OnGaugeChanged -= HandleDomainGaugeChanged;
                    _authoritativeTurnManager.OnCombatEnded -= HandleDomainCombatEnded;
                }
            }
            else
            {
                CurrentPhase.OnValueChanged -= HandlePhaseChangedOnClient;
                RoundNumber.OnValueChanged -= HandleRoundChangedOnClient;
                HandState.OnValueChanged -= HandleHandStateChangedOnClient;
                ActionsRemaining.OnValueChanged -= HandleActionsRemainingChangedOnClient;

                HostHP.OnValueChanged -= HandleHostHPChanged;
                ClientHP.OnValueChanged -= HandleClientHPChanged;
                BossHP.OnValueChanged -= HandleBossHPChanged;

                ReplicatedActions.OnValueChanged -= HandleReplicatedActionsChanged;
            }
        }

        private IEnumerator StartCombatDelayed()
        {
            // Wait until the client is fully connected and visible in the clients list
            float elapsed = 0f;
            while (NetworkManager.Singleton.ConnectedClientsList.Count < 2 && elapsed < 5.0f)
            {
                elapsed += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
            Debug.Log($"[NetworkedTurnManager] Server StartCombat delay ended. Connected clients = {NetworkManager.Singleton.ConnectedClientsList.Count}, elapsed = {elapsed}s");
            // Initialize local presentation first so it's ready to receive domain events
            StartCombatSimulationLocal();
            // Start authoritative combat (this will immediately trigger PlayerTurnBegin)
            StartCombatSimulation();
        }

        private IEnumerator BootClientPresentationDelayed()
        {
            // Wait for the host to initialize and replicate the player healths/stats
            float elapsed = 0f;
            while (ClientMaxHP.Value <= 0f && elapsed < 4.0f)
            {
                elapsed += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
            Debug.Log($"[NetworkedTurnManager] Client Boot delay ended. ClientMaxHP = {ClientMaxHP.Value}, elapsed = {elapsed}s");
            StartCombatSimulationLocal();
        }

        private void StartCombatSimulation()
        {
            if (playerCharacterData == null || bossEnemyData == null)
            {
                Debug.LogError("[NetworkedTurnManager] Missing domain CharacterData or EnemyData references in inspector!");
                return;
            }

            // Actors were already created by StartCombatSimulationLocal()
            if (_playerActors.Count > 0)
            {
                HostMaxHP.Value = _playerActors[0].Model.MaxHP;
                HostHP.Value = _playerActors[0].Model.CurrentHP;
            }

            if (_playerActors.Count > 1)
            {
                ClientMaxHP.Value = _playerActors[1].Model.MaxHP;
                ClientHP.Value = _playerActors[1].Model.CurrentHP;
            }
            else
            {
                ClientMaxHP.Value = 0f;
                ClientHP.Value = 0f;
            }

            if (_enemyActors.Count > 0)
            {
                BossMaxHP.Value = _enemyActors[0].Model.MaxHP;
                BossHP.Value = _enemyActors[0].Model.CurrentHP;
            }

            // 4. Create authoritative context and start loop
            var context = new CombatContext(_playerActors, _enemyActors);
            _authoritativeTurnManager.StartCombat(context, _fieldChars, hasBench: false);

            Debug.Log("[NetworkedTurnManager] Pure Domain Combat started autoritatively on Server.");
        }

        private void StartCombatSimulationLocal()
        {
            _fieldChars.Clear();
            _playerActors.Clear();
            _enemyActors.Clear();

            // 1. Create Player 1 (Host)
            var hostActor = new PlayerActor(playerCharacterData);
            _playerActors.Add(hostActor);
            _fieldChars.Add(playerCharacterData);

            // 2. Create Player 2 (Client)
            var clientChar = (clientCharacterData != null) ? clientCharacterData : playerCharacterData;
            if ((IsServer && NetworkManager.Singleton.ConnectedClientsList.Count > 1) || (!IsServer && IsClient && ClientMaxHP.Value > 0f))
            {
                var clientActor = new PlayerActor(clientChar);
                _playerActors.Add(clientActor);
                _fieldChars.Add(clientChar);
            }

            // 3. Create Boss (Orco)
            var bossActor = new EnemyAgent(bossEnemyData);
            _enemyActors.Add(bossActor);

            // 4. Create local Context
            var context = new CombatContext(_playerActors, _enemyActors);
            
            // Initialize the Card Hand locally for the local player only!
            var localFieldChars = new List<CharacterData>();
            if (IsServer)
            {
                localFieldChars.Add(playerCharacterData);
            }
            else
            {
                localFieldChars.Add(clientCharacterData != null ? clientCharacterData : playerCharacterData);
            }

            var pool = new CardPool(localFieldChars, new System.Random());
            var hand = new CombatHand(pool, 2, hasBench: false);

            // Programmatically inject context and hand into local _domainTurnManager
            _domainTurnManager.SetContextAndHandNetworked(context, hand);

            // Deal cards locally!
            hand.Deal(localFieldChars);

            Debug.Log($"[NetworkedTurnManager] [Local Hand Init] IsServer={IsServer}, IsClient={IsClient}, localFieldChars.Count={localFieldChars.Count}");
            for (int i = 0; i < localFieldChars.Count; i++)
            {
                var c = localFieldChars[i];
                if (c != null && c.skill1 == null && c.skill2 == null)
                {
                    Debug.LogWarning($"[NetworkedTurnManager] Character {c.characterName} has NO skills assigned! Injecting Ice Queen skills as fallback to prevent crash.");
                    if (playerCharacterData != null)
                    {
                        c.skill1 = playerCharacterData.skill1;
                        c.skill2 = playerCharacterData.skill2;
                    }
                }
                Debug.Log($"[NetworkedTurnManager]   Char {i}: {(c != null ? c.name : "null")}, skill1={(c != null && c.skill1 != null ? c.skill1.skillName : "null")}, skill2={(c != null && c.skill2 != null ? c.skill2.skillName : "null")}");
            }

            // Dynamically set up the visual presentation layers
            EnsurePresentationComponents();

            // Catch up to current phase in case we missed the network event (e.g. late join or initialization order)
            if (CurrentPhase.Value == CombatPhase.PlayerTurn)
            {
                LocalStartPlayerTurn(RoundNumber.Value);
            }
        }

        private void EnsurePresentationComponents()
        {
            var bootstrapper = FindFirstObjectByType<CombatBootstrapper>();
            if (bootstrapper == null)
            {
                Debug.Log("[NetworkedTurnManager] Programmatic Presentation Components setup initiated...");
                
                var go = gameObject; // NetworkedTurnManager GameObject
                
                // 1. Add CombatCameraController to Main Camera if missing
                var mainCam = Camera.main;
                CombatCameraController camCtrl = null;
                if (mainCam != null)
                {
                    camCtrl = mainCam.GetComponent<CombatCameraController>() ?? mainCam.gameObject.AddComponent<CombatCameraController>();
                }
                
                // 2. Add presentation components to NetworkedTurnManager
                var animDriver = go.GetComponent<CombatAnimationDriver>() ?? go.AddComponent<CombatAnimationDriver>();
                var camDirector = go.GetComponent<CombatCameraDirector>() ?? go.AddComponent<CombatCameraDirector>();
                var vfxPlayer = go.GetComponent<CombatVFXPlayer>() ?? go.AddComponent<CombatVFXPlayer>();
                
                // Add CombatBootstrapper
                bootstrapper = go.AddComponent<CombatBootstrapper>();
                
                // Wire fields
                bootstrapper.cameraController = camCtrl;
                bootstrapper.animationDriver = animDriver;
                bootstrapper.cameraDirector = camDirector;
                bootstrapper.vfxPlayer = vfxPlayer;
                
                // Load UI prefab programmatically
                string uiPrefabPath = "Assets/_Project/Systems/Combat/Prefabs/UI/CombatUI_Canvas.prefab";
                #if UNITY_EDITOR
                var uiPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(uiPrefabPath);
                typeof(CombatBootstrapper).GetField("_combatUIPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(bootstrapper, uiPrefab);
                #endif
            }
            
            // Now boot it!
            bootstrapper.InitializeFromNetwork(this);
        }

        private void Update()
        {
            if (!IsServer || !_isTimerActive) return;

            // Tick turn timer
            TurnTimer.Value -= Time.deltaTime;
            if (TurnTimer.Value <= 0f)
            {
                TurnTimer.Value = 0f;
                _isTimerActive = false;
                
                // End player phase and process actions automatically on time out
                EndPlayerPhase();
            }
        }

        private void HandlePlayerTurnBegin(int round)
        {
            RoundNumber.Value = round;
            CurrentPhase.Value = CombatPhase.PlayerTurn;
            TurnTimer.Value = 30f; // Reset to 30 seconds
            _isTimerActive = true;
            _playerFinishedTurn.Clear();
            _queuedActions.Clear();
            ReplicatedActions.Value = new NetworkedActionState();
            
            // Trigger local start on Host
            LocalStartPlayerTurn(round);
            
            Debug.Log($"[NetworkedTurnManager] Round {round} Player turn started. Timer reset.");
        }

        private void HandleEnemyTurnStarted()
        {
            _isTimerActive = false;
            CurrentPhase.Value = CombatPhase.EnemyTurn;
            
            Debug.Log("[NetworkedTurnManager] Transitioning to Enemy turn phase.");
        }

        private void HandleDomainActionPending(PendingAction pending)
        {
            // Authoritative is bypassed
        }

        [ClientRpc]
        private void BroadcastActionPendingClientRpc(ulong casterNetId, ulong targetNetId, string skillName, int rank, bool isUltimate)
        {
            ICombatActor caster = GetActorFromNetworkId(casterNetId);
            ICombatActor target = GetActorFromNetworkId(targetNetId);
            
            SkillData skill = GetSkillByName(skillName);
            UltimateData ultimate = GetUltimateByName(skillName);
            
            var pending = new PendingAction(
                caster: caster,
                target: target,
                skill: skill,
                ultimate: ultimate,
                rank: rank,
                targetType: skill != null ? skill.targetType : TargetType.SingleEnemy,
                isUltimate: isUltimate
            );
            
            _domainTurnManager.RaiseActionPendingNetworked(pending);
        }

        private void HandleDomainActionResolved(CombatActionResult result)
        {
            // Authoritative is bypassed
        }

        [ClientRpc]
        private void BroadcastActionResolvedClientRpc(ulong casterNetId, ulong targetNetId, string skillName, int rank, float damageDealt, bool isCrit, bool isAoe)
        {
            ICombatActor caster = GetActorFromNetworkId(casterNetId);
            ICombatActor target = GetActorFromNetworkId(targetNetId);
            
            SkillData skill = GetSkillByName(skillName);
            
            var result = new CombatActionResult(
                caster: caster,
                target: target,
                skill: skill,
                rank: rank,
                damageDealt: damageDealt,
                isCrit: isCrit,
                lifeStealApplied: 0f,
                healApplied: 0f,
                isAoe: isAoe
            );
            
            _domainTurnManager.RaiseActionResolvedNetworked(result);
        }

        private void HandleDomainGaugeChanged(ICombatActor actor, int orbs)
        {
            ulong actorNetId = GetNetworkIdForActor(actor);
            BroadcastGaugeChangedClientRpc(actorNetId, orbs);
        }

        [ClientRpc]
        private void BroadcastGaugeChangedClientRpc(ulong actorNetId, int orbs)
        {
            ICombatActor actor = GetActorFromNetworkId(actorNetId);
            _domainTurnManager.RaiseGaugeChangedNetworked(actor, orbs);
        }

        private void HandleDomainCombatEnded(bool playerWon)
        {
            BroadcastCombatEndedClientRpc(playerWon);
        }

        [ClientRpc]
        private void BroadcastCombatEndedClientRpc(bool playerWon)
        {
            _domainTurnManager.RaiseCombatEndedNetworked(playerWon);
        }

        // ── Client Interception ServerRpcs ───────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void SubmitSkillFromClientServerRpc(int skillType, int rank, ulong targetNetId, ulong casterNetId, ServerRpcParams rpcParams = default)
        {
            if (!IsServer || CurrentPhase.Value != CombatPhase.PlayerTurn) return;
            QueuePlayerActionOnServer(1, casterNetId, skillType, rank, targetNetId);
        }

        public void SubmitSkillFromHostServer(int skillType, int rank, ulong targetNetId, ulong casterNetId)
        {
            if (!IsServer || CurrentPhase.Value != CombatPhase.PlayerTurn) return;
            QueuePlayerActionOnServer(0, casterNetId, skillType, rank, targetNetId);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitMoveFromClientServerRpc(int cardId, int toIndex, ulong casterNetId, ServerRpcParams rpcParams = default)
        {
            if (!IsServer || CurrentPhase.Value != CombatPhase.PlayerTurn) return;
            QueuePlayerMoveOnServer(1, casterNetId);
        }

        public void SubmitMoveFromHostServer(int fromIndex, int toIndex, ulong casterNetId)
        {
            if (!IsServer || CurrentPhase.Value != CombatPhase.PlayerTurn) return;
            QueuePlayerMoveOnServer(0, casterNetId);
        }

        public void EndPlayerTurnFromHostServer()
        {
            if (!IsServer || CurrentPhase.Value != CombatPhase.PlayerTurn) return;
            PlayerFinishedTurn(0);
        }

        [ServerRpc(RequireOwnership = false)]
        public void EndPlayerTurnFromClientServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer || CurrentPhase.Value != CombatPhase.PlayerTurn) return;
            PlayerFinishedTurn(1);
        }

        // ── Client Reactive Receivers ────────────────────────────────────────────

        private void HandlePhaseChangedOnClient(CombatPhase oldPhase, CombatPhase newPhase)
        {
            if (IsServer) return;
            if (newPhase == CombatPhase.PlayerTurn)
            {
                LocalStartPlayerTurn(RoundNumber.Value);
            }
            else if (newPhase == CombatPhase.EnemyTurn)
            {
                _domainTurnManager.SetPhaseNetworked(newPhase);
                _domainTurnManager.RaiseEnemyTurnStartedNetworked();
            }
            else
            {
                _domainTurnManager.SetPhaseNetworked(newPhase);
            }
        }

        private void HandleRoundChangedOnClient(int oldRound, int newRound)
        {
            if (IsServer) return;
            _domainTurnManager.SetRoundNetworked(newRound);
        }

        private void HandleActionsRemainingChangedOnClient(int oldVal, int newVal)
        {
            if (IsServer) return;
            _domainTurnManager.SyncActionsRemainingNetworked(newVal);
        }

        private void HandleHostHPChanged(float oldHP, float newHP)
        {
            if (IsServer) return;
            if (_playerActors.Count > 0 && _playerActors[0]?.Model != null)
            {
                _playerActors[0].Model.SyncHP(newHP);
            }
        }

        private void HandleClientHPChanged(float oldHP, float newHP)
        {
            if (IsServer) return;
            if (_playerActors.Count > 1 && _playerActors[1]?.Model != null)
            {
                _playerActors[1].Model.SyncHP(newHP);
            }
        }

        private void HandleBossHPChanged(float oldHP, float newHP)
        {
            if (IsServer) return;
            if (_enemyActors.Count > 0 && _enemyActors[0]?.Model != null)
            {
                _enemyActors[0].Model.SyncHP(newHP);
            }
        }

        private void HandleHandStateChangedOnClient(NetworkedHand oldHand, NetworkedHand newHand)
        {
            // Fully local hand strategy — hand synchronization is bypassed
        }

        // ── Legacy methods kept for compatibility ──────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void PlayCardServerRpc(int skillType, int rank, ulong targetNetId, ServerRpcParams rpcParams = default)
        {
            // Legacy RPC kept for interface compatibility
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitActionServerRpc(int cardIndex, ulong targetNetId, ServerRpcParams rpcParams = default)
        {
            // Legacy RPC kept for interface compatibility
        }



        [ClientRpc]
        private void BroadcastActionClientRpc(ulong casterNetId, ulong targetNetId, string skillName, float damageDealt, bool isCrit)
        {
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(casterNetId, out var casterNetObj);
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetId, out var targetNetObj);

            if (casterNetObj != null && targetNetObj != null)
            {
                // 1. Play attack animation trigger on visual pawn
                var animator = casterNetObj.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.SetTrigger("Attack");
                }

                // 2. Play impact flash/animation on target visual pawn
                var targetAnimator = targetNetObj.GetComponentInChildren<Animator>();
                if (targetAnimator != null)
                {
                    targetAnimator.SetTrigger("GetHit");
                }

                Debug.Log($"[NetworkedTurnManager] Synchronised Client Visual: {casterNetObj.name} played {skillName} dealing {damageDealt} (Crit: {isCrit}) to {targetNetObj.name}");
            }
        }

        // ── Helper Resolvers ──────────────────────────────────────────────────────

        public ulong GetNetworkIdForActor(ICombatActor actor)
        {
            if (actor == null) return 0;

            var spawnedObjects = NetworkManager.Singleton.SpawnManager.SpawnedObjects;
            foreach (var kvp in spawnedObjects)
            {
                var pawn = kvp.Value.GetComponent<NetworkedCombatPawn>();
                if (pawn != null)
                {
                    if (pawn.Role.Value == CombatRole.Boss && _enemyActors.Count > 0 && actor == _enemyActors[0]) return kvp.Key;
                    if (pawn.Role.Value == CombatRole.HostPlayer && _playerActors.Count > 0 && actor == _playerActors[0]) return kvp.Key;
                    if (pawn.Role.Value == CombatRole.ClientPlayer && _playerActors.Count > 1 && actor == _playerActors[1]) return kvp.Key;
                }
            }
            return 0;
        }

        public TurnManager GetLocalTurnManager() => _domainTurnManager;
        
        public CombatContext GetCombatContext() => _domainTurnManager != null ? _domainTurnManager.Context : null;
        
        public ICombatActor GetHostActor() => _playerActors.Count > 0 ? _playerActors[0] : null;

        public Dictionary<ICombatActor, Transform> GetActorPawns()
        {
            var dict = new Dictionary<ICombatActor, Transform>();
            var spawnedObjects = NetworkManager.Singleton.SpawnManager.SpawnedObjects;
            foreach (var kvp in spawnedObjects)
            {
                var pawn = kvp.Value.GetComponent<NetworkedCombatPawn>();
                if (pawn != null)
                {
                    if (pawn.Role.Value == CombatRole.Boss && _enemyActors.Count > 0)
                    {
                        dict[_enemyActors[0]] = pawn.transform;
                    }
                    else if (pawn.Role.Value == CombatRole.HostPlayer && _playerActors.Count > 0)
                    {
                        dict[_playerActors[0]] = pawn.transform;
                    }
                    else if (pawn.Role.Value == CombatRole.ClientPlayer && _playerActors.Count > 1)
                    {
                        dict[_playerActors[1]] = pawn.transform;
                    }
                }
            }
            return dict;
        }

        private ICombatActor GetActorFromNetworkId(ulong netId)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netId, out var netObj))
            {
                var pawn = netObj.GetComponent<NetworkedCombatPawn>();
                if (pawn != null)
                {
                    switch (pawn.Role.Value)
                    {
                        case CombatRole.Boss: return _enemyActors.Count > 0 ? _enemyActors[0] : null;
                        case CombatRole.HostPlayer: return _playerActors.Count > 0 ? _playerActors[0] : null;
                        case CombatRole.ClientPlayer: return _playerActors.Count > 1 ? _playerActors[1] : (_playerActors.Count > 0 ? _playerActors[0] : null);
                    }
                }
            }
            return _enemyActors.Count > 0 ? _enemyActors[0] : null; // Fallback to target Boss
        }

        // ── Hand Synchronisation and Asset Resolvers ──────────────────────────────────

        private void UpdateHandState()
        {
            // Fully local hand strategy — hand synchronization is bypassed
        }

        public SkillData GetSkillByName(string name)
        {
            if (playerCharacterData != null)
            {
                if (playerCharacterData.skill1 != null && (playerCharacterData.skill1.skillName == name || playerCharacterData.skill1.name == name)) return playerCharacterData.skill1;
                if (playerCharacterData.skill2 != null && (playerCharacterData.skill2.skillName == name || playerCharacterData.skill2.name == name)) return playerCharacterData.skill2;
            }
            if (clientCharacterData != null)
            {
                if (clientCharacterData.skill1 != null && (clientCharacterData.skill1.skillName == name || clientCharacterData.skill1.name == name)) return clientCharacterData.skill1;
                if (clientCharacterData.skill2 != null && (clientCharacterData.skill2.skillName == name || clientCharacterData.skill2.name == name)) return clientCharacterData.skill2;
            }
            return null;
        }

        public UltimateData GetUltimateByName(string name)
        {
            if (playerCharacterData != null && playerCharacterData.ultimate != null && (playerCharacterData.ultimate.ultimateName == name || playerCharacterData.ultimate.name == name)) 
                return playerCharacterData.ultimate;
            if (clientCharacterData != null && clientCharacterData.ultimate != null && (clientCharacterData.ultimate.ultimateName == name || clientCharacterData.ultimate.name == name)) 
                return clientCharacterData.ultimate;
            return null;
        }

        private CharacterData GetCharacterDataForSkill(SkillData skill, UltimateData ultimate)
        {
            if (skill != null)
            {
                if (playerCharacterData != null && (playerCharacterData.skill1 == skill || playerCharacterData.skill2 == skill))
                    return playerCharacterData;
                if (clientCharacterData != null && (clientCharacterData.skill1 == skill || clientCharacterData.skill2 == skill))
                    return clientCharacterData;
            }
            if (ultimate != null)
            {
                if (playerCharacterData != null && playerCharacterData.ultimate == ultimate)
                    return playerCharacterData;
                if (clientCharacterData != null && clientCharacterData.ultimate == ultimate)
                    return clientCharacterData;
            }
            return null;
        }

        // ── Cooperative Simulation Debug helpers ──────────────────────────────────────

        public void SimulateCardPlay(ulong clientId)
        {
            // Debug helper bypassed
        }

        private void HandleReplicatedActionsChanged(NetworkedActionState oldState, NetworkedActionState newState)
        {
            if (IsServer) return;
            SyncActionSlotsToPresenter(newState);
        }

        private void SyncActionSlotsToPresenter(NetworkedActionState state)
        {
            var bootstrapper = FindFirstObjectByType<CombatBootstrapper>();
            var presenter = bootstrapper != null ? bootstrapper.presenter as CombatHUDPresenter : null;
            if (presenter != null)
            {
                bool[] isActive = new bool[6];
                ulong[] clientIds = new ulong[6];
                int[] skillTypes = new int[6];
                int[] ranks = new int[6];
                int activeCount = 0;

                for (int i = 0; i < 6; i++)
                {
                    NetworkedActionSlot action = i switch
                    {
                        0 => state.Slot0,
                        1 => state.Slot1,
                        2 => state.Slot2,
                        3 => state.Slot3,
                        4 => state.Slot4,
                        _ => state.Slot5
                    };

                    isActive[i] = action.IsActive;
                    if (action.IsActive) activeCount++;
                    clientIds[i] = action.ClientId;
                    skillTypes[i] = action.SkillType;
                    ranks[i] = action.Rank;
                }

                if (_domainTurnManager?.Hand != null)
                {
                    _domainTurnManager.Hand.ActionsRemaining = 6 - activeCount;
                    presenter.RefreshCardHand(animate: false);
                }

                presenter.SyncMultiplayerActionSlots(isActive, clientIds, skillTypes, ranks, this);
            }
        }

        private void LocalStartPlayerTurn(int round)
        {
            if (_domainTurnManager == null || _domainTurnManager.Hand == null) return;

            // Sync visual phase and round number locally FIRST
            // This is critical because Hand.Refill() triggers OnHandChanged, which the UI uses to evaluate
            // 'canAct' based on the Current Phase. If Phase is still EnemyTurn, buttons are locked!
            _domainTurnManager.SetRoundNetworked(round);
            _domainTurnManager.SetPhaseNetworked(CombatPhase.PlayerTurn);

            // Reset actions and refill visual hand locally!
            _domainTurnManager.Hand.ResetActions();
            _domainTurnManager.Hand.Refill();

            // Trigger visual transitions and draw animation
            _domainTurnManager.RaisePlayerTurnBeginNetworked(round);
            _domainTurnManager.RaisePlayerTurnStartedNetworked(round);

            Debug.Log($"[NetworkedTurnManager] Local visual Player Turn started for round {round}. Hand size = {_domainTurnManager.Hand.Slots.Count}");
        }

        [ClientRpc]
        private void StartActionExecutionClientRpc(ulong[] casters, ulong[] targets, int[] skills, int[] ranks)
        {
            StartLocalActionExecution(casters, targets, skills, ranks);
        }

        private void TriggerActionExecutionOnBoth()
        {
            int count = _queuedActions.Count;
            ulong[] casters = new ulong[count];
            ulong[] targets = new ulong[count];
            int[] skills = new int[count];
            int[] ranks = new int[count];

            for (int i = 0; i < count; i++)
            {
                var action = _queuedActions[i];
                casters[i] = GetNetworkIdForActor(action.Caster);
                targets[i] = GetNetworkIdForActor(action.Target);
                skills[i] = action.SkillType;
                ranks[i] = action.Rank;
            }

            // Trigger locally on Host visual proxy
            StartLocalActionExecution(casters, targets, skills, ranks);

            // Broadcast to Client
            StartActionExecutionClientRpc(casters, targets, skills, ranks);
        }

        private void StartLocalActionExecution(ulong[] casters, ulong[] targets, int[] skills, int[] ranks)
        {
            if (_domainTurnManager == null || animationDriver == null) return;

            // Hide HUD UI locally during resolution
            var presenter = FindFirstObjectByType<CombatHUDPresenter>();
            presenter?.HideAllUI();

            // Clear visual queue
            var animQueueField = typeof(CombatAnimationDriver).GetField("_animQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var animQueue = animQueueField?.GetValue(animationDriver) as Queue<PendingAction>;
            animQueue?.Clear();

            // Reconstruct actions
            int activeCount = 0;
            for (int i = 0; i < casters.Length; i++)
            {
                var caster = GetActorFromNetworkId(casters[i]);
                var target = GetActorFromNetworkId(targets[i]);
                if (caster == null) continue;

                int skillType = skills[i];
                int rank = ranks[i];
                
                var cd = (caster is PlayerActor pa) ? _fieldChars[_playerActors.IndexOf(pa)] : null;
                if (cd == null) cd = playerCharacterData; // fallback

                SkillData skill = null;
                UltimateData ultimate = null;
                bool isUltimate = skillType == 2;
                if (isUltimate) ultimate = cd.ultimate;
                else if (skillType == 0) skill = cd.skill1;
                else if (skillType == 1) skill = cd.skill2;

                if (skillType == 3)
                {
                    // Dummy MOVE action
                    var dummyMove = new PendingAction(
                        caster: caster,
                        target: caster,
                        skill: null,
                        ultimate: null,
                        rank: 0,
                        targetType: TargetType.Self,
                        isUltimate: false
                    );
                    animationDriver.Enqueue(dummyMove);
                    activeCount++;
                }
                else
                {
                    var pending = new PendingAction(
                        caster: caster,
                        target: target,
                        skill: skill,
                        ultimate: ultimate,
                        rank: rank,
                        targetType: isUltimate ? (ultimate != null ? ultimate.targetType : TargetType.SingleEnemy) : (skill != null ? skill.targetType : TargetType.SingleEnemy),
                        isUltimate: isUltimate
                    );
                    animationDriver.Enqueue(pending);
                    activeCount++;
                }
            }

            Debug.Log($"[NetworkedTurnManager] Local execution started. Enqueued {activeCount} actions visually.");

            // Start playing the queued animations
            animationDriver.PlayQueuedAnimations(onComplete: null, fadeSlots: true);
        }

        private ulong GetHostActorNetId()
        {
            var spawnedObjects = NetworkManager.Singleton.SpawnManager.SpawnedObjects;
            foreach (var kvp in spawnedObjects)
            {
                var pawn = kvp.Value.GetComponent<NetworkedCombatPawn>();
                if (pawn != null && pawn.Role.Value == CombatRole.HostPlayer) return kvp.Key;
            }
            return 0;
        }

        private ulong GetClientActorNetId()
        {
            var spawnedObjects = NetworkManager.Singleton.SpawnManager.SpawnedObjects;
            foreach (var kvp in spawnedObjects)
            {
                var pawn = kvp.Value.GetComponent<NetworkedCombatPawn>();
                if (pawn != null && pawn.Role.Value == CombatRole.ClientPlayer) return kvp.Key;
            }
            return 0;
        }

        private void UpdateAuthoritativeHPs()
        {
            if (!IsServer) return;

            BossHP.Value = _enemyActors[0].Model.CurrentHP;
            HostHP.Value = _playerActors[0].Model.CurrentHP;
            if (_playerActors.Count > 1)
            {
                ClientHP.Value = _playerActors[1].Model.CurrentHP;
            }
        }

        private void EndPlayerPhase()
        {
            if (CurrentPhase.Value != CombatPhase.PlayerTurn) return;
            CurrentPhase.Value = CombatPhase.Idle;
            _isTimerActive = false;
            StartCoroutine(AuthoritativeTurnResolutionCo());
        }

        private void QueuePlayerActionOnServer(ulong clientId, ulong casterNetId, int skillType, int rank, ulong targetNetId)
        {
            var caster = GetActorFromNetworkId(casterNetId);
            if (caster == null)
            {
                if (clientId == 1 && _playerActors.Count <= 1) return;
                caster = clientId == 0 ? _playerActors[0] : _playerActors[1];
            }
            var target = GetActorFromNetworkId(targetNetId);

            var cd = (caster is PlayerActor pa) ? _fieldChars[_playerActors.IndexOf(pa)] : null;
            if (cd == null) cd = playerCharacterData;

            SkillData skill = null;
            UltimateData ultimate = null;
            bool isUltimate = skillType == 2;

            if (isUltimate) ultimate = cd.ultimate;
            else if (skillType == 0) skill = cd.skill1;
            else if (skillType == 1) skill = cd.skill2;

            var action = new PendingServerAction
            {
                Caster = caster,
                Target = target,
                Skill = skill,
                Ultimate = ultimate,
                Rank = rank,
                TargetType = isUltimate ? (ultimate != null ? ultimate.targetType : TargetType.SingleEnemy) : (skill != null ? skill.targetType : TargetType.SingleEnemy),
                IsUltimate = isUltimate,
                SkillType = skillType
            };

            _queuedActions.Add(action);
            Debug.Log($"[NetworkedTurnManager] Queued action: Caster={caster.Name}, Skill/Ult={skill?.skillName ?? ultimate?.ultimateName}, Rank={rank}, Target={target?.Name}");

            UpdateReplicatedActionsOnServer(clientId, skillType, rank);
        }

        private void QueuePlayerMoveOnServer(ulong clientId, ulong casterNetId)
        {
            var caster = GetActorFromNetworkId(casterNetId);
            if (caster == null)
            {
                if (clientId == 1 && _playerActors.Count <= 1) return;
                caster = clientId == 0 ? _playerActors[0] : _playerActors[1];
            }

            var action = new PendingServerAction
            {
                Caster = caster,
                Target = null,
                Skill = null,
                Ultimate = null,
                Rank = 0,
                TargetType = TargetType.Self,
                IsUltimate = false,
                SkillType = 3
            };

            _queuedActions.Add(action);
            Debug.Log($"[NetworkedTurnManager] Queued MOVE action for Client {clientId}");

            UpdateReplicatedActionsOnServer(clientId, 3, 0);
        }

        private void UpdateReplicatedActionsOnServer(ulong clientId, int skillType, int rank)
        {
            if (!IsServer) return;

            var state = ReplicatedActions.Value;

            if (!state.Slot0.IsActive)
            {
                state.Slot0.IsActive = true;
                state.Slot0.ClientId = clientId;
                state.Slot0.SkillType = skillType;
                state.Slot0.Rank = rank;
            }
            else if (!state.Slot1.IsActive)
            {
                state.Slot1.IsActive = true;
                state.Slot1.ClientId = clientId;
                state.Slot1.SkillType = skillType;
                state.Slot1.Rank = rank;
            }
            else if (!state.Slot2.IsActive)
            {
                state.Slot2.IsActive = true;
                state.Slot2.ClientId = clientId;
                state.Slot2.SkillType = skillType;
                state.Slot2.Rank = rank;
            }
            else if (!state.Slot3.IsActive)
            {
                state.Slot3.IsActive = true;
                state.Slot3.ClientId = clientId;
                state.Slot3.SkillType = skillType;
                state.Slot3.Rank = rank;
            }
            else if (!state.Slot4.IsActive)
            {
                state.Slot4.IsActive = true;
                state.Slot4.ClientId = clientId;
                state.Slot4.SkillType = skillType;
                state.Slot4.Rank = rank;
            }
            else if (!state.Slot5.IsActive)
            {
                state.Slot5.IsActive = true;
                state.Slot5.ClientId = clientId;
                state.Slot5.SkillType = skillType;
                state.Slot5.Rank = rank;
            }

            ReplicatedActions.Value = state;
            SyncActionSlotsToPresenter(state);

            // Automatically transition to the next phase if all 6 slots are filled
            if (state.Slot5.IsActive && CurrentPhase.Value == CombatPhase.PlayerTurn)
            {
                Debug.Log("[NetworkedTurnManager] All 6 action slots filled. Automatically ending Player Phase.");
                EndPlayerPhase();
            }
        }

        private void PlayerFinishedTurn(ulong clientId)
        {
            if (!IsServer) return;

            _playerFinishedTurn.Add(clientId);
            Debug.Log($"[NetworkedTurnManager] Player {clientId} finished turn. Total finished: {_playerFinishedTurn.Count}");

            CheckIfAllFinished();
        }

        private void CheckIfAllFinished()
        {
            if (!IsServer || CurrentPhase.Value != CombatPhase.PlayerTurn) return;

            bool hostDone = _playerFinishedTurn.Contains(0) || GetCasterActionCount(0) >= GetMaxActionsAllowed(0);
            bool clientConnected = NetworkManager.Singleton.ConnectedClientsList.Count > 1;
            bool clientDone = !clientConnected || _playerFinishedTurn.Contains(1) || GetCasterActionCount(1) >= GetMaxActionsAllowed(1);

            if (hostDone && clientDone)
            {
                Debug.Log("[NetworkedTurnManager] All players done. Starting turn resolution coroutine...");
                StartCoroutine(AuthoritativeTurnResolutionCo());
            }
        }

        private int GetMaxActionsAllowed(ulong clientId)
        {
            if (clientId == 0)
            {
                return (_playerActors.Count > 0 && _playerActors[0].IsAlive) ? 3 : 0;
            }
            else
            {
                return (_playerActors.Count > 1 && _playerActors[1].IsAlive) ? 3 : 0;
            }
        }

        private int GetCasterActionCount(ulong clientId)
        {
            int count = 0;
            foreach (var action in _queuedActions)
            {
                if (clientId == 0 && action.Caster == _playerActors[0]) count++;
                if (clientId == 1 && _playerActors.Count > 1 && action.Caster == _playerActors[1]) count++;
            }
            return count;
        }

        private IEnumerator AuthoritativeTurnResolutionCo()
        {
            _isTimerActive = false;

            // Trigger visual execution on both players
            TriggerActionExecutionOnBoth();

            int activeActionCount = _queuedActions.Count;

            // 1. Process players' queued actions sequentially on the Server
            for (int i = 0; i < _queuedActions.Count; i++)
            {
                var action = _queuedActions[i];

                if (action.Skill == null && action.Ultimate == null)
                {
                    // It's a MOVE action!
                    if (action.Caster != null && action.Caster.IsAlive)
                    {
                        _authoritativeTurnManager.RaiseGaugeChangedNetworked(action.Caster, 1);
                    }
                    yield return new WaitForSeconds(0.4f);
                    continue;
                }

                if (action.Caster == null || !action.Caster.IsAlive) continue;

                var pending = new PendingAction(
                    caster: action.Caster,
                    target: action.Target,
                    skill: action.Skill,
                    ultimate: action.Ultimate,
                    rank: action.Rank,
                    targetType: action.TargetType,
                    isUltimate: action.IsUltimate
                );

                _authoritativeTurnManager.ResolveAction(pending);
                UpdateAuthoritativeHPs();

                yield return new WaitForSeconds(1.5f);
            }

            _queuedActions.Clear();

            if (_authoritativeTurnManager.Context.IsOver)
            {
                _authoritativeTurnManager.FinishCombatNetworked();
                yield break;
            }

            // 2. Process enemy turn phase authoritatively
            Debug.Log("[NetworkedTurnManager] [Authoritative] Starting execution of Enemy actions sequentially...");

            var sortedEnemies = new List<ICombatActor>();
            foreach (var enemy in _enemyActors)
            {
                if (enemy.IsAlive) sortedEnemies.Add(enemy);
            }

            for (int i = 0; i < sortedEnemies.Count; i++)
            {
                var enemy = sortedEnemies[i];
                if (!enemy.IsAlive) continue;
                if (enemy.Effects.HasBehavior(EffectBehavior.SkipTurn)) continue;

                var player = GetAuthoritativeAlivePlayer();
                if (player == null) continue;

                if (enemy is IEnemyTurnHandler handler)
                {
                    var pending = handler.TakeTurn(_authoritativeTurnManager.Context, player);
                    if (pending.Caster != null)
                    {
                        ulong casterNetId = GetNetworkIdForActor(pending.Caster);
                        ulong targetNetId = GetNetworkIdForActor(pending.Target);
                        string skillName = pending.Skill != null ? pending.Skill.skillName : (pending.Ultimate != null ? pending.Ultimate.ultimateName : "Ataque");

                        BroadcastActionPendingClientRpc(casterNetId, targetNetId, skillName, pending.Rank, pending.IsUltimate);
                        _domainTurnManager.RaiseActionPendingNetworked(pending);

                        var results = _authoritativeTurnManager.ResolveAction(pending);
                        UpdateAuthoritativeHPs();

                        if (results != null && results.Length > 0)
                        {
                            var result = results[0];
                            BroadcastActionResolvedClientRpc(casterNetId, targetNetId, skillName, result.Rank, result.DamageDealt, result.IsCrit, result.IsAoe);
                            _domainTurnManager.RaiseActionResolvedNetworked(result);
                        }

                        yield return new WaitForSeconds(1.5f);
                    }
                }
            }

            if (_authoritativeTurnManager.Context.IsOver)
            {
                _authoritativeTurnManager.FinishCombatNetworked();
                yield break;
            }

            // 3. Conclude the round authoritatively
            _authoritativeTurnManager.EndOfRoundManually();
            _playerFinishedTurn.Clear();

            RoundNumber.Value = _authoritativeTurnManager.Round;
            LocalStartPlayerTurn(_authoritativeTurnManager.Round);

            CurrentPhase.Value = CombatPhase.PlayerTurn;
            TurnTimer.Value = 30f;
            _isTimerActive = true;
        }

        private ICombatActor GetAuthoritativeAlivePlayer()
        {
            if (_playerActors.Count > 0 && _playerActors[0].IsAlive) return _playerActors[0];
            if (_playerActors.Count > 1 && _playerActors[1].IsAlive) return _playerActors[1];
            return null;
        }

        private CombatAnimationDriver animationDriver => FindFirstObjectByType<CombatAnimationDriver>();
    }

    // ── Network Serializable Structs for Shared Card Hand ─────────────────────────────

    [System.Serializable]
    public struct NetworkedCard : INetworkSerializable, IEquatable<NetworkedCard>
    {
        public int Id;
        public int Rank;
        public int SkillType; // 0 = Skill1, 1 = Skill2, 2 = Ultimate
        public bool IsUltimate;
        public int ActorIndex;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Rank);
            serializer.SerializeValue(ref SkillType);
            serializer.SerializeValue(ref IsUltimate);
            serializer.SerializeValue(ref ActorIndex);
        }

        public bool Equals(NetworkedCard other)
        {
            return Id == other.Id && Rank == other.Rank && SkillType == other.SkillType && IsUltimate == other.IsUltimate && ActorIndex == other.ActorIndex;
        }
    }

    [System.Serializable]
    public struct NetworkedHand : INetworkSerializable, IEquatable<NetworkedHand>
    {
        public int CardCount;
        public NetworkedCard Card0;
        public NetworkedCard Card1;
        public NetworkedCard Card2;
        public NetworkedCard Card3;
        public NetworkedCard Card4;
        public NetworkedCard Card5;
        public NetworkedCard Card6;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref CardCount);
            serializer.SerializeValue(ref Card0);
            serializer.SerializeValue(ref Card1);
            serializer.SerializeValue(ref Card2);
            serializer.SerializeValue(ref Card3);
            serializer.SerializeValue(ref Card4);
            serializer.SerializeValue(ref Card5);
            serializer.SerializeValue(ref Card6);
        }

        public bool Equals(NetworkedHand other)
        {
            return CardCount == other.CardCount &&
                   Card0.Equals(other.Card0) &&
                   Card1.Equals(other.Card1) &&
                   Card2.Equals(other.Card2) &&
                   Card3.Equals(other.Card3) &&
                   Card4.Equals(other.Card4) &&
                   Card5.Equals(other.Card5) &&
                   Card6.Equals(other.Card6);
        }
    }

    [System.Serializable]
    public struct NetworkedActionSlot : INetworkSerializable, IEquatable<NetworkedActionSlot>
    {
        public bool IsActive;
        public ulong ClientId;
        public int SkillType; // 0 = Skill1, 1 = Skill2, 2 = Ultimate
        public int Rank;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref IsActive);
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref SkillType);
            serializer.SerializeValue(ref Rank);
        }

        public bool Equals(NetworkedActionSlot other)
        {
            return IsActive == other.IsActive && ClientId == other.ClientId && SkillType == other.SkillType && Rank == other.Rank;
        }
    }

    [System.Serializable]
    public struct NetworkedActionState : INetworkSerializable, IEquatable<NetworkedActionState>
    {
        public NetworkedActionSlot Slot0;
        public NetworkedActionSlot Slot1;
        public NetworkedActionSlot Slot2;
        public NetworkedActionSlot Slot3;
        public NetworkedActionSlot Slot4;
        public NetworkedActionSlot Slot5;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Slot0);
            serializer.SerializeValue(ref Slot1);
            serializer.SerializeValue(ref Slot2);
            serializer.SerializeValue(ref Slot3);
            serializer.SerializeValue(ref Slot4);
            serializer.SerializeValue(ref Slot5);
        }

        public bool Equals(NetworkedActionState other)
        {
            return Slot0.Equals(other.Slot0) &&
                   Slot1.Equals(other.Slot1) &&
                   Slot2.Equals(other.Slot2) &&
                   Slot3.Equals(other.Slot3) &&
                   Slot4.Equals(other.Slot4) &&
                   Slot5.Equals(other.Slot5);
        }
    }
}
