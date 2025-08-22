# Dynamic Pit Width Generation - Usage Guide

## Overview
The dynamic pit width generation system allows the pit to expand every 10 levels by 2 tiles to the right. The system stores tile patterns from the map and applies them dynamically when the pit level changes.

## Key Classes

### PitWidthManager
Main service class that handles pit expansion logic.

**Key Methods:**
- `SetPitLevel(int newLevel)` - Set the pit level and trigger expansion
- `GetCurrentPitCandidateTargets()` - Get movement targets for current pit width
- `CalculateCurrentPitWorldBounds()` - Get collision bounds for current pit size

**Properties:**
- `CurrentPitLevel` - Current pit level (starts at 1)
- `CurrentPitRightEdge` - Current rightmost X coordinate of the pit

### PitLevelTestComponent
Testing component for manual pit level changes via keyboard input.

**Usage:**
- Press `0` to reset to Level 1
- Press `1-9` to set pit level to 10, 20, 30, etc.

## Integration Points

### MoveToPitAction
Automatically uses dynamic candidate targets from PitWidthManager service.
Falls back to static targets if service unavailable.

### MainGameScene
- Initializes PitWidthManager as a service
- Handles dynamic pit collision bound updates
- Includes PitLevelTestComponent for testing

## Expansion Formula
```csharp
int extensionTiles = ((int)(PitLevel / 10)) * 2;
```

**Examples:**
- Level 1-9: 0 extension tiles (original pit)
- Level 10-19: 2 extension tiles (pit extends to x=14)
- Level 20-29: 4 extension tiles (pit extends to x=16)
- Level 30-39: 6 extension tiles (pit extends to x=18)

## Tile Patterns
The system stores 6 tile pattern dictionaries initialized from specific map coordinates:

| Pattern | Layer | Source Coordinates | Usage |
|---------|-------|-------------------|-------|
| baseOuterFloor | Base | (13, 1-11) | Outer edge tiles |
| collisionOuterFloor | Collision | (13, 1-11) | Outer edge collision |
| baseInnerWall | Base | (12, 1-11) | Inner wall tiles |
| collisionInnerWall | Collision | (12, 1-11) | Inner wall collision |
| baseInnerFloor | Base | (11, 1-11) | Inner floor tiles |
| collisionInnerFloor | Collision | (11, 1-11) | Inner floor collision |

Plus FogOfWar index from coordinate (2,3).

## Service Access
```csharp
// Get the service
var pitWidthManager = Core.Services.GetService<PitWidthManager>();

// Set pit level
pitWidthManager.SetPitLevel(25); // Level 25 = 4 extension tiles

// Get current targets for AI pathfinding
var targets = pitWidthManager.GetCurrentPitCandidateTargets();

// Get current collision bounds
var bounds = pitWidthManager.CalculateCurrentPitWorldBounds();
```

## Testing
Run all tests with: `dotnet test PitHero.Tests/`

The test suite includes:
- Extension formula validation
- Bounds calculation verification
- Service integration tests
- Edge case handling

Total: 32 tests (22 original + 10 new PitWidthManager tests)