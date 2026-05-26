# Player Passives Design — Runefall

> Documento de referencia para implementación. Incluye diseño, triggers y sistemas nuevos requeridos.

---

## Sistemas nuevos requeridos (compartidos)

Antes de implementar cualquier pasiva, estos sistemas deben existir:

| Sistema | Descripción |
|---|---|
| `PassiveDefinition` SO abstracto | Base class con `Activate(owner, tm, ctx)` / `Deactivate(owner, tm)` |
| `CharacterData.passives[]` | Array de `PassiveDefinition` en CharacterData |
| `CombatBootstrapper` activa pasivas | Llama `Activate` al iniciar combate, `Deactivate` al terminar |
| `StatModifier` tipo `IncomingDamageMultiplier` | Multiplicador de daño entrante en `CharacterStats` |
| `IsCrit` en `CombatActionResult` | Flag booleano para detectar golpes críticos |
| Efectos con `duration = -1` (permanentes) | `EffectDefinition` soporta duración infinita sin expirar |

---

## Personaje Hielo

### Pasiva: Dominio Glacial

**Descripción completa:**
- Al comienzo del turno del jugador, aplica *Hipotermia* a todos los enemigos por 2 turnos
- Al atacar enemigo con *Hipotermia*: +30% daño recibido por el enemigo
- Al anotar crítico en enemigo con *Hipotermia*: cura 50% del daño infligido
- Al recibir daño mayor al 60% de HP máximo en un solo golpe: congela todos los enemigos + aplica *Dominio del Hielo* al héroe

**Efectos involucrados:**

| Efecto | Duración | Descripción |
|---|---|---|
| *Hipotermia* | 2 turnos | -20% DEF + marca para +30% daño entrante |
| *Dominio del Hielo* | Permanente (combate) | +15% ATK + ×2 daño a enemigos congelados |
| *Congelado* | Hasta ser roto | Enemigo no actúa en su turno |

### Triggers y hooks

| Cláusula | Hook | Estado |
|---|---|---|
| Hipotermia al inicio de turno | `tm.OnPlayerTurnStarted` | ✅ existe |
| +30% daño vs Hipotermia | `StatModifier IncomingDamageMultiplier` en actor con efecto | ❌ nuevo |
| Heal 50% en crítico | `tm.OnActionResolved` — leer `IsCrit` + check efecto en target | ⚠️ falta `IsCrit` |
| Trigger daño >60% maxHP | `tm.OnActionResolved` — target == owner, calcular ratio | ✅ manejable |
| Congelar todos al trigger | `ActorEffects.Apply(Congelado)` sobre `ctx.Enemies` | ✅ |
| Dominio del Hielo al trigger | `ActorEffects.Apply(DominioDelHielo)` sobre owner | ✅ |

### Sistemas específicos requeridos

- `StatModifier` tipo `IncomingDamageMultiplier` (Hipotermia lo aplica al ser añadida)
- `IsCrit` en `CombatActionResult`
- `EffectDefinition`: Hipotermia, Congelado, Dominio del Hielo

---

## Personaje Fuego

### Pasiva: Espíritu del Fénix

**Descripción completa:**
- Al inicio de la batalla, aplica *Armadura Volcánica* al héroe (permanente)
- Por cada skill enemiga que impacte al héroe durante el turno enemigo, aplica *Quemadura* a todos los actores del campo
- Si los HP del héroe caen a 0 durante el turno enemigo, intercepta la muerte: aplica *Voluntad del Fénix* al héroe por 2 turnos y 2 stacks de *Quemadura* a todos los enemigos

**Efectos involucrados:**

| Efecto | Duración | Descripción |
|---|---|---|
| *Armadura Volcánica* | Permanente | Repele 15% del daño recibido al atacante |
| *Quemadura* | Permanente, stacking | +10% daño recibido por stack. Sin límite de stacks. Aplica a todos (aliados + enemigos) |
| *Voluntad del Fénix* | 2 turnos | Cura 50% HP máx + ATK/CritDmg/CritRate/DmgIndex +5% por cada stack de Quemadura en campo |

