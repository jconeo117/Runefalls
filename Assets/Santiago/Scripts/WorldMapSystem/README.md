# Dungeon Interaction System — Setup Guide
Unity 3D · Isometric World Map (Mount & Blade style)

## Overview

| Script | Responsibility |
|---|---|
| `CrosshairSystem.cs` | Manages the two crosshair Images with smooth alpha fade |
| `PlayerInteractionRaycaster.cs` | Casts mouse ray every frame, dispatches hover/click to IInteractable |
| `IInteractable.cs` | Interface contract (implement on any new POI type) |
| `DungeonInteractable.cs` | Attached to your Dungeon GameObject – range check, hover, click |
| `DungeonUIPanel.cs` | The entry panel (fade/scale animation, Enter / Cancel buttons) |
| `WorldTooltipSystem.cs` | Floating tooltip that follows the mouse with dungeon name + hint |
| `ColorExtensions.cs` | Unity Color helper (WithAlpha) |

---

## Scene Hierarchy

```
Scene
├── Player (Tag: "Player")
│   └── PlayerInteractionRaycaster  ← attach here
│
├── IsometricCamera (Camera component)
│
├── Dungeon (your existing GameObject)
│   ├── DungeonInteractable          ← attach here
│   └── Collider (any type)         ← required for raycasting
│
└── UI Canvas (Screen Space – Overlay, Sort Order 10)
    ├── CrosshairRoot
    │   ├── CrosshairDefault (Image) ← your default crosshair sprite
    │   └── CrosshairInteract (Image)← your interact crosshair sprite
    │
    ├── DungeonPanel (CanvasGroup)
    │   ├── DungeonUIPanel           ← attach here
    │   ├── TMP_Text "DungeonName"
    │   ├── TMP_Text "Description"
    │   ├── Button "Enter"
    │   └── Button "Cancel"
    │
    └── TooltipPanel (CanvasGroup)
        ├── WorldTooltipSystem       ← attach here
        ├── TMP_Text "Title"
        └── TMP_Text "Hint"
```

---

## Step-by-Step Setup

### 1. Crosshair System
1. Create a `Canvas` → add empty child `CrosshairRoot`.
2. Add two `Image` children: `CrosshairDefault` and `CrosshairInteract`.
3. Set both to **anchored center**, assign your crosshair sprites.
4. Add `CrosshairSystem.cs` to `CrosshairRoot`.
5. Drag the two Images into the Inspector slots.

### 2. Player Raycaster
1. Add `PlayerInteractionRaycaster.cs` to your Player GameObject.
2. Set **Interactable Layer** to the layer your Dungeon objects live on
   (create a layer called `Interactable` and assign it there).
3. Assign your isometric Camera reference.

### 3. Dungeon Object
1. Make sure the Dungeon has a **Collider** (Box, Mesh, etc.) on the
   `Interactable` layer.
2. Add `DungeonInteractable.cs` to the Dungeon root.
3. Set **Dungeon Name**, **Description**, and **Interaction Range** in Inspector.
4. Assign Player Transform (or tag your Player as "Player" and leave it empty).

### 4. Dungeon UI Panel
1. Create a UI Panel with `CanvasGroup` on the Canvas.
2. Add `DungeonUIPanel.cs` to it.
3. Wire up: `panelGroup`, both TMP_Text fields, Enter button, Cancel button.
4. In `DungeonInteractable → onDungeonEnter` you can add extra events
   (e.g. SceneManager.LoadScene).

### 5. Tooltip
1. Create a small UI panel with `CanvasGroup` and `RectTransform`.
2. Add `WorldTooltipSystem.cs` to it.
3. Assign `tooltipGroup`, `tooltipRect`, `titleText`, `hintText`.

---

## Extending to Other POIs (Towns, Castles, etc.)

Any new map object just needs:
1. A **Collider** on the `Interactable` layer.
2. A script that **implements `IInteractable`**.

The raycaster will automatically call the correct hover/click methods.
No changes required to `PlayerInteractionRaycaster`.

---

## Outline Shader (Optional)

`DungeonInteractable.SetOutline()` calls `material.SetFloat("_OutlineEnabled", ...)`.

Compatible packages (pick one):
- **Quick Outline** (free, GitHub)
- **Highlight Plus** (Asset Store)
- **URP Renderer Feature** with Stencil outline

If you don't use an outline shader, simply remove the `SetOutline` call —
the crosshair + tooltip changes already communicate hover state clearly.

---

## Tips

- Keep **Interaction Range** between 3–6 units for a tight, Mount & Blade feel.
- Set `pauseTimeScale = true` on `DungeonUIPanel` if you want the world to freeze while the entry dialog is open.
- The `onDungeonEnter` UnityEvent is the hook for scene loading, fade-to-black, etc. Wire it in the Inspector or subscribe in code.
