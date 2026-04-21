# RUNEFALL — Memoria del Proyecto

> Este archivo es leído por Claude Code al inicio de cada sesión.
> Mantenlo actualizado. Es la única fuente de verdad del estado del proyecto.

---

## Stack y Motor

- **Motor:** Unity 6000.0.30f1 LTS, C#, URP
- **Networking:** Mirror Networking (co-op 2 jugadores)
- **Cámara:** Cinemachine 3.x — dos modos:
  - **Exploración:** orbital libre, jugador controla ángulo con mouse/stick derecho. `CinemachineOrbitalFollow`.
  - **Combate:** estática detrás del hombro apuntando al enemigo. No se mueve durante turnos. `CinemachineFollow` + `CinemachineHardLookAt`. Blend 0.5s al entrar, 0.3s al salir.
  - Namespace Unity 6: `using Unity.Cinemachine;` — no `using Cinemachine;`
- **Input:** Unity New Input System (incluido por defecto en Unity 6)
- **UI:** UI Toolkit (preferido en Unity 6) o TextMeshPro con uGUI
- **Persistencia:** JSON local + PlayerPrefs (sin backend)
- **MCP Unity:** `com.unity.ai.assistant` disponible nativamente en Unity 6 — configurar en `Edit > Project Settings > AI > Unity MCP`

### Notas de compatibilidad Unity 6 — leer antes de escribir código de presentación

- `Object.FindObjectOfType` está **obsoleto** en Unity 6 → usar `Object.FindFirstObjectByType` si es absolutamente necesario en `Presentation/` (sigue prohibido en dominio)
- `GameObject.Find` sigue prohibido en todas las capas
- Render Graph es el pipeline por defecto en URP Unity 6 → no usar `CommandBuffer` legacy
- `UnityEngine.Random` disponible, pero en clases de dominio puro usar `System.Random` para evitar dependencia de UnityEngine
- Cinemachine 3.x tiene namespace `Unity.Cinemachine` en lugar de `Cinemachine`
- Physics API igual: `Physics.OverlapSphere`, `Physics.Raycast`, etc. sin cambios
- Assembly Definitions igual que versiones anteriores

---

## Arquitectura — LEER ANTES DE ESCRIBIR CÓDIGO

### Tres capas, separación estricta

```
Assets/_Project/Scripts/
├── Core/           → ServiceLocator, GameEvent<T>, interfaces globales
├── Characters/     → CharacterModel, CharacterStats — C# puro, sin MonoBehaviour
├── Combat/         → TurnManager, CardSystem, CombatResolver — C# puro
├── Enemies/        → EnemyModel, BehaviorTree, AIDirector — C# puro
├── Dungeon/        → BSPSplitter, DungeonGenerator, RoomManager — C# puro
├── Gacha/          → GachaSystem, PityTracker, LocalSaveSystem — C# puro
├── Resonance/      → ResonanceDetector — C# puro
└── Presentation/   → MonoBehaviours ÚNICAMENTE. Solo muestran, no deciden.
    ├── Player/     → PlayerController, CameraManager
    ├── UI/         → HUDPresenter, CardHandView, etc.
    └── Enemies/    → EnemyAnimationController, etc.

Assets/_Project/ScriptableObjects/
├── Characters/     → CharacterData.asset (por personaje)
├── Weapons/        → WeaponData.asset (por arma)
├── Cards/          → SkillData.asset, RuneData.asset, UltimateData.asset
├── Enemies/        → EnemyData.asset (por tipo de enemigo)
├── Events/         → GameEvent<T>.asset (eventos tipados)
└── Resonance/      → ResonanceSetData.asset
```

### Reglas de arquitectura (se verifican automáticamente)

1. **Las clases en `Core/`, `Combat/`, `Characters/`, `Enemies/`, `Dungeon/`, `Gacha/`, `Resonance/` NO heredan de MonoBehaviour jamás.**
2. **Los sistemas se comunican con `GameEvent<T>` ScriptableObjects, no con referencias directas.**
3. **`ServiceLocator.cs` es el único registro global. No hay Singletons.**
4. **`FindObjectOfType`, `FindFirstObjectByType` y `GameObject.Find` están prohibidos en dominio.**
5. **Los `MonoBehaviours` están en `Presentation/` y solo reciben referencias al dominio, nunca al revés.**
6. **Namespace de Cinemachine en Unity 6: `using Unity.Cinemachine;` — no `using Cinemachine;`**

### Convenciones de nombre

| Qué | Convención | Ejemplo |
|---|---|---|
| Interfaces | Prefijo `I` | `ICombatSystem` |
| Eventos | Sufijo `Event` | `OnHPChangedEvent` |
| ScriptableObjects | Sufijo `Data` | `CharacterData` |
| Presentadores | Sufijo `Presenter` | `CombatHUDPresenter` |

