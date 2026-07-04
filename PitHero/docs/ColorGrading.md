# Color Grading — Tilemap Day/Night System

## Overview

Day/night color grading is applied to the **tilemap terrain layers** (Base, Detail, Top) plus a few **environment sprites** (pit walls, and placed buildings — MonsterHouse & CropStorage) by attaching a shared `Material` (backed by `ColorGradingEffect`) to their renderers. It cross-fades between two LUT textures (LUT A and LUT B) using a GPU-side `BlendFactor` uniform, so all transitions happen on the shader with no CPU overhead beyond computing the blend value.

Because the grade lives on individual renderers' material rather than a full-screen post-processor, **actors, monsters, dropped items, the FogOfWar layer, and the UI are NOT tinted** — they keep their normal daytime colors at all times of day. `ColorGradingController` owns the effect, the three LUTs, and the shared material, and drives `UpdateTimeOfDay()` each frame. It is registered as a Nez service so any spawner (e.g. `PitGenerator`, `BuildingModeOverlay`) can fetch `ColorGradingController.Material` and `SetMaterial` it onto the sprites it wants graded.

## LUT Files

All three files live in `PitHero/Content/Shaders/` and are loaded as raw PNGs at scene startup (not through the MGCB pipeline):

| File | Period | Description |
|---|---|---|
| `lut_default.png` | Day | Identity-ish LUT — natural, unmodified look |
| `lut_dusk_dawn.png` | Dawn & Dusk | Warm, golden-hour tone |
| `lut_night.png` | Night | Cool, desaturated blue-shift |

LUT format: 64×64 px, Size=16, SizeRoot=4 (4×4 grid of 16×16 blue-slices). All three must share the same Size/SizeRoot or `ColorGradingEffect.Size`/`SizeRoot` must be adjusted to match.

## Time-of-Day Schedule

`InGameTimeService` drives the blend. `SecondsPerInGameHour = 60f` (1 real second = 1 in-game minute).

| In-Game Time | Phase | LUT A | LUT B | Blend |
|---|---|---|---|---|
| 4:00 – 5:00 AM | Night → Dawn | `lut_night` | `lut_dusk_dawn` | linear 0→1 (60 min) |
| 5:00 – 5:30 AM | Pure Dawn | `lut_dusk_dawn` | — | 0 |
| 5:30 – 6:30 AM | Dawn → Day | `lut_dusk_dawn` | `lut_default` | linear 0→1 (60 min) |
| 6:30 AM – 5:30 PM | Pure Day | `lut_default` | — | 0 |
| 5:30 – 6:30 PM | Day → Dusk | `lut_default` | `lut_dusk_dawn` | linear 0→1 (60 min) |
| 6:30 – 7:00 PM | Pure Dusk | `lut_dusk_dawn` | — | 0 |
| 7:00 – 9:00 PM | Dusk → Night | `lut_dusk_dawn` | `lut_night` | linear 0→1 (120 min) |
| 9:00 PM – 4:00 AM | Pure Night | `lut_night` | — | 0 |

The ±30-minute windows around the 6 AM and 6 PM boundaries (5:30–6:30) ensure no hard pop at those transitions.

## Key Files

| File | Role |
|---|---|
| `PitHero/Content/Shaders/ColorGrading.fx` | HLSL source — PS-only SM3.0, dual LUT sampler + BlendFactor |
| `PitHero/Content/Shaders/ColorGrading.fxb` | Compiled binary — regenerate via `compileShadersFNA.bat` from that directory |
| `PitHero/Content/Shaders/compileShadersFNA.bat` | Calls `fxc.exe /T fx_2_0 ColorGrading.fx /Fo ColorGrading.fxb` |
| `PitHero/Graphics/Effects/ColorGradingEffect.cs` | Effect wrapper — exposes `LookUpTableA`, `LookUpTableB`, `BlendFactor`, `Size`, `SizeRoot` |
| `PitHero/Graphics/Effects/ColorGradingController.cs` | Owns the effect, all 3 LUTs, and the shared `Material`; implements `UpdateTimeOfDay()` |
| `PitHero/ECS/Scenes/MainGameScene.cs` | Creates the controller in `LoadMap()`, `SetMaterial`s it onto the Base/Detail/Top renderers; calls `_colorGrading?.UpdateTimeOfDay()` each frame after `InGameTimeService.Update()`; disposes it in `Unload()` |

All files are in namespace `PitHero.Rendering` (not `PitHero.Graphics`, which collides with `Nez.Graphics`).

## How the Shader Works

The pixel shader (`ColorGrading.fx`) reads the sampled tile texel from `InputSampler : register(s0)` (bound automatically by the Nez `Batcher` — the tile atlas when used as a sprite material). For each pixel it performs a trilinear LUT lookup on both `LUTSamplerA` and `LUTSamplerB`:
- Hardware bilinear handles red+green interpolation within a blue-slice.
- Manual `lerp` between adjacent blue-slices handles the blue axis.
- A final `lerp(gradedA, gradedB, BlendFactor)` cross-fades the two results.

Because it runs as a **per-sprite material** (not a full-screen pass over an opaque frame), the shader preserves the source alpha and applies the vertex color: `return float4(graded, src.a) * input.Color;`. This keeps transparent tile pixels transparent and honors per-layer opacity. Pixel-art tiles have hard (binary) alpha, so this is correct whether textures are premultiplied or not; if soft/antialiased edges ever show color halos, switch to the premultiplied form `float4(graded * src.a, src.a) * input.Color`.

`InvLUTSize = 1 / (SizeRoot × Size)` is updated from C# whenever `Size` or `SizeRoot` change and stays constant otherwise.

## Extending the System

**Adding a new LUT phase** (e.g., an indoor or cave look):
1. Drop a matching 64×64 PNG in `Content/Shaders/`.
2. Load it in the `ColorGradingController` constructor and store as a field.
3. Add branches to `UpdateTimeOfDay()` as needed.
4. Dispose it in `Dispose()`.

**Adjusting timing constants**: All time boundaries are plain float literals in `UpdateTimeOfDay()` — edit in place. Hours are fractional (e.g. 17.5f = 5:30 PM).

**Changing which layers are graded**: In `MainGameScene.LoadMap()`, the shared `_colorGrading.Material` is assigned to `baseLayerRenderer`, `detailLayerRenderer`, and `topLayerRenderer`. Add `fogLayerRenderer.SetMaterial(...)` to include FogOfWar, or drop a `SetMaterial` call to exclude a layer.

**Disabling the effect**: skip the three `SetMaterial(...)` calls in `MainGameScene.LoadMap()` (the terrain renderers then draw with no material).

**Recompiling the shader**: Run `compileShadersFNA.bat` from `PitHero/Content/Shaders/` after any `.fx` edit. The `.fxb` must be recompiled before the change takes effect.
