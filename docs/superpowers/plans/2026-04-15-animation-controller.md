# CharacterAnimationController — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** MonoBehaviour que traduce `PlayerController.CurrentSpeed` a parámetros de Animator, con API pública lista para combate futuro.

**Architecture:** `CharacterAnimationController` vive en `Presentation/Player/`. Lee `CurrentSpeed` de `PlayerController` (propiedad pública nueva). Hashes precalculados. Null-guards deshabilitan el componente si faltan referencias. Sin lógica de juego. Sin tests automáticos — `Animator` requiere PlayMode.

**Tech Stack:** Unity 6 / C# / UnityEngine.Animator / `Runefall.Characters.CharacterModel` (referencia futura, Sprint 3).

---

## Archivos

| Acción | Ruta |
|--------|------|
| Modificar | `Assets/_Project/Scripts/Presentation/Player/PlayerController.cs` |
| Crear | `Assets/_Project/Scripts/Presentation/Player/CharacterAnimationController.cs` |

---

## Task 1: Exponer CurrentSpeed en PlayerController

**Files:**
- Modify: `Assets/_Project/Scripts/Presentation/Player/PlayerController.cs`

`currentSpeed` es `private float`. `CharacterAnimationController` necesita leerlo. Agregar propiedad pública de solo lectura.

- [ ] **Step 1.1 — Agregar propiedad CurrentSpeed**

En `PlayerController.cs`, después del bloque de campos privados (línea ~31, después de `private Vector3 velocity;`), agregar:

```csharp
        // ── API pública ──────────────────────────────────────────────────────
        /// <summary>Velocidad actual de movimiento. Leída por CharacterAnimationController.</summary>
        public float CurrentSpeed => currentSpeed;
```

El archivo completo en esa sección queda:

```csharp
        // ── Estado interno ───────────────────────────────────────────────────
        private CharacterController cc;
        private Transform cachedCamTransform;
        private Vector3 moveDirection;   // dirección normalizada en world-space
        private float   currentSpeed;    // velocidad actual (con aceleración)
        private Vector3 velocity;        // acumulado de gravedad

        // ── API pública ──────────────────────────────────────────────────────
        /// <summary>Velocidad actual de movimiento. Leída por CharacterAnimationController.</summary>
        public float CurrentSpeed => currentSpeed;
```

- [ ] **Step 1.2 — Verificar que no hay errores de compilación**

Abrir Unity Editor y esperar recompilación. Console no debe mostrar errores relacionados con `PlayerController`.

---

## Task 2: CharacterAnimationController

**Files:**
- Create: `Assets/_Project/Scripts/Presentation/Player/CharacterAnimationController.cs`

- [ ] **Step 2.1 — Crear el archivo**

Crear `Assets/_Project/Scripts/Presentation/Player/CharacterAnimationController.cs`:

```csharp
using UnityEngine;
using Runefall.Characters;

namespace Runefall.Presentation.Player
{
    /// <summary>
    /// Traduce estado de movimiento a parámetros del Animator.
    /// No sabe de lógica de juego — recibe comandos simples.
    /// Asignar Animator e PlayerController en el inspector.
    /// </summary>
    public class CharacterAnimationController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Animator         animator;
        [SerializeField] private PlayerController playerController;

        // ── Hashes precalculados (más rápido que strings en runtime) ─────────
        private static readonly int SpeedHash  = Animator.StringToHash("Speed");
        private static readonly int DodgeHash  = Animator.StringToHash("Dodge");
        private static readonly int AttackHash = Animator.StringToHash("AttackIndex");

        // ── Ciclo de vida ────────────────────────────────────────────────────

        private void Awake()
        {
            if (animator == null)
            {
                Debug.LogError("[CharacterAnimationController] Animator no asignado.", this);
                enabled = false;
                return;
            }
            if (playerController == null)
            {
                Debug.LogError("[CharacterAnimationController] PlayerController no asignado.", this);
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            // Damping 0.1s para transición suave entre estados
            animator.SetFloat(SpeedHash, playerController.CurrentSpeed, 0.1f, Time.deltaTime);
        }

        // ── API pública (sistemas de combate, Sprint 3+) ─────────────────────

        /// <summary>Dispara animación de dodge.</summary>
        public void PlayDodge() =>
            animator.SetTrigger(DodgeHash);

        /// <summary>Dispara animación de skill por índice (0=skill1, 1=skill2).</summary>
        public void PlaySkill(int skillIndex) =>
            animator.SetTrigger(AttackHash + skillIndex);

        /// <summary>
        /// Conecta eventos del modelo de personaje.
        /// Sprint 3: model.OnStatusApplied += HandleStatus;
        /// </summary>
        public void Init(CharacterModel model)
        {
            // Sprint 3 — descomentar cuando CharacterModel tenga OnStatusApplied:
            // model.OnStatusApplied += HandleStatus;
        }
    }
}
```

- [ ] **Step 2.2 — Verificar compilación**

Abrir Unity Editor, esperar recompilación. Sin errores en Console.
`CharacterAnimationController` debe aparecer en `Add Component`.

- [ ] **Step 2.3 — Verificación PlayMode (manual)**

En la cápsula de la escena:
1. `Add Component > CharacterAnimationController`
2. Asignar `Animator` (si existe en el GO o hijo) y `PlayerController`
3. Si no hay Animator real: dejar vacío — debe aparecer error `[CharacterAnimationController] Animator no asignado.` en Console y el componente se deshabilita. Comportamiento correcto.
4. Press Play — `PlayerController` sigue funcionando normalmente (componente independiente).
