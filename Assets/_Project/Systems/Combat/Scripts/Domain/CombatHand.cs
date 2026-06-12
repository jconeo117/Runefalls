using System;
using System.Collections.Generic;
using Runefall.Data;

namespace Runefall.Combat
{
    /// <summary>
    /// Player's card hand for one combat encounter.
    ///
    /// Hand layout:
    ///   HandSize = 3 + fieldCount + (hasBench ? 1 : 0)
    ///   ActionsPerTurn = 3 (fixed — one action per card PLAYED or card MOVED)
    ///
    /// Turn 1 deal: guaranteed 2 skills per field character in order, then random fills.
    /// Subsequent refills: random draws from pool until hand is full.
    /// </summary>
    public class CombatHand
    {
        private readonly List<BattleCard> _slots;
        private readonly CardPool         _pool;

        public int  HandSize           { get; private set; }
        public int  ActionsPerTurn     { get; private set; }
        public int  ActionsRemaining   { get; private set; }
        public bool Dealt              { get; private set; }
        /// <summary>Net new slots added by the last Refill() call (after merges). All slots on first Deal.</summary>
        public int  NewCardsThisRefill { get; private set; }

        public IReadOnlyList<BattleCard> Slots => _slots.AsReadOnly();

        /// <summary>Fired after each merge. Args: merged SkillData, newRank.</summary>
        public event Action<SkillData, int> OnMerge;

        public CombatHand(CardPool pool, int fieldCount, bool hasBench)
        {
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));
            if (fieldCount < 1 || fieldCount > 3)
                throw new ArgumentOutOfRangeException(nameof(fieldCount), "Must be 1–3.");

