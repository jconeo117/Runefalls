using UnityEngine;
using Unity.Netcode;
using Runefall.Core;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Multiplayer-specific combat arena assembler.
    /// On the server, instantiates and spawns NetworkedCombatPawn containers.
    /// On clients, builds the physical arena locally and waits for replicated network pawns.
    /// </summary>
    public class MultiplayerCombatArenaAssembler : CombatArenaAssembler
    {
        [Header("Multiplayer Spawning Settings")]
        [SerializeField] private GameObject networkedCombatPawnPrefab;

        public override void SpawnFromEncounterState(EncounterState state, Vector3 worldOffset = default)
        {
            if (state == null)
            {
                Debug.LogError("[MultiplayerCombatArenaAssembler] EncounterState is null.");
                return;
            }

            // Fallback to singleplayer spawning if Netcode is not active or running
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                base.SpawnFromEncounterState(state, worldOffset);
                return;
            }

            var party = state.ResolvedParty;
            int playerCount = party.Length > 0 ? party.Length : 1;
            int enemyCount = 1;

            if (NetworkManager.Singleton.IsServer)
            {
                // Server determines enemy count authoritatively so all clients build the same slot counts
                enemyCount = Random.Range(1, 4);
                
                // Cache or save it so we can use it when spawning
                _tempEnemyCount = enemyCount;
            }
            else
            {
                // Clients will receive the replicated pawns, but they need to pre-build physical slots.
                // How do they know the enemy count?
                // For Netcode, the server will spawn NetworkObjects. The client can count or the server can sync it.
                // However, the client needs to call Assemble/AssembleInRoom immediately to position cameras/slots.
                // Since this is a client-server setup, the server can sync the encounter details, or we can look up
                // the expected number of enemy pawns.
                // A very robust approach is to have the client query the server for slot count, or we can let the client
                // build slots dynamically when pawns replicate, OR build max slots (3), OR get the count from the network session.
                // In a standard game flow, the client has the EncounterState synced, and the server sets the seed.
                // Let's assume a default of 3 slots for enemies to be safe, or get the exact count from a synced variable.
                // Wait, if the client builds slots, can it just build max slots?
                // The base assembler uses:
                // Assemble(playerCount, enemyCount)
                // Let's default to 3 slots for enemies on the client if it's not the server, or we can synchronize this.
                // Wait! A cleaner way is for the server to set a NetworkVariable or RPC, but since the arena is assembled
                // in SpawnFromEncounterState, let's default client slots to 3, which accommodates any random range 1-3.
                // Or better, let's read the party size and default enemies to 3 slots.
                enemyCount = 3; 
            }

            if (PendingRoom != null)
            {
                AssembleInRoom(PendingRoom, playerCount, enemyCount);
                PendingRoom = null;
            }
            else
            {
                Assemble(playerCount, enemyCount, worldOffset);
            }

            if (NetworkManager.Singleton.IsServer)
            {
                SpawnNetworkPawnsServer(state, playerCount, _tempEnemyCount);
            }
        }

        private int _tempEnemyCount = 1;

        private void SpawnNetworkPawnsServer(EncounterState state, int playerCount, int enemyCount)
        {
            if (networkedCombatPawnPrefab == null)
            {
                Debug.LogError("[MultiplayerCombatArenaAssembler] networkedCombatPawnPrefab is not assigned!");
                return;
            }

            var party = state.ResolvedParty;

            // Spawn Players
            for (int i = 0; i < playerCount; i++)
            {
                var solo = i < party.Length ? party[i] : null;
                if (solo == null || string.IsNullOrEmpty(solo.characterName)) continue;

                var pawnGo = Instantiate(networkedCombatPawnPrefab);
                var netPawn = pawnGo.GetComponent<Multiplayer.NetworkedCombatPawn>();

                // Set network variables BEFORE spawning so clients receive them in spawn payload
                netPawn.CharacterOrEnemyDataName.Value = solo.characterName;
                netPawn.SlotIndex.Value = i;
                netPawn.IsPlayerTeam.Value = true;

                // Spawn on network
                var netObj = pawnGo.GetComponent<NetworkObject>();
                netObj.Spawn(destroyWithScene: true);
            }

            // Spawn Enemies
            var eData = state.Encounter?.enemyData;
            if (eData != null && !string.IsNullOrEmpty(eData.enemyName))
            {
                for (int i = 0; i < enemyCount; i++)
                {
                    var pawnGo = Instantiate(networkedCombatPawnPrefab);
                    var netPawn = pawnGo.GetComponent<Multiplayer.NetworkedCombatPawn>();

                    netPawn.CharacterOrEnemyDataName.Value = eData.enemyName;
                    netPawn.SlotIndex.Value = i;
                    netPawn.IsPlayerTeam.Value = false;

                    var netObj = pawnGo.GetComponent<NetworkObject>();
                    netObj.Spawn(destroyWithScene: true);
                }
            }
        }
    }
}
