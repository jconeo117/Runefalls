# Sprint 6 activo
**Semana:** 18–24 Mayo 2026
**Objetivo:** Loop de combate jugable y satisfactorio + groundwork co-op NGO.
**Iniciado:** 2026-05-16

## Tareas descartadas MVP
- [~] 6.2-original SceneTransitionSystem — combate in-scene, sin cambio de escena. Reservada para hub→mazmorra (post-MVP).
- [~] 6.3-original EquipmentScreen — no entra en flujo exploración→combate MVP.

## Tareas

- [x] Tarea 6.0 — CombatArenaAssembler: Spawna pawns desde EncounterState antes de arrancar CombatBootstrapper. ✓
- [x] Tarea 6.1 — EncounterState Wiring: EnemyEncounterTrigger → EncounterState en ServiceLocator. ✓

- [ ] Tarea 6.2 — Fix animaciones + loop post-combate (bloquea experiencia)
  - Asignar `CombatBase.controller` a `CombatAnimationDriver.combatBaseController` en Inspector
  - Victory screen mínima: popup "Volver a explorar" + resumen (enemigo derrotado)
  - Defeat screen mínima: popup "Derrota" + botón retry (re-activa CombatSetup sin recargar escena)
  - Enemy respawn: `ExitCombatMode` llama `EnemyEncounterTrigger.ResetTrigger()` + re-activa GO enemigo

- [ ] Tarea 6.3 — BossRoomData + BossPhaseController (necesario para loop jefe)
  - `BossRoomData` SO: lista de `BossPhase` (hpThreshold float, animatorTrigger string, specialMove SkillData)
  - `BossPhaseController` MonoBehaviour: suscribe a `OnHPChanged` del EnemyAgent; detecta threshold cruzado; levanta evento de fase; dispara animator trigger
  - `EnemyAgent` expone `OnPhaseChanged` event para que BossPhaseController pueda reaccionar
  - Fase 2: enemy gana nueva skill / modifier de stat vía EffectDefinition

- [ ] Tarea 6.4 — NGO Groundwork: co-op turnos simultáneos (Opción B confirmada)
  **Topología:** Host autoridad total. Hub estático (sin exploración ni movimiento) → boss directo a combate.
  **TurnManager refactor (backward-compatible solitario):**
  - Añadir `TurnPhase { Planning, Resolution, Enemy }` — en solitario Planning cierra al agotar acciones, sin timer
  - `CardHand` por actor: `Dictionary<ICombatActor, CardHand> _hands` — solitario = 1 entrada, sin impacto
  - Cola combinada durante planning: `List<(ICombatActor actor, int cardIdx, ICombatActor target, long timestamp)> _planningQueue`
  - Orden de resolución por timestamp de llegada al host (natural para co-op, sin lógica extra)
  - Condición cierre planning: timer expira OR todos los `PlayerActor` vivos agotaron `ActionsPerTurn`
  **NGO:**
  - Instalar `com.unity.netcode.gameobjects`; remover Mirror de manifest.json
  - `NetworkedTurnManager` (NetworkBehaviour en Presentation): wrappea TurnManager del dominio; host es única instancia que corre TurnManager
  - `[ServerRpc] QueueCardServerRpc(int cardIdx, ulong targetNetId, ServerRpcParams rpc)` — host encola con timestamp UTC
  - `[ClientRpc] OnActionResolvedClientRpc(CombatActionResultDto)` — ambos clientes actualizan UI y lanzan animaciones locales
  - `NetworkVariable<int>` para HP de cada actor (sync automático a late-joiners)
  - `NetworkVariable<int> _activeTurnOwner` — índice del actor con turno activo
  **ActionTimerSystem (30s compartido):**
  - Timer arranca en host al inicio de PlanningPhase; `NetworkVariable<float> _planningTimeRemaining` visible en ambos HUD
  - Al expirar: host ejecuta acción automática por cada jugador que no agotó acciones (primera carta vs primer enemigo vivo)
  - En solitario: timer deshabilitado (`_timerEnabled = false` cuando `NetworkManager` no activo)
  **Hub scene:**
  - Lobby estático: pawns de ambos jugadores en pantalla, botón "Listo" individual, arranca cuando ambos ready
  - Unity Relay para conexión sin abrir puertos — link directo (sin matchmaking)

