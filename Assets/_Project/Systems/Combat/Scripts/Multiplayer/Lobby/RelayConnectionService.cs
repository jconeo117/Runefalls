using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace Runefall.Multiplayer.Lobby
{
    /// <summary>
    /// Establishes the NGO connection over Unity Relay (NAT traversal, no port forwarding).
    ///
    /// Single responsibility: UGS bootstrap (initialize + anonymous sign-in) and Relay
    /// allocation/join, pointing the UnityTransport at the resulting RelayServerData. The host
    /// gets a join code to share; the client joins with it. Connection type is DTLS.
    ///
    /// Knows nothing about the lobby/game flow — LobbyManager calls this, then StartHost/StartClient
    /// exactly as before. Everything downstream of the connection is transport-agnostic and
    /// unchanged, so direct-IP and Relay are interchangeable.
    /// </summary>
    public static class RelayConnectionService
    {
        private const string ConnectionType = "dtls";

        /// <summary>Initializes UGS and signs in anonymously. Idempotent — safe to call repeatedly.</summary>
        public static async Task EnsureSignedInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>
        /// Host: allocate a Relay server, point the transport at it, and return the join code
        /// the clients use to connect.
        /// </summary>
        /// <param name="maxPlayers">Total players including the host.</param>
        public static async Task<string> CreateAllocationAsync(int maxPlayers)
        {
            await EnsureSignedInAsync();

            // Relay counts peer connections, i.e. everyone except the host.
            int maxPeers = System.Math.Max(1, maxPlayers - 1);
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPeers);
            string     joinCode   = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            GetTransport().SetRelayServerData(new RelayServerData(allocation, ConnectionType));
            return joinCode;
        }

        /// <summary>Client: join a Relay allocation by code and point the transport at it.</summary>
        public static async Task JoinAllocationAsync(string joinCode)
        {
            await EnsureSignedInAsync();

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            GetTransport().SetRelayServerData(new RelayServerData(joinAllocation, ConnectionType));
        }

        private static UnityTransport GetTransport()
        {
            var transport = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.GetComponent<UnityTransport>()
                : null;

            if (transport == null)
                throw new System.InvalidOperationException(
                    "[RelayConnectionService] UnityTransport not found on NetworkManager.");

            return transport;
        }
    }
}
