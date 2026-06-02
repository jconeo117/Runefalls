using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Runefall.Presentation.Network
{
    /// <summary>
    /// Service wrapper handling asynchronous calls to Unity Gaming Services (UGS),
    /// specifically Lobbies and Relay APIs. Operates purely on data and is decoupled from UI.
    /// </summary>
    public class LobbyRelayService
    {
        private const int MaxPlayers = 2;
        private const string RelayJoinCodeKey = "RelayJoinCode";

        /// <summary>
        /// Stores the active Host allocation details so the Host can configure their transport directly.
        /// </summary>
        public Allocation HostAllocation { get; private set; }

        /// <summary>
        /// Initializes Unity Services and authenticates the player anonymously.
        /// </summary>
        public async Task InitializeServicesAsync()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log($"[UGS] Authenticated successfully. PlayerID: {AuthenticationService.Instance.PlayerId}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UGS] Failed to initialize services or sign in: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates a new allocation in Unity Relay and binds it to a private Unity Lobby.
        /// Returns the lobby connection structure containing the Join Code.
        /// </summary>
        public async Task<LobbyConnectionData> CreateLobbyAsync()
        {
            try
            {
                // 1. Create a Relay allocation for up to 2 players
                var allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers);
                HostAllocation = allocation; // Store for Host transport configuration
                
                // 2. Fetch the Join Code corresponding to this allocation
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                Debug.Log($"[Relay] Created allocation. Generated Relay Join Code: {joinCode}");

                // 3. Configure the private Lobby with the Relay Join Code as metadata
                var lobbyOptions = new CreateLobbyOptions
                {
                    IsPrivate = true,
                    Data = new Dictionary<string, DataObject>
                    {
                        { RelayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                    }
                };

                // 4. Create the Lobby
                string lobbyName = $"Runefalls_Lobby_{Guid.NewGuid().ToString().Substring(0, 5)}";
                var lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, MaxPlayers, lobbyOptions);
                Debug.Log($"[Lobby] Created private lobby: {lobby.Name} (ID: {lobby.Id}, LobbyCode: {lobby.LobbyCode})");

                // Return LobbyCode as the short JoinCode so that clients can use it to join!
                return new LobbyConnectionData(lobby.Id, lobby.LobbyCode, isHost: true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbyRelayService] Error in CreateLobbyAsync: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Joins an existing Unity Lobby by its short Join Code and extracts the Host's Relay Join Code.
        /// </summary>
        public async Task<string> JoinLobbyAndGetRelayCodeAsync(string joinCode)
        {
            try
            {
                // 1. Join the Lobby by code
                var lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(joinCode);
                Debug.Log($"[Lobby] Joined lobby successfully: {lobby.Name} (ID: {lobby.Id})");

                // 2. Extract the Relay connection code from Lobby metadata
                if (lobby.Data != null && lobby.Data.TryGetValue(RelayJoinCodeKey, out var lobbyDataObject))
                {
                    return lobbyDataObject.Value;
                }

                throw new Exception("Relay join code metadata not found inside the Lobby.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbyRelayService] Error in JoinLobbyAndGetRelayCodeAsync: {ex.Message}");
                throw;
            }
        }
    }
}
