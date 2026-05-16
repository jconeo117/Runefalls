using System;
using System.Collections.Generic;
using Runefall.Data;

namespace Runefall.Combat
{
    public enum CombatPhase { Idle, PlayerTurn, EnemyTurn, Over }

    /// <summary>
    /// Drives the combat round loop: Player phase → Enemy phase → End of round → repeat.
    ///
    /// Round structure:
    ///   1. BeginPlayerTurn  — resets actions, fires OnPlayerTurnStarted
    ///   2. SubmitSkill/SubmitMove — called by Presenter per player action
    ///   3. EndPlayerTurn    — auto when ActionsRemaining == 0, or called manually by Presenter
    ///   4. Enemy phase      — IEnemyPhaseAnimator.RunEnemyPhase (animated) or ProcessEnemyPhase (sync)
    ///   5. EndOfRound       — tick effects, apply regen, refill hand, loop
    ///
    /// Presentation communicates through:
    ///   - IEnemyPhaseAnimator (constructor injection) — animates enemy turns
    ///   - C# events on this class — Presentation subscribes, or SkillEventBridge forwards to SOs
    /// </summary>
    public class TurnManager
    {
        public CombatContext Context { get; private set; }
        public CombatHand    Hand    { get; private set; }
        public CombatPhase   Phase   { get; private set; } = CombatPhase.Idle;
        public int           Round   { get; private set; }

        public event Action<int>                OnPlayerTurnStarted;  // round number
        public event Action                     OnEnemyTurnStarted;
        public event Action<CombatActionResult> OnActionResolved;
        public event Action<bool>               OnCombatEnded;        // playerWon
        public event Action<string, int>        OnMergeOccurred;      // skillName, newRank
        /// <summary>Fires whenever a player's ultimate gauge changes. (actor, currentOrbs) — max = UltimateGaugeMax (7).</summary>
        public event Action<ICombatActor, int> OnGaugeChanged;

        /// <summary>
        /// Fires when the player has used all action slots but before EndPlayerTurn runs.
        /// Subscriber is responsible for calling EndPlayerTurn() after animations complete.
        /// If no subscribers, EndPlayerTurn() runs immediately.
        /// </summary>
        public event Action OnPlayerActionsExhausted;

        private readonly IEnemyPhaseAnimator                         _phaseAnimator;
        private readonly Dictionary<SkillData, ICombatActor>          _skillOwners   = new();
        private readonly Dictionary<ICombatActor, CharacterData>      _actorChars    = new();
        private readonly HashSet<ICombatActor>                        _purgedPlayers = new();
        private ICombatActor _ultimateOwner;
        private Random       _rng;

        private readonly Dictionary<ICombatActor, int> _ultimateGauge = new();
        private const int UltimateGaugeMax = 7;

        /// <param name="phaseAnimator">
        /// Presentation MonoBehaviour that animates enemy turns.
        /// Pass null to resolve enemy turns synchronously (useful in tests).
        /// </param>
        public TurnManager(IEnemyPhaseAnimator phaseAnimator = null)
        {
            _phaseAnimator = phaseAnimator;
        }

        public void StartCombat(
            CombatContext context,
            IReadOnlyList<CharacterData> fieldChars,
            bool hasBench,
            Random rng = null)
        {
            if (context    == null) throw new ArgumentNullException(nameof(context));
            if (fieldChars == null) throw new ArgumentNullException(nameof(fieldChars));

            Context = context;
            Round   = 0;
            Phase   = CombatPhase.Idle;
            _rng    = rng ?? new Random();

            _skillOwners.Clear();
            _actorChars.Clear();
            _purgedPlayers.Clear();
            _ultimateGauge.Clear();
            _ultimateOwner = null;

            for (int i = 0; i < fieldChars.Count && i < context.Players.Count; i++)
            {
                var actor = context.Players[i];
                var cd    = fieldChars[i];
                if (cd.skill1   != null) _skillOwners[cd.skill1] = actor;
                if (cd.skill2   != null) _skillOwners[cd.skill2] = actor;
                if (cd.ultimate != null && _ultimateOwner == null) _ultimateOwner = actor;
                _actorChars[actor] = cd;
            }

            for (int i = 0; i < context.Players.Count; i++)
                _ultimateGauge[context.Players[i]] = 0;

            var pool = new CardPool(fieldChars, _rng);
            Hand = new CombatHand(pool, fieldChars.Count, hasBench);
            Hand.OnMerge += (skill, rank) =>
            {
                OnMergeOccurred?.Invoke(skill?.skillName ?? "?", rank);
                if (skill != null && _skillOwners.TryGetValue(skill, out var mergeOwner))
                    FillGauge(mergeOwner);
            };
            Hand.Deal(fieldChars);

            BeginPlayerTurn();
        }

