# PitHero — Agent Development Guidelines

This file is the source of truth for project-wide development rules for any AI agent working on PitHero (Claude Code, GitHub Copilot, Cursor, etc.). Tool-specific guidance lives alongside it (`CLAUDE.md` for Claude Code specifics).

## Project Overview

PitHero is a horizontal RPG strip game built in **C# (.NET 8.0)** with **FNA + Nez** (not MonoGame). The game runs as a borderless window at the bottom of the screen at a virtual resolution of **1920×`GameConfig.VirtualHeight`** (currently **296**; was 360 — the OS window height follows the constant). A single hero adventures in a single growing pit while the player interacts with other desktop apps.

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
- Virtual resolution **1920×`GameConfig.VirtualHeight`** (currently **296**, flip the constant to compare heights; every tall UI window fits itself to `Stage.GetHeight()` at show time); game runs borderless, always-on-top, with optional click-through; maintain integer scaling for pixel-perfect rendering
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
- **Every service registered in `MainGameScene` (`Begin()` or `LoadMap`) needs a matching `RemoveService` in `MainGameScene.Unload()`** — loading a save tears down the scene and re-runs `Begin()`; a missing removal crashes `AddService` with a duplicate-key `ArgumentException`. Also mind registration order within `Begin()`: a service that captures another in its constructor must be registered after its dependency (e.g. `AutoSeedPurchaseService` after `FarmTaskCoordinator`)
- Add GOAP conditions to `GoapConstants` for strong typing
- Keep `Program.cs` as standard Nez boilerplate
- Components inherit from `Nez.Component`; rendering via `Nez.RenderableComponent` (or custom)
- Components live under `ECS/Components/`, scenes under `ECS/Scenes/`
- Hero collider uses `GameConfig.PhysicsHeroWorldLayer` and collides with `GameConfig.PhysicsTileMapLayer`
- Do not throttle entity update rate unless explicitly asked (entities update every frame)

### Rendering

See `PitHero/docs/RenderingSystem.md` for the full reference.  Key rules:

- **Never use plain `SpriteRenderer` at `RenderLayerActors` or `RenderLayerSingleTileObject`** — it skips Y-sort and renders in arbitrary order.
- Multi-layer humanoid actors (hero, mercenaries, innkeeper) → `MultiSpriteAnimator` at `RenderLayerActors`.
- Multi-layer static objects (treasure chests) → `StaticSpriteCompositor` at `RenderLayerSingleTileObject`.
- Single-sprite world objects (walls, orbs, statues, any ≤ 32×32 tile objects) → `YSortSpriteRenderer` at `RenderLayerSingleTileObject`.
- Single-sprite larger world objects → `YSortSpriteRenderer` at `RenderLayerActors`.
- Animated single-sprite monsters → subclass `EnemyAnimationComponent` at `RenderLayerActors`.
- Y-sort (`LayerDepth`) updates are tile-row-snapped and change-gated — do not call `SetLayerDepth` every frame unconditionally.

### UI
- Use the `"ph-default"` style for all `PitHeroSkin` elements unless a unique style is explicitly needed
- Never call `SetFontScale()` — load a larger bitmap font asset instead
- Never set `FontColor` on the `ph-default` style directly — create a child style that inherits from it

### Input
- Any world-space mouse pick must gate on `MouseUtils.IsMouseInsideWindow()` before acting. The OS
  reports coordinates outside the window (negative, or past `Screen.Width/Height`) when the cursor
  is on another monitor, and `Camera.MouseToWorldPoint()` maps those onto real tiles — so an
  ungated pick fires on objects the user never pointed at
- Gate hover the same way as clicks, or outlines/ghosts track a cursor that isn't there
- `Stage.Hit(Stage.GetMousePosition())` (pointer-over-UI) is a *separate* guard — most pick sites need both

### Localization
- All display text lives in `Content/Localization/en-us/*.txt` (one file per `TextType`: UI, Inventory, Skill, Job, Monster, Dialogue, Name), accessed via `TextService.DisplayText(TextType.X, SomeTextKey.Y)`
- No hardcoded display strings anywhere in game code (debug logs are exempt)
- `Names.txt` is **list-valued**: each line is `PoolKey,entry,entry,...`, a key may repeat across lines (entries append), and callers read it with `TextService.DisplayTextList`. Character name pools live there, not in C#

### Constants
- All sizes, positions, speeds, and physics layers go in `GameConfig.cs`
- Cave-specific progression (pit bounds, boss floors, enemy pools, loot thresholds) goes in `Config/CaveBiomeConfig.cs`
- Keep Cave floor cadence explicit (boss every 5 levels) — avoid duplicating Cave rules across generators/components
- Route Cave enemy scaling through `GetScaledEnemyLevelForPitLevel` and Cave treasure transitions through `DetermineCaveTreasureLevel`
- If a `private` method needs to be called from another class, make it `public` — don't use reflection

