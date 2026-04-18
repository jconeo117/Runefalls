# CharacterModel — Design Spec
**Fecha:** 2026-04-15
**Sprint:** 2 — Tarea 2.1
**Estado:** Aprobado

---

## Alcance

Clase C# pura (sin MonoBehaviour, sin dependencias de UnityEngine) que representa el estado de un personaje: HP, MP y stats base. Versión mínima evolutiva — se expande con `CharacterData`, `CharacterStats`, `CardInstance`, etc. en Sprint 3+.

---

## Clase

**Archivo:** `Assets/_Project/Scripts/Characters/CharacterModel.cs`
**Namespace:** `Runefall.Characters`

### Constructor

```csharp
CharacterModel(string name, float maxHP, float maxMP, float baseAttack, float baseDefense)
```

Inicializa `CurrentHP = maxHP` y `CurrentMP = maxMP`.

### Estado (propiedades de solo lectura)

| Propiedad | Tipo | Descripción |
|-----------|------|-------------|
| `Name` | `string` | Nombre del personaje |
| `CurrentHP` | `float` | HP actual |
| `MaxHP` | `float` | HP máximo |
| `CurrentMP` | `float` | MP actual |
| `MaxMP` | `float` | MP máximo |
| `BaseAttack` | `float` | Ataque base (se convertirá en CharacterStats.attack en Sprint 3) |
| `BaseDefense` | `float` | Defensa base |
| `IsAlive` | `bool` | `CurrentHP > 0` |

### Métodos públicos

| Método | Comportamiento |
|--------|----------------|
| `TakeDamage(float amount)` | Reduce HP, clampea a 0, dispara `OnHPChanged` |
| `Heal(float amount)` | Aumenta HP, clampea a MaxHP, dispara `OnHPChanged` |
| `UseMP(float amount)` | Reduce MP, clampea a 0, dispara `OnMPChanged` |
| `RestoreMP(float amount)` | Aumenta MP, clampea a MaxMP, dispara `OnMPChanged` |

### Eventos C#

```csharp
public event Action<float> OnHPChanged;
public event Action<float> OnMPChanged;
```

La capa de Presentación suscribe. El modelo nunca conoce a sus suscriptores.

---

## Tests

Archivo: `Assets/_Project/Tests/EditMode/CharacterModelTests.cs`
Assembly: `Runefall.Tests.EditMode` (ya existe)

Sin GameObject — la clase es C# puro, NUnit directo.

Casos: HP inicial = maxHP, TakeDamage reduce, no baja de 0, Heal aumenta, no supera maxHP, UseMP reduce, no baja de 0, RestoreMP aumenta, no supera maxMP, IsAlive, evento OnHPChanged se dispara, evento OnMPChanged se dispara.

---

## Lo que NO incluye

- `CharacterData` SO — Sprint 3
- `CharacterStats` wrapper — Sprint 3
- `ElementType`, afinidades, daño elemental — Sprint 4
- `CardInstance`, `Hand`, ultimate gauge — Sprint 2.3+
- `StatusEffect`, `RuneData` — Sprint 3+
- `CombatFormulas` — Sprint 4
