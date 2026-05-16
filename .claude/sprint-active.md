# Sprint 6 activo
**Semana:** 18–24 Mayo 2026
**Objetivo:** Core loop completo Exploración→Equipamiento→Combate. Mazmorras de jefe prediseñadas. Groundwork Netcode for GameObjects para co-op.
**Iniciado:** 2026-05-16

## Tareas

- [ ] Tarea 6.0 — CombatArenaAssembler: Spawna pawns de jugador y enemigo desde EncounterState antes de que arranque CombatBootstrapper.
- [ ] Tarea 6.1 — EncounterState Wiring: EnemyEncounterTrigger conecta detección de enemigo en exploración con SceneTransitionSystem; EncounterState queda en ServiceLocator para la escena de combate.
- [ ] Tarea 6.2 — SceneTransitionSystem: Fade in/out centralizado (ISceneTransition interface + SceneTransitionRunner MonoBehaviour DontDestroyOnLoad); registrado en ServiceLocator.
- [ ] Tarea 6.3 — EquipmentScreen: Panel UI Toolkit entre confrontación y combate; EquipmentState SO con arma + runas; botón "Entrar al combate" transiciona a escena Combat.
- [ ] Tarea 6.4 — BossRoomData + BossPhaseController: BossRoomData SO con fases (HP threshold + animator trigger); BossPhaseController MonoBehaviour escucha OnHPChanged del enemigo.
- [ ] Tarea 6.5 — NGO Groundwork: Instalar com.unity.netcode.gameobjects; NetworkedTurnManager wrapper (NetworkBehaviour en Presentation); remover Mirror de manifest.json; host ejecuta TurnManager, clientes via ServerRpc/ClientRpc + NetworkVariable.

## Entregable verificable
Core loop Exploración→Equipamiento→Combate. Mazmorras de jefe prediseñadas con 2 fases funcionales (HP threshold + animator trigger). Dos clientes en la misma red pueden completar un combate; host y cliente ven el mismo HP en tiempo real.

## Notas de sesión
