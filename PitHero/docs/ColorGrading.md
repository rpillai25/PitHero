# Color Grading — Post-Processing & Day/Night System

## Overview

A full-screen color grading pass runs every frame via `ColorGradingPostProcessor`. It cross-fades between two LUT textures (LUT A and LUT B) using a GPU-side `BlendFactor` uniform, so all transitions happen on the shader with no CPU overhead beyond computing the blend value.

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
| `PitHero/Graphics/Effects/ColorGradingPostProcessor.cs` | `PostProcessor<ColorGradingEffect>` — loads all 3 LUTs, implements `UpdateTimeOfDay()` |
| `PitHero/ECS/Scenes/MainGameScene.cs` | Instantiates the PostProcessor; calls `_colorGrading?.UpdateTimeOfDay()` each frame after `InGameTimeService.Update()` |

All files are in namespace `PitHero.Rendering` (not `PitHero.Graphics`, which collides with `Nez.Graphics`).

## How the Shader Works

The pixel shader (`ColorGrading.fx`) reads the scene from `InputSampler : register(s0)` (bound automatically by the Nez `Batcher`). For each pixel it performs a trilinear LUT lookup on both `LUTSamplerA` and `LUTSamplerB`:
- Hardware bilinear handles red+green interpolation within a blue-slice.
- Manual `lerp` between adjacent blue-slices handles the blue axis.
- A final `lerp(gradedA, gradedB, BlendFactor)` cross-fades the two results.

`InvLUTSize = 1 / (SizeRoot × Size)` is updated from C# whenever `Size` or `SizeRoot` change and stays constant otherwise.

## Extending the System

**Adding a new LUT phase** (e.g., an indoor or cave look):
1. Drop a matching 64×64 PNG in `Content/Shaders/`.
2. Load it in `ColorGradingPostProcessor.OnAddedToScene()` and store as a field.
3. Add branches to `UpdateTimeOfDay()` as needed.
4. Dispose it in `Unload()`.

**Adjusting timing constants**: All time boundaries are plain float literals in `UpdateTimeOfDay()` — edit in place. Hours are fractional (e.g. 17.5f = 5:30 PM).

**Disabling the effect**: `_colorGrading.Enabled = false` at runtime, or comment out the `AddPostProcessor` call in `MainGameScene.Initialize()`.

**Recompiling the shader**: Run `compileShadersFNA.bat` from `PitHero/Content/Shaders/` after any `.fx` edit. The `.fxb` must be recompiled before the change takes effect.