        /// <summary>
        /// Player plays the card at cardIndex.
        /// explicitTarget: pass the player-selected enemy for SingleEnemy skills.
        ///   Pass null to let TurnManager resolve automatically (random for SingleEnemy,
        ///   all-enemies for AllEnemies, caster for Self, etc.).
        /// Fires OnActionResolved once per target hit (multiple times for AoE).
        /// Returns false if phase is wrong, card index invalid, or no valid target exists.
        /// </summary>
        public bool SubmitSkill(int cardIndex, ICombatActor explicitTarget = null)
        {
            if (Phase != CombatPhase.PlayerTurn) return false;
            if (!Hand.TryUse(cardIndex, out var slot, out _)) return false;

            var caster = ResolveCaster(slot);
            FillGauge(caster);                   // +1 gauge for using a card

            var targetType = slot.IsUltimate
                ? (slot.Ultimate?.targetType ?? TargetType.SingleEnemy)
                : (slot.Skill?.targetType    ?? TargetType.SingleEnemy);

            switch (targetType)
            {
                case TargetType.AllEnemies:
                {
                    var results = slot.IsUltimate
                        ? CombatResolver.ExecuteUltimateAll(slot.Ultimate, caster, Context.Enemies)
                        : CombatResolver.ExecuteAll(slot.Skill, slot.Rank, caster, Context.Enemies);
                    for (int i = 0; i < results.Length; i++)
                        Resolve(results[i]);
                    break;
                }

                case TargetType.AllAllies:
                {
                    for (int i = 0; i < Context.Players.Count; i++)
                    {
                        if (!Context.Players[i].IsAlive) continue;
                        var r = slot.IsUltimate
                            ? CombatResolver.ExecuteUltimate(slot.Ultimate, caster, Context.Players[i])
                            : CombatResolver.Execute(slot.Skill, slot.Rank, caster, Context.Players[i]);
                        Resolve(r);
                    }
                    break;
                }

                case TargetType.Self:
                {
                    var r = slot.IsUltimate
                        ? CombatResolver.ExecuteUltimate(slot.Ultimate, caster, caster)
                        : CombatResolver.Execute(slot.Skill, slot.Rank, caster, caster);
                    Resolve(r);
                    break;
                }

                default: // SingleEnemy and RandomEnemy
                {
                    ICombatActor target = (targetType == TargetType.SingleEnemy)
                        ? (explicitTarget != null && explicitTarget.IsAlive ? explicitTarget : RandomAliveEnemy())
                        : RandomAliveEnemy();

                    if (target == null) return false;

                    var r = slot.IsUltimate
                        ? CombatResolver.ExecuteUltimate(slot.Ultimate, caster, target)
                        : CombatResolver.Execute(slot.Skill, slot.Rank, caster, target);
                    OnActionResolved?.Invoke(r);
                    break;
                }
            }

            if (Context.IsOver)             { FinishCombat(); return true; }
            if (Hand.ActionsRemaining == 0) NotifyActionsExhausted();

            return true;
        }

        /// <summary>
        /// Player moves card at fromIndex to toIndex. Costs 1 action.
        /// Returns false if phase is wrong or move is invalid.
        /// </summary>
        public bool SubmitMove(int fromIndex, int toIndex)
        {
            if (Phase != CombatPhase.PlayerTurn) return false;
            var movedCard = fromIndex >= 0 && fromIndex < Hand.Slots.Count
                ? Hand.Slots[fromIndex] : default;
            bool ok = Hand.TryMove(fromIndex, toIndex, out _);
            if (ok)
            {
                if (!movedCard.IsUltimate && movedCard.Skill != null
                    && _skillOwners.TryGetValue(movedCard.Skill, out var mover))
                    FillGauge(mover);            // +1 gauge for moving a card
                if (Hand.ActionsRemaining == 0) NotifyActionsExhausted();
            }
            return ok;
        }

        /// <summary>Player ends their turn before exhausting all actions.</summary>
        public void EndPlayerTurn()
        {
            if (Phase != CombatPhase.PlayerTurn) return;
            Phase = CombatPhase.EnemyTurn;
            OnEnemyTurnStarted?.Invoke();

            if (_phaseAnimator != null)
                _phaseAnimator.RunEnemyPhase(BuildEnemyTurnOrder(), ExecuteSingleEnemyTurn, EndOfRound);
            else
                ProcessEnemyPhase();
        }

        // ── private ───────────────────────────────────────────────────────────────

        private void NotifyActionsExhausted()
        {
            if (OnPlayerActionsExhausted != null)
                OnPlayerActionsExhausted.Invoke();
            else
                EndPlayerTurn();
        }

