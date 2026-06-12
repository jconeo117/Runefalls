# Guion — Video entregable Runefall (Multiplayer)

> **Duración objetivo:** 12-14 min · **Formato:** narración + screen capture del Editor + gameplay
> **Estructura:** 3 bloques de ~5 min — (1) decisiones, (2) demo, (3) análisis de scripts.

---

## Intro (~30 s)

**Decir:**
> "Runefall, prototipo de juego co-op por turnos para 2 jugadores, hecho en Unity 6 con Netcode for GameObjects. Combate estilo RPG táctico: dos jugadores enfrentan a un jefe usando cartas de habilidad. Voy a cubrir tres cosas: decisiones de diseño y arquitectura de red, una demo en vivo, y el análisis de los scripts clave."

**Mostrar:** título / juego corriendo de fondo.

---

## BLOQUE 1 — Decisiones de diseño, topología e infraestructura (~5 min)

### 1.1 El juego y por qué define la red (~1 min)
> "Es un combate **por turnos**, no de acción en tiempo real. Cada jugador llena un tablero compartido de 6 action slots con sus cartas; cuando se agotan, el servidor resuelve el turno. En turn-based el **estado es discreto y la latencia no es crítica**, pero **necesitas una única fuente de verdad** para que ambos clientes vean lo mismo y nadie haga trampa."

**Mostrar:** combate con slots y cartas.

### 1.2 Topología: client-server host-authoritative (~1.5 min)
> "Topología **cliente-servidor con host autoritativo** (listen server). Un jugador es **host y servidor**: autoridad total sobre el combate. Los clientes **piden** acciones por `ServerRpc` y **reciben** resultados por `ClientRpc` y `NetworkVariable`.
> ¿Por qué es adecuada?
> 1. **Una sola autoridad** resuelve daño y orden de turnos → estado consistente y anti-trampa.
> 2. Para **co-op de 2** no justifica servidor dedicado: el host hace de servidor, sin costo de cómputo.
> 3. NGO está **diseñado para este modelo** — `NetworkObject`, RPCs y `NetworkVariable` encajan directo."

**Mostrar:** diagrama Host(Server) ↔ Client con flechas ServerRpc / ClientRpc.

### 1.3 Infraestructura: Unity Relay + Authentication (~1.5 min)
> "Para conectar por internet uso **Unity Relay** (UGS). El problema del modelo host es el **NAT**: sin Relay habría que abrir puertos o estar en LAN. Relay actúa de **retransmisor**: el host crea una allocation y obtiene un **join code** de 6 caracteres; el cliente se une con ese código. **Sin abrir puertos, sin IPs.**
> Antes de Relay inicializo **UGS** y hago **sign-in anónimo** con Authentication — Relay requiere un jugador autenticado. Todo en un servicio dedicado: `RelayConnectionService`.
> Mantengo un **fallback de IP directa** para LAN, conmutable con un flag — el prototipo funciona aunque UGS no esté disponible."

**Mostrar:** Project Settings → Services (proyecto vinculado) y cloud.unity.com con Relay.

### 1.4 Stack y sincronización (~1 min)
> "Stack: Unity 6, URP, NGO 2.12. La sincronización combina tres mecanismos según el dato:
> - **`NetworkObject`** para los pawns (spawn replicado).
> - **`NetworkVariable` / `NetworkList`** para el tablero de slots compartido.
> - **RPCs** para resolver el combate: el host calcula el daño y **transmite el HP**; el daño se aplica en el **frame de impacto** de la animación, sincronizado por Animation Events."

**Mostrar:** un golpe con número de daño + barra de HP bajando.

---

## BLOQUE 2 — Demo: del menú al inicio de partida (~5 min)

> Graba con 2 ventanas (host + virtual player de MPPM). Narra mientras haces.

### 2.1 Menú y lobby (~1.5 min)
1. Arranca en **MainMenu** → Multiplayer.
2. **Crear sala** (host): "UGS se inicializa, se crea la allocation de Relay y aparece el **join code**."
3. Muestra el **join code**.

**Mostrar:** consola del host con `[Lobby] StartHost() vía Relay | JoinCode: XXXXXX`.

### 2.2 Unirse y selección de personaje (~1.5 min)
1. Segundo cliente: **unirse con el join code**.
2. "Se conecta **por Relay**, sin IP. El `NetworkedLobbyManager` registra el personaje de cada jugador, sincronizado al host."
3. Ambos jugadores listos.

