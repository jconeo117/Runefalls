using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Server-authoritative shared action slot board.
    /// Spawned by MultiplayerBossFightInitializer after arena assembly.
    /// SlotCount = ActionsPerPlayer * playerCount (e.g. 3 * 2 = 6).
    /// Each player has an individual action budget of ActionsPerPlayer per turn.
    /// Any client can fill any available slot while they have actions remaining.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class MultiplayerActionSlotsSync : NetworkBehaviour
    {
        public const int ActionsPerPlayer = 3;

        public static MultiplayerActionSlotsSync Instance { get; private set; }

        // Server-authoritative slot contents
        private NetworkList<NetworkSlotState> _slots;

        // Per-player action usage — indexed by connection order (clientId 0, clientId 1)
        public NetworkVariable<int> ActionsUsed0 = new(writePerm: NetworkVariableWritePermission.Server);
        public NetworkVariable<int> ActionsUsed1 = new(writePerm: NetworkVariableWritePermission.Server);

        // Server-only: dead players are removed from the board — total slots shrink to
        // aliveCount * ActionsPerPlayer (e.g. 2 players = 6 slots; one dies = 3 slots).
        private readonly HashSet<ulong> _deadPlayers = new();
        private int _playerCount;

        public int SlotCount => _slots?.Count ?? 0;

        public NetworkSlotState GetSlot(int i)    => _slots[i];
        public int GetActionsRemaining(ulong cid) =>
            _deadPlayers.Contains(cid) ? 0 : ActionsPerPlayer - GetActionsUsed(cid);
        public int GetActionsUsed(ulong cid)      => cid == 0 ? ActionsUsed0.Value : ActionsUsed1.Value;

        public event Action<int> OnSlotUpdated;        // fires on ALL clients when slot changes
        public event Action      OnActionsChanged;     // fires when any action count changes
        /// <summary>Fires on ALL clients when every player has exhausted their actions for the turn.</summary>
        public event Action      OnAllPlayersExhausted;

        private void Awake()
        {
            _slots = new NetworkList<NetworkSlotState>();
        }

        public override void OnNetworkSpawn()
        {
            // Always update Instance — never destroy the newly spawned object.
            // Old pattern (Destroy if Instance exists) caused InitializeSlots to run on
            // the destroyed GO while Instance still pointed to the uninitialized prior object.
            Instance = this;

            // Skip Clear/out-of-range events (board is resized on death) so subscribers
            // never read an index past the live list.
            _slots.OnListChanged        += e => { if (e.Index >= 0 && e.Index < _slots.Count) OnSlotUpdated?.Invoke(e.Index); };
            ActionsUsed0.OnValueChanged += (_, _) => { OnActionsChanged?.Invoke(); CheckAllExhausted(); };
            ActionsUsed1.OnValueChanged += (_, _) => { OnActionsChanged?.Invoke(); CheckAllExhausted(); };

            Debug.Log($"[ActionSlotsSync] Spawned. IsServer={IsServer} SlotCount={SlotCount}");
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        // ── Server API ─────────────────────────────────────────────────────────

        /// <summary>Call on server after knowing playerCount.</summary>
        public void InitializeSlots(int playerCount)
        {
            if (!IsServer) return;
            _playerCount = playerCount;
            _slots.Clear();
            int total = playerCount * ActionsPerPlayer;
            for (int i = 0; i < total; i++)
                _slots.Add(default);
            Debug.Log($"[ActionSlotsSync] {total} slots inicializados para {playerCount} jugadores.");
        }

        /// <summary>
        /// Server: a player died. Shrink the shared board to aliveCount * ActionsPerPlayer
        /// and reset usage so the turn can continue with the remaining player(s).
        /// </summary>
        public void MarkPlayerDead(ulong clientId)
        {
            if (!IsServer) return;
            if (!_deadPlayers.Add(clientId)) return;

            int alive = Mathf.Max(0, _playerCount - _deadPlayers.Count);
            int total = alive * ActionsPerPlayer;

            _slots.Clear();
            for (int i = 0; i < total; i++) _slots.Add(default);

            // NOTE: do NOT reset ActionsUsed here. Death happens during the enemy phase; the
            // counters are reset at the next BeginNewPlayerTurn (ResetSlotsServerRpc). Resetting
            // now would fire CheckAllExhausted (used could momentarily equal the new SlotCount)
            // and re-trigger a whole extra enemy phase.
            Debug.Log($"[ActionSlotsSync] Player {clientId} muerto → board reducido a {total} slots ({alive} vivos).");
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void CheckAllExhausted()
        {
            // Only server decides when turn is over — then notifies all clients via RPC.
            if (!IsServer) return;
            if (SlotCount == 0) return;
            int used = ActionsUsed0.Value + ActionsUsed1.Value;
            Debug.Log($"[ActionSlotsSync] CheckAllExhausted: used={used} SlotCount={SlotCount}");
            if (used >= SlotCount)
                NotifyAllExhaustedClientRpc();
        }

        /// <summary>Server → all clients: every player has used all their actions this turn.</summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyAllExhaustedClientRpc()
        {
            Debug.Log($"[ActionSlotsSync] NotifyAllExhausted recibido en ClientId={NetworkManager.Singleton?.LocalClientId}");
            OnAllPlayersExhausted?.Invoke();
        }

        // ── RPCs ───────────────────────────────────────────────────────────────

        /// <summary>Any client requests placing their card in a slot.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void PlaceCardServerRpc(int slotIndex, NetworkBattleCard card, RpcParams rpcParams = default)
        {
            ulong cid = rpcParams.Receive.SenderClientId;

            if (_deadPlayers.Contains(cid))
            {
                Debug.LogWarning($"[ActionSlotsSync] Cliente {cid} muerto — no puede jugar cartas.");
                return;
            }
            if (slotIndex < 0 || slotIndex >= _slots.Count)
            {
                Debug.LogWarning($"[ActionSlotsSync] Slot {slotIndex} fuera de rango.");
                return;
            }
            if (_slots[slotIndex].IsOccupied)
            {
                Debug.LogWarning($"[ActionSlotsSync] Slot {slotIndex} ya ocupado.");
                return;
            }
            if (GetActionsUsed(cid) >= ActionsPerPlayer)
            {
                Debug.LogWarning($"[ActionSlotsSync] Cliente {cid} sin acciones.");
                return;
            }

            _slots[slotIndex] = new NetworkSlotState
            {
                IsOccupied    = true,
                OwnerClientId = cid,
                Card          = card
            };

            if (cid == 0) ActionsUsed0.Value++;
            else          ActionsUsed1.Value++;

            Debug.Log($"[ActionSlotsSync] ✅ Cliente {cid} → slot {slotIndex} '{card.SkillName}' " +
                      $"({GetActionsUsed(cid)}/{ActionsPerPlayer} acciones)");
        }

        /// <summary>Server resets all slots at start of new turn.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ResetSlotsServerRpc()
        {
            for (int i = 0; i < _slots.Count; i++)
                _slots[i] = default;
            ActionsUsed0.Value = 0;
            ActionsUsed1.Value = 0;
            Debug.Log("[ActionSlotsSync] Slots reseteados para nuevo turno.");
        }
    }
}
