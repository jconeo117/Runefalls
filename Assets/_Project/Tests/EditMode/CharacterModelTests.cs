using NUnit.Framework;
using Runefall.Characters;

namespace Runefall.Tests.EditMode
{
    public class CharacterModelTests
    {
        private CharacterModel model;

        private static CharacterStats MakeStats(float ps, float ataque = 10f, float defensa = 5f)
        {
            return new CharacterStats
            {
                ofensivas  = new OffensiveStats { ataque = ataque },
                defensivas = new DefensiveStats { defensa = defensa },
                vitales    = new VitalStats { ps = ps }
            };
        }

        [SetUp]
        public void SetUp()
        {
            model = new CharacterModel("TestHero", MakeStats(100f), maxMP: 50f);
        }

        // ── HP ───────────────────────────────────────────────────────────────

        [Test]
        public void CurrentHP_StartsAtMaxHP()
        {
            Assert.AreEqual(100f, model.CurrentHP, 0.001f);
        }

        [Test]
        public void TakeDamage_ReducesHP()
        {
            model.TakeDamage(30f);
            Assert.AreEqual(70f, model.CurrentHP, 0.001f);
        }

        [Test]
        public void TakeDamage_CannotGoBelowZero()
        {
            model.TakeDamage(999f);
            Assert.AreEqual(0f, model.CurrentHP, 0.001f);
        }

        [Test]
        public void TakeDamage_NegativeOrZero_DoesNothing()
        {
            model.TakeDamage(-10f);
            Assert.AreEqual(100f, model.CurrentHP, 0.001f);
        }

        [Test]
        public void Heal_IncreasesHP()
        {
            model.TakeDamage(50f);
            model.Heal(20f);
            Assert.AreEqual(70f, model.CurrentHP, 0.001f);
        }

        [Test]
        public void Heal_CannotExceedMaxHP()
        {
            model.Heal(999f);
            Assert.AreEqual(100f, model.CurrentHP, 0.001f);
        }

        [Test]
        public void Heal_AmplifiedByTasaRecuperacion()
        {
            var stats = MakeStats(100f);
            stats.vitales.tasaRecuperacion = 0.5f; // +50% heal
            var hero = new CharacterModel("Hero", stats);
            hero.TakeDamage(60f);
            hero.Heal(20f); // effective = 20 * 1.5 = 30
            Assert.AreEqual(70f, hero.CurrentHP, 0.001f);
        }

        [Test]
        public void IsAlive_TrueWhenHPAboveZero()
        {
            Assert.IsTrue(model.IsAlive);
        }

        [Test]
        public void IsAlive_FalseWhenHPIsZero()
        {
            model.TakeDamage(100f);
            Assert.IsFalse(model.IsAlive);
        }

        // ── ApplyRegen ───────────────────────────────────────────────────────

        [Test]
        public void ApplyRegen_RecoversFractionOfLostHP()
        {
            var stats = MakeStats(100f);
            stats.vitales.tasaRegen = 0.1f; // 10% of lost HP
            var hero = new CharacterModel("Hero", stats);
            hero.TakeDamage(50f);    // lost = 50
            hero.ApplyRegen();       // regain = 50 * 0.1 = 5
            Assert.AreEqual(55f, hero.CurrentHP, 0.001f);
        }

        [Test]
        public void ApplyRegen_ZeroRate_DoesNothing()
        {
            model.TakeDamage(50f);
            model.ApplyRegen(); // tasaRegen = 0
            Assert.AreEqual(50f, model.CurrentHP, 0.001f);
        }

        // ── MP ───────────────────────────────────────────────────────────────

        [Test]
        public void CurrentMP_StartsAtMaxMP()
        {
            Assert.AreEqual(50f, model.CurrentMP, 0.001f);
        }

        [Test]
        public void UseMP_ReducesMP()
        {
            model.UseMP(20f);
            Assert.AreEqual(30f, model.CurrentMP, 0.001f);
        }

        [Test]
        public void UseMP_CannotGoBelowZero()
        {
            model.UseMP(999f);
            Assert.AreEqual(0f, model.CurrentMP, 0.001f);
        }

        [Test]
        public void UseMP_NegativeOrZero_DoesNothing()
        {
            model.UseMP(-5f);
            Assert.AreEqual(50f, model.CurrentMP, 0.001f);
        }

        [Test]
        public void RestoreMP_IncreasesMP()
        {
            model.UseMP(30f);
            model.RestoreMP(10f);
            Assert.AreEqual(30f, model.CurrentMP, 0.001f);
        }

        [Test]
        public void RestoreMP_CannotExceedMaxMP()
        {
            model.RestoreMP(999f);
            Assert.AreEqual(50f, model.CurrentMP, 0.001f);
        }

        // ── Eventos ──────────────────────────────────────────────────────────

        [Test]
        public void TakeDamage_FiresOnHPChanged()
        {
            float received = -1f;
            model.OnHPChanged += v => received = v;
            model.TakeDamage(30f);
            Assert.AreEqual(70f, received, 0.001f);
        }

        [Test]
        public void Heal_FiresOnHPChanged()
        {
            float received = -1f;
            model.TakeDamage(50f);
            model.OnHPChanged += v => received = v;
            model.Heal(20f);
            Assert.AreEqual(70f, received, 0.001f);
        }

        [Test]
        public void TakeDamage_ZeroAmount_DoesNotFireEvent()
        {
            bool fired = false;
            model.OnHPChanged += _ => fired = true;
            model.TakeDamage(0f);
            Assert.IsFalse(fired);
        }

        [Test]
        public void UseMP_FiresOnMPChanged()
        {
            float received = -1f;
            model.OnMPChanged += v => received = v;
            model.UseMP(20f);
            Assert.AreEqual(30f, received, 0.001f);
        }

        [Test]
        public void RestoreMP_FiresOnMPChanged()
        {
            float received = -1f;
            model.UseMP(30f);
            model.OnMPChanged += v => received = v;
            model.RestoreMP(10f);
            Assert.AreEqual(30f, received, 0.001f);
        }

        // ── Constructor Validation ────────────────────────────────────────────

        [Test]
        public void Heal_NegativeOrZero_DoesNothing()
        {
            model.TakeDamage(50f);
            model.Heal(-10f);
            Assert.AreEqual(50f, model.CurrentHP, 0.001f);
        }

        [Test]
        public void RestoreMP_NegativeOrZero_DoesNothing()
        {
            model.UseMP(30f);
            model.RestoreMP(-5f);
            Assert.AreEqual(20f, model.CurrentMP, 0.001f);
        }

        [Test]
        public void TakeDamage_Overkill_FiresEventWithZero()
        {
            float received = -1f;
            model.OnHPChanged += v => received = v;
            model.TakeDamage(999f);
            Assert.AreEqual(0f, received, 0.001f);
        }

        [Test]
        public void Constructor_NullName_Throws()
        {
            Assert.Throws<System.ArgumentException>(
                () => new CharacterModel(null, MakeStats(100f)));
        }

        [Test]
        public void Constructor_ZeroPS_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new CharacterModel("Hero", MakeStats(0f)));
        }
    }
}
