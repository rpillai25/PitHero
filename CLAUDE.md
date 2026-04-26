# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

PitHero is a 2D horizontal RPG strip game built in **C# (.NET 8.0)** using **FNA + Nez** (not MonoGame). The game runs as a borderless window at the bottom of the screen at a virtual resolution of **1920×360**. A single hero adventures in a single growing pit while the player interacts with other desktop apps.

## Commands

```bash
# First-time setup — initialize submodules (FNA and Nez)
git submodule update --init --recursive

# Build
dotnet build PitHero.sln

# Run
dotnet run --project PitHero/PitHero.csproj

# Test
dotnet test PitHero.Tests/PitHero.Tests.csproj

# Content pipeline (requires MGCB.exe — use VS Code task "Build Content" instead)
# Shaders: use VS Code task "Build Effects" to compile .fx → .fxb
```

VS Code tasks (`Ctrl+Shift+B`) cover Build, Run, Clean, Build Content, and Build Effects.

## Architecture

```
Game1 (Nez.Core)
├── ECS/Scenes/          — Title, HeroCreation, MainGameScene
├── RolePlayingFramework/ — Pure game logic, no rendering dependencies
│   ├── Balance/BalanceConfig.cs      — All damage/XP/stat formulas
│   ├── Stats/StatBlock.cs            — Core stat container (STR/AGI/VIT/MAG)
│   ├── Combat/EnhancedAttackResolver.cs
│   ├── Jobs/Primary/                 — Job (vocation) implementations
│   └── Equipment/, Enemies/, Skills/, Synergies/
├── AI/                  — GOAP-based hero/mercenary decision-making
│   ├── HeroStateMachine.cs, MercenaryStateMachine.cs
│   ├── BattleTacticDecisionEngine.cs
│   └── ActionQueue.cs + 20+ action types
├── Services/            — Global singletons (TextService, SaveLoadService, etc.)
├── UI/                  — HUD panels, shop UIs, inventory drag-drop
├── VirtualGame/         — Non-graphical simulation for balance/AI testing
└── Config/              — GameConfig.cs (constants), CaveBiomeConfig.cs
```

**Key architectural constraints:**
- **Single hero, single pit** — no multi-hero or multi-pit support
- **`WorldState` is a struct** — always pass by `ref` to methods that mutate it
- **`VirtualGame/VirtualGameSimulation.cs`** runs the full game loop without graphics; use it for testing game logic and balance without launching the game

## Hard Rules

### AOT Compliance
- Use `for` loops — **never `foreach`** (critical for AOT)
- No LINQ in performance-critical (per-frame) code
- No reflection
- Strings in the game loop must be `const` — no dynamic concatenation (Debug.Log is exempt)
- Pre-allocate collections with sufficient capacity; avoid `new` during gameplay

### Nez Framework
- `Game1` inherits `Nez.Core` — do not override `Draw()` or `Update()`
- Scenes inherit `Nez.Scene`, override `Initialize()` for setup
- Use `PausableSpriteAnimator` instead of `SpriteAnimator`
- Use `Nez.Time.DeltaTime` for all timing (respects `timeScale` for pausing)
- Use `Nez.Random` instead of `System.Random`
- Register/retrieve services: `Core.Services.AddService<T>()` / `Core.Services.GetService<T>()`
- Add GOAP conditions to `GoapConstants` for strong typing

### UI
- Use the `"ph-default"` style for all `PitHeroSkin` elements unless a unique style is explicitly needed
- Never call `SetFontScale()` — create a larger font asset instead
- Never set `FontColor` on the `ph-default` style directly — create a child style that inherits from it

### Localization
- All display text lives in `Content/Localization/en-US/UI.txt`, accessed via `TextService.GetText(TextKey.X)`
- No hardcoded display strings anywhere in game code (debug logs are exempt)

### Constants
- All sizes, positions, speeds, and physics layers go in `GameConfig.cs`
- Cave-specific progression (pit bounds, boss floors, enemy pools, loot thresholds) goes in `CaveBiomeConfig.cs`
- If a `private` method needs to be called from another class, make it `public` — don't use reflection

### Code Style
- Every public method gets a `/// <summary>` doc comment (keep it concise)
- One component class per file (structs are exempt)
- Log with `Nez.Debug`; log `Vector2`/`Point`/`Rectangle` fields individually, not the whole object

## Balance & Stat System

All formulas are in `BalanceConfig.cs`. Caps are enforced via `StatConstants`:

| Derived Stat | Formula |
|---|---|
| HP | `25 + (VIT × 5)`, max 9999 |
| MP | `10 + (MAG × 3)`, max 999 |
| Stat caps | STR/AGI/VIT/MAG max 99, Level max 99 |

Use `StatConstants.ClampHP/MP/Stat/Level/StatBlock()` to enforce caps.

**Elemental multipliers**: 2.0× on advantage, 0.5× on same element. Use `BalanceConfig.GetElementalDamageMultiplier(attackElement, defenderProps)`.

Balance reference docs at repo root: `EQUIPMENT_BALANCE_GUIDE.md`, `JOB_STAT_CURVES.md`, `CAVE_BIOME_BALANCE_REPORT.md`.

## TileMap Layers

`Base` → `Collision` → `FogOfWar` (4 surrounding tiles cleared when hero lands on tile below)

## Documentation

- `.github/copilot-instructions.md` — extended development rules (source of truth for patterns not covered here)
- `.github/agents/` — specialist agent specs for equipment design, monster design, UI, balance testing
- `PitHero/docs/RolePlayingFramework.md` — detailed RPG system design
- `features/` — feature design docs for cave biomes and other systems
