using System;
using UnityEngine;
using Unity.Netcode;
using Runefall.Combat;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// MonoBehaviour NetworkBehaviour bridge that communicates network commands/updates
    /// between Client and Server for MultiplayerTurnManager.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class MultiplayerTurnManagerBridge : NetworkBehaviour
    {
        public NetworkVariable<CombatPhase> NetPhase = new(
            writePerm: NetworkVariableWritePermission.Server);

        public NetworkVariable<int> NetRound = new(
            writePerm: NetworkVariableWritePermission.Server);

        private MultiplayerTurnManager _manager;

        public void SetManager(MultiplayerTurnManager manager)
        {
            _manager = manager;
        }

        public override void OnNetworkSpawn()
        {
            NetPhase.OnValueChanged += OnPhaseChanged;
            NetRound.OnValueChanged += OnRoundChanged;

            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
                _manager?.SetPhaseAndRoundDirectly(NetPhase.Value, NetRound.Value);
        }

        public override void OnNetworkDespawn()
        {
            NetPhase.OnValueChanged -= OnPhaseChanged;
            NetRound.OnValueChanged -= OnRoundChanged;
        }

        private void OnPhaseChanged(CombatPhase oldPhase, CombatPhase newPhase)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) return;
            _manager?.SetPhaseAndRoundDirectly(newPhase, NetRound.Value);
        }

        private void OnRoundChanged(int oldRound, int newRound)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) return;
            _manager?.SetPhaseAndRoundDirectly(NetPhase.Value, newRound);
        }

        // --- Server RPCs ---

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SubmitSkillServerRpc(int cardIndex, NetworkActorRef targetRef)
        {
            var targetActor = targetRef.Resolve(_manager.Context);
            _manager.SubmitSkill(cardIndex, targetActor);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SubmitMoveServerRpc(int fromIndex, int toIndex)
        {
            _manager.SubmitMove(fromIndex, toIndex);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void EndPlayerTurnServerRpc()
        {
            _manager.EndPlayerTurn();
        }

        // --- Client RPCs (SendTo.NotServer: host already processed locally) ---

        [Rpc(SendTo.NotServer)]
        public void ClientOnActionPendingClientRpc(NetworkPendingAction netAction)
        {
            _manager?.HandleClientOnActionPending(netAction);
        }

        [Rpc(SendTo.NotServer)]
        public void ClientOnActionResolvedClientRpc(NetworkCombatActionResult netResult)
        {
            _manager?.HandleClientOnActionResolved(netResult);
        }

        [Rpc(SendTo.NotServer)]
        public void ClientOnCombatEndedClientRpc(bool playerWon)
        {
            _manager?.HandleClientOnCombatEnded(playerWon);
        }

        [Rpc(SendTo.NotServer)]
        public void ClientSyncHandClientRpc(NetworkBattleCard[] netCards, int actionsRemaining)
        {
            _manager?.HandleClientSyncHand(netCards, actionsRemaining);
        }

        // ── Animation sync ─────────────────────────────────────────────────────

        /// <summary>
        /// Fired on non-server clients when server is about to drain its animation queue.
        /// Clients subscribe to call PlayQueuedAnimations(null) on their local CombatAnimationDriver.
        /// </summary>
        public event Action OnClientShouldPlayAnimations;

        /// <summary>Server → non-server clients: drain local animation queues now.</summary>
        [Rpc(SendTo.NotServer)]
        public void ClientPlayAnimationsClientRpc()
        {
            OnClientShouldPlayAnimations?.Invoke();
        }
    }
}
