# Equipment Balance Guide

## Introduction

This guide provides comprehensive guidelines for creating and balancing equipment in PitHero. All formulas and configurations are centralized in `BalanceConfig.cs`, ensuring consistent scaling across the game's 100 pit levels and all rarity tiers.

**Balance Philosophy:**
- Equipment stats scale with pit level (1-100) and rarity (Normal to Legendary)
- Progression provides meaningful power increases without breaking balance
- Elemental properties add strategic depth to equipment choices
- Different equipment types serve distinct roles in character builds

---

## System Constants and Stat Caps

All equipment stat bonuses are subject to the game's hard stat caps defined in `StatConstants.cs`:

**Hard Caps:**
- **HP:** Maximum 9999
- **MP:** Maximum 999
- **Stats (STR/AGI/VIT/MAG):** Maximum 99 each
- **Level:** Maximum 99

**Important Notes:**
- Equipment bonuses cannot push stats beyond these caps
- These caps apply to the final calculated values after all equipment and job bonuses
- The stat caps ensure balance and prevent overflow issues in damage calculations
- When designing equipment, consider that high-level characters may already be near cap values

**Reference:** See `StatConstants.cs` for implementation and clamping functions.

---

## Equipment Stat Calculation Formulas

All equipment stats are calculated using formulas in `BalanceConfig.cs` that take into account the pit level and rarity.

### Attack Bonus Calculation (Weapons)

**Formula:** `(1 + pitLevel / 2) * rarity_multiplier`

**Purpose:** Determines the flat attack bonus provided by weapons.

**Example Values:**

| Pit Level | Normal | Uncommon | Rare | Epic | Legendary |
|-----------|--------|----------|------|------|-----------|
| 1         | 1      | 1        | 2    | 2    | 3         |
| 10        | 6      | 9        | 12   | 15   | 21        |
| 25        | 13     | 19       | 26   | 32   | 45        |
| 50        | 26     | 39       | 52   | 65   | 91        |
| 75        | 38     | 57       | 76   | 95   | 133       |
| 100       | 51     | 76       | 102  | 127  | 178       |

**Tuning Advice:** Adjust divisor (2) if weapons feel too weak/strong relative to monster defense.

---

### Defense Bonus Calculation (Armor, Shields, Helms)

**Formula:** `(1 + pitLevel / 3) * rarity_multiplier`

**Purpose:** Determines the flat defense bonus provided by defensive equipment.

**Example Values:**

| Pit Level | Normal | Uncommon | Rare | Epic | Legendary |
|-----------|--------|----------|------|------|-----------|
| 1         | 1      | 2        | 2    | 3    | 4         |
| 10        | 4      | 6        | 8    | 10   | 15        |
| 25        | 9      | 14       | 18   | 23   | 32        |
| 50        | 17     | 26       | 35   | 44   | 61        |
| 75        | 26     | 39       | 52   | 65   | 91        |
| 100       | 34     | 51       | 68   | 85   | 120       |

**Tuning Advice:** Defense is intentionally lower than attack to keep combat fast-paced. Adjust divisor (3) if armor feels too weak/strong.

---

### Stat Bonus Calculation (Accessories, Secondary Stats)

**Formula:** `(pitLevel / 5) * rarity_multiplier`

**Purpose:** Determines bonus to primary stats (Strength, Agility, Vitality, Magic).

**Example Values:**

| Pit Level | Normal | Uncommon | Rare | Epic | Legendary |
|-----------|--------|----------|------|------|-----------|
| 1         | 0      | 0        | 0    | 0    | 0         |
| 10        | 2      | 3        | 4    | 5    | 7         |
| 25        | 5      | 7        | 10   | 12   | 17        |
| 50        | 10     | 15       | 20   | 25   | 35        |
| 75        | 15     | 22       | 30   | 37   | 52        |
| 100       | 20     | 30       | 40   | 50   | 70        |

**Tuning Advice:** Stat bonuses provide secondary scaling. Adjust divisor (5) if stat bonuses feel too impactful/negligible.

---

## Rarity System

### Rarity Multipliers

