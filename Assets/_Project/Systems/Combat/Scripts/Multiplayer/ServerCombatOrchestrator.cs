using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Server-only orchestrator for the full combat turn loop in multiplayer.
    ///
    /// Turn sequence (server-driven):
    ///   Phase 1 — Players fill action slots (client-side, tracked by ActionSlotsSync)
    ///   Phase 2 — Card resolution: server iterates slots sequentially, signals each client
    ///             to animate, waits for completion, resolves damage, broadcasts HP.
    ///   Phase 3 — Enemy turn: server runs AI per enemy, broadcasts attack, resolves damage.
    ///   Phase 4 — New player turn: resets slots, signals all clients to deal new hands.
    ///
    /// Clients subscribe to the C# events (fired by ClientRpcs) to drive local UI/animation.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class ServerCombatOrchestrator : NetworkBehaviour
    {
        // ── Tuning ─────────────────────────────────────────────────────────────
        // Attacks each enemy performs per enemy turn (boss is meant to hit 3 times).
        private const int   EnemyAttacksPerTurn  = 3;
        private const float BetweenAttacksDelay  = 0.25f;

        // ── Singleton ──────────────────────────────────────────────────────────
        public static ServerCombatOrchestrator Instance { get; private set; }

        // ── Server state ───────────────────────────────────────────────────────
        private ServerCombatContext       _ctx;
        private MultiplayerCombatRegistry _registry;
        private bool                      _cardAnimDone;
        private bool                      _enemyAnimDone;
        private ulong                     _expectedCardOwner;

        // Impact gating: each reported impact frame (AE / projectile arrival) applies one hit.
        // A 3-AE skill deals 3 hits (1/3 damage each) → 3 damage numbers.
        private int  _currentImpactSlot = -1;
        private int  _cardTotalHits;
        private int  _cardHitsApplied;
        private int  _enemyTotalHits;
        private int  _enemyHitsApplied;

        // End-of-combat state.
        private bool _combatOver;
        private readonly HashSet<ulong> _deadBroadcast = new();

        // ── Client-side events (fired via ClientRpcs on ALL clients) ───────────

        /// <summary>Animate the card in slotIndex owned by ownerClientId.</summary>
        public event Action<int, ulong, NetworkBattleCard> OnExecuteCard;

        /// <summary>Animate enemy at enemyIndex attacking targetClientId for damage hp.</summary>
        public event Action<int, ulong, int>               OnEnemyAttacking;

        /// <summary>Update local player HP bar (targetClientId, newHp, maxHp, damage, isCrit).</summary>
        public event Action<ulong, int, int, int, bool>    OnPlayerHpChanged;

        /// <summary>Update enemy HP bar (enemyIndex, newHp, maxHp, damage, isCrit).</summary>
        public event Action<int, int, int, int, bool>      OnEnemyHpChanged;

        /// <summary>Start new player turn — call _tm.ForceNewPlayerTurn(round).</summary>
        public event Action<int>                           OnBeginNewPlayerTurn;

        /// <summary>A player died — deactivate their pawn (all clients) and HUD (owner).</summary>
        public event Action<ulong>                         OnPlayerDied;

        /// <summary>Combat ended — show the victory (won=true) or defeat screen on every client.</summary>
        public event Action<bool>                          OnCombatOver;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer) return;
            StartCoroutine(SubscribeWhenSyncReady());
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            if (IsServer && MultiplayerActionSlotsSync.Instance != null)
                MultiplayerActionSlotsSync.Instance.OnAllPlayersExhausted -= OnAllPlayersExhausted;
        }

        // ── Initialization (server calls after spawning) ───────────────────────

        public void Initialize(ServerCombatContext ctx, MultiplayerCombatRegistry registry)
        {
            if (!IsServer) return;
            _ctx      = ctx;
            _registry = registry;
            Debug.Log($"[Orchestrator] Inicializado: {ctx.Players.Count} players, {ctx.Enemies.Count} enemies.");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Host-only debug hotkeys to exercise end-of-combat without playing it out:
        //   F9  → Victory (kill boss)   F10 → kill one player (spectator)   F11 → Defeat (kill all players)
        private void Update()
        {
            if (!IsServer || _combatOver || _ctx == null) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.f9Key.wasPressedThisFrame)  DebugForceVictory();
            if (kb.f10Key.wasPressedThisFrame) DebugKillOnePlayer();
            if (kb.f11Key.wasPressedThisFrame) DebugForceDefeat();
        }

        private void DebugForceVictory()
        {
            for (int i = 0; i < _ctx.Enemies.Count; i++)
            {
                var e = _ctx.Enemies[i];
                if (e == null) continue;
                e.Model.SetHPDirectly(0);
                EnemyHpChangedClientRpc(i, 0, (int)e.Model.MaxHP, 0, false);
            }
            EndCombat(true);
        }

        private void DebugKillOnePlayer()
        {
            // Kill the highest-id alive player so the host can stay and spectate.
            ulong target = ulong.MaxValue;
            foreach (var kvp in _ctx.Players)
                if (kvp.Value.IsAlive && (target == ulong.MaxValue || kvp.Key > target))
                    target = kvp.Key;
            if (target == ulong.MaxValue) return;
            DebugKill(target);
            if (_ctx.AllPlayersDead()) EndCombat(false);
        }

        private void DebugForceDefeat()
        {
            foreach (var kvp in _ctx.Players)
                if (kvp.Value.IsAlive) DebugKill(kvp.Key);
            EndCombat(false);
        }

        private void DebugKill(ulong cid)
        {
            var p = _ctx.GetPlayer(cid);
            if (p == null) return;
            p.Model.SetHPDirectly(0);
            PlayerHpChangedClientRpc(cid, 0, (int)p.Model.MaxHP, 0, false);
            if (_deadBroadcast.Add(cid))
            {
                MultiplayerActionSlotsSync.Instance?.MarkPlayerDead(cid);
                PlayerDiedClientRpc(cid);
            }
        }
