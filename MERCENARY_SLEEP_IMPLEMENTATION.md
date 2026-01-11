# Mercenary Sleep Implementation

## Overview
When the hero sleeps in bed at position (73,3), hired mercenaries are positioned in designated beds, have their HP/MP restored to full alongside the hero, and then resume normal following behavior when the hero wakes up.

## Implementation Details

### Mercenary Bed Positions
- **Mercenary 1**: Bed at tile position (76, 3)
- **Mercenary 2**: Bed at tile position (73, 7)
- **Hero**: Bed at tile position (73, 3)

### Sleep Behavior Flow

#### 1. Hero Initiates Sleep (SleepInBedAction.Execute)
- Hero starts the sleep action
- `_isSleeping` flag is set to `true`
- Sleep coroutine is started

#### 2. Mercenaries Positioned in Beds (SleepCoroutine Start)
- Get list of hired mercenaries from `MercenaryManager`
- For each mercenary (up to 2):
  - Store original position for potential future use
  - **Disable** `MercenaryFollowComponent` (prevents pathfinding during sleep)
  - **Forcefully stop** any in-progress movement using `TileByTileMover.StopMoving()`
  - Teleport mercenary to their designated bed position
  - Snap to tile grid for precise positioning
  - Update `LastTilePosition` to prevent pathfinding confusion

#### 3. Sleep Duration (10 seconds)
- Hero and mercenaries remain in bed positions
- `MercenaryFollowComponent.Update()` returns early when disabled
- Time elapses using `Time.DeltaTime` (respects pause state)

#### 4. HP/MP Restoration (After 10 seconds)
All restorations happen simultaneously:

**Hero Restoration**:
- HP restored to full using `Hero.RestoreHP()`
- MP restored to full using `Hero.RestoreMP(-1)` (negative = full restore)

**Mercenary Restoration**:
- For each hired mercenary:
  - Calculate HP deficit: `MaxHP - CurrentHP`
  - Calculate MP deficit: `MaxMP - CurrentMP`
  - HP restored using `Mercenary.Heal(hpToRestore)`
  - MP restored using `Mercenary.RestoreMP(mpToRestore)`
  - Debug logs show restoration amounts

#### 5. Wake Up and Resume Following (0.5 seconds after restoration)
- Brief delay to ensure restoration is complete
- For each mercenary:
  - **Re-enable** `MercenaryFollowComponent`
  - Call `ResetPathfinding()` to clear old path data
  - Mercenary will recalculate path to hero on next update

#### 6. Sleep Complete
- `_sleepCompleted` flag set to `true`
- `_isSleeping` flag set to `false`
- Sleep coroutine reference cleared
- Action completes, hero can proceed with next action

## Key Code Changes

### SleepInBedAction.cs
- Added `_isSleeping` flag for reliable state tracking
- Added `_mercenaryOriginalPositions` list (for future use)
- Modified `SleepCoroutine()` to:
  - Position mercenaries in beds before sleeping
  - Disable mercenary following during sleep
  - **Forcefully stop in-progress movement** using `TileByTileMover.StopMoving()`
  - Update mercenary `LastTilePosition` to prevent pathfinding confusion
  - Restore mercenary HP/MP alongside hero
  - Re-enable following and reset pathfinding after waking

### MercenaryFollowComponent.cs
- Added early return if component is `Enabled == false`
- Prevents pathfinding attempts while mercenaries sleep
- `ResetPathfinding()` method clears stale path data after teleportation

## Behavior Summary

**Before Sleep**:
- Hero at bed (73,3)
- Mercenary 1 following hero
- Mercenary 2 following mercenary 1

**During Sleep** (10 seconds):
- Hero at (73,3) - sleeping
- Mercenary 1 at (76,3) - sleeping (following disabled)
- Mercenary 2 at (73,7) - sleeping (following disabled)

**After Sleep**:
- Hero wakes up and moves toward pit
- Mercenary 1 following component re-enabled, pathfinds to hero
- Mercenary 2 following component re-enabled, pathfinds to mercenary 1
- All HP/MP at maximum

## Testing Considerations

1. **Single Mercenary**: Only mercenary 1 should be positioned in bed (76,3)
2. **Two Mercenaries**: Both should be positioned in their respective beds
3. **HP/MP Restoration**: Should occur simultaneously for all characters
4. **Resume Following**: Mercenaries should immediately start following when hero leaves bed
5. **Pause During Sleep**: Sleep timer should respect pause state (uses `Time.DeltaTime`)
6. **No Hired Mercenaries**: Hero sleeps normally, no mercenary positioning needed

## Future Enhancements

- Store and restore original mercenary positions (currently stored but not used)
- Add sleep animations for mercenaries
- Visual indicator when mercenaries are sleeping
- Sound effects for sleep/wake actions

## Troubleshooting

### Issue: Mercenaries teleport to beds but immediately move back
**Cause**: The `TileByTileMover` component has in-flight movement that completes after teleportation.

**Solution**: The implementation now calls `TileByTileMover.StopMoving()` before teleporting mercenaries. This method:
1. Clears the `IsMoving` flag
2. Clears the `CurrentDirection`
3. Snaps to the current tile grid
4. Prevents any queued movement from completing

**Key Code**:
```csharp
if (mercTileMover.IsMoving)
{
    mercTileMover.StopMoving(); // Forcefully stop in-flight movement
}
// Now safe to teleport
merc.Transform.Position = bedWorldPos;
mercTileMover.SnapToTileGrid();
```

### Issue: MercenaryFollowComponent interferes with bed positioning
**Cause**: Following component continues to pathfind during teleportation.

**Solution**: The following component is disabled **before** any position changes:
```csharp
if (mercFollowComp != null)
{
    mercFollowComp.Enabled = false; // Disable FIRST
}
// Then stop movement and teleport
```

### Issue: Mercenary LastTilePosition out of sync after teleport
**Cause**: Teleportation doesn't update the tracked position used for following.

**Solution**: Update `LastTilePosition` after teleporting:
```csharp
var mercComp = merc.GetComponent<MercenaryComponent>();
if (mercComp != null)
{
    mercComp.LastTilePosition = bedPos; // Update tracked position
}
```
