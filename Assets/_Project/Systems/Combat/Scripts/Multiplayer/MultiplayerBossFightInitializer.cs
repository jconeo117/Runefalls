using System.Collections;
using UnityEngine;
using Unity.Netcode;
using Runefall.Core;
using Runefall.Data;
using Runefall.Presentation.Combat;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Entry point for Multiplayer_BossFight scene.
    /// Waits for NetworkManager, assembles arena, spawns LOCAL visual pawns from registry
    /// (same prefabs on every client — no NetworkedCombatPawn needed for animation),
    /// then activates MultiplayerCombatBootstrapper which runs the full SP-compatible
    /// animation + turn pipeline with server authority via MultiplayerTurnManager.
    /// </summary>
    public class MultiplayerBossFightInitializer : MonoBehaviour
    {
        [SerializeField] private CombatArenaAssembler          arenaAssembler;
        [SerializeField] private MultiplayerCombatRegistry     registry;
        [SerializeField] private MultiplayerCombatBootstrapper bootstrapper;

        [Header("Arena")]
        [SerializeField] private int maxPlayers = 2;
        [SerializeField] private int enemySlots = 1;

        private void Awake()
        {
#if UNITY_EDITOR
            if (registry == null)
                registry = UnityEditor.AssetDatabase.LoadAssetAtPath<MultiplayerCombatRegistry>(
                    "Assets/_Project/Systems/Combat/ScriptableObjects/Combat/MultiplayerCombatRegistry.asset");
#endif
            if (registry != null)
            {
                ServiceLocator.Register<MultiplayerCombatRegistry>(registry);
                registry.Initialize();
                Debug.Log($"[BossFight] Registry: {registry.characters?.Count ?? 0} chars, {registry.enemies?.Count ?? 0} enemies.");
            }
            else
                Debug.LogError("[BossFight] MultiplayerCombatRegistry not assigned.");

            // Bootstrapper starts disabled — we activate it after arena + pawns are ready.
            if (bootstrapper != null)
                bootstrapper.gameObject.SetActive(false);
        }

        private IEnumerator Start()
        {
            yield return new WaitUntil(() =>
                NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);

            if (arenaAssembler == null)
            {
                Debug.LogError("[BossFight] ArenaAssembler not assigned.");
                yield break;
            }

            arenaAssembler.Assemble(maxPlayers, enemySlots);
            yield return null; // one frame for arena slots to settle

            SpawnLocalPawns();
            PositionCamera();

            if (bootstrapper != null)
                bootstrapper.gameObject.SetActive(true); // triggers OnEnable → full combat init + BeginCombat
            else
                Debug.LogError("[BossFight] MultiplayerCombatBootstrapper not assigned.");
        }

        // Spawn LOCAL (non-networked) visual prefabs into arena slots.
        // Every client does this independently — same registry, same deterministic slot positions.
        // CombatAnimationDriver can move these freely without NGO interference.
        private void SpawnLocalPawns()
        {
            if (registry == null || arenaAssembler == null || !arenaAssembler.IsReady) return;

            var playerSlots = arenaAssembler.PlayerSlots;
            var enemySlots  = arenaAssembler.EnemySlots;

            // Spawn one player pawn per slot using registry characters in order.
            for (int i = 0; i < playerSlots.Count && i < registry.characters.Count; i++)
            {
                var cd = registry.characters[i];
                if (cd == null || cd.prefab == null) continue;

                var pawn = Instantiate(cd.prefab, playerSlots[i]);
                pawn.transform.localPosition = Vector3.zero;
                pawn.transform.localRotation = Quaternion.identity;

                if (cd.animatorController != null)
                {
                    var anim = pawn.GetComponentInChildren<Animator>();
                    if (anim != null) anim.runtimeAnimatorController = cd.animatorController;
                }

                var slot = pawn.AddComponent<CharacterSlot>();
                slot.data = cd;
            }

            // Spawn enemy pawns (enemySlots count, all of same type — registry.enemies[0]).
            if (registry.enemies.Count == 0) return;
            var eData = registry.enemies[0];
            if (eData == null || eData.prefab == null) return;

            for (int i = 0; i < enemySlots.Count; i++)
            {
                var pawn = Instantiate(eData.prefab, enemySlots[i]);
                pawn.transform.localPosition = Vector3.zero;
                pawn.transform.localRotation = Quaternion.identity;

                if (eData.animatorController != null)
                {
                    var anim = pawn.GetComponentInChildren<Animator>();
                    if (anim != null) anim.runtimeAnimatorController = eData.animatorController;
                }

                var slot = pawn.AddComponent<EnemySlot>();
                slot.data = eData;
            }

            Debug.Log($"[BossFight] Local pawns spawned: {playerSlots.Count} players, {enemySlots.Count} enemies.");
        }

        private void PositionCamera()
        {
            var cam = Camera.main;
            if (cam == null || arenaAssembler == null || !arenaAssembler.IsReady) return;

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
                Vector3 center  = arenaAssembler.FieldCenter;
                Vector3 pPos    = arenaAssembler.PlayerRoot.position;
                Vector3 backDir = (pPos - center).normalized;
                cam.transform.position = pPos + backDir * 3f + Vector3.up * 3.5f;
                cam.transform.LookAt(center + Vector3.up * 1f);
            }
        }
    }
}
