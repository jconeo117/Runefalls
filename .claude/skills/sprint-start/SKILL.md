---
name: sprint-start
description: Inicia un sprint de RUNEFALL. Lee el sprint plan, genera el checklist de tareas del sprint indicado, actualiza CLAUDE.md con el estado actual y prepara el contexto de trabajo. Invocar al comenzar una semana de desarrollo.
invocation: explicit
---

Se te invocó con: `/sprint-start $ARGUMENTS`

El argumento es el número de sprint (1-8). Si no se proporcionó, pregunta cuál.

## Tu tarea

1. **Lee el sprint plan completo**
   - Abre `docs/RUNEFALL_Sprints.md`
   - Localiza el sprint indicado
   - Extrae todas las tareas (Tarea X.Y) con sus objetivos técnicos

2. **Lee el estado actual**
   - Abre `CLAUDE.md`
   - Identifica el sprint activo y tareas completadas previamente

3. **Genera el checklist del sprint**
   - Crea o sobreescribe `.claude/sprint-active.md` con este formato exacto:

```markdown
# Sprint $NUMERO activo
**Semana:** [fechas del sprint]
**Objetivo:** [objetivo del sprint del plan]
**Iniciado:** [fecha actual]

## Tareas

- [ ] Tarea X.1 — [nombre]: [objetivo técnico en una línea]
- [ ] Tarea X.2 — [nombre]: [objetivo técnico en una línea]
- [ ] Tarea X.3 — [nombre]: [objetivo técnico en una línea]

## Entregable verificable
[copiado literalmente del sprint plan]

## Notas de sesión
[vacío al inicio]
```

4. **Actualiza CLAUDE.md**
   - Sección "Estado actual del sprint": Sprint X activo, fecha de inicio, 0/N tareas completadas

5. **Reporta al usuario**
   - Lista las tareas del sprint con sus objetivos técnicos
   - Indica cuál es la Tarea 1 recomendada para comenzar
   - Recuerda: una tarea a la vez, no el sprint completo de golpe

**Regla crítica:** No escribas ningún código en este skill. Solo lees, generas documentación y reportas.
