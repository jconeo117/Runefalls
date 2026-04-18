---
name: new-class
description: Crea una nueva clase C# siguiendo la arquitectura de RUNEFALL. Determina automáticamente si es dominio o presentación, la carpeta correcta, y genera el boilerplate apropiado con interfaces si aplica. Invocar cuando necesites crear un archivo nuevo.
invocation: explicit
---

Se te invocó con: `/new-class $ARGUMENTS`

Formato esperado: `NombreClase TipoClase`

Tipos válidos: `domain`, `presenter`, `scriptableobject`, `monobehaviour`, `interface`, `enum`

Si no se proporciona el tipo, dedúcelo del nombre según estas reglas:
- Termina en `Data` → `scriptableobject`
- Termina en `Presenter` o `View` → `presenter`
- Empieza con `I` mayúscula → `interface`
- Termina en `Controller` o `Manager` → pregunta si es dominio o presentación
- Cualquier otra cosa → `domain`

---

## Proceso

### 1. Determinar ubicación

Según el tipo y nombre, determina la carpeta en `Assets/_Project/Scripts/`:

| Tipo | Carpeta |
|---|---|
| Clases de combate | `Combat/` |
| Clases de personaje | `Characters/` |
| Clases de enemigo | `Enemies/` |
| Clases de dungeon | `Dungeon/` |
| Clases de gacha | `Gacha/` |
| Clases de resonancia | `Resonance/` |
| MonoBehaviours de movimiento/cámara | `Presentation/Player/` |
| MonoBehaviours de UI | `Presentation/UI/` |
| MonoBehaviours de enemigos | `Presentation/Enemies/` |
| ScriptableObjects | `ScriptableObjects/[categoría]/` |
| Core/infraestructura | `Core/` |

Si no está claro, pregunta antes de crear.

### 2. Generar el boilerplate según tipo

**domain:**
```csharp
// [Carpeta]/[NombreClase].cs
// Clase de dominio — sin MonoBehaviour, sin referencias a UnityEngine salvo Mathf/Random/Vector3

public class [NombreClase] {
    // Eventos del dominio
    public event System.Action OnChanged;

    public [NombreClase]() {
    }
}
```

**scriptableobject:**
```csharp
// ScriptableObjects/[categoría]/[NombreClase].cs
using UnityEngine;

[CreateAssetMenu(menuName = "Runefall/[Categoría]/[Nombre sin Data]")]
public class [NombreClase] : ScriptableObject {
    [Header("Identity")]
    // campos aquí
}
```

**presenter (MonoBehaviour de presentación):**
```csharp
// Presentation/.../[NombreClase].cs
using UnityEngine;

// Presenter — solo muestra estado, no toma decisiones de juego
public class [NombreClase] : MonoBehaviour {
    // Referencias a componentes Unity (Animator, Slider, etc.)

    // Recibe referencia al modelo de dominio y se suscribe a sus eventos
    public void Init([TipoDominio] model) {
        // model.OnAlgo += HandleAlgo;
    }

    private void OnDestroy() {
        // Desuscribirse de todos los eventos
    }
}
```

**interface:**
```csharp
// Core/Interfaces/[NombreClase].cs
public interface [NombreClase] {
    // Métodos públicos del contrato
}
```

### 3. Verificación antes de crear

Antes de escribir el archivo, confirma con el usuario:
- Ruta completa donde se creará
- Tipo de clase
- Si es dominio, confirma que NO hereda de MonoBehaviour

### 4. Actualizar CLAUDE.md

Si la clase es un sistema principal (no un helper), añádela a la sección de arquitectura en CLAUDE.md.