### 2.3 Inicio de partida y combate (~2 min)
1. Host inicia → carga `Multiplayer_BossFight` **vía NGO SceneManager** (todos juntos).
2. "Aparecen los pawns de ambos jugadores y el jefe, replicados."
3. Juega un turno: cartas en slots → agótalos → **resolución** (animaciones, daño en frame de impacto, números).
4. Muestra el **turno del jefe** (3 ataques).
5. Fuerza fin con teclas debug: **F9** (boss a 1 HP → victoria), **F11** (ambos a 1 HP → derrota). Muestra la **pantalla de fin** y el **consenso de reintentar** ("Esperando al otro jugador").

**Mostrar:** el ciclo en ambas ventanas para evidenciar la sincronización.

---

## BLOQUE 3 — Análisis de scripts relevantes (~5 min)

> Abre cada script y resalta lo clave. Orden sugerido abajo; el detalle de cada método está en la sección **"Métodos clave por archivo"**.

- **3.1 `RelayConnectionService`** (~1 min) — infraestructura: init UGS + auth + Relay.
- **3.2 `LobbyManager`** (~1 min) — orquesta la conexión (Relay vs IP), StartHost/StartClient.
- **3.3 `ServerCombatOrchestrator`** (~1.5 min) — corazón server-authoritative: fases + RPCs + impact-driven damage + fin de combate.
- **3.4 `MultiplayerActionSlotsSync`** (~1 min) — estado compartido con `NetworkList`.
- **3.5 `NetworkedCombatPawn`** (~30 s) — objeto sincronizado.

---

## Cierre (~30 s)
> "En resumen: topología cliente-servidor host-autoritativa, adecuada para co-op por turnos; Unity Relay como infraestructura para conectar sin abrir puertos; y sincronización con NGO usando NetworkObject, NetworkVariable, NetworkList y RPCs, con el host como única autoridad. Gracias."

---

## Tips de grabación
- **MPPM:** 1 virtual player = el 2º jugador. No abras un 3º (la allocation es de 2; `maxPlayers = 2`).
- Ten la **consola visible** al conectar (evidencia el join code de Relay).
- Graba las 2 ventanas lado a lado → la sincronización se ve sola.

---
---

# Métodos clave por archivo

> Referencia para explicar cada script durante el Bloque 3. Cada método con qué hace.

## `RelayConnectionService` (infraestructura de conexión)
Servicio estático, responsabilidad única: UGS + Relay. No conoce el juego.

| Método | Qué hace |
|---|---|
| `EnsureSignedInAsync()` | Inicializa UGS (`UnityServices.InitializeAsync`) y hace sign-in **anónimo** con Authentication. **Idempotente** (chequea estado antes), seguro de llamar varias veces. |
| `CreateAllocationAsync(maxPlayers)` | **Host**: crea la allocation de Relay para `maxPlayers-1` peers, obtiene el **join code** (`GetJoinCodeAsync`), apunta el `UnityTransport` al `RelayServerData` y devuelve el código. |
| `JoinAllocationAsync(joinCode)` | **Cliente**: se une a la allocation por código (`JoinAllocationAsync`) y apunta el `UnityTransport` al `RelayServerData`. |
| `GetTransport()` | Helper privado: obtiene el `UnityTransport` del `NetworkManager`; lanza excepción si falta. |

## `LobbyManager` (orquesta lobby + conexión)
| Método | Qué hace |
|---|---|
| `CreateRoom()` | Punto de entrada del botón "Crear sala". Ramifica: si `useRelay` → `CreateRoomRelayAsync`; si no → `CreateRoomDirectIp`. |
| `CreateRoomRelayAsync()` | Crea la allocation Relay (guarda el join code en `CurrentCode`), hace `StartHost()`, spawnea el `NetworkedLobbyManager`, registra su personaje y dispara `OnHostStarted`. Maneja errores → `OnConnectionFailed`. |
| `CreateRoomDirectIp()` | Path LAN preservado: genera código local, `SetConnectionData("0.0.0.0", port)`, `StartHost()`. |
| `JoinRoom(codeOrIp)` | Punto de entrada de "Unirse". Si `useRelay` → `JoinRoomRelayAsync(joinCode)`; si no → `JoinRoomDirectIp`. |
| `JoinRoomRelayAsync(joinCode)` | Se une a Relay con el código, `StartClient()`, arranca el timeout de conexión. Errores → `OnConnectionFailed`. |
| `JoinRoomDirectIp(input)` | Path LAN: resuelve código→IP o IP directa, `SetConnectionData`, `StartClient()`. |
| `LoadCombatScene()` | Solo host: carga `Multiplayer_BossFight` con `NetworkManager.SceneManager.LoadScene` (NGO sincroniza la carga a todos). |
| `LeaveRoom()` / `CancelJoin()` | `Shutdown()` de la red y notifican `OnDisconnected`. |
| Eventos (`OnHostStarted`, `OnClientConnected`, `OnConnectionFailed`, `OnPlayerCountChanged`) | Desacoplan la lógica de red de la UI (`LobbyUIController` se suscribe). |

