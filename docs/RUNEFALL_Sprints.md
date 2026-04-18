# RUNEFALL — Sprint Plan Técnico
**Versión:** 2.0 · Abril 2026  
**Duración total:** 8 semanas (6 Abril — 1 Junio 2026)  
**Desarrollador:** 1 persona  
**Motor:** Unity 6000.0.30f1 LTS · C#  
**Filosofía:** Data-driven, asset-agnostic, SOLID, arquitectura limpia

> **Estado:** S1 ✅ completado · S2 ✅ completado · S3 🔄 en curso  
> **Visión de combate confirmada:** Exploración 3D tercera persona → transición → pantalla de combate 2.5D lateral (estilo 7DS Grand Cross) · MVP con 1 personaje activo en combate · Equipos múltiples post-MVP · Enemigos reaparecen al salir de sala

---

## Principios de Arquitectura

Antes de los sprints, hay decisiones de arquitectura que aplican a **todo** el proyecto y que deben estar claras desde el día 1. Estas decisiones son las que hacen que el sistema sea intercambiable con cualquier bundle de assets.

### Asset-Agnostic por diseño

Toda la lógica del juego vive en C# puro, sin referencias directas a `GameObject`, `Animator` ni `Sprite`. Los sistemas de juego no saben que Unity existe. Unity es solo el **renderer y el input provider**.

El patrón que hace esto posible se llama **Humble Object**: los MonoBehaviours son cascarones que delegan toda la lógica a clases C# puras.

```
┌─────────────────────────────────────────────┐
│             CAPA DE PRESENTACIÓN            │
│  MonoBehaviours · Animators · Particle FX   │
│  Solo saben MOSTRAR. No toman decisiones.   │
├─────────────────────────────────────────────┤
│              CAPA DE DOMINIO                │
│  CharacterModel · CombatSystem · CardSystem │
│  C# puro. Sin MonoBehaviour. Testeable.     │
├─────────────────────────────────────────────┤
│             CAPA DE DATOS                   │
│  ScriptableObjects · JSON local             │
│  Personajes, armas, runas, enemigos.        │
│  Cambiar assets = cambiar SO. Zero código.  │
└─────────────────────────────────────────────┘
```

### ScriptableObjects como fuente de verdad

Cada entidad del juego es un `ScriptableObject`. Cambiar un bundle de assets significa crear nuevos SO con las referencias visuales nuevas. La lógica no se toca.

```csharp
// Esto es todo lo que cambia al cambiar un bundle de assets:
[CreateAssetMenu]
public class CharacterData : ScriptableObject {
    public string characterName;
    public Sprite portrait;          // ← única referencia visual
    public GameObject prefab;        // ← único prefab
    public ElementType element;
    public CharacterStats baseStats;
    public SkillData skill1;
    public SkillData skill2;
    public UltimateData ultimate;
    public PassiveData passive;
}
```

### Event-driven con ScriptableObject Events

Los sistemas se comunican a través de eventos tipados, sin referencias directas entre sí. Esto permite que el sistema de combate no sepa que existe un sistema de UI, y viceversa.

```csharp
// Un GameEvent<T> es un ScriptableObject que cualquier sistema puede raise/listen
public class GameEvent<T> : ScriptableObject {
    private List<Action<T>> listeners = new();
    public void Raise(T value) => listeners.ForEach(l => l(value));
    public void Subscribe(Action<T> listener) => listeners.Add(listener);
    public void Unsubscribe(Action<T> listener) => listeners.Remove(listener);
}
```

---

## Calendario de Sprints

| Sprint | Semanas | Foco |
|---|---|---|
| S1 | Sem 1 | Fundamentos: proyecto, arquitectura base, input, cámara |
| S2 | Sem 2 | Movimiento, personaje, animación desacoplada |
| S3 | Sem 3 | Sistema de datos: SO de personajes, armas, cartas, enemigos |
| S4 | Sem 4 | Sistema de combate por turnos y lógica de cartas |
| S5 | Sem 5 | IA de enemigos: Behavior Trees |
| S6 | Sem 6 | Dungeon procedural + progresión de run |
| S7 | Sem 7 | Sistema Gacha local + Resonancia + UI funcional |
| S8 | Sem 8 | Integración, polish, Director de IA, build final |

---

## SPRINT 1 — Fundamentos del Proyecto ✅ COMPLETADO
**Semana 1 · 6–12 Abril**  
**Objetivo:** Proyecto limpio con arquitectura definida, input robusto y cámara funcional.

> **Estado real implementado:**
> - ✅ Tarea 1.1 — Proyecto Unity 6000.0.30f1 creado con estructura de carpetas
> - ✅ Tarea 1.2 — ServiceLocator + GameEvent\<T\> funcionando
> - ✅ Tarea 1.3 — InputReader ScriptableObject funcional
> - ✅ Tarea 1.4 — CameraManager con `cam_exploration` (OrbitalFollow), `cam_combat` (Follow) y `cam_lookon` (LookAt)
> - ⚠️ Assembly Definitions **no configurados** — todo en un assembly. Deuda técnica registrada, no bloquea el MVP.

---

### Tarea 1.1 — Setup del Proyecto Unity

**Objetivo técnico:** Proyecto configurado para escalar sin deuda técnica desde el inicio.

**Pasos:**
1. Crear proyecto Unity 6000.0.30f1 LTS con template 3D (URP)
2. Configurar estructura de carpetas:
```
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Core/          # GameManager, ServiceLocator, EventBus
│   │   ├── Characters/    # CharacterModel, Stats, PassiveSystem
│   │   ├── Combat/        # CombatSystem, TurnManager, CardSystem
│   │   ├── Enemies/       # EnemyModel, BehaviorTree, AIDirector
│   │   ├── Dungeon/       # RoomGenerator, BSPSplitter, RoomData
│   │   ├── Gacha/         # GachaSystem, Pool, PityTracker
│   │   ├── Resonance/     # ResonanceDetector, ResonanceData
│   │   └── Presentation/  # MonoBehaviours, solo referencias a dominio
│   ├── ScriptableObjects/
│   │   ├── Characters/
│   │   ├── Weapons/
│   │   ├── Cards/
│   │   ├── Enemies/
│   │   ├── Events/
│   │   └── Resonance/
│   ├── Prefabs/
│   ├── Scenes/
│   └── Tests/
```
3. Instalar y verificar packages:
   - **Mirror Networking** — via Package Manager (Add from git URL)
   - **Input System** — ya incluido en Unity 6, solo habilitar en Project Settings
   - **Cinemachine 3.x** — incluido en Unity 6. **Namespace cambiado:** `using Unity.Cinemachine;` (no `using Cinemachine;`)
   - **TextMeshPro** — incluido en Unity 6 via `com.unity.textmeshpro`
   - **MCP Unity** — nativo en Unity 6: `Edit > Project Settings > AI > Unity MCP` (no requiere paquete externo)
4. Configurar **Assembly Definitions** para separar dominio de presentación

> **Notas de compatibilidad Unity 6 — leer antes de escribir código:**
> - `CinemachineFreeLook` y `CinemachineVirtualCamera` fueron reemplazados por `CinemachineCamera` con `CinemachineOrbitalFollow` y `CinemachineFollow`.
> - `Object.FindObjectOfType` está **obsoleto** → usar `Object.FindFirstObjectByType` si es absolutamente necesario en `Presentation/` (sigue prohibido en dominio).
> - **Render Graph** es el pipeline por defecto en URP Unity 6 → no usar `CommandBuffer` legacy.
> - `UnityWebRequest` requiere `using UnityEngine.Networking;` igual que antes.
> - El nuevo **Input System** no necesita instalación separada en Unity 6.

