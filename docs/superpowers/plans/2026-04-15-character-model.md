# CharacterModel — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Clase C# pura que encapsula el estado de un personaje (HP, MP, stats base) con eventos para que la Presentación reaccione a cambios.

**Architecture:** Clase de dominio puro en `Characters/` — sin MonoBehaviour, sin dependencias de UnityEngine. Usa `System.Math.Clamp` y `System.Action<T>`. Los tests son NUnit EditMode sin necesidad de GameObject. Se conectará con `CharacterData` (SO) y `CharacterStats` en Sprint 3.

**Tech Stack:** C# / NUnit / Unity Test Runner EditMode. Assembly `Runefall` (ya existe en `Assets/_Project/Scripts/Runefall.asmdef`). Tests en `Runefall.Tests.EditMode` (ya existe).

---

## Archivos

| Acción | Ruta |
|--------|------|
| Crear | `Assets/_Project/Scripts/Characters/CharacterModel.cs` |
| Crear | `Assets/_Project/Tests/EditMode/CharacterModelTests.cs` |

---

## Task 1: CharacterModel + tests

**Files:**
- Create: `Assets/_Project/Scripts/Characters/CharacterModel.cs`
- Create: `Assets/_Project/Tests/EditMode/CharacterModelTests.cs`

- [ ] **Step 1.1 — Escribir los tests (TDD: primero el test, luego la implementación)**

Crear `Assets/_Project/Tests/EditMode/CharacterModelTests.cs`:

```csharp
using NUnit.Framework;
using Runefall.Characters;

namespace Runefall.Tests.EditMode
{
    public class CharacterModelTests
    {
        private CharacterModel model;

        [SetUp]
        public void SetUp()
        {
            // name, maxHP, maxMP, baseAttack, baseDefense
            model = new CharacterModel("TestHero", 100f, 50f, 10f, 5f);
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
    }
}
```

- [ ] **Step 1.2 — Verificar que los tests fallan (compilación)**

Abrir Unity Editor → `Window > General > Test Runner > EditMode`.
Los tests deben aparecer con error de compilación porque `CharacterModel` no existe aún.
(Paso manual — documentar en el reporte que los tests fueron escritos antes de la implementación.)

- [ ] **Step 1.3 — Implementar CharacterModel**

Crear `Assets/_Project/Scripts/Characters/CharacterModel.cs`:

```csharp
using System;

namespace Runefall.Characters
{
    /// <summary>
    /// Estado de un personaje: HP, MP y stats base.
    /// Clase C# pura — sin MonoBehaviour, sin dependencias de UnityEngine.
    /// Sprint 3 añadirá CharacterData SO, CharacterStats, cartas y runas.
    /// </summary>
    public class CharacterModel
    {
        // ── Identidad ────────────────────────────────────────────────────────
        public string Name { get; }

        // ── HP ───────────────────────────────────────────────────────────────
        public float CurrentHP  { get; private set; }
        public float MaxHP      { get; }
        public bool  IsAlive    => CurrentHP > 0f;

        // ── MP ───────────────────────────────────────────────────────────────
        public float CurrentMP  { get; private set; }
        public float MaxMP      { get; }

        // ── Stats base (migrarán a CharacterStats en Sprint 3) ───────────────
        public float BaseAttack  { get; }
        public float BaseDefense { get; }

        // ── Eventos (Presentación suscribe, dominio nunca conoce al suscriptor)
        public event Action<float> OnHPChanged;
        public event Action<float> OnMPChanged;

        // ── Constructor ──────────────────────────────────────────────────────
        public CharacterModel(string name, float maxHP, float maxMP,
                              float baseAttack, float baseDefense)
        {
            Name        = name;
            MaxHP       = maxHP;
            MaxMP       = maxMP;
            BaseAttack  = baseAttack;
            BaseDefense = baseDefense;
            CurrentHP   = maxHP;
            CurrentMP   = maxMP;
        }

        // ── HP ───────────────────────────────────────────────────────────────

        /// <summary>Reduce el HP. Ignora valores menores o iguales a 0.</summary>
        public void TakeDamage(float amount)
        {
            if (amount <= 0f) return;
            CurrentHP = Math.Clamp(CurrentHP - amount, 0f, MaxHP);
            OnHPChanged?.Invoke(CurrentHP);
        }

        /// <summary>Aumenta el HP. Ignora valores menores o iguales a 0.</summary>
        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            CurrentHP = Math.Clamp(CurrentHP + amount, 0f, MaxHP);
            OnHPChanged?.Invoke(CurrentHP);
        }

        // ── MP ───────────────────────────────────────────────────────────────

        /// <summary>Reduce el MP. Ignora valores menores o iguales a 0.</summary>
        public void UseMP(float amount)
        {
            if (amount <= 0f) return;
            CurrentMP = Math.Clamp(CurrentMP - amount, 0f, MaxMP);
            OnMPChanged?.Invoke(CurrentMP);
        }

        /// <summary>Aumenta el MP. Ignora valores menores o iguales a 0.</summary>
        public void RestoreMP(float amount)
        {
            if (amount <= 0f) return;
            CurrentMP = Math.Clamp(CurrentMP + amount, 0f, MaxMP);
            OnMPChanged?.Invoke(CurrentMP);
        }
    }
}
```

- [ ] **Step 1.4 — Verificar que los tests pasan (manual Unity Editor)**

`Test Runner > EditMode > Run All`.
Resultado esperado: 18 tests PASS (7 de PlayerHealth + 11 nuevos de CharacterModelTests... esperar recompilación primero).
(Paso manual — documentar resultado en el reporte.)
