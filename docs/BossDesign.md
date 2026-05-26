# Boss Design — Runefall

## Estructura general

- **Turnos por ronda:** 3 ataques (igual que enemigos normales y jugadores)
- **Skills:** 6 totales — 2 por fase
- **Ultimate:** 1
- **Fases:** 3 (basadas en HP threshold)
- **Tipos de boss:**
  - **Mazmorra (solo):** 1 jugador, balance agresivo, skills de debuff/daño alto
  - **Co-op:** 2 jugadores, skills con split aggro, mecánica de aggro forzado

---

## Pasivas fijas (activas toda la pelea)

| # | Nombre | Efecto |
|---|---|---|
| 1 | **Armadura Reactiva** | Cada hit recibido otorga +X DEF hasta el siguiente turno del boss — penaliza spam de cartas débiles |
| 2 | **Furia Acumulada** | Cada ronda que pasa sin morir, +10% DMG acumulativo (no resetea) — presiona al jugador a cerrar rápido |
| 3 | **Represalia** | Al recibir un crítico, el boss ejecuta una skill adicional del pool actual fuera de turno |
| 4 | **Maldición Persistente** | Al inicio de cada turno del jugador, aplica Debilitar (-15% ATK) a un jugador aleatorio |

> **Combo peligroso:** Represalia + Furia Acumulada — en ronda 5+ un crítico genera un contraataque con daño amplificado.

---

## Pasiva cambiante por fase

| Fase | HP Threshold | Nombre | Efecto |
|---|---|---|---|
| 1 | 100–75% | **Intimidación** | El jugador empieza su turno con 1 acción menos disponible |
| 2 | 75–50% | **Frenesí** | El boss ataca 2 veces por turno en vez de 1 |
| 3 | 50–0% | **Último Aliento** | Una vez por pelea, sobrevive un golpe mortal con 1 HP y gana +50% DMG por 1 turno |

> **Nota Frenesí:** En fase 2 el jugador debe priorizar DEF/heal antes de atacar.  
> **Nota Último Aliento:** Momento dramático — si el jugador no tiene heals disponibles puede ser fatal.

---

## Skills por fase (estructura, sin nombres definitivos)

| Fase | Skill A | Skill B |
|---|---|---|
| 1 | Single target daño moderado | Debuff (ATK o DEF down) |
| 2 | AoE daño | Self-buff (stat up o escudo) |
| 3 | High single target daño | Mechanic especial (ej. AllAllies, counter) |

---

## Co-op — mecánicas específicas

- Skills con `TargetType.RandomEnemy` alternan targets entre los 2 jugadores
- **Split Aggro:** skill que fuerza al boss a atacar solo a 1 jugador durante N turnos — el otro cura/buffea
- Si un jugador muere → sus cartas salen de mano, el otro continúa solo
  - Compensación: boss pierde 1 fase de pasiva o reduce stats base

---

## Pendiente de definir

- [ ] Pasivas del jugador (trabajar en sesión aparte)
- [ ] Valores numéricos exactos (X DEF, X DMG, duración de efectos)
- [ ] Nombres definitivos de skills por boss
- [ ] Implementación: `BossPhase` SO + sistema de pasivas
