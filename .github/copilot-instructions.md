# GitHub Copilot Instructions – Pit Hero

## Project Overview
PitHero is a horizontal RPG strip game built with C# using **FNA + Nez** (not MonoGame). The game runs as a borderless window at the bottom of the screen with a virtual resolution of 1920×360. It features an event-driven architecture with a comprehensive balance system for RPG progression.

---

## General Development Rules

### Code Style & Documentation
- Comment all methods with `/// <summary>` tags (keep concise)
- Do not mark unused methods as unused in comments (may change later)
- Do not create .md files unless explicitly told to do so
- Each component class must be in its own file (structs are an exception)
- Avoid using Reflection
- For random numbers use `Nez.Random` instead of `System.Random`

### Build & Testing
- Run `dotnet build` after making code changes to ensure it compiles
- Run `dotnet test PitHero.Tests/` for unit tests after code changes are complete
- Initialize submodules before first build: `git submodule update --init --recursive`
- Clone FNA if missing: `git clone --recursive https://github.com/FNA-XNA/FNA.git FNA`
- Both FNA and Nez must be properly initialized before building

### Debugging & Logging
- Use `Nez.Debug` for all logs
- Log Vector2/Point X & Y components individually (not the whole object)
- Log Rectangle X, Y, Width, and Height individually (not the whole object)
- Avoid excess logging unless debugging a specific issue (remove after fixing)

---

## Architecture Guidelines

### Game Architecture Constraints
- **Single Hero**: Only 1 active hero at a time (no multiple heroes)
- **Single Pit**: Only 1 active pit that grows in width as the player progresses
- **Virtual Logic Layer**: `PitHero.VirtualGame` simulates the game in a non-graphical context for testing
- **WorldState**: This is a struct. Must be passed by reference to methods that update it (passing by value only modifies a copy)

### ECS Pattern with Nez Framework
- All components inherit from `Nez.Component`
- Use `Nez.RenderableComponent` or custom extensions for rendering
- Components under `ECS/Components/`, Scenes under `ECS/Scenes/`
- Hero has a collider with `PhysicsLayer = GameConfig.PhysicsHeroWorldLayer`
- Hero collider collides with TileMap (`GameConfig.PhysicsTileMapLayer`)

### Nez Framework Compliance
- NEVER use SetFontScale() for any UI element.  If I want to scale a font, I will create a larger font.
- Game1 should inherit from `Nez.Core` (do not override Draw() or Update() methods)
- Scenes should inherit from `Nez.Scene` and override Initialize() for setup
- Use `PausableSpriteAnimator` instead of `SpriteAnimator`
- Use `Nez.Time.DeltaTime` for all timing calculations (movement, animations) for proper timeScale support
- Record total game time with `Time.TotalTime` or `Time.UnscaledDeltaTime`
- Do not throttle entity update rate unless explicitly asked (entities update every frame)
- Services: Register with `Core.Services.AddService()`, retrieve with `Core.Services.GetService<Service>()`
- GOAP Conditions: Add to `GoapConstants` for strong typing
- Keep `Program.cs` as standard Nez boilerplate (only modify if absolutely required)
- For UI code that uses PitHeroSkin, use the "ph-default" style for all elements (unless a unique style is explicitly needed)
- Never override the ph-default style's FontColor by doing a .GetStyle().FontColor = someColor.  If a unique color is needed, create a new style that inherits from ph-default and set the FontColor there.

### Game World Layout
- Virtual resolution: **1920×360** (horizontal strip at bottom of screen)
- Game runs borderless, always-on-top, with optional click-through
- Maintain integer scaling for pixel-perfect rendering
- Pit width (tiles) is dynamic, changes every 10 pit levels (Pit Center X is dynamic)
- Pit height (tiles) is constant (Pit Center Y is constant)
- Game continues running idle while player interacts with other desktop apps

### TileMap Layers
- `Base`: The lowest layer
- `Collision`: The collision layer
- `FogOfWar`: FogOfWar layer (4 surrounding tiles cleared when Hero lands on tile underneath)

### Constants & Configuration
- Keep all constants in `GameConfig.cs` (sizes, positions, movement speeds, physics layers)
- Use `PitHero/Config/CaveBiomeConfig.cs` for Cave-specific progression rules (pit bounds, boss floors, enemy pools, cave loot thresholds)
- Keep Cave floor cadence explicit (boss every 5 levels) and avoid duplicating Cave rules across generators/components
- Route Cave enemy scaling through `GetScaledEnemyLevelForPitLevel` and Cave treasure transitions through `DetermineCaveTreasureLevel`
- If a private method needs to be called from another class, make it public