Equipment rarity determines the power multiplier applied to base stat calculations:

| Rarity    | Multiplier | Description                        |
|-----------|------------|------------------------------------|
| Normal    | 1.0x       | Standard equipment, widely available |
| Uncommon  | 1.5x       | Enhanced equipment, better stats   |
| Rare      | 2.0x       | Powerful equipment, significant boost |
| Epic      | 2.5x       | Very powerful, rare finds          |
| Legendary | 3.5x       | Ultimate equipment, extremely rare |

**Implementation:**
```csharp
int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(pitLevel, ItemRarity.Rare);
```

---

## Pit Level Tiers

Equipment is designed for different pit level ranges, representing progression through the game:

### Tier Breakdown

| Tier          | Pit Levels | Description                    | Example Items        |
|---------------|------------|--------------------------------|----------------------|
| Starter       | 1-10       | Basic equipment for beginners  | ShortSword, LeatherArmor |
| Early         | 11-25      | Improved equipment             | LongSword, IronArmor |
| Mid           | 26-40      | Solid mid-game equipment       | Future items         |
| Late          | 41-70      | Advanced equipment             | Future items         |
| Legendary     | 71-100     | End-game equipment             | Future items         |

**Assignment Guidelines:**
- Assign pit level based on intended acquisition depth
- Higher rarity items can exist at lower pit levels (rare drops)
- Ensure smooth progression curve between tiers

---

## Equipment Types

### Weapons (Swords, etc.)

**Primary Stat:** Attack Bonus  
**Formula:** `CalculateEquipmentAttackBonus(pitLevel, rarity)`

**Current Weapons:**
- **ShortSword** (Pit 5, Normal): 3 Attack, Neutral element
- **LongSword** (Pit 15, Normal): 8 Attack, Fire element

**Design Pattern:**
```csharp
private const int PitLevel = 15;
private const ItemRarity Rarity = ItemRarity.Normal;

public static Gear Create()
{
    int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
    return new Gear(
        "WeaponName",
        ItemKind.WeaponSword,
        Rarity,
        $"+{attackBonus} Attack",
        priceInGold,
        new StatBlock(0, 0, 0, 0),
        atk: attackBonus,
        elementalProps: new ElementalProperties(ElementType.Fire));
}
```

---

### Armor

**Primary Stat:** Defense Bonus  
**Formula:** `CalculateEquipmentDefenseBonus(pitLevel, rarity)`

**Current Armor:**
- **LeatherArmor** (Pit 5, Normal): 2 Defense, Neutral element
- **IronArmor** (Pit 15, Normal): 6 Defense, Earth element with resistances

**Design Pattern:**
```csharp
private const int PitLevel = 15;
private const ItemRarity Rarity = ItemRarity.Normal;

public static Gear Create()
{
    int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
    return new Gear(
        "ArmorName",
        ItemKind.ArmorMail,
        Rarity,
        $"+{defenseBonus} Defense, Earth Resistant",
        priceInGold,
        new StatBlock(0, 0, 0, 0),
        def: defenseBonus,
        elementalProps: new ElementalProperties(
            ElementType.Earth,
            new Dictionary<ElementType, float>
            {
                { ElementType.Earth, 0.25f },   // 25% resistance
                { ElementType.Wind, -0.15f }    // 15% weakness
            }));
}
```

---

### Shields

**Primary Stat:** Defense Bonus  
**Formula:** `CalculateEquipmentDefenseBonus(pitLevel, rarity)`

**Current Shields:**
- **WoodenShield** (Pit 5, Normal): 2 Defense, Neutral element
- **IronShield** (Pit 15, Normal): 6 Defense, Water element with resistances

**Design Notes:**
- Shields typically provide slightly less defense than armor
- Can have stronger elemental resistances than armor
- Often paired with specific elemental themes

---

### Helms

**Primary Stat:** Defense Bonus  
**Formula:** `CalculateEquipmentDefenseBonus(pitLevel, rarity)`

**Current Helms:**
- **SquireHelm** (Pit 5, Normal): 2 Defense, Neutral element
- **IronHelm** (Pit 15, Normal): 6 Defense, Earth element with resistances

