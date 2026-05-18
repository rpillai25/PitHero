# PitHero — Agent Development Guidelines

This file is the source of truth for project-wide development rules for any AI agent working on PitHero (Claude Code, GitHub Copilot, Cursor, etc.). Tool-specific guidance lives alongside it (`CLAUDE.md` for Claude Code specifics).

## Project Overview

PitHero is a horizontal RPG strip game built in **C# (.NET 8.0)** with **FNA + Nez** (not MonoGame). The game runs as a borderless window at the bottom of the screen at a virtual resolution of **1920×360**. A single hero adventures in a single growing pit while the player interacts with other desktop apps.

## Commands

```bash
git submodule update --init --recursive   # first-time setup (FNA + Nez)
dotnet build PitHero.sln
dotnet run --project PitHero/PitHero.csproj
dotnet test PitHero.Tests/PitHero.Tests.csproj
```

VS Code tasks (`Ctrl+Shift+B`) cover Build, Run, Clean, Build Content, and Build Effects. Content pipeline needs MGCB.exe; shaders compile `.fx` → `.fxb` via "Build Effects".

## Architecture

```
Game1 (Nez.Core)
├── ECS/Scenes/             — Title, HeroCreation, MainGameScene
├── RolePlayingFramework/   — Pure game logic, no rendering dependencies
│   ├── Balance/BalanceConfig.cs   — Damage/XP/stat formulas
│   ├── Stats/StatBlock.cs         — STR/AGI/VIT/MAG container
│   ├── Combat/EnhancedAttackResolver.cs
│   ├── Jobs/Primary/              — Job (vocation) implementations
│   └── Equipment/, Enemies/, Skills/, Synergies/
├── AI/                     — GOAP-based hero/mercenary decision-making
│   ├── HeroStateMachine.cs, MercenaryStateMachine.cs
│   ├── BattleTacticDecisionEngine.cs
│   └── ActionQueue.cs + 20+ action types
├── Services/               — Global singletons (TextService, SaveLoadService, …)
├── UI/                     — HUD panels, shop UIs, inventory drag-drop
├── VirtualGame/            — Non-graphical simulation for balance/AI testing
└── Config/                 — GameConfig.cs (constants), CaveBiomeConfig.cs
```

**Architectural constraints:**
- **Single hero, single pit** — no multi-hero or multi-pit support
- **`WorldState` is a struct** — always pass by `ref` to methods that mutate it
- **`VirtualGame/VirtualGameSimulation.cs`** runs the full game loop without graphics; use it for testing game logic and balance without launching the game
- Virtual resolution **1920×360**; game runs borderless, always-on-top, with optional click-through; maintain integer scaling for pixel-perfect rendering
- Pit width grows every 10 pit levels (Pit Center X is dynamic); pit height is constant (Pit Center Y is constant)
- Game continues running idle while the player interacts with other desktop apps

## Hard Rules

### AOT Compliance (critical)
- Use `for` loops — **never `foreach`**
- No LINQ in performance-critical (per-frame) code
- No reflection
- Strings in the game loop must be `const` — no dynamic concatenation (`Debug.Log` is exempt)
- Pre-allocate collections with sufficient capacity; avoid `new` during gameplay

### Nez Framework
- `Game1` inherits `Nez.Core` — do not override `Draw()` or `Update()`
- Scenes inherit `Nez.Scene`, override `Initialize()` for setup
- Use `PausableSpriteAnimator` instead of `SpriteAnimator`
- Use `Nez.Time.DeltaTime` for all timing (respects `timeScale` for pausing); use `Time.TotalTime` or `Time.UnscaledDeltaTime` for absolute time
- Use `Nez.Random` instead of `System.Random`
- Register/retrieve services: `Core.Services.AddService<T>()` / `Core.Services.GetService<T>()`
- Add GOAP conditions to `GoapConstants` for strong typing
- Keep `Program.cs` as standard Nez boilerplate
- Components inherit from `Nez.Component`; rendering via `Nez.RenderableComponent` (or custom)
- Components live under `ECS/Components/`, scenes under `ECS/Scenes/`
- Hero collider uses `GameConfig.PhysicsHeroWorldLayer` and collides with `GameConfig.PhysicsTileMapLayer`
- Do not throttle entity update rate unless explicitly asked (entities update every frame)

### UI
- Use the `"ph-default"` style for all `PitHeroSkin` elements unless a unique style is explicitly needed
- Never call `SetFontScale()` — load a larger bitmap font asset instead
- Never set `FontColor` on the `ph-default` style directly — create a child style that inherits from it

### Localization
- All display text lives in `Content/Localization/en-US/UI.txt`, accessed via `TextService.GetText(TextKey.X)`
- No hardcoded display strings anywhere in game code (debug logs are exempt)

### Constants
- All sizes, positions, speeds, and physics layers go in `GameConfig.cs`
- Cave-specific progression (pit bounds, boss floors, enemy pools, loot thresholds) goes in `Config/CaveBiomeConfig.cs`
- Keep Cave floor cadence explicit (boss every 5 levels) — avoid duplicating Cave rules across generators/components
- Route Cave enemy scaling through `GetScaledEnemyLevelForPitLevel` and Cave treasure transitions through `DetermineCaveTreasureLevel`
- If a `private` method needs to be called from another class, make it `public` — don't use reflection