**Entregable:** Proyecto abre sin errores con la estructura de carpetas lista.

---

### Tarea 1.2 — ServiceLocator y EventBus

**Objetivo técnico:** Los sistemas se comunican sin acoplamiento directo.

**Por qué no Singleton:** Los Singletons crean dependencias ocultas y dificultan los tests. El ServiceLocator centraliza el registro pero permite sustituir implementaciones.

```csharp
// Core/ServiceLocator.cs
public static class ServiceLocator {
    private static Dictionary<Type, object> services = new();

    public static void Register<T>(T service) =>
        services[typeof(T)] = service;

    public static T Get<T>() {
        if (services.TryGetValue(typeof(T), out var s)) return (T)s;
        throw new Exception($"Service {typeof(T).Name} not registered.");
    }
}

// Uso: ServiceLocator.Register<ICombatSystem>(new CombatSystem());
// Uso: var combat = ServiceLocator.Get<ICombatSystem>();
```

```csharp
// Core/GameEvent.cs — ScriptableObject tipado
[CreateAssetMenu(menuName = "Events/GameEvent")]
public class GameEvent : ScriptableObject {
    private readonly List<Action> listeners = new();
    public void Raise() => listeners.ForEach(l => l?.Invoke());
    public void Subscribe(Action l) => listeners.Add(l);
    public void Unsubscribe(Action l) => listeners.Remove(l);
}

[CreateAssetMenu(menuName = "Events/GameEvent<T>")]
public class GameEvent<T> : ScriptableObject {
    private readonly List<Action<T>> listeners = new();
    public void Raise(T val) => listeners.ForEach(l => l?.Invoke(val));
    public void Subscribe(Action<T> l) => listeners.Add(l);
    public void Unsubscribe(Action<T> l) => listeners.Remove(l);
}
```

**Entregable:** ServiceLocator y EventBus con tests unitarios pasando.

---

### Tarea 1.3 — Input System

**Objetivo técnico:** Input completamente desacoplado del movimiento. Un `InputReader` SO lee inputs y levanta eventos. Nada más lee `Input` directamente.

```csharp
// Core/InputReader.cs
[CreateAssetMenu(menuName = "Input/InputReader")]
public class InputReader : ScriptableObject, GameInputActions.IPlayerActions {
    private GameInputActions inputActions;

    public event Action<Vector2> MoveEvent;
    public event Action DodgeEvent;
    public event Action InteractEvent;
    public event Action<int> UseCardEvent; // 0=skill1, 1=skill2, 2=ult

    private void OnEnable() {
        inputActions ??= new GameInputActions();
        inputActions.Player.SetCallbacks(this);
        inputActions.Player.Enable();
    }

    public void OnMove(InputAction.CallbackContext ctx) =>
        MoveEvent?.Invoke(ctx.ReadValue<Vector2>());

    public void OnDodge(InputAction.CallbackContext ctx) {
        if (ctx.performed) DodgeEvent?.Invoke();
    }
}
```

**Entregable:** `InputReader` SO funcional. Se puede asignar en el inspector a cualquier MonoBehaviour sin que el MB sepa de Unity Input System directamente.

---

### Tarea 1.4 — Camera System

**Objetivo técnico:** Dos modos de cámara con transición automática: libre en exploración, estática sobre el hombro en combate.

**Comportamiento definido:**

- **Exploración** → cámara orbital libre. El jugador controla el ángulo horizontal y vertical con mouse/stick derecho. Sigue al personaje con damping suave.
- **Combate** → cámara estática detrás y encima del hombro del jugador, apuntando al enemigo. No se puede mover durante el combate. La transición desde exploración es un blend de 0.5s. Al terminar el combate vuelve a exploración con blend de 0.3s.
- **Sin lock-on separado** — la cámara de combate apunta al enemigo directamente. Si hay múltiples enemigos apunta al centroide del grupo.

**Diseño:** Cinemachine 3.x hace el trabajo pesado. El `CameraManager` es el único sistema que habla con Cinemachine. En Unity 6 el namespace es `Unity.Cinemachine`.

```csharp
// Presentation/Player/CameraManager.cs
using Unity.Cinemachine;
using UnityEngine;

public class CameraManager : MonoBehaviour {
    [Header("Cámaras virtuales")]
    [SerializeField] private CinemachineCamera explorationCam;
    // explorationCam requiere en el inspector:
    //   - CinemachineOrbitalFollow (follow = PlayerTransform)
    //   - CinemachineRotationComposer (look at = PlayerTransform)
    //   - CinemachineDeoccluder

    [SerializeField] private CinemachineCamera combatCam;
    // combatCam requiere en el inspector:
    //   - CinemachineFollow (follow = CombatRig, un Transform hijo del jugador)
    //   - CinemachineHardLookAt (look at = EnemyTarget, asignado en runtime)

    [Header("Blend")]
    [SerializeField] private float blendToCombar  = 0.5f;
    [SerializeField] private float blendToExplore = 0.3f;

    private CinemachineBrain brain;

    private void Awake() {
        brain = Camera.main.GetComponent<CinemachineBrain>();
        explorationCam.Priority = 10;
        combatCam.Priority = 0;
    }

    public void EnterCombat(Transform enemyTarget) {
        // Apuntar la cam de combate al enemigo (o centroide si hay varios)
        var hardLookAt = combatCam.GetComponent<CinemachineHardLookAt>();
        if (hardLookAt != null) hardLookAt.LookAtTarget = enemyTarget;

        brain.DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Styles.EaseInOut, blendToCombar);

        combatCam.Priority = 20;
        explorationCam.Priority = 0;
    }

    public void ExitCombat() {
        brain.DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Styles.EaseOut, blendToExplore);

        explorationCam.Priority = 10;
        combatCam.Priority = 0;
    }
}
```

**Valores de partida para el inspector — ajustar en runtime:**

| Parámetro | Cámara exploración | Cámara combate |
|---|---|---|
| Follow offset Y | 1.5 | 1.8 |
| Follow offset Z | -3.5 | -3.0 |
| Vertical axis (tilt) | -15° a 60° | fijo en 15° |
| Damping position | 0.5 | 0.0 (estática) |
| Damping rotation | 0.3 | 0.0 (estática) |
| FOV | 60° | 55° (ligeramente más cerrado, focaliza el combate) |

**CombatRig — qué es y cómo se configura:**
`CombatRig` es un `Transform` vacío hijo del jugador, posicionado en `(0, 1.8, -3.0)` en local space. La cámara de combate lo sigue como punto de anclaje fijo. Esto garantiza que la cámara esté siempre detrás del hombro sin importar cómo esté orientado el personaje.

**Flujo de activación:**
El `TurnManager` llama a `CameraManager.EnterCombat()` cuando inicia el primer turno, y `ExitCombat()` cuando el combate termina. El `CameraManager` no sabe nada de combate, solo recibe el Transform del enemigo.

