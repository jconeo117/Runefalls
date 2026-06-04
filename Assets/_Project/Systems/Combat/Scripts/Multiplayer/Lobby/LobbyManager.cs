using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Runefall.Data;

namespace Runefall.Multiplayer.Lobby
{
    /// <summary>
    /// Cross-instance registry via PlayerPrefs — shared across MPPM virtual players.
    /// Sprint 7: replace TryResolve with Unity Relay JoinAllocation lookup.
    /// </summary>
    public static class LobbyCodeRegistry
    {
        private const string Prefix = "RF_Lobby_";

        public static void Register(string code, string ip)
        {
            PlayerPrefs.SetString(Prefix + code.ToUpper(), ip);
            PlayerPrefs.Save();
            Debug.Log($"[LobbyRegistry] '{code}' → {ip} guardado en PlayerPrefs");
        }

        public static bool TryResolve(string code, out string ip)
        {
            string key = Prefix + code.ToUpper().Trim();
            if (PlayerPrefs.HasKey(key))
            {
                ip = PlayerPrefs.GetString(key);
                return true;
            }
            ip = null;
            return false;
        }

        public static void Unregister(string code)
        {
            PlayerPrefs.DeleteKey(Prefix + code.ToUpper());
            PlayerPrefs.Save();
            Debug.Log($"[LobbyRegistry] '{code}' eliminado de PlayerPrefs");
        }
    }

    public class LobbyManager : MonoBehaviour
    {
        [SerializeField] private string combatSceneName = "Multiplayer_BossFight";
        [SerializeField] private ushort port            = 7777;

        [Header("Connection")]
        [Tooltip("On = Unity Relay (NAT traversal, internet). Off = direct IP / LAN fallback.")]
        [SerializeField] private bool   useRelay   = true;
        [Tooltip("Total players including the host. Used to size the Relay allocation.")]
        [SerializeField] private int    maxPlayers = 2;

        [Header("Character")]
        [Tooltip("Registry con characters ordenados: índice 0 = host (clientId 0), 1 = cliente 1, etc.")]
        [SerializeField] private MultiplayerCombatRegistry characterRegistry;

        [Header("Prefabs")]
        [SerializeField] private NetworkedLobbyManager networkedLobbyManagerPrefab;

        private void Awake()
        {
            if (characterRegistry != null) return;

#if UNITY_EDITOR
            characterRegistry = UnityEditor.AssetDatabase.LoadAssetAtPath<MultiplayerCombatRegistry>(
                "Assets/_Project/Systems/Combat/ScriptableObjects/Combat/MultiplayerCombatRegistry.asset");
            if (characterRegistry != null)
                Debug.Log($"[Lobby] Registry cargado: {characterRegistry.name} " +
                          $"({characterRegistry.characters?.Count ?? 0} personajes)");
            else
                Debug.LogError("[Lobby] No se encontró MultiplayerCombatRegistry en la ruta esperada.");
#else
            Debug.LogError("[Lobby] characterRegistry no asignado en Inspector (requerido en build).");
#endif
        }

        public event Action         OnHostStarted;
        public event Action         OnClientConnected;
        public event Action         OnDisconnected;
        public event Action<string> OnConnectionFailed;
        public event Action<int>    OnPlayerCountChanged;

        public bool   IsHost         => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        public int    ConnectedCount => NetworkManager.Singleton?.ConnectedClients?.Count ?? 0;
        public string CurrentCode    { get; private set; }

        [SerializeField] private float connectionTimeoutSeconds = 5f;
        private Coroutine _connectTimeout;

        private bool _subscribed;

        private void Start()
        {
            // Start() runs after all Awake() — NetworkManager.Singleton guaranteed ready
            Subscribe();
        }

        private void OnEnable()
        {
            // Only re-subscribe if already subscribed before (i.e., after a disable/enable cycle)
            // Avoids null-singleton noise on first enable before Start() runs
            if (!_subscribed) return;
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogWarning("[Lobby] NetworkManager.Singleton es null al suscribir. " +
                                 "Verifica que NetworkManager existe en la escena.");
                return;
            }
            if (_subscribed) return;
            NetworkManager.Singleton.OnClientConnectedCallback   += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
            NetworkManager.Singleton.OnTransportFailure          += HandleTransportFailure;
            _subscribed = true;
            Debug.Log("[Lobby] Callbacks de NetworkManager suscritos.");
        }

        private void Unsubscribe()
        {
            if (!_subscribed || NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.OnClientConnectedCallback   -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
            NetworkManager.Singleton.OnTransportFailure          -= HandleTransportFailure;
            _subscribed = false;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void CreateRoom()
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[Lobby] NetworkManager.Singleton es null. ¿Existe en la escena?");
                OnConnectionFailed?.Invoke("Error interno: NetworkManager no inicializado.");
                return;
            }

            if (useRelay) _ = CreateRoomRelayAsync();
            else          CreateRoomDirectIp();
        }