### Save Format (backwards compatibility is mandatory)
- The save system lives in `Services/SaveData.cs` (`CurrentVersion` / `MinSupportedVersion`). When a feature changes the byte layout, bump `CurrentVersion` **and keep every version from `MinSupportedVersion` up loadable**: read new fields conditionally on the file's version (`fileVersion >= N ? reader.ReadX() : safeDefault`) and pick defaults that degrade gracefully (e.g. "no active buff", "feature off")
- Never raise `MinSupportedVersion` or drop a reader path on your own. Periodic **save unifications** (collapsing old versions into one, e.g. issue #311 → v17, PR #391 → v29) happen **only when the owner explicitly asks for one** — a past unification is a one-time cleanup, not a standing policy of rejecting old saves
- Every version bump gets a backwards-compatibility test proving the previous layout still reads (see `SaveData_V29DiningRecord_ReadsWithDefaultExpiry` in `SaveLoadTests.cs`); loads always rewrite at `CurrentVersion` on the next save

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
- `PitHero/docs/AnalyticsSchema.md` — debug-only balance analytics: JSONL event schema, output location, interpretation caveats
- `PitHero/docs/EquipmentBalanceGuide.md`
- `PitHero/docs/MonsterBalanceGuide.md`
- `PitHero/docs/JobStatCurves.md`
- `PitHero/docs/CaveBiomeBalanceReport.md`
- `PitHero/docs/EquipmentLibrary.md`
- `PitHero/docs/MonsterLibrary.md`

**Architecture / subsystems:**
- `PitHero/docs/RenderingSystem.md` — render layer stack, Y-sort, MultiSpriteAnimator / StaticSpriteCompositor / YSortSpriteRenderer
- `PitHero/docs/ParticleEffects.md` — ParticleEffectManager, .pex authoring quirks, sizing rules, effect patterns (attached/projectile/AoE), battle + out-of-battle wiring map
- `PitHero/docs/RolePlayingFramework.md`
- `PitHero/docs/VirtualGameLogicLayer.md`
- `PitHero/docs/JpSystem.md` — Job Points, skill purchase flow, mastery
- `PitHero/docs/SynergySystem.md` — Inventory pattern matching architecture
- `PitHero/docs/ColorGrading.md` — dual-LUT post-processor, day/night schedule, shader recompile steps
- `PitHero/docs/DynamicPit.md` — `PitWidthManager` + expansion cadence
- `PitHero/docs/Permadeath.md` — Hero death, crystal vault, sell-value formula
- `PitHero/docs/CrystalCeremony.md` — Post-death hero respawn and crystal imbuement at the statue
- `PitHero/docs/TavernDiningSystem.md` — Kitchen/tavern dining: ticket lifecycle, worker FSM roles, patron + party dining flows, meal buffs, dish pricing, save v18 dining state; plus the pre-stock fridge system (issue #386): `FridgeInventoryService` slot inventory, runner pre-stock jobs with held-for-transfer storage reservations (save-anytime lossless), runner carry levels, runner-only staff-exit routing, and the Refrigerator window
- `PitHero/docs/AutoJobAssignmentSystem.md` — Automated monster job assignment: demand evaluators, pure solver, day/night shifts, reassess cadence, and the step-by-step recipe for adding a new job (e.g. fishing)
- `PitHero/docs/AutomationSystem.md` — The Settings → Automation tab as a whole: which service owns each toggle, ticked vs call-driven automations, the shared Gold Buffer, append-only save sections, the two-phase load sync, the grayed-control recipe, and how to add a new Automation option
- `PitHero/docs/SpeechBubbleSystem.md` — Speech-bubble dialogue: SpeechBubbleComponent + SpeechBubbleDialogue option/gate tables, Dialogue.txt localization rules, full event catalog, the System.Random-not-Nez.Random rule, and the recipe for adding a new dialogue event or speaker
- `PitHero/docs/ThreatSystem.md` — Battle threat/aggro: percent-of-max-HP threat units, per-skill `ThreatValue`, Knight ×2, evasion escalation, round decay, one-`NextFloat` monster target pick, red HUD tint plumbing, tank AI tiers (hold/maintain/rescue), and the out-of-turn Knight Provoke reaction
- `PitHero/docs/ShuffleBagSystem.md` — Shuffle-bag ("marble bag") controlled randomness: ShuffleBag<T>/NextFromRoll, the two-floats-per-ally-attack battle RNG contract, CritBagSet persistence, LootBagSet compositions + LiveBags null fallback, boss epic chest rules, tavern dish bag, and the recipe for adding a new bag-driven drop

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
