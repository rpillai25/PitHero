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
- Game1 should inherit from `Nez.Core` (do not override Draw() or Update() methods)
- Scenes should inherit from `Nez.Scene` and override Initialize() for setup
- Use `PausableSpriteAnimator` instead of `SpriteAnimator`
- Use `Nez.Time.DeltaTime` for all timing calculations (movement, animations) for proper timeScale support
- Record total game time with `Time.TotalTime` or `Time.UnscaledDeltaTime`
- Do not throttle entity update rate unless explicitly asked (entities update every frame)
- Services: Register with `Core.Services.AddService()`, retrieve with `Core.Services.GetService<Service>()`
- GOAP Conditions: Add to `GoapConstants` for strong typing
- Keep `Program.cs` as standard Nez boilerplate (only modify if absolutely required)

### UI Components
- Use `HoverableImageButton` instead of Nez `ImageButton`
- Use Nez `TabPane` for tab functionality
- Use `EnhancedSlider` instead of Nez `Slider`
- For new UI needs: inherit and override Nez classes first; if not possible, duplicate Nez element and enhance
- Live strip renders current `WorldState`

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

### Balance System Architecture
The balance system consists of several interconnected components:
- **Stat System**: Core stats (Strength, Agility, Vitality, Magic) with hard caps
- **Level Progression**: Experience curves and stat growth for heroes/jobs
- **Monster Generation**: Archetype-based stat formulas with elemental properties
- **Equipment System**: Pit-level and rarity-based stat bonuses
- **Elemental Combat**: Elemental matchups with damage multipliers and resistances
- **Combat Resolution**: Damage calculation integrating stats, equipment, and elements

### Key Implementation Files
- **Balance**: `PitHero/RolePlayingFramework/Balance/BalanceConfig.cs`
- **Stats**: `PitHero/RolePlayingFramework/Stats/StatBlock.cs`, `StatConstants.cs`, `GrowthCurveCalculator.cs`
- **Combat**: `PitHero/RolePlayingFramework/Combat/ElementType.cs`, `ElementalProperties.cs`, `EnhancedAttackResolver.cs`
- **Equipment**: `PitHero/RolePlayingFramework/Equipment/Gear.cs`, `GearItems.cs`
- **Enemies**: `PitHero/RolePlayingFramework/Enemies/IEnemy.cs` and individual enemy classes
- **Jobs**: `PitHero/RolePlayingFramework/Jobs/Primary/`, `Secondary/`, `Tertiary/`

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

## Monster Generation Guidelines

### Monster Archetypes (BalanceConfig.MonsterArchetype)
Monsters are created using one of four archetypes, each with distinct stat distributions:

**1. Balanced** (baseline archetype)
- Multipliers: HP 1.0x, STR 1.0x, AGI 1.0x, VIT 1.0x, MAG 1.0x
- Use for: General encounters, teaching combat mechanics

**2. Tank** (high HP/defense, low speed)
- Multipliers: HP 1.5x, STR 0.8x, AGI 0.6x, VIT 1.3x, MAG 0.7x
- Use for: Boss encounters, endurance challenges, protecting weaker enemies

**3. FastFragile** (high speed/damage, low HP)
- Multipliers: HP 0.7x, STR 1.2x, AGI 1.5x, VIT 0.6x, MAG 0.9x
- Use for: Swarm encounters, speed challenges, glass cannon enemies

**4. MagicUser** (high magic, moderate HP)
- Multipliers: HP 0.9x, STR 0.7x, AGI 0.8x, VIT 0.8x, MAG 1.5x
- Use for: Elemental encounters, magic damage challenges

### Monster Stat Formulas (BalanceConfig.cs)

**HP Formula**: `(10 + level * 5) * archetype_hp_multiplier`
- Example: Level 50 Balanced = (10 + 50*5) * 1.0 = 260 HP
- Example: Level 50 Tank = (10 + 50*5) * 1.5 = 390 HP

**Stat Formula**: `(1 + level * 2/3) * archetype_stat_multiplier`
- Example: Level 50 Balanced STR = (1 + 50*2/3) * 1.0 = 34 STR
- Example: Level 50 FastFragile AGI = (1 + 50*2/3) * 1.5 = 51 AGI

**Experience Yield**: `10 + level * 8`
- Example: Level 50 = 10 + 50*8 = 410 XP

