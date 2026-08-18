# Item Synergy System Documentation

## Overview

The Item Synergy System is a flexible pattern-matching framework that detects spatial arrangements of items in the hero's inventory grid and applies powerful effects. It replaces the traditional secondary/tertiary job progression system with a more dynamic, equipment-driven progression model.

## Core Concepts

### 1. Synergy Patterns

A **SynergyPattern** defines:
- A unique spatial arrangement of items (offsets from an anchor point)
- Required item types for each position
- Effects granted when the pattern is matched
- Synergy points required to unlock bonus skills

### 2. Active Synergies

When a pattern is detected in the inventory, an **ActiveSynergy** is created that:
- Tracks the pattern instance and its grid positions
- Accumulates synergy points earned through combat
- Unlocks special skills when point thresholds are met

**Stacking:** there is no limit on how many distinct patterns can be active at once, and no
hard cap on copies of the same pattern. Non-overlapping copies of one pattern stack additively
with diminishing returns (`SynergyEffectAggregator`): the first three copies are the
normal-return region (1.0 / 0.5 / 0.25 → 1.75x total), and every copy past that keeps halving
(0.125, 0.0625, ...), so the total multiplier asymptotes just under **2.0x** no matter how many
copies are stacked. Copies that share an item with an already-counted copy are rejected
(overlap rule). Synergy-point acceleration is capped separately at +70% (`MaxAccelerationCap`).

### 3. Synergy Effects

Effects are modular and can be combined:
- **StatBonusEffect**: Direct stat bonuses (flat or percentage)
- **SkillModifierEffect**: Modify skill properties (MP cost, range, power)
- **PassiveAbilityEffect**: Grant passive abilities (counter, deflect, regen)
- **GrowthModifierEffect**: Modify stat growth at level-up

## Architecture

```
RolePlayingFramework/Synergies/
├── ISynergyEffect.cs           # Base interface for all effects
├── SynergyPattern.cs           # Pattern definition
├── ActiveSynergy.cs            # Active pattern instance
├── SynergyDetector.cs          # Pattern detection engine
├── SynergyPatternRegistry.cs   # Single shared registry of all 63 patterns (canonical slot order)
├── StatBonusEffect.cs          # Stat bonus implementation
├── SkillModifierEffect.cs      # Skill modifier implementation
├── PassiveAbilityEffect.cs     # Passive ability implementation
├── GrowthModifierEffect.cs     # Growth modifier implementation
└── ExampleSynergyPatterns.cs   # Example pattern definitions
```

## Integration Points

### Hero Class
```csharp
public sealed class Hero
{
    // Active synergies detected in current inventory
    private readonly List<ActiveSynergy> _activeSynergies;
    public IReadOnlyList<ActiveSynergy> ActiveSynergies => _activeSynergies;
    
    // Update synergies when inventory changes
    public void UpdateActiveSynergies(List<ActiveSynergy> detectedSynergies);
    
    // Earn synergy points from battles
    public void EarnSynergyPoints(int amount);
}
```

### HeroCrystal Class
```csharp
public sealed class HeroCrystal
{
    // Persistent synergy progression
    private readonly Dictionary<string, int> _synergyPoints;
    private readonly HashSet<string> _learnedSynergySkillIds;
    private readonly HashSet<string> _discoveredSynergyIds;
    
    // Track synergy points per pattern
    public void EarnSynergyPoints(string synergyId, int amount);
    public int GetSynergyPoints(string synergyId);
    
    // Discover and learn synergy skills
    public void DiscoverSynergy(string synergyId);
    public void LearnSynergySkill(string skillId);
}
```

## Usage Examples

### Creating a Simple Synergy Pattern

