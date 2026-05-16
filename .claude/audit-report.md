# Audit Report — 2026-05-09
**Sprint activo:** 5 | **Tareas completadas:** 3/8

## Resumen ejecutivo

🟢 Separación de capas intacta. 23 archivos de dominio sin MonoBehaviour ni referencias a Unity Engine ilegales. 14 de presentación correctamente aislados, 12 SOs de datos puros. Sin TODOs inline. Dos advertencias de acoplamiento conocido. Codebase aprobado para merge.

## Violaciones Críticas (bloquean merge)

*Ninguna.*

## Advertencias (revisar antes del siguiente sprint)

- `Presentation/Combat/CombatBootstrapper.cs` (~630 líneas) — `_tm.EnemyPhaseRunner = () => StartCoroutine(...)` asigna delegate de Presentation al TurnManager (dominio). Dependencia inversa aceptada en Unity por decisión S4, pero el archivo ya superó un tamaño razonable.
- `Presentation/Player/PlayerController.cs` — `Camera.main` cacheado en Awake. Falla silenciosa si la cámara se instancia dinámicamente.

## Acoplamiento detectado

- `TurnManager` expone `EnemyPhaseRunner` (Action) y `OnPlayerActionsExhausted` (event) como hooks para Presentation — patrón aceptado por decisión técnica S4. No es error pero es el único punto donde dominio "espera" suscriptores de Presentation.
- `CombatBootstrapper` conoce directamente `CombatHUDPresenter`, `CombatCameraController`, `CharacterSlot`, `EnemySlot` — correcto para bootstrapper, pero concentra bootstrapping + animación + HP bars + markers + cámara en un solo archivo.

## Deuda técnica (TODO/HACK/FIXME)

- `Combat/TurnManager.cs:279` — `CheckUltimateInsertion()` vacío — gauge system pendiente S5/S6
- `Combat/TurnManager.cs` comentario — bridge `GameEvent<T>` pendiente desde S4
- `Presentation/Combat/CombatBootstrapper.cs:17` — producción usa ServiceLocator (blockout pendiente de migrar)

## Métricas

- Archivos de dominio: 23
- Archivos de presentación: 14
- Archivos de ScriptableObjects: 12
- Archivos con violaciones críticas: 0
- `FindObjectOfType` / `GameObject.Find` en dominio: 0
- `MonoBehaviour` en dominio: 0
- Cobertura de interfaces: 2/2 sistemas (`ICombatActor` ✅, `IEnemyTurnHandler` ✅)
- TODOs inline en dominio: 2 (ambos documentados en CLAUDE.md)

## Recomendaciones

1. **Dividir `CombatBootstrapper`** — extraer `CombatAnimationDriver` (lunge/pawn) y `CombatUIBinder` (HP bars, markers) como MonoBehaviours separados. El archivo ya es el más acoplado del proyecto y seguirá creciendo con S5.3.
2. **Agregar `ICombatPresenter`** — `CombatPresenterBase` es abstracta pero sin interfaz formal. Extraer interfaz para que el Bootstrapper dependa de contrato, no de clase base concreta.
3. **Implementar bridge `GameEvent<T>` para TurnManager** — deuda pendiente desde S4. Desacopla definitivamente Presentation del dominio y permite tests sin escena.