**Design Notes:**
- Helms provide similar defense to shields
- Can include minor stat bonuses in advanced versions
- Often themed around protection types (physical vs. magical)

---

### Accessories (Rings, Necklaces)

**Primary Stats:** Varies (stats, defense, HP, MP)  
**Formulas:** 
- Stats: `CalculateEquipmentStatBonus(pitLevel, rarity)`
- Defense: `CalculateEquipmentDefenseBonus(pitLevel, rarity)`
- HP/MP: Derived from stat bonuses (HP = stat * 5, MP = stat * 3)

**Current Accessories:**
- **RingOfPower** (Pit 15, Uncommon): +4 Strength
- **NecklaceOfHealth** (Pit 20, Rare): +40 HP, +8 Vitality
- **ProtectRing** (Pit 12, Normal): +5 Defense, +2 Vitality
- **MagicChain** (Pit 18, Uncommon): +15 MP, +5 Magic

**Design Pattern:**
```csharp
private const int PitLevel = 20;
private const ItemRarity Rarity = ItemRarity.Rare;

public static Gear Create()
{
    int statBonus = BalanceConfig.CalculateEquipmentStatBonus(PitLevel, Rarity);
    int hpBonus = statBonus * 5; // HP scales with stat
    return new Gear(
        "AccessoryName",
        ItemKind.Accessory,
        Rarity,
        $"+{hpBonus} HP, +{statBonus} Vitality",
        priceInGold,
        new StatBlock(0, 0, statBonus, 0),
        hp: hpBonus,
        elementalProps: new ElementalProperties(ElementType.Light));
}
```

**HP/MP Scaling Guidelines:**
- HP bonus: stat * 5 (for vitality-focused items)
- MP bonus: stat * 3 (for magic-focused items)
- These multipliers can be adjusted per item for variety

---

## Elemental System Integration

### Element Assignments

Equipment can have elemental properties that affect combat:

**Current Element Distribution:**

| Equipment Type | Element | Notes                              |
|----------------|---------|-------------------------------------|
| ShortSword     | Neutral | Basic weapon                        |
| LongSword      | Fire    | Offensive element                   |
| LeatherArmor   | Neutral | Basic armor                         |
| IronArmor      | Earth   | Defensive element with resistances  |
| WoodenShield   | Neutral | Basic shield                        |
| IronShield     | Water   | Defensive element with resistances  |
| SquireHelm     | Neutral | Basic helm                          |
| IronHelm       | Earth   | Defensive element with resistances  |
| RingOfPower    | Neutral | Universal accessory                 |
| NecklaceOfHealth | Light | Healing/vitality theme            |
| ProtectRing    | Neutral | Universal defensive accessory       |
| MagicChain     | Dark    | Magical/mysterious theme            |

### Element Types and Matchups

**Available Elements:**
- **Neutral:** No advantages or disadvantages
- **Fire:** Opposes Water
- **Water:** Opposes Fire
- **Earth:** Opposes Wind
- **Wind:** Opposes Earth
- **Light:** Opposes Dark
- **Dark:** Opposes Light

**Damage Multipliers (Base Elemental Matchups):**
- **2.0x damage** when attacking with an element that opposes the defender's element (advantage)
  - Example: Fire attack vs Water defender = 2.0x damage
- **0.5x damage** when attacking with the same element as the defender (disadvantage)
  - Example: Fire attack vs Fire defender = 0.5x damage
- **1.0x damage** for Neutral attacks, Neutral defenders, or unrelated elements
  - Example: Fire attack vs Earth defender = 1.0x damage (no relationship)

**Custom Resistances:**
Custom resistance values in `ElementalProperties.Resistances` modify the base multipliers:
- **Positive values** = resistance (damage reduction)
  - Example: `{ ElementType.Fire, 0.5f }` = 50% resistance to Fire (reduces damage by 50%)
- **Negative values** = weakness (damage increase)
  - Example: `{ ElementType.Water, -0.5f }` = 50% weakness to Water (increases damage by 50%)

