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
        public CombatContext Context { get; protected set; }
        public CombatHand    Hand    { get; protected set; }
        public CombatPhase   Phase   { get; protected set; } = CombatPhase.Idle;
        public int           Round   { get; protected set; }

        /// <summary>
        /// When set, the NEXT EndPlayerTurn skips the enemy phase entirely and goes straight to the next
        /// player turn. Used by a boss phase transition: the transition cinematic IS the boss's turn, so
        /// the boss does not also attack that round. Auto-clears after one use.
        /// </summary>
        public bool SkipNextEnemyPhase { get; set; }

        /// <summary>Fires immediately when player turn logic begins. Use for camera repositioning.</summary>
        public event Action<int>                OnPlayerTurnBegin;
        /// <summary>Fires after PlayerTurnStartHandler delay (or immediately if handler is null). Use for passives, UI, card display.</summary>
        public event Action<int>                OnPlayerTurnStarted;  // round number
        public event Action                     OnEnemyTurnStarted;
        /// <summary>Fires when a skill/ultimate is submitted but NOT yet resolved. CombatAnimationDriver enqueues this for deferred resolution at impact frame.</summary>
        public event Action<PendingAction>      OnActionPending;
        /// <summary>Fires when damage/heal is actually applied (at animation impact frame via ResolveAction). SkillEventBridge, HUD, and camera director subscribe here.</summary>
        public event Action<CombatActionResult> OnActionResolved;
        public event Action<bool>               OnCombatEnded;        // playerWon
        public event Action<string, int>        OnMergeOccurred;      // skillName, newRank
        /// <summary>Fires whenever a player's ultimate gauge changes. (actor, currentOrbs) — max = UltimateGaugeMax (7).</summary>
        public event Action<ICombatActor, int> OnGaugeChanged;

        /// <summary>
        /// Optional. When set, BeginPlayerTurn fires OnPlayerTurnBegin immediately (camera),
        /// then calls this handler with a "fire" callback. Invoke the callback when ready
        /// (e.g., after camera settles) to trigger OnPlayerTurnStarted. If null, fires immediately.
        /// </summary>
        public Action<Action> PlayerTurnStartHandler;

        /// <summary>
        /// Fires when the player has used all action slots but before EndPlayerTurn runs.
        /// Subscriber is responsible for calling EndPlayerTurn() after animations complete.
        /// If no subscribers, EndPlayerTurn() runs immediately.
        /// </summary>
        public event Action OnPlayerActionsExhausted;

        protected readonly IEnemyPhaseAnimator                         _phaseAnimator;
        protected readonly Dictionary<SkillData, ICombatActor>          _skillOwners   = new();
        protected readonly Dictionary<ICombatActor, CharacterData>      _actorChars    = new();
        protected readonly HashSet<ICombatActor>                        _purgedPlayers = new();
        protected ICombatActor _ultimateOwner;
        protected Random       _rng;

        protected readonly Dictionary<ICombatActor, int> _ultimateGauge = new();
        public const int UltimateGaugeMax = 7;   // 7 sections: +1 per card played, moved, or merged

        /// <param name="phaseAnimator">
        /// Presentation MonoBehaviour that animates enemy turns.
        /// Pass null to resolve enemy turns synchronously (useful in tests).
        /// </param>
        public TurnManager(IEnemyPhaseAnimator phaseAnimator = null)
        {
            _phaseAnimator = phaseAnimator;
        }

        public virtual void StartCombat(
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
        /// Fires OnActionPending — damage is deferred to ResolveAction() at animation impact frame.
        /// Returns false if phase is wrong, card index invalid, or no valid target exists.
        /// </summary>
        public virtual bool SubmitSkill(int cardIndex, ICombatActor explicitTarget = null)
        {
            if (Phase != CombatPhase.PlayerTurn) return false;
            if (Context != null && Context.IsOver) return false;
            if (!Hand.TryUse(cardIndex, out var slot, out _)) return false;

            var caster = ResolveCaster(slot);
            if (slot.IsUltimate)
                ResetGauge(caster);              // ultimate consumed → gauge empties, cycle repeats
            else
                FillGauge(caster);               // +1 gauge for using a regular card

            var targetType = slot.IsUltimate
                ? (slot.Ultimate?.targetType ?? TargetType.SingleEnemy)
                : (slot.Skill?.targetType    ?? TargetType.SingleEnemy);

            ICombatActor singleTarget = null;
            switch (targetType)
            {
                case TargetType.Self:
                    singleTarget = caster;
                    break;

                case TargetType.AllEnemies:
                case TargetType.AllAllies:
                    singleTarget = null;   // resolved from context at impact time
                    break;

                default: // SingleEnemy, RandomEnemy
                    singleTarget = (targetType == TargetType.SingleEnemy)
                        ? (explicitTarget != null && explicitTarget.IsAlive ? explicitTarget : RandomAliveEnemy())
                        : RandomAliveEnemy();
                    if (singleTarget == null) return false;
                    break;
            }

            var pending = new PendingAction(
                caster:     caster,
                target:     singleTarget,
                skill:      slot.IsUltimate ? null : slot.Skill,
                ultimate:   slot.IsUltimate ? slot.Ultimate : null,
                rank:       slot.Rank,
                targetType: targetType,
                isUltimate: slot.IsUltimate);

            OnActionPending?.Invoke(pending);

            if (Hand.ActionsRemaining == 0) NotifyActionsExhausted();

            return true;
        }

        /// <summary>
        /// Called by CombatAnimationDriver at the animation impact frame.
        /// Applies damage/heal, fires OnActionResolved per target, and checks for death/combat end.
        /// Returns one result per target hit (array of 1 for single-target, N for AoE).
        /// </summary>
        /// <summary>
        /// Resolves a single hit against a specific actor, bypassing TargetType.
        /// Used by AoE projectile triggers where each projectile hits one distinct target.
        /// Returns empty array if target is null or dead.
        /// </summary>
        public virtual CombatActionResult[] ResolveForTarget(PendingAction pending, float hitFraction, ICombatActor specificTarget)
        {
            if (specificTarget == null || !specificTarget.IsAlive)
                return System.Array.Empty<CombatActionResult>();
            var r = pending.IsUltimate
                ? CombatResolver.ExecuteUltimate(pending.Ultimate, pending.Caster, specificTarget, hitFraction)
                : CombatResolver.Execute(pending.Skill, pending.Rank, pending.Caster, specificTarget, hitFraction);
            Resolve(r);
            return new[] { r };
        }

        /// <param name="hitFraction">
        /// Fraction of total skill damage this hit represents.
        /// Pass 1/N for N-hit skills. Default 1f = single hit (full damage).
        /// </param>
        public virtual CombatActionResult[] ResolveAction(PendingAction pending, float hitFraction = 1f)
        {
            if (pending.Caster == null) return System.Array.Empty<CombatActionResult>();

            switch (pending.TargetType)
            {
                case TargetType.AllEnemies:
                {
                    var results = pending.IsUltimate
                        ? CombatResolver.ExecuteUltimateAll(pending.Ultimate, pending.Caster, Context.Enemies, hitFraction)
                        : CombatResolver.ExecuteAll(pending.Skill, pending.Rank, pending.Caster, Context.Enemies, hitFraction);
                    for (int i = 0; i < results.Length; i++) Resolve(results[i]);
                    return results;
                }

                case TargetType.AllAllies:
                {
                    var results = pending.IsUltimate
                        ? CombatResolver.ExecuteUltimateAll(pending.Ultimate, pending.Caster, Context.Players, hitFraction)
                        : CombatResolver.ExecuteAll(pending.Skill, pending.Rank, pending.Caster, Context.Players, hitFraction);
                    for (int i = 0; i < results.Length; i++) Resolve(results[i]);
                    return results;
                }

                default: // SingleEnemy, RandomEnemy, Self
                {
                    var target = pending.Target;
                    if (target == null || !target.IsAlive)
                        return System.Array.Empty<CombatActionResult>();
                    var r = pending.IsUltimate
                        ? CombatResolver.ExecuteUltimate(pending.Ultimate, pending.Caster, target, hitFraction)
                        : CombatResolver.Execute(pending.Skill, pending.Rank, pending.Caster, target, hitFraction);
                    Resolve(r);
                    return new[] { r };
                }
            }
        }

        /// <summary>
        /// Player moves card at fromIndex to toIndex. Costs 1 action.
        /// Returns false if phase is wrong or move is invalid.
        /// </summary>
        public virtual bool SubmitMove(int fromIndex, int toIndex)
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

        /// <summary>
        /// Multiplayer: server resolved the enemy phase remotely.
        /// Ticks end-of-round effects/regen, redeals hand, and begins the next player turn
        /// at the server-authoritative round number — skips local enemy phase entirely.
        /// </summary>
        public virtual void ForceNewPlayerTurn(int round)
        {
            if (Phase == CombatPhase.Over) return;
            if (Context == null || Hand == null) return;

            // End-of-round maintenance (same as EndOfRound, minus ProcessEnemyPhase)
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

            int handBefore = Hand.Slots.Count;
            CheckUltimateInsertion();
            Hand.Refill(handBefore);

            if (Context.IsOver) { FinishCombat(); return; }

            Round              = round;
            Phase              = CombatPhase.PlayerTurn;
            Context.TurnNumber = round;
            Hand.ResetActions();

            OnPlayerTurnBegin?.Invoke(Round);

            void Fire() => OnPlayerTurnStarted?.Invoke(Round);
            if (PlayerTurnStartHandler != null)
                PlayerTurnStartHandler(Fire);
            else
                Fire();
        }

        /// <summary>Player ends their turn before exhausting all actions.</summary>
        public virtual void EndPlayerTurn()
        {
            if (Phase != CombatPhase.PlayerTurn) return;
            if (Context.IsOver) { FinishCombat(); return; }

            // Boss phase transition consumed the boss's turn this round → skip enemy actions, go to the
            // next player turn (still run end-of-round maintenance: effect ticks, regen, refill).
            if (SkipNextEnemyPhase)
            {
                SkipNextEnemyPhase = false;
                Phase = CombatPhase.EnemyTurn;
                EndOfRound();
                return;
            }

            Phase = CombatPhase.EnemyTurn;
            OnEnemyTurnStarted?.Invoke();

            if (_phaseAnimator != null)
                _phaseAnimator.RunEnemyPhase(BuildEnemyTurnOrder(), ExecuteSingleEnemyTurn, EndOfRound);
            else
                ProcessEnemyPhase();
        }

        /// <summary>Auto-pass: the player has actions left but no playable cards. Burns the remaining
        /// actions and runs the normal end-of-turn flow (queued skill animations drain, then the turn
        /// ends), so the player never gets stuck with an unusable action.</summary>
        public virtual void PassRemainingActions()
        {
            if (Phase != CombatPhase.PlayerTurn) return;
            Hand.SetActionsRemaining(0);
            NotifyActionsExhausted();
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
            // Ultimate insertion happens in EndOfRound (before the refill) so it completes the hand.

            OnPlayerTurnBegin?.Invoke(Round);   // immediate: camera starts moving

            void Fire() => OnPlayerTurnStarted?.Invoke(Round);
            if (PlayerTurnStartHandler != null)
                PlayerTurnStartHandler(Fire);
            else
                Fire();
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
            if (enemy.Effects.HasBehavior(EffectBehavior.SkipTurn)) return;
            var player = GetAlivePlayer();
            if (player == null) return;
            if (enemy is IEnemyTurnHandler handler)
                OnActionPending?.Invoke(handler.TakeTurn(Context, player));
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

            // Check defeat/victory BEFORE refilling: a dead player team means combat is over, and the
            // card pool may be empty (no field characters left) — refilling there throws.
            if (Context.IsOver) { FinishCombat(); return; }

            // Insert the ultimate FIRST (if the gauge is full), then refill the rest: the ultimate is the
            // next drawn card and COMPLETES the hand to HandSize — not an extra card on top of a full hand.
            int handBefore = Hand.Slots.Count;
            CheckUltimateInsertion();
            Hand.Refill(handBefore);
            BeginPlayerTurn();
        }

        private void FinishCombat()
        {
            Phase = CombatPhase.Over;
            OnCombatEnded?.Invoke(Context.PlayerWon);
        }

        // ── ultimate gauge ────────────────────────────────────────────────────────

        /// <summary>Empties a player's ultimate gauge — called when the ultimate card is USED.</summary>
        private void ResetGauge(ICombatActor actor)
        {
            if (actor == null) return;
            _ultimateGauge[actor] = 0;
            OnGaugeChanged?.Invoke(actor, 0);
        }

        private void FillGauge(ICombatActor actor, int amount = 1)
        {
            if (actor == null || !actor.IsAlive) return;
            if (!_ultimateGauge.ContainsKey(actor)) _ultimateGauge[actor] = 0;
            _ultimateGauge[actor] = Math.Min(_ultimateGauge[actor] + amount, UltimateGaugeMax);
            OnGaugeChanged?.Invoke(actor, _ultimateGauge[actor]);
            // Do NOT insert the ultimate mid-turn. A full gauge only means "ready"; the ultimate is DRAWN
            // into the hand at the next player turn (CheckUltimateInsertion runs in BeginPlayerTurn, with the
            // refill). Inserting here shifted hand indices and corrupted the queued play actions.
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

                _ultimateOwner = player;   // ResolveCaster uses this for ultimate cards
                Hand.InsertUltimate(cd.ultimate);
                // Gauge stays FULL while the ultimate sits in hand; it empties only when the ultimate is USED.
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
            var alive = new List<ICombatActor>(Context.Enemies.Count);
            for (int i = 0; i < Context.Enemies.Count; i++)
                if (Context.Enemies[i].IsAlive) alive.Add(Context.Enemies[i]);
            alive.Sort((a, b) => b.CombatClass.CompareTo(a.CombatClass));

            // Expand by ActionsPerTurn: an actor acting N times appears N times consecutively.
            // The phase animator runs one animated turn per list entry (boss passive: +2 acciones).
            var order = new List<ICombatActor>(alive.Count);
            for (int i = 0; i < alive.Count; i++)
            {
                int actions = Math.Max(1, alive[i].ActionsPerTurn);
                for (int k = 0; k < actions; k++) order.Add(alive[i]);
            }
            return order;
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

        protected void InvokeOnPlayerTurnBegin(int round) => OnPlayerTurnBegin?.Invoke(round);
        protected void InvokeOnPlayerTurnStarted(int round) => OnPlayerTurnStarted?.Invoke(round);
        protected void InvokeOnEnemyTurnStarted() => OnEnemyTurnStarted?.Invoke();
        protected void InvokeOnActionPending(PendingAction action) => OnActionPending?.Invoke(action);
        protected void InvokeOnActionResolved(CombatActionResult result) => OnActionResolved?.Invoke(result);
        protected void InvokeOnCombatEnded(bool playerWon) => OnCombatEnded?.Invoke(playerWon);
        protected void InvokeOnMergeOccurred(string skillName, int rank) => OnMergeOccurred?.Invoke(skillName, rank);
        protected void InvokeOnGaugeChanged(ICombatActor actor, int orbs) => OnGaugeChanged?.Invoke(actor, orbs);
        protected void InvokeOnPlayerActionsExhausted() => OnPlayerActionsExhausted?.Invoke();
    }
}