            _pool            = pool;
            HandSize         = 3 + fieldCount + (hasBench ? 1 : 0);
            ActionsPerTurn   = 3;   // fixed: 3 actions per turn (play a card OR move a card), always
            ActionsRemaining = ActionsPerTurn;
            _slots           = new List<BattleCard>(HandSize);
        }

        /// <summary>
        /// First deal only. Guarantees skill1+skill2 at the leftmost visual positions (highest indices),
        /// and fills the remaining slots randomly from the pool.
        /// </summary>
        public void Deal(IReadOnlyList<CharacterData> fieldChars)
        {
            if (Dealt) throw new InvalidOperationException("Deal already called.");
            if (fieldChars == null || fieldChars.Count == 0)
                throw new ArgumentException("fieldChars required.", nameof(fieldChars));

            // We need the first 2 visual cards (leftmost, corresponding to highest indices: HandSize-1 and HandSize-2)
            // to be Skill 1 and Skill 2 in that order.
            // So we first fill indices 0 to HandSize-3 with random cards from the pool.
            int randomCount = HandSize - 2;
            for (int i = 0; i < randomCount; i++)
            {
                _slots.Add(new BattleCard(_pool.Draw(), rank: 1));
            }

            // Then we append Skill 2 and Skill 1.
            var firstChar = fieldChars[0];
            if (firstChar.skill2 != null) _slots.Add(new BattleCard(firstChar.skill2, rank: 1));
            if (firstChar.skill1 != null) _slots.Add(new BattleCard(firstChar.skill1, rank: 1));

            // Run initial check for merges in case the random cards matched
            CheckMerges();

            // Refill to HandSize by inserting new cards at index 0 to keep Skill 1 & 2 at the leftmost positions
            int safety = HandSize * 4;
            while (_slots.Count < HandSize && safety-- > 0)
            {
                _slots.Insert(0, new BattleCard(_pool.Draw(), rank: 1));
                CheckMerges();
            }

            NewCardsThisRefill = _slots.Count; // animate all cards on first deal
            Dealt = true;
        }

        /// <summary>
        /// Draw from pool until hand reaches HandSize. Keeps existing cards.
        /// Checks merges after each draw so adjacencies resolve in real time.
        /// </summary>
        public void Refill(int countBefore = -1)
        {
            // countBefore lets the caller count cards added just before Refill (e.g. an ultimate inserted
            // first) as part of NewCardsThisRefill, so the ultimate animates in as one of the drawn cards.
            int before = countBefore >= 0 ? countBefore : _slots.Count;
            int safety = HandSize * 4; // guard against degenerate all-rank-3 pools
            while (_slots.Count < HandSize && safety-- > 0)
            {
                var skill = _pool.Draw();
                if (skill == null) break;   // empty pool — stop instead of crashing
                _slots.Add(new BattleCard(skill, rank: 1));
                CheckMerges();
            }
            NewCardsThisRefill = _slots.Count - before;
        }

        private bool ValidIndex(int i) => i >= 0 && i < _slots.Count;

        // Scan left to right, merge first adjacent pair (same Skill, same Rank, Rank < 3).
        // Restart after each merge — cascades until hand is stable.
        private void CheckMerges()
        {
            bool merged;
            do
            {
                merged = false;
                for (int i = 0; i < _slots.Count - 1; i++)
                {
                    var a = _slots[i];
                    var b = _slots[i + 1];

                    if (a.IsUltimate || b.IsUltimate) continue;
                    if (a.Skill != b.Skill || a.Rank != b.Rank || a.Rank >= 3) continue;

                    _slots[i] = a.WithRank(a.Rank + 1);
                    _slots.RemoveAt(i + 1);
                    OnMerge?.Invoke(a.Skill, a.Rank + 1);
                    merged = true;
                    break;
                }
            } while (merged);
        }

        /// <summary>
        /// Move card from fromIndex to toIndex. Costs 1 action.
        /// Triggers merge check after repositioning — adjacent equal cards combine immediately.
        /// </summary>
        public bool TryMove(int from, int to, out string error)
        {
            if (ActionsRemaining <= 0)  { error = "No actions remaining.";       return false; }
            if (!ValidIndex(from))      { error = $"from {from} out of range.";  return false; }
            if (!ValidIndex(to))        { error = $"to {to} out of range.";      return false; }
            if (from == to)             { error = "from equals to.";             return false; }

            var card = _slots[from];
            _slots.RemoveAt(from);
            _slots.Insert(to, card);

            ActionsRemaining--;
            CheckMerges();
            error = null;
            return true;
        }

        /// <summary>
        /// Play card at index. Costs 1 action. Returns card to caller for resolution.
        /// Hand stays short until Refill() at end of turn — do not refill mid-turn.
        /// </summary>
        public bool TryUse(int index, out BattleCard used, out string error)
        {
            if (ActionsRemaining <= 0) { used = default; error = "No actions remaining.";      return false; }
            if (!ValidIndex(index))    { used = default; error = $"index {index} out of range."; return false; }

            used = _slots[index];
            _slots.RemoveAt(index);
            ActionsRemaining--;
            CheckMerges();
            error = null;
            return true;
        }

        public void SetSlots(List<BattleCard> newSlots)
        {
            _slots.Clear();
            if (newSlots != null)
            {
                _slots.AddRange(newSlots);
            }
        }

        public void SetActionsRemaining(int count)
        {
            ActionsRemaining = count;
        }

        public void ResetActions() => ActionsRemaining = ActionsPerTurn;

        /// <summary>Add the ultimate as a freshly DRAWN card (appended, like a pool draw) and count it as a
        /// new card so it animates in with the turn-start refill — never injected mid-turn at index 0.
        /// Caller must verify the gauge is full before calling.</summary>
        public void InsertUltimate(UltimateData ultimate)
        {
            if (ultimate == null) return;
            _slots.Add(new BattleCard(ultimate));
        }

        /// <summary>
        /// Call when a field character dies.
        /// Removes their skills from pool, purges their cards from hand,
        /// and reduces HandSize / ActionsPerTurn by 1.
        /// </summary>
        public void OnCharacterLeft(CharacterData character)
        {
            _pool.RemoveCharacter(character);

            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                var card = _slots[i];
                if (!card.IsUltimate &&
                    (card.Skill == character.skill1 || card.Skill == character.skill2))
                    _slots.RemoveAt(i);
            }

            HandSize         = Math.Max(0, HandSize - 1);
            ActionsPerTurn   = Math.Max(0, ActionsPerTurn - 1);
            ActionsRemaining = Math.Min(ActionsRemaining, ActionsPerTurn);
            // Refill is intentionally deferred — EndOfRound calls Hand.Refill() before BeginPlayerTurn,
            // so new cards only appear (animated) when OnPlayerTurnStarted fires.
        }
    }
}
