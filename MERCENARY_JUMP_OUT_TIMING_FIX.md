# Mercenary Jump Out Timing and Alignment Fixes

## Problem 1: Mercenaries Stuck in Pit (Timing Issue)
Mercenaries were getting stuck in the pit when the hero executed `JumpOutOfPitForInnAction`. The mercenaries would remain in the pit while the hero walked to the inn bed, unable to follow.

### Root Cause Analysis (Timing)

The issue was a **timing problem** in the hero's `InsidePit` flag update:

1. **Hero starts jumping out of pit** at position (3,4)
2. **Mercenaries are adjacent** at positions (4,5) and (5,4)
3. **Mercenaries' `FollowTargetAction` completes** while hero is still mid-jump
4. **Mercenaries transition to Idle** and call GOAP planner to replan
5. **Hero's `InsidePit` flag is still `true`** at this point (only updated at END of jump)
6. **Mercenaries see no reason to jump out** - target appears to still be inside pit
7. **Hero's `InsidePit` flag updates to `false`** after jump completes
8. **Too late** - mercenaries already settled into follow state, stuck in pit

### Solution 1: Early Flag Update

**Update the hero's `InsidePit` flag at the START of the jump-out movement, not at the END.**

**File: `PitHero\AI\JumpOutOfPitForInnAction.cs`**

Set `InsidePit = false` when jump starts:
```csharp
_plannedTargetTile = targetTile.Value;

// CRITICAL: Update InsidePit flag BEFORE starting movement
// This ensures mercenaries immediately see the hero is outside and can trigger their own jump-out actions
hero.InsidePit = false;

StartJumpOutMovement(hero, _plannedTargetTile);
```

---

## Problem 2: Mercenaries Landing at Wrong Tile (Alignment Issue)
After fixing the timing issue, mercenaries would start jumping out but land at the **wrong tile** (e.g., (13,5) instead of (13,6) or (14,5) instead of expected position).

### Root Cause Analysis (Alignment)

Looking at the logs:
```
Log: [TileByTileMover] Movement to tile (14,6) blocked by collision with tilemap
Log: [TileByTileMover] Overriding tilemap collision at (14,6) due to empty Collision tile
Log: [TileByTileMover] Started moving Right from 436.2401,206.05519 to 468.2401,206.05519
Warn: [MercenaryJumpOutOfPit] Jump finished flag set but mercenary at 13,5 not at planned target 13,6
```

The mercenary's Y position was **206.05519** instead of the expected **208** (center of Y=6 tile). This position drift caused:

1. **Mercenary walks to jump position** - may not be perfectly aligned to grid
2. **Starts jump coroutine** using `Vector2.Lerp` from current position to target
3. **Small position drift accumulates** during interpolation
4. **At jump end, `SnapToTileGrid()` called** - uses `Math.Floor` with collider offset
5. **Due to drift, snaps to wrong tile** (13,5 instead of 13,6)
6. **Position verification fails** - mercenary stuck in infinite retry loop

### Solution 2: Grid Alignment Before Jump

**Snap to grid BEFORE starting the jump movement** to ensure perfect alignment.

**Files Modified:**
- `PitHero\AI\JumpOutOfPitForInnAction.cs`
- `PitHero\AI\JumpIntoPitAction.cs`
- `PitHero\AI\MercenaryJumpOutOfPitAction.cs`
- `PitHero\AI\MercenaryJumpIntoPitAction.cs`

All jump actions now snap to grid before calculating target position:
```csharp
private void StartJumpMovement(HeroComponent hero, Point targetTile)
{
    var entity = hero.Entity;
    
    // CRITICAL: Snap to grid BEFORE calculating target position
    // This ensures we start from a perfectly aligned position
    var tileMover = entity.GetComponent<TileByTileMover>();
    if (tileMover != null)
    {
        tileMover.SnapToTileGrid();
    }
    
    var targetPosition = TileToWorldPosition(targetTile);
    // ... rest of method
}
```

---

## Why These Fixes Work Together

### Fix 1: Timing (State Flags)
1. **Hero starts jumping** ? `InsidePit` immediately set to `false`
2. **Mercenaries detect change** ? `ShouldReplan()` returns `true` because target pit state changed
3. **Mercenaries replan** ? GOAP sees `TargetInsidePit = false` and plans jump-out action
4. **Mercenaries execute `MercenaryJumpOutOfPitAction`** ? they follow hero out of pit

### Fix 2: Alignment (Position Accuracy)
1. **Before jump starts** ? `SnapToTileGrid()` ensures perfect starting position
2. **Lerp interpolation** ? starts from aligned position, target is tile center
3. **Small drift is minimized** ? both endpoints are well-defined
4. **Final snap** ? lands on correct tile with high probability
5. **Position verification succeeds** ? action completes successfully

---

## Testing Recommendations

1. **Hire 2 mercenaries** and have them follow the hero
2. **Enter the pit** with hero at low HP
3. **Trigger `JumpOutOfPitForInnAction`** (hero should jump out to visit inn)
4. **Verify mercenaries follow** - they should jump out shortly after hero starts jumping
5. **Check final positions** - all actors should be outside pit when hero reaches inn bed
6. **Verify tile alignment** - mercenaries should land at correct tiles without position drift warnings

---

## Related Files
- `PitHero\AI\JumpOutOfPitForInnAction.cs` (modified - timing + alignment)
- `PitHero\AI\JumpIntoPitAction.cs` (modified - alignment only)
- `PitHero\AI\MercenaryJumpOutOfPitAction.cs` (modified - alignment only)
- `PitHero\AI\MercenaryJumpIntoPitAction.cs` (modified - alignment only)
- `PitHero\AI\MercenaryStateMachine.cs` (unchanged - replan logic already correct)
- `PitHero\ECS\Components\MercenaryFollowComponent.cs` (unchanged - follow logic already correct)
- `PitHero\ECS\Components\TileByTileMover.cs` (unchanged - snap logic already correct)

---

## Design Patterns Applied

### Pattern 1: State Flag Timing
**"Update state flags when the intention is committed, not when the action completes"**

For jump-out actions:
- ? Set `InsidePit = false` when jump STARTS (intention committed)
- ? Don't wait until jump ENDS (too late for other actors to react)

This ensures dependent actors (mercenaries) can react immediately to state changes rather than lagging behind by several frames.

### Pattern 2: Position Alignment
**"Snap to grid before coroutine-based movement to prevent drift accumulation"**

For all jump actions:
- ? Call `SnapToTileGrid()` BEFORE calculating target position
- ? Use aligned starting position for interpolation
- ? Don't start lerp from potentially misaligned position

This ensures pixel-perfect movement and prevents small floating-point errors from accumulating during interpolation.

---

## Notes

- The hero's position physically updates over time (coroutine movement)
- The `InsidePit` flag represents logical state, not physical position
- Other systems use trigger zones to validate physical position
- This separation allows for smooth movement while maintaining correct logical state for AI planning
- Position alignment fixes apply to ALL jump actions (in and out of pit, hero and mercenary)
