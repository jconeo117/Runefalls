using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using Runefall.Data;
using Runefall.Core;
using Runefall.Presentation.Combat;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Network-replicated pawn wrapper that spawns the singleplayer visual representation in combat.
    /// Avoids modifying original character/enemy prefabs by acting as a container.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedCombatPawn : NetworkBehaviour
    {
        public NetworkVariable<FixedString32Bytes> CharacterOrEnemyDataName = new(
            writePerm: NetworkVariableWritePermission.Server);

        public NetworkVariable<int> SlotIndex = new(
            writePerm: NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsPlayerTeam = new(
            writePerm: NetworkVariableWritePermission.Server);

        private bool _isInitialized = false;
        private bool _parented = false;

        public override void OnNetworkSpawn()
        {
            InitializePawn();
            CharacterOrEnemyDataName.OnValueChanged += OnNameChanged;
        }

        public override void OnNetworkDespawn()
        {
            CharacterOrEnemyDataName.OnValueChanged -= OnNameChanged;
        }

        private void OnNameChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal)
        {
            InitializePawn();
        }

        private void Update()
        {
            if (_isInitialized && !_parented)
            {
                TryParentToSlot();
            }
        }

        private void InitializePawn()
        {
            if (_isInitialized) return;

            string dataName = CharacterOrEnemyDataName.Value.ToString();
            if (string.IsNullOrEmpty(dataName)) return; // Wait until name is replicated

            _isInitialized = true;

            if (!ServiceLocator.TryGet<MultiplayerCombatRegistry>(out var registry))
            {
                Debug.LogError("[NetworkedCombatPawn] MultiplayerCombatRegistry not registered in ServiceLocator.");
                return;
            }

            GameObject visualPrefab = null;
            RuntimeAnimatorController animatorController = null;

            if (IsPlayerTeam.Value)
            {
                CharacterData charData = registry.GetCharacter(dataName);
                if (charData != null)
                {
                    visualPrefab = charData.prefab;
                    animatorController = charData.animatorController;

                    var charSlot = gameObject.AddComponent<CharacterSlot>();
                    charSlot.data = charData;
                }
                else
                {
                    Debug.LogError($"[NetworkedCombatPawn] CharacterData '{dataName}' not found in registry.");
                }
            }
            else
            {
                EnemyData enemyData = registry.GetEnemy(dataName);
                if (enemyData != null)
                {
                    visualPrefab = enemyData.prefab;
                    animatorController = enemyData.animatorController;

                    var enemySlot = gameObject.AddComponent<EnemySlot>();
                    enemySlot.data = enemyData;
                }
                else
                {
                    Debug.LogError($"[NetworkedCombatPawn] EnemyData '{dataName}' not found in registry.");
                }
            }

            if (visualPrefab != null)
            {
                var visual = Instantiate(visualPrefab, transform);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                if (animatorController != null)
                {
                    var anim = visual.GetComponentInChildren<Animator>();
                    if (anim != null)
                    {
                        anim.runtimeAnimatorController = animatorController;
                    }
                }
            }

            TryParentToSlot();
        }

        private void TryParentToSlot()
        {
            // Server already spawns the pawn at the slot's world position.
            // NGO's SynchronizeTransform replicates that position to all clients.
            // Do NOT reparent here — AutoObjectParentSync would override it back.
            // Just confirm the arena is ready and mark as parented.
            var assembler = FindFirstObjectByType<CombatArenaAssembler>();
            if (assembler != null && assembler.IsReady)
                _parented = true;
        }
    }
}