### Triggers y hooks

| Cláusula | Hook | Estado |
|---|---|---|
| Armadura Volcánica al inicio | `Activate()` directo | ✅ |
| Repeler 15% daño | Nuevo `DamageReflect` modifier en `CombatResolver` | ❌ nuevo |
| Quemadura por skill recibida (no por impact frame) | `tm.OnActionResolved` donde target == owner + deduplicación por `ActionId` | ❌ falta `ActionId` |
| Quemadura a TODOS | `ActorEffects.Apply` sobre `ctx.AllActors` | ✅ |
| Quemadura stacking infinito sin expirar | `EffectDefinition` con `duration = -1` + contador de stacks | ❌ falta stack infinito |
| Interceptar muerte (HP → 0) | Nuevo hook `OnLethalDamage` en TurnManager antes de confirmar muerte | ❌ nuevo |
| Fénix: contar stacks en campo | Iterar `ctx.AllActors`, sumar stacks de Quemadura | ✅ manejable |
| Fénix una sola vez por combate | Flag `_phoenixUsed` en instancia de pasiva | ✅ trivial |

### Sistemas específicos requeridos

- `DamageReflect` mechanic en `CombatResolver` (cuando actor recibe daño, devuelve X% al atacante)
- `ActionId` (`System.Guid`) en `CombatActionResult` — mismo skill AoE comparte mismo ID, evita múltiples Quemaduras por un solo skill AoE
- `EffectDefinition` con stacking infinito (`duration = -1`, `maxStacks = -1`)
- Hook `OnLethalDamage` en `TurnManager` — `Func<ICombatActor, int, bool>` retorna `true` si interceptado
- `EffectDefinition`: Armadura Volcánica, Quemadura, Voluntad del Fénix

### Interacción de diseño intencional

Quemadura aplica también al héroe → más daño recibido → mayor probabilidad de activar Fénix → Fénix escala con stacks de Quemadura en campo (propios + enemigos). Loop deliberado riesgo/recompensa: permitir golpes = Fénix más potente.

---

## Personaje Sombra

### Pasiva: Instinto Umbral

**Descripción completa:**

> "Al comienzo del turno del jugador, aplica *Marca Umbral* sobre el enemigo con menor HP presente en el campo. En combate de jefe, la *Marca Umbral* recae siempre sobre el jefe. ***Marca Umbral:* el héroe ignora el 25% de la defensa del portador y este recibe +20% de daño de todas las fuentes.** Cuando el héroe anota un golpe crítico sobre un enemigo que tenga *Marca Umbral*, aplica una instancia de *Velo de Sombra* sobre sí mismo. ***Velo de Sombra* (máx. 5 instancias, stackeable): suprime el 15% del daño recibido, aumenta el índice de golpe crítico un 12% y contraataca automáticamente con el 40% del ATK base por instancia activa.** Al acumular 5 instancias activas de *Velo de Sombra*, aplica *Forma Umbral* de forma permanente sobre el héroe por el resto del combate. ***Forma Umbral:* cada ataque del héroe aplica una carga de *Corrupción* al objetivo. *Corrupción:* -10% de todos los stats por carga, sin límite de cargas.**"

**Efectos involucrados:**

| Efecto | Duración | Descripción |
|---|---|---|
| *Marca Umbral* | Hasta inicio siguiente turno | Objetivo: -25% DEF efectiva + recibe +20% daño |
| *Velo de Sombra* | Permanente, max 5 stacks | Por stack: -15% daño recibido, +12% crit index, counter 40% ATK |
| *Corrupción* | Permanente, stacking infinito | -10% todos los stats por carga |
| *Forma Umbral* | Permanente (resto del combate) | Activa generación de Corrupción en cada ataque |

**Loop estratégico:**
```
Marca Umbral → anotar crítico en marcado
→ +1 Velo de Sombra (acumulable)
→ 5 Velos → Forma Umbral activa
→ Corrupción en cada hit → enemigos se debilitan progresivamente
→ enemigos más débiles = crits más frecuentes = loop
```

