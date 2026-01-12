# Gold Yield System Implementation

## Overview
Implemented a GoldYield system where monsters drop gold when defeated. The gold is stored in a global `Funds` property in `GameStateService` that persists across all heroes (not tied to individual heroes).

## Changes Made

### 1. Balance System (`BalanceConfig.cs`)
Added `CalculateMonsterGoldYield(int level)` method:
- **Formula**: `5 + level * 3`
- **Example values**:
  - Level 1: 8 gold
  - Level 5: 20 gold
  - Level 10: 35 gold
  - Level 25: 80 gold
  - Level 50: 155 gold
  - Level 99: 302 gold
- Includes level clamping (1-99) like other monster yield formulas

### 2. Enemy Interface (`IEnemy.cs`)
Added new property:
```csharp
int GoldYield { get; }
```

### 3. All Enemy Implementations
Updated all 11 enemy classes to include GoldYield:
- `Slime.cs`
- `Bat.cs`
- `Rat.cs`
- `Goblin.cs`
- `Skeleton.cs`
- `Snake.cs`
- `Spider.cs`
- `Orc.cs`
- `Wraith.cs`
- `PitLord.cs`

Each enemy now:
1. Has a `public int GoldYield { get; }` property
2. Initializes it in constructor: `GoldYield = BalanceConfig.CalculateMonsterGoldYield(Level);`

### 4. Game State Service (`GameStateService.cs`)
Added new property:
```csharp
public int Funds { get; set; }
```
- Starts at 0
- Persists independently of heroes
- Can be increased/decreased as needed

### 5. Battle Resolution (`AttackMonsterAction.cs`)
Updated two sections where monsters are defeated:

#### When Hero defeats monster:
```csharp
if (enemyDied)
{
    hero.AddExperience(targetEnemy.ExperienceYield);
    hero.EarnJP(targetEnemy.JPYield);
    hero.EarnSynergyPointsWithAcceleration(targetEnemy.SPYield);
    
    // Add gold to global Funds
    var gameState = Core.Services.GetService<GameStateService>();
    if (gameState != null)
    {
        gameState.Funds += targetEnemy.GoldYield;
        Debug.Log($"Earned {targetEnemy.ExperienceYield} XP, {targetEnemy.JPYield} JP, {targetEnemy.SPYield} SP, {targetEnemy.GoldYield} Gold (Total: {gameState.Funds})");
    }
}
```

#### When Mercenary defeats monster:
```csharp
if (enemyDied)
{
    // Add gold to global Funds (mercenaries don't gain XP/JP/SP but gold is still awarded)
    var gameState = Core.Services.GetService<GameStateService>();
    if (gameState != null)
    {
        gameState.Funds += targetEnemy.GoldYield;
        Debug.Log($"Earned {targetEnemy.GoldYield} Gold (Total: {gameState.Funds})");
    }
}
```

### 6. Test Updates
Created comprehensive test coverage:

#### `GoldYieldSystemTests.cs` (10 tests)
- Tests for `CalculateMonsterGoldYield` at various levels (1, 5, 10, 25, 50, 99)
- Tests for level clamping (below 1, above 99)
- Tests that all monsters have GoldYield > 0
- Tests that all monsters' GoldYield matches expected formula

#### `GameStateFundsTests.cs` (5 tests)
- Tests Funds starts at 0
- Tests Funds can be increased/decreased
- Tests Funds persists across multiple operations
- Tests Funds supports large values

#### Updated `BalanceSystemTests.cs`
- Updated `TestMonster` class to include `GoldYield` property
- Initializes with `BalanceConfig.CalculateMonsterGoldYield(level)`

## Design Decisions

### 1. Global Funds vs Hero-Specific
**Decision**: Funds stored in `GameStateService` (global)
**Reason**: Per requirements, "Funds must not be linked to a particular hero...it's linked to the whole game session and persists between heroes"

### 2. Gold Awarded for Both Hero and Mercenary Kills
**Decision**: Gold is added to Funds regardless of who defeats the monster
**Reason**: Gold is a shared resource, so it makes sense that the party benefits from all defeats

### 3. Formula Balance
**Decision**: `5 + level * 3`
**Reason**: 
- Provides meaningful gold amounts at all levels
- Scales well with equipment costs (which also scale with pit level)
- Higher level monsters provide substantially more gold (3x per level)
- Conservative base ensures even low-level monsters provide some gold

### 4. Service-Based Access
**Decision**: Access Funds via `Core.Services.GetService<GameStateService>()`
**Reason**: Follows existing pattern in codebase for global services

## Integration Points

### Current State
? Monsters have GoldYield property
? GoldYield is calculated based on monster level
? Gold is added to Funds when monsters are defeated
? Funds persist in GameStateService
? All tests pass
? Build successful

### Future Integration Needed
- **UI Display**: Show current Funds in HUD/UI
- **Shops/Merchants**: Use Funds for purchasing items
- **Save/Load**: Persist Funds when saving/loading game state
- **Reset Logic**: Decide if/when Funds should reset (new game session?)

## Testing

All tests pass:
- 10 GoldYield formula tests
- 5 GameStateService Funds tests
- All existing balance system tests still pass
- Build successful with no errors

## Formula Examples

| Monster Level | Gold Yield | Notes |
|---------------|------------|-------|
| 1 | 8 | Early game starter enemies |
| 3 | 14 | Goblins, Spiders, Snakes |
| 6 | 23 | Skeletons, Orcs, Wraiths |
| 10 | 35 | Pit Lords (bosses) |
| 25 | 80 | Mid-game |
| 50 | 155 | Late game |
| 75 | 230 | Advanced |
| 99 | 302 | Max level |

## Backward Compatibility

All changes are additive:
- New property added to interface (all implementers updated)
- New method in BalanceConfig (doesn't affect existing code)
- New property in GameStateService (optional to use)
- Battle resolution enhanced but doesn't break existing flow
