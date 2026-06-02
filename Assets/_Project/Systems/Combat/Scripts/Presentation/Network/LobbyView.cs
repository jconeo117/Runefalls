using System;
using UnityEngine;
using UnityEngine.UI;

namespace Runefall.Presentation.Network
{
    /// <summary>
    /// UI View component for the Network Lobby, mapping screen components
    /// and buttons to simple actions and events. Completely decoupled from Netcode.
    /// </summary>
    public class LobbyView : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject panelMenu;
        [SerializeField] private GameObject panelWaitingRoom;

        [Header("Menu Elements")]
        [SerializeField] private Button buttonCreate;
        [SerializeField] private Button buttonJoin;
        [SerializeField] private Button buttonClose;
        [SerializeField] private TMPro.TMP_InputField inputFieldCode;

        [Header("Waiting Room Elements")]
        [SerializeField] private TMPro.TMP_Text textJoinCode;
        [SerializeField] private TMPro.TMP_Text textPlayerCount;
        [SerializeField] private TMPro.TMP_Text textPlayer1Status;
        [SerializeField] private TMPro.TMP_Text textPlayer2Status;
        [SerializeField] private Button buttonStartGame;
        [SerializeField] private Button buttonReady;
        [SerializeField] private Button buttonCancelPrep;
        [SerializeField] private Button buttonLeaveLobby;

        [Header("Loading Overlay")]
        [SerializeField] private GameObject loadingOverlay;

        // UI Action Events
        public event Action OnCreateClicked;
        public event Action<string> OnJoinClicked;
        public event Action OnStartGameClicked;
        public event Action OnCloseClicked;
        public event Action OnReadyClicked;
        public event Action OnCancelPrepClicked;
        public event Action OnLeaveLobbyClicked;

        private void Awake()
        {
            ShowMainMenu();
        }

        private void Start()
        {
            // Register listeners in Start to guarantee all serialized and injected references are resolved
            if (buttonCreate != null)
            {
                buttonCreate.onClick.RemoveAllListeners();
                buttonCreate.onClick.AddListener(() => OnCreateClicked?.Invoke());
            }
            
            if (buttonJoin != null)
            {
                buttonJoin.onClick.RemoveAllListeners();
                buttonJoin.onClick.AddListener(() => OnJoinClicked?.Invoke(inputFieldCode.text.Trim().ToUpper()));
            }
            
            if (buttonClose != null)
            {
                buttonClose.onClick.RemoveAllListeners();
                buttonClose.onClick.AddListener(() => OnCloseClicked?.Invoke());
            }
            
            if (buttonStartGame != null)
            {
                buttonStartGame.onClick.RemoveAllListeners();
                buttonStartGame.onClick.AddListener(() => OnStartGameClicked?.Invoke());
            }

            if (buttonReady != null)
            {
                buttonReady.onClick.RemoveAllListeners();
                buttonReady.onClick.AddListener(() => OnReadyClicked?.Invoke());
            }

            if (buttonCancelPrep != null)
            {
                buttonCancelPrep.onClick.RemoveAllListeners();
                buttonCancelPrep.onClick.AddListener(() => OnCancelPrepClicked?.Invoke());
            }

            if (buttonLeaveLobby != null)
            {
                buttonLeaveLobby.onClick.RemoveAllListeners();
                buttonLeaveLobby.onClick.AddListener(() => OnLeaveLobbyClicked?.Invoke());
            }
        }

        /// <summary>
        /// Displays the initial "Create or Join" panel.
        /// </summary>
        public void ShowMainMenu()
        {
            if (panelMenu != null) panelMenu.SetActive(true);
            if (panelWaitingRoom != null) panelWaitingRoom.SetActive(false);
            if (loadingOverlay != null) loadingOverlay.SetActive(false);
        }

        /// <summary>
        /// Displays the Waiting Room panel, activating buttons for the Host.
        /// </summary>
        public void ShowWaitingRoom(string joinCode, bool isHost)
        {
            if (panelMenu != null) panelMenu.SetActive(false);
            if (panelWaitingRoom != null) panelWaitingRoom.SetActive(true);
            if (loadingOverlay != null) loadingOverlay.SetActive(false);
            
            if (textJoinCode != null)
            {
                textJoinCode.text = $"CÓDIGO DE SALA: {joinCode}";
            }

            if (buttonStartGame != null)
            {
                buttonStartGame.gameObject.SetActive(isHost);
                buttonStartGame.interactable = true;
            }
        }

        /// <summary>
        /// Sincroniza visualmente los jugadores presentes y su estado en el panel de sala de espera.
        /// </summary>
        public void UpdatePlayerList(int currentPlayers, int maxPlayers, bool hasClientJoined)
        {
            if (textPlayerCount != null)
            {
                textPlayerCount.text = $"JUGADORES EN SALA: {currentPlayers} / {maxPlayers}";
            }

            if (textPlayer1Status != null)
            {
                textPlayer1Status.text = "Jugador 1 (Host): <color=green>LISTO</color>";
            }

            if (textPlayer2Status != null)
            {
                if (hasClientJoined)
                {
                    textPlayer2Status.text = "Jugador 2 (Aliado): <color=green>CONECTADO</color>";
                }
                else
                {
                    textPlayer2Status.text = "Jugador 2: <color=yellow>ESPERANDO...</color>";
                }
            }
        }

        /// <summary>
        /// Activa o desactiva la pantalla negra/bloqueo de carga asíncrona.
        /// </summary>
        public void SetLoadingState(bool isLoading)
        {
            if (loadingOverlay != null)
            {
                loadingOverlay.SetActive(isLoading);
            }
        }

        /// <summary>
        /// Deactivates the Lobby Canvas UI GameObject, closing the overlay entirely.
        /// </summary>
        public void CloseUI()
        {
            gameObject.SetActive(false);
        }
    }
}
