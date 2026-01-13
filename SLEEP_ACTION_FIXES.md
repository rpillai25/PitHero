# SleepInBedAction Fixes

## Issues Fixed

### 1. Hero Attempting to Sleep Without Enough Gold
**Problem**: Hero would walk to the inn even when he only had 8 gold (requires 10 gold).

**Solution**: The gold check was already in the `Execute()` method, but the hero's GOAP planner would still select this action because there was no way to communicate the gold requirement as a precondition. The fix ensures that if the hero doesn't have enough gold, the action returns `true` (marking it as "complete") so the planner can try other actions. This prevents the hero from getting stuck trying to sleep when he can't afford it.

**Code Location**: `PitHero/AI/SleepInBedAction.cs`, lines 42-48

### 2. Mercenaries Not Walking to Beds (Teleporting Instead)
**Problem**: Mercenaries were being teleported directly to their bed positions instead of walking there, which looked unnatural.

**Solution**: Replaced the direct position assignment with pathfinding-based movement:
- Disable the mercenary's following component to prevent interference
- Calculate a path from current position to bed position using `PathfindingActorComponent`
- Walk the mercenary tile-by-tile along the path using `StartMoving(Direction)`
- Wait for each movement to complete before moving to next tile
- Fall back to teleportation only if pathfinding fails

**Code Location**: `PitHero/AI/SleepInBedAction.cs`, lines 314-358
- Mercenary #1 walks to bed at (76, 3)
- Mercenary #2 walks to bed at (73, 7)

### 3. Hero Never Waking Up (Stuck in Bed)
**Problem**: After sleeping, the hero would remain in the bed at position (73, 3) and never exit, preventing further gameplay.

**Solution**: Added a final step to the sleep coroutine that walks the hero out of the bed:
- After sleep completes and HP/MP is restored, hero walks to exit tile (71, 3)
- Exit tile is positioned between the payment tile (67, 3) and the bed (73, 3)
- Uses the same pathfinding pattern as the walk to bed
- Re-enables mercenary following AFTER hero exits the bed
- Marks sleep as completed so the hero can continue with other actions

**Code Location**: `PitHero/AI/SleepInBedAction.cs`, lines 420-500

## Sleep Action Flow (Complete Sequence)

1. **Precondition Check**: Hero must be `OutsidePit` and `HPCritical`
2. **Gold Check**: Hero must have at least 10 gold in `GameStateService.Funds`
3. **HeroStateMachine GoTo**: HeroStateMachine pathfinds hero to payment tile (67, 3)
4. **SleepInBedAction Begins**: Action starts with hero already at payment position
5. **Face Innkeeper**: Hero faces right towards innkeeper at (69, 3)
6. **Pay Innkeeper**: Deduct 10 gold from Funds (0.5s delay for animation)
7. **Walk to Bed**: Hero walks from payment tile to bed at (73, 3)
8. **Position Mercenaries**: 
   - Disable mercenary following
   - Walk mercenary #1 to bed at (76, 3)
   - Walk mercenary #2 to bed at (73, 7)
9. **Sleep**: Wait for 10 seconds
10. **Restore HP/MP**: Hero and all mercenaries fully healed
11. **Exit Bed**: Hero walks from bed (73, 3) to exit tile (71, 3)
12. **Re-enable Following**: Mercenaries resume following the hero
13. **Complete**: Action marked as complete, hero can continue other actions

## Bed Positions Reference

| Entity | Bed Tile Position | Notes |
|--------|------------------|-------|
| Hero | (73, 3) | Main bed in inn |
| Mercenary #1 | (76, 3) | First mercenary bed |
| Mercenary #2 | (73, 7) | Second mercenary bed |
| Payment Tile | (67, 3) | Where hero pays innkeeper (HeroStateMachine target) |
| Exit Tile | (71, 3) | Where hero exits after sleep |
| Innkeeper | (69, 3) | NPC position (always faces left) |

## Key Fix

The root cause was in `HeroStateMachine.CalculateBedLocation()` which was returning the bed position (73, 3) instead of the payment position (67, 3). This caused the GoTo state to pathfind the hero to the bed BEFORE the SleepInBedAction even started executing.

**Solution**: Changed `CalculateBedLocation()` to return `(GameConfig.InnPaymentTileX, GameConfig.InnPaymentTileY)` so the hero pathfinds to the payment tile first. The SleepInBedAction then handles the sequence: pay ? walk to bed ? sleep ? walk to exit.

## Testing

? All 709 tests pass (703 succeeded, 6 skipped)
? Build successful with no errors

## Key Implementation Details

### Pathfinding Pattern for Walking
All movement (hero to payment, hero to bed, hero to exit, mercenaries to beds) uses the same pattern:

```csharp
var path = pathfinding.CalculatePath(currentTile, targetTile);
for (int i = 0; i < path.Count; i++)
{
    var targetTile = path[i];
    var currentTilePos = /* calculate from entity position */;
    
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
            yield return null;
    }
    
    yield return Coroutine.WaitForSeconds(0.05f);
}
```

### Mercenary Following Management
Critical to disable following component BEFORE moving mercenaries to beds:

```csharp
// CRITICAL: Disable following component FIRST
if (mercFollowComp != null)
{
    mercFollowComp.Enabled = false;
}

// Stop any current movement
if (mercTileMover.IsMoving)
{
    mercTileMover.StopMoving();
}

// Then walk to bed...
```

Re-enable AFTER hero exits bed:

```csharp
// After hero exits bed
for (int i = 0; i < hiredMercenaries.Count; i++)
{
    var mercFollowComp = merc.GetComponent<MercenaryFollowComponent>();
    if (mercFollowComp != null)
    {
        mercFollowComp.Enabled = true;
        mercFollowComp.ResetPathfinding();
    }
}
```

## Notes

- The gold requirement (10 gold) cannot be added as a static GOAP precondition because preconditions are set at initialization time
- Instead, the gold check happens in `Execute()` and returns `true` (complete) if insufficient funds
- This allows the GOAP planner to try other actions when the hero can't afford the inn
- Pathfinding failures fall back to teleportation to prevent getting stuck
- All movement respects the tile-based grid system with proper snap-to-grid after teleportation
