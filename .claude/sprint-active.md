# Sprint 4 activo
**Semana:** 27 Abril – 3 Mayo 2026
**Objetivo:** Combate completo funcionando: cartas en mano, rank-up por fusión, gauge de última, resolución de turno, efectos de estado.
**Iniciado:** 2026-04-20

## Tareas

- [ ] Tarea 4.1 — TurnManager: árbitro de fases (PlayerDecision → EnemyDecision → Resolution → PostTurn) con buffer de acciones, timeout 30s, GameEvents para UI sin acoplamiento
- [ ] Tarea 4.2 — CardSystem y Hand Management: mano repuesta por turno con skill1/skill2, fusión de cartas del mismo tipo y rango (max Rango 3), gauge de última al usar/fusionar
- [ ] Tarea 4.3 — CombatResolver y StatusEffects: resolución simultánea ordenada por velocidad, efectos de estado con Tick por turno, GameEvent<CombatResult>
- [ ] Tarea 4.4 — CombatCameraDirector: Presentation MB escucha SkillUsedEvent y ejecuta CameraSequenceData según rango (R1=Static, R2=PushIn, R3=DynamicOrbit, Ult=secuencia propia)

## Entregable verificable
Sistema de encuentro (popup + pre-combate) · Combate 2.5D con cartas, rank-up, gauge, resolución · Al usar carta R1 cámara no se mueve, R2 push-in visible, R3 órbita dinámica

## Notas de sesión
