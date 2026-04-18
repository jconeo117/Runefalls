# PlayerController + PlayerHealth — Design Spec
**Fecha:** 2026-04-15
**Sprint:** 2 — Tarea 2.2
**Estado:** Aprobado

---

## Alcance

Dos MonoBehaviours en `Presentation/Player/`. Sin `CharacterModel` todavía — el HP vive en `PlayerHealth` como componente separado por SRP. Sin dodge, sin muerte, sin eventos. Solo movimiento base y almacenamiento de HP.

---

## Componentes

### `PlayerHealth`
- **Capa:** `Presentation/Player/`
- **Responsabilidad única:** almacenar y modificar el HP del jugador
- Campos `[SerializeField]`: `maxHP` (float, default 100), `currentHP` (float, inicializado a `maxHP` en `Awake`)
- Métodos públicos: `TakeDamage(float amount)`, `Heal(float amount)` — ambos con `Mathf.Clamp`
- Sin eventos, sin lógica de muerte, sin referencia a otros sistemas
- Propiedad pública `IsAlive => currentHP > 0`

### `PlayerController`
- **Capa:** `Presentation/Player/`
- `[RequireComponent(typeof(CharacterController))]`
- **Dependencias por inspector:** `InputReader input` (SO)
- **Movimiento:**
  - Lee `input.MoveInput` por polling en `Update`
  - Dirección convertida a espacio de cámara: `Camera.main.transform.forward` proyectado en plano horizontal + `Camera.main.transform.right`
  - Velocidad con `Mathf.MoveTowards` (aceleración cuando sube, deceleración cuando baja)
  - `CharacterController.Move()` aplica movimiento + gravedad acumulada
- **Rotación:** `Quaternion.Slerp` hacia `moveDirection` cuando hay input (`sqrMagnitude > 0.01f`)
- **Gravedad:** acumulada en `Vector3 velocity.y`, reseteada cuando `cc.isGrounded`
- **Campos `[SerializeField]`:** `moveSpeed`, `rotationSpeed`, `acceleration`, `deceleration`
- Sin referencia a `PlayerHealth` — son componentes independientes

---

## Lo que NO incluye este diseño

- Dodge / dash
- Animaciones
- Eventos de movimiento
- Lógica de muerte
- `CharacterModel` — se conectará en Sprint 2 Tarea 2.3 y posterior

---

## Entregable verificable

Cápsula moviéndose con aceleración/deceleración suave, rotando hacia la dirección de movimiento, cámara de tercera persona sin clip. `PlayerHealth` inspectable con valores de HP visibles en el Inspector.
