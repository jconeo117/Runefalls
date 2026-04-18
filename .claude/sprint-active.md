# Sprint 3 activo
**Semana:** 20–26 Abril 2026
**Objetivo:** Completar el movimiento de exploración (salto + dash) y definir todos los ScriptableObjects incluyendo el sistema de encuentro con enemigos.
**Iniciado:** 2026-04-16

## Tareas

- [x] Tarea 3.0 — Salto y Dash ✓: Completar `PlayerController` con salto (gravedad real) y dash direccional con cooldown. Prerequisito de exploración.
- [ ] Tarea 3.1 — Jerarquía de ScriptableObjects: Definir `CharacterData`, `SkillData`, `SkillEffect`, `CameraSequenceData`, `WeaponData`, `RuneData`, `EnemyData` como clases SO.
- [ ] Tarea 3.2 — SO del Sistema de Encuentro: Definir `EncounterData` (runtime) y `EncounterState` (Core) para la transición exploración→combate.
- [ ] Tarea 3.3 — Stats y Fórmulas de Combate: Implementar `CharacterStats` con operador `+`, y `CombatFormulas` estático con multiplicadores elementales.

## Entregable verificable
Salto + Dash funcionales · Todos los SO definidos incluyendo EncounterData · Assets creados en el inspector: Kael, Lyra, Vorn, 4 armas, 10 Runas, 4 enemigos con `detectionRange` y `approximatePower` configurados.

## Notas de sesión
- **2026-04-16 · Tarea 3.0:** Salto y Dash implementados. `GameInputActions` actualizado: Dodge→Jump (Space/South) + Dash (LeftShift/West). `InputReader` con `JumpEvent`/`DashEvent`, `DodgeEvent` como alias. `PlayerController`: `HandleVertical` + `PerformDash` coroutine + `Physics.CheckSphere` para ground check confiable (reemplaza `cc.isGrounded`). Dash funciona en aire y tierra. Jump requiere `GroundCheck` Transform en los pies asignado en inspector.
