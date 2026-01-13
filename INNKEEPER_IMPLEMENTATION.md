# Innkeeper System Implementation

## Overview
The innkeeper is an NPC entity that stands at a fixed position in the inn and facilitates paid sleeping services for the hero. When the hero needs to sleep at the inn, they must first pay the innkeeper 10 gold before accessing the bed.

## Implementation Details

### Configuration Constants (GameConfig.cs)
```csharp
// Inn configuration
public const int InnkeeperTileX = 69; // Innkeeper stands at (69, 3)
public const int InnkeeperTileY = 3;
public const int InnPaymentTileX = 67; // Hero pays at (67, 3) facing right
public const int InnPaymentTileY = 3;
public const int InnCostGold = 10; // Cost to sleep at inn

// Tags
public const int TAG_INNKEEPER = 10; // Tag for innkeeper entity
```

### Innkeeper Entity (MainGameScene.cs)

#### Spawning
- **Method**: `SpawnInnkeeper()`
- **Position**: Tile (69, 3)
- **Facing**: Always faces left
- **Appearance**:
  - Fair skin tone (Color(251, 200, 178))
  - Brown shirt (Color(140, 91, 62)) for apron-like appearance
  - Gray hair (Color(100, 100, 100)) for older innkeeper look
  - Uses the same paperdoll animation system as heroes and mercenaries

#### Components
The innkeeper uses the standard paperdoll system with the following layers (bottom to top):
1. `HeroBodyAnimationComponent` (fair skin)
2. `HeroHand2AnimationComponent` (matches body color)
3. `HeroPantsAnimationComponent` (white)
4. `HeroShirtAnimationComponent` (brown)
5. `HeroHairAnimationComponent` (gray)
6. `HeroHand1AnimationComponent` (matches body color)
7. `ActorFacingComponent` (set to Direction.Left)

### Sleep Action Flow (SleepInBedAction.cs)

#### Preconditions
1. Hero must be `OutsidePit` (outside the pit)
2. Hero must have `HPCritical` (HP below 40% threshold)
3. Hero must have at least 10 gold in funds

#### Payment Flow
The `SleepInBedAction` now implements a multi-step payment flow:

1. **Check Funds**: Before starting the coroutine, verify `GameStateService.Funds >= GameConfig.InnCostGold`
   - If insufficient funds, return true to mark action as "complete" so hero can try other actions
   
2. **HeroStateMachine GoTo**: The HeroStateMachine's GoTo state pathfinds the hero to the payment tile (67, 3)
   - `CalculateBedLocation()` returns the payment position, not the actual bed position
   - This ensures hero walks to innkeeper first, not directly to bed
   
3. **Verify Position**: SleepInBedAction verifies hero is at payment tile
   - If not (shouldn't normally happen), pathfind there as fallback
   
4. **Face Innkeeper**: Hero sets facing to `Direction.Right` to face the innkeeper at (69, 3)

5. **Pay Innkeeper**: Deduct 10 gold from `GameStateService.Funds`
   - Payment happens immediately with 0.5s delay for animation
   - If payment fails (not enough funds), abort the sleep action
   
6. **Walk to Bed**: Hero uses pathfinding to navigate to bed at tile (73, 3)
   - Uses standard pathfinding pattern
   
7. **Sleep**: Hero sleeps for 10 seconds, restoring full HP and MP
   - Mercenaries walk to their respective beds
   - All hired mercenaries also restore to full HP/MP

#### Movement Pattern
The implementation uses the proven path-following pattern from `MercenaryManager.WalkToTavern()`:

```csharp
var path = pathfinding.CalculatePath(currentTile, targetTile);
for (int i = 0; i < path.Count; i++)
{
    var targetTile = path[i];
    var currentTilePos = new Point(
        (int)(heroEntity.Transform.Position.X / GameConfig.TileSize),
        (int)(heroEntity.Transform.Position.Y / GameConfig.TileSize)
    );

    // Calculate direction
    var dx = targetTile.X - currentTilePos.X;
    var dy = targetTile.Y - currentTilePos.Y;

    Direction? direction = null;
    if (dx > 0) direction = Direction.Right;
    else if (dx < 0) direction = Direction.Left;
    else if (dy > 0) direction = Direction.Down;
    else if (dy < 0) direction = Direction.Up;

    if (direction.HasValue)
    {
        tileMover.StartMoving(direction.Value);
        while (tileMover.IsMoving)
        {
            yield return null;
        }
    }
    
    yield return Coroutine.WaitForSeconds(0.05f);
}
```

### Integration Points

#### GameStateService
- **Funds Property**: Tracks total gold available to the player
- Checked before sleep action starts
- Deducted when hero pays innkeeper

#### MainGameScene
- `SpawnInnkeeper()` is called in `Begin()` after `SpawnHeroStatue()`
- Innkeeper is spawned at a fixed position and never moves

#### GOAP System
- No new GOAP constants were added
- Uses existing `SleepInBedAction` with enhanced payment logic
- Hero will only attempt inn sleep if they have enough gold

## Testing

### Build Status
? All code compiles successfully
? All 709 tests pass (703 succeeded, 6 skipped)

### Test Updates
- Updated `MoveToPitActionTests.GoapConstants_SimplifiedModel_ShouldOnlyContainCoreConstants()` to reflect current constant count (29 constants)

## Future Enhancements
- Add visual payment animation (coins exchanging hands)
- Add innkeeper dialogue system
- Dynamic pricing based on hero level or room quality
- Innkeeper could offer other services (item storage, quest hub)

## References
- **Payment Flow**: Based on `MercenaryManager.WalkToTavern()` pathfinding pattern
- **Paperdoll System**: Uses same components as hero and mercenary entities
- **Gold System**: Integrates with existing `GameStateService.Funds` property from gold yield implementation
