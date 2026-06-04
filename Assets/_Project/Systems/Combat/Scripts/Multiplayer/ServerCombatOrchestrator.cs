using System;
using System.Collections;
using UnityEngine;
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

        // Impact gating: damage resolves on the impact frame the client reports, not anim end.
        private bool _cardImpactDone;
        private int  _currentImpactSlot = -1;
        private bool _enemyImpactDone;
        private (ulong cid, int dmg, bool crit) _enemyPending;

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

        // ── Subscription ───────────────────────────────────────────────────────

        private IEnumerator SubscribeWhenSyncReady()
        {
            yield return new WaitUntil(() => MultiplayerActionSlotsSync.Instance != null);
            MultiplayerActionSlotsSync.Instance.OnAllPlayersExhausted += OnAllPlayersExhausted;
            Debug.Log("[Orchestrator] Suscrito a OnAllPlayersExhausted.");
        }

        private void OnAllPlayersExhausted()
        {
            if (!IsServer || _ctx == null) return;
            StartCoroutine(RunCardResolutionPhase());
        }

        // ── PHASE 2: Card resolution ───────────────────────────────────────────

        private IEnumerator RunCardResolutionPhase()
        {
            var sync = MultiplayerActionSlotsSync.Instance;
            Debug.Log("[Orchestrator] Fase 2: resolución de cartas.");

            for (int i = 0; i < sync.SlotCount; i++)
            {
                var slot = sync.GetSlot(i);
                if (!slot.IsOccupied) continue;

                _cardAnimDone      = false;
                _cardImpactDone    = false;
                _currentImpactSlot = i;
                _expectedCardOwner = slot.OwnerClientId;

                ExecuteCardClientRpc(i, slot.OwnerClientId, slot.Card);

                // Damage applies on the owner's reported impact frame (CardImpactServerRpc).
                yield return StartCoroutine(WaitForFlag(() => _cardImpactDone, 3f));
                if (!_cardImpactDone) ResolveCardImpact(i); // fallback: client never signaled

                // Then wait for the full animation to finish before the next card.
                yield return StartCoroutine(WaitForCardAnim(8f, i));
            }
            _currentImpactSlot = -1;

            StartCoroutine(RunEnemyPhase());
        }

        // ── PHASE 3: Enemy turn ────────────────────────────────────────────────

        private IEnumerator RunEnemyPhase()
        {
            Debug.Log("[Orchestrator] Fase 3: turno enemigo.");

            for (int i = 0; i < _ctx.Enemies.Count; i++)
            {
                var enemy = _ctx.GetEnemy(i);
                if (enemy == null || !enemy.IsAlive) continue;

                // Boss attacks multiple times per turn. Each attack re-targets (may hit a
                // different player) and animates independently so all clients see each swing.
                for (int a = 0; a < EnemyAttacksPerTurn; a++)
                {
                    if (!enemy.IsAlive) break;

                    // Resolve (applies damage) + pick target up front; broadcast on impact frame.
                    var (damage, targetCid, crit) = _ctx.ResolveEnemyAttack(i);
                    if (damage < 0) continue;

                    _enemyAnimDone   = false;
                    _enemyImpactDone = false;
                    _enemyPending    = (targetCid, damage, crit);

                    EnemyAttackingClientRpc(i, targetCid, damage);

                    // Player HP/damage number applies on the reported impact frame.
                    yield return StartCoroutine(WaitForFlag(() => _enemyImpactDone, 3f));
                    if (!_enemyImpactDone) ResolveEnemyImpact(); // fallback

                    yield return StartCoroutine(WaitForEnemyAnim(8f, i));

                    if (a < EnemyAttacksPerTurn - 1)
                        yield return new WaitForSeconds(BetweenAttacksDelay);
                }
            }

            StartCoroutine(BeginNewPlayerTurnPhase());
        }

        // ── PHASE 4: New player turn ───────────────────────────────────────────

        private IEnumerator BeginNewPlayerTurnPhase()
        {
            _ctx.AdvanceRound();
            MultiplayerActionSlotsSync.Instance?.ResetSlotsServerRpc();
            yield return null; // one frame for NetworkList reset to replicate
            Debug.Log($"[Orchestrator] Fase 4: nuevo turno player — round {_ctx.Round}.");
            BeginNewPlayerTurnClientRpc(_ctx.Round);
        }

        // ── Impact resolution (damage applies on the reported impact frame) ─────

        private void ResolveCardImpact(int slotIndex)
        {
            if (_cardImpactDone) return;
            var sync = MultiplayerActionSlotsSync.Instance;
            if (sync == null || slotIndex < 0 || slotIndex >= sync.SlotCount) { _cardImpactDone = true; return; }

            var slot = sync.GetSlot(slotIndex);
            if (!slot.IsOccupied) { _cardImpactDone = true; return; }

            var (damage, enemyIdx, crit) = _ctx.ResolvePlayerCard(slot.Card, slot.OwnerClientId, _registry);
            if (damage >= 0 && enemyIdx >= 0)
            {
                var enemy = _ctx.GetEnemy(enemyIdx);
                if (enemy != null)
                    EnemyHpChangedClientRpc(enemyIdx, (int)enemy.Model.CurrentHP, (int)enemy.Model.MaxHP, damage, crit);
            }
            _cardImpactDone = true;
        }

        private void ResolveEnemyImpact()
        {
            if (_enemyImpactDone) return;
            var (cid, dmg, crit) = _enemyPending;
            if (cid != ulong.MaxValue)
            {
                var player = _ctx.GetPlayer(cid);
                if (player != null)
                    PlayerHpChangedClientRpc(cid, (int)player.Model.CurrentHP, (int)player.Model.MaxHP, dmg, crit);
            }
            _enemyImpactDone = true;
        }

        // ── Wait helpers (no ref params — C# iterators don't support ref) ──────

        private IEnumerator WaitForFlag(System.Func<bool> done, float timeout)
        {
            float elapsed = 0f;
            while (!done() && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

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

        // ── RPCs: Clients → Server ─────────────────────────────────────────────

        /// <summary>Owner client calls this on the card's impact frame → resolve + broadcast damage.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void CardImpactServerRpc(int slotIndex, RpcParams rp = default)
        {
            if (slotIndex != _currentImpactSlot) return;            // stale / wrong card
            if (rp.Receive.SenderClientId != _expectedCardOwner) return;
            ResolveCardImpact(slotIndex);
        }

        /// <summary>Any client calls this on the enemy attack's impact frame → broadcast damage.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void EnemyImpactServerRpc(RpcParams rp = default)
        {
            ResolveEnemyImpact();
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