### Code Style
- Every public method gets a `/// <summary>` doc comment (keep it concise)
- One component class per file (structs are exempt)
- Don't mark unused methods as "unused" in comments (they may change later)
- Don't create `.md` files unless explicitly asked
- Log with `Nez.Debug`; log `Vector2`/`Point` X & Y individually and `Rectangle` X, Y, Width, Height individually — never the whole object
- Avoid excess logging unless debugging a specific issue (remove after)

## Balance & Stat System

All formulas live in `BalanceConfig.cs`. Caps are enforced via `StatConstants`:

| Derived Stat | Formula |
|---|---|
| HP | `25 + (VIT × 5)`, max 9999 |
| MP | `10 + (MAG × 3)`, max 999 |
| Stat caps | STR/AGI/VIT/MAG max 99, Level max 99 |

**Clamping helpers (always use these to enforce caps):**
- `StatConstants.ClampHP(int)` — [0, 9999]
- `StatConstants.ClampMP(int)` — [0, 999]
- `StatConstants.ClampStat(int)` — [0, 99]
- `StatConstants.ClampLevel(int)` — [1, 99]
- `StatConstants.ClampStatBlock(in StatBlock)` — clamps all stats

**Primary stats:** STR (physical attack), AGI (speed/turn order/evasion), VIT (HP pool + physical defense), MAG (MP pool + magical power).

**Key implementation files:**
- Balance: `PitHero/RolePlayingFramework/Balance/BalanceConfig.cs`
- Stats: `PitHero/RolePlayingFramework/Stats/StatBlock.cs`, `StatConstants.cs`, `GrowthCurveCalculator.cs`
- Combat: `PitHero/RolePlayingFramework/Combat/ElementType.cs`, `ElementalProperties.cs`, `EnhancedAttackResolver.cs`
- Equipment: `PitHero/RolePlayingFramework/Equipment/Gear.cs`, `GearItems.cs`
- Enemies: `PitHero/RolePlayingFramework/Enemies/IEnemy.cs` and individual enemy classes
- Jobs: `PitHero/RolePlayingFramework/Jobs/Primary/`

## Elemental System

**Element types** (`ElementType.cs`): Neutral, Fire ↔ Water, Earth ↔ Wind, Light ↔ Dark.

**Base matchup multipliers** (`ElementalProperties.cs`):
- **2.0×** — attack element opposes defender's element (advantage)
- **0.5×** — attack element matches defender's element (disadvantage)
- **1.0×** — Neutral attacks, Neutral defenders, or unrelated elements

**Custom resistances** (`ElementalProperties.Resistances`):
- Positive → resistance (damage reduction)
- Negative → weakness (damage increase)
- Use `BalanceConfig.GetElementalDamageMultiplier(attackElement, defenderProps)` for the full calculation

## TileMap Layers

`Base` → `Collision` → `FogOfWar` (4 surrounding tiles cleared when the hero lands on the tile below).

## Reference Documents

Design docs under `PitHero/docs/` (kept as standalone references — don't duplicate their content into agent files or skills):

**Balance / data:**
- `PitHero/docs/EquipmentBalanceGuide.md`
- `PitHero/docs/MonsterBalanceGuide.md`
- `PitHero/docs/JobStatCurves.md`
- `PitHero/docs/CaveBiomeBalanceReport.md`
- `PitHero/docs/EquipmentLibrary.md`
- `PitHero/docs/MonsterLibrary.md`

**Architecture / subsystems:**
- `PitHero/docs/RolePlayingFramework.md`
- `PitHero/docs/VirtualGameLogicLayer.md`
- `PitHero/docs/JpSystem.md` — Job Points, skill purchase flow, mastery
- `PitHero/docs/SynergySystem.md` — Inventory pattern matching architecture
- `PitHero/docs/DynamicPit.md` — `PitWidthManager` + expansion cadence
- `PitHero/docs/Permadeath.md` — Hero death, crystal vault, sell-value formula
- `PitHero/docs/HeroPromotion.md` — Mercenary→hero conversion via crystal/statue

**Per-feature docs:**
- `features/`

## Agent Skills (Claude Code)

Domain skills under `.claude/skills/` provide on-demand guidance via progressive disclosure. They surface automatically based on task context — don't reference them explicitly:

- `nez-ai` — GOAP, state machines, behavior trees, virtual-layer AI
- `nez-ui` — Nez.UI patterns, skins, drag-drop, dialogs, UI implementation
- `monster-design` — monster balance, biome progression, `PitHero/docs/MonsterLibrary.md`
- `equipment-design` — equipment balance, biome progression, `PitHero/docs/EquipmentLibrary.md`
- `pit-balance-test` — virtual-game balance testing across pit levels
- `virtual-game-layer` — coverage analysis for `VirtualGame/`
- `make-skill-template` — meta-skill for scaffolding new skills

`.github/skills/` mirrors `.claude/skills/` via symlinks; `.claude/` is canonical. On Windows clones, ensure developer mode is enabled and `git config core.symlinks true`.
