# Hero Promotion System Implementation

## Overview
When a hero dies, a random unhired mercenary is automatically selected to become the next hero. The mercenary walks to the hero statue at tile (112, 6), faces it for 1 second, a lightning strike animation plays, and then the mercenary is converted into the new hero with a randomly generated crystal.

## Architecture

### 1. Core Components

#### **HeroPromotionService** (`PitHero/Services/HeroPromotionService.cs`)
The main service that orchestrates the hero promotion process.

**Key Methods:**
- `CheckAndPromoteIfNeeded()`: Periodically called from `MainGameScene.Update()` to check if a living hero exists
- `TryPromoteMercenary()`: Selects a random unhired mercenary for promotion
- `ExecutePromotionSequence()`: Executes the full promotion sequence (walk ? face statue ? lightning ? conversion)
- `PlayLightningStrike()`: Plays the "LightningStrike" animation from Actors.atlas
- `ConvertMercenaryToHero()`: Transforms the mercenary entity into a hero entity
- `GenerateRandomHeroCrystal()`: Creates a random hero crystal (temporary implementation)

**Promotion Sequence:**
1. Detect no living hero
2. Select random unhired mercenary
3. Mark mercenary as `IsBeingPromoted = true`
4. Add `MercenaryStateMachine` if not present
5. Wait for mercenary to arrive at statue
6. Face statue for 1 second
7. Play lightning strike animation
8. Convert mercenary to hero

#### **WalkToHeroStatueAction** (`PitHero/AI/WalkToHeroStatueAction.cs`)
GOAP action that makes a mercenary walk to the hero statue.

**Preconditions:**
- `IsAlive = true`
- `IsBeingPromotedToHero = true`

**Postconditions:**
- `HasArrivedAtHeroStatue = true`

**Implementation:**
- Uses pathfinding to navigate to tile (112, 6)
- Faces statue (Direction.Up) upon arrival
- Sets `mercenary.HasArrivedAtStatue = true`

### 2. GOAP Integration

#### **New GOAP Constants** (`PitHero/AI/GoapConstants.cs`)
```csharp
public const string IsAlive = "IsAlive";
public const string IsBeingPromotedToHero = "IsBeingPromotedToHero";
public const string HasArrivedAtHeroStatue = "HasArrivedAtHeroStatue";
public const string WalkToHeroStatueAction = "WalkToHeroStatueAction";
```

#### **MercenaryStateMachine Updates** (`PitHero/AI/MercenaryStateMachine.cs`)
- Added `WalkToHeroStatueAction` to planner
- Updated `GetCurrentState()` to include promotion flags
- Updated `GetGoalState()` to prioritize statue arrival when `IsBeingPromoted = true`
- Modified `Update()` to allow updates during promotion (even when not hired)

### 3. Component Updates

#### **MercenaryComponent** (`PitHero/ECS/Components/MercenaryComponent.cs`)
**New Properties:**
```csharp
public bool IsBeingPromoted { get; set; }
public bool HasArrivedAtStatue { get; set; }
```

#### **MainGameScene** (`PitHero/ECS/Scenes/MainGameScene.cs`)
**Initialization:**
- Creates and registers `HeroPromotionService` in `Begin()`

**Update Loop:**
- Calls `heroPromotionService.CheckAndPromoteIfNeeded()` every frame
- Skips mercenaries being promoted in hover/click detection

### 4. Hero Conversion Process

When converting a mercenary to hero, the following happens:

**Components Removed:**
- `MercenaryComponent`
- `MercenaryStateMachine`
- `MercenaryFollowComponent`

**Components Added:**
- `HeroComponent` (with HP, MaxHP, PitInitialized)
- `BouncyDigitComponent` (damage display)
- `BouncyTextComponent` (miss display)
- `ActionQueueVisualizationComponent` (action queue display)
- `Historian` (action history)
- `HeroStateMachine` (hero AI)

**Entity Updates:**
- Name changed to "hero"
- Tag changed to `GameConfig.TAG_HERO`
- Collider updated to hero physics layer
- New random `HeroCrystal` generated and linked

