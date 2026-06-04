using System.Collections;
using UnityEngine;
using Unity.Netcode;
using Runefall.Core;
using Runefall.Presentation.Combat;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Entry point for Multiplayer_BossFight scene.
    /// Assembles arena, registers registry in ServiceLocator, spawns NetworkedCombatPawn
    /// per player (server only), and positions each client's camera.
    /// </summary>
    public class MultiplayerBossFightInitializer : MonoBehaviour
    {
        [SerializeField] private CombatArenaAssembler      arenaAssembler;
        [SerializeField] private GameObject                networkedCombatPawnPrefab;
        [SerializeField] private MultiplayerCombatRegistry registry;
        [SerializeField] private GameObject                actionSlotsSyncPrefab;
        [SerializeField] private GameObject                serverCombatOrchestratorPrefab;

        [Header("Arena")]
        [SerializeField] private int maxPlayers = 2;
        [SerializeField] private int enemySlots = 1;

        private void Awake()
        {
            // Registrar registry en Awake — ANTES de cualquier Start() o spawn replicado de NGO
#if UNITY_EDITOR
            if (registry == null)
                registry = UnityEditor.AssetDatabase.LoadAssetAtPath<MultiplayerCombatRegistry>(
                    "Assets/_Project/Systems/Combat/ScriptableObjects/Combat/MultiplayerCombatRegistry.asset");
#endif
            if (registry != null)
            {
                ServiceLocator.Register<MultiplayerCombatRegistry>(registry);
                registry.Initialize();
                Debug.Log($"[BossFight] Registry registrado en Awake: {registry.characters?.Count ?? 0} chars, {registry.enemies?.Count ?? 0} enemies.");
            }
            else
                Debug.LogError("[BossFight] MultiplayerCombatRegistry no encontrado.");
        }

        private IEnumerator Start()
        {
            // Wait until NetworkManager is listening
            yield return new WaitUntil(() =>
                NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);

            // Assemble physical arena on all clients
            if (arenaAssembler != null)
                arenaAssembler.Assemble(maxPlayers, enemySlots);
            else
                Debug.LogError("[BossFight] ArenaAssembler no asignado.");

            // Position camera at gameplay anchor
            yield return null; // wait one frame for arena to settle
            PositionCamera();

            // Server spawns pawns (players + boss) and shared action slots
            if (NetworkManager.Singleton.IsServer)
            {
                var assignments = Lobby.NetworkedLobbyManager.Instance?.GetAllAssignments();
                int playerCount = assignments?.Count > 0 ? assignments.Count : NetworkManager.Singleton.ConnectedClientsIds.Count;
                if (playerCount < 1) playerCount = 1;
                Debug.Log($"[BossFight] playerCount={playerCount} (lobby={assignments?.Count ?? -1} connected={NetworkManager.Singleton.ConnectedClientsIds.Count})");
                SpawnActionSlotsSync(playerCount);
                SpawnServerCombatOrchestrator(assignments);
                SpawnPlayerPawns();
                SpawnBoss();
            }
        }

        private void SpawnPlayerPawns()
        {
            var assignments = Lobby.NetworkedLobbyManager.Instance?.GetAllAssignments();
            if (assignments == null || assignments.Count == 0)
            {
                Debug.LogError("[BossFight] NetworkedLobbyManager sin assignments.");
                return;
            }
            if (networkedCombatPawnPrefab == null)
            {
                Debug.LogError("[BossFight] networkedCombatPawnPrefab no asignado.");
                return;
            }

            var playerSlots = arenaAssembler?.PlayerSlots;
            Debug.Log($"[BossFight] Spawning {assignments.Count} pawns. Slots disponibles: {playerSlots?.Count ?? 0}");

            int slotIdx = 0;
            foreach (var kvp in assignments)
            {
                ulong  clientId = kvp.Key;
                string charName = kvp.Value;

                // Determinar posición del slot ANTES de spawnar
                Vector3    slotPos = Vector3.zero;
                Quaternion slotRot = Quaternion.identity;
                if (playerSlots != null && slotIdx < playerSlots.Count)
                {
                    slotPos = playerSlots[slotIdx].position;
                    slotRot = playerSlots[slotIdx].rotation;
                }

                // Instanciar en la posición del slot — NGO sincroniza este transform a todos los clientes
                var go = Instantiate(networkedCombatPawnPrefab, slotPos, slotRot);

                var pawn = go.GetComponent<NetworkedCombatPawn>();
                if (pawn == null) { Destroy(go); continue; }

                pawn.CharacterOrEnemyDataName.Value = charName;
                pawn.SlotIndex.Value                = slotIdx;
                pawn.IsPlayerTeam.Value             = true;

                go.GetComponent<NetworkObject>().SpawnWithOwnership(clientId, destroyWithScene: true);

                Debug.Log($"[BossFight] ✅ Pawn → clientId:{clientId} '{charName}' slot:{slotIdx} pos:{slotPos}");
                slotIdx++;
            }
        }

        private void SpawnServerCombatOrchestrator(System.Collections.Generic.IReadOnlyDictionary<ulong, string> assignments)
        {
            // Reuse scene-placed instance if present
            var orchestrator = ServerCombatOrchestrator.Instance;
            if (orchestrator == null)
            {
                if (serverCombatOrchestratorPrefab == null)
                {
                    Debug.LogError("[BossFight] serverCombatOrchestratorPrefab no asignado.");
                    return;
                }
                var go = Instantiate(serverCombatOrchestratorPrefab);
                go.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
                orchestrator = go.GetComponent<ServerCombatOrchestrator>();
            }

            if (orchestrator == null || registry == null) return;

            var clientChars = new System.Collections.Generic.Dictionary<ulong, string>();
            if (assignments != null)
                foreach (var kvp in assignments)
                    clientChars[kvp.Key] = kvp.Value;

            // Fallback: if lobby has no assignments, map connected clients to registry characters
            if (clientChars.Count == 0)
            {
                var ids = NetworkManager.Singleton.ConnectedClientsIds;
                for (int i = 0; i < ids.Count && i < registry.characters.Count; i++)
                    clientChars[ids[i]] = registry.characters[i].characterName;
            }

            var serverCtx = new ServerCombatContext(registry, clientChars, registry.enemies);
            orchestrator.Initialize(serverCtx, registry);
            Debug.Log($"[BossFight] ServerCombatOrchestrator inicializado: {clientChars.Count} players.");
        }

        private void SpawnActionSlotsSync(int playerCount)
        {
            // If a scene-placed NetworkObject already set Instance (spawned by NGO on scene load),
            // just initialize it — don't spawn a second one.
            if (MultiplayerActionSlotsSync.Instance != null)
            {
                MultiplayerActionSlotsSync.Instance.InitializeSlots(playerCount);
                Debug.Log($"[BossFight] ActionSlotsSync (pre-existente) inicializado con {playerCount * MultiplayerActionSlotsSync.ActionsPerPlayer} slots.");
                return;
            }

            if (actionSlotsSyncPrefab == null)
            {
                Debug.LogError("[BossFight] actionSlotsSyncPrefab no asignado.");
                return;
            }
            var go   = Instantiate(actionSlotsSyncPrefab);
            var net  = go.GetComponent<NetworkObject>();
            net.Spawn(destroyWithScene: true);
            go.GetComponent<MultiplayerActionSlotsSync>().InitializeSlots(playerCount);
            Debug.Log($"[BossFight] ActionSlotsSync spawneado con {playerCount * MultiplayerActionSlotsSync.ActionsPerPlayer} slots.");
        }

        private void SpawnBoss()
        {
            if (registry == null || registry.enemies == null || registry.enemies.Count == 0)
            {
                Debug.LogError("[BossFight] No hay enemies en el registry.");
                return;
            }

            var enemyData = registry.enemies[0];
            if (enemyData == null) { Debug.LogError("[BossFight] registry.enemies[0] es null."); return; }

            var enemySlots = arenaAssembler?.EnemySlots;
            Vector3    pos = enemySlots != null && enemySlots.Count > 0
                ? enemySlots[0].position : Vector3.forward * 4f;
            Quaternion rot = enemySlots != null && enemySlots.Count > 0
                ? enemySlots[0].rotation : Quaternion.identity;

            var go   = Instantiate(networkedCombatPawnPrefab, pos, rot);
            var pawn = go.GetComponent<NetworkedCombatPawn>();
            if (pawn == null) { Destroy(go); return; }

            pawn.CharacterOrEnemyDataName.Value = enemyData.enemyName;
            pawn.SlotIndex.Value                = 0;
            pawn.IsPlayerTeam.Value             = false;

            go.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            Debug.Log($"[BossFight] Boss spawneado → '{enemyData.enemyName}' pos:{pos}");
        }

        private void PositionCamera()
        {
            var cam = Camera.main;
            if (cam == null || arenaAssembler == null || !arenaAssembler.IsReady) return;

            // Disable CinemachineBrain if present — we control camera directly
            var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null) brain.enabled = false;

            if (arenaAssembler.CameraGameplayAnchor != null)
            {
                cam.transform.SetPositionAndRotation(
                    arenaAssembler.CameraGameplayAnchor.position,
                    arenaAssembler.CameraGameplayAnchor.rotation);
            }
            else if (arenaAssembler.PlayerRoot != null && arenaAssembler.EnemyRoot != null)
            {
                Vector3 center   = arenaAssembler.FieldCenter;
                Vector3 pPos     = arenaAssembler.PlayerRoot.position;
                Vector3 backDir  = (pPos - center).normalized;
                cam.transform.position = pPos + backDir * 3f + Vector3.up * 3.5f;
                cam.transform.LookAt(center + Vector3.up * 1f);
            }

            Debug.Log($"[BossFight] Cámara posicionada en {cam.transform.position}");
        }
    }
}
