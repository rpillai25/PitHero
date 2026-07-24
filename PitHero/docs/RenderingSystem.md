# Rendering System

## Overview

All world entities use a single-pass, Y-sorted render pipeline.  Multi-sprite actors
(hero, mercenaries, innkeeper) are composited into a render-target each frame so that
every piece of a character always appears on the same side of every other world object.

---

## Render Layer Stack

Higher number = drawn first = further back.  Lower number = drawn later = in front.

| Constant | Value | What lives here |
|---|---|---|
| `RenderLayerTop` | 2 | Tilemap "Top" layer (tree-tops, overhangs) — covers everything |
| `RenderLayerFogOfWar` | 40 | Tilemap fog-of-war overlay — covers actors but not the top layer |
| `RenderLayerActors` | 60 | Heroes, mercenaries, monsters, **placed buildings** (all Y-sorted via `LayerDepth`) |
| `RenderLayerSingleTileObject` | 61 | Static 32×32 world objects: treasure chests, walls/obstacles (Y-sorted) |
| `RenderLayerDroppedItems` | 65 | Dropped loot items |
| `RenderLayerBase` | 100 | Tilemap base layer |

UI / HUD layers (screen-space, unaffected by camera): `RenderLayerActionQueue` (996),
`RenderLayerGraphicalHUD` (997), `RenderLayerUI` (998), `TransparentPauseOverlay` (999).

---

## Y-Sorting

All actors at `RenderLayerActors` and `RenderLayerSingleTileObject` are Y-sorted each
frame by `YSortManager` (a SceneComponent). It queries all enabled renderables on those
two layers, filters to only those whose bounds intersect the camera viewport, and assigns:

```
layerDepth = Clamp01(1 − entity.Y × YSortDepthScale)
```

Using pixel-granular Y (not tile rows) ensures a unique depth per pixel row, eliminating
the flickering that tile-row snapping caused between adjacent entities.

`GameConfig.YSortDepthScale = 1f / 100000f` supports pits up to ~3 000 tiles deep.

### Per-entity sort offset (`IYSortOffset`)

A renderable may implement `IYSortOffset { float YSortOffset { get; } }`; `YSortManager` adds
that offset to `entity.Y` before computing depth, moving the sort point off the entity centre.
`YSortSpriteRenderer` exposes a settable `YSortOffset` (default 0).

Used by **placed buildings** (Monster House / Crop Storage). A building's entity sits at its
sprite centre, but its "ground / front-face" line is the bottom of its footprint — the lower
64 px (2 tiles) that `FarmPathfinder` walls off. Setting `YSortOffset = spriteHeight / 2` puts
the sort point at the sprite's bottom edge, so a monster walking in the walkable top rows
*behind* the building sorts behind it (the building draws over it), while a monster standing in
front draws over the building. Buildings therefore render at `RenderLayerActors`, not a fixed
layer.

### Monster sprite tile-anchor (large sprites)

Monsters occupy a single entity-centred 32×32 tile for pathfinding/collision regardless of art
size, but sprites use a centre origin, so a 64×64 sprite hangs 16 px below its tile. To make
medium/large monsters visually "stand" on their tile (and sit correctly behind top-layer
overhangs such as the tavern wall), `EnemyAnimationComponent.ApplyTileAnchorOffset` adds
`LocalOffset.Y += (TileSize − spriteHeight) / 2` after the first sprite is assigned, aligning the
sprite's **lower 32 px** with the tile. It is a no-op for 32×32 monsters and never touches the
entity position, so tile logic is unchanged.

---

## Renderer Classes

### `ICompositeLayer` — layer contract

**File:** `ECS/Components/ICompositeLayer.cs`

Interface for any layer that participates in `MultiSpriteAnimator` compositing:

```csharp
Sprite Sprite { get; }           // current animation frame
Vector2 LocalOffset { get; }     // draw offset from entity world-position
bool FlipX { get; }              // horizontal mirror flag
Color LayerColor { get; }        // tint (wraps RenderableComponent.Color field)
bool OwnedByComposite { get; set; }
```

