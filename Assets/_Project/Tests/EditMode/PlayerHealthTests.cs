using NUnit.Framework;
using UnityEngine;
using Runefall.Presentation.Player;

namespace Runefall.Tests.EditMode
{
    public class PlayerHealthTests
    {
        private PlayerHealth health;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("TestPlayer");
            health = go.AddComponent<PlayerHealth>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(health.gameObject);
        }

        [Test]
        public void CurrentHP_StartsAtMaxHP()
        {
            Assert.AreEqual(health.MaxHP, health.CurrentHP);
        }

        [Test]
        public void TakeDamage_ReducesHP()
        {
            health.TakeDamage(30f);
            Assert.AreEqual(70f, health.CurrentHP, 0.001f);
        }

        [Test]
        public void TakeDamage_CannotGoBelowZero()
        {
            health.TakeDamage(999f);
            Assert.AreEqual(0f, health.CurrentHP, 0.001f);
        }

        [Test]
        public void Heal_IncreasesHP()
        {
            health.TakeDamage(50f);
            health.Heal(20f);
            Assert.AreEqual(70f, health.CurrentHP, 0.001f);
        }

        [Test]
        public void Heal_CannotExceedMaxHP()
        {
            health.Heal(999f);
            Assert.AreEqual(health.MaxHP, health.CurrentHP, 0.001f);
        }

        [Test]
        public void IsAlive_FalseWhenHPIsZero()
        {
            health.TakeDamage(100f);
            Assert.IsFalse(health.IsAlive);
        }

        [Test]
        public void IsAlive_TrueWhenHPAboveZero()
        {
            Assert.IsTrue(health.IsAlive);
        }
    }
}
