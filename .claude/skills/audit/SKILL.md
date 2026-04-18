---
name: audit
description: Auditoría completa de arquitectura del proyecto RUNEFALL. Verifica separación de capas, convenciones, acoplamiento, y genera un reporte de deuda técnica. Invocar cuando quieras saber el estado real del código.
invocation: explicit
---

Se te invocó con: `/audit $ARGUMENTS`

Argumentos opcionales: `quick` (solo convenciones), `deep` (análisis completo), o vacío (deep por defecto).

## Tu tarea

Analiza todos los archivos `.cs` en `Assets/_Project/Scripts/` y genera un reporte de auditoría.

---

### AUDITORÍA DE ARQUITECTURA

#### 1. Separación de capas (CRÍTICO)

Busca en cada archivo:

**Violaciones de capa de Dominio** (archivos en `Core/`, `Combat/`, `Characters/`, `Enemies/`, `Dungeon/`, `Gacha/`, `Resonance/`):
- `MonoBehaviour` en clases de dominio → VIOLACIÓN CRÍTICA
- `using UnityEngine` excepto `UnityEngine.Mathf`, `UnityEngine.Random`, `UnityEngine.Vector3` → SOSPECHOSO
- `GameObject`, `Transform`, `Animator`, `Rigidbody` como campos → VIOLACIÓN CRÍTICA
- `FindObjectOfType`, `GameObject.Find`, `GetComponent` → VIOLACIÓN CRÍTICA

**Violaciones de capa de Presentación** (archivos en `Presentation/`):
- Lógica de negocio (cálculos de daño, reglas de juego) → VIOLACIÓN
- Llamadas directas entre MonoBehaviours sin pasar por eventos → ACOPLAMIENTO

#### 2. Sistema de eventos

- ¿Los sistemas usan `GameEvent<T>` para comunicarse?
- ¿Hay referencias directas `[SerializeField]` entre sistemas de dominio distintos? → ACOPLAMIENTO
- ¿El `ServiceLocator` está siendo usado correctamente?

#### 3. Convenciones de nombre

Verifica en TODOS los archivos:
- Interfaces con prefijo `I` → `ICombatSystem`, no `CombatSystemInterface`
- Eventos con sufijo `Event` → `OnHPChangedEvent`
- ScriptableObjects con sufijo `Data` → `CharacterData`, `WeaponData`
- MonoBehaviours de presentación sin sufijo especial → `PlayerController`, `CombatHUDPresenter`

#### 4. ScriptableObjects

- ¿Tienen `[CreateAssetMenu]`?
- ¿Referencias a `GameObject` o `Prefab` están SOLO en los SO de datos, no en clases de dominio?

#### 5. Deuda técnica acumulada

Lista todos los `// TODO`, `// HACK`, `// FIXME` encontrados con su archivo y línea.

---

### FORMATO DEL REPORTE

Genera `.claude/audit-report.md` con:

```markdown
# Audit Report — [fecha]
**Sprint activo:** X | **Tareas completadas:** N

## Resumen ejecutivo
🟢 Bien | 🟡 Atención | 🔴 Crítico

## Violaciones Críticas (bloquean merge)
[lista con archivo:línea y descripción]

## Advertencias (revisar antes del siguiente sprint)
[lista]

## Deuda técnica (TODO/HACK/FIXME)
[lista]

## Métricas
- Archivos de dominio: N
- Archivos de presentación: N  
- Archivos con violaciones: N
- Cobertura de interfaces: N/M sistemas

## Recomendaciones
[máximo 3 acciones concretas priorizadas]
```

Después de generar el archivo, muestra el resumen ejecutivo en consola.
