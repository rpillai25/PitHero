# Mercenary Cleanup on Hero Death Implementation

## Overview
This document describes the implementation of the mercenary cleanup system that manages mercenary behavior during hero death and promotion transitions.

## Feature Requirements
1. **Block Hiring During Transition**: Mercenaries cannot be hired while the hero is dead (from death animation start until new hero is fully promoted)
2. **Remove Hired Mercenaries**: When a hero dies, all hired mercenaries fade out in place and are removed from the game
3. **Re-enable Hiring**: Hiring is automatically re-enabled after a new hero is successfully promoted

## Implementation Details

### 1. MercenaryManager Service (`PitHero/Services/MercenaryManager.cs`)

#### New Fields
```csharp
private bool _hiringBlocked; // Flag to prevent hiring during hero death/promotion
```

#### New Public Methods
- **`BlockHiring()`**: Blocks mercenary hiring (called when hero dies)
- **`UnblockHiring()`**: Unblocks mercenary hiring (called when new hero is promoted)
- **`RemoveAllHiredMercenaries()`**: Removes all currently hired mercenaries

#### Updated Methods
- **`CanHireMore()`**: Now checks `_hiringBlocked` flag before allowing hiring
  - Returns `false` if hiring is blocked
  - Returns `false` if already at max hired mercenaries (2)
  - Returns `true` otherwise

#### New Private Coroutines
- **`FadeOutAndRemoveMercenary(Entity mercEntity)`**: Fades out a hired mercenary in place
  - Marks mercenary as being removed
  - Disables AI state machine, follow component, and tile mover
  - Gets all renderer components (body, hair, shirt, pants, hands)
  - Fades alpha from 255 to 0 over 2 seconds (same as hero death fade)
  - Respects pause state during fade
  - Removes mercenary entity when fade completes

### 2. HeroDeathComponent (`PitHero/ECS/Components/HeroDeathComponent.cs`)

#### Changes to `ExecuteDeathAnimation()` Coroutine
After transferring items to vault and adding crystal to vault, now also:
```csharp
// Block mercenary hiring and remove all hired mercenaries
var mercenaryManager = Core.Services.GetService<MercenaryManager>();
if (mercenaryManager != null)
{
    mercenaryManager.BlockHiring();
    mercenaryManager.RemoveAllHiredMercenaries();
    Debug.Log("[HeroDeathComponent] Blocked hiring and started removal of hired mercenaries");
}
```

This happens **before** the hero entity is destroyed, ensuring the mercenary removal sequence starts immediately when the hero dies.

### 3. HeroPromotionService (`PitHero/Services/HeroPromotionService.cs`)

#### Changes to `ExecutePromotionSequence()` Coroutine
After promotion completes successfully (after setting flags and logging "PROMOTION COMPLETE"):
```csharp
// Unblock mercenary hiring now that new hero is ready
var mercenaryManager = Core.Services.GetService<MercenaryManager>();
if (mercenaryManager != null)
{
    mercenaryManager.UnblockHiring();
    Debug.Log("[HeroPromotionService] Unblocked mercenary hiring - new hero ready");
}
```

This re-enables hiring immediately after the new hero is fully functional.

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
3. **Hired Mercenaries Start Fading Out** (`MercenaryManager.RemoveAllHiredMercenaries()`)
   - For each hired mercenary:
     - AI state machine disabled
     - Follow component disabled
     - Tile mover disabled
     - Fade alpha from 255 to 0 over 2 seconds
     - Entity removed after fade completes
   - All mercenaries fade simultaneously
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

## Technical Details

### Fade Animation
The fade-out animation mirrors the hero's death fade:
- **Duration**: 2.0 seconds (constant `fadeOutDuration`)
- **Pause Awareness**: Respects pause state - fade pauses when game is paused
- **Alpha Calculation**: `alpha = 255 * (1 - progress)` where progress goes from 0 to 1
- **Renderers Affected**: All 6 renderer components (body, hair, shirt, pants, hand1, hand2)
- **Color Preservation**: Only alpha channel changes, RGB values remain unchanged

### Fade vs Walk Comparison
**Original Design** (walk to exit):
- Jump out of pit if inside
- Pathfind to exit tile (43, 11)
- Walk step-by-step
- Complex edge cases (pit detection, pathfinding failures)

**New Design** (fade in place):
- Disable movement immediately
- Fade alpha over 2 seconds
- Remove when invisible
- Simple, reliable, no edge cases

### Simultaneous Fading
All hired mercenaries start their fade coroutines simultaneously (not sequentially), allowing them to all fade at the same time for a consistent visual effect.

## Testing Recommendations

### Test Case 1: Hero Death with No Hired Mercenaries
- Kill hero
- Verify hiring is blocked (can't click mercenaries)
- Verify new hero is promoted from unhired mercenary
- Verify hiring is re-enabled after promotion

### Test Case 2: Hero Death with 1 Hired Mercenary
- Hire 1 mercenary (following hero)
- Kill hero
- Verify hired mercenary fades out in place over 2 seconds
- Verify hiring is blocked during transition
- Verify new hero is promoted
- Verify hiring is re-enabled

### Test Case 3: Hero Death with 2 Hired Mercenaries
- Hire 2 mercenaries (chain following)
- Kill hero
- Verify both hired mercenaries fade out simultaneously
- Verify new hero is promoted
- Verify hiring is re-enabled

### Test Case 4: Hero Death with Hired Mercenaries in Pit
- Hire mercenaries and jump into pit with them
- Kill hero while mercenaries are in pit
- Verify mercenaries fade out in place (no jump out needed)
- Verify new hero is promoted
- Verify hiring is re-enabled

### Test Case 5: Hero Death with Hired Mercenaries Outside Pit
- Hire mercenaries while hero is outside pit (e.g., near tavern)
- Kill hero before mercenaries jump into pit
- Verify mercenaries fade out in place wherever they are
- Verify new hero is promoted
- Verify hiring is re-enabled

### Test Case 6: Pause During Fade
- Hire mercenary
- Kill hero to start fade
- Pause game during fade animation
- Verify fade animation pauses
- Unpause game
- Verify fade animation resumes and completes

## Logging
All major steps are logged with the `[MercenaryManager]` prefix:
- "Hiring blocked - hero is dead"
- "Removing X hired mercenaries due to hero death"
- "Starting fade-out removal of hired mercenary [name]"
- "Fade-out complete for [name] - removing"
- "Hiring unblocked - new hero ready"

## Files Modified
1. `PitHero/Services/MercenaryManager.cs` - Added hiring block flag and removal logic
2. `PitHero/ECS/Components/HeroDeathComponent.cs` - Call BlockHiring and RemoveAllHiredMercenaries
3. `PitHero/Services/HeroPromotionService.cs` - Call UnblockHiring after promotion
4. `PitHero/ECS/Scenes/MainGameScene.cs` - Updated comment (no functional change)
