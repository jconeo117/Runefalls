---
name: task-done
description: Marca una tarea del sprint como completada. Audita que el código entregado cumple los criterios de aceptación antes de marcarla. Actualiza el estado en sprint-active.md y CLAUDE.md. Invocar cuando terminas de implementar una tarea.
invocation: explicit
---

Se te invocó con: `/task-done $ARGUMENTS`

El argumento es el ID de tarea (ej: `2.1`, `4.3`). Si no se proporcionó, pregunta cuál.

## Tu tarea

### Paso 1 — Leer el contexto
- Abre `.claude/sprint-active.md` para conocer la tarea activa
- Abre `docs/RUNEFALL_Sprints.md` y localiza la tarea indicada
- Lee el objetivo técnico y los criterios de la tarea

### Paso 2 — Auditoría de código

Para cada archivo relevante que se haya creado/modificado en esta tarea:

**Verificaciones obligatorias:**
- [ ] ¿La clase está en la carpeta correcta según la arquitectura? (`Core/`, `Combat/`, `Enemies/`, etc.)
- [ ] ¿Los MonoBehaviours están en `Presentation/` y no contienen lógica de dominio?
- [ ] ¿Las clases de dominio NO heredan de `MonoBehaviour`?
- [ ] ¿Los sistemas se comunican con `GameEvent<T>` y no con referencias directas?
- [ ] ¿Hay algún `FindObjectOfType` o `GameObject.Find` en el código nuevo? (prohibido)
- [ ] ¿Las interfaces están definidas para los sistemas principales?
- [ ] ¿El código nuevo sigue las convenciones de nombre del proyecto?

**Si encuentra violaciones:** Lista cada una con el archivo y línea. No marques la tarea como completada hasta que se corrijan.

### Paso 3 — Verificar el entregable

Confirma que el entregable verificable del sprint plan está cumplido:
- Describe qué existe ahora que antes no existía
- Si hay algo que falta del entregable, indícalo explícitamente

### Paso 4 — Actualizar estado (solo si la auditoría pasó)

En `.claude/sprint-active.md`:
- Cambia `- [ ] Tarea X.Y` a `- [x] Tarea X.Y ✓`
- Añade en "Notas de sesión": fecha, qué se hizo, alguna decisión técnica relevante

En `CLAUDE.md`:
- Actualiza el contador de tareas completadas
- Si corresponde, añade archivos clave nuevos a la sección de arquitectura

### Paso 5 — Reporte al usuario

```
✅ Tarea X.Y completada — [nombre]
📁 Archivos creados/modificados: [lista]
🔍 Auditoría: PASÓ / FALLÓ (con detalle)
⏭️  Siguiente tarea recomendada: Tarea X.Z — [nombre]
```