## `NetworkedLobbyManager` (NetworkBehaviour, DDOL)
Sincroniza qué personaje eligió cada jugador; sobrevive a la carga de escena.

| Método | Qué hace |
|---|---|
| `RegisterCharacterServerRpc(name)` | El cliente envía su personaje; el servidor lo guarda en un diccionario `clientId → personaje`. |
| `GetCharacterName(clientId)` / `GetAllAssignments()` | Consultas que usa el BossFight para spawnear el pawn correcto por jugador. |

## `ServerCombatOrchestrator` (NetworkBehaviour, server-only) — núcleo del combate
Corre **solo en el servidor**. Conduce el loop de turnos y difunde resultados por RPC.

| Método | Qué hace |
|---|---|
| `Initialize(ctx, registry)` | Recibe el estado server-authoritative (`ServerCombatContext`) con players/enemies. |
| `OnAllPlayersExhausted()` | Disparador: cuando se llenan todos los slots, arranca la resolución (guard `_phaseRunning` evita ciclos dobles). |
| `RunCardResolutionPhase()` | **Fase 2**: itera los slots, por cada carta hace `ExecuteCardClientRpc` (anima en todos), espera la animación, aplica daño y al final chequea **victoria**. |
| `RunEnemyPhase()` | **Fase 3**: el jefe ataca `EnemyAttacksPerTurn` (3) veces; cada swing elige objetivo, anima y aplica daño; al final resuelve **muerte/derrota**. |
| `BeginNewPlayerTurnPhase()` | **Fase 4**: avanza la ronda, resetea el tablero de slots y avisa nuevo turno (`BeginNewPlayerTurnClientRpc`). |
| `ApplyAndBroadcastCardHit()` | Aplica **un golpe** de la carta (fracción `1/AEs`) y transmite el HP del enemigo (`EnemyHpChangedClientRpc`) con daño + crit. |
| `ApplyAndBroadcastEnemyHit()` | Igual para el ataque enemigo → transmite HP del player; en el **último AE** dispara `ResolvePlayerDeath`. |
| `ResolvePlayerDeath(cid)` | Marca al player muerto: reduce el board de slots (`MarkPlayerDead`) y avisa a todos (`PlayerDiedClientRpc`). Una sola vez. |
| `EndCombat(won)` | Marca `_combatOver`, detiene el loop y difunde `CombatEndedClientRpc(won)`. |
| `CardImpactServerRpc(slot)` | El **dueño** de la carta avisa el frame de impacto → el servidor aplica el daño **en ese momento** (impact-driven). |
| `EnemyImpactServerRpc()` | El **host** avisa el impacto del enemigo → aplica el daño en el frame correcto. |
| `RetryServerRpc()` | Registra el **voto de reintentar**; solo recarga el BossFight cuando **ambos** votaron (`RetryStatusClientRpc` informa el progreso). |
| `*ClientRpc` (`ExecuteCard`, `EnemyAttacking`, `PlayerHpChanged`, `EnemyHpChanged`, `BeginNewPlayerTurn`, `PlayerDied`, `CombatEnded`, `RetryStatus`) | Difunden eventos a **todos** los clientes (`SendTo.ClientsAndHost`); cada cliente los refleja en UI/animación. |
| `DebugSetBossLow / SetOnePlayerLow / SetAllPlayersLow` (F9/F10/F11, solo editor) | Setean HP a 1 para probar victoria/derrota sin jugar todo. |

