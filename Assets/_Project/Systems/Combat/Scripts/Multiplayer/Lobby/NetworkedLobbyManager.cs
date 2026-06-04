using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

namespace Runefall.Multiplayer.Lobby
{
    /// <summary>
    /// Spawned by host on room creation. DontDestroyOnLoad — survives to Multiplayer_BossFight.
    /// Collects character selections from each connected client via ServerRpc.
    /// MultiplayerCombatArenaAssembler reads GetAllAssignments() to spawn pawns.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedLobbyManager : NetworkBehaviour
    {
        public static NetworkedLobbyManager Instance { get; private set; }

        private readonly Dictionary<ulong, string> _playerCharacters = new();

        public override void OnNetworkSpawn()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[NetworkedLobbyManager] Spawned. IsServer={IsServer}");
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        // ── Client → Server ────────────────────────────────────────────────────

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RegisterCharacterServerRpc(
            FixedString64Bytes characterName,
            RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            _playerCharacters[clientId] = characterName.ToString();
            Debug.Log($"[NetworkedLobbyManager] ✅ Cliente {clientId} → '{characterName}'");
        }

        // ── Queries ────────────────────────────────────────────────────────────

        public string GetCharacterName(ulong clientId) =>
            _playerCharacters.TryGetValue(clientId, out var name) ? name : null;

        public IReadOnlyDictionary<ulong, string> GetAllAssignments() => _playerCharacters;

        public bool AllPlayersRegistered(int expectedCount) =>
            _playerCharacters.Count >= expectedCount;
    }
}
