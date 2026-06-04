using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using Runefall.Core;
using Runefall.Data;
using Runefall.Presentation.Dungeon;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Builds the physical combat arena layout at runtime.
    /// Instantiates the ArenaData.layoutPrefab at the arena center, reads child Transforms
    /// for camera anchors, then creates slot Transforms for each side.
    /// Knows nothing about character models or game data — receives only slot counts.
    /// </summary>
    public class CombatArenaAssembler : MonoBehaviour
    {
        [SerializeField] private ArenaData _data;

        public Transform                PlayerRoot           { get; private set; }
        public Transform                EnemyRoot            { get; private set; }
        public IReadOnlyList<Transform> PlayerSlots          { get; private set; }
        public IReadOnlyList<Transform> EnemySlots           { get; private set; }
        public Vector3                  FieldCenter          { get; private set; }
        public bool                     IsReady              { get; private set; }

        // Camera anchors populated from layoutPrefab children after Assemble.
        public Transform CameraGameplayAnchor { get; private set; }
        public Transform IntroEnemyAnchor     { get; private set; }
        public Transform IntroPlayerAnchor    { get; private set; }

        // Set before enabling CombatBootstrapper so SpawnFromEncounterState uses room bounds.
        public RoomVolume PendingRoom { get; set; }

        private GameObject _environmentInstance;
        private GameObject _layoutInstance;

        /// <summary>
        /// Instantiates layoutPrefab at worldOffset and uses its PlayerLine/EnemyLine children
        /// as team line positions. Camera anchors are read from remaining children.
        /// </summary>
        public void Assemble(int playerCount, int enemyCount, Vector3 worldOffset = default)
        {
            if (_data == null) { Debug.LogError("[CombatArenaAssembler] ArenaData not assigned.", this); return; }

            var (playerPos, enemyPos) = SpawnLayout(worldOffset, Quaternion.identity);

            AssembleAtPositions(
                Mathf.Clamp(playerCount, 1, _data.maxPlayerSlots),
                Mathf.Clamp(enemyCount,  1, _data.maxEnemySlots),
                playerPos, enemyPos);
        }

        /// <summary>
        /// Instantiates layoutPrefab at the room's arena center (oriented along longest axis).
        /// Team lines are overridden by room-computed positions; camera anchors stay relative to center.
        /// </summary>
        public void AssembleInRoom(RoomVolume room, int playerCount, int enemyCount)
        {
            if (_data == null) { Debug.LogError("[CombatArenaAssembler] ArenaData not assigned.", this); return; }
            if (room == null)  { Assemble(playerCount, enemyCount); return; }

            float      lineHalfSpan   = GetLineHalfSpan();
            var        (pCenter, eCenter, forward) = room.GetArenaLayout(lineHalfSpan);
            Vector3    arenaCenter    = (pCenter + eCenter) * 0.5f;
            Quaternion layoutRotation = forward.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(forward) : Quaternion.identity;

            // Spawn layout for camera anchors; team lines come from room, not the prefab.
            SpawnLayout(arenaCenter, layoutRotation);

            AssembleAtPositions(
                Mathf.Clamp(playerCount, 1, _data.maxPlayerSlots),
                Mathf.Clamp(enemyCount,  1, _data.maxEnemySlots),
                pCenter, eCenter);
        }

        /// <summary>
        /// Reads EncounterState, calls Assemble/AssembleInRoom, then instantiates prefabs from
        /// CharacterData/EnemyData. Spawned pawns get CharacterSlot/EnemySlot added so
        /// BuildFromSlots in CombatBootstrapper can find them.
        /// </summary>
        public virtual void SpawnFromEncounterState(EncounterState state, Vector3 worldOffset = default)
        {
            if (state == null)
            {
                Debug.LogError("[CombatArenaAssembler] EncounterState is null.", this);
                return;
            }

            // MVP: always 1 player slot, 1-3 enemies of the same type
            var party       = state.ResolvedParty;
            int playerCount = 1;
            int enemyCount  = UnityEngine.Random.Range(1, 4);

            if (PendingRoom != null)
            {
                AssembleInRoom(PendingRoom, playerCount, enemyCount);
                PendingRoom = null;
            }
            else
                Assemble(playerCount, enemyCount, worldOffset);

            // Spawn player directly into its arena slot (child of ArenaPlayerRoot).
            var solo = party.Length > 0 ? party[0] : null;
            if (solo != null && solo.prefab != null && PlayerSlots.Count > 0)
            {
                var pawn = Instantiate(solo.prefab, PlayerSlots[0]);
                pawn.transform.localPosition = Vector3.zero;
                pawn.transform.localRotation = Quaternion.identity;
                if (solo.animatorController != null)
                {
                    var anim = pawn.GetComponentInChildren<Animator>();
                    if (anim != null) anim.runtimeAnimatorController = solo.animatorController;
                }
                pawn.AddComponent<CharacterSlot>().data = solo;
            }
            else if (solo == null)
                Debug.LogWarning("[CombatArenaAssembler] No player character in EncounterState.", this);
            else
                Debug.LogWarning($"[CombatArenaAssembler] '{solo?.characterName}' has no prefab or no player slots.", this);

            var eData = state.Encounter?.enemyData;
            if (eData == null)
            {
                Debug.LogWarning("[CombatArenaAssembler] EncounterState has no enemy data.", this);
                return;
            }
            if (eData.prefab == null)
            {
                Debug.LogWarning($"[CombatArenaAssembler] Enemy '{eData.enemyName}' has no prefab.", this);
                return;
            }

            // Spawn each enemy directly into its arena slot (child of ArenaEnemyRoot).
            for (int i = 0; i < enemyCount && i < EnemySlots.Count; i++)
            {
                var ePawn = Instantiate(eData.prefab, EnemySlots[i]);
                ePawn.transform.localPosition = Vector3.zero;
                ePawn.transform.localRotation = Quaternion.identity;
                if (eData.animatorController != null)
                {
                    var anim = ePawn.GetComponentInChildren<Animator>();
                    if (anim != null) anim.runtimeAnimatorController = eData.animatorController;
                }
                ePawn.AddComponent<EnemySlot>().data = eData;
            }
        }

        public void Teardown()
        {
            if (_layoutInstance != null)
            {
                Destroy(_layoutInstance);
                _layoutInstance = null;
            }
            else
            {
                if (PlayerRoot != null) Destroy(PlayerRoot.gameObject);
                if (EnemyRoot  != null) Destroy(EnemyRoot.gameObject);
            }

            if (_environmentInstance != null) Destroy(_environmentInstance);

            PlayerRoot           = null;
            EnemyRoot            = null;
            PlayerSlots          = null;
            EnemySlots           = null;
            CameraGameplayAnchor = null;
            IntroEnemyAnchor     = null;
            IntroPlayerAnchor    = null;
            IsReady              = false;
        }

        public PlayableDirector GetLayoutDirector()
        {
            return _layoutInstance != null ? _layoutInstance.GetComponentInChildren<PlayableDirector>() : null;
        }

        // ── private ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Instantiates layoutPrefab at worldPos with rotation. Reads camera anchor children.
        /// Returns the world positions of PlayerLine and EnemyLine (or fallback offsets).
        /// </summary>
        private (Vector3 playerPos, Vector3 enemyPos) SpawnLayout(Vector3 worldPos, Quaternion rotation)
        {
            if (_layoutInstance != null) Destroy(_layoutInstance);

            CameraGameplayAnchor = null;
            IntroEnemyAnchor     = null;
            IntroPlayerAnchor    = null;

            if (_data.layoutPrefab != null)
            {
                _layoutInstance = Instantiate(_data.layoutPrefab, worldPos, rotation, transform);
                var t = _layoutInstance.transform;

                CameraGameplayAnchor = t.Find("CameraGameplay");
                IntroEnemyAnchor     = t.Find("CamIntro_Enemy");
                IntroPlayerAnchor    = t.Find("CamIntro_Player");

                var playerLine = t.Find("PlayerLine");
                var enemyLine  = t.Find("EnemyLine");

                return (
                    playerLine != null ? playerLine.position : worldPos + rotation * (Vector3.back    * 3.5f),
                    enemyLine  != null ? enemyLine.position  : worldPos + rotation * (Vector3.forward * 3.5f)
                );
            }

            // No layout prefab — axis-aligned fallback
            return (
                worldPos + rotation * (Vector3.back    * 3.5f),
                worldPos + rotation * (Vector3.forward * 3.5f)
            );
        }

        private float GetLineHalfSpan()
        {
            if (_data?.layoutPrefab == null) return 3.5f;
            var pLine = _data.layoutPrefab.transform.Find("PlayerLine");
            var eLine = _data.layoutPrefab.transform.Find("EnemyLine");
            return (pLine != null && eLine != null)
                ? Vector3.Distance(pLine.localPosition, eLine.localPosition) * 0.5f
                : 3.5f;
        }

        private void AssembleAtPositions(int pc, int ec, Vector3 playerWorldPos, Vector3 enemyWorldPos)
        {
            Vector3 playerFacing = (enemyWorldPos - playerWorldPos).normalized;
            Vector3 enemyFacing  = -playerFacing;
            FieldCenter = (playerWorldPos + enemyWorldPos) * 0.5f;

            Transform playerLine = _layoutInstance != null ? _layoutInstance.transform.Find("PlayerLine") : null;
            Transform enemyLine  = _layoutInstance != null ? _layoutInstance.transform.Find("EnemyLine") : null;

            if (playerLine != null)
            {
                PlayerRoot = playerLine;
                PlayerRoot.position = playerWorldPos;
                if (playerFacing.sqrMagnitude > 0.001f)
                    PlayerRoot.rotation = Quaternion.LookRotation(playerFacing);
            }
            else
            {
                PlayerRoot = CreateRoot("ArenaPlayerRoot", playerWorldPos, playerFacing);
            }

            if (enemyLine != null)
            {
                EnemyRoot = enemyLine;
                EnemyRoot.position = enemyWorldPos;
                if (enemyFacing.sqrMagnitude > 0.001f)
                    EnemyRoot.rotation = Quaternion.LookRotation(enemyFacing);
            }
            else
            {
                EnemyRoot = CreateRoot("ArenaEnemyRoot", enemyWorldPos, enemyFacing);
            }

            PlayerSlots = BuildSlots(PlayerRoot, pc);
            EnemySlots  = BuildSlots(EnemyRoot,  ec);

            if (_data.environmentPrefab != null)
                _environmentInstance = Instantiate(
                    _data.environmentPrefab, FieldCenter, Quaternion.identity, transform);

            IsReady = true;
        }

        private Transform CreateRoot(string rootName, Vector3 worldPos, Vector3 facing)
        {
            var go = new GameObject(rootName);
            go.transform.SetParent(transform, false);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.LookRotation(facing);
            return go.transform;
        }

        private IReadOnlyList<Transform> BuildSlots(Transform parent, int count)
        {
            var   slots  = new Transform[count];
            float offset = (count - 1) * _data.slotSpacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var slot = new GameObject($"Slot_{i}");
                slot.transform.SetParent(parent, false);
                slot.transform.localPosition = new Vector3(i * _data.slotSpacing - offset, 0f, 0f);
                slot.transform.localRotation = Quaternion.identity;
                slots[i] = slot.transform;
            }

            return System.Array.AsReadOnly(slots);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_data == null) return;

            if (IsReady)
            {
                if (PlayerRoot != null) DrawSideGizmo(PlayerRoot.position, Color.cyan, _data.maxPlayerSlots);
                if (EnemyRoot  != null) DrawSideGizmo(EnemyRoot.position,  Color.red,  _data.maxEnemySlots);
                if (PlayerRoot != null && EnemyRoot != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(FieldCenter, 0.3f);
                }
            }
            else if (_data.layoutPrefab != null)
            {
                var pLine = _data.layoutPrefab.transform.Find("PlayerLine");
                var eLine = _data.layoutPrefab.transform.Find("EnemyLine");
                if (pLine != null) DrawSideGizmo(transform.position + pLine.localPosition, Color.cyan, _data.maxPlayerSlots);
                if (eLine != null) DrawSideGizmo(transform.position + eLine.localPosition, Color.red,  _data.maxEnemySlots);
                if (pLine != null && eLine != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(transform.position + (pLine.localPosition + eLine.localPosition) * 0.5f, 0.3f);
                }
            }
        }

        private void DrawSideGizmo(Vector3 center, Color color, int maxSlots)
        {
            Gizmos.color = color;
            float offset = (maxSlots - 1) * _data.slotSpacing * 0.5f;
            for (int i = 0; i < maxSlots; i++)
            {
                Vector3 pos = center + new Vector3(0f, 0f, i * _data.slotSpacing - offset);
                Gizmos.DrawWireSphere(pos, 0.4f);
            }
        }
#endif
    }
}
