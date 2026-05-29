using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Runefall.Combat;
using Runefall.Data;
using Runefall.Characters;

namespace Runefall.Tests.EditMode
{
    [TestFixture]
    public class CardSystemTests
    {
        private CharacterData _character;
        private SkillData     _skill1;
        private SkillData     _skill2;
        private CardPool      _pool;

        [SetUp]
        public void SetUp()
        {
            _skill1 = ScriptableObject.CreateInstance<SkillData>();
            _skill1.skillName = "Ataque Igneo";
            _skill1.element   = ElementType.Fire;

            _skill2 = ScriptableObject.CreateInstance<SkillData>();
            _skill2.skillName = "Barrera Ignia";
            _skill2.element   = ElementType.Fire;

            _character = ScriptableObject.CreateInstance<CharacterData>();
            _character.skill1 = _skill1;
            _character.skill2 = _skill2;

            var chars = new List<CharacterData> { _character };
            _pool = new CardPool(chars);
        }

        [TearDown]
        public void TearDown()
        {
            if (_skill1 != null) Object.DestroyImmediate(_skill1);
            if (_skill2 != null) Object.DestroyImmediate(_skill2);
            if (_character != null) Object.DestroyImmediate(_character);
        }

        [Test]
        public void BattleCard_GeneratesUniqueIds()
        {
            var cardA = new BattleCard(_skill1, 1);
            var cardB = new BattleCard(_skill1, 1);

            Assert.AreNotEqual(cardA.Id, cardB.Id, "Each instantiated BattleCard should have a unique ID.");
        }

        [Test]
        public void BattleCard_WithRank_PreservesId()
        {
            var card = new BattleCard(_skill1, 1);
            int originalId = card.Id;

            var upgradedCard = card.WithRank(2);

            Assert.AreEqual(originalId, upgradedCard.Id, "WithRank should preserve the card's original unique ID.");
            Assert.AreEqual(2, upgradedCard.Rank, "The upgraded card's rank should be updated.");
        }

        [Test]
        public void CombatHand_Deal_GuaranteesSkill1AndSkill2Leftmost()
        {
            var skill3 = ScriptableObject.CreateInstance<SkillData>();
            skill3.skillName = "Ataque Sombrío";
            skill3.element   = ElementType.Shadow;

            var skill4 = ScriptableObject.CreateInstance<SkillData>();
            skill4.skillName = "Barrera Sombría";
            skill4.element   = ElementType.Shadow;

            var char2 = ScriptableObject.CreateInstance<CharacterData>();
            char2.skill1 = skill3;
            char2.skill2 = skill4;

            var charsForPool = new List<CharacterData> { _character, char2 };
            var mockRng = new TestRandom(2, 3);
            var customPool = new CardPool(charsForPool, mockRng);

            var hand = new CombatHand(customPool, fieldCount: 1, hasBench: false);

            var charsForDeal = new List<CharacterData> { _character };
            hand.Deal(charsForDeal);

            var slots = hand.Slots;

            // Visual order leftmost corresponds to highest indices (HandSize - 1, HandSize - 2)
            // Visually:
            // Sibling 0 (leftmost) = slots[3] (which must be Skill 1)
            // Sibling 1 (next)     = slots[2] (which must be Skill 2)
            Assert.AreEqual(4, slots.Count, "The deal should populate exactly 4 slots (MVP size).");
            
            Assert.AreEqual(_skill1, slots[3].Skill, "Leftmost card (slots[3]) must be Skill 1.");
            Assert.AreEqual(1, slots[3].Rank, "Leftmost card must start at Rank 1.");

            Assert.AreEqual(_skill2, slots[2].Skill, "Second leftmost card (slots[2]) must be Skill 2.");
            Assert.AreEqual(1, slots[2].Rank, "Second leftmost card must start at Rank 1.");

            Object.DestroyImmediate(skill3);
            Object.DestroyImmediate(skill4);
            Object.DestroyImmediate(char2);
        }