        private void BeginPlayerTurn()
        {
            if (Context.IsOver) { FinishCombat(); return; }

            Round++;
            Phase              = CombatPhase.PlayerTurn;
            Context.TurnNumber = Round;
            Hand.ResetActions();
            CheckUltimateInsertion();
            OnPlayerTurnStarted?.Invoke(Round);
        }

        private void ProcessEnemyPhase()
        {
            var sorted = BuildEnemyTurnOrder();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (Context.IsOver) break;
                ExecuteSingleEnemyTurn(sorted[i]);
            }
            EndOfRound();
        }

        private void ExecuteSingleEnemyTurn(ICombatActor enemy)
        {
            if (!enemy.IsAlive) return;
            var player = GetAlivePlayer();
            if (player == null) return;
            if (enemy is IEnemyTurnHandler handler)
                Resolve(handler.TakeTurn(Context, player));
        }

        private void EndOfRound()
        {
            for (int i = 0; i < Context.AllActors.Count; i++)
            {
                var actor = Context.AllActors[i];
                if (actor.IsAlive) actor.Effects.Tick();
            }

            for (int i = 0; i < Context.AllActors.Count; i++)
            {
                var actor = Context.AllActors[i];
                if (actor.IsAlive) actor.Model.ApplyRegen();
            }

            Hand.Refill();

            if (Context.IsOver) { FinishCombat(); return; }

            BeginPlayerTurn();
        }

        private void FinishCombat()
        {
            Phase = CombatPhase.Over;
            OnCombatEnded?.Invoke(Context.PlayerWon);
        }

        // ── ultimate gauge ────────────────────────────────────────────────────────

        private void FillGauge(ICombatActor actor, int amount = 1)
        {
            if (actor == null || !actor.IsAlive) return;
            if (!_ultimateGauge.ContainsKey(actor)) _ultimateGauge[actor] = 0;
            _ultimateGauge[actor] = Math.Min(_ultimateGauge[actor] + amount, UltimateGaugeMax);
            OnGaugeChanged?.Invoke(actor, _ultimateGauge[actor]);
            CheckUltimateInsertion();
        }

        private void CheckUltimateInsertion()
        {
            for (int i = 0; i < Context.Players.Count; i++)
            {
                var player = Context.Players[i];
                if (!player.IsAlive) continue;
                if (!_ultimateGauge.TryGetValue(player, out var gauge) || gauge < UltimateGaugeMax) continue;
                if (!_actorChars.TryGetValue(player, out var cd) || cd.ultimate == null) continue;

                // Don't insert if an ultimate is already in hand
                bool alreadyPresent = false;
                for (int j = 0; j < Hand.Slots.Count; j++)
                    if (Hand.Slots[j].IsUltimate) { alreadyPresent = true; break; }
                if (alreadyPresent) continue;

                _ultimateOwner      = player;   // ResolveCaster uses this for ultimate cards
                _ultimateGauge[player] = 0;
                OnGaugeChanged?.Invoke(player, 0);
                Hand.InsertUltimate(cd.ultimate);
            }
        }

        private ICombatActor ResolveCaster(BattleCard card)
        {
            if (card.IsUltimate)
                return _ultimateOwner ?? Context.Players[0];
            if (card.Skill != null && _skillOwners.TryGetValue(card.Skill, out var owner))
                return owner;
            return Context.Players[0];
        }

        private ICombatActor GetAlivePlayer()
        {
            for (int i = 0; i < Context.Players.Count; i++)
                if (Context.Players[i].IsAlive) return Context.Players[i];
            return null;
        }

        private List<ICombatActor> BuildEnemyTurnOrder()
        {
            var list = new List<ICombatActor>(Context.Enemies.Count);
            for (int i = 0; i < Context.Enemies.Count; i++)
                if (Context.Enemies[i].IsAlive) list.Add(Context.Enemies[i]);
            list.Sort((a, b) => b.CombatClass.CompareTo(a.CombatClass));
            return list;
        }

        private ICombatActor RandomAliveEnemy()
        {
            var alive = new List<ICombatActor>(Context.Enemies.Count);
            for (int i = 0; i < Context.Enemies.Count; i++)
                if (Context.Enemies[i].IsAlive) alive.Add(Context.Enemies[i]);
            return alive.Count > 0 ? alive[_rng.Next(alive.Count)] : null;
        }

        private void Resolve(CombatActionResult result)
        {
            OnActionResolved?.Invoke(result);
            if (result.Target != null && !result.Target.IsAlive)
                PurgeDeadPlayerIfNeeded(result.Target);
        }

        private void PurgeDeadPlayerIfNeeded(ICombatActor actor)
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
    }
}