**Funciona en boss:** Marca siempre recae en el boss → acumular 5 crits a lo largo del combate → Forma Umbral llega en fase media del boss. Incentiva build de crit rate.

**Funciona en enemigos normales:** Marca en el más débil → kill rápido → nueva Marca en el siguiente → Velos se acumulan por oleadas.

### Triggers y hooks

| Cláusula | Hook | Estado |
|---|---|---|
| Marca Umbral al inicio de turno | `tm.OnPlayerTurnStarted` — target = menor HP en `ctx.Enemies` | ✅ existe |
| +20% daño recibido por marcado | `StatModifier IncomingDamageMultiplier` en actor marcado | ❌ nuevo (compartido con Hielo) |
| -25% DEF ignorada al atacar | `StatModifier DefensePenetration` en owner mientras marcado vive | ❌ nuevo |
| +1 Velo en crítico vs marcado | `tm.OnActionResolved` — `IsCrit` + check efecto Marca en target | ⚠️ falta `IsCrit` |
| Velo: suprimir daño | `StatModifier IncomingDamageMultiplier` acumulativo por stack | ❌ nuevo |
| Velo: +crit index | `StatModifier CritIndexBonus` por stack | ❌ nuevo |
| Velo: contraataque | `tm.OnActionResolved` donde target == owner — dispara hit extra | ⚠️ necesita hit fuera de turno |
| Forma Umbral al llegar a 5 Velos | Check en lógica de pasiva tras aplicar Velo | ✅ trivial |
| Corrupción en cada hit con Forma Umbral | `tm.OnActionResolved` donde caster == owner | ✅ manejable |

### Sistemas específicos requeridos

- `StatModifier` tipo `DefensePenetration` (ignorar X% DEF del objetivo)
- `StatModifier` tipo `CritIndexBonus`
- Hit fuera de turno (contraataque) — reutilizable desde lógica de Fuego si se generaliza
- `IsCrit` en `CombatActionResult` (compartido con Hielo)
- `EffectDefinition` con stacking infinito para Corrupción (compartido con Fuego)
- `EffectDefinition`: Marca Umbral, Velo de Sombra, Corrupción, Forma Umbral

---

## Scope de implementación — resumen por sistema nuevo

| Sistema nuevo | Hielo | Fuego | Sombra | Prioridad |
|---|---|---|---|---|
| `PassiveDefinition` SO abstracto | ✅ | ✅ | ✅ | Alta — bloquea todo |
| `CharacterData.passives[]` | ✅ | ✅ | ✅ | Alta |
| `IsCrit` en `CombatActionResult` | ✅ | — | ✅ | Alta |
| `IncomingDamageMultiplier` stat | ✅ | ✅ | ✅ | Alta |
| `EffectDefinition` permanentes (duration=-1) | — | ✅ | ✅ | Alta |
| `EffectDefinition` stacking infinito | — | ✅ | ✅ | Alta |
| `ActionId` en `CombatActionResult` | ⚠️ útil | ✅ | — | Media |
| `DamageReflect` en `CombatResolver` | — | ✅ | — | Media |
| `OnLethalDamage` hook en `TurnManager` | — | ✅ | — | Media |
| `DefensePenetration` stat modifier | — | — | ✅ | Media |
| `CritIndexBonus` stat modifier | — | — | ✅ | Media |
| Hit fuera de turno (contraataque) | — | ✅ | ✅ | Media |

---

## Pendiente

- [x] Pasiva personaje Hielo — Dominio Glacial
- [x] Pasiva personaje Fuego — Espíritu del Fénix
- [x] Pasiva personaje Sombra — Instinto Umbral
- [ ] Pasivas de boss (ver `BossDesign.md`)
- [ ] Valores numéricos balanceados tras primera iteración de pruebas
- [ ] Implementación: arrancar por sistemas compartidos, luego Hielo, luego Fuego, luego Sombra
