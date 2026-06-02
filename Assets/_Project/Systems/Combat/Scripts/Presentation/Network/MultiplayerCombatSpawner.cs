using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Runefall.Presentation.Network
{
    /// <summary>
    /// Managed component in the BossFight_Arena scene.
    /// Handles dynamically instantiating sessional network combat pawns (Host, Client, and Boss)
    /// across the network on loading.
    /// </summary>
    public class MultiplayerCombatSpawner : NetworkBehaviour
    {
        [Header("Pawn Prefab Reference")]
        [Tooltip("The genric NetworkedCombatPawn prefab registered in the NetworkManager.")]
        [SerializeField] private GameObject networkedPawnPrefab;

        [Header("Spawn Layout Coordinates")]
        [SerializeField] private Vector3 spawnRight = new Vector3(2f, 0.05f, -5f);   // Host Player slot (side-by-side)
        [SerializeField] private Vector3 spawnLeft = new Vector3(-2f, 0.05f, -5f);   // Client Player slot (side-by-side)
        [SerializeField] private Vector3 spawnCenter = new Vector3(0f, 0.05f, 5f);   // Boss slot (10 units away)

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return; // Spawning is authority of the Server/Host

            Debug.Log("[MultiplayerCombatSpawner] Initiating sessional network spawner...");
            StartCoroutine(SpawnSessionalPawnsDelayed());
        }

        private IEnumerator SpawnSessionalPawnsDelayed()
        {
            // Give Netcode a frame to fully sync client connections before spawning
            yield return new WaitForEndOfFrame();

            // 1. Spawn Host Player (always present, client ID is the local host ID)
            ulong hostClientId = NetworkManager.Singleton.LocalClientId;
            GameObject hostObj = Instantiate(networkedPawnPrefab, spawnRight, Quaternion.identity);
            var hostPawn = hostObj.GetComponent<NetworkedCombatPawn>();
            if (hostPawn != null)
            {
                var netObj = hostObj.GetComponent<NetworkObject>();
                // Set the role first so it synchronizes on spawn
                hostPawn.Role.Value = CombatRole.HostPlayer;
                netObj.SpawnWithOwnership(hostClientId);
                Debug.Log($"[MultiplayerCombatSpawner] Spawned Host Player pawn at {spawnRight} for Client {hostClientId}");
            }

            // 2. Spawn Client Player if present in sessional clients
            foreach (var connectedClient in NetworkManager.Singleton.ConnectedClientsList)
            {
                ulong clientId = connectedClient.ClientId;
                if (clientId != hostClientId)
                {
                    // Found Player 2 (Client)
                    GameObject clientObj = Instantiate(networkedPawnPrefab, spawnLeft, Quaternion.identity);
                    var clientPawn = clientObj.GetComponent<NetworkedCombatPawn>();
                    if (clientPawn != null)
                    {
                        var netObj = clientObj.GetComponent<NetworkObject>();
                        clientPawn.Role.Value = CombatRole.ClientPlayer;
                        netObj.SpawnWithOwnership(clientId);
                        Debug.Log($"[MultiplayerCombatSpawner] Spawned Client Player pawn at {spawnLeft} for Client {clientId}");
                    }
                    break;
                }
            }

            // 3. Spawn Boss (owned by Server/Host)
            GameObject bossObj = Instantiate(networkedPawnPrefab, spawnCenter, Quaternion.Euler(0f, 180f, 0f));
            var bossPawn = bossObj.GetComponent<NetworkedCombatPawn>();
            if (bossPawn != null)
            {
                var netObj = bossObj.GetComponent<NetworkObject>();
                bossPawn.Role.Value = CombatRole.Boss;
                netObj.Spawn();
                Debug.Log($"[MultiplayerCombatSpawner] Spawned Boss pawn at {spawnCenter} under Server Authority");
            }
        }
    }
}