```
EnemyDetector detecta colisión con jugador
        ↓
TurnManager.StartCombat(EnemyModel enemy)
        ↓
CameraManager.EnterCombat(enemy.Transform)  ← único acoplamiento permitido
        ↓
Cinemachine blend 0.5s → cámara estática sobre el hombro apuntando al enemigo
        ↓
[turnos de combate]
        ↓
TurnManager.EndCombat()
        ↓
CameraManager.ExitCombat()
        ↓
Cinemachine blend 0.3s → cámara libre orbital
```

**Entregable:** Cámara libre en exploración. Al simular entrada a combate (tecla de prueba), transiciona a cámara estática sobre el hombro apuntando a un dummy. Al salir, vuelve a exploración con blend suave.

---

## SPRINT 2 — Movimiento y Personaje ✅ COMPLETADO
**Semana 2 · 13–19 Abril**  
**Objetivo:** Personaje moviéndose con sensación de peso, dodge funcional, animaciones desacopladas.

> **Estado real implementado:**
> - ✅ Tarea 2.1 — CharacterModel C# puro implementado
> - ✅ Tarea 2.2 — PlayerController con CharacterController, movimiento fluido con aceleración/desaceleración y rotación hacia dirección de movimiento
> - ✅ Tarea 2.3 — AnimationController desacoplado implementado
> - ⚠️ Salto y Dash **no implementados** — se añaden al inicio del Sprint 3 como Tarea 3.0 antes de continuar con los SO. Son prerequisitos para la pantalla de exploración.

---

### Tarea 2.1 — CharacterModel (dominio puro)

**Por qué primero el dominio:** Si empezamos con el MonoBehaviour, mezclamos lógica con presentación y jamás los separamos. Primero la lógica, después el visualizador.

```csharp
// Characters/CharacterModel.cs — C# puro, sin MonoBehaviour
public class CharacterModel {
    public CharacterData Data { get; }
    public CharacterStats CurrentStats { get; private set; }
    public float CurrentHP { get; private set; }
    public float CurrentMP { get; private set; }
    public ElementType Affinity => Data.element;

    // Estado de combate
    public List<CardInstance> Hand { get; private set; } = new();
    public float UltimateGauge { get; private set; }
    public List<StatusEffect> ActiveEffects { get; private set; } = new();
    public List<RuneData> EquippedRunes { get; private set; } = new();

    // Eventos internos que la capa de presentación puede escuchar
    public event Action<float> OnHPChanged;
    public event Action<float> OnUltGaugeChanged;
    public event Action<StatusEffect> OnStatusApplied;

    public CharacterModel(CharacterData data) {
        Data = data;
        CurrentStats = data.baseStats.Clone();
        CurrentHP = CurrentStats.maxHP;
    }

    public void TakeDamage(float amount, ElementType source) {
        float mitigated = CombatFormulas.CalculateDamage(amount, CurrentStats.defense, source, Affinity);
        CurrentHP = Mathf.Max(0, CurrentHP - mitigated);
        OnHPChanged?.Invoke(CurrentHP);
    }

    public void ChargeUltimate(float amount) {
        UltimateGauge = Mathf.Min(1f, UltimateGauge + amount);
        OnUltGaugeChanged?.Invoke(UltimateGauge);
        if (UltimateGauge >= 1f) Hand.Add(new CardInstance(Data.ultimate));
    }
}
```

---

### Tarea 2.2 — CharacterController y Movimiento

**Objetivo técnico:** Movimiento fluido con sensación de peso, aceleración y deceleración suaves, rotate-to-move.

```csharp
// Presentation/PlayerController.cs
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour {
    [SerializeField] private InputReader input;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float deceleration = 12f;

    private CharacterController cc;
    private Vector3 velocity;
    private Vector3 moveDirection;
    private float currentSpeed;

    // El personaje rota hacia donde se mueve, no hacia la cámara
    // La cámara es independiente del personaje

    private void Update() {
        ApplyGravity();
        HandleMovement();
        HandleRotation();
    }

    private void HandleMovement() {
        // Convertir input 2D a dirección 3D relativa a la cámara
        Vector3 camForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
        Vector3 camRight = Camera.main.transform.right;
        moveDirection = (camForward * input.MoveAxis.y + camRight * input.MoveAxis.x).normalized;

        float targetSpeed = moveDirection.magnitude > 0.1f ? moveSpeed : 0f;
        float accel = targetSpeed > currentSpeed ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.deltaTime);

        cc.Move(moveDirection * currentSpeed * Time.deltaTime + velocity * Time.deltaTime);
    }

    private void HandleRotation() {
        if (moveDirection.sqrMagnitude < 0.01f) return;
        Quaternion target = Quaternion.LookRotation(moveDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
    }
}
```

---

### Tarea 2.3 — AnimationController desacoplado

**Objetivo técnico:** El Animator no sabe de lógica de juego. Recibe comandos simples de un `AnimationController` que escucha eventos del dominio.

```csharp
// Presentation/CharacterAnimationController.cs
public class CharacterAnimationController : MonoBehaviour {
    [SerializeField] private Animator animator;

    // Hashes precalculados — más rápido que strings
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int DodgeHash = Animator.StringToHash("Dodge");
    private static readonly int AttackHash = Animator.StringToHash("AttackIndex");

    // El MB recibe referencia al dominio, nunca al revés
    public void Init(CharacterModel model) {
        model.OnStatusApplied += HandleStatus;
    }

    public void SetSpeed(float speed) =>
        animator.SetFloat(SpeedHash, speed, 0.1f, Time.deltaTime);

    public void PlaySkill(int skillIndex) =>
        animator.SetTrigger(AttackHash + skillIndex);

    public void PlayDodge() =>
        animator.SetTrigger(DodgeHash);
}
```

**Entregable:** Cubo o cápsula moviéndose con aceleración/deceleración, rotando hacia la dirección de movimiento, con cámara de tercera persona sin clip.

---

## SPRINT 3 — Movimiento Completo + Sistema de Datos
**Semana 3 · 20–26 Abril**  
**Objetivo:** Completar el movimiento de exploración (salto + dash) y definir todos los ScriptableObjects incluyendo el sistema de encuentro con enemigos.

---

### Tarea 3.0 — Salto y Dash (deuda del Sprint 2)

**Objetivo técnico:** Completar el `PlayerController` con salto y dash antes de avanzar. Son prerequisitos de la exploración.

**Salto** — se añade al `PlayerController` existente:

```csharp
// Añadir a Presentation/Player/PlayerController.cs
[Header("Jump")]
[SerializeField] private float jumpHeight = 1.8f;
[SerializeField] private float gravity = -20f;

private Vector3 verticalVelocity;
private bool isGrounded;

// En Update(), reemplazar ApplyGravity() con:
private void HandleVertical() {
    isGrounded = cc.isGrounded;
    if (isGrounded && verticalVelocity.y < 0f)
        verticalVelocity.y = -2f; // mantiene pegado al suelo

    if (input.JumpPressed && isGrounded)
        verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

    verticalVelocity.y += gravity * Time.deltaTime;
    cc.Move(verticalVelocity * Time.deltaTime);
}
```

**Dash** — estado separado con cooldown e invencibilidad temporal:

```csharp
[Header("Dash")]
[SerializeField] private float dashDistance = 4f;
[SerializeField] private float dashDuration = 0.15f;
[SerializeField] private float dashCooldown = 1.2f;

private bool isDashing;
private float dashCooldownTimer;

// El dash cancela la gravedad durante dashDuration
// La dirección es la última moveDirection, o forward si está quieto
// Durante el dash el jugador es invulnerable (flag en CharacterModel)
private IEnumerator PerformDash() {
    isDashing = true;
    float timer = 0f;
    Vector3 dir = moveDirection.sqrMagnitude > 0.01f
        ? moveDirection : transform.forward;

    while (timer < dashDuration) {
        cc.Move(dir * (dashDistance / dashDuration) * Time.deltaTime);
        timer += Time.deltaTime;
        yield return null;
    }
    isDashing = false;
    dashCooldownTimer = dashCooldown;
}
```

**Añadir al InputReader:**
```csharp
public event Action JumpEvent;
public event Action DashEvent;

public void OnJump(InputAction.CallbackContext ctx) {
    if (ctx.performed) JumpEvent?.Invoke();
}
public void OnDash(InputAction.CallbackContext ctx) {
    if (ctx.performed) DashEvent?.Invoke();
}
```

**Entregable:** Cápsula salta, cae con gravedad real, y hace dash direccional con cooldown visible.

---

### Tarea 3.1 — Jerarquía de ScriptableObjects

Esta es la tarea más importante del proyecto para la escalabilidad. Una vez bien definida, agregar un personaje nuevo toma 10 minutos en el inspector.

```csharp
// ScriptableObjects/Characters/CharacterData.cs
[CreateAssetMenu(menuName = "Runefall/Character")]
public class CharacterData : ScriptableObject {
    [Header("Identity")]
    public string characterName;
    public Sprite portrait;
    public GameObject prefab;          // intercambiable con cualquier asset
    public RarityType rarity;
    public ElementType element;

    [Header("Stats")]
    public CharacterStats baseStats;

    [Header("Skills")]
    public SkillData skill1;
    public SkillData skill2;
    public UltimateData ultimate;
    public PassiveData passive;

    [Header("Gacha")]
    [Range(0f, 1f)] public float baseDropRate;
}

// ScriptableObjects/Cards/SkillData.cs
[CreateAssetMenu(menuName = "Runefall/Skill")]
public class SkillData : ScriptableObject {
    public string skillName;
    public Sprite cardArt;             // intercambiable
    public SkillType type;             // Offensive, OffensiveEffect, Debuff, Support
    public ElementType element;
    public float ultimateChargeAmount;

    // Efectos por rango — el rango determina cuál se ejecuta
    public SkillEffect[] effectsByRank; // [0]=rango1, [1]=rango2, [2]=rango3
}

// ScriptableObjects/Cards/SkillEffect.cs
[System.Serializable]
public class SkillEffect {
    public float damageMultiplier;
    public StatusEffectData appliedStatus;  // null si no aplica estado
    public float statusChance;
    public float healPercent;               // para skills de soporte
    public string animationTrigger;         // trigger en el Animator de combate 2.5D

    [Header("Camera")]
    public CameraSequenceData cameraSequence; // SO con la secuencia de cámara para este rango
}

// ScriptableObjects/Combat/CameraSequenceData.cs
[CreateAssetMenu(menuName = "Runefall/Camera/CameraSequence")]
public class CameraSequenceData : ScriptableObject {
    public CameraShot[] shots;
    public float returnDuration = 0.3f; // tiempo para volver a cámara base tras la secuencia
}

[System.Serializable]
public class CameraShot {
    public CameraShotType type;
    public float duration;
    public float fov;
    public float blendIn;
    [Range(-1f, 1f)] public float dutchAngle;
}

public enum CameraShotType {
    Static,        // sin movimiento — Rango 1
    PushIn,        // acercamiento al personaje — Rango 2
    DynamicOrbit,  // órbita rápida alrededor del impacto — Rango 3
    OverShoulder,  // sobre el hombro mirando al enemigo
    LowAngle,      // ángulo bajo dramático — jefes
}


// ScriptableObjects/Weapons/WeaponData.cs
[CreateAssetMenu(menuName = "Runefall/Weapon")]
public class WeaponData : ScriptableObject {
    public string weaponName;
    public Sprite weaponArt;
    public GameObject weaponPrefab;
    public WeaponType weaponType;          // Sword, Staff, Dagger, Hammer
    public ElementType element;
    public RarityType rarity;
    public WeaponStats stats;
    public ConditionalBonus conditionalBonus;
    [Range(0f, 1f)] public float baseDropRate;
}

// ScriptableObjects/Cards/RuneData.cs
[CreateAssetMenu(menuName = "Runefall/Rune")]
public class RuneData : ScriptableObject {
    public string runeName;
    public Sprite runeArt;
    public RuneType runeType;        // Passive, ActiveSkill, Aura
    public ElementType element;
    public int slotCost;
    public RarityType rarity;
    public RuneEffect effect;
    [Range(0f, 1f)] public float baseDropRate;
}

// ScriptableObjects/Enemies/EnemyData.cs
[CreateAssetMenu(menuName = "Runefall/Enemy")]
public class EnemyData : ScriptableObject {
    [Header("Identity")]
    public string enemyName;
    public GameObject prefab;
    public Sprite portrait;              // para el popup de encuentro
    public ElementType element;

    [Header("Stats")]
    public EnemyStats stats;
    public int approximatePower;         // mostrado en el popup: "Poder ~450"

    [Header("Exploration")]
    public float detectionRange = 8f;    // rango a partir del cual detecta al jugador
    public float patrolRadius = 4f;      // radio de patrulla desde su spawn point
    public bool respawnsOnRoomExit = true;

    [Header("Combat")]
    public BehaviorTreeType behaviorTree;
    public EnemyReward dropReward;
}
```

---

### Tarea 3.2 — SO del Sistema de Encuentro

El popup de encuentro y la pantalla pre-combate necesitan sus propios datos. Esto es nuevo respecto al plan original.

```csharp
// ScriptableObjects/Combat/EncounterData.cs
// Generado en runtime por el EnemyController cuando detecta al jugador
// No es un asset fijo — se instancia con los datos del EnemyData concreto
[System.Serializable]
public class EncounterData {
    public EnemyData enemyData;
    public int enemyLevel;
    public int approximatePower;      // calculado: enemyData.stats * levelMultiplier
    public Transform enemyTransform;  // para que la cámara de combate lo apunte
}

// Core/EncounterState.cs — estado compartido entre exploración y combate
// Se registra en ServiceLocator antes de la transición de escena
public class EncounterState {
    public EncounterData Encounter { get; set; }
    public CharacterData SelectedCharacter { get; set; }
    public WeaponData EquippedWeapon { get; set; }
    public List<RuneData> EquippedRunes { get; set; } = new();
}
```

---

### Tarea 3.3 — Stats y Fórmulas de Combate