### AOT Compilation Compliance
- Avoid garbage generation during gameplay
- Only use strings as `const` (no dynamic string concatenation/patterns in game loop)
  - Exception: Debug.Log statements can use dynamic strings
- Pre-allocate everything ahead of the game loop (avoid `new` keyword during gameplay)
- Avoid using LINQ in performance-critical code
- Initialize collections with large enough capacity to avoid internal resizing
- **Use `for` loops instead of `foreach` loops** (VERY IMPORTANT for AOT)

---

## Balance System Overview

PitHero features a comprehensive RPG balance system with centralized formulas and stat progression for heroes, monsters, equipment, and elemental combat. All balance formulas are defined in `BalanceConfig.cs` for easy tuning.

Detailed domain-specific balance knowledge lives in the specialist agents and guide files:
- **Monster design**: Monster Designer agent + `MONSTER_BALANCE_GUIDE.md`
- **Equipment design**: Equipment Designer agent + `EQUIPMENT_BALANCE_GUIDE.md`
- **Balance testing**: Pit Balance Tester agent + `JOB_STAT_CURVES.md`
- **Implementation patterns**: Principal Game Engineer agent

### Key Implementation Files
- **Balance**: `PitHero/RolePlayingFramework/Balance/BalanceConfig.cs`
- **Stats**: `PitHero/RolePlayingFramework/Stats/StatBlock.cs`, `StatConstants.cs`, `GrowthCurveCalculator.cs`
- **Combat**: `PitHero/RolePlayingFramework/Combat/ElementType.cs`, `ElementalProperties.cs`, `EnhancedAttackResolver.cs`
- **Equipment**: `PitHero/RolePlayingFramework/Equipment/Gear.cs`, `GearItems.cs`
- **Enemies**: `PitHero/RolePlayingFramework/Enemies/IEnemy.cs` and individual enemy classes
- **Jobs**: `PitHero/RolePlayingFramework/Jobs/Primary/`

---

## Stat System

### Stat Caps (StatConstants.cs)
All heroes, jobs, and equipment must respect these hard caps:
- **HP**: Maximum 9999 (`StatConstants.MaxHP`)
- **MP**: Maximum 999 (`StatConstants.MaxMP`)
- **Stats** (STR/AGI/VIT/MAG): Maximum 99 each (`StatConstants.MaxStat`)
- **Level**: Maximum 99 (`StatConstants.MaxLevel`)

### Stat Clamping Functions
Use `StatConstants` methods to enforce caps:
- `ClampHP(int hp)`: Clamps HP to [0, 9999]
- `ClampMP(int mp)`: Clamps MP to [0, 999]
- `ClampStat(int stat)`: Clamps stat to [0, 99]
- `ClampLevel(int level)`: Clamps level to [1, 99]
- `ClampStatBlock(in StatBlock stats)`: Clamps all stats in a StatBlock

### Primary Stats
- **Strength (STR)**: Physical attack power
- **Agility (AGI)**: Speed, turn order, evasion
- **Vitality (VIT)**: HP pool and physical defense
- **Magic (MAG)**: MP pool and magical power

### Derived Stats
- **HP**: `25 + (Vitality × 5)` (max 9999)
- **MP**: `10 + (Magic × 3)` (max 999)

---

## Elemental System

### Element Types (ElementType.cs)
- **Neutral**: No advantages or disadvantages
- **Fire**: Opposes Water
- **Water**: Opposes Fire
- **Earth**: Opposes Wind
- **Wind**: Opposes Earth
- **Light**: Opposes Dark
- **Dark**: Opposes Light

### Base Elemental Matchup Rules (ElementalProperties.cs)
**Damage Multipliers**:
- **2.0x damage**: Attack element opposes defender's element (advantage)
- **0.5x damage**: Attack element matches defender's element (disadvantage)
- **1.0x damage**: Neutral attacks, Neutral defenders, or unrelated elements

### Custom Resistances (ElementalProperties.Resistances)
- **Positive values**: Resistance (damage reduction)
- **Negative values**: Weakness (damage increase)
- Use `BalanceConfig.GetElementalDamageMultiplier(attackElement, defenderProps)` for complete calculation