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
| `RenderLayerCloudOverlay` | -10 | Volumetric scrolling cloud overlay — front-most world-space layer, covers everything below |
| `RenderLayerTop` | 2 | Tilemap "Top" layer (tree-tops, overhangs) — covers everything |
| `RenderLayerPitWarpParty` | 30 | The party floating through the wizard-orb warp (#469). Excluded from the world pass; drawn by `PitWarpPartyRenderer` **after** the post-processors so the pit swirl never twists it |
| `RenderLayerActors` | 60 | Heroes, mercenaries, monsters, **placed buildings** (all Y-sorted via `LayerDepth`) |
| `RenderLayerSingleTileObject` | 61 | Static 32×32 world objects: treasure chests, walls/obstacles (Y-sorted) |
| `RenderLayerDroppedItems` | 65 | Dropped loot items |
| `RenderLayerFogOfWar` | 70 | Tilemap fog-of-war overlay — renders **behind** actors/objects (#337) |
| `RenderLayerTreeBand` | 80 | Decorative tree bands north/south of the map (#348) |
| `RenderLayerGroundPropOverlay` | 87 | Overlays on a flat ground building: the Digging Field's dark tile-cooldown squares (#452) |
| `RenderLayerGroundProp` | 88 | Flat ground buildings (the Digging Field, #452): a plain day/night-graded `SpriteRenderer`, not Y-sorted, under every actor, above tilled soil |
| `RenderLayerDetail` | 90 | Tilemap detail layer (tilled soil) |
| `RenderLayerBase` | 100 | Tilemap base layer |

Fog of war renders behind actors so the party is never partially covered when walking next
to fogged tiles. Pit entities that spawn under covered fog are instead hidden per-entity:
`FogHideableComponent` (treasure chests, walls/obstacles, wizard orb) disables its target
renderable while the entity's tile is fogged, and `TiledMapService.RefreshFogHiddenEntities`
— called automatically after any fog clear — re-enables revealed statics and monster
animators. Monsters additionally self-hide/self-reveal when they move between fogged and
clear tiles (`EnemyAnimationComponent.CheckFogVisibility` / `TileByTileMover.CompleteMove`).
Heroes and mercenaries are never fog-hidden.

`RenderLayerTreeBand` holds the two decorative tree bands that fill the empty space above and
below the map when the player zooms out (`TreeBandComponent`, #348). Each band is painted **once**
into its own `RenderTexture` at map load and then blits a single quad per frame, so its ~1900 trees
cost nothing after the first frame. It sits in front of the Base/Detail tilemap layers (so the
fringe that spills over the map edge covers terrain) but behind fog and actors, and it is
deliberately not 60/61 so `YSortManager` ignores it — the bands are static and never sort.

Each band first tiles the map tileset's grass tile (`GameConfig.TreeBandGrassTileGid`, resolved via
`TmxMap.GetTilesetForTileGid` + `TmxTileset.TileRegions`) across the part of the band outside the
map, then draws trees over it. Without that backdrop the gaps between trunks fall through to the
window's ungraded background colour — which passes for grass by day but stands out badly once the
terrain is graded at night. Grass stops at the map edge so the strip the trees overhang stays
transparent and the real tilemap shows through.

Each band anchors its rows on the edge that faces the map. The **top** band's trees stand above the
map, so rows anchor by **trunk base** and the render texture clips those trunks flush at
`TreeBandMapOverlapPx` — a cut trunk just reads as the tree standing on the ground. The **bottom**
band's trees grow upward toward the map, so rows anchor by **canopy top**, holding the first row's
crowns `TreeBandCanopyPeekPx` over the map's bottom tile row instead of burying it; that texture is
sized to contain whole trees so no canopy is ever cut.

`RenderLayerCloudOverlay` holds a single `CloudOverlayComponent` (`ECS/Components/CloudOverlayComponent.cs`)
drawing one quad covering `camera.Bounds` per frame, textured with a small code-generated tileable
noise texture (`CloudNoiseGenerator`) and shaded by a custom pixel shader (`CloudOverlay.fx` /
`CloudOverlayEffect` / `CloudOverlayController`, all in `PitHero.Rendering`). Because `RenderableComparer`
sorts by descending `RenderLayer`, the negative value places it after every other world-space renderable
(terrain, actors, fog-of-war, tree bands, buildings) — clouds cover all of them. It sits strictly below
the screen-space UI group (996–1000), which renders in Nez's separate `ScreenSpaceRenderer` pass
afterward, so HUD/windows/speech bubbles are never occluded. The shader reconstructs per-pixel world
position from `CameraTopLeft`/`CameraSize` uniforms set each `Render()` call, so clouds stay
world-anchored under any pan/zoom without needing a giant fixed-size quad. Drift, weather (coverage
threshold), shape morph, and day/night tint are all driven once per frame from `CloudOverlayController.Update()`
(called from `MainGameScene.Update`), off `InGameTimeService.AccumulatedSeconds` — freezing with the
rest of the world when paused. Two **cloud dead zones** (`GameConfig.CloudDeadZone*`, world-space tile
rects: the tavern, x 63–106, and the pit, x 0 to the live `PitWidthManager.CurrentPitRightEdge`, both y 0–11) are pushed to the shader as `DeadZoneMin`/`DeadZoneMax`, `DeadZone2Min`/`DeadZone2Max`, and
`DeadZoneFeather`; cloud alpha is multiplied by a distance-to-rect mask so clouds never render inside
them and feather back in over `CloudDeadZoneFeatherPx` outside. The tavern rect is set once; the pit rect is re-pushed by `CloudOverlayController.UpdatePitDeadZone` whenever the pit right edge changes. Set `CloudDeadZoneEnabled = false` to
switch it off. Editing `CloudOverlay.fx` requires recompiling `CloudOverlay.fxb` via
`Content/Shaders/compileShadersFNA.bat`.

**Wizard-orb warp (#469).** `MainGameScene` draws the world with an explicit
`RenderLayerExcludeRenderer(0, RenderLayerPitWarpParty)` instead of the auto-added `DefaultRenderer`. While the
orb warp runs, `PitWarpService` (simulation, stepped by `ActivateWizardOrbAction`) moves the party composites to
`RenderLayerPitWarpParty` and drives their float, bob and spin through `HeroJumpComponent` render offsets;
`PitWarpPostProcessor` (`PitWarp.fx`) twists every pit tile (the outer wall ring and rim too, fading out softly just past it, over the wider of the old and new pit) into a drain and back, and is disabled (zero cost)
otherwise. In a replay the swirl comes from the frame's `HudRecord` (`PitWarp*` fields) and a second
`RecordedFrameRenderer` pass draws the recorded warp-layer sprites after post-processing. Editing `PitWarp.fx`
requires recompiling `PitWarp.fxb` the same way. When the orb is touched, `PitWarpRingRenderer` (order 49, after
post, under the party, additive) draws a blue ring of light spreading from the orb to the pit's width, fading from halfway out (gone at full size),
on its own clock in `PitWarpService` (`Ring*`; `HudRecord.OrbRing*` in replays). An uncovered orb that is not red
gives off rising blue motes (`WizardOrbMotesComponent`, the continuous `OrbMotes` particle effect); turning red
pauses them.

UI / HUD layers (screen-space, unaffected by camera): `RenderLayerActionQueue` (996),
`RenderLayerGraphicalHUD` (997), `RenderLayerUI` (998), `TransparentPauseOverlay` (999),
`RenderLayerSpeechBubble` (1000 — speech bubbles hold constant screen size at any zoom;
back-most of the screen-space group, so UI windows and the pause dim draw over them).

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

**Convention: every Y-sorted entity sorts at the centre of its bottom tile row.** A 32px actor
sits on its tile centre, so it needs no offset; medium/large monsters are shifted up so their
bottom tile does the same (`EnemyAnimationComponent.ApplyTileAnchorOffset`). A taller
single-sprite object whose entity is at its sprite centre must opt in with
`YSortSpriteRenderer.AnchorToBottomTile()` (offset `= (spriteHeight − 32) / 2`, zero for 32px
sprites), and call it again whenever it swaps in a sprite of a different height. Used by crops and
crop-plan previews (the 56×73 Apple Tree) and the 181px hero statue. Without it a tall crop planted
on the row directly below a building sorts *above* the building's bottom edge and draws behind it.

**Placed buildings** (Monster House / Crop Storage) deliberately sort lower than this convention. A building's entity sits at its
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

A layer that draws as several sprites with their own tints also implements `ICompositeLayerGroup`
(`PartCount`, `GetPart(i)`). `MultiSpriteAnimator` lists its parts in place of the layer, so the pose
cache, afterimages and replay capture see every part (`LayerCount`/`GetLayer` count parts).

### Hero tone layers (issue #462)

Tintable paperdoll parts are split into **tone layers** so a recolor keeps the art's shading instead of
multiplying one gray ramp by one color. Each tone is its own gray atlas animation (folder
`MaleHeroHair1ShadowMoveDown`, sprite `male_hero_hair1_shadow_move_down_0`):

| Part | Tones |
|---|---|
| Hair1 | Shadow, Midtone, Highlight, Light |
| Hair2–Hair4 | Shadow, Midtone, Highlight |
| Shirt / Skirt, Head, female Body | Shadow, Midtone |
| male Body, Armor, EyesBase, Eyes | not split (one Midtone) |

- The chosen color is the **Midtone**. Every other tone's tint is the chosen color moved by the HSV offset
  the artist painted between that tone and the Midtone (`HeroToneProfiles`, `ECS/Components/HeroTone.cs`:
  hue shift capped at `HeroToneProfile.MaxHueShift`, saturation and value ratios; value past 1 is spent as
  less saturation). **Light** (white glints, hair ties) is never tinted.
- **Hair** ignores the painted offsets and uses `HairToneRamp` (OKLab, outside DB32): Shadow = midtone
  lightness −0.21 (floor 0.22) with hue turned ≤12° toward purple; Highlight = +0.13 with hue turned ≤8° toward
  yellow and 0.9× chroma. Too light for a full highlight → pure white highlight (yellow) and deeper shadow; too dark for a
  full shadow → brighter highlight. The measured offsets left some presets flat (a highlight at value ×0.99).
  `HeroToneProfileTests.HairPresets_*` checks every `GameConfig.HairColors` preset keeps visible contrast.
- **Skin** (Head, female Body) uses `SkinToneRamp`: Shadow = midtone lightness −0.12 (floor 0.25 or 70% of a
  darker midtone), chroma ×1.15, hue turned ≤8° toward warm orange. The painted offsets doubled saturation, so pale
  blue skin got a royal-blue mask and saturated skins barely darkened. Skin presets 2 (lighter brown `#ad7550`) and 5
  (red `#f47069`, was yellow) changed with it; skins 3–5 were lightened (`#6bdfa3`, `#9cb2ff`, `#f47069`) and strongly
  colored skins (OKLab chroma ≥ 0.10→0.125) get up to 0.05 extra shadow step, so their shadows stay put and the
  contrast grows. `HeroToneProfileTests.SkinPresets_*` pins the approved shadows.
- `HeroSpriteNames.Get(...)` answers with the Midtone's names, which drive the animator; `.Tones` lists every
  tone's names back to front. The other tones show the same frame of their matching animation.
- `HeroAnimationComponent` exposes `PartCount`, `GetToneSprite`, `GetToneColor` (cached per `Color`) and
  `AppendPortraitParts`; drawn on its own (hero creation preview) its `Render` draws every tone.
- **Custom tones (issue #467)**: the hero creation screen's Skin, Hair Color and Shirt/Skirt rows always show a
  palette button; the arrows cycle presets only, and a row reads *Custom* once the picker changed its color
  (seeded from the current preset). The palette button (`ColorPaletteButton`, `UIColorPalette` in the UI atlas) opens the
  shared `ColorPickerDialog` (hue strip + saturation/value square + typed `#hex` / R / G / B fields, the skin's
  "ph-default" `TextFieldStyle`; Midtone and Shadow, plus Highlight for hair;
  Auto Shade remakes the others from the Midtone with the ramp). The picked Midtone is the design's color;
  Shadow/Highlight live in `HeroCustomTones` (`HeroDesign.SkinTones/HairTones/ShirtTones`, save section 59,
  v46) and reach the tint through `HeroAnimationComponent.CustomTones` /
  `HeroToneProfiles.Tint(..., in HeroCustomTones)`, which swaps the ramp's color for the picked one and keeps
  the chosen color's alpha. Mercenaries never have custom tones.
- Art pipeline: `Male2`/`Female2.aseprite` hold each part as an Aseprite group of tone layers (aseprite-pixel-art
  skill `examples/tone_split.lua`); `prep_for_game.lua` grays each tone on its own and prints the `profile`
  lines the table is built from; `export_game_frames.lua` names group children `<Part><Tone>`.

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

Composites any `ICompositeLayer` stack into a single 32×48 RT. Generic — works for hero
paperdolls, innkeeper, mercenaries, and any future multi-part animated actor (e.g. a dragon
built from independently-animated body-part components). Extends `SpriteCompositorBase`.

**How it works (baked frames, issue #459):**
1. Constructor receives any `ICompositeLayer[]` in back-to-front draw order.
2. `OnAddedToEntity` sets `OwnedByComposite = true` on each layer (suppresses direct render)
   and creates the character's `CharacterPoseCache` (`Rendering/CharacterPoseCache.cs`).
3. Each frame `Render()` reads the current **pose** from the layers — every layer's sprite,
   `LocalOffset`, `FlipX` and `LayerColor` — and asks the cache for it. A pose seen before
   returns its cell in the shared `CharacterFrameAtlas` (`Rendering/CharacterFrameAtlas.cs`:
   1024×1024 render-target pages cut into 32×48 cells); a new pose is baked into a fresh cell
   first (one render-target round trip, clipped to the cell by scissor). The character is then
   one quad from the shared page drawn with the composite's `Color`, `LocalOffset`, Y-sort depth
   and bounds, so characters batch with each other. Left-facing poses are baked as their own
   cells (the layers' flips and offsets are part of the pose).
4. Nothing has to announce a look change: a recolor, a new hairstyle or the Armor layer
   appearing simply produce new poses. Stale poses are dropped when the cache reaches
   `GameConfig.CharacterFrameMaxPosesPerCharacter`; cells return to the atlas in
   `OnRemovedFromEntity` (also on scene teardown). FNA never loses render-target contents, so
   pages need no copy to a `Texture2D`.
5. **Whole-body tints go on the composite `Color`, never on every layer** (death fades,
   `HeroJumpComponent.SetColor`, Vanish). A per-frame change to layer colors makes a new pose
   every frame and bakes every frame.
6. Fallback: a pose that can't be baked (more than `CharacterPoseCache.MaxLayers` = 16 layers, atlas
   full at `GameConfig.CharacterFrameMaxPages`, no graphics device) uses the base class's
   per-frame 32×48 render target, created on first use.
7. Y-sort is applied each frame by `YSortManager` (SceneComponent).
8. Replays are unchanged: `FrameCaptureAdapters.CaptureMultiSprite` still records per-layer
   Composite ops from the live layers (baked pages have no atlas name), so the layer animators
   keep ticking even though they no longer draw.

`AfterimageTrailComponent` draws each trail copy as one flat baked image from the body's cache
(layers in white, tinted by the trail color) and still records per-layer ops for replays.

**Measuring:** set `GameConfig.LogRenderStats = true` (Debug build) for a `[RenderStats]` line
every `RenderStatsWindowFrames` frames: draw calls, compositor render-target switches, baked vs
fallback characters, bakes, pages and cells in use. Draw calls come from `Batcher.DrawCallCount`
in the Nez fork.

**Key constants:**
- `RT_WIDTH = 32`, `RT_HEIGHT = 48`
- `RtEntityPivot = (16, 39)` — accounts for center sprite origin (16, 24) + local offset
  (0, −15, `GameConfig.HeroLayerOffsetY`): `pivot.Y = origin.Y + |offset.Y| = 24 + 15 = 39`

**Usage (hero/mercenary — existing ICompositeLayer implementors):**
```csharp
// Add paperdoll components first, then pass them in back-to-front order:
var animator = entity.AddComponent(new MultiSpriteAnimator(
    bodyAnimator, armorAnimator, shirtAnimator, headAnimator, // armorAnimator is null for males and skipped
    eyesBaseAnimator, eyesAnimator, hairAnimator));
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
r.AnchorToBottomTile(); // required when the sprite is taller than one tile; no-op for 32px
```

Currently used by: pit walls, wizard orbs, hero statue, crops and crop-plan previews, placed
buildings (with the bottom-edge offset described above).

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

A monster has one movement animation, `[MonsterName]MoveRight` (e.g. `SlimeMoveRight`), and only
that one (plus `[MonsterName]Attack` when the atlas has it) is registered on the animator. It
loops from spawn onwards, moving or idle. Facing left flips it (`FlipX`); facing up/down changes
nothing, so the last left/right look keeps playing. Until dedicated attack animations exist, the
attack is the flip-and-hop placeholder, which restores the prior flip when it ends.

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

`SpriteCompositorBase.Render()` (used by `StaticSpriteCompositor`, and by `MultiSpriteAnimator` only as its fallback) follows this sequence:

```
1. prevRTs = GraphicsDevice.GetRenderTargets()
2. batcher.End()                        // flush pending outer batch to scene RT
3. compositor.RenderComposite(entityPos) // sets 32×32/32×48 RT, draws children, ends
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
