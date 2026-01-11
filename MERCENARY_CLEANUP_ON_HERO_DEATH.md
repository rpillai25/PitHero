# Mercenary Freeze/Unfreeze on Hero Death Implementation

## Overview
This document describes the implementation of the mercenary freeze/unfreeze system that manages mercenary behavior during hero death and promotion transitions.

## Feature Requirements
1. **Block Hiring During Transition**: Mercenaries cannot be hired while the hero is dead (from death animation start until new hero is fully promoted)
2. **Freeze Hired Mercenaries**: When a hero dies, all hired mercenaries freeze in place:
   - State machines disabled
   - Follow components disabled
   - Tile movers disabled
   - Mercenaries remain visible but motionless
3. **Unfreeze and Reassign**: After a new hero is promoted, mercenaries are unfrozen and reassigned:
   - First mercenary follows new hero
   - Second mercenary follows first mercenary (chain maintained)
   - All components re-enabled
4. **Re-enable Hiring**: Hiring is automatically re-enabled after new hero is promoted

## Implementation Details

### 1. MercenaryManager Service (`PitHero/Services/MercenaryManager.cs`)

#### New Fields
```csharp
private bool _hiringBlocked; // Flag to prevent hiring during hero death/promotion
```

#### New Public Methods
- **`BlockHiring()`**: Blocks mercenary hiring (called when hero dies)
- **`UnblockHiring()`**: Unblocks mercenary hiring (called when new hero is promoted)
- **`FreezeAllHiredMercenaries()`**: Freezes all currently hired mercenaries in place
- **`UnfreezeAndReassignMercenaries(Entity newHeroEntity)`**: Unfreezes mercenaries and reassigns follow targets

#### Updated Methods
- **`CanHireMore()`**: Now checks `_hiringBlocked` flag before allowing hiring
  - Returns `false` if hiring is blocked
  - Returns `false` if already at max hired mercenaries (2)
  - Returns `true` otherwise

#### New Methods Details

**`FreezeAllHiredMercenaries()`**:
- Iterates through all hired mercenaries
- Disables state machine (stops AI planning)
- Disables follow component (stops following behavior)
- Disables tile mover (stops movement)
- Clears follow target (hero is dead)
- Mercenaries remain visible but frozen in place

**`UnfreezeAndReassignMercenaries(Entity newHeroEntity)`**:
- Iterates through all hired mercenaries
- Reassigns follow targets based on position:
  - First mercenary ? follows new hero
  - Second mercenary ? follows first mercenary
- Re-enables state machine (resumes AI)
- Re-enables follow component (resumes following)
- Re-enables tile mover (resumes movement)
- Mercenaries resume normal behavior with new hero

### 2. HeroDeathComponent (`PitHero/ECS/Components/HeroDeathComponent.cs`)

#### Changes to `ExecuteDeathAnimation()` Coroutine
After transferring items to vault and adding crystal to vault, now also:
```csharp
// Block mercenary hiring and freeze all hired mercenaries in place
var mercenaryManager = Core.Services.GetService<MercenaryManager>();
if (mercenaryManager != null)
{
    mercenaryManager.BlockHiring();
    mercenaryManager.FreezeAllHiredMercenaries();
    Debug.Log("[HeroDeathComponent] Blocked hiring and froze hired mercenaries in place");
}
```

This happens **before** the hero entity is destroyed, ensuring mercenaries freeze immediately when the hero dies.

### 3. HeroPromotionService (`PitHero/Services/HeroPromotionService.cs`)

#### Changes to `ExecutePromotionSequence()` Coroutine
After promotion completes successfully (after setting flags and logging "PROMOTION COMPLETE"):
```csharp
// Unblock mercenary hiring and unfreeze/reassign hired mercenaries to new hero
var mercenaryManager = Core.Services.GetService<MercenaryManager>();
if (mercenaryManager != null)
{
    mercenaryManager.UnblockHiring();
    mercenaryManager.UnfreezeAndReassignMercenaries(mercenary);
    Debug.Log("[HeroPromotionService] Unblocked mercenary hiring and reassigned frozen mercenaries to new hero");
}
```

This re-enables hiring and unfreezes mercenaries, reassigning them to follow the new hero (maintaining the follow chain).

### 4. MainGameScene (`PitHero/ECS/Scenes/MainGameScene.cs`)

#### Updated `HandleMercenaryClicks()` Method
Updated comment to clarify that `CanHireMore()` now includes hiring block check:
```csharp
// Don't show dialog if player can't hire more mercenaries (includes hiring block check)
if (!mercenaryManager.CanHireMore())
    return;
```

No functional change needed - the hiring block is automatically enforced by the updated `CanHireMore()` method.

## Game Flow

### Normal Operation
1. Player can hire up to 2 mercenaries
2. Hired mercenaries follow the hero/each other
3. Mercenaries can be hired/fired normally

### When Hero Dies
1. **Hero Death Animation Starts** (`HeroDeathComponent.StartDeathAnimation()`)
2. **Hiring Blocked** (`MercenaryManager.BlockHiring()`)
   - Player cannot click mercenaries to hire
   - Hire dialog will not appear