```csharp
// Characters/CharacterStats.cs
[System.Serializable]
public class CharacterStats {
    public float maxHP;
    public float attack;
    public float defense;
    public float speed;
    public float critChance;
    public float critMultiplier;

    public CharacterStats Clone() => (CharacterStats)MemberwiseClone();

    public static CharacterStats operator +(CharacterStats a, StatModifier m) {
        var result = a.Clone();
        result.attack    *= (1 + m.attackBonus);
        result.defense   *= (1 + m.defenseBonus);
        result.critChance = Mathf.Clamp01(result.critChance + m.critBonus);
        return result;
    }
}

// Combat/CombatFormulas.cs — C# puro, estático, testeable
public static class CombatFormulas {
    public static float CalculateDamage(
        float rawAttack, float targetDefense,
        ElementType attackElement, ElementType targetElement,
        float critChance, float critMult) {

        float elementalMult = GetElementalMultiplier(attackElement, targetElement);
        bool isCrit = UnityEngine.Random.value < critChance;
        float critFactor = isCrit ? critMult : 1f;
        float defense = Mathf.Max(1f, targetDefense);

        return rawAttack * elementalMult * critFactor * (100f / (100f + defense));
    }

    private static float GetElementalMultiplier(ElementType atk, ElementType def) =>
        (atk, def) switch {
            (ElementType.Fire,   ElementType.Ice)    => 1.5f,
            (ElementType.Ice,    ElementType.Shadow) => 1.5f,
            (ElementType.Shadow, ElementType.Fire)   => 1.5f,
            (ElementType.Fire,   ElementType.Fire)   => 0.5f,
            _ => 1.0f
        };
}
```

**Entregable:** Todos los SO definidos como clases. Assets creados en el inspector: Kael, Lyra, Vorn, 4 armas, 10 Runas, 4 enemigos con `detectionRange` y `approximatePower` configurados.

---

## SPRINT 4 — Sistema de Combate por Turnos
**Semana 4 · 27 Abril – 3 Mayo**  
**Objetivo:** Combate completo funcionando: cartas en mano, rank-up por fusión, gauge de última, resolución de turno, efectos de estado.

---

### Tarea 4.1 — TurnManager

**Filosofía:** El `TurnManager` es el árbitro. No sabe qué hacen los sistemas, solo coordina quién actúa y cuándo.

```csharp
// Combat/TurnManager.cs
public class TurnManager {
    public enum TurnPhase { Idle, PlayerDecision, EnemyDecision, Resolution, PostTurn }

    public TurnPhase CurrentPhase { get; private set; }
    private readonly ICombatantInput playerInput;
    private readonly ICombatantInput enemyInput;
    private readonly CombatResolver resolver;
    private readonly TurnBuffer buffer;

    // GameEvents para notificar a la UI sin acoplamiento
    [SerializeField] private GameEvent onTurnStart;
    [SerializeField] private GameEvent onResolutionStart;
    [SerializeField] private GameEvent onTurnEnd;

    public async UniTask RunTurn() {
        CurrentPhase = TurnPhase.PlayerDecision;
        onTurnStart.Raise();

        // Esperar acción del jugador con timeout (para co-op)
        CombatAction playerAction = await buffer.WaitForAction(
            playerInput, timeoutSeconds: 30f, defaultAction: CombatAction.Defend());

        CurrentPhase = TurnPhase.EnemyDecision;
        CombatAction enemyAction = enemyInput.DecideAction();

        CurrentPhase = TurnPhase.Resolution;
        onResolutionStart.Raise();
        await resolver.Resolve(playerAction, enemyAction);

        CurrentPhase = TurnPhase.PostTurn;
        ApplyPostTurnEffects();
        onTurnEnd.Raise();
    }
}
```

---

### Tarea 4.2 — CardSystem y Hand Management

```csharp
// Combat/CardSystem.cs
public class CardSystem {
    private readonly CharacterModel owner;
    public IReadOnlyList<CardInstance> Hand => hand;

    private List<CardInstance> hand = new();

    // Al inicio de cada turno, la mano se repone con skill1 y skill2
    public void RefreshHand() {
        hand.Clear();
        hand.Add(new CardInstance(owner.Data.skill1, rank: 1));
        hand.Add(new CardInstance(owner.Data.skill2, rank: 1));
        // Aplicar runas que añaden copias extra
        ApplyHandRunes();
        // Si gauge lleno, añadir última
        if (owner.UltimateGauge >= 1f)
            hand.Add(new CardInstance(owner.Data.ultimate, rank: 1));
    }

    // Intenta fusionar dos cartas en posiciones dadas
    // Retorna true si la fusión fue exitosa
    public bool TryMerge(int indexA, int indexB) {
        var a = hand[indexA];
        var b = hand[indexB];

        if (a.SkillData != b.SkillData) return false;
        if (a.Rank != b.Rank) return false;
        if (a.Rank >= 3) return false;

        var merged = new CardInstance(a.SkillData, rank: a.Rank + 1);
        hand.RemoveAt(Mathf.Max(indexA, indexB));
        hand.RemoveAt(Mathf.Min(indexA, indexB));
        hand.Insert(Mathf.Min(indexA, indexB), merged);

        // La fusión carga el gauge
        owner.ChargeUltimate(0.2f);
        return true;
    }

    public CardInstance UseCard(int index) {
        var card = hand[index];
        hand.RemoveAt(index);
        owner.ChargeUltimate(card.SkillData.ultimateChargeAmount);
        return card;
    }
}

// Combat/CardInstance.cs
public class CardInstance {
    public SkillData SkillData { get; }
    public UltimateData UltimateData { get; }
    public int Rank { get; }
    public bool IsUltimate => UltimateData != null;

    public SkillEffect GetEffect() =>
        IsUltimate ? UltimateData.effect : SkillData.effectsByRank[Rank - 1];
}
```

---

### Tarea 4.3 — CombatResolver y StatusEffects

```csharp
// Combat/CombatResolver.cs
public class CombatResolver {
    private readonly ResonanceSystem resonance;
    private readonly GameEvent<CombatResult> onCombatResult;

    public async UniTask Resolve(CombatAction playerAction, CombatAction enemyAction) {
        // Resolver acciones simultáneas por orden de velocidad
        bool playerFirst = playerAction.ActorSpeed >= enemyAction.ActorSpeed;

        var first  = playerFirst ? playerAction : enemyAction;
        var second = playerFirst ? enemyAction  : playerAction;

        var result1 = ExecuteAction(first);
        await PlayAnimation(result1); // esperar animación antes del segundo

        if (!result1.TargetDied) {
            var result2 = ExecuteAction(second);
            await PlayAnimation(result2);
        }

        onCombatResult.Raise(new CombatResult(result1, result2));
    }

    private ActionResult ExecuteAction(CombatAction action) {
        var effect = action.Card.GetEffect();
        float bonusMultiplier = resonance.GetDamageBonus(action.Actor);

        float damage = CombatFormulas.CalculateDamage(
            action.Actor.CurrentStats.attack * effect.damageMultiplier * bonusMultiplier,
            action.Target.CurrentStats.defense,
            action.Card.SkillData?.element ?? ElementType.Neutral,
            action.Target.Affinity,
            action.Actor.CurrentStats.critChance,
            action.Actor.CurrentStats.critMultiplier
        );

        action.Target.TakeDamage(damage, action.Card.SkillData?.element ?? ElementType.Neutral);

        // Aplicar status si aplica
        if (effect.appliedStatus != null && Random.value < effect.statusChance)
            StatusEffectSystem.Apply(action.Target, effect.appliedStatus);

        return new ActionResult(action, damage, action.Target.CurrentHP <= 0);
    }
}

// Combat/StatusEffectSystem.cs
public static class StatusEffectSystem {
    public static void Apply(CharacterModel target, StatusEffectData data) {
        var instance = new StatusEffect(data);
        target.ActiveEffects.Add(instance);
        target.OnStatusApplied?.Invoke(instance);
    }

    // Llamado al final de cada turno
    public static void Tick(CharacterModel target) {
        for (int i = target.ActiveEffects.Count - 1; i >= 0; i--) {
            var effect = target.ActiveEffects[i];
            effect.Tick(target);
            if (effect.IsExpired) target.ActiveEffects.RemoveAt(i);
        }
    }
}
```