**Complete Formula:**
See `BalanceConfig.GetElementalDamageMultiplier()` for the complete implementation.

### Resistance Patterns

**Standard Pattern for Defensive Gear:**
```csharp
var resistances = new Dictionary<ElementType, float>
{
    { ElementType.Fire, 0.25f },   // 25-30% resistance to own element
    { ElementType.Water, -0.15f }  // 10-15% weakness to opposing element
};
```

**Guidelines:**
- Weapons: Usually pure element (no resistances)
- Armor/Shields/Helms: Can have resistances to own element
- Accessories: Typically pure element (no resistances)
- Neutral equipment: No resistances or weaknesses

---

## Creating New Equipment: Step-by-Step Guide

### Step 1: Determine Pit Level and Rarity

**Questions to ask:**
- At what pit depth should this equipment be found?
- What rarity tier is appropriate?
- Is this a progression item or a rare drop?

**Pit Level Assignment:**
- Starter (1-10): Basic equipment for early game
- Early (11-25): First upgrades
- Mid (26-40): Mid-game equipment
- Late (41-70): Advanced equipment
- Legendary (71-100): End-game equipment

### Step 2: Choose Equipment Type and Stats

**Equipment Type Selection:**
- **Weapon:** Focuses on attack bonus
- **Armor:** Focuses on defense bonus
- **Shield:** Focuses on defense bonus (slightly lower than armor)
- **Helm:** Focuses on defense bonus
- **Accessory:** Flexible (stats, HP, MP, or defense)

### Step 3: Calculate Base Stats

Use the appropriate BalanceConfig method:

```csharp
// For weapons
int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(pitLevel, rarity);

// For armor/shields/helms
int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(pitLevel, rarity);

// For accessories (stats)
int statBonus = BalanceConfig.CalculateEquipmentStatBonus(pitLevel, rarity);
```

### Step 4: Assign Element and Resistances

**Element Selection:**
- Consider thematic fit (Fire sword, Earth armor, etc.)
- Balance element distribution across available equipment
- Neutral is safe but less interesting

**Resistance Assignment (optional):**
- Only for defensive gear typically
- 20-30% resistance to own element
- 10-15% weakness to opposing element

### Step 5: Implement the Equipment Class

**Code Template:**

```csharp
using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Equipment.Category
{
    /// <summary>Factory for creating YourItem gear.</summary>
    public static class YourItem
    {
        private const int PitLevel = 25;
        private const ItemRarity Rarity = ItemRarity.Rare;

        public static Gear Create()
        {
            // Calculate stats using BalanceConfig
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            
            // Set up elemental properties (optional)
            var resistances = new Dictionary<ElementType, float>
            {
                { ElementType.Fire, 0.3f },   // Resist own element
                { ElementType.Water, -0.3f }  // Weak to opposing element
            };
            
            return new Gear(
                "YourItem",
                ItemKind.WeaponSword, // Choose appropriate kind
                Rarity,
                $"+{attackBonus} Attack", // Update based on stats
                500, // Price in gold
                new StatBlock(0, 0, 0, 0), // Stat bonuses
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Fire, resistances));
        }
    }
}
```

### Step 6: Register in GearItems

Add a factory method in `GearItems.cs`:

```csharp
/// <summary>Create Your Item.</summary>
public static Gear YourItem() => Category.YourItem.Create();
```

### Step 7: Add Tests

Add test cases in `GearItemsTests.cs`:

```csharp
[TestMethod]
public void YourItem_ShouldHaveCorrectProperties()
{
    var item = GearItems.YourItem();
    
    Assert.IsNotNull(item);
    Assert.AreEqual("YourItem", item.Name);
    Assert.AreEqual(ItemKind.WeaponSword, item.Kind);
    Assert.AreEqual(ItemRarity.Rare, item.Rarity);
    
    // Verify stats match BalanceConfig calculation
    int expectedAttack = BalanceConfig.CalculateEquipmentAttackBonus(25, ItemRarity.Rare);
    Assert.AreEqual(expectedAttack, item.AttackBonus);
}
```

---

## Testing and Validation