Implementors must return `false` from `IsVisibleFromCamera` when `OwnedByComposite = true`
so the DefaultRenderer skips them. See `HeroAnimationComponent` for the reference pattern.

---

### `SpriteCompositorBase` — shared RT-compositing base

**File:** `ECS/Components/SpriteCompositorBase.cs`

Abstract base for both `MultiSpriteAnimator` and `StaticSpriteCompositor`. Handles
bounds calculation, the per-frame RT blit loop (pixel-rounding, batcher save/restore,
composite sprite draw), and disposal. Subclasses call
`InitCompositor(rtWidth, rtHeight, pivot, drawCallback)` from `OnAddedToEntity()` and
supply a per-layer draw callback — the only thing that differs between compositor types.
Y-sort is handled centrally by `YSortManager`.

---

### `MultiSpriteAnimator` — multi-layer animated actors

**File:** `ECS/Components/MultiSpriteAnimator.cs`

Composites any `ICompositeLayer` stack into a single 32×46 RT. Generic — works for hero
paperdolls, innkeeper, mercenaries, and any future multi-part animated actor (e.g. a dragon
built from independently-animated body-part components). Extends `SpriteCompositorBase`.

**How it works:**
1. Constructor receives any `ICompositeLayer[]` in back-to-front draw order.
2. `OnAddedToEntity` sets `OwnedByComposite = true` on each layer (suppresses direct render),
   then calls `InitCompositor` with a draw callback that iterates layers via the interface.
3. Each frame in `Render()` (base class): ends the outer batcher, switches to a 32×46
   `RenderTarget2D`, draws layers with a translation matrix that maps entity world-position →
   RT pixel (16, 39), restores the scene RT, and blits the composited sprite.
4. Y-sort is applied each frame by `YSortManager` (SceneComponent).

**Key constants:**
- `RT_WIDTH = 32`, `RT_HEIGHT = 46`
- `RtEntityPivot = (16, 39)` — accounts for center sprite origin (16, 23) + local offset
  (0, −16): `pivot.Y = origin.Y + |offset.Y| = 23 + 16 = 39`

**Usage (hero/mercenary — existing ICompositeLayer implementors):**
```csharp
// Add paperdoll components first, then pass them in back-to-front order:
var animator = entity.AddComponent(new MultiSpriteAnimator(
    hand2Animator, bodyAnimator, pantsAnimator, shirtAnimator,
    headAnimator, eyesAnimator, hairAnimator, hand1Animator));
animator.SetRenderLayer(GameConfig.RenderLayerActors);
```

**Adding a new multi-part animated actor (e.g. a dragon):**
1. Each body-part component extends `SpriteAnimator` (or similar) and implements
   `ICompositeLayer` (add `LayerColor` property and `OwnedByComposite` flag; override
   `IsVisibleFromCamera` to return `false` when `OwnedByComposite`).
2. Pass all parts to `MultiSpriteAnimator(params ICompositeLayer[])` back-to-front.
3. Layer-specific animation control (play attack, set direction) is called directly on
   the part components — `MultiSpriteAnimator` only handles compositing, not animation logic.

---

### `StaticSpriteCompositor` — multi-layer static objects

**File:** `ECS/Components/StaticSpriteCompositor.cs`

Use for non-animated objects with multiple `SpriteRenderer` layers (treasure chests:
base + wood lid). Extends `SpriteCompositorBase`.

**How it works:** Same RT compositing pattern as `MultiSpriteAnimator` (both share the
base class), but children are plain `SpriteRenderer` instances.  Children are
`SetEnabled(false)` (not rendered by the DefaultRenderer); the compositor calls
`child.Render(batcher, null)` directly each frame, so `Sprite` and `Color` changes from
`UpdateSprites()` / `UpdateWoodColor()` are picked up automatically.

**Usage (from `TreasureComponent.InitializeRenderers`):**
```csharp
var compositor = Entity.AddComponent(new StaticSpriteCompositor(
    new SpriteRenderer[] { _baseRenderer, _woodRenderer },  // back → front
    rtWidth:  GameConfig.TileSize,
    rtHeight: GameConfig.TileSize,
    rtEntityPivot: new Vector2(GameConfig.TileSize / 2f, GameConfig.TileSize / 2f)));
compositor.SetRenderLayer(GameConfig.RenderLayerSingleTileObject);
```