**Entregable:** Combate completo jugable en una escena de prueba. Dos esferas que se atacan por turnos con el sistema de cartas funcionando.

---

### Tarea 4.4 — CombatCameraDirector

**Objetivo técnico:** La cámara de combate reacciona al rango de la carta usada reproduciendo la `CameraSequenceData` definida en el SO. El dominio no sabe que existe una cámara.

```csharp
// Core/Events/SkillUsedEvent.cs
public class SkillUsedEvent {
    public CardInstance Card;
    public Transform ActorTransform;
    public Transform TargetTransform;
}

// Presentation/Combat/CombatCameraDirector.cs
using Unity.Cinemachine;
using UnityEngine;

public class CombatCameraDirector : MonoBehaviour {
    [SerializeField] private GameEvent<SkillUsedEvent> onSkillUsed;
    [SerializeField] private CinemachineCamera baseCombatCam;
    [SerializeField] private CinemachineCamera dynamicCam;

    private Coroutine activeSequence;

    private void OnEnable()  => onSkillUsed.Subscribe(HandleSkillUsed);
    private void OnDisable() => onSkillUsed.Unsubscribe(HandleSkillUsed);

    private void HandleSkillUsed(SkillUsedEvent e) {
        var sequence = e.Card.GetEffect().cameraSequence;
        if (sequence == null) return;
        if (activeSequence != null) StopCoroutine(activeSequence);
        activeSequence = StartCoroutine(PlaySequence(sequence, e.ActorTransform));
    }

    private IEnumerator PlaySequence(CameraSequenceData seq, Transform actor) {
        dynamicCam.Priority = 20; // sobre la base
        foreach (var shot in seq.shots) {
            ApplyShot(shot, actor);
            yield return new WaitForSeconds(shot.duration);
        }
        // Volver a cámara base
        yield return new WaitForSeconds(seq.returnDuration);
        dynamicCam.Priority = 0;
        activeSequence = null;
    }

    private void ApplyShot(CameraShot shot, Transform actor) {
        // Ajustar FOV en la dynamicCam
        var lens = dynamicCam.Lens;
        lens.FieldOfView = shot.fov;
        dynamicCam.Lens = lens;

        switch (shot.type) {
            case CameraShotType.PushIn:
                // Acercar follow target al actor — el offset se reduce temporalmente
                // Implementar con DOTween o lerp manual en coroutine
                break;
            case CameraShotType.DynamicOrbit:
                // Activar rotación rápida alrededor del actor
                // Implementar con CinemachineOrbitalFollow en dynamicCam
                break;
            case CameraShotType.LowAngle:
                // Bajar el rig y aumentar dutch angle
                break;
        }
    }
}
```

**Convención de assets en el inspector para Kael:**

| Skill | Rango | SO de secuencia | Shots |
|---|---|---|---|
| Skill 1 | 1 | `Seq_Kael_S1_R1` | Static 0.4s · fov 60 |
| Skill 1 | 2 | `Seq_Kael_S1_R2` | PushIn 0.2s · fov 55 → Static 0.3s |
| Skill 1 | 3 | `Seq_Kael_S1_R3` | PushIn 0.15s → DynamicOrbit 0.4s dutchAngle 8° → Static 0.2s |
| Última  | — | `Seq_Kael_Ult`   | LowAngle 0.2s → PushIn 0.3s → DynamicOrbit 0.6s |

**Entregable:** Al usar una carta de Rango 1 la cámara no se mueve. Rango 2 hace un push-in visible. Rango 3 hace la órbita dinámica. La Última tiene su propia secuencia.

---

## SPRINT 5 — IA de Enemigos
**Semana 5 · 4–10 Mayo**  
**Objetivo:** Behavior Trees propios para los 4 tipos de enemigo. IA legible, expandible, sin librerías externas.

---

### Tarea 5.1 — Motor de Behavior Trees

El BT es un árbol de nodos. Cada nodo retorna `Success`, `Failure` o `Running`. Sin librerías externas: 80 líneas de código que cubren el 90% de los casos de uso.

```csharp
// Enemies/BehaviorTree/BtNode.cs
public abstract class BtNode {
    public enum Status { Success, Failure, Running }
    public abstract Status Evaluate(EnemyBlackboard bb);
}

// Nodos compuestos
public class Selector : BtNode {
    private readonly BtNode[] children;
    public Selector(params BtNode[] children) => this.children = children;
    public override Status Evaluate(EnemyBlackboard bb) {
        foreach (var child in children) {
            var status = child.Evaluate(bb);
            if (status != Status.Failure) return status;
        }
        return Status.Failure;
    }
}

public class Sequence : BtNode {
    private readonly BtNode[] children;
    public Sequence(params BtNode[] children) => this.children = children;
    public override Status Evaluate(EnemyBlackboard bb) {
        foreach (var child in children) {
            var status = child.Evaluate(bb);
            if (status != Status.Success) return status;
        }
        return Status.Success;
    }
}

// Nodo de condición — wrapper de predicado
public class Condition : BtNode {
    private readonly Func<EnemyBlackboard, bool> predicate;
    public Condition(Func<EnemyBlackboard, bool> p) => predicate = p;
    public override Status Evaluate(EnemyBlackboard bb) =>
        predicate(bb) ? Status.Success : Status.Failure;
}

// Nodo de acción
public class BtAction : BtNode {
    private readonly Func<EnemyBlackboard, Status> action;
    public BtAction(Func<EnemyBlackboard, Status> a) => action = a;
    public override Status Evaluate(EnemyBlackboard bb) => action(bb);
}

// Blackboard — datos compartidos del enemigo
public class EnemyBlackboard {
    public EnemyModel Self;
    public CharacterModel Target;
    public float DistanceToTarget;
    public bool IsInCombat;
    public Vector3 FleeTarget;
}
```

---

### Tarea 5.2 — BTs de los 4 Enemigos

```csharp
// Enemies/Trees/GoblinScoutTree.cs
public class GoblinScoutTree {
    public static BtNode Build() =>
        new Selector(
            // Si HP bajo, huir
            new Sequence(
                new Condition(bb => bb.Self.HPPercent < 0.3f),
                new BtAction(bb => { bb.Self.FleeToExit(); return BtNode.Status.Running; })
            ),
            // Si lejos del jugador, perseguir
            new Sequence(
                new Condition(bb => bb.DistanceToTarget > 3f),
                new BtAction(bb => { bb.Self.ChaseTarget(bb.Target); return BtNode.Status.Running; })
            ),
            // Si cerca, atacar
            new Sequence(
                new Condition(bb => bb.DistanceToTarget <= 3f),
                new BtAction(bb => { bb.Self.DecideCombatAction(); return BtNode.Status.Success; })
            )
        );
}

// Enemies/Trees/OrcGuardianTree.cs
public class OrcGuardianTree {
    public static BtNode Build() =>
        new Selector(
            // Si jugador lejos, cargar
            new Sequence(
                new Condition(bb => bb.DistanceToTarget > 6f),
                new BtAction(bb => { bb.Self.ChargeToTarget(bb.Target); return BtNode.Status.Running; })
            ),
            // Atacar — nunca huye
            new BtAction(bb => { bb.Self.DecideCombatAction(); return BtNode.Status.Success; })
        );
}
```