### Testing Checklist

- [ ] Equipment stats calculated correctly via BalanceConfig
- [ ] Description reflects actual bonuses
- [ ] Rarity tier appropriate for pit level
- [ ] Element assignment is thematic and balanced
- [ ] Resistances work correctly (if applicable)
- [ ] Price is reasonable for tier
- [ ] Tests pass for all stat values

### Balance Validation

**Compare to existing equipment:**
- Is this item significantly better/worse than items at similar pit levels?
- Does the rarity multiplier make sense?
- Would players reasonably choose this over alternatives?

**Power Curve Check:**
- Plot attack/defense values across pit levels
- Ensure smooth progression without sudden jumps
- Verify rarity tiers scale appropriately

---

## Best Practices

### 1. Consistent Formulas

**Always use BalanceConfig methods:**
```csharp
// Good
int atk = BalanceConfig.CalculateEquipmentAttackBonus(pitLevel, rarity);

// Bad - hardcoded values
int atk = 10;
```

### 2. Meaningful Progression

**Ensure equipment upgrades feel worthwhile:**
- 20-30% stat increase between tiers is noticeable
- Rarity upgrades should be desirable
- Late-game equipment should feel powerful

### 3. Elemental Variety

**Spread elements across equipment:**
- Not all items need to be Neutral
- Consider player team composition
- Create interesting strategic choices

### 4. Descriptive Names and Descriptions

**Clear communication:**
```csharp
// Good - shows exact bonuses
$"+{attackBonus} Attack, Fire Element"

// Bad - vague
"Powerful sword"
```

### 5. Testing at Multiple Pit Levels

**Verify formulas at key milestones:**
- Pit 1 (absolute minimum)
- Pit 25 (early-mid transition)
- Pit 50 (mid-game)
- Pit 75 (late game)
- Pit 100 (maximum)

---

## Formula Reference

### Quick Reference Table

| Formula | Purpose | Example (Pit 50, Normal) |
|---------|---------|--------------------------|
| Atk = (1 + P/2) * R | Weapon attack | 26 |
| Def = (1 + P/3) * R | Armor defense | 17 |
| Stat = (P/5) * R | Stat bonus | 10 |
| HP = Stat * 5 | HP bonus | 50 |
| MP = Stat * 3 | MP bonus | 30 |

### Rarity Multipliers

| Rarity | Multiplier |
|--------|------------|
| Normal | 1.0x |
| Uncommon | 1.5x |
| Rare | 2.0x |
| Epic | 2.5x |
| Legendary | 3.5x |

### Code Reference

All formulas implemented in: `/PitHero/RolePlayingFramework/Balance/BalanceConfig.cs`

Key methods:
- `BalanceConfig.CalculateEquipmentAttackBonus(pitLevel, rarity)`
- `BalanceConfig.CalculateEquipmentDefenseBonus(pitLevel, rarity)`
- `BalanceConfig.CalculateEquipmentStatBonus(pitLevel, rarity)`

---

## Conclusion

This guide provides a comprehensive framework for creating balanced equipment in PitHero. By following these formulas and guidelines, you can create diverse, meaningful, and balanced equipment that scales appropriately across all 100 pit levels.

**Key Takeaways:**
- Use `BalanceConfig` methods for all stat calculations
- Assign pit levels based on intended acquisition depth
- Choose rarity based on power level and availability
- Add elements and resistances for strategic depth
- Test formulas at multiple pit levels
- Write tests to verify calculations

**Remember:** Balance is iterative. Use the formulas as a starting point, then adjust based on playtesting feedback. The centralized `BalanceConfig` makes tuning easy!

---

**Related Documentation:**
- `BalanceConfig.cs` - All balance formulas and constants
- `MONSTER_BALANCE_GUIDE.md` - Monster balancing guidelines
- `Gear.cs` - Equipment class definition
- Existing equipment implementations in `/PitHero/RolePlayingFramework/Equipment/`

**For Questions or Balance Feedback:**
- File an issue on GitHub
- Reference this guide when discussing balance changes
- Suggest formula adjustments based on playtesting data