#endif

        // ── Subscription ───────────────────────────────────────────────────────

        private IEnumerator SubscribeWhenSyncReady()
        {
            yield return new WaitUntil(() => MultiplayerActionSlotsSync.Instance != null);
            MultiplayerActionSlotsSync.Instance.OnAllPlayersExhausted += OnAllPlayersExhausted;
            Debug.Log("[Orchestrator] Suscrito a OnAllPlayersExhausted.");
        }

        private void OnAllPlayersExhausted()
        {
            if (!IsServer || _ctx == null || _combatOver) return;
            StartCoroutine(RunCardResolutionPhase());
        }

        // ── PHASE 2: Card resolution ───────────────────────────────────────────

        private IEnumerator RunCardResolutionPhase()
        {
            var sync = MultiplayerActionSlotsSync.Instance;
            Debug.Log("[Orchestrator] Fase 2: resolución de cartas.");

            for (int i = 0; i < sync.SlotCount; i++)
            {
                if (_combatOver) yield break; // boss died on a previous card → stop

                var slot = sync.GetSlot(i);
                if (!slot.IsOccupied) continue;

                // Select caster/target/skill once; each impact frame applies one hit.
                if (!_ctx.BeginPlayerCard(slot.Card, slot.OwnerClientId, _registry, out _, out _cardTotalHits))
                    continue;

                _cardAnimDone      = false;
                _cardHitsApplied   = 0;
                _currentImpactSlot = i;
                _expectedCardOwner = slot.OwnerClientId;

                ExecuteCardClientRpc(i, slot.OwnerClientId, slot.Card);

                // Hits apply via CardImpactServerRpc as AEs fire during the animation.
                yield return StartCoroutine(WaitForCardAnim(8f, i));

                // Fallback: apply any hits the client never reported (missing AEs / disconnect).
                while (_cardHitsApplied < _cardTotalHits) ApplyAndBroadcastCardHit();
            }
            _currentImpactSlot = -1;

            if (_combatOver) yield break;
            StartCoroutine(RunEnemyPhase());
        }

        // ── PHASE 3: Enemy turn ────────────────────────────────────────────────

        private IEnumerator RunEnemyPhase()
        {
            Debug.Log("[Orchestrator] Fase 3: turno enemigo.");

            for (int i = 0; i < _ctx.Enemies.Count; i++)
            {
                if (_combatOver) yield break;

                var enemy = _ctx.GetEnemy(i);
                if (enemy == null || !enemy.IsAlive) continue;

                // Boss attacks multiple times per turn. Each attack re-targets (may hit a
                // different player) and animates independently so all clients see each swing.
                for (int a = 0; a < EnemyAttacksPerTurn; a++)
                {
                    if (_combatOver || !enemy.IsAlive) break;

                    // Pick target + skill up front; each impact frame applies one hit.
                    if (!_ctx.BeginEnemyAttack(i, out ulong targetCid, out _enemyTotalHits))
                        continue;

                    _enemyAnimDone    = false;
                    _enemyHitsApplied = 0;

                    EnemyAttackingClientRpc(i, targetCid, 0);

                    // Hits apply via EnemyImpactServerRpc as AEs fire during the animation.
                    yield return StartCoroutine(WaitForEnemyAnim(8f, i));

                    // Fallback: apply any unreported hits.
                    while (_enemyHitsApplied < _enemyTotalHits) ApplyAndBroadcastEnemyHit();

                    if (a < EnemyAttacksPerTurn - 1)
                        yield return new WaitForSeconds(BetweenAttacksDelay);
                }
            }

            if (_combatOver) yield break;
            StartCoroutine(BeginNewPlayerTurnPhase());
        }

        // ── PHASE 4: New player turn ───────────────────────────────────────────

        private IEnumerator BeginNewPlayerTurnPhase()
        {
            if (_combatOver) yield break;
            _ctx.AdvanceRound();
            MultiplayerActionSlotsSync.Instance?.ResetSlotsServerRpc();
            yield return null; // one frame for NetworkList reset to replicate
            Debug.Log($"[Orchestrator] Fase 4: nuevo turno player — round {_ctx.Round}.");
            BeginNewPlayerTurnClientRpc(_ctx.Round);
        }

        // ── Impact resolution (one hit per reported impact frame / AE) ─────────

        private void ApplyAndBroadcastCardHit()
        {
            if (_cardHitsApplied >= _cardTotalHits) return;
            var (damage, enemyIdx, crit) = _ctx.ApplyPlayerCardHit();
            _cardHitsApplied++;
            if (damage >= 0 && enemyIdx >= 0)
            {
                var enemy = _ctx.GetEnemy(enemyIdx);
                if (enemy != null)
                    EnemyHpChangedClientRpc(enemyIdx, (int)enemy.Model.CurrentHP, (int)enemy.Model.MaxHP, damage, crit);
            }

            // Victory: all enemies down.
            if (!_combatOver && _ctx.AllEnemiesDead()) EndCombat(true);
        }

        private void ApplyAndBroadcastEnemyHit()
        {
            if (_enemyHitsApplied >= _enemyTotalHits) return;
            var (damage, cid, crit) = _ctx.ApplyEnemyAttackHit();
            _enemyHitsApplied++;
            if (cid != ulong.MaxValue)
            {
                var player = _ctx.GetPlayer(cid);
                if (player != null)
                    PlayerHpChangedClientRpc(cid, (int)player.Model.CurrentHP, (int)player.Model.MaxHP, damage, crit);

                // Player died: shrink the slot board, deactivate their pawn + HUD (once).
                if (!_ctx.IsPlayerAlive(cid) && _deadBroadcast.Add(cid))
                {
                    MultiplayerActionSlotsSync.Instance?.MarkPlayerDead(cid);
                    PlayerDiedClientRpc(cid);
                }
            }

            // Defeat: all players down.
            if (!_combatOver && _ctx.AllPlayersDead()) EndCombat(false);
        }

        private void EndCombat(bool won)
        {
            if (_combatOver) return;
            _combatOver = true;
            Debug.Log($"[Orchestrator] Combate terminado — won={won}.");
            CombatEndedClientRpc(won);
        }

        // ── Wait helpers (no ref params — C# iterators don't support ref) ──────

        private IEnumerator WaitForCardAnim(float timeout, int slotIndex)
        {
            float elapsed = 0f;
            while (!_cardAnimDone && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (!_cardAnimDone)
                Debug.LogWarning($"[Orchestrator] Timeout animación carta slot {slotIndex}.");
        }

        private IEnumerator WaitForEnemyAnim(float timeout, int enemyIndex)
        {
            float elapsed = 0f;
            while (!_enemyAnimDone && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (!_enemyAnimDone)
                Debug.LogWarning($"[Orchestrator] Timeout animación ataque enemy[{enemyIndex}].");
        }

        // ── RPCs: Server → All clients ─────────────────────────────────────────

        [Rpc(SendTo.ClientsAndHost)]
        private void ExecuteCardClientRpc(int slotIndex, ulong ownerClientId, NetworkBattleCard card)
        {
            Debug.Log($"[Orchestrator] ExecuteCard slot={slotIndex} owner={ownerClientId}");
            OnExecuteCard?.Invoke(slotIndex, ownerClientId, card);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void EnemyAttackingClientRpc(int enemyIndex, ulong targetClientId, int damage)
        {
            Debug.Log($"[Orchestrator] EnemyAttacking enemy={enemyIndex} target={targetClientId} dmg={damage}");
            OnEnemyAttacking?.Invoke(enemyIndex, targetClientId, damage);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayerHpChangedClientRpc(ulong targetClientId, int newHp, int maxHp, int damage, bool isCrit)
        {
            OnPlayerHpChanged?.Invoke(targetClientId, newHp, maxHp, damage, isCrit);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void EnemyHpChangedClientRpc(int enemyIndex, int newHp, int maxHp, int damage, bool isCrit)
        {
            OnEnemyHpChanged?.Invoke(enemyIndex, newHp, maxHp, damage, isCrit);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void BeginNewPlayerTurnClientRpc(int round)
        {
            Debug.Log($"[Orchestrator] BeginNewPlayerTurn round={round}");
            OnBeginNewPlayerTurn?.Invoke(round);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayerDiedClientRpc(ulong clientId)
        {
            Debug.Log($"[Orchestrator] PlayerDied client={clientId}");
            OnPlayerDied?.Invoke(clientId);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void CombatEndedClientRpc(bool won)
        {
            Debug.Log($"[Orchestrator] CombatEnded won={won}");
            OnCombatOver?.Invoke(won);
        }

        /// <summary>Defeat → Retry: any client requests it, host reloads the BossFight scene for all.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RetryServerRpc(RpcParams rp = default)
        {
            if (!IsServer) return;
            Debug.Log("[Orchestrator] Retry — recargando Multiplayer_BossFight.");
            NetworkManager.SceneManager.LoadScene("Multiplayer_BossFight", LoadSceneMode.Single);
        }

        // ── RPCs: Clients → Server ─────────────────────────────────────────────

        /// <summary>Owner client calls this on each card impact frame → apply one hit.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void CardImpactServerRpc(int slotIndex, RpcParams rp = default)
        {
            if (slotIndex != _currentImpactSlot) return;            // stale / wrong card
            if (rp.Receive.SenderClientId != _expectedCardOwner) return;
            ApplyAndBroadcastCardHit();
        }

        /// <summary>
        /// Server-side client (host) calls this on each enemy attack impact frame → apply one hit.
        /// Only the host signals (deterministic timing) so hits aren't double-counted across clients.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void EnemyImpactServerRpc(RpcParams rp = default)
        {
            ApplyAndBroadcastEnemyHit();
        }

        /// <summary>Owner client calls this when its card animation finishes.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void CardAnimationCompleteServerRpc(int slotIndex, RpcParams rp = default)
        {
            if (rp.Receive.SenderClientId == _expectedCardOwner)
                _cardAnimDone = true;
        }

        /// <summary>
        /// Any client calls this when the enemy attack animation finishes on their screen.
        /// First client to respond unblocks the server.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void EnemyAttackCompleteServerRpc(RpcParams rp = default)
        {
            _enemyAnimDone = true;
        }
    }
}