---

### Tarea 5.3 — EnemyModel y EnemyCombatAI

```csharp
// Enemies/EnemyModel.cs — dominio puro
public class EnemyModel {
    public EnemyData Data { get; }
    public float CurrentHP { get; private set; }
    public float HPPercent => CurrentHP / Data.stats.maxHP;
    public BtNode BehaviorTree { get; }
    public EnemyBlackboard Blackboard { get; } = new();

    // El enemigo decide su acción de combate en base a su BT
    // En combate por turnos, "decidir" = elegir una CombatAction
    public CombatAction DecideAction(CharacterModel target) {
        Blackboard.Target = target;
        Blackboard.DistanceToTarget = Vector3.Distance(/*pos self*/ Vector3.zero, /*pos target*/ Vector3.zero);
        BehaviorTree.Evaluate(Blackboard);
        return Blackboard.ChosenAction;
    }
}
```

**Entregable:** Los 4 enemigos con BTs propios funcionando en escena de prueba de combate.

---

## SPRINT 6 — Dungeon Procedural
**Semana 6 · 11–17 Mayo**  
**Objetivo:** Generación de pisos con BSP. Tipos de sala. Transición entre salas.

---

### Tarea 6.1 — BSP Room Generator

```csharp
// Dungeon/BSPSplitter.cs
public class BSPSplitter {
    private readonly int minRoomSize;
    private readonly System.Random rng;

    public List<RectInt> Generate(RectInt bounds, int depth) {
        if (depth == 0 || !CanSplit(bounds)) return new List<RectInt> { bounds };

        bool splitHorizontal = bounds.width < bounds.height ||
            (bounds.width == bounds.height && rng.NextDouble() > 0.5);

        var (left, right) = splitHorizontal
            ? SplitHorizontal(bounds)
            : SplitVertical(bounds);

        var rooms = new List<RectInt>();
        rooms.AddRange(Generate(left, depth - 1));
        rooms.AddRange(Generate(right, depth - 1));
        return rooms;
    }

    private (RectInt, RectInt) SplitVertical(RectInt bounds) {
        int splitX = rng.Next(bounds.x + minRoomSize, bounds.xMax - minRoomSize);
        return (
            new RectInt(bounds.x, bounds.y, splitX - bounds.x, bounds.height),
            new RectInt(splitX,   bounds.y, bounds.xMax - splitX, bounds.height)
        );
    }
}

// Dungeon/DungeonGenerator.cs
public class DungeonGenerator {
    private readonly BSPSplitter splitter;
    private readonly RoomFactory roomFactory;

    public DungeonLayout Generate(int floor) {
        var bounds = new RectInt(0, 0, 100, 100);
        var spaces = splitter.Generate(bounds, depth: 4);

        var rooms = spaces
            .Select(s => roomFactory.Create(s, AssignRoomType(floor)))
            .ToList();

        ConnectRooms(rooms);
        rooms.Last().Type = RoomType.Boss; // última sala siempre jefe

        return new DungeonLayout(rooms, floor);
    }

    private RoomType AssignRoomType(int floor) {
        // Pesos de tipos de sala por piso
        float roll = Random.value;
        return roll switch {
            < 0.6f => RoomType.Combat,
            < 0.8f => RoomType.Elite,
            _      => RoomType.Rest
        };
    }
}
```

---

### Tarea 6.2 — RoomManager y Transiciones

```csharp
// Dungeon/RoomManager.cs
public class RoomManager : MonoBehaviour {
    [SerializeField] private GameEvent<RoomData> onRoomEntered;
    [SerializeField] private GameEvent onRoomCleared;
    [SerializeField] private GameEvent<RuneData[]> onChestSpawned;

    private RoomData currentRoom;
    private EnemySpawner spawner;

    public void LoadRoom(RoomData room) {
        currentRoom = room;
        spawner.SpawnEnemies(room.enemies);
        onRoomEntered.Raise(room);
    }

    // Llamado cuando todos los enemigos de la sala mueren
    public void OnRoomCleared() {
        onRoomCleared.Raise();
        if (currentRoom.Type == RoomType.Combat || currentRoom.Type == RoomType.Elite)
            SpawnChest();
    }

    private void SpawnChest() {
        var runeOffer = GachaSystem.RollRuneChest(currentRoom.Floor, playerAffinity);
        onChestSpawned.Raise(runeOffer);
    }
}
```

**Entregable:** Dungeon de 1 piso generado proceduralmente con salas conectadas y tipos asignados.

---

## SPRINT 7 — Gacha Local, Resonancia y UI Funcional
**Semana 7 · 18–24 Mayo**  
**Objetivo:** Sistema gacha local sin backend. Sistema de resonancia. UI que muestra estado de juego.

---

### Tarea 7.1 — GachaSystem Local

**Decisión técnica:** Sin backend. Todo en `PlayerPrefs` + JSON local. El gacha es determinista a partir de una seed guardada localmente. El pity se persiste entre sesiones.

```csharp
// Gacha/GachaSystem.cs
public class GachaSystem {
    private readonly GachaPool pool;
    private readonly PityTracker pity;
    private readonly LocalSaveSystem save;

    public GachaResult Roll(GachaPoolType poolType) {
        // Determinar rareza con pity
        RarityType rarity = pity.DetermineRarity(poolType);
        pity.IncrementCounter(poolType);

        // Seleccionar item de esa rareza del pool
        var candidates = pool.GetByRarity(poolType, rarity);
        var selected = WeightedRandom.Select(candidates, x => x.baseDropRate);

        pity.OnItemObtained(poolType, rarity);
        save.SavePityState(pity.GetState());

        return new GachaResult(selected, rarity, pity.IsPityActivated);
    }
}

// Gacha/PityTracker.cs
public class PityTracker {
    private Dictionary<GachaPoolType, int> rollsSinceEpic  = new();
    private Dictionary<GachaPoolType, int> rollsSinceLeg   = new();

    public RarityType DetermineRarity(GachaPoolType pool) {
        // Hard pity: garantizado en N rolls
        if (rollsSinceLeg[pool]  >= 100) return RarityType.Legendary;
        if (rollsSinceEpic[pool] >= 50)  return RarityType.Epic;

        // Soft pity: probabilidad aumenta gradualmente desde roll 40
        float epicRate   = rollsSinceEpic[pool] > 40
            ? 0.08f + (rollsSinceEpic[pool] - 40) * 0.02f
            : 0.08f;

        float roll = Random.value;
        if (roll < 0.02f)               return RarityType.Legendary;
        if (roll < 0.02f + epicRate)    return RarityType.Epic;
        if (roll < 0.10f + epicRate)    return RarityType.Rare;
        return RarityType.Common;
    }
}

// Gacha/LocalSaveSystem.cs
public class LocalSaveSystem {
    private const string SaveKey = "runefall_save";

    public void Save(SaveData data) {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    public SaveData Load() {
        string json = PlayerPrefs.GetString(SaveKey, "{}");
        return JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
    }
}
```

---

### Tarea 7.2 — ResonanceSystem

