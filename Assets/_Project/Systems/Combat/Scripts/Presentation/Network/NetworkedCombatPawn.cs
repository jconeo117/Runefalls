using Unity.Netcode;
using UnityEngine;
using Runefall.Presentation.Combat;

namespace Runefall.Presentation.Network
{
    public enum CombatRole { HostPlayer, ClientPlayer, Boss }

    /// <summary>
    /// Lightweight network wrapper prefab attached to player avatars and the boss inside BossFight_Arena.
    /// Handles physical netcode synchronisation using NetworkTransform, while dynamically spawning
    /// the local visual meshes/animators on OnNetworkSpawn to remain fully compatible with offline prefabs.
    /// </summary>
    public class NetworkedCombatPawn : NetworkBehaviour
    {
        [Header("Role Settings")]
        public NetworkVariable<CombatRole> Role = new(
            CombatRole.HostPlayer, 
            NetworkVariableReadPermission.Everyone, 
            NetworkVariableWritePermission.Server
        );

        [Header("Visual Prefabs")]
        [Tooltip("The high-fidelity visual model prefab for the Player (e.g. Ice Queen mesh/animator).")]
        [SerializeField] private GameObject playerVisualPrefab;

        [Tooltip("The high-fidelity visual model prefab for the Boss (e.g. Orco mesh/animator).")]
        [SerializeField] private GameObject bossVisualPrefab;

        [Header("UI Prefabs")]
        [SerializeField] private GameObject combatCanvasPrefab;

        private GameObject _instantiatedVisual;

        public override void OnNetworkSpawn()
        {
            InstantiateVisualModel();
            ConfigureCameraForLocalOwner();

            // Commented out: dynamic UI is now instantiated & wired by premium local CombatBootstrapper!
            /*
            if (IsOwner && (Role.Value == CombatRole.HostPlayer || Role.Value == CombatRole.ClientPlayer))
            {
                InstantiateLocalUI();
            }
            */
        }

        /// <summary>
        /// Spawns the high-fidelity visual models (e.g. Ice Queen / Orco) from ScriptableObjects, or fallbacks if missing.
        /// </summary>
        private void InstantiateVisualModel()
        {
            var netTM = FindFirstObjectByType<NetworkedTurnManager>();
            GameObject visualPrefab = null;
            RuntimeAnimatorController runtimeAnimController = null;

            if (netTM != null)
            {
                if (Role.Value == CombatRole.HostPlayer)
                {
                    visualPrefab = netTM.PlayerCharacterData != null ? netTM.PlayerCharacterData.prefab : null;
                    runtimeAnimController = netTM.PlayerCharacterData != null ? netTM.PlayerCharacterData.animatorController : null;
                }
                else if (Role.Value == CombatRole.ClientPlayer)
                {
                    visualPrefab = netTM.ClientCharacterData != null ? netTM.ClientCharacterData.prefab : null;
                    runtimeAnimController = netTM.ClientCharacterData != null ? netTM.ClientCharacterData.animatorController : null;
                }
                else if (Role.Value == CombatRole.Boss)
                {
                    visualPrefab = netTM.BossEnemyData != null ? netTM.BossEnemyData.prefab : null;
                    runtimeAnimController = netTM.BossEnemyData != null ? netTM.BossEnemyData.animatorController : null;
                }
            }

            if (visualPrefab != null)
            {
                _instantiatedVisual = Instantiate(visualPrefab, transform);
                _instantiatedVisual.transform.localPosition = GetLocalMirrorOffset();
                _instantiatedVisual.transform.localRotation = Quaternion.identity;

                var anim = _instantiatedVisual.GetComponentInChildren<Animator>();
                if (anim != null && runtimeAnimController != null)
                {
                    anim.runtimeAnimatorController = runtimeAnimController;
                }

                _instantiatedVisual.name = Role.Value.ToString() + "_Visual";
                
                // Add the CombatPawnAnimator if missing so CombatAnimationDriver can find it
                if (_instantiatedVisual.GetComponentInChildren<CombatPawnAnimator>() == null)
                {
                    var animObj = anim != null ? anim.gameObject : _instantiatedVisual;
                    animObj.AddComponent<CombatPawnAnimator>();
                }
                
                Debug.Log($"[NetworkedCombatPawn] Successfully instantiated high-fidelity visual for {Role.Value}");
            }
            else
            {
                Debug.LogWarning($"[NetworkedCombatPawn] High-fidelity prefab missing for {Role.Value}. Falling back to primitive shapes.");
                CreatePrimitiveFallback();
            }
        }