```csharp
// Sword & Shield pattern
var offsets = new List<Point> 
{ 
    new Point(0, 0),  // Sword (anchor)
    new Point(1, 0)   // Shield (adjacent)
};

var requiredKinds = new List<ItemKind> 
{ 
    ItemKind.WeaponSword, 
    ItemKind.Shield 
};

var effects = new List<ISynergyEffect>
{
    new PassiveAbilityEffect(
        "sword_shield_defense",
        "+5 Defense, +10% Deflect",
        defenseBonus: 5,
        deflectChanceIncrease: 0.1f
    )
};

var pattern = new SynergyPattern(
    "sword_shield_mastery",
    "Sword & Shield Mastery",
    "Classic defensive stance",
    offsets,
    requiredKinds,
    effects,
    synergyPointsRequired: 100
);
```

### Detecting Synergies

```csharp
// Set up detector with patterns
var detector = new SynergyDetector();
detector.RegisterPattern(pattern);

// Create inventory grid (8x7 in PitHero)
var grid = new IItem[8, 7];
grid[0, 3] = GearItems.ShortSword();
grid[1, 3] = GearItems.WoodenShield();

// Detect active synergies
var synergies = detector.DetectSynergies(grid, 8, 7);

// Apply to hero
hero.UpdateActiveSynergies(synergies);
```

### Earning Synergy Points

```csharp
// After defeating an enemy
hero.EarnSynergyPoints(10);

// This distributes points to all active synergies
// and automatically unlocks synergy skills when thresholds are met
```

## Pattern Design Guidelines

### 1. Pattern Size
- Keep patterns compact (2-4 items typically)
- Larger patterns = more powerful effects but harder to achieve

### 2. Item Requirements
- Use job-specific equipment for thematic synergies
- Mix equipment types for interesting combinations
- Consider equipment slot limitations

### 3. Effect Balance
- Flat bonuses: +5 to +10 for minor effects
- Percentage bonuses: +10% to +20% for balanced effects
- Passive abilities: Save for advanced patterns
- Synergy points: 100-200 for basic, 300+ for advanced

### 4. Spatial Patterns

**Linear (Easy)**
```
[Sword][Shield]
```

**L-Shape (Medium)**
```
[Mail]
[Helm][Shield]
```

**Triangle (Medium)**
```
[Rod][Accessory]
[Accessory]
```

**Cross (Hard)**
```
    [Item]
[Item][Item][Item]
    [Item]
```

## Example Patterns

### Sword & Shield Mastery
- **Items**: Sword + Shield (horizontal)
- **Effect**: +5 Defense, +10% Deflect
- **Points**: 100

### Mage's Focus
- **Items**: Rod + 2 Accessories (triangle)
- **Effect**: +5 Magic, -20% MP Cost
- **Points**: 150

### Monk's Balance
- **Items**: Knuckle + Gi + Headband (horizontal line)
- **Effect**: +3 STR, +3 AGI, Counter enabled
- **Points**: 200

### Heavy Armor Set
- **Items**: Mail + Helm + Shield (L-shape)
- **Effect**: +10 Defense, +50 HP
- **Points**: 150

### Priest's Devotion
- **Items**: Staff + Robe + Priest Hat (vertical line)
- **Effect**: +20% Heal Power, +2 MP Regen
- **Points**: 175

## Testing

The system includes comprehensive unit tests covering:

- Pattern creation and validation
- Synergy detection in various grid layouts
- Effect application and removal
- Synergy point accumulation
- Skill unlocking
- Crystal persistence

Run tests with:
```bash
dotnet test --filter "FullyQualifiedName~SynergySystemTests"
```

## Future Enhancements

### Planned Features
1. **Visual Indicators**: Highlight active synergies in UI
2. **Synergy Stencils**: Discoverable templates showing pattern shapes
3. **Rotation Variants**: Auto-detect rotated versions of patterns
4. **Pattern Conflicts**: Prioritize certain patterns when overlapping
5. **Synergy Tiers**: Common, Rare, Epic, Legendary patterns
6. **Combo Synergies**: Patterns that require multiple synergies active

### Integration Opportunities
1. **Battle System**: Grant bonus synergy points based on performance
2. **Equipment System**: Tag items as "synergy boosters"
3. **UI System**: Real-time synergy detection preview
4. **Save System**: Persist discovered synergies and earned points
5. **Achievement System**: Unlock achievements for discovering all synergies

## API Reference