        // Relay: the join code clients use IS the Relay code (no IP shared, NAT traversed).
        private async Task CreateRoomRelayAsync()
        {
            try
            {
                CurrentCode = await RelayConnectionService.CreateAllocationAsync(maxPlayers);
                Debug.Log($"[Lobby] StartHost() vía Relay | JoinCode: {CurrentCode}");
                NetworkManager.Singleton.StartHost();

                SpawnNetworkedLobbyManager();
                StartCoroutine(RegisterCharacterDelayed());

                OnHostStarted?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Lobby] Relay host falló: {e}");
                OnConnectionFailed?.Invoke("No se pudo crear la sala (Relay). Verifica tu conexión.");
            }
        }

        private void CreateRoomDirectIp()
        {
            string ip = GetLocalIP();
            CurrentCode = GenerateCode();

            LobbyCodeRegistry.Register(CurrentCode, ip);
            SetTransport("0.0.0.0", port);
            Debug.Log($"[Lobby] StartHost() → escuchando en 0.0.0.0:{port}");
            NetworkManager.Singleton.StartHost();

            SpawnNetworkedLobbyManager();
            StartCoroutine(RegisterCharacterDelayed()); // host registra su propio personaje

            Debug.Log($"[Lobby] ✅ Sala creada | Código: {CurrentCode} | IP: {ip} | Puerto: {port}");
            OnHostStarted?.Invoke();
        }

        private void SpawnNetworkedLobbyManager()
        {
            if (networkedLobbyManagerPrefab == null)
            {
                Debug.LogError("[Lobby] networkedLobbyManagerPrefab no asignado en Inspector.");
                return;
            }
            if (NetworkedLobbyManager.Instance != null) return; // ya existe

            var go  = Instantiate(networkedLobbyManagerPrefab.gameObject);
            var net = go.GetComponent<NetworkObject>();
            net.Spawn(destroyWithScene: false); // sobrevive carga de escena
            Debug.Log("[Lobby] NetworkedLobbyManager spawneado.");
        }

        public void JoinRoom(string codeOrIp)
        {
            string input = (codeOrIp ?? "").Trim();

            if (string.IsNullOrEmpty(input))
            {
                OnConnectionFailed?.Invoke("Ingresa un código de sala o una IP.");
                return;
            }
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[Lobby] NetworkManager.Singleton es null en JoinRoom.");
                OnConnectionFailed?.Invoke("Error interno: NetworkManager no inicializado.");
                return;
            }

            if (useRelay) _ = JoinRoomRelayAsync(input.ToUpper());
            else          JoinRoomDirectIp(input);
        }

        // Relay: input is the Relay join code (uppercase). No IP involved.
        private async Task JoinRoomRelayAsync(string joinCode)
        {
            try
            {
                await RelayConnectionService.JoinAllocationAsync(joinCode);
                Debug.Log($"[Lobby] ⏳ StartClient() vía Relay | JoinCode: {joinCode}");
                NetworkManager.Singleton.StartClient();
                _connectTimeout = StartCoroutine(ConnectionTimeout());
            }
            catch (Exception e)
            {
                Debug.LogError($"[Lobby] Relay join falló: {e}");
                OnConnectionFailed?.Invoke("No se pudo unir a la sala. Verifica el código e intenta de nuevo.");
            }
        }

        private void JoinRoomDirectIp(string input)
        {
            string address;

            if (LobbyCodeRegistry.TryResolve(input.ToUpper(), out var resolved))
            {
                address = resolved;
                Debug.Log($"[Lobby] 🔍 Código '{input}' resuelto → {address}:{port}");
            }
            else if (System.Net.IPAddress.TryParse(input, out _))
            {
                address = input;
                Debug.Log($"[Lobby] 🔍 Conectando por IP directa → {address}:{port}");
            }
            else
            {
                Debug.LogWarning($"[Lobby] ❌ Código '{input}' no encontrado en registry y no es una IP válida.");
                OnConnectionFailed?.Invoke($"Código '{input}' no encontrado. Verifica e intenta de nuevo.");
                return;
            }

            Debug.Log($"[Lobby] ⏳ StartClient() → {address}:{port}");
            SetTransport(address, port);
            NetworkManager.Singleton.StartClient();
            _connectTimeout = StartCoroutine(ConnectionTimeout());
        }

        public void CancelJoin()
        {
            Debug.Log("[Lobby] 🚫 Conexión cancelada por el usuario.");
            StopTimeout();
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsConnectedClient)
                NetworkManager.Singleton.Shutdown();
            OnDisconnected?.Invoke();
        }

        public void LeaveRoom()
        {
            Debug.Log($"[Lobby] 🚪 Saliendo de sala | Código: {CurrentCode} | Era host: {IsHost}");

            if (!string.IsNullOrEmpty(CurrentCode))
                LobbyCodeRegistry.Unregister(CurrentCode);

            CurrentCode = null;
            NetworkManager.Singleton.Shutdown();
            OnDisconnected?.Invoke();
        }

        public void LoadCombatScene()
        {
            if (!IsHost) return;
            Debug.Log($"[Lobby] ⚔ Host cargando escena de combate: {combatSceneName}");
            NetworkManager.Singleton.SceneManager.LoadScene(
                combatSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        public string GetLocalIP()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect("8.8.8.8", 65530);
                return ((IPEndPoint)socket.LocalEndPoint).Address.ToString();
            }
            catch { return "127.0.0.1"; }
        }

        // ── Private ────────────────────────────────────────────────────────────

        private void SetTransport(string address, ushort p)
        {
            var t = NetworkManager.Singleton?.GetComponent<UnityTransport>();
            if (t != null) t.SetConnectionData(address, p);
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                int count = NetworkManager.Singleton.ConnectedClients.Count;
                Debug.Log($"[Lobby] 👤 Cliente {clientId} conectado. Jugadores en sala: {count}/2");
                OnPlayerCountChanged?.Invoke(count);
                return;
            }

            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                StopTimeout();
                Debug.Log($"[Lobby] ✅ Conectado al host. ClientId: {clientId}");
                StartCoroutine(RegisterCharacterDelayed());
                OnClientConnected?.Invoke();
            }
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return;

            if (NetworkManager.Singleton.IsServer)
            {
                int count = NetworkManager.Singleton.ConnectedClients.Count;
                Debug.Log($"[Lobby] 👤 Cliente {clientId} desconectado. Jugadores: {count}/2");
                OnPlayerCountChanged?.Invoke(count);
                return;
            }

            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                StopTimeout();
                Debug.Log("[Lobby] ⚠ Desconectado del host.");
                OnDisconnected?.Invoke();
            }
        }

        private void HandleTransportFailure()
        {
            StopTimeout();
            Debug.LogWarning("[Lobby] ❌ Error de transporte. Verifica IP y que el host esté activo.");
            OnConnectionFailed?.Invoke("No se pudo conectar. Verifica el código e intenta de nuevo.");
        }

        // ── Timeout ────────────────────────────────────────────────────────────

        private System.Collections.IEnumerator ConnectionTimeout()
        {
            float elapsed = 0f;
            while (elapsed < connectionTimeoutSeconds)
            {
                elapsed += UnityEngine.Time.unscaledDeltaTime;
                yield return null;
            }

            bool connected = NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;
            if (!connected)
            {
                Debug.LogWarning($"[Lobby] ⏱ Timeout ({connectionTimeoutSeconds}s). No se pudo conectar.");
                if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
                OnConnectionFailed?.Invoke("Tiempo agotado. Verifica el código e intenta de nuevo.");
            }
            _connectTimeout = null;
        }

        private void StopTimeout()
        {
            if (_connectTimeout == null) return;
            StopCoroutine(_connectTimeout);
            _connectTimeout = null;
        }

        private IEnumerator RegisterCharacterDelayed()
        {
            // Wait for NetworkedLobbyManager to replicate to client
            yield return null;
            yield return null;

            if (characterRegistry == null)
            {
                Debug.LogError("[Lobby] characterRegistry no asignado en Inspector.");
                yield break;
            }

            if (NetworkedLobbyManager.Instance == null)
            {
                Debug.LogError("[Lobby] NetworkedLobbyManager.Instance es null.");
                yield break;
            }

            ulong localId = NetworkManager.Singleton.LocalClientId;
            int   slot    = (int)localId;
            var   chars   = characterRegistry.characters;

            if (chars == null || chars.Count == 0)
            {
                Debug.LogError("[Lobby] MultiplayerCombatRegistry.characters vacío.");
                yield break;
            }

            if (slot >= chars.Count)
            {
                Debug.LogWarning($"[Lobby] ClientId {localId} sin slot en registry. Usando slot 0.");
                slot = 0;
            }

            var character = chars[slot];
            if (character == null)
            {
                Debug.LogError($"[Lobby] Registry.characters[{slot}] es null.");
                yield break;
            }

            NetworkedLobbyManager.Instance.RegisterCharacterServerRpc(character.characterName);
            Debug.Log($"[Lobby] 🎭 ClientId {localId} → '{character.characterName}'");
        }

        private static string GenerateCode()
        {
            // Excluye caracteres ambiguos: 0/O, 1/I, 5/S
            const string chars = "ABCDEFGHJKLMNPQRTUVWXYZ2346789";
            var sb  = new StringBuilder(6);
            var rng = new System.Random();
            for (int i = 0; i < 6; i++)
                sb.Append(chars[rng.Next(chars.Length)]);
            return sb.ToString();
        }
    }
}