The `rtEntityPivot` is `(width/2, height/2)` for 32×32 sprites with center origin and
no local offset.

---

### `YSortSpriteRenderer` — single-sprite static world objects

**File:** `ECS/Components/YSortSpriteRenderer.cs`

Named `SpriteRenderer` subclass for any world entity at `RenderLayerActors` or
`RenderLayerSingleTileObject` that renders as a single sprite. Y-sort is handled
centrally by `YSortManager`; this class exists solely as a semantic marker.

**Usage:**
```csharp
var r = entity.AddComponent(new YSortSpriteRenderer(mySprite));
r.SetRenderLayer(GameConfig.RenderLayerSingleTileObject); // or RenderLayerActors
```

Currently used by: pit walls, wizard orbs, hero statue.

---

### `YSortManager` — centralized Y-sort SceneComponent

**File:** `ECS/Components/YSortManager.cs`

SceneComponent that runs once per frame before entity updates. Iterates
`RenderLayerActors` and `RenderLayerSingleTileObject`, skips renderables outside the
camera bounds, and writes a pixel-granular `LayerDepth` to each visible renderable.
Replaces the per-component `IUpdatable` approach that was on `YSortSpriteRenderer`,
`SpriteCompositorBase`, and `EnemyAnimationComponent`.

Registered in `MainGameScene.Initialize()` via `AddSceneComponent<YSortManager>()`.

---

### `EnemyAnimationComponent` — single-sprite animated monsters

**File:** `ECS/Components/EnemyAnimationComponent.cs`

Extends `PausableSpriteAnimator`.  Handles animation (walk, attack, wobble for
1-frame sprites), fog-of-war visibility, and tile-row Y-sort in a single component.
No separate compositor needed — monsters are single-sprite.

---

## Adding a New World Entity

| Entity type | Renderer to use | Render layer |
|---|---|---|
| Animated multi-layer actor (humanoid) | `MultiSpriteAnimator` | `RenderLayerActors` |
| Static multi-layer object | `StaticSpriteCompositor` | `RenderLayerSingleTileObject` |
| Single-sprite static object (≤ 32×32) | `YSortSpriteRenderer` | `RenderLayerSingleTileObject` |
| Single-sprite static object (> 32×32) | `YSortSpriteRenderer` | `RenderLayerActors` |
| Animated single-sprite monster | `EnemyAnimationComponent` (subclass) | `RenderLayerActors` |
| Pickup / dropped item | Plain `SpriteRenderer` | `RenderLayerDroppedItems` |

**Never use a plain `SpriteRenderer` at `RenderLayerActors` or
`RenderLayerSingleTileObject`** — it will not Y-sort and will render in an arbitrary
order relative to heroes and monsters.

---

## Implementation Notes

### Render-target compositing pattern

`SpriteCompositorBase.Render()` (shared by `MultiSpriteAnimator` and `StaticSpriteCompositor`) follows this sequence:

```
1. prevRTs = GraphicsDevice.GetRenderTargets()
2. batcher.End()                        // flush pending outer batch to scene RT
3. compositor.RenderComposite(entityPos) // sets 32×32/32×46 RT, draws children, ends
4. GraphicsDevice.SetRenderTargets(prevRTs) // restore scene RT
5. batcher.Begin(BlendState.AlphaBlend, SamplerState.PointClamp, ..., camera.TransformMatrix, ...)
6. batcher.Draw(compositeSprite, ...)   // queue composite into resumed outer batch
```

`SamplerState.PointClamp` is used explicitly in both passes to prevent bilinear
filtering on the RT texture (which would cause a "heat-haze" shimmer during movement).

### Entity position rounding

The entity position is rounded to the nearest integer pixel before computing the RT
translation and display draw position.  This prevents sub-pixel shimmer caused by a
mismatch between the fractional RT translation and the rounded display destination.

```csharp
var entityPos = new Vector2(
    (float)Math.Round(Entity.Transform.Position.X),
    (float)Math.Round(Entity.Transform.Position.Y));
```
