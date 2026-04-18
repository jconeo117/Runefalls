---
name: session-end
description: Cierra una sesión de desarrollo correctamente. Guarda el estado, registra decisiones técnicas tomadas, y prepara el contexto para la próxima sesión. Invocar siempre antes de cerrar Claude Code.
invocation: explicit
---

## Tu tarea

### Paso 1 — Recopilar el estado de la sesión

Lee:
- `.claude/sprint-active.md` — tareas del sprint y su estado
- Los últimos archivos modificados en esta sesión (usa el historial de herramientas)

### Paso 2 — Generar el resumen de sesión

Añade al final de `.claude/sprint-active.md` en la sección "Notas de sesión":

```markdown
### Sesión [fecha y hora aproximada]
**Tareas trabajadas:** [lista]
**Estado:** [completada / en progreso / bloqueada]
**Archivos modificados:** [lista de los más importantes]
**Decisiones técnicas:** [cualquier decisión de diseño o arquitectura tomada]
**Pendiente para próxima sesión:** [qué falta exactamente para completar la tarea en curso]
**Problemas encontrados:** [errores, bugs, o bloqueos que quedaron sin resolver]
```

### Paso 3 — Actualizar CLAUDE.md

Actualiza la sección "Estado actual del sprint" con:
- Sprint activo
- Tarea en curso y su porcentaje estimado de avance
- Próximo paso concreto

### Paso 4 — Mini auditoría de lo trabajado hoy

Solo para los archivos modificados en esta sesión, verifica rápidamente:
- ¿Algún `FindObjectOfType` o `GameObject.Find` se coló?
- ¿Algún MonoBehaviour en carpeta de dominio?
- ¿Algún TODO sin registrar?

Si encuentra algo: reporta y añade a `.claude/audit-report.md` si existe.

### Paso 5 — Reporte de cierre

```
📋 SESIÓN CERRADA
━━━━━━━━━━━━━━━━━━━━━━━━━━━
Sprint: X | Tarea: X.Y
Estado de la tarea: [%] completada

✅ Hecho hoy: [resumen en 1 línea]
⏭️  Próxima sesión: [acción concreta, no vaga]
⚠️  Pendientes: [si hay algo sin resolver]
━━━━━━━━━━━━━━━━━━━━━━━━━━━
Recuerda ejecutar /sprint-start al iniciar la próxima sesión
si cambias de sprint, o continúa con /task-done cuando termines.
```