### Monster Creation Pattern
```csharp
using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

public sealed class YourMonster : IEnemy
{
    private int _hp;
    public string Name => "YourMonster";
    public int Level { get; }
    public StatBlock Stats { get; }
    public DamageKind AttackKind => DamageKind.Physical; // or DamageKind.Magic
    public ElementType Element => ElementType.Fire;
    public ElementalProperties ElementalProps { get; }
    public int MaxHP { get; }
    public int CurrentHP => _hp;
    public int ExperienceYield { get; }

    public YourMonster(int level = 25)
    {
        Level = level;
        var archetype = BalanceConfig.MonsterArchetype.Balanced;
        
        // Calculate stats using BalanceConfig
        Stats = new StatBlock(
            BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Strength),
            BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Agility),
            BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Vitality),
            BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Magic)
        );
        
        MaxHP = BalanceConfig.CalculateMonsterHP(Level, archetype);
        _hp = MaxHP;
        ExperienceYield = BalanceConfig.CalculateMonsterExperience(Level);
        
        // Set up elemental properties (see Elemental System section)
        var resistances = new Dictionary<ElementType, float>
        {
            { ElementType.Fire, 0.3f },   // 30% resistance to Fire
            { ElementType.Water, -0.3f }  // 30% weakness to Water
        };
        ElementalProps = new ElementalProperties(ElementType.Fire, resistances);
    }

    public bool TakeDamage(int amount)
    {
        if (amount <= 0) return false;
        _hp -= amount;
        if (_hp < 0) _hp = 0;
        return _hp == 0;
    }
}
```

### Monster Level Guidelines
- Pit 1-10: Levels 1-15 (beginner)
- Pit 11-20: Levels 16-35 (intermediate)
- Pit 21-40: Levels 36-70 (advanced)
- Pit 41+: Levels 71-99 (expert)
- Use `BalanceConfig.EstimatePlayerLevelForPitLevel(pitLevel)` for guidance

---

## Equipment Generation Guidelines

### Equipment Rarity System
Equipment rarity determines the power multiplier applied to base stat calculations:
- **Normal**: 1.0x multiplier (standard equipment)
- **Uncommon**: 1.5x multiplier (enhanced equipment)
- **Rare**: 2.0x multiplier (powerful equipment)
- **Epic**: 2.5x multiplier (very powerful equipment)
- **Legendary**: 3.5x multiplier (ultimate equipment)

### Equipment Stat Formulas (BalanceConfig.cs)

**Attack Bonus (Weapons)**: `(1 + pitLevel / 2) * rarity_multiplier`
- Example: Pit 50 Rare = (1 + 50/2) * 2.0 = 52 Attack
- Method: `BalanceConfig.CalculateEquipmentAttackBonus(pitLevel, rarity)`

**Defense Bonus (Armor/Shields/Helms)**: `(1 + pitLevel / 3) * rarity_multiplier`
- Example: Pit 50 Rare = (1 + 50/3) * 2.0 = 35 Defense
- Method: `BalanceConfig.CalculateEquipmentDefenseBonus(pitLevel, rarity)`

**Stat Bonus (Accessories)**: `(pitLevel / 5) * rarity_multiplier`
- Example: Pit 50 Rare = (50/5) * 2.0 = 20 to a stat
- Method: `BalanceConfig.CalculateEquipmentStatBonus(pitLevel, rarity)`

### Equipment Pit Level Tiers
- **Starter** (Pit 1-10): Basic equipment for beginners
- **Early** (Pit 11-25): First upgrades
- **Mid** (Pit 26-40): Mid-game equipment
- **Late** (Pit 41-70): Advanced equipment
- **Legendary** (Pit 71-100): End-game equipment

### Equipment Creation Pattern
```csharp
using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Equipment.Swords
{
    /// <summary>Factory for creating YourWeapon gear.</summary>
    public static class YourWeapon
    {
        private const int PitLevel = 25;
        private const ItemRarity Rarity = ItemRarity.Rare;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            
            // Optional: Set up elemental properties
            var resistances = new Dictionary<ElementType, float>
            {
                { ElementType.Fire, 0.3f },   // 30% resist own element
                { ElementType.Water, -0.15f } // 15% weak to opposing element
            };
            
            return new Gear(
                "YourWeapon",
                ItemKind.WeaponSword,
                Rarity,
                $"+{attackBonus} Attack",
                500, // Price in gold
                new StatBlock(0, 0, 0, 0), // Stat bonuses (for accessories)
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Fire, resistances)
            );
        }
    }
}
```

### HP/MP Bonus Scaling (Accessories)
- **HP Bonus**: `stat * 5` (for vitality-focused items)
- **MP Bonus**: `stat * 3` (for magic-focused items)

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
  - Example: Fire attack vs Water defender = 2.0x
- **0.5x damage**: Attack element matches defender's element (disadvantage)
  - Example: Fire attack vs Fire defender = 0.5x
- **1.0x damage**: Neutral attacks, Neutral defenders, or unrelated elements
  - Example: Fire attack vs Earth defender = 1.0x

### Custom Resistances (ElementalProperties.Resistances)
Custom resistance values modify base multipliers:
- **Positive values**: Resistance (damage reduction)
  - Example: `{ ElementType.Fire, 0.5f }` = 50% resistance to Fire
