# PlayerController + PlayerHealth — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cápsula moviéndose con aceleración/deceleración suave, rotate-to-move relativo a cámara, HP inspectable.

**Architecture:** Dos MonoBehaviours independientes en `Presentation/Player/`. `PlayerHealth` almacena HP sin lógica extra. `PlayerController` lee `InputReader` SO, mueve al personaje vía `CharacterController`, aplica gravedad manual. Sin `CharacterModel` por ahora.

**Tech Stack:** Unity 6 / C# / Unity New Input System (`InputReader` SO ya existe) / `CharacterController` Unity built-in / NUnit + Unity Test Runner (EditMode).

---

## Archivos

| Acción | Ruta |
|--------|------|
| Crear | `Assets/_Project/Scripts/Presentation/Player/PlayerHealth.cs` |
| Crear | `Assets/_Project/Scripts/Presentation/Player/PlayerController.cs` |
| Crear | `Assets/_Project/Tests/EditMode/PlayerHealthTests.cs` |
| Crear | `Assets/_Project/Tests/EditMode/Runefall.Tests.EditMode.asmdef` |

---

## Task 1: PlayerHealth + EditMode tests

**Files:**
- Create: `Assets/_Project/Scripts/Presentation/Player/PlayerHealth.cs`
- Create: `Assets/_Project/Tests/EditMode/Runefall.Tests.EditMode.asmdef`
- Create: `Assets/_Project/Tests/EditMode/PlayerHealthTests.cs`

- [ ] **Step 1.1 — Crear la Assembly Definition para tests EditMode**

Crear `Assets/_Project/Tests/EditMode/Runefall.Tests.EditMode.asmdef` con este contenido:

```json
{
    "name": "Runefall.Tests.EditMode",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 1.2 — Escribir los tests que deben fallar**

Crear `Assets/_Project/Tests/EditMode/PlayerHealthTests.cs`:

```csharp
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
```

- [ ] **Step 1.3 — Abrir Test Runner y verificar que los tests fallan**

`Window > General > Test Runner > EditMode`.
Deben aparecer los 7 tests con error de compilación o `TypeNotFound` porque `PlayerHealth` no existe aún.

- [ ] **Step 1.4 — Implementar PlayerHealth**

Crear `Assets/_Project/Scripts/Presentation/Player/PlayerHealth.cs`:

```csharp
using UnityEngine;

namespace Runefall.Presentation.Player
{
    /// <summary>
    /// Almacena y modifica el HP del jugador.
    /// No dispara eventos ni tiene lógica de muerte — eso va en CharacterModel (Sprint 2.3+).
    /// Asignar en el mismo GameObject que PlayerController.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Salud")]
        [SerializeField] private float maxHP = 100f;

        private float currentHP;

        public float CurrentHP => currentHP;
        public float MaxHP     => maxHP;
        public bool  IsAlive   => currentHP > 0f;

        private void Awake()
        {
            currentHP = maxHP;
        }

        /// <summary>Reduce el HP. No baja de 0.</summary>
        public void TakeDamage(float amount)
        {
            currentHP = Mathf.Clamp(currentHP - amount, 0f, maxHP);
        }

        /// <summary>Aumenta el HP. No supera maxHP.</summary>
        public void Heal(float amount)
        {
            currentHP = Mathf.Clamp(currentHP + amount, 0f, maxHP);
        }
    }
}
```

- [ ] **Step 1.5 — Correr los tests y verificar que pasan**

`Test Runner > EditMode > Run All`.
Resultado esperado: 7 tests PASS.

---

## Task 2: PlayerController

**Files:**
- Create: `Assets/_Project/Scripts/Presentation/Player/PlayerController.cs`

> `PlayerController` mueve al personaje. No tiene tests automáticos — la verificación es PlayMode manual (el CharacterController depende de la física de Unity que no funciona en EditMode sin scene completa).

- [ ] **Step 2.1 — Implementar PlayerController**

Crear `Assets/_Project/Scripts/Presentation/Player/PlayerController.cs`:

```csharp
using UnityEngine;
using Runefall.Core;

namespace Runefall.Presentation.Player
{
    /// <summary>
    /// Mueve al jugador con aceleración/deceleración suave.
    /// El personaje rota hacia donde se mueve; la cámara es independiente.
    /// Asignar InputReader SO en el inspector.
    /// Requiere CharacterController en el mismo GameObject.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Referencia de input")]
        [SerializeField] private InputReader input;

        [Header("Movimiento")]
        [SerializeField] private float moveSpeed      = 5f;
        [SerializeField] private float acceleration   = 8f;
        [SerializeField] private float deceleration   = 12f;

