# Second Chance Merchant Implementation

## Overview
The Second Chance Merchant is a system that stores all items (equipped + inventory) from deceased heroes. Items are automatically stacked up to 999 per item type, providing a way for players to eventually purchase back items lost to permadeath.

## Implementation Components

### 1. SecondChanceMerchantVault Service
**Location**: `PitHero/Services/SecondChanceMerchantVault.cs`

This service manages the storage and stacking of items from fallen heroes.

#### Key Features:
- **Stacking System**: Items are stacked by name, kind, and rarity
- **Max Stack Size**: 999 items per stack (vs 16 for regular inventory)
- **Automatic Overflow**: When a stack exceeds 999, new stacks are automatically created
- **Consumable Handling**: Properly handles consumable stack counts
- **Gear Stacking**: Even unique gear items can stack if they're identical

#### Public API:
```csharp
// Add a single item (handles stacking automatically)
void AddItem(IItem item)

// Add multiple items at once
void AddItems(IEnumerable<IItem> items)

// Remove a quantity from a stack (for future purchase UI)
bool RemoveQuantity(StackedItem stack, int quantity)

// Properties
IReadOnlyList<StackedItem> Stacks { get; }  // All stacked items
int StackCount { get; }                      // Number of unique item stacks
int TotalItemCount { get; }                  // Total quantity of all items
```

#### StackedItem Class:
```csharp
public sealed class StackedItem
{
    public IItem ItemTemplate { get; }  // The item as a template
    public int Quantity { get; set; }   // Number of this item in the stack
}
```

### 2. Service Registration
**Location**: `PitHero/Game1.cs`

The service is registered globally during game initialization:
```csharp
Services.AddService(new SecondChanceMerchantVault());
```

### 3. Hero Death Integration
**Location**: `PitHero/ECS/Components/HeroDeathComponent.cs`

When a hero dies, all items are transferred to the Second Chance vault:

#### Items Transferred:
1. **Equipped Items** (6 slots):
   - WeaponShield1 (weapon)
   - Armor
   - Hat
   - WeaponShield2 (shield)
   - Accessory1
   - Accessory2

2. **Inventory Items**:
   - All items from the hero's bag (including consumables with their stack counts)

#### Process:
1. Collect all equipped items from hero
2. Collect all inventory items from hero's bag
3. Transfer all items to SecondChanceMerchantVault (automatic stacking occurs)
4. Clear equipped slots on hero
5. Clear inventory bag

## Stacking Logic

### Same Item Detection
Items are considered the same if they match:
- **Name** (e.g., "HPPotion")
- **Kind** (e.g., ItemKind.Consumable)
- **Rarity** (e.g., ItemRarity.Normal)

### Stacking Algorithm
When adding an item:
1. Check for existing stacks of the same item with available space (< 999)
2. Fill existing stacks first (up to 999 max)
3. If quantity remains, create new stacks (each up to 999 max)
4. Repeat until all quantity is stored

### Examples

#### Example 1: Simple Stacking
```
Hero 1 dies with 20 HP Potions ? Vault: [HPPotion x20]
Hero 2 dies with 30 HP Potions ? Vault: [HPPotion x50]
```

#### Example 2: Max Stack Overflow
```
Hero 1 dies with 998 HP Potions ? Vault: [HPPotion x998]
Hero 2 dies with 10 HP Potions  ? Vault: [HPPotion x999], [HPPotion x9]
```

#### Example 3: Multiple Item Types
```
Hero dies with:
- 2x ShortSword (inventory duplicates)
- 1x LeatherArmor (equipped)
- 32x HPPotion (2 stacks of 16 each)
- 1x ProtectRing (equipped)

Result in Vault:
[ShortSword x2]
[LeatherArmor x1]
[HPPotion x32]
[ProtectRing x1]
```

#### Example 4: Accumulation Across Multiple Deaths
```
10 heroes die, each with 150 HP Potions
Total: 10 × 150 = 1500 potions

Result in Vault:
[HPPotion x999]  (first stack maxed)
[HPPotion x501]  (remainder in second stack)
```

## Testing

### Test Coverage
**Location**: `PitHero.Tests/SecondChanceMerchantVaultTests.cs`

Comprehensive test suite with 14 tests covering:
- Single item addition
- Identical item stacking
- Different item separation
- Consumable stacking
- Max stack overflow handling
- Multiple item batch addition
- Remove quantity operations
- Hero death simulation
- Multi-hero accumulation
- Large stack handling across multiple heroes

All tests pass successfully.

## Future UI Implementation

The system is designed for future UI integration where players can:
- View all items in the Second Chance vault
- Purchase items back at a price
- See quantity available for each item type
- Filter/search through accumulated items

The `RemoveQuantity` method is already implemented to support purchasing:
```csharp
var stack = vault.Stacks[0];
bool success = vault.RemoveQuantity(stack, 5);  // Purchase 5 items
```

## Design Considerations

### Why 999 Stack Size?
- Matches common RPG conventions (e.g., Final Fantasy)
- Allows significant accumulation across many hero deaths
- Still requires multiple stacks for extreme cases
- Easy to display in UI (3 digits)

### Why Stack Everything (Even Gear)?
- Simplifies storage and reduces memory overhead
- Makes sense for items with identical properties
- Enables bulk purchasing in future UI
- Tracks item value across multiple hero deaths

### Item Template Pattern
Each stack stores the item as a template, with quantity tracked separately. This:
- Avoids creating duplicate item objects
- Reduces memory usage
- Makes quantity operations simple
- Works well for both consumables and gear

## Integration Points

### Services Used:
- **SecondChanceMerchantVault**: New service for item storage
- **CrystalMerchantVault**: Existing service for crystal storage (unchanged)
- **PitMerchantVault**: Existing service for regular merchant (unchanged)

### Components Modified:
- **HeroDeathComponent**: Now transfers items to Second Chance vault instead of Pit vault
- **Game1**: Registers the new service

### No Breaking Changes:
- Existing PitMerchantVault remains for its original purpose
- Hero death animation and crystal transfer unchanged
- No impact on existing gameplay systems

## Summary

The Second Chance Merchant provides a complete item recovery system for permadeath scenarios:
? All hero items (equipped + inventory) are saved
? Items automatically stack up to 999
? Handles overflow across multiple stacks
? Works with consumables and gear
? Accumulates across multiple hero deaths
? Ready for future purchase UI
? Fully tested with 14 passing tests
? No breaking changes to existing systems
