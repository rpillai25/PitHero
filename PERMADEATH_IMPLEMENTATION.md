# Hero Death Animation - Implementation Details

## Overview
When a hero's HP reaches 0, the hero now permanently dies instead of having HP refilled. The death triggers a cinematic animation sequence and the hero's crystal is moved to the CrystalMerchantVault.

## Animation Sequence

### Phase 1: Death Detection (AttackMonsterAction.cs, line ~421)
When `hero.TakeDamage()` returns true (HP reached 0):
1. A `HeroDeathComponent` is added to the hero entity (if not already present)
2. The `StartDeathAnimation()` method is called
3. The battle immediately ends

### Phase 2: Death Animation (HeroDeathComponent.cs)

#### Step 1: Initial Setup
- Hero faces downward (`Direction.Down`)
- A shadow sprite (HeroShadow from Actors.atlas) is created at the hero's death location
- Shadow properties:
  - Position: Same as hero's position when death occurs
  - Sprite region: (198, 499, 27, 5) from Actors.atlas
  - Color: Semi-transparent black (RGBA: 0, 0, 0, 128)
  - Render layer: Lowest layer (appears below hero)

#### Step 2: Rise and Fade (2 seconds duration)
The hero entity animates upward while fading out:
- **Position**: Lerps from death position to 64 pixels above
- **Alpha**: Fades from 255 (opaque) to 0 (transparent)
- **Affected components**: All hero paperdoll layers fade simultaneously:
  - HeroBodyAnimationComponent
  - HeroHairAnimationComponent
  - HeroShirtAnimationComponent
  - HeroPantsAnimationComponent
  - HeroHand1AnimationComponent
  - HeroHand2AnimationComponent

The shadow **remains in place** during this animation, creating the illusion of the hero floating higher and higher into the air.

#### Step 3: Cleanup and Crystal Storage
When animation completes:
1. Shadow entity is destroyed
2. Hero crystal is moved to CrystalMerchantVault service
3. Crystal sell value is logged to console
4. Hero entity is destroyed

## Crystal Sell Value Calculation

The sell value of a fallen hero's crystal is calculated using:
```
sellValue = baseValuePerLevel * level * tierMultiplier
```

Where:
- `baseValuePerLevel = 50`
- `level` = the hero's level when they died
- `tierMultiplier` depends on job tier:
  - Primary jobs (Knight, Mage, Monk, Priest, Thief, Bowman): **1.0x**
  - Secondary jobs (Samurai, Paladin, Ninja, etc.): **1.5x**
  - Tertiary jobs (Templar, ShadowPaladin, DivineSamurai, etc.): **2.0x**

### Examples:
- Level 10 Knight (Primary): 50 × 10 × 1.0 = **500 gold**
- Level 10 Samurai (Secondary): 50 × 10 × 1.5 = **750 gold**
- Level 10 Templar (Tertiary): 50 × 10 × 2.0 = **1000 gold**
- Level 50 Tertiary: 50 × 50 × 2.0 = **5000 gold**

## CrystalMerchantVault Service

The vault service stores all fallen hero crystals:
- Registered as a global service in `Game1.Initialize()`
- Provides methods to add, remove, clear, and query crystals
- Crystals are stored with all their properties:
  - Hero name
  - Job and tier
  - Level
  - Base stats
  - Learned skills
  - JP earned
  - Calculated sell value

## Future Integration
The vault is designed to be accessed by a future Crystal Merchant NPC who can:
- Display available crystals to the player
- Sell crystals back to the player for gold
- Allow players to recover or merge fallen hero crystals

## Technical Notes

### Pause Handling
The death animation respects the PauseService:
- Animation pauses when game is paused
- Elapsed time only advances when game is unpaused
- Ensures smooth animation regardless of pause state

### Component Dependencies
The death animation requires:
- `HeroComponent` with valid `LinkedHero`
- `LinkedHero` must have a `BoundCrystal`
- `ActorFacingComponent` (optional, for facing direction)
- Hero paperdoll animation components (for fade effect)

### Edge Cases Handled
- Hero with no bound crystal: Logs warning, no vault storage
- Vault service not found: Logs warning
- Missing paperdoll components: Skipped gracefully (only affects visual)
- Multiple death triggers: Animation only starts once
