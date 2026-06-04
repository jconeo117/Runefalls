using UnityEngine;
using UnityEngine.UIElements;

namespace Runefall.Multiplayer.Lobby
{
    public enum LobbyUIState { Hidden, Main, Transition, WaitRoom }

    [RequireComponent(typeof(UIDocument))]
    public class LobbyUIController : MonoBehaviour
    {
        private LobbyManager          _manager;
        private LobbyPlayerController _playerController;
        private UIDocument            _doc;
        private LobbyUIState          _state = LobbyUIState.Hidden;

        // Main panel
        private VisualElement _mainPanel;
        private Button        _createBtn;
        private TextField     _ipInput;
        private Button        _joinBtn;
        private Label         _mainError;
        private Button        _mainCloseBtn;

        // Transition panel
        private VisualElement _transitionPanel;
        private Label         _transitionLabel;
        private Button        _cancelBtn;
        private Coroutine     _dotsCoroutine;

        // WaitRoom panel
        private VisualElement _waitPanel;
        private Label         _roomCodeLabel;
        private Label         _player1Label;
        private Label         _player2Label;
        private Label         _waitStatus;
        private Button        _startBtn;
        private Button        _leaveBtn;
        private Button        _waitCloseBtn;

        private void Awake()
        {
            _doc              = GetComponent<UIDocument>();
            _manager          = FindFirstObjectByType<LobbyManager>();
            _playerController = FindFirstObjectByType<LobbyPlayerController>();

            if (_manager == null)
            {
                Debug.LogError("[LobbyUIController] LobbyManager not found.", this);
                return;
            }

            QueryElements();
            BindButtons();
            BindManagerEvents();
            SetState(LobbyUIState.Hidden);
        }

        public void Show()
        {
            SetMovement(false);
            SetState(LobbyUIState.Main);
        }

        public void Hide()
        {
            SetMovement(true);
            SetState(LobbyUIState.Hidden);
        }

        // ── private ────────────────────────────────────────────────────────────

        private void QueryElements()
        {
            var root = _doc.rootVisualElement;

            _mainPanel    = root.Q("main-panel");
            _createBtn    = root.Q<Button>("create-btn");
            _ipInput      = root.Q<TextField>("ip-input");
            _joinBtn      = root.Q<Button>("join-btn");
            _mainError    = root.Q<Label>("main-error");
            _mainCloseBtn = root.Q<Button>("main-close-btn");

            _transitionPanel = root.Q("transition-panel");
            _transitionLabel = root.Q<Label>("transition-label");
            _cancelBtn       = root.Q<Button>("cancel-btn");

            _waitPanel     = root.Q("waitroom-panel");
            _roomCodeLabel = root.Q<Label>("room-code-label");
            _player1Label  = root.Q<Label>("player1-label");
            _player2Label  = root.Q<Label>("player2-label");
            _waitStatus    = root.Q<Label>("wait-status");
            _startBtn      = root.Q<Button>("start-btn");
            _leaveBtn      = root.Q<Button>("leave-btn");
            _waitCloseBtn  = root.Q<Button>("wait-close-btn");
        }

        private void BindButtons()
        {
            _createBtn?.RegisterCallback<ClickEvent>(_ => OnCreate());
            _joinBtn?.RegisterCallback<ClickEvent>(_ => OnJoin());
            _cancelBtn?.RegisterCallback<ClickEvent>(_ => OnCancel());
            _startBtn?.RegisterCallback<ClickEvent>(_ => _manager.LoadCombatScene());
            _leaveBtn?.RegisterCallback<ClickEvent>(_ => OnLeave());
            _mainCloseBtn?.RegisterCallback<ClickEvent>(_ => Hide());
            _waitCloseBtn?.RegisterCallback<ClickEvent>(_ => Hide());
        }

        private void BindManagerEvents()
        {
            _manager.OnHostStarted        += OnHostStarted;
            _manager.OnClientConnected    += OnClientConnected;
            _manager.OnDisconnected       += () => { StopDotsAnimation(); SetState(LobbyUIState.Main); SetMovement(false); };
            _manager.OnConnectionFailed   += ShowMainError;
            _manager.OnPlayerCountChanged += RefreshWaitRoom;
        }

        private void OnCreate()
        {
            ClearError();
            SetState(LobbyUIState.Transition);
            _manager.CreateRoom();
        }

        private void OnJoin()
        {
            ClearError();
            SetState(LobbyUIState.Transition);
            StartDotsAnimation();
            _manager.JoinRoom(_ipInput?.value);
        }

        private void OnCancel()
        {
            StopDotsAnimation();
            _manager.CancelJoin();
        }

        private void OnLeave()
        {
            _manager.LeaveRoom();
            SetMovement(false); // vuelve al panel principal, sigue bloqueado
        }

        private void OnHostStarted()
        {
            SetState(LobbyUIState.WaitRoom);
            if (_roomCodeLabel != null)
                _roomCodeLabel.text = $"Código de sala:  {_manager.CurrentCode}";
            RefreshWaitRoom(1);
        }

        private void OnClientConnected()
        {
            StopDotsAnimation();
            SetState(LobbyUIState.WaitRoom);
            if (_roomCodeLabel != null)
                _roomCodeLabel.text = "Conectado ✓";
            RefreshWaitRoom(2);
        }

        private void RefreshWaitRoom(int playerCount)
        {
            if (_state != LobbyUIState.WaitRoom) return;

            bool isHost = _manager.IsHost;

            if (_player1Label != null)
                _player1Label.text = isHost ? "Jugador 1  ·  Tú (Host)" : "Jugador 1  ·  Host";

            if (_player2Label != null)
            {
                bool p2Ready = playerCount >= 2;
                _player2Label.text = p2Ready
                    ? "Jugador 2  ·  " + (isHost ? "Conectado ✓" : "Tú")
                    : "Jugador 2  ·  Esperando...";
                _player2Label.EnableInClassList("slot-waiting", !p2Ready);
                _player2Label.EnableInClassList("slot-ready",    p2Ready);
            }

            if (_waitStatus != null)
                _waitStatus.text = playerCount >= 2
                    ? "¡Listos para iniciar!"
                    : "Esperando al segundo jugador...";

            if (_startBtn != null)
            {
                _startBtn.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
                _startBtn.SetEnabled(playerCount >= 2);
            }
        }

        private void SetState(LobbyUIState next)
        {
            _state = next;
            bool visible = next != LobbyUIState.Hidden;
            _doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            Show(_mainPanel,       next == LobbyUIState.Main);
            Show(_transitionPanel, next == LobbyUIState.Transition);
            Show(_waitPanel,       next == LobbyUIState.WaitRoom);
        }

        private void ShowMainError(string msg)
        {
            StopDotsAnimation();
            SetState(LobbyUIState.Main);
            SetMovement(false);
            if (_mainError != null)
            {
                _mainError.text = msg;
                _mainError.style.display = DisplayStyle.Flex;
            }
        }

        private void ClearError()
        {
            if (_mainError != null) _mainError.style.display = DisplayStyle.None;
        }

        private void SetMovement(bool enabled)
        {
            if (_playerController != null)
                _playerController.enabled = enabled;
        }

        private static void Show(VisualElement el, bool visible)
        {
            if (el != null)
                el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void StartDotsAnimation()
        {
            StopDotsAnimation();
            _dotsCoroutine = StartCoroutine(DotsLoop());
        }

        private void StopDotsAnimation()
        {
            if (_dotsCoroutine == null) return;
            StopCoroutine(_dotsCoroutine);
            _dotsCoroutine = null;
        }

        private System.Collections.IEnumerator DotsLoop()
        {
            string[] frames = { "Conectando", "Conectando.", "Conectando..", "Conectando..." };
            int i = 0;
            while (true)
            {
                if (_transitionLabel != null) _transitionLabel.text = frames[i % frames.Length];
                i++;
                yield return new WaitForSecondsRealtime(0.45f);
            }
        }
    }
}