**UI Reconnection:**
After the mercenary is converted to a hero, all UI components must be reconnected to the new hero entity:
- `ShortcutBar` reconnected via `ConnectShortcutBarToHero()`
- `InventoryGrid` reconnected via `inventoryGrid.ConnectToHero(heroComponent)`
- HeroUI automatically finds the new hero via `FindEntity("hero")`

This is handled by calling `mainGameScene.ReconnectUIToHero()` after the conversion completes.

**Random Crystal Generation:**
For now, generates a random crystal with:
- Random primary job (Knight, Monk, Thief, Archer, Mage, or Priest)
- Level 1
- Random base stats (2-5 in each stat)

> **Future Enhancement:** Crystal Forge queue will be used instead of random generation.

## Hero Statue

**Location:** Tile (112, 3) for statue sprite, tile (112, 6) for mercenary destination
**Sprite:** "HeroStatue" from Actors.atlas
**Render Layer:** `GameConfig.RenderLayerActors`

## Lightning Strike Animation

**Animation:** "LightningStrike" from Actors.atlas
**Play Mode:** Once (LoopMode.Once)
**Render Layer:** `GameConfig.RenderLayerTop`
**Duration:** Until animation completes

## Periodic Hero Check

The system checks for a living hero every frame in `MainGameScene.Update()`:
1. If no hero entity exists ? promote mercenary
2. If hero entity exists but HP ? 0 ? promote mercenary
3. If promotion already in progress ? skip check
4. If no unhired mercenaries available ? log warning and retry later

## Edge Cases Handled

1. **No mercenaries available:** Service logs warning and waits for next check
2. **Mercenary being promoted:** Excluded from hiring, hover, and click detection
3. **Promotion in progress:** Prevents starting a second promotion
4. **Pathfinding failure:** Logs warning and marks as arrived to prevent softlock
5. **Missing components:** Safe fallbacks with error logging

## Future Enhancements

### Crystal Forge Integration
Replace `GenerateRandomHeroCrystal()` with:
```csharp
var heroForge = Core.Services.GetService<IHeroForge>();
var queuedCrystal = heroForge.InfuseNext("Promoted Hero");
heroComponent.LinkedHero = new Hero(
    mercComponent.LinkedMercenary.Name,
    queuedCrystal.Job,
    queuedCrystal.Level,
    queuedCrystal.BaseStats,
    queuedCrystal
);
```

### Customization Options
- Allow player to choose which mercenary to promote
- Add promotion ceremony animations
- Play special sound effects
- Camera zoom to statue during promotion
- Particle effects during lightning strike

## Testing Checklist

- [ ] Hero death triggers promotion
- [ ] Random mercenary selected correctly
- [ ] Mercenary walks to statue using pathfinding
- [ ] Mercenary faces statue for 1 second
- [ ] Lightning strike animation plays
- [ ] Mercenary successfully converts to hero
- [ ] New hero has all required components
- [ ] New hero has valid crystal and stats
- [ ] Promoted mercenary excluded from hiring
- [ ] System handles no available mercenaries gracefully
- [ ] System handles pathfinding failures gracefully
- [ ] Multiple promotion attempts prevented

## Debug Commands

To test the system:
1. Kill the current hero (reduce HP to 0)
2. Ensure at least one unhired mercenary exists in tavern
3. Observe mercenary walk to statue
4. Observe lightning strike and conversion

## Related Files

### New Files
- `PitHero/Services/HeroPromotionService.cs`
- `PitHero/AI/WalkToHeroStatueAction.cs`

### Modified Files
- `PitHero/ECS/Components/MercenaryComponent.cs`
- `PitHero/AI/GoapConstants.cs`
- `PitHero/AI/MercenaryStateMachine.cs`
- `PitHero/Services/MercenaryManager.cs`
- `PitHero/ECS/Scenes/MainGameScene.cs`

## Configuration Constants

```csharp
// Hero Statue Position
private const int StatueTileX = 112;
private const int StatueTileY = 6; // Mercenary destination

// Statue Sprite Position
var tileX = 112;
var tileY = 3; // Statue sprite location

// Face Statue Duration
yield return Coroutine.WaitForSeconds(1.0f);
```