### Core Classes

#### SynergyPattern
```csharp
public sealed class SynergyPattern
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public IReadOnlyList<Point> GridOffsets { get; }
    public IReadOnlyList<ItemKind> RequiredKinds { get; }
    public IReadOnlyList<ISynergyEffect> Effects { get; }
    public int SynergyPointsRequired { get; }
    public ISkill? UnlockedSkill { get; }
}
```

#### ActiveSynergy
```csharp
public sealed class ActiveSynergy
{
    public SynergyPattern Pattern { get; }
    public Point AnchorSlot { get; }
    public IReadOnlyList<Point> AffectedSlots { get; }
    public int PointsEarned { get; private set; }
    public bool IsSkillUnlocked { get; }
    
    public void EarnPoints(int amount);
}
```

#### SynergyDetector
```csharp
public sealed class SynergyDetector
{
    public void RegisterPattern(SynergyPattern pattern);
    public List<ActiveSynergy> DetectSynergies(IItem?[,] gridItems, int gridWidth, int gridHeight);
}
```

### Effect Interfaces

#### ISynergyEffect
```csharp
public interface ISynergyEffect
{
    string EffectId { get; }
    string Description { get; }
    void Apply(Hero hero);
    void Remove(Hero hero);
}
```

#### StatBonusEffect
```csharp
public sealed class StatBonusEffect : ISynergyEffect
{
    public StatBlock StatBonus { get; }
    public bool IsPercentage { get; }
    public int HPBonus { get; }
    public int MPBonus { get; }
}
```

#### SkillModifierEffect
```csharp
public sealed class SkillModifierEffect : ISynergyEffect
{
    public string? TargetSkillId { get; }
    public int MPCostReduction { get; }
    public float MPCostReductionPercent { get; }
    public int RangeIncrease { get; }
    public float PowerMultiplier { get; }
}
```

#### PassiveAbilityEffect
```csharp
public sealed class PassiveAbilityEffect : ISynergyEffect
{
    public int DefenseBonus { get; }
    public float DeflectChanceIncrease { get; }
    public bool EnableCounter { get; }
    public int MPTickRegen { get; }
    public float HealPowerBonus { get; }
    public float FireDamageBonus { get; }
}
```

## Performance Considerations

### Detection Optimization
- Detection runs O(P × W × H) where P=patterns, W=width, H=height
- With 10 patterns and 8×7 grid: 560 checks per detection
- Cache detection results until inventory changes
- Consider spatial indexing for large pattern collections

### Memory Management
- Pre-allocate effect lists in pattern definitions
- Reuse Point structs where possible
- Avoid allocating new lists during detection
- Use structs for frequently created objects

### AOT Compatibility
- All loops use `for` instead of `foreach`
- No LINQ in hot paths
- Pre-allocated collections with known capacity
- Avoid dynamic string manipulation in game loop

## Troubleshooting

### Pattern Not Detected
1. Check exact item kind matches (e.g., WeaponSword vs WeaponKnuckle)
2. Verify pattern fits within grid bounds
3. Ensure all required items are present
4. Check for null slots interrupting pattern

### Effects Not Applying
1. Verify `UpdateActiveSynergies()` is called after inventory changes
2. Check effect implementation in `Apply()` method
3. Ensure hero stats are recalculated after applying effects
4. Confirm crystal is bound to hero for persistence

### Synergy Points Not Accumulating
1. Check crystal is bound to hero
2. Verify `EarnSynergyPoints()` is called after battles
3. Confirm synergy ID matches pattern ID
4. Check that active synergy exists

## SynergyPatternRegistry

`SynergyPatternRegistry` (in `RolePlayingFramework/Synergies/SynergyPatternRegistry.cs`) is the
**single source of truth** for all patterns. It is initialized once at startup via a static
constructor (AOT-safe, no reflection) and exposes:

- `All` — `IReadOnlyList<SynergyPattern>` (63 patterns) in canonical stencil library panel slot order.
- `GetById(string id)` — O(1) dictionary lookup; returns null for unknown / null ids.