---

## Comandos disponibles (Skills)

| Comando | Cuándo usarlo |
|---|---|
| `/sprint-start N` | Al comenzar una nueva semana/sprint |
| `/task-done X.Y` | Al terminar una tarea, antes de pasar a la siguiente |
| `/audit` | Para revisar la salud del código en cualquier momento |
| `/session-end` | SIEMPRE antes de cerrar Claude Code |
| `/new-class NombreClase tipo` | Para crear cualquier clase nueva |

---

## Estado actual del sprint

**Sprint activo:** 4 🔄 EN CURSO — iniciado 2026-04-20 · 0/4 tareas completadas
**Sprints anteriores:** S1 ✅ · S2 ✅ · S3 ✅
**Próximo paso:** Tarea 4.1 — TurnManager (dominio puro, `Combat/TurnManager.cs`)

---

## Decisiones técnicas tomadas

- **Abril 2026:** Unity 6000.0.30f1 LTS seleccionado. MCP Unity nativo disponible.
- **Abril 2026:** Visión de combate confirmada — exploración 3D tercera persona + transición a pantalla de combate 2.5D lateral estilo 7DS Grand Cross.
- **Abril 2026:** MVP con 1 personaje activo en combate. Equipos múltiples (3-4) son post-MVP.
- **Abril 2026:** Enemigos reaparecen al volver a la sala, no son permanentes.
- **Abril 2026:** Assembly Definitions no configurados — deuda técnica aceptada, no bloquea MVP.
- **Abril 2026:** Secuencias de cámara por rango de carta — `CameraSequenceData` SO asignado en cada `SkillEffect`. El dominio levanta `SkillUsedEvent`, el `CombatCameraDirector` (Presentation) lo escucha y ejecuta la secuencia. Rango 1 = Static, Rango 2 = PushIn, Rango 3 = DynamicOrbit.
- **Sprint 2:** Salto y Dash no implementados — se completan en Tarea 3.0 al inicio del Sprint 3.
- **Abril 2026:** Pool de Runas aumentado a 16 (era 10) — decisión final.
- **Sprint 1:** Cámara implementada con `cam_exploration` (OrbitalFollow), `cam_combat` (Follow), `cam_lookon` (LookAt). Namespace `Unity.Cinemachine`.

---

## Archivos clave del proyecto

| Archivo | Qué hace |
|---|---|
| `Core/ServiceLocator.cs` | Registro de servicios — implementado ✅ |
| `Core/GameEvent.cs` | Sistema de eventos tipados — implementado ✅ |
| `Core/InputReader.cs` | Input SO desacoplado — implementado ✅ |
| `Core/Events/SkillUsedEvent.cs` | Evento de skill usada: carta + actor + target — pendiente S4 |
| `Presentation/Player/CameraManager.cs` | 3 cámaras virtuales Cinemachine — implementado ✅ |
| `Presentation/Player/PlayerController.cs` | Movimiento + salto + dash — implementado ✅ |
| `Presentation/Combat/CombatCameraDirector.cs` | Secuencias de cámara por rango de carta — pendiente S4 |
| `Characters/CharacterModel.cs` | Modelo de dominio del personaje — implementado ✅ |
| `Presentation/CharacterAnimationController.cs` | Animator desacoplado — implementado ✅ |
| `ScriptableObjects/Combat/CameraSequenceData.cs` | SO de secuencia de cámara por skill/rango — implementado ✅ |
| `ScriptableObjects/Characters/CharacterData.cs` | SO de personaje: stats, skills, gacha — implementado ✅ |
| `ScriptableObjects/Cards/SkillData.cs` | SO de habilidad con efectos por rango — implementado ✅ |
| `ScriptableObjects/Weapons/WeaponData.cs` | SO de arma con stats y bonus condicional — implementado ✅ |
| `ScriptableObjects/Cards/RuneData.cs` | SO de runa con efecto y slot cost — implementado ✅ |
| `ScriptableObjects/Enemies/EnemyData.cs` | SO de enemigo: stats, AI, loot, detectionRange — implementado ✅ |
| `Characters/CharacterStats.cs` | Stats + StatModifier + operador + — implementado ✅ |
| `Core/EncounterState.cs` | Estado compartido exploración→combate, registrado en ServiceLocator — implementado ✅ |
| `Combat/CombatFormulas.cs` | Fórmula de daño + tabla elemental estática — implementado ✅ |
| `ScriptableObjects/Combat/EncounterData.cs` | Datos de encuentro runtime (enemigo, nivel, transform) — implementado ✅ |