        [Test]
        public void CombatHand_Deal_RefillsHandOnMerge_AndKeepsSkill1AndSkill2Leftmost()
        {
            var skill3 = ScriptableObject.CreateInstance<SkillData>();
            skill3.skillName = "Ataque Sombrío";
            skill3.element   = ElementType.Shadow;

            var skill4 = ScriptableObject.CreateInstance<SkillData>();
            skill4.skillName = "Barrera Sombría";
            skill4.element   = ElementType.Shadow;

            var char2 = ScriptableObject.CreateInstance<CharacterData>();
            char2.skill1 = skill3;
            char2.skill2 = skill4;

            var charsForPool = new List<CharacterData> { _character, char2 };
            // First two draws return index 2 (skill3), causing them to merge.
            // Third draw returns index 3 (skill4), filling the hand back to 4.
            var mockRng = new TestRandom(2, 2, 3);
            var customPool = new CardPool(charsForPool, mockRng);

            var hand = new CombatHand(customPool, fieldCount: 1, hasBench: false);

            var charsForDeal = new List<CharacterData> { _character };
            hand.Deal(charsForDeal);

            var slots = hand.Slots;

            // Hand must be refilled to 4 slots because of the merge
            Assert.AreEqual(4, slots.Count, "The deal should refill to 4 slots when merges occur.");

            // Highest indices must still be Skill 1 and Skill 2 in that order
            Assert.AreEqual(_skill1, slots[3].Skill, "Leftmost card (slots[3]) must be Skill 1.");
            Assert.AreEqual(1, slots[3].Rank, "Leftmost card must start at Rank 1.");

            Assert.AreEqual(_skill2, slots[2].Skill, "Second leftmost card (slots[2]) must be Skill 2.");
            Assert.AreEqual(1, slots[2].Rank, "Second leftmost card must start at Rank 1.");

            // Skill 3 merged into Rank 2 at index 1
            Assert.AreEqual(skill3, slots[1].Skill, "Slot 1 must contain Skill 3.");
            Assert.AreEqual(2, slots[1].Rank, "Slot 1 Skill 3 must be Rank 2 due to merge.");

            // Skill 4 is at index 0 (rightmost visually)
            Assert.AreEqual(skill4, slots[0].Skill, "Slot 0 must contain Skill 4.");
            Assert.AreEqual(1, slots[0].Rank, "Slot 0 Skill 4 must be Rank 1.");

            Object.DestroyImmediate(skill3);
            Object.DestroyImmediate(skill4);
            Object.DestroyImmediate(char2);
        }

        [Test]
        public void CombatHand_CheckMerges_CascadesAdjacentEqualCards()
        {
            var hand = new CombatHand(_pool, fieldCount: 1, hasBench: false);
            
            // Manually add matching cards consecutively
            // slots[0] (right) = skill1, slots[1] = skill1 (identical)
            // They should merge automatically!
            var chars = new List<CharacterData> { _character };
            hand.Deal(chars);

            // Let's clear slots and inject manually to test merging
            var slotsField = typeof(CombatHand).GetField("_slots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var slotsList = (List<BattleCard>)slotsField.GetValue(hand);
            slotsList.Clear();

            var cardA = new BattleCard(_skill1, 1);
            var cardB = new BattleCard(_skill1, 1);
            var cardC = new BattleCard(_skill2, 1);

            slotsList.Add(cardA); // slots[0]
            slotsList.Add(cardB); // slots[1] (identical to slots[0])
            slotsList.Add(cardC); // slots[2]

            // Trigger merge check manually (or via public methods like TryMove)
            var checkMergesMethod = typeof(CombatHand).GetMethod("CheckMerges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            checkMergesMethod.Invoke(hand, null);

            // After merge, cardA and cardB should combine into rank 2 at slots[0].
            // CardC should slide to slots[1].
            Assert.AreEqual(2, slotsList.Count);
            Assert.AreEqual(_skill1, slotsList[0].Skill);
            Assert.AreEqual(2, slotsList[0].Rank);
            Assert.AreEqual(cardA.Id, slotsList[0].Id, "Merged card should preserve the ID of the first merge partner.");
            Assert.AreEqual(_skill2, slotsList[1].Skill);
        }

        private class TestRandom : System.Random
        {
            private readonly int[] _sequence;
            private int _index;
            
            public TestRandom(params int[] sequence)
            {
                _sequence = sequence;
            }
            
            public override int Next(int maxValue)
            {
                int val = _sequence[_index];
                _index = (_index + 1) % _sequence.Length;
                return val % maxValue;
            }
        }
    }
}
