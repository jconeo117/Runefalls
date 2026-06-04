using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Runefall.Combat;
using Runefall.Enemies;
using Runefall.Data;
using Runefall.Core;
using Runefall.Presentation.Combat;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Runs on every client after BossFight loads.
    /// Reads the local player's CharacterData from the registry, builds a local TurnManager
    /// with that character's card pool, and wires the CombatUI_Canvas HUD.
    /// Each client sees only their own hand.
    /// </summary>
    public class MultiplayerLocalCombatSetup : MonoBehaviour
    {
        [SerializeField] private GameObject combatUIPrefab;
        [SerializeField] private MultiplayerCombatRegistry registry;
        [Tooltip("Shared combat base AnimatorController (Idle/Approach/Hit/Death + placeholder clips). " +
                 "Same asset assigned to CombatAnimationDriver.combatBaseController in SP. " +
                 "Drives PlayApproach/PlayHit/PlayReturn cross-fades on MP pawns.")]
        [SerializeField] private RuntimeAnimatorController combatBaseController;
        [Tooltip("FloatingDamage.prefab (Prefabs/UI) — world-space DamageNumber spawned on hits.")]
        [SerializeField] private GameObject floatingDamagePrefab;

        private TurnManager                  _tm;
        private CombatContext                _ctx;
        private GameObject                   _uiInstance;
        private CombatPresenterBase          _presenter;
        private MultiplayerActionSlotsSync   _syncRef;
        private ServerCombatOrchestrator     _orchestratorRef;

        private IEnumerator Start()
        {
            // Wait until NGO has assigned LocalClientId
            yield return new WaitUntil(() =>
                NetworkManager.Singleton != null &&
                (NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsHost));

#if UNITY_EDITOR
            if (registry == null)
                registry = UnityEditor.AssetDatabase.LoadAssetAtPath<MultiplayerCombatRegistry>(
                    "Assets/_Project/Systems/Combat/ScriptableObjects/Combat/MultiplayerCombatRegistry.asset");
#endif

            if (registry == null)
            {
                Debug.LogError("[LocalCombatSetup] Registry no encontrado.");
                yield break;
            }

            ulong localId  = NetworkManager.Singleton.LocalClientId;
            int   slot     = (int)localId;
            var   chars    = registry.characters;
            var   enemies  = registry.enemies;

            if (chars == null || slot >= chars.Count || chars[slot] == null)
            {
                Debug.LogError($"[LocalCombatSetup] No CharacterData para slot {slot}.");
                yield break;
            }

            var charData  = chars[slot];
            var enemyData = (enemies != null && enemies.Count > 0) ? enemies[0] : null;

            Debug.Log($"[LocalCombatSetup] ClientId {localId} → '{charData.characterName}'");

            // Build domain
            var player    = new PlayerActor(charData);
            var enemyList = new List<ICombatActor>();
            if (enemyData != null) enemyList.Add(new EnemyAgent(enemyData));

            _ctx = new CombatContext(new List<ICombatActor> { player }, enemyList);
            _tm  = new TurnManager();

            // Instantiate UI locally (not networked)
            if (combatUIPrefab == null)
            {
                Debug.LogError("[LocalCombatSetup] combatUIPrefab no asignado.");
                yield break;
            }

            _uiInstance = Instantiate(combatUIPrefab);
            _presenter  = _uiInstance.GetComponent<CombatPresenterBase>();

            if (_presenter == null)
            {
                Debug.LogError("[LocalCombatSetup] CombatPresenterBase no encontrado en combatUIPrefab.");
                yield break;
            }

            // Block TurnManager's internal auto-advance (NotifyActionsExhausted fallback).
            // In MP, turn end is server-authoritative — fires via OnAllPlayersExhausted RPC.
            _tm.OnPlayerActionsExhausted += () => { };

            // Wire TurnManager → Presenter (card hand only — slots handled by MultiplayerActionSlotsPresenter)
            _tm.OnPlayerTurnStarted += round =>
            {
                // Server resets shared slot board at start of each player turn
                if (NetworkManager.Singleton.IsServer)
                    MultiplayerActionSlotsSync.Instance?.ResetSlotsServerRpc();
                _presenter.OnPlayerTurnStarted(round);
            };
            _tm.OnGaugeChanged  += (actor, orbs) => _presenter.OnGaugeChanged(actor, orbs);
            _tm.OnMergeOccurred += (name, rank)   => _presenter.OnCardMerged(name, rank);
            _tm.OnActionResolved += result         => _presenter.OnActionResolved(result);
            _tm.OnCombatEnded   += won             => _presenter.OnCombatEnded(won);

            // Wire MultiplayerActionSlotsPresenter → LocalTurnManager + Registry
            var slotsPresenter = _uiInstance.GetComponent<MultiplayerActionSlotsPresenter>();
            if (slotsPresenter != null)
            {
                slotsPresenter.LocalTurnManager = _tm;
                slotsPresenter.Registry         = registry;
            }

            // If the prefab uses the MP-aware HUD presenter, inject the slots presenter
            var mpHud = _uiInstance.GetComponent<MultiplayerCombatHUDPresenter>();
            if (mpHud != null && slotsPresenter != null)
                mpHud.InjectSlotsPresenter(slotsPresenter);

            _presenter.Initialize(_tm, _ctx);

            // Start combat immediately — deals local card hand.
            _tm.StartCombat(_ctx, new List<CharacterData> { charData }, hasBench: false);

            // Animation controller — drives pawn animations in response to server RPCs.
            var animCtrlGo = new GameObject("MultiplayerAnimationController");
            var animCtrl   = animCtrlGo.AddComponent<MultiplayerAnimationController>();
            animCtrl.Initialize(localId, combatBaseController, registry);

            // HP controller — manages world-space player bars + boss screen bar.
            var hpCtrlGo = new GameObject("MultiplayerHPController");
            var hpCtrl   = hpCtrlGo.AddComponent<MultiplayerHPController>();
            var hudCanvas = _uiInstance != null ? _uiInstance.GetComponentInChildren<Canvas>() : null;
            hpCtrl.Initialize(hudCanvas, floatingDamagePrefab);

            Debug.Log($"[LocalCombatSetup] ✅ HUD listo para '{charData.characterName}'. Cartas en mano: {_tm.Hand?.Slots?.Count}");

            // Subscribe to OnAllPlayersExhausted AFTER Instance is guaranteed to exist.
            // Cannot check Instance inline here — client may not have received the
            // network spawn yet. Coroutine waits until it arrives.
            StartCoroutine(SubscribeWhenSyncReady());
        }

        private IEnumerator SubscribeWhenSyncReady()
        {
            // Slots exhausted → visual only (animate slots mini). Turn advancement is
            // server-authoritative via ServerCombatOrchestrator.OnBeginNewPlayerTurn.
            yield return new WaitUntil(() => MultiplayerActionSlotsSync.Instance != null);
            _syncRef = MultiplayerActionSlotsSync.Instance;
            _syncRef.OnAllPlayersExhausted += OnSlotsExhausted;
            Debug.Log($"[LocalCombatSetup] Suscrito a OnAllPlayersExhausted (SlotCount={_syncRef.SlotCount})");

            // New player turn signal comes from the orchestrator after enemy phase completes.
            yield return new WaitUntil(() => ServerCombatOrchestrator.Instance != null);
            _orchestratorRef = ServerCombatOrchestrator.Instance;
            _orchestratorRef.OnBeginNewPlayerTurn += OnNewPlayerTurn;
            Debug.Log("[LocalCombatSetup] Suscrito a OnBeginNewPlayerTurn.");
        }

        private void OnSlotsExhausted()
        {
            // Visual only: shrink slots while server resolves cards + enemy turn.
            _presenter?.SetActionSlotsActive(false);
        }

        private void OnNewPlayerTurn(int round)
        {
            _presenter?.SetActionSlotsActive(true);
            _tm?.ForceNewPlayerTurn(round);
        }

        private void OnDestroy()
        {
            if (_syncRef != null)
                _syncRef.OnAllPlayersExhausted -= OnSlotsExhausted;
            if (_orchestratorRef != null)
                _orchestratorRef.OnBeginNewPlayerTurn -= OnNewPlayerTurn;
        }
    }
}