- [ ] Tarea 6.5 — Pulidos y deuda técnica sesión 6.1
  - Desactivar renderer cápsula jugador durante combate
  - Remover `ThirdPersonCamera` component de Main Camera (input doble)
  - Remover Debug.Log Steps y try/catch de diagnóstico de CombatBootstrapper
  - `introEnemyAnchor` / `introPlayerAnchor` — crear GOs y asignar en Inspector

## Entregable verificable
Combate con boss de 2 fases funcional. Victory/Defeat screens. Enemy respawn. Dos clientes conectados via Relay completan un combate de jefe; HP en sync; timer de acción auto.

## Notas de sesión

### Sesión 2026-05-22 — Exploración visual + datos + diseño pasivas
**Tareas trabajadas:** Groundwork exploración (visuals, animación, data binding) + diseño de sistemas futuros (pasivas, boss)
**Estado:** En progreso — exploración visual completa; 6.2 victory/defeat screens pendiente
**Archivos modificados:**
- `Scripts/Presentation/Player/PlayerController.cs` — 3-charge dash (List<float> timers independientes), removido jump, stop instantáneo al soltar tecla (`targetSpeed==0 → _currentSpeed=0`)
- `Scripts/Presentation/Player/PlayerAnimationDriver.cs` — NUEVO: lee `CurrentSpeed`/`IsDashing`, setea params Animator cada frame
- `Scripts/Presentation/Player/ExplorationPlayer.cs` — NUEVO: MonoBehaviour en Player GO con `CharacterData[] _party`
- `Scripts/Presentation/Enemies/EnemyAnimationDriver.cs` — NUEVO: lee `NavMeshAgent.velocity.magnitude` → param Speed
- `Scripts/Presentation/UI/EncounterPromptPresenter.cs` — lee `ExplorationPlayer.Party` en lugar de `_defaultParty` hardcodeado; `_defaultParty` queda como fallback
- `Animations/Exploration/ExplorationPlayer.controller` — NUEVO: estados Idle/RunStart/RunLoop/RunStop/Dash; params Speed(float) + IsDashing(bool); clips Run_Start/Loop/Stop asignados desde FBX con rig Humanoid
- `Animations/Exploration/ExplorationEnemy.controller` — NUEVO: estados Idle/Walk/Run; param Speed(float)
- `Animations/Exploration/Placeholder_*.anim` — 4 clips placeholder (Idle/Walk/Run/Dash)
- `Scenes/DungeonExploration.unity` — RPG-Character en Player + 7 enemigos (swap cápsula→modelo), ExplorationPlayer component en Player GO, EnemyEncounterTrigger + EnemyData en todos los enemigos del Dungeon, Enemy suelto movido a Dungeon/Enemies
- `Docs/BossDesign.md` — NUEVO: estructura boss (3 fases, 6 skills, 1 ult), pasivas fijas + cambiantes, mecánicas co-op
- `Docs/PlayerPassivesDesign.md` — NUEVO: diseño completo pasivas Hielo (Dominio Glacial), Fuego (Espíritu del Fénix), Sombra (Instinto Umbral) con triggers, hooks y scope de implementación
**Decisiones técnicas:**
- Stop instantáneo: `targetSpeed==0 → _currentSpeed=0` directamente. Aceleración al arrancar sigue con `MoveTowards`. Sin deceleration suavizada en stop.
- `ExplorationPlayer` desacopla la composición del party del EncounterPromptPresenter — el player GO es la fuente de verdad
- AnimatorController run con 3 estados (Start/Loop/Stop): transition `Run→Stop→Idle` con exit time en Stop para animación completa; `Run→Idle` directo disponible para stop instantáneo sin transición Stop
- EnemyEncounterTrigger añadido a los 6 enemigos del Dungeon vía MCP execute_code (no tenían el componente)
- Pasiva Sombra: trigger Velo de Sombra cambiado de "kill" a "crit en Marca" para funcionar vs boss (1 solo enemigo)
- Velo de Sombra: stackeable max 5, -15% daño recibido +12% crit index +40% ATK counter por stack; Forma Umbral a 5 stacks activos
**Pendiente para próxima sesión:**
1. **6.2 Victory screen** — popup mínimo "Volver a explorar" + nombre enemigo derrotado
2. **6.2 Defeat screen** — popup "Derrota" + retry sin recargar escena
3. **6.2 Enemy respawn** — `ExitCombatMode` llama `EnemyEncounterTrigger.ResetTrigger()` + re-activa GO
4. **Asignar CharacterData en Inspector** — `ExplorationPlayer._party` en Player GO (Guerrero/Maga/Arquera según personaje elegido)
5. **Implementar PassiveDefinition SO** — sistema base para pasivas; desbloquea Hielo/Fuego/Sombra
**Problemas encontrados:**
- `AnimatorControllerParameterType` namespace: no está en `UnityEditor.Animations` ni en `UnityEditor` — está en `UnityEngine`. Error recurrente en execute_code CodeDom.
- FBX run clips nombrados "Unreal Take" internamente — renombrados desde Inspector antes de asignar
- `EnemyEncounterTrigger` ausente en 6 de 7 enemigos — solo el Enemy suelto lo tenía; añadidos vía MCP

