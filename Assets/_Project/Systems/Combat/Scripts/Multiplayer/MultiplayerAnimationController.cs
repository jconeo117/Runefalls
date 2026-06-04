using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Runefall.Presentation.Combat;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Per-client animation coordinator for multiplayer combat.
    ///
    /// Subscribes to ServerCombatOrchestrator events and drives CombatPawnAnimator
    /// on the appropriate NetworkedCombatPawn instances. All clients see the same animations
    /// because every RPC fires on ClientsAndHost.
    ///
    /// Only the card owner sends CardAnimationCompleteServerRpc.
    /// Any client sends EnemyAttackCompleteServerRpc (first one wins — server uses a flag).
    /// </summary>
    public class MultiplayerAnimationController : MonoBehaviour
    {
        // Duration constants (seconds) — replace with Animator.GetCurrentAnimatorStateInfo
        // once clips are tuned per character.
        private const float ApproachDuration = 0.8f;
        private const float HitDuration      = 0.4f;

        private ulong _localClientId;

        // Cached pawn lookups — refreshed on first use (pawns may spawn after this MonoBehaviour).
        private readonly Dictionary<ulong, NetworkedCombatPawn> _playerPawns = new();
        private readonly List<NetworkedCombatPawn>              _enemyPawns  = new();
        private bool _pawnsScanned;

        // ── Initialization ─────────────────────────────────────────────────────

        public void Initialize(ulong localClientId)
        {
            _localClientId = localClientId;
            StartCoroutine(SubscribeWhenOrchestratorReady());
        }

        private IEnumerator SubscribeWhenOrchestratorReady()
        {
            yield return new WaitUntil(() => ServerCombatOrchestrator.Instance != null);
            var orch = ServerCombatOrchestrator.Instance;
            orch.OnExecuteCard   += OnExecuteCard;
            orch.OnEnemyAttacking += OnEnemyAttacking;
            Debug.Log("[MPAnimCtrl] Suscrito a OnExecuteCard y OnEnemyAttacking.");
        }

        private void OnDestroy()
        {
            if (ServerCombatOrchestrator.Instance == null) return;
            ServerCombatOrchestrator.Instance.OnExecuteCard    -= OnExecuteCard;
            ServerCombatOrchestrator.Instance.OnEnemyAttacking -= OnEnemyAttacking;
        }

        // ── Card execution (Phase 2) ───────────────────────────────────────────

        private void OnExecuteCard(int slotIndex, ulong ownerClientId, NetworkBattleCard card)
        {
            StartCoroutine(PlayCardAnimation(slotIndex, ownerClientId, card));
        }

        private IEnumerator PlayCardAnimation(int slotIndex, ulong ownerClientId, NetworkBattleCard card)
        {
            ScanPawnsIfNeeded();

            // Attacker lunge
            var attackerPawn = GetPlayerPawn(ownerClientId);
            var attackerAnim = GetPawnAnimator(attackerPawn);
            attackerAnim?.PlayApproach();

            // After half the approach, target reacts
            yield return new WaitForSeconds(ApproachDuration * 0.5f);

            var enemyPawn = GetFirstAliveEnemyPawn();
            var enemyAnim = GetPawnAnimator(enemyPawn);
            enemyAnim?.PlayHit();

            // Wait for approach to finish
            yield return new WaitForSeconds(ApproachDuration * 0.5f);

            // Attacker returns
            attackerAnim?.PlayReturn();

            yield return new WaitForSeconds(HitDuration);

            // Only the card owner reports animation complete — server awaits this signal.
            if (_localClientId == ownerClientId)
            {
                Debug.Log($"[MPAnimCtrl] CardAnimComplete slot={slotIndex}");
                ServerCombatOrchestrator.Instance?.CardAnimationCompleteServerRpc(slotIndex);
            }
        }

        // ── Enemy attack (Phase 3) ─────────────────────────────────────────────

        private void OnEnemyAttacking(int enemyIndex, ulong targetClientId, int damage)
        {
            StartCoroutine(PlayEnemyAttackAnimation(enemyIndex, targetClientId, damage));
        }

        private IEnumerator PlayEnemyAttackAnimation(int enemyIndex, ulong targetClientId, int damage)
        {
            ScanPawnsIfNeeded();

            // Enemy approaches
            var enemyPawn = GetEnemyPawn(enemyIndex);
            var enemyAnim = GetPawnAnimator(enemyPawn);
            enemyAnim?.PlayApproach();

            yield return new WaitForSeconds(ApproachDuration * 0.5f);

            // Target player takes hit
            var targetPawn = GetPlayerPawn(targetClientId);
            var targetAnim = GetPawnAnimator(targetPawn);
            targetAnim?.PlayHit();

            yield return new WaitForSeconds(ApproachDuration * 0.5f);

            enemyAnim?.PlayReturn();

            yield return new WaitForSeconds(HitDuration);

            // Any client can unblock the server — first one wins.
            Debug.Log($"[MPAnimCtrl] EnemyAttackComplete enemy={enemyIndex}");
            ServerCombatOrchestrator.Instance?.EnemyAttackCompleteServerRpc();
        }

        // ── Pawn discovery ─────────────────────────────────────────────────────

        private void ScanPawnsIfNeeded()
        {
            if (_pawnsScanned) return;
            _pawnsScanned = true;
            RebuildPawnCache();
        }

        private void RebuildPawnCache()
        {
            _playerPawns.Clear();
            _enemyPawns.Clear();

            var all = Object.FindObjectsByType<NetworkedCombatPawn>(FindObjectsSortMode.None);
            foreach (var pawn in all)
            {
                if (pawn.IsPlayerTeam.Value)
                    _playerPawns[pawn.NetworkObject.OwnerClientId] = pawn;
                else
                    _enemyPawns.Add(pawn);
            }

            _enemyPawns.Sort((a, b) => a.SlotIndex.Value.CompareTo(b.SlotIndex.Value));
            Debug.Log($"[MPAnimCtrl] Pawns escaneados: {_playerPawns.Count} players, {_enemyPawns.Count} enemies.");
        }

        private NetworkedCombatPawn GetPlayerPawn(ulong clientId)
        {
            if (_playerPawns.TryGetValue(clientId, out var p)) return p;
            // Pawn may have spawned after initial scan — retry once.
            RebuildPawnCache();
            return _playerPawns.TryGetValue(clientId, out p) ? p : null;
        }

        private NetworkedCombatPawn GetEnemyPawn(int index)
        {
            if (index < _enemyPawns.Count) return _enemyPawns[index];
            RebuildPawnCache();
            return index < _enemyPawns.Count ? _enemyPawns[index] : null;
        }

        private NetworkedCombatPawn GetFirstAliveEnemyPawn()
        {
            foreach (var p in _enemyPawns)
                if (p != null && p.gameObject.activeInHierarchy) return p;
            return _enemyPawns.Count > 0 ? _enemyPawns[0] : null;
        }

        private static CombatPawnAnimator GetPawnAnimator(NetworkedCombatPawn pawn)
        {
            if (pawn == null) return null;
            return pawn.GetComponentInChildren<CombatPawnAnimator>();
        }
    }
}
