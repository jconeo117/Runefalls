using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Runefall.Presentation.Network
{
    /// <summary>
    /// Persistent Singleton manager controlling the network state, starting Hosts and Clients,
    /// integrating with Unity Transport, and handling scene loading across the network.
    /// </summary>
    public class MultiplayerManager : MonoBehaviour
    {
        public static MultiplayerManager Instance { get; private set; }

        private LobbyRelayService _lobbyRelayService;
        private LobbyConnectionData _currentLobbyData;

        /// <summary>
        /// Triggered when connection state changes. Passes true on successful connection,
        /// and false on disconnection or failure.
        /// </summary>
        public event Action<bool> OnConnectionStateChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                _lobbyRelayService = new LobbyRelayService();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Initializes services, registers a private lobby in UGS, allocates a Relay server slot,
        /// sets up UnityTransport, and starts the local client as the Host.
        /// Returns the short Join Code of the session.
        /// </summary>
        public async Task<string> CreateMatchAsync()
        {
            try
            {
                // 1. Initialize UGS
                await _lobbyRelayService.InitializeServicesAsync();

                // 2. Register Lobby in the cloud (LobbyRelayService handles Allocation & Lobby binding)
                _currentLobbyData = await _lobbyRelayService.CreateLobbyAsync();

                // 3. Obtain cached allocation details directly from LobbyRelayService
                var allocation = _lobbyRelayService.HostAllocation;
                if (allocation == null)
                {
                    throw new Exception("Host Relay allocation was not initialized.");
                }

                // 4. Configure UnityTransport component
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport == null)
                {
                    throw new Exception("UnityTransport component not found on NetworkManager GameObject.");
                }

                // Inject allocation details into the transport (Host uses connectionData as hostConnectionData too)
                transport.SetRelayServerData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData,
                    allocation.ConnectionData
                );

                // 5. Start Netcode Host
                bool started = NetworkManager.Singleton.StartHost();
                if (!started)
                {
                    throw new Exception("Failed to start Netcode Host.");
                }

                Debug.Log($"[MultiplayerManager] Host started successfully. Lobby Code: {_currentLobbyData.JoinCode}");
                OnConnectionStateChanged?.Invoke(true);
                return _currentLobbyData.JoinCode;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultiplayerManager] Error creating match: {ex.Message}");
                OnConnectionStateChanged?.Invoke(false);
                throw;
            }
        }

        /// <summary>
        /// Joins an existing Lobby, fetches its Host allocation credentials,
        /// configures UnityTransport, and starts the local client.
        /// </summary>
        public async Task JoinMatchAsync(string joinCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(joinCode))
                {
                    throw new ArgumentException("Join Code cannot be empty.", nameof(joinCode));
                }

                // 1. Initialize UGS
                await _lobbyRelayService.InitializeServicesAsync();

                // 2. Join Lobby and extract Host's Relay Join Code
                string relayJoinCode = await _lobbyRelayService.JoinLobbyAndGetRelayCodeAsync(joinCode);

                // 3. Resolve allocation from Relay
                var joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

                // 4. Configure UnityTransport component
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport == null)
                {
                    throw new Exception("UnityTransport component not found on NetworkManager GameObject.");
                }

                // Inject allocation details into the transport
                transport.SetRelayServerData(
                    joinAllocation.RelayServer.IpV4,
                    (ushort)joinAllocation.RelayServer.Port,
                    joinAllocation.AllocationIdBytes,
                    joinAllocation.Key,
                    joinAllocation.ConnectionData,
                    joinAllocation.HostConnectionData
                );

                // 5. Start Netcode Client
                bool started = NetworkManager.Singleton.StartClient();
                if (!started)
                {
                    throw new Exception("Failed to start Netcode Client.");
                }

                Debug.Log($"[MultiplayerManager] Client joined successfully using code: {joinCode}");
                OnConnectionStateChanged?.Invoke(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultiplayerManager] Error joining match: {ex.Message}");
                OnConnectionStateChanged?.Invoke(false);
                throw;
            }
        }

        /// <summary>
        /// Loads the specified scene across the network for all active clients.
        /// Can only be called by the Server (Host).
        /// </summary>
        public void LoadCombatScene(string sceneName = "BossFight_Arena")
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                Debug.Log($"[MultiplayerManager] Loading network scene: {sceneName}");
                NetworkManager.Singleton.SceneManager.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            else
            {
                Debug.LogWarning("[MultiplayerManager] LoadCombatScene failed. Only the Host/Server can trigger network scene loading.");
            }
        }

        /// <summary>
        /// Cleans up network status, stopping Host or Client and shutting down current session.
        /// </summary>
        public void Shutdown()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
                Debug.Log("[MultiplayerManager] NetworkManager shut down.");
            }
            OnConnectionStateChanged?.Invoke(false);
        }
    }
}