3. **Hired Mercenaries Freeze in Place** (`MercenaryManager.FreezeAllHiredMercenaries()`)
   - For each hired mercenary:
     - AI state machine disabled
     - Follow component disabled
     - Tile mover disabled
     - Follow target cleared
     - Mercenary remains visible but frozen
   - Mercenaries stay wherever they were when hero died
4. **Hero Entity Destroyed**
5. **Hero Promotion Check** (every frame in MainGameScene.Update)
   - Detects no living hero
   - Selects random unhired mercenary
   - Walks to statue, lightning strike, conversion

### When New Hero Promoted
1. **Promotion Completes** (`HeroPromotionService.ExecutePromotionSequence()`)
2. **Hiring Unblocked** (`MercenaryManager.UnblockHiring()`)
   - Player can now click mercenaries again
   - New mercenaries can be hired
3. **Hired Mercenaries Unfrozen and Reassigned** (`MercenaryManager.UnfreezeAndReassignMercenaries()`)
   - For each hired mercenary:
     - Follow target reassigned:
       - First mercenary ? new hero
       - Second mercenary ? first mercenary
     - AI state machine re-enabled
     - Follow component re-enabled
     - Tile mover re-enabled
   - Mercenaries resume following new hero from their frozen positions

## Technical Details

### Freeze Mechanism
**Component Disabling**:
- **State Machine**: Disabling prevents AI from planning new actions
- **Follow Component**: Disabling stops following behavior updates
- **Tile Mover**: Disabling stops all movement
- **Follow Target**: Set to `null` since hero is dead

**Visual Appearance**:
- Mercenaries remain fully visible (no fade)
- Current animation frame frozen
- Position unchanged from moment of hero death

### Unfreeze and Reassignment
**Follow Chain Preservation**:
- First hired mercenary always follows the new hero
- Second hired mercenary always follows the first mercenary
- Chain structure maintained across hero deaths

**Component Re-enabling**:
- State machine resumes AI planning
- Follow component resumes following updates
- Tile mover resumes movement capability
- Mercenaries pathfind from frozen position to their follow target

### Design Benefits
1. **Continuity**: Hired mercenaries persist across hero deaths
2. **Visual Clarity**: Player sees frozen mercenaries waiting for new hero
3. **No Data Loss**: Mercenary stats, equipment, and state preserved
4. **Simple Logic**: No complex removal/respawn sequences
5. **Consistent Behavior**: Follow chain always maintained correctly

## Testing Recommendations

### Test Case 1: Hero Death with No Hired Mercenaries
- Kill hero
- Verify hiring is blocked (can't click mercenaries)
- Verify new hero is promoted from unhired mercenary
- Verify hiring is re-enabled after promotion

### Test Case 2: Hero Death with 1 Hired Mercenary
- Hire 1 mercenary (following hero)
- Kill hero
- Verify hired mercenary freezes in place (stops moving)
- Verify hiring is blocked during transition
- Verify new hero is promoted
- Verify mercenary unfreezes and follows new hero
- Verify hiring is re-enabled

### Test Case 3: Hero Death with 2 Hired Mercenaries
- Hire 2 mercenaries (chain following)
- Kill hero
- Verify both hired mercenaries freeze in place
- Verify new hero is promoted
- Verify first mercenary follows new hero
- Verify second mercenary follows first mercenary (chain maintained)
- Verify hiring is re-enabled

### Test Case 4: Hero Death with Hired Mercenaries in Pit
- Hire mercenaries and jump into pit with them
- Kill hero while mercenaries are in pit
- Verify mercenaries freeze in pit
- Verify new hero is promoted
- Verify mercenaries unfreeze and resume following from pit
- Verify hiring is re-enabled

### Test Case 5: Hero Death with Hired Mercenaries Outside Pit
- Hire mercenaries while hero is outside pit (e.g., near tavern)
- Kill hero before mercenaries jump into pit
- Verify mercenaries freeze wherever they are
- Verify new hero is promoted
- Verify mercenaries unfreeze and pathfind to new hero
- Verify hiring is re-enabled

### Test Case 6: Multiple Hero Deaths with Same Mercenaries
- Hire mercenaries
- Kill hero ? verify freeze
- Promote new hero ? verify unfreeze and reassignment
- Kill new hero again ? verify freeze again
- Promote another hero ? verify mercenaries still work correctly
- Verify mercenaries persist through multiple hero deaths

## Logging
All major steps are logged with the `[MercenaryManager]` prefix:
- "Hiring blocked - hero is dead"
- "Freezing X hired mercenaries due to hero death"
- "Freezing hired mercenary [name] in place"
- "All hired mercenaries frozen"
- "Unfreezing X hired mercenaries and reassigning to new hero"
- "First mercenary [name] will follow new hero"
- "Second mercenary [name] will follow [first merc name]"
- "Unfroze and reassigned [name] to follow [target]"
- "All hired mercenaries unfrozen and reassigned"
- "Hiring unblocked - new hero ready"

## Files Modified
1. `PitHero/Services/MercenaryManager.cs` - Added freeze/unfreeze methods
2. `PitHero/ECS/Components/HeroDeathComponent.cs` - Call FreezeAllHiredMercenaries
3. `PitHero/Services/HeroPromotionService.cs` - Call UnfreezeAndReassignMercenaries after promotion
4. `PitHero/ECS/Scenes/MainGameScene.cs` - Updated comment (no functional change)
