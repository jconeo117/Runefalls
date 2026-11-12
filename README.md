# Runefalls

**Juego de cartas por turnos en Unity** · proyecto de grado de la Tecnicatura en Desarrollo y Producción
de Videojuegos (UTN). **En desarrollo:** el diseño todavía puede cambiar.

*Turn-based card game in Unity, my graduation project. Work in progress: the design may still change.*

---

## La idea · The idea

Exploración en tercera persona por mazmorras y, al chocar con un enemigo, un combate por turnos en 2.5D
donde cada habilidad es una carta. Las cartas iguales se fusionan para subir de rango, y usar cartas
llena una barra para la habilidad definitiva. Hay elementos con ventajas cruzadas: fuego, hielo y sombra.

*Third-person dungeon exploration that switches to 2.5D turn-based combat, where every skill is a card.
Matching cards fuse to rank up, and playing cards charges an ultimate. Elements counter each other:
fire, ice and shadow.*

## Arquitectura · Architecture

La lógica del juego vive en **C# puro**, sin depender de Unity, y se puede probar con pruebas
unitarias. Los MonoBehaviour solo muestran y reciben la entrada (patrón *Humble Object*).

```
Presentación   MonoBehaviours, animaciones, cámara    → solo muestran
Dominio        CharacterModel, fórmulas de combate    → C# puro, testeable
Datos          ScriptableObjects                      → personajes, armas, cartas, enemigos
```

- **Por datos:** personajes, armas, runas, habilidades, pasivas, habilidades definitivas y enemigos son
  `ScriptableObject`. Para cambiar el contenido o el paquete de assets no hay que tocar el código.
- **Eventos tipados** (`GameEvent<T>`) y un *service locator* para que las capas no se conozcan entre sí.
- **Pruebas en modo edición** con Unity Test Framework para el modelo del personaje y la salud del
  jugador.
- Una regla automática revisa en cada cambio que la capa de dominio no dependa de Unity.

## Estado · Status

| Sprint | Contenido · Scope | |
|---|---|---|
| 1 | Fundamentos: estructura, capas, eventos, entrada · *Foundations* | ✅ |
| 2 | Movimiento y personaje: controlador en tercera persona, cámara, animación · *Movement & character* | ✅ |
| 3 | Sistema de datos: jerarquía de ScriptableObjects, fórmulas de combate, estado del encuentro · *Data system* | ✅ |
| 4 | Combate por turnos: mano de cartas, fusión, definitiva, resolución · *Turn-based combat* | 🔄 |
| 5–8 | IA de enemigos, mazmorra procedural, progresión y build final · *Enemy AI, procedural dungeon, progression* | ⏳ |

## Stack

Unity 6 (6000.0 LTS) · C# · Universal Render Pipeline · Cinemachine 3 · Input System · Unity Test
Framework

## Cómo abrirlo · How to open

1. Instalar Unity **6000.0.30f1** desde Unity Hub.
2. *Add project from disk* → esta carpeta.
3. Pruebas: *Window → General → Test Runner → EditMode*.