        [Header("Rotación")]
        [SerializeField] private float rotationSpeed  = 10f;

        // ── Estado interno ───────────────────────────────────────────────────
        private CharacterController cc;
        private Vector3 moveDirection;   // dirección normalizada en world-space
        private float   currentSpeed;    // velocidad actual (con aceleración)
        private Vector3 velocity;        // acumulado de gravedad

        // ── Ciclo de vida ────────────────────────────────────────────────────

        private void Awake()
        {
            cc = GetComponent<CharacterController>();

            if (input == null)
                Debug.LogError("[PlayerController] InputReader no asignado.", this);
        }

        private void Update()
        {
            ApplyGravity();
            HandleMovement();
            HandleRotation();
        }

        // ── Lógica de movimiento ─────────────────────────────────────────────

        private void HandleMovement()
        {
            // Dirección de input en world-space relativa a la cámara
            Transform cam     = Camera.main.transform;
            Vector3 camFwd    = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 camRight  = cam.right;

            Vector3 inputDir  = camFwd * input.MoveInput.y + camRight * input.MoveInput.x;

            // Normalizar solo si la magnitud supera 1 (evita normalizar el vector cero)
            if (inputDir.sqrMagnitude > 1f)
                inputDir.Normalize();

            moveDirection = inputDir;

            // Aceleración suave hacia la velocidad objetivo
            float targetSpeed = inputDir.sqrMagnitude > 0.01f ? moveSpeed : 0f;
            float accel       = targetSpeed > currentSpeed ? acceleration : deceleration;
            currentSpeed      = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.deltaTime);

            // Mover: traslación + gravedad acumulada
            cc.Move(moveDirection * currentSpeed * Time.deltaTime + velocity * Time.deltaTime);
        }

        private void HandleRotation()
        {
            if (moveDirection.sqrMagnitude < 0.01f) return;

            Quaternion targetRot = Quaternion.LookRotation(moveDirection);
            transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        private void ApplyGravity()
        {
            if (cc.isGrounded && velocity.y < 0f)
                velocity.y = -2f;   // fuerza constante para mantener contacto con el suelo

            velocity.y += Physics.gravity.y * Time.deltaTime;
        }
    }
}
```

---

## Task 3: Wiring en escena Unity

> Pasos en el editor de Unity. No se escribe código aquí.

- [ ] **Step 3.1 — Verificar que el Player GameObject tiene CharacterController**

En la escena (`SampleScene`), seleccionar el GameObject del jugador (el que tiene `CameraManager` o el capsule).
Si no tiene `CharacterController`, agregar: `Add Component > CharacterController`.
Valores sugeridos: `Height = 2`, `Radius = 0.5`, `Center = (0, 1, 0)`.

- [ ] **Step 3.2 — Agregar PlayerController al GameObject**

`Add Component > PlayerController`.
En el inspector, arrastrar el `InputReader` SO (en `Assets/_Project/ScriptableObjects/` o donde fue guardado) al campo `Input`.

- [ ] **Step 3.3 — Agregar PlayerHealth al GameObject**

`Add Component > PlayerHealth`.
Verificar que `Max HP = 100` aparece en el inspector.

- [ ] **Step 3.4 — Verificar que el suelo existe**

Debe haber un plano o terreno con collider en la escena para que `cc.isGrounded` funcione.
Si no existe: `GameObject > 3D Object > Plane`. Escalar a `(10, 1, 10)`. Posicionar en `(0, 0, 0)`.

- [ ] **Step 3.5 — PlayMode: verificar movimiento**

Presionar Play. Con WASD / stick izquierdo:
- El personaje debe acelerar suavemente al mover el stick.
- Al soltar, debe desacelerar (no detenerse instantáneamente).
- El personaje debe rotar hacia donde se mueve.
- La cámara debe moverse independientemente del personaje.
- El personaje no debe atravesar el suelo.

Si el personaje no se mueve: verificar que `InputReader` está asignado y que el Input System está activo.
Si se cae indefinidamente: verificar que hay suelo con collider y que `CharacterController.Center.y ≈ 1`.

---

## Verificación final

| Check | Método |
|-------|--------|
| 7 tests PlayerHealth pasan | Test Runner > EditMode > Run All |
| Movimiento con aceleración | PlayMode manual — WASD |
| Deceleración al soltar | PlayMode manual — soltar WASD |
| Rotate-to-move | PlayMode manual — cambiar dirección |
| HP visible en inspector | Seleccionar GO en Play mode |
| No atraviesa el suelo | PlayMode — gravedad activa |
