# Audit Report — 2026-05-27
**Sprint activo:** 6 | **Tareas completadas:** 2/6 (6.2 ~30%)

## Resumen ejecutivo
🟢 Arquitectura sólida — separación de capas excelente, convenciones perfectas, zero violaciones críticas
🟡 CombatBootstrapper: 767 líneas, viola SRP (ya identificado en S5, creció)
🟡 EffectDefinition: campo `Sprite icon` en clase de dominio

## Violaciones Críticas (bloquean merge)
_Ninguna._

## Advertencias (revisar antes del siguiente sprint)

### ⚠ CombatBootstrapper — God Object (SRP)
**Archivo:** `Presentation/Combat/CombatBootstrapper.cs` (~767 líneas)
- Responsabilidades mezcladas: construir contexto, instanciar UI, cablear eventos, animar HP bars, manejar cámara, restaurar exploración
- 11 referencias `[SerializeField]` a MonoBehaviours de presentación
- Funciona pero dificulta testing y mantenimiento

### ⚠ EffectDefinition.icon — acoplamiento dominio→presentación
**Archivo:** `Combat/Effects/EffectDefinition.cs:20`
```csharp
public Sprite icon;  // campo de presentación en clase de dominio
```
- Sprite es un asset de presentación; la clase abstracta de dominio no debería conocerlo
- No bloquea nada hoy pero crea dependencia cruzada

## Deuda técnica (TODO/HACK/FIXME)
_Ninguna encontrada. Código limpio._

## Métricas
- Archivos de dominio: 52
- Archivos de presentación: 43
- Total archivos .cs auditados: 95
- Archivos con violaciones críticas: 0
- Archivos con advertencias: 2
- MonoBehaviour en dominio: 0 ✅
- FindObjectOfType/Find en dominio: 0 ✅
- GetComponent en dominio: 0 ✅
- TODO/HACK/FIXME: 0 ✅
- Cobertura de interfaces naming: 100% ✅

## Positivos destacados
- ✅ Interfaces: prefijo `I` aplicado en todas (ICombatActor, IEnemyPhaseAnimator, IImpactTrigger…)
- ✅ Eventos: sufijo correcto en GameEvent<T> SOs
- ✅ ScriptableObjects: sufijo `Data` en todos (CharacterData, SkillData, EnemyData…)
- ✅ Sin MonoBehaviour en ninguna clase de dominio
- ✅ ServiceLocator usado correctamente en 3 puntos
- ✅ GameEvent<T> para comunicación entre sistemas via SkillEventBridge
- ✅ [CreateAssetMenu] aplicado en todos los SOs
- ✅ Cero lógica de negocio en capa Presentation
- ✅ Dependency injection en TurnManager, CombatAnimationDriver (IEnemyPhaseAnimator)

## Recomendaciones
1. **[ALTA] Fraccionar CombatBootstrapper** — Extraer `CombatContextBuilder` (dominio puro) y `CombatUIFactory` (presentación) para que el Bootstrapper sea un orchestrator delgado. Esfuerzo: ~3-4h. Candidato para S6.5 (pulidos).
2. **[MEDIA] Mover `EffectDefinition.icon` a capa de presentación** — Crear `EffectDisplay` SO en Presentation que wrappee `EffectDefinition` + Sprite + displayName, o mover `icon` a un SO hermano de presentación. Esfuerzo: ~1-2h.
3. **[MEDIA] Tests de integración para pipeline de combate** — `TurnManager`, `CombatResolver`, `CombatFormulas` son dominio puro y testables sin MonoBehaviour. Crear `Tests/CombatResolverTests.cs`, `Tests/TurnManagerTests.cs`. Esfuerzo: ~4-6h.