- **Negative values**: Weakness (damage increase)
  - Example: `{ ElementType.Water, -0.5f }` = 50% weakness to Water

### Standard Resistance Pattern for Monsters/Equipment
```csharp
var resistances = new Dictionary<ElementType, float>
{
    { ElementType.Fire, 0.25f },   // 25-30% resistance to own element
    { ElementType.Water, -0.15f }  // 10-15% weakness to opposing element
};
ElementalProps = new ElementalProperties(ElementType.Fire, resistances);
```

### Element Assignment Guidelines
**Monsters**:
- Balanced: Earth, Water, Neutral
- Tank: Earth (defensive), Fire (aggressive)
- FastFragile: Wind (speed), Dark (evasive)
- MagicUser: Fire (offensive), Water (support), Light (holy), Dark (shadow)

**Equipment**:
- Weapons: Usually pure element (no resistances)
- Armor/Shields/Helms: Can have resistances to own element
- Accessories: Typically pure element (no resistances)
- Neutral equipment: No resistances or weaknesses

### Complete Damage Calculation
Use `BalanceConfig.GetElementalDamageMultiplier(attackElement, defenderProps)` for complete calculation including custom resistances.

---

## Job Stat Progression

### Primary Jobs (6 total)
Jobs provide stat bonuses that grow with level using linear formulas:
- **Formula**: `BaseBonus + (GrowthPerLevel × (Level - 1))`
- **HP**: `25 + (Vitality × 5)`
- **MP**: `10 + (Magic × 3)`

**Example Jobs at Level 99**:
- **Knight**: 68 STR, 42 AGI, 78 VIT, 28 MAG, 415 HP, 94 MP (Tank role)
- **Monk**: 73 STR, 62 AGI, 58 VIT, 37 MAG, 315 HP, 121 MP (Balanced Fighter)
- **Thief**: 58 STR, 82 AGI, 43 VIT, 32 MAG, 240 HP, 106 MP (Speed/Evasion)
- **Bowman**: 62 STR, 72 AGI, 48 VIT, 37 MAG, 265 HP, 121 MP (Ranged)
- **Mage**: 33 STR, 48 AGI, 33 VIT, 88 MAG, 190 HP, 274 MP (Magic DPS)
- **Priest**: 38 STR, 53 AGI, 43 VIT, 78 MAG, 240 HP, 244 MP (Healer)

### Secondary Jobs (15 total)
Combinations of two primary jobs, 15-25% stronger than primaries:
- Examples: Paladin (Knight+Priest), Ninja (Knight+Thief), Wizard (Mage+Priest)
- Implementation: `PitHero/RolePlayingFramework/Jobs/Secondary/`

### Tertiary Jobs (22 total)
Elite combinations of secondary jobs, 25-40% stronger than primaries:
- Examples: Templar (Paladin+WarMage), ShinobiMaster (Samurai+Ninja)
- Many tertiary jobs reach stat cap (99) in their primary attributes
- Implementation: `PitHero/RolePlayingFramework/Jobs/Tertiary/`

### Job Implementation Files
- **Primary**: `PitHero/RolePlayingFramework/Jobs/Primary/*.cs`
- **Secondary**: `PitHero/RolePlayingFramework/Jobs/Secondary/*.cs`
- **Tertiary**: `PitHero/RolePlayingFramework/Jobs/Tertiary/*.cs`
- **Growth Calculator**: `PitHero/RolePlayingFramework/Stats/GrowthCurveCalculator.cs`

---

## Balance System Reference

### Quick Formula Reference
| Component | Formula | Example (Level/Pit 50) |
|-----------|---------|------------------------|
| Monster HP | `(10 + L*5) * archetype_mult` | Balanced: 260 HP |
| Monster Stat | `(1 + L*2/3) * archetype_mult` | Balanced: 34 stat |
| Monster XP | `10 + L*8` | 410 XP |
| Weapon Attack | `(1 + P/2) * rarity_mult` | Normal: 26 |
| Armor Defense | `(1 + P/3) * rarity_mult` | Normal: 17 |
| Accessory Stat | `(P/5) * rarity_mult` | Normal: 10 |

### Balance Documentation Files
- **Equipment Balance**: `EQUIPMENT_BALANCE_GUIDE.md` (comprehensive equipment guide)
- **Monster Balance**: `MONSTER_BALANCE_GUIDE.md` (comprehensive monster guide)
- **Job Stat Curves**: `JOB_STAT_CURVES.md` (all job progressions with tables)

### Testing Balance Changes
- Unit tests: `PitHero.Tests/BalanceSystemTests.cs`, `GearItemsTests.cs`, `*JobStatGrowthTests.cs`
- Always test at multiple pit/character levels: 1, 25, 50, 75, 99
- Validate formulas produce smooth progression curves without sudden jumps