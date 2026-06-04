using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Runefall.Combat;
using Runefall.Core;
using Runefall.Data;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Multiplayer-specific implementation of TurnManager.
    /// Acts authoritatively on the server and mirrors the combat state on clients.
    /// Redirects UI requests to Server RPCs on clients and replicates outcomes via Client RPCs.
    /// </summary>
    public class MultiplayerTurnManager : TurnManager
    {
        private readonly MultiplayerTurnManagerBridge _bridge;
        private readonly MultiplayerCombatRegistry _registry;
        private readonly Queue<NetworkCombatActionResult> _pendingClientResults = new();

        public MultiplayerTurnManager(MultiplayerTurnManagerBridge bridge, IEnemyPhaseAnimator phaseAnimator = null)
            : base(phaseAnimator)
        {
            _bridge = bridge;
            _bridge.SetManager(this);

            if (!ServiceLocator.TryGet<MultiplayerCombatRegistry>(out _registry))
            {
                Debug.LogError("[MultiplayerTurnManager] MultiplayerCombatRegistry not registered in ServiceLocator.");
            }

            // Server-side: register callbacks to propagate outcomes to all clients
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                OnActionPending += ServerOnActionPending;
                OnActionResolved += ServerOnActionResolved;
                OnCombatEnded += ServerOnCombatEnded;
            }
        }

        private void ServerOnActionPending(PendingAction action)
        {
            var netAction = new NetworkPendingAction(action, Context);
            _bridge.ClientOnActionPendingClientRpc(netAction);
        }

        private void ServerOnActionResolved(CombatActionResult result)
        {
            var netResult = new NetworkCombatActionResult(result, Context);
            _bridge.ClientOnActionResolvedClientRpc(netResult);
        }

        private void ServerOnCombatEnded(bool playerWon)
        {
            _bridge.ClientOnCombatEndedClientRpc(playerWon);
        }

        public void ServerSyncHand()
        {
            if (Hand == null) return;

            var slots = Hand.Slots;
            var netCards = new NetworkBattleCard[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                var card = slots[i];
                netCards[i] = new NetworkBattleCard
                {
                    CardId = card.Id,
                    SkillName = card.Skill != null ? (Unity.Collections.FixedString32Bytes)card.Skill.skillName : default,
                    UltimateName = card.Ultimate != null ? (Unity.Collections.FixedString32Bytes)card.Ultimate.ultimateName : default,
                    Rank = card.Rank,
                    IsUltimate = card.IsUltimate
                };
            }
            _bridge.ClientSyncHandClientRpc(netCards, Hand.ActionsRemaining);
        }

        public override void StartCombat(CombatContext context, IReadOnlyList<CharacterData> fieldChars, bool hasBench, System.Random rng = null)
        {
            base.StartCombat(context, fieldChars, hasBench, rng);

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                _bridge.NetPhase.Value = Phase;
                _bridge.NetRound.Value = Round;
                ServerSyncHand();
            }
        }

        public override bool SubmitSkill(int cardIndex, ICombatActor explicitTarget = null)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                bool success = base.SubmitSkill(cardIndex, explicitTarget);
                if (success)
                {
                    ServerSyncHand();
                }
                return success;
            }
            else
            {
                // Client forwards action request to Server
                var targetRef = new NetworkActorRef(explicitTarget, Context);
                _bridge.SubmitSkillServerRpc(cardIndex, targetRef);
                return true;
            }
        }

        public override bool SubmitMove(int fromIndex, int toIndex)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                bool success = base.SubmitMove(fromIndex, toIndex);
                if (success)
                {
                    ServerSyncHand();
                }
                return success;
            }
            else
            {
                // Client forwards move request to Server
                _bridge.SubmitMoveServerRpc(fromIndex, toIndex);
                return true;
            }
        }

        public override void EndPlayerTurn()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                base.EndPlayerTurn();
                _bridge.NetPhase.Value = Phase;
                ServerSyncHand();
            }
            else
            {
                // Client requests end of turn
                _bridge.EndPlayerTurnServerRpc();
            }
        }

        public override CombatActionResult[] ResolveAction(PendingAction pending, float hitFraction = 1f)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                return base.ResolveAction(pending, hitFraction);
            }
            else
            {
                // Client resolves action by pulling autoritative results from server queue
                int count = 1;
                if (pending.TargetType == TargetType.AllEnemies) count = Context.Enemies.Count;
                else if (pending.TargetType == TargetType.AllAllies) count = Context.Players.Count;

                var results = new List<CombatActionResult>();
                for (int i = 0; i < count; i++)
                {
                    if (_pendingClientResults.Count > 0)
                    {
                        results.Add(ResolveAndApplyClient(_pendingClientResults.Dequeue()));
                    }
                }
                return results.ToArray();
            }
        }

        public override CombatActionResult[] ResolveForTarget(PendingAction pending, float hitFraction, ICombatActor specificTarget)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                return base.ResolveForTarget(pending, hitFraction, specificTarget);
            }
            else
            {
                if (_pendingClientResults.Count > 0)
                {
                    return new[] { ResolveAndApplyClient(_pendingClientResults.Dequeue()) };
                }
                return System.Array.Empty<CombatActionResult>();
            }
        }

        private CombatActionResult ResolveAndApplyClient(NetworkCombatActionResult netResult)
        {
            var result = netResult.Resolve(Context, _registry);
            if (result.Target != null)
            {
                result.Target.Model.SetHPDirectly(netResult.TargetPostHP);
                result.Target.Model.SetShieldDirectly(netResult.TargetPostShield);
            }

            InvokeOnActionResolved(result);

            if (result.Target != null && !result.Target.IsAlive)
            {
                LocalPurgeDeadPlayer(result.Target);
            }

            return result;
        }

        private void LocalPurgeDeadPlayer(ICombatActor actor)
        {
            if (!_purgedPlayers.Add(actor)) return;
            if (!_actorChars.TryGetValue(actor, out var cd)) return;

            var toRemove = new List<SkillData>();
            foreach (var kvp in _skillOwners)
                if (kvp.Value == actor) toRemove.Add(kvp.Key);
            foreach (var s in toRemove)
                _skillOwners.Remove(s);

            Hand.OnCharacterLeft(cd);
        }

        // --- Client state synchronization setters ---

        public void SetPhaseAndRoundDirectly(CombatPhase phase, int round)
        {
            Phase = phase;
            Round = round;

            if (phase == CombatPhase.PlayerTurn)
            {
                InvokeOnPlayerTurnBegin(round);
                void Fire() => InvokeOnPlayerTurnStarted(round);
                if (PlayerTurnStartHandler != null)
                    PlayerTurnStartHandler(Fire);
                else
                    Fire();
            }
            else if (phase == CombatPhase.EnemyTurn)
            {
                InvokeOnEnemyTurnStarted();
            }
        }

        // --- Client replication callbacks invoked by the bridge ---

        public void HandleClientOnActionPending(NetworkPendingAction netAction)
        {
            var pending = netAction.Resolve(Context, _registry);
            InvokeOnActionPending(pending);
        }

        public void HandleClientOnActionResolved(NetworkCombatActionResult netResult)
        {
            _pendingClientResults.Enqueue(netResult);
        }

        public void HandleClientOnCombatEnded(bool playerWon)
        {
            InvokeOnCombatEnded(playerWon);
        }

        public void HandleClientSyncHand(NetworkBattleCard[] netCards, int actionsRemaining)
        {
            if (Hand == null) return;

            var cardsList = new List<BattleCard>();
            foreach (var netCard in netCards)
            {
                cardsList.Add(netCard.Resolve(_registry));
            }

            Hand.SetSlots(cardsList);
            Hand.SetActionsRemaining(actionsRemaining);
        }
    }

}
