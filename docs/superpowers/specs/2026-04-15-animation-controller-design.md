# CharacterAnimationController — Design Spec
**Fecha:** 2026-04-15
**Sprint:** 2 — Tarea 2.3
**Estado:** Aprobado

## Alcance

MonoBehaviour en `Presentation/Player/` que traduce estado de movimiento a parámetros del Animator. No sabe de lógica de juego. Listo para wiring cuando llegue rig y animaciones reales.

## Cambios

| Archivo | Acción |
|---------|--------|
| `Presentation/Player/CharacterAnimationController.cs` | Crear |
| `Presentation/Player/PlayerController.cs` | Modificar — exponer `CurrentSpeed` |

## CharacterAnimationController

- `[SerializeField] private Animator animator` — puede estar en hijo del GO
- `[SerializeField] private PlayerController playerController`
- Hashes precalculados: `Speed`, `Dodge`, `AttackIndex`
- `Awake`: null-guard — deshabilita componente si falta animator o playerController
- `Update`: `animator.SetFloat(SpeedHash, playerController.CurrentSpeed, 0.1f, Time.deltaTime)`
- `PlayDodge()`: dispara trigger Dodge
- `PlaySkill(int skillIndex)`: dispara trigger AttackIndex + skillIndex
- `Init(CharacterModel model)`: vacío, comentado "Sprint 3 — suscribir OnStatusApplied"

## PlayerController — cambio mínimo

Agregar propiedad pública de solo lectura:
```csharp
public float CurrentSpeed => currentSpeed;
```

## Lo que NO incluye

- Tests automáticos (Animator requiere PlayMode)
- Conexión real a CharacterModel (Sprint 3)
- Lógica de combate o estado de animación
