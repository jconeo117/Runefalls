# RUNEFALL — Memoria del Proyecto

> Este archivo es leído por Claude Code al inicio de cada sesión.
> Mantenlo actualizado. Es la única fuente de verdad del estado del proyecto.

---

## Stack y Motor

- **Motor:** Unity 6000.0.30f1 LTS, C#, URP
- **Networking:** Netcode for GameObjects / NGO (migrado de Mirror en Sprint 6 — co-op 2 jugadores)
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

**Sprint activo:** 6 🔄 EN CURSO — iniciado 2026-05-16 · 0/6 tareas
**Sprint anterior:** 5 ✅ COMPLETO — iniciado 2026-04-27 · cerrado 2026-05-15 · 8/8 tareas
**Sprints anteriores:** S1 ✅ · S2 ✅ · S3 ✅ · S4 ✅ (4.1 ✅ · 4.2 ✅ · 4.3 🔶 parcial · 4.4 ❌ → migrado a S5)
**Completadas S5:** 5.0 ✅ · 5.1 ✅ · 5.2 ✅ · 5.3 ✅ · 5.4 ✅ · 5.5 ✅ · 5.6 ✅ · 5.7 ✅
- 5.6: Ultimate Gauge — `CheckUltimateInsertion()`, `_ultimateGauge[actor]`, 7 orbs HUD, wired en Bootstrapper
- 5.5: CombatCameraDirector + deuda técnica arquitectónica completa (IEnemyPhaseAnimator, ICombatPresenter, CombatAnimationDriver split, SkillUsedPayload → dominio)
- 5.7: Sistema de efectos completo — EffectDefinition pipeline, ActorEffects + 11 EffectDefs
**Bonus S5 (2026-05-15):** Fix Animation Events (primary/safety-net), multi-hit support, Shoot event separado ranged, `CombatVFXPlayer` + `SkillVFXConfig` SO pipeline.
**S6 en curso:** 6.0 CombatArenaAssembler · 6.1 EncounterState Wiring · 6.2 SceneTransitionSystem · 6.3 EquipmentScreen · 6.4 BossRoomData · 6.5 NGO Groundwork

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
- **Sprint 4 (2026-04-27):** Runefall.asmdef movido de `Scripts/` a `_Project/` — cubre Scripts/ y ScriptableObjects/ en mismo assembly. Fix crítico para que `Runefall.Data` sea visible desde `Runefall.Combat`.
- **Sprint 4 (2026-04-27):** C# events (no GameEvent<T>) para dominio→presentación en combate. GameEvent<T> SOs siguen siendo el target de producción; el bridge MonoBehaviour se implementa en Tarea 4.4.
- **Sprint 4 (2026-04-27):** CardHand: HandSize = 3 + fieldCount + (hasBench?1:0); ActionsPerTurn = fieldCount; merge cascade por adyacencia misma-skill mismo-rank → rank+1 max 3.
- **Sprint 5 (2026-05-05):** Lunge placeholder en `LungePawn` coroutine (Transform.Lerp). Migración a Animator = swap solo de `LungePawn` + Animation Event bridge. Queue, grouping, timing hooks no cambian.
- **Sprint 5 (2026-05-05):** Camera orbit en combate: playerAnchor = Edit-Mode cam position; enemyAnchor = mirror XZ a través de fieldCenter preservando Y. TurnManager hooks: `EnemyPhaseRunner` inserta delay antes del turno enemigo.
- **Sprint 5 (2026-05-05):** Card order en mano: iteración reversa de playerTeam children — pawn más a la derecha en jerarquía → cartas más a la izquierda en mano.
- **Sprint 5 (2026-05-13):** EffectDefinition pipeline (strategy pattern SOs) reemplaza damageMultiplier legacy. CombatResolver mantiene fallback legacy para compatibilidad con assets existentes.
- **Sprint 5 (2026-05-13):** ArrebatoEffectDef usa GroupId + LinkedActor para expiración bidireccional. Ataque/Defensa = fracción multiplicativa del base propio; sub-stats = flat aditivo.
- **Sprint 5 (2026-05-13):** SkillUsedPayload movido a Runefall.Combat (dominio) — Data SO puede referenciar dominio sin violar capas. IEnemyPhaseAnimator: TurnManager recibe por constructor, pasa callbacks propios (executeTurn, onComplete) — animator nunca importa TurnManager. CombatBootstrapper reducido a Composition Root puro (~250 líneas). CombatAnimationDriver extrae toda la lógica de lunge/impact/queue. CombatCameraDirector suscribe a SkillUsedEvent SO: R1=Static, R2=PushIn (+2u forward), R3=DynamicOrbit (60°).
- **Sprint 6 (2026-05-13):** Networking migrado de Mirror a Netcode for GameObjects (NGO). Host = autoridad total sobre TurnManager. Clientes envían ServerRpc, reciben ClientRpc + NetworkVariable. Unity Relay (sin abrir puertos) = Sprint 7. Mazmorras son escenarios prediseñados — BSP descartado definitivamente.

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
| `Combat/ICombatActor.cs` | Interfaz unificada jugador/enemigo para combate — implementado ✅ |
| `Combat/CombatContext.cs` | Estado runtime del encuentro: jugadores, enemigos, turno — implementado ✅ |
| `Combat/CombatActionResult.cs` | Resultado readonly struct de una acción — implementado ✅ |
| `Combat/IEnemyTurnHandler.cs` | Interfaz para acción de enemigo, desacopla TurnManager — implementado ✅ |
| `Combat/CardHand.cs` | Mano de cartas: deal, merge cascade, refill, ultimate insert — implementado ✅ |
| `Combat/TurnManager.cs` | Árbitro de fases: PlayerTurn↔EnemyTurn, eventos C# — implementado ✅ |
| `Combat/PlayerActor.cs` | ICombatActor wrapping CharacterData — implementado ✅ |
| `Enemies/EnemyAgent.cs` | ICombatActor + IEnemyTurnHandler, BehaviorTree dispatch — implementado ✅ |
| `Presentation/Combat/CombatBootstrapper.cs` | Blockout: crea CharacterData/EnemyData runtime, arranca TurnManager — implementado ✅ |
| `Presentation/Combat/CombatBlockoutPresenter.cs` | UI blockout: cartas, HP, log, slots de acción — implementado ✅ |
| `Presentation/Combat/CombatCameraController.cs` | Orbit entre playerAnchor/enemyAnchor, lerp LateUpdate. playerAnchor=Edit-Mode pos, enemyAnchor=mirror XZ preservando Y — implementado ✅ |
| `Scenes/CombatBlockout.unity` | Escena blockout completa — implementado ✅ (verificar HP arrays en Inspector) |
| `Presentation/Combat/CombatIntroSequencer.cs` | Reveal enemy+player teams antes de StartCombat; custom Transform anchors Inspector; drop+settle anim — implementado ✅ |
| `Presentation/Combat/ImpactContext.cs` | Struct payload del ImpactEvent: attacker, target, damage, isCrit, AttackType, HitPosition — implementado ✅ |
| `ScriptableObjects/Combat/ImpactEvent.cs` | GameEvent\<ImpactContext\> SO — dispatch de impacto por hit; VFX/audio suscriben aquí — implementado ✅ |
| `Presentation/Combat/CombatCameraDirector.cs` | Director de cámara: suscribe SkillUsedEvent SO, R1=Static/R2=PushIn/R3=DynamicOrbit, restaura anchor de fase — implementado ✅ |
| `Combat/IEnemyPhaseAnimator.cs` | Interfaz DI: TurnManager pasa callbacks propios al animator, sin dependencia cruzada — implementado ✅ |
| `Presentation/Combat/ICombatPresenter.cs` | Contrato del presentador — Bootstrapper depende de interfaz, no de clase concreta — implementado ✅ |
| `Combat/SkillUsedPayload.cs` | Payload en dominio (movido de Presentation) — SkillUsedEvent SO legal — implementado ✅ |
| `Presentation/Combat/CombatAnimationDriver.cs` | Toda la animación de combate: lunge, impacto, damage numbers, fase enemigo animada — implementado ✅ |
| `Presentation/Combat/HPBarPresenter.cs` | HP bar world-space billboard por pawn, suscrita a OnHPChanged — implementado ✅ |
| `Presentation/Combat/CombatVFXPlayer.cs` | Suscrito a ImpactEvent SO → onImpactVFX; llamado por CombatAnimationDriver → onStartVFX — implementado ✅ |
| `ScriptableObjects/Combat/SkillVFXConfig.cs` | SO con onStartVFX + onImpactVFX prefabs, offsets, autoDestroyAfter — implementado ✅ |
| `Presentation/Combat/CharacterSlot.cs` | MonoBehaviour slot jugador: CharacterData + hpBarOffset — implementado ✅ |
| `Presentation/Combat/EnemySlot.cs` | MonoBehaviour slot enemigo: EnemyData + hpBarOffset — implementado ✅ |