## `ServerCombatContext` (estado autoritativo, no replicado)
| Método | Qué hace |
|---|---|
| `BeginPlayerCard(...)` / `ApplyPlayerCardHit()` | Selecciona caster/target/skill una vez y aplica el daño por golpe (multi-hit = `1/AEs`). |
| `BeginEnemyAttack(...)` / `ApplyEnemyAttackHit()` | Igual para el ataque enemigo. |
| `AllEnemiesDead()` / `AllPlayersDead()` / `IsPlayerAlive(cid)` | Condiciones de fin de combate. |

## `MultiplayerActionSlotsSync` (NetworkBehaviour) — tablero compartido
Ejemplo claro de estado **server-authoritative** con `NetworkList`.

| Método | Qué hace |
|---|---|
| `InitializeSlots(playerCount)` | El servidor crea `playerCount × 3` slots en el `NetworkList` (2 jugadores = 6). |
| `PlaceCardServerRpc(slot, card)` | El cliente **pide** colocar una carta; el servidor valida (rango, ocupado, acciones restantes, no muerto) y escribe el slot. |
| `CheckAllExhausted()` | El servidor decide cuándo terminó el turno (`used >= SlotCount`) y avisa (`OnAllPlayersExhausted`). |
| `ResetSlotsServerRpc()` | Limpia el tablero al iniciar un nuevo turno. |
| `MarkPlayerDead(cid)` | Reduce el tablero a `vivos × 3` (6→3 al morir uno); el muerto no puede jugar. |
| `GetActionsRemaining(cid)` | Acciones restantes del jugador (0 si está muerto). |

## `NetworkedCombatPawn` (NetworkBehaviour) — objeto sincronizado
| Método | Qué hace |
|---|---|
| `OnNetworkSpawn()` | Al spawnear (replicado a todos), inicializa el pawn. |
| `InitializePawn()` | Lee `CharacterOrEnemyDataName` (NetworkVariable), busca el `CharacterData`/`EnemyData` en el registro e **instancia el modelo visual** con su animator. |
| `TryParentToSlot()` | Confirma que la arena está lista; el servidor ya spawneó el pawn en la posición del slot (NGO replica el transform en el spawn). |
| `NetworkVariable`s (`CharacterOrEnemyDataName`, `SlotIndex`, `IsPlayerTeam`) | Datos replicados que definen qué/quién es cada pawn. |

## `MultiplayerLocalCombatSetup` (por cliente) — arma el HUD local
| Método | Qué hace |
|---|---|
| `Start()` | Cada cliente construye su **mano local** (su personaje), instancia el HUD, crea el animation/HP controller y se suscribe al orchestrator. |
| `OnNewPlayerTurn(round)` | Reactiva los slots y rota la mano al nuevo turno. |
| `OnPlayerDied(cid)` | Si murió el **player local**, oculta su HUD → modo **espectador** (la arena sigue visible). |
| `OnCombatOver(won)` | Muestra la pantalla de fin (`MultiplayerEndScreen`) en todos los clientes. |

## `MultiplayerAnimationController` (por cliente) — animación cosmética
| Método | Qué hace |
|---|---|
| `PlayCardAnimation(...)` | Reproduce la animación de la carta; en el frame de impacto (AE) el dueño avisa al servidor (`CardImpactServerRpc`). |
| `DrainEnemyAttacks()` | **Serializa** los swings del jefe (cola) para que no se solapen ni corrompan la animación. |
| `PlayAttackChoreography(...)` | Coreografía SP: rotar → lunge al objetivo → secuencia de clips → impacto por AE (melee `ImpactFrame`, ranged `Shoot`+proyectil) → retorno a origin (rotación de spawn determinística). |
| `OnPlayerDied(cid)` / `OnCombatOver(won)` | Reproducen `PlayDeath` y desactivan el pawn (jugador muerto / jefe en victoria). |

## `MultiplayerEndScreen` (UI runtime) — victoria/derrota
| Método | Qué hace |
|---|---|
| `Show(won)` | Construye el overlay (victoria o derrota) en **todos** los clientes. |
| `Retry()` | Vota reintentar (`RetryServerRpc`), desactiva el botón y muestra "Esperando al otro jugador". |
| `OnRetryStatus(ready, total)` | Actualiza el progreso del consenso ("Listos N/M"). |
| `ReturnToLobby()` | `Shutdown()` local + carga el lobby (cada cliente se desconecta). |
