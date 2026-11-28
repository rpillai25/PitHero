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
// PitWidthManager.ExtensionTilesForLevel (shared by the real and virtual managers)
int expansionLevel = Math.Clamp(pitLevel, 0, 100);  // Cap expansion at level 100
int extensionTiles = (expansionLevel / 10) * 2;
```

`pitLevel` is the level within the current 100-level cycle (1 to `BiomeProgressionConfig.CycleLength`), never the cumulative depth, so every
new cycle (tier) starts back at the base 10-tile floor and the pit widens across all four biomes. Monster and loot scaling still use the effective depth.
`CalculateRightEdgeForLevel` predicts the edge `SetPitLevel` shrinks to and must match `RegeneratePitWidth`.

**Examples:**
- Level 1-9: 0 extension tiles (original pit)
- Level 10-19: 2 extension tiles (pit extends to x=14)
- Level 20-29: 4 extension tiles (pit extends to x=16)
- Level 30-39: 6 extension tiles (pit extends to x=18)
- Level 100-109: 20 extension tiles (pit extends to x=34)
- Level 200+: 20 extension tiles (expansion capped at level 100)

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
## Biomes (issue #473)

The pit cycles through four biomes, 25 levels each: **Cave** 1–25, **Forest** 26–50, **Castle** 51–75,
**Underworld** 76–100 (`BiomeProgressionConfig.GetBiome`). The tier increments only past level 100.

- **Tiles.** `Content/Tilemaps/PitHeroDungeons.tmx` holds one 14x11 Base-layer block per biome, side by
  side (Cave, Forest, Castle; it shares `PitHeroTiles.tsx`, firstgid 1). `PitWidthManager.SetPitLevel`
  stamps the level's block into the main map's Base layer at tile (0,1) whenever the biome changes (new
  game, load, orb advance, death reset) and re-records the expansion columns (x=11 inner floor, x=12
  inner wall, x=13 outer floor) and the shrink snapshot of x=12–13 from it, so expansion copies the
  biome's columns. Writes go through `TiledMapService.SetTile`, so replays record them as tile events.
  `PitBiomeTileSet` parses the blocks from the TMX XML (no texture load). Collision is not stamped: every
  block has the same pit shape. PitHero.tmx is authored with the Castle block, which a test pins.
- **Walls.** Pit obstacles use the biome's wall tile from the tileset (0-based ids: Cave 191, Forest 211,
  Castle 34; `PitBiomeTileSet.GetWallSprite`).
- **Underworld.** Reuses the Cave block and wall, graded purple: `PitBiomeTintComponent` multiplies
  `PitBiomeTileSet.UnderworldTint` over the pit tiles (render layer 95, between Base and Detail), and the
  walls carry the same color. The tint is live-only and follows the HUD record's pit level in replays.
- **Content.** Every biome reuses the Cave tables keyed on the **biome level** (1–25,
  `GetBiomeLevel`): monster pools, boss floors (5/10/15/20/25), chest rarity bands, monster density.
  Enemy levels use the effective depth; tier-scaled "+N" gear and chest gold use the **scaling tier**
  (one per biome of depth, `GetScalingTier`), so loot keeps the per-25-level curve it had when each
  25-level loop was its own tier. Chest/obstacle counts and pit width follow the cycle level.
- **Saves.** v48 stores the cycle level; v47 and older (level, tier) pairs convert by effective depth
  (`ConvertLegacyPitPosition`: legacy level 1 tier 2 → level 26 tier 1).