Always use `SynergyPatternRegistry.All` and `GetById` rather than constructing patterns directly.
The canonical order matches the HeroUI stencil-library panel so slot indices are stable.

## Placed-Stencil Persistence

When the player places a stencil onto the inventory grid, the position is mirrored as a non-UI
snapshot in `GameStateService.PlacedStencils` (`List<PlacedStencilRecord>`, each record holding
`PatternId`, `AnchorX`, `AnchorY`).

**API on `GameStateService`:**
- `SetPlacedStencil(patternId, anchorX, anchorY)` — upserts (same patternId replaces the old record).
- `RemovePlacedStencil(patternId)` — no-op when absent.
- `ClearPlacedStencils()` — removes all records.

This list is persisted in **section 44** of the save file (appended after section 43). A save with
no placed stencils recovers an empty list; the grid re-mirrors placements on its next
`ConnectToHero` call after loading.

`SaveLoadService` copies `GameStateService.PlacedStencils ↔ SaveData.PlacedStencils`
(`List<SavedPlacedStencil>`) on save and load respectively.

## Auto-Slotting (StencilBagSlotPreferenceProvider)

`StencilBagSlotPreferenceProvider` implements `IBagSlotPreferenceProvider` and steers incoming
bag items toward the first empty stencil cell whose `RequiredKind` matches the item's `ItemKind`.

**Rules (in priority order):**
1. **Consumable stacking always wins.** If the bag already has a non-full stack of the same
   consumable, the item absorbs into it — the provider is never consulted.
2. **First matching empty cell wins.** The provider iterates `PlacedStencils` in list order
   (placement order). Within each stencil, it iterates the pattern's offsets in definition order.
   The first cell whose kind matches and whose bag slot is empty is returned.
3. **Out-of-bounds cells are skipped.** Only cells in grid rows 3–8 (the 120 bag slots,
   `bagIndex = (gridY-3)*20 + gridX`) and columns 0–19 are valid.
4. **Occupied cells are skipped.** If the preferred slot is already taken the provider returns -1
   for that candidate and the next valid candidate is tried.
5. **Fallback to first-empty.** A return value of -1 from the provider causes `ItemBag.TryAdd`
   to fall back to the traditional first-empty scan.

The provider is set on `ItemBag.SlotPreferenceProvider`. A test constructor
`StencilBagSlotPreferenceProvider(GameStateService)` is available for headless unit tests.

## Chest Stencil Drops

Chests at pit level 2+ have an **8% chance** (`BalanceConfig.StencilChestDropRate = 0.08f`) to
yield an undiscovered stencil instead of normal loot. This branch:
- Collects all `SynergyPatternRegistry.All` entries where `HasStencil && !IsStencilDiscovered`.
- If any candidates exist, rolls the 8% chance, picks one at random, and sets
  `TreasureComponent.ContainedStencilPatternId` to its pattern ID.
- The chest becomes **purple** (level 4, `GameConfig.TREASURE_SHADE_4` color-matches-contents rule).
- After pickup via `OpenChestAction`, the pattern is discovered with source `StencilDiscoverySource.LootReward`.
- **Headless / test path:** the stencil branch requires `Core.Instance != null` to reach
  `GameStateService`; in the test harness (Core absent) the branch is fully inert.
- **VirtualPitGenerator gap:** stencil drops are not simulated in the virtual layer (same gap as seed chests).

## Version History

### v1.0.0
- Initial implementation of core synergy system
- Pattern detection engine
- Four effect types implemented
- Integration with Hero and HeroCrystal
- Comprehensive unit tests
- Example patterns demonstrating system

### v1.1.0 (issue #362)
- `SynergyPatternRegistry` — single source of truth (63 patterns, AOT-safe static init)
- Placed-stencil snapshot: `GameStateService.PlacedStencils` + persistence in save v27 section 44
- Auto-slotting: `IBagSlotPreferenceProvider` seam on `ItemBag`, `StencilBagSlotPreferenceProvider`
- Chest stencil drops: 8% on level-2+ chests, purple chest, `LootReward` source