### Sesión 2026-05-19 — Tarea 6.1 pipeline exploración→combate funcional (pulido pendiente)
**Tareas trabajadas:** 6.1 (continuación) — transición exploración→combate in-scene
**Estado:** En progreso — pipeline funciona de extremo a extremo, faltan pulidos de posicionamiento y UX
**Archivos modificados:**
- `Assets/_Project/Scripts/Presentation/Combat/CombatBootstrapper.cs` — `Start()→OnEnable()` (re-uso entre encuentros), `OnDisable()` cleanup, `ClearTeamChildren()` (evita acumulación de pawns), `AutoInitCamera()` refactored (desactiva Cinemachine antes de capturar posición, posiciona cámara desde geometría del arena), `BeginCombat()` público separado de `OnEnableImpl()`, `_pendingFieldChars` para deferir `StartCombat` hasta post-fade, try/catch + Debug.Log steps para diagnóstico (remover en producción)
- `Assets/_Project/Scripts/Presentation/Combat/CombatTransitionScreen.cs` — removido `DontDestroyOnLoad` (error: UIManager no es root GO), `fadeOutDuration` 0.3→0.6s, range ampliado a 2s
- `Assets/_Project/Scripts/Presentation/UI/EncounterPromptPresenter.cs` — `TransitionToCombat` coroutine: FadeToBlack → enable CombatSetup → yield x2 → FadeFromBlack → `BeginCombat()`
- `Assets/_Project/Scripts/Presentation/Combat/CombatBlockoutPresenter.cs` — `BuildProceduralUI()`: construye canvas completo cuando refs son null (round label, HP, log, gauge, cards, action slots, end turn btn); `MakeCardButton()`/`MakeActionSlot()` procedurales como fallback
- `Assets/_Project/ScriptableObjects/Characters/CharacterData.cs` — campo `animatorController` (RuntimeAnimatorController)
- `Assets/_Project/ScriptableObjects/Enemies/EnemyData.cs` — campo `animatorController`
- `Assets/_Project/Scripts/Presentation/Combat/CombatArenaAssembler.cs` — slots en eje X (no Z), `animatorController` aplicado en spawn
- `Assets/_Project/Prefabs/CombatUI_Canvas.prefab` — NUEVO: prefab desde Combat.unity con `CombatHUDPresenter` y todas las refs internas (CardHandContainer, ActionSlotsContainer, GaugeContainer, etc.)
- `Assets/_Project/Scenes/DungeonExploration.unity` — `CombatSetup` deshabilitado al arrancar; `CombatUI_Canvas` (prefab) como hijo de CombatSetup; `CombatIntroSequencer` añadido a CombatSetup con `cameraController` wired; `CombatBootstrapper.presenter` → `CombatHUDPresenter`
**Decisiones técnicas:**
- `BeginCombat()` público en Bootstrapper: OnEnable init sistemas pero NO arranca TurnManager; EncounterPromptPresenter llama BeginCombat() después del fade-in → intro sequencer → StartCombat
- `ClearTeamChildren(DestroyImmediate)` antes de SpawnFromEncounterState para evitar acumulación (causa fue players=5 en lugar de 3)
- Camera fix: desactivar CinemachineBrain + vcam_Exploration ANTES de `InitFromTeams`, luego posicionar Main Camera detrás del equipo jugador desde geometría del arena (playerDir * span*0.8 + up*3.5)
- `CombatUI_Canvas` usa `CombatHUDPresenter` (de Combat.unity), no `CombatBlockoutPresenter` — tiene prefabs reales de cards y action slots
**Pendiente para próxima sesión (pulidos):**
1. **Desactivar cápsula del jugador durante combate** — `EnterCombatMode` desactiva `PlayerController` pero el GO del jugador sigue visible; probablemente necesita `SetActive(false)` en el renderer o en el GO capsule, no solo el controller
2. **Anchors de cámara de combate** — posición/rotación actual es funcional pero subóptima; ajustar `camDistanceMultiplier` (0.8) y `camHeight` (3.5) en `AutoInitCamera` según el arena real, o exponer como campos en Inspector
3. **Anchors del intro sequencer** — `introEnemyAnchor` e `introPlayerAnchor` null → usa fallback de gameplay anchors; crear GOs vacíos en DungeonExploration y asignarlos para una intro cinematográfica
4. **UI en posición correcta** — `CombatUI_Canvas` instanciado con posición world de Combat.unity; verificar que el canvas ScreenSpaceOverlay se vea correctamente en DungeonExploration; ajustar sortingOrder si compite con EncounterPromptPresenter (sortingOrder=200 > 10)
5. **Debug logs temporales** — remover `Debug.Log("[CombatBootstrapper] Step X")` y el try/catch de diagnóstico antes de producción
**Problemas encontrados:**
- `Start()` solo dispara una vez → combate solo iniciaba en primer encuentro. Fix: `OnEnable()`
- `PlayerTeam` acumulaba hijos de runs anteriores (players=5) → `ClearTeamChildren` con `DestroyImmediate`
- `InitFromTeams` capturaba posición Cinemachine (cerca de cápsula) → anchors en posición incorrecta. Fix: reposicionar cámara antes de capturar
- `DontDestroyOnLoad` en hijo de UIManager → excepción en runtime

### Sesión 2026-05-18 — Tarea 6.1 completada
**Archivos creados/modificados:**
- `Assets/_Project/Scripts/Presentation/Dungeon/EnemyEncounterTrigger.cs` — NUEVO: `[RequireComponent(SphereCollider)]`, radio desde `EnemyData.detectionRange`, `OnTriggerEnter("Player")` → raise `EncounterReadyEvent`, `ResetTrigger()` público, Gizmo rojo en editor
- `Assets/_Project/Scripts/Presentation/UI/EncounterPromptPresenter.cs` — añadido `_onEncounterAccepted?.Raise(_pending)` en `OnEnfrentar()` (estaba declarado pero nunca se llamaba)
**Pipeline completo exploración→combate:**
1. `EnemyEncounterTrigger.OnTriggerEnter` → raise `EncounterReadyEvent`
2. `EncounterPromptPresenter` muestra panel con info del enemigo
3. "Enfrentar" → `ServiceLocator.Register<EncounterState>` + raise `EncounterAcceptedEvent`
4. 6.2 (`SceneTransitionSystem`) se suscribe a `EncounterAcceptedEvent` y hace fade+load
**Setup Inspector requerido:**
- Agregar `EnemyEncounterTrigger` a cada GO enemigo en `DungeonExploration`
- Asignar `EnemyData` + `EncounterReadyEvent.asset` a cada trigger
- GO del jugador necesita tag `"Player"`

