# Permadeath Feature - Implementation Summary

## Issue
**Title**: Add Permadeath  
**Request**: When hero's HP reaches 0, instead of refilling HP, the hero should permanently die with a death animation and their crystal should be moved to the CrystalMerchantVault for future sale by a Crystal Merchant.

## Solution Overview

This implementation adds a complete permadeath system with the following components:

### 1. Job Tier System
- **New Files**:
  - `PitHero/RolePlayingFramework/Jobs/JobTier.cs` - Enum defining Primary, Secondary, and Tertiary tiers
  
- **Modified Files**:
  - `IJob.cs` - Added `JobTier Tier` property
  - `BaseJob.cs` - Added tier parameter to constructor
  - All 6 Primary job classes - Set tier to `JobTier.Primary`
  - All 15 Secondary job classes - Set tier to `JobTier.Secondary`
  - All 29 Tertiary job classes - Set tier to `JobTier.Tertiary`
  - `CompositeJob.cs` - Tier is max of component job tiers

### 2. Crystal Merchant Vault Service
- **New Files**:
  - `PitHero/Services/CrystalMerchantVault.cs` - Service to store fallen hero crystals
  
- **Modified Files**:
  - `Game1.cs` - Registered vault as global service
  
- **Features**:
  - Store unlimited hero crystals
  - Add/Remove/Clear operations
  - Read-only collection access
  - Null-safe operations

### 3. Sell Value Calculation
- **Modified Files**:
  - `HeroCrystal.cs` - Added `CalculateSellValue()` method
  - `Hero.cs` - Added `BoundCrystal` property accessor
  
- **Formula**: `50 × level × tierMultiplier`
  - Primary tier multiplier: 1.0x
  - Secondary tier multiplier: 1.5x
  - Tertiary tier multiplier: 2.0x
  
- **Examples**:
  - Level 10 Knight: 500 gold
  - Level 10 Samurai: 750 gold
  - Level 10 Templar: 1000 gold

### 4. Hero Death Animation Component
- **New Files**:
  - `PitHero/ECS/Components/HeroDeathComponent.cs` - Handles death animation sequence
  
- **Animation Sequence** (2 seconds):
  1. Hero faces downward
  2. HeroShadow sprite appears at death location
  3. Hero rises 64 pixels upward while fading from opaque to transparent
  4. Shadow remains in place (creates floating illusion)
  5. All paperdoll layers fade simultaneously
  6. Crystal moves to vault
  7. Shadow and hero entity destroyed

- **Features**:
  - Pause-aware (respects PauseService)
  - Edge case handling (missing crystal, no vault service)
  - Logging for debugging

### 5. Battle System Integration
- **Modified Files**:
  - `AttackMonsterAction.cs` - Changed hero death to trigger permadeath animation
  
- **Changes**:
  - Removed: `hero.RestoreHP(hero.MaxHP)` on death
  - Added: `HeroDeathComponent` instantiation and animation trigger
  - Battle ends immediately when hero dies

## Testing

### New Tests Added (15 total, all passing)
1. **CrystalMerchantVaultTests.cs** (7 tests):
   - Add single crystal
   - Add multiple crystals
   - Remove crystal
   - Remove non-existent crystal
   - Clear all crystals
   - Add null crystal (should not increase count)
   - Verify read-only collection

2. **HeroCrystalSellValueTests.cs** (8 tests):
   - Primary job level 1 sell value
   - Primary job level 10 sell value
   - Secondary job level 10 sell value
   - Tertiary job level 10 sell value
   - High level (50) primary sell value
   - High level (50) secondary sell value
   - High level (50) tertiary sell value
   - Composite job tier calculation

### Test Results
- **New Tests**: 15/15 passing (100%)
- **Existing Tests**: 352/371 passing (same as before)
- **Pre-existing Failures**: 19 (unchanged)
- **New Failures**: 0
- **Regressions**: None

## Files Changed Summary

### New Files (5)
1. `PitHero/RolePlayingFramework/Jobs/JobTier.cs`
2. `PitHero/Services/CrystalMerchantVault.cs`
3. `PitHero/ECS/Components/HeroDeathComponent.cs`
4. `PitHero.Tests/CrystalMerchantVaultTests.cs`
5. `PitHero.Tests/HeroCrystalSellValueTests.cs`

### Modified Files (56)
- Core framework: 5 files (IJob, BaseJob, CompositeJob, Hero, HeroCrystal)
- Job classes: 50 files (6 Primary, 15 Secondary, 29 Tertiary)
- Game integration: 2 files (Game1, AttackMonsterAction)

## Documentation

- `PERMADEATH_IMPLEMENTATION.md` - Detailed technical documentation of the death animation system

## Code Quality

- All magic numbers extracted to named constants
- Comprehensive inline documentation
- Null-safe operations throughout
- Pause-aware animations
- Edge case handling
- Debug logging for troubleshooting

## Future Enhancements

The system is designed to support future features:
1. Crystal Merchant NPC to sell crystals back to players
2. Crystal merging/combining system
3. Adjustable sell value formulas (BaseValuePerLevel constant)
4. Customizable death animation parameters

## Verification

✅ Solution builds without errors  
✅ All new tests pass  
✅ No new test failures introduced  
✅ Code review feedback addressed  
✅ Magic numbers extracted to constants  
✅ Comprehensive documentation provided  

## Security

No security vulnerabilities introduced:
- No external dependencies added
- No user input processed
- No file I/O beyond existing patterns
- No network operations
- Memory management follows existing patterns