```csharp
// Resonance/ResonanceDetector.cs
public class ResonanceDetector {
    public ResonanceLevel Detect(CharacterData character, WeaponData weapon, List<RuneData> runes) {
        int matchCount = 0;
        if (weapon.element == character.element) matchCount++;
        matchCount += runes.Count(r => r.element == character.element);

        // Verificar si la pasiva es compatible
        bool passiveCompatible = character.passive.resonanceCompatible;

        return (matchCount, passiveCompatible) switch {
            (>= 3, true)  => ResonanceLevel.Total,
            (>= 3, false) => ResonanceLevel.Major,
            (>= 2, _)     => ResonanceLevel.Minor,
            _             => ResonanceLevel.None
        };
    }

    public StatModifier GetModifier(ResonanceLevel level) =>
        level switch {
            ResonanceLevel.Total => new StatModifier { attackBonus = 0.40f, critBonus = 0.15f },
            ResonanceLevel.Major => new StatModifier { attackBonus = 0.25f },
            ResonanceLevel.Minor => new StatModifier { attackBonus = 0.10f },
            _                    => StatModifier.Zero
        };
}
```

---

### Tarea 7.3 — UI Funcional (no decorativa)

La UI es la parte más propensa a deuda técnica. Regla: la UI **observa** el dominio, nunca lo modifica directamente.

```csharp
// Presentation/UI/CombatHUDPresenter.cs
public class CombatHUDPresenter : MonoBehaviour {
    [SerializeField] private Slider hpBar;
    [SerializeField] private Slider ultGauge;
    [SerializeField] private CardHandView handView;

    // Recibe referencia al modelo, se suscribe a sus eventos
    public void Init(CharacterModel model, CardSystem cards) {
        model.OnHPChanged      += val => hpBar.value = val / model.CurrentStats.maxHP;
        model.OnUltGaugeChanged += val => ultGauge.value = val;
        cards.OnHandChanged    += handView.Refresh;
    }
}
```

**Entregable:** Gacha local funcional guardando en PlayerPrefs. Resonancia detectada y aplicando modifiers. UI mostrando HP, gauge y cartas en mano.

---

## SPRINT 8 — Integración, Director de IA y Build Final
**Semana 8 · 25 Mayo – 1 Junio**  
**Objetivo:** Todo integrado. Director de IA funcionando. Bug fixing. Build publicable.

---

### Tarea 8.1 — Director de IA

```csharp
// Enemies/AIDirector.cs
public class AIDirector {
    // Métricas de la run actual
    private float avgDamageReceivedPerRoom;
    private float avgTimePerRoom;
    private float hpOnBossEntry;
    private int roomsCleared;

    // Ajustes que aplica al siguiente piso
    public FloorModifiers CalculateModifiers() {
        float difficulty = 1.0f;

        // Si el jugador recibe poco daño → subir dificultad
        if (avgDamageReceivedPerRoom < 10f)  difficulty += 0.2f;
        // Si limpia salas muy rápido → más enemigos
        if (avgTimePerRoom < 30f)            difficulty += 0.15f;
        // Si llega al jefe con mucha vida → jefe más fuerte
        if (hpOnBossEntry > 0.8f)            difficulty += 0.25f;
        // Si llega muy débil → dar respiro
        if (hpOnBossEntry < 0.3f)            difficulty -= 0.2f;

        return new FloorModifiers {
            enemyHPMultiplier  = difficulty,
            enemyATKMultiplier = difficulty * 0.8f,
            // Sesgar drops si el jugador recibe mucho daño
            healDropBias = avgDamageReceivedPerRoom > 30f ? 0.4f : 0f
        };
    }

    public void RecordRoom(float damageReceived, float timeInRoom) {
        avgDamageReceivedPerRoom = (avgDamageReceivedPerRoom * roomsCleared + damageReceived)
                                   / (roomsCleared + 1);
        avgTimePerRoom = (avgTimePerRoom * roomsCleared + timeInRoom)
                         / (roomsCleared + 1);
        roomsCleared++;
    }
}
```

---

### Tarea 8.2 — Integración y GameManager

```csharp
// Core/GameManager.cs
public class GameManager : MonoBehaviour {
    [Header("Events")]
    [SerializeField] private GameEvent onGameStart;
    [SerializeField] private GameEvent onRunStart;
    [SerializeField] private GameEvent onRunEnd;

    private void Awake() {
        // Registrar todos los servicios
        ServiceLocator.Register<IGachaSystem>(new GachaSystem());
        ServiceLocator.Register<IResonanceDetector>(new ResonanceDetector());
        ServiceLocator.Register<IAIDirector>(new AIDirector());
        ServiceLocator.Register<ILocalSave>(new LocalSaveSystem());
    }

    public void StartRun(CharacterData character, WeaponData weapon, List<RuneData> runes) {
        var model = new CharacterModel(character);
        var resonance = ServiceLocator.Get<IResonanceDetector>()
            .Detect(character, weapon, runes);
        // Aplicar modifiers de resonancia al model
        // Cargar primer piso del dungeon
        onRunStart.Raise();
    }
}
```

---

### Tarea 8.3 — Checklist de Build Final

Antes de exportar la build de entrega:

- [ ] Separar layer de física para jugador, enemigos y escenario
- [ ] Configurar Quality Settings: Medium para PC, Low para móvil
- [ ] LOD Groups en prefabs de personajes (solo si se usan assets 3D complejos)
- [ ] Object Pooling para VFX de partículas y proyectiles
- [ ] Compilar sin errores en Android Build Settings
- [ ] Verificar que el gacha local carga y guarda correctamente entre sesiones
- [ ] Probar run completa de 3 pisos sin crashes
- [ ] Exportar build PC (.exe) y Android (.apk)
- [ ] Subir build PC a itch.io como página de proyecto

---

## Resumen de Entregables por Sprint

| Sprint | Semana | Estado | Entregable verificable |
|---|---|---|---|
| S1 | 1 | ✅ | Proyecto, ServiceLocator, GameEvent\<T\>, InputReader, 3 cámaras virtuales con transición |
| S2 | 2 | ✅ | Personaje moviéndose con aceleración/desaceleración, rotación fluida, CharacterModel |
| S3 | 3 | 🔄 | Salto + Dash funcionales · Todos los SO definidos incluyendo EncounterData · Assets creados |
| S4 | 4 | — | Sistema de encuentro (popup + pre-combate) · Combate 2.5D con cartas, rank-up, gauge, resolución |
| S5 | 5 | — | 4 enemigos con Behavior Trees propios · Patrulla y detección de rango en exploración |
| S6 | 6 | — | Dungeon de 1 piso generado proceduralmente · Reaparición de enemigos al salir de sala |
| S7 | 7 | — | Gacha local con pity · Resonancia aplicando modifiers · UI funcional de combate y exploración |
| S8 | 8 | — | Todo integrado · Director de IA activo · Build PC + Android publicable |

---

## Notas de Arquitectura Final

**Agregar un personaje nuevo** (post-MVP): Crear `CharacterData` SO, asignar prefab, portrait, skills. Zero código. 15 minutos.

**Cambiar bundle de assets completo**: Reemplazar referencias en SO (`prefab`, `portrait`, `cardArt`). La lógica no se toca. Un día de trabajo.

**Agregar un tipo de enemigo nuevo**: Crear `EnemyData` SO + una clase con el BT. El resto del sistema lo detecta automáticamente. 2 horas.

**Agregar una Runa nueva**: Crear `RuneData` SO + implementar su `RuneEffect`. El GachaSystem y el RoomManager la incluyen automáticamente en los pools. 1 hora.