### Sesión 2026-05-17 (noche) — Tarea 6.0 completada
**Archivos creados/modificados:**
- `Assets/_Project/Scripts/Presentation/Combat/CombatArenaAssembler.cs` — refactored: `AssembleInRoom()`, `AssembleAtPositions()`, `SpawnFromEncounterState()`, `PendingRoom`, `Teardown()`
- `Assets/_Project/Scripts/Presentation/Combat/CombatBootstrapper.cs` — camera swap (`EnterCombatMode`/`ExitCombatMode`), exploration refs (_explorationBrain, _explorationVcam, _playerController, _cameraInput)
- `Assets/_Project/Scripts/Presentation/UI/EncounterPromptPresenter.cs` — NUEVO: panel confirmación encuentro, Enfrentar/cerrar, bloqueo input, _defaultParty
- `Assets/_Project/Scripts/Presentation/Dungeon/RoomVolume.cs` — NUEVO: BoxCollider room bounds, `GetArenaLayout()`
- `Assets/_Project/Scripts/Presentation/Dungeon/RoomRegistry.cs` — NUEVO: `FindNearest(Vector3)`
- `Assets/_Project/ScriptableObjects/Combat/EncounterAcceptedEvent.cs` — NUEVO: GameEvent<EncounterData> concreto
- `Assets/_Project/ScriptableObjects/Combat/EncounterState.cs` — añadido PlayerParty, ArenaCenter, ResolvedParty
- `Assets/_Project/Scenes/DungeonExploration.unity` — CombatSetup GO (CombatBootstrapper disabled), UIManager (EncounterPromptPresenter), camera wiring, _defaultParty=[Guerrero, Arquera, Maga]
**Decisiones técnicas:**
- In-scene combat (Option B): CombatBootstrapper deshabilitado en exploración, activado al pulsar Enfrentar — preserva inmersión del entorno
- PendingRoom pattern: Assembler guarda RoomVolume antes de activarse; SpawnFromEncounterState usa bounds de sala para layout óptimo
- CinemachineBrain disable swap: al entrar combate → Brain+vcam_Exploration off + CombatCameraController on; al salir → reverso
- EncounterState.PlayerParty poblado con defaultParty=[Guerrero, Arquera, Maga] desde EncounterPromptPresenter
- RPG-Character.prefab asignado a los 6 assets (3 CharacterData + 3 EnemyData) como prefab de blockout
**Deuda técnica nota:**
- EncounterState.cs vive en `ScriptableObjects/Combat/` pero es clase Core pura (Runefall.Core) — debería estar en `Scripts/Core/`. No bloquea, anotar para refactor.

### Sesión 2026-05-17 (tarde)
**Tareas trabajadas:** Pre-6.1 — Setup cámara exploración en DungeonExploration (deuda técnica de sesión anterior)
**Estado:** En progreso — cámara funcionando, pero ThirdPersonCamera.cs sigue en Main Camera (ver pendiente)
**Archivos modificados:**
- `Assets/_Project/Scripts/Presentation/Player/CameraManager.cs` — null-safe para combatCam/lockOnCam opcionales
- `Assets/_Project/Scripts/Presentation/Player/ThirdPersonCamera.cs` — CREADO (referencia para valores; debe quitarse de Main Camera)
- `Assets/_Project/Scripts/Presentation/Player/CameraWallAvoidance.cs` — CREADO, `DefaultExecutionOrder(1000)`, SphereCast pivot→cam
- `Assets/_Project/Scenes/DungeonExploration.unity` — vcam_Exploration rehecho: OrbitalFollow + HardLookAt + InputAxisController; CameraRig con CameraManager; CameraWallAvoidance en Main Camera
**Decisiones técnicas:**
- `CinemachineOrbitalFollow` (body) + `CinemachineHardLookAt` (aim) = equivalente exacto a `ThirdPersonCamera.cs`
- `CameraWallAvoidance` corre post-Cinemachine, solo acorta distancia en el mismo rayo — nunca cambia dirección ni rotación
- `CinemachineDeoccluder` descartado definitivamente — causa comportamiento errático
- `CameraManager` null-safe: explorationCam obligatorio, combatCam/lockOnCam opcionales (warnings, no errors)
- vcam_Exploration: Radius=7, TargetOffset=(0,1.5,0), GainX=0.12, GainY=-0.09, PositionDamping=0, BindingMode=WorldSpace
**Pendiente para próxima sesión:**
- **CRÍTICO**: Remover `ThirdPersonCamera` component de Main Camera en Inspector — está redundante con CinemachineBrain, consume input doble
- En Inspector de `CameraWallAvoidance`: excluir layer del Player en `Obstacle Mask`
- Continuar con Tarea 6.0 CombatArenaAssembler o 6.1 EncounterState Wiring
**Problemas encontrados:**
- `CinemachineOrbitalFollow` sin aim component no mira al player — se necesita `CinemachineHardLookAt` explícito
- `InputAxis` en Cinemachine 3.x no tiene `AccelTime`/`DecelTime` — esos campos están en el `Driver` del `InputAxisController`
- `ThirdPersonCamera` quedó en Main Camera sin remover al migrar a Cinemachine