        /// <summary>
        /// Calculates local horizontal offset to visually swap Client and Host on the Client's screen,
        /// ensuring each player always sees their character on the right slot (Asymmetric Mirror View).
        /// </summary>
        private Vector3 GetLocalMirrorOffset()
        {
            // Only apply visual mirror swaps on the Client's local screen (Host sees canonical slots)
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            {
                if (Role.Value == CombatRole.ClientPlayer)
                {
                    // Client player physically at X = -2f locally offsets to X = 2f (offset +4f)
                    return new Vector3(4f, 0f, 0f);
                }
                else if (Role.Value == CombatRole.HostPlayer)
                {
                    // Host player physically at X = 2f locally offsets to X = -2f (offset -4f)
                    return new Vector3(-4f, 0f, 0f);
                }
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Creates a clean primitive fallback (Capsule for players, large Cylinder for Boss) if prefabs are missing.
        /// </summary>
        private void CreatePrimitiveFallback()
        {
            GameObject fallbackObj;
            if (Role.Value == CombatRole.Boss)
            {
                fallbackObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fallbackObj.name = "Boss_Visual_Fallback";
                fallbackObj.transform.localScale = new Vector3(2.5f, 1.8f, 2.5f); // Massive boss size
                var renderer = fallbackObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    renderer.sharedMaterial.color = new Color(0.45f, 0.1f, 0.1f); // Menacing Crimson Red
                    renderer.sharedMaterial.EnableKeyword("_EMISSION");
                    renderer.sharedMaterial.SetColor("_EmissionColor", new Color(0.2f, 0.02f, 0.02f));
                }
            }
            else
            {
                fallbackObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fallbackObj.name = Role.Value == CombatRole.HostPlayer ? "Host_Player_Fallback" : "Client_Player_Fallback";
                fallbackObj.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
                var renderer = fallbackObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    renderer.sharedMaterial.color = Role.Value == CombatRole.HostPlayer 
                        ? new Color(0f, 0.5f, 0.85f) // Cyan Blue for Host
                        : new Color(0.85f, 0.5f, 0f); // Gold Amber for Client
                }
            }

            fallbackObj.transform.SetParent(transform, false);
            fallbackObj.transform.localPosition = GetLocalMirrorOffset();
            fallbackObj.transform.localRotation = Quaternion.identity;

            // Remove primitive collider to prevent physics overlap issues in turn-based arena
            var col = fallbackObj.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        /// <summary>
        /// Positions the Main Camera behind the local player's avatar in standard third-person combat orbit.
        /// </summary>
        private void ConfigureCameraForLocalOwner()
        {
            if (IsOwner && (Role.Value == CombatRole.HostPlayer || Role.Value == CombatRole.ClientPlayer))
            {
                var mainCam = Camera.main;
                if (mainCam != null)
                {
                    // DO NOT SET PARENT! Cinemachine / CombatCameraController will fight this parenting causing a StackOverflowException.
                    mainCam.transform.position = transform.position + new Vector3(0f, 2.2f, -4.5f); // Combat third person offset
                    mainCam.transform.rotation = Quaternion.Euler(16f, 0f, 0f);
                    Debug.Log($"[NetworkedCombatPawn] Snapped Main Camera behind local owner client {NetworkManager.Singleton.LocalClientId} without parenting.");
                }
            }
        }

        /// <summary>
        /// Instantiates the local player's premium CombatUI_Canvas containing their hand and action slots.
        /// </summary>
        private void InstantiateLocalUI()
        {
            if (combatCanvasPrefab == null)
            {
                // Dynamic robust editor asset loading fallback
                #if UNITY_EDITOR
                string canvasPrefabPath = "Assets/_Project/Systems/Combat/Prefabs/UI/CombatUI_Canvas.prefab";
                combatCanvasPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(canvasPrefabPath);
                #endif
            }

            if (combatCanvasPrefab != null)
            {
                GameObject canvasObj = Instantiate(combatCanvasPrefab);
                canvasObj.name = "CombatUI_Canvas_Local";

                // Resolve correct local character data based on role
                var netTM = FindObjectOfType<NetworkedTurnManager>();
                Runefall.Data.CharacterData localChar = null;
                if (netTM != null)
                {
                    localChar = (Role.Value == CombatRole.HostPlayer) ? netTM.PlayerCharacterData : netTM.ClientCharacterData;
                }

                // Wire up our custom multiplayer UI adapter
                var adapter = canvasObj.AddComponent<NetworkedCombatUIAdapter>();
                if (localChar != null)
                {
                    adapter.SetLocalCharacterData(localChar);
                }
                Debug.Log($"[NetworkedCombatPawn] Attached NetworkedCombatUIAdapter to local CombatUI_Canvas. Character: {(localChar != null ? localChar.characterName : "Default")}");
            }
            else
            {
                Debug.LogError("[NetworkedCombatPawn] CRITICAL: combatCanvasPrefab is null and fallback search failed!");
            }
        }
    }
}
