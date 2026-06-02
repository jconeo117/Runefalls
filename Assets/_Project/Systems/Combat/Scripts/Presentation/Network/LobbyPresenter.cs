using UnityEngine;
using Unity.Netcode;

namespace Runefall.Presentation.Network
{
    /// <summary>
    /// Presenter managing the lifecycle of the Lobby UI.
    /// Binds LobbyView user inputs to MultiplayerManager service actions,
    /// and listens to direct Netcode connection events to update players statuses.
    /// </summary>
    [RequireComponent(typeof(LobbyView))]
    public class LobbyPresenter : MonoBehaviour
    {
        [Header("Wait Room Assembler")]
        [SerializeField] private WaitRoomAssembler waitRoomAssembler;

        private LobbyView _view;

        private void Awake()
        {
            _view = GetComponent<LobbyView>();
        }

        private void OnEnable()
        {
            _view.OnCreateClicked += HandleCreateLobby;
            _view.OnJoinClicked += HandleJoinLobby;
            _view.OnStartGameClicked += HandleStartGame;
            _view.OnCloseClicked += HandleCloseLobby;
            _view.OnReadyClicked += HandleReady;
            _view.OnCancelPrepClicked += HandleCancelPrep;
            _view.OnLeaveLobbyClicked += HandleLeaveLobby;

            // Bind native Netcode connection callbacks
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
            }
        }

        private void OnDisable()
        {
            _view.OnCreateClicked -= HandleCreateLobby;
            _view.OnJoinClicked -= HandleJoinLobby;
            _view.OnStartGameClicked -= HandleStartGame;
            _view.OnCloseClicked -= HandleCloseLobby;
            _view.OnReadyClicked -= HandleReady;
            _view.OnCancelPrepClicked -= HandleCancelPrep;
            _view.OnLeaveLobbyClicked -= HandleLeaveLobby;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
        }

        private void HandleCloseLobby()
        {
            _view.CloseUI();

            // Buscar al jugador en el lobby para volver a bloquear el cursor y que pueda seguir caminando
            var playerCtrl = FindFirstObjectByType<SimpleCharacterController>();
            if (playerCtrl == null)
            {
                #pragma warning disable CS0618
                playerCtrl = FindObjectOfType<SimpleCharacterController>();
                #pragma warning restore CS0618
            }

            if (playerCtrl != null)
            {
                playerCtrl.SetCursorState(locked: true);
                if (waitRoomAssembler != null)
                {
                    waitRoomAssembler.TeardownWaitRoom(playerCtrl);
                }
            }

            // Notificar al trigger de la puerta enorme que la UI fue cerrada manualmente
            var doorTrigger = FindFirstObjectByType<BossRoomDoorTrigger>();
            if (doorTrigger == null)
            {
                #pragma warning disable CS0618
                doorTrigger = FindObjectOfType<BossRoomDoorTrigger>();
                #pragma warning restore CS0618
            }

            if (doorTrigger != null)
            {
                doorTrigger.NotifyUIClose();
            }
        }

        private void HandleReady()
        {
            Debug.Log("[LobbyPresenter] local player toggled READY.");
            // To be synchronised in network (Fase B/Día 2).
            // For now, mock a visual confirmation:
            if (NetworkManager.Singleton != null)
            {
                int currentPlayers = NetworkManager.Singleton.ConnectedClients.Count;
                _view.UpdatePlayerList(currentPlayers, 2, currentPlayers > 1);
            }
        }

        private void HandleCancelPrep()
        {
            Debug.Log("[LobbyPresenter] local player canceled PREPARATION (standby).");
        }

        private void HandleLeaveLobby()
        {
            Debug.Log("[LobbyPresenter] Leaving lobby, shutting down services...");
            _view.SetLoadingState(true);
            try
            {
                MultiplayerManager.Instance.Shutdown();
                _view.ShowMainMenu();

                var playerCtrl = FindFirstObjectByType<SimpleCharacterController>();
                if (playerCtrl == null)
                {
                    #pragma warning disable CS0618
                    playerCtrl = FindObjectOfType<SimpleCharacterController>();
                    #pragma warning restore CS0618
                }

                if (playerCtrl != null && waitRoomAssembler != null)
                {
                    waitRoomAssembler.TeardownWaitRoom(playerCtrl);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LobbyPresenter] Error leaving lobby: {ex.Message}");
            }
            finally
            {
                _view.SetLoadingState(false);
            }
        }

        private async void HandleCreateLobby()
        {
            _view.SetLoadingState(true);
            try
            {
                string code = await MultiplayerManager.Instance.CreateMatchAsync();
                _view.ShowWaitingRoom(code, isHost: true);
                UpdateWaitingRoomUI();

                var localPlayer = FindFirstObjectByType<SimpleCharacterController>();
                if (localPlayer == null)
                {
                    #pragma warning disable CS0618
                    localPlayer = FindObjectOfType<SimpleCharacterController>();
                    #pragma warning restore CS0618
                }

                if (waitRoomAssembler != null && localPlayer != null)
                {
                    waitRoomAssembler.AssembleWaitRoom(localPlayer);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LobbyPresenter] Create Lobby failed: {ex.Message}");
                _view.SetLoadingState(false);
                _view.ShowMainMenu();
            }
        }

        private async void HandleJoinLobby(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogWarning("[LobbyPresenter] Cannot join. Join Code is empty.");
                return;
            }

            _view.SetLoadingState(true);
            try
            {
                await MultiplayerManager.Instance.JoinMatchAsync(code);
                _view.ShowWaitingRoom(code, isHost: false);
                UpdateWaitingRoomUI();

                var localPlayer = FindFirstObjectByType<SimpleCharacterController>();
                if (localPlayer == null)
                {
                    #pragma warning disable CS0618
                    localPlayer = FindObjectOfType<SimpleCharacterController>();
                    #pragma warning restore CS0618
                }

                if (waitRoomAssembler != null && localPlayer != null)
                {
                    waitRoomAssembler.AssembleWaitRoom(localPlayer);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LobbyPresenter] Join Lobby failed: {ex.Message}");
                _view.SetLoadingState(false);
                _view.ShowMainMenu();
            }
        }

        private void HandleStartGame()
        {
            _view.SetLoadingState(true);
            
            // Server (Host) triggers scene loading across the network
            MultiplayerManager.Instance.LoadCombatScene("BossFight_Arena");
        }

        private void HandleClientConnected(ulong clientId)
        {
            Debug.Log($"[LobbyPresenter] Netcode client connected with network ID: {clientId}");
            UpdateWaitingRoomUI();
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            Debug.Log($"[LobbyPresenter] Netcode client disconnected with network ID: {clientId}");
            UpdateWaitingRoomUI();
        }

        private void UpdateWaitingRoomUI()
        {
            if (NetworkManager.Singleton == null) return;

            int currentPlayers = NetworkManager.Singleton.ConnectedClients.Count;
            // More than 1 connected client implies the Ally Player has joined
            bool hasClientJoined = currentPlayers > 1;

            _view.UpdatePlayerList(currentPlayers, maxPlayers: 2, hasClientJoined);
        }
    }
}
