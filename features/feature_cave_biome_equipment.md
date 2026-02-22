# Feature: Cave Biome Phase 2 - Equipment Implementation

**Agent**: Planner Agent  
**Feature ID**: CAVE_BIOME_EQUIPMENT_001  
**Version**: 1.0  
**Status**: Planning Complete  
**Target Release**: Sprint 2

---

## 1. Feature Name

**Cave Biome Phase 2: Equipment Implementation (113 Missing Pieces)**

---

## 2. Objective

Create 113 missing equipment pieces for the Cave Biome (Pit Levels 1-25) using the established factory pattern, BalanceConfig formulas, and integration with GearItems.cs. This completes the Cave Biome equipment roster to provide full item progression across all 25 pit levels.

### Success Metrics
- All 113 equipment files created following factory pattern
- All equipment integrated into GearItems.cs
- All equipment uses BalanceConfig formulas correctly
- Project compiles successfully after implementation
- Unit tests validate all equipment stats match EQUIPMENT_LIBRARY.md specifications

---

## 3. Inputs Consumed

### Source Documents
1. **EQUIPMENT_LIBRARY.md**: Complete specification of all 113 missing pieces
   - Equipment stats (pit level, rarity, bonuses)
   - Visual themes and descriptions
   - Price and element assignments
   - Spawn window information

2. **Existing Implementation Pattern**:
   - Factory pattern: `Swords/ShortSword.cs`, `Swords/LongSword.cs`
   - Integration pattern: `GearItems.cs`
   - Balance formulas: `BalanceConfig.CalculateEquipmentAttackBonus()`, `CalculateEquipmentDefenseBonus()`

3. **Existing Folder Structure**:
   - Implemented: `Swords/` (11 files), `Axes/` (3 files), `Daggers/` (2 files), `Armor/` (2 files), `Shields/` (2 files), `Helms/` (2 files)
   - Missing: `Spears/`, `Hammers/`, `Staves/`

---

## 4. Equipment Inventory Analysis

### Current Implementation Status

#### Weapons (60 total: 16 implemented, 44 missing)
- **Swords**: 25 total (11 ✅, 14 ❌)
- **Axes**: 8 total (3 ✅, 5 ❌)
- **Daggers**: 6 total (2 ✅, 4 ❌)
- **Spears**: 6 total (0 ✅, 6 ❌) — *folder missing*
- **Hammers**: 5 total (0 ✅, 5 ❌) — *folder missing*
- **Staves**: 5 total (0 ✅, 5 ❌) — *folder missing*

#### Armor (25 total: 2 implemented, 23 missing)
- ✅ Implemented: LeatherArmor, IronArmor
- ❌ Missing: 23 pieces (Pit 1-25 progression)

#### Shields (25 total: 2 implemented, 23 missing)
- ✅ Implemented: WoodenShield, IronShield
- ❌ Missing: 23 pieces (Pit 1-25 progression)

#### Helms (25 total: 2 implemented, 23 missing)
- ✅ Implemented: SquireHelm, IronHelm
- ❌ Missing: 23 pieces (Pit 1-25 progression)

### Missing Equipment Breakdown (113 pieces)

| Category | Count | New Folders Required |
|----------|-------|----------------------|
| Swords   | 14    | No (folder exists)   |
| Axes     | 5     | No (folder exists)   |
| Daggers  | 4     | No (folder exists)   |
| Spears   | 6     | **Yes** (new folder) |
| Hammers  | 5     | **Yes** (new folder) |
| Staves   | 5     | **Yes** (new folder) |
| Armor    | 23    | No (folder exists)   |
| Shields  | 23    | No (folder exists)   |
| Helms    | 23    | No (folder exists)   |
| **Total**| **113** | **3 new folders**  |

---

## 5. Implementation Plan

### Phase Structure Overview

The implementation is broken into **4 phases** based on logical grouping, complexity, and dependencies:

1. **Phase 1**: Weapons Part 1 (Existing Folders) — 23 pieces
2. **Phase 2**: Weapons Part 2 (New Folders) — 16 pieces
3. **Phase 3**: Defensive Equipment Part 1 — 35 pieces
4. **Phase 4**: Defensive Equipment Part 2 — 39 pieces

Each phase includes:
- File creation with factory pattern
- Integration into GearItems.cs
- Build verification
- Formula validation

---

## Phase 1: Weapons Part 1 (Existing Folders)

**Objective**: Complete weapon types that already have folder structure in place.

### Deliverables (23 pieces)

#### Swords/ (14 new files)
1. `CrystalEdge.cs` (Pit 11, Uncommon, Earth)
2. `UndergroundRapier.cs` (Pit 12, Uncommon, Neutral)
3. `EmberSword.cs` (Pit 13, Uncommon, Fire)
4. `VoidCutter.cs` (Pit 14, Uncommon, Darkness)
5. `StalagmiteSword.cs` (Pit 16, Uncommon, Earth)
6. `GloomBlade.cs` (Pit 17, Uncommon, Darkness)
7. `LavaForgedSword.cs` (Pit 18, Uncommon, Fire)
8. `DepthsReaver.cs` (Pit 19, Uncommon, Darkness)
9. `QuartzSaber.cs` (Pit 20, Uncommon, Earth)
10. `InfernoEdge.cs` (Pit 21, Uncommon, Fire)
11. `AbyssFang.cs` (Pit 22, Uncommon, Darkness)
12. `DiamondEdge.cs` (Pit 23, Uncommon, Earth)
13. `MagmaBlade.cs` (Pit 24, Uncommon, Fire)
14. `PitLordsSword.cs` (Pit 25, Uncommon, Darkness)

#### Axes/ (5 new files)
1. `FlameHatchet.cs` (Pit 10, Normal, Fire)
2. `CrystalCleaver.cs` (Pit 13, Uncommon, Earth)
3. `ShadowSplitter.cs` (Pit 16, Uncommon, Darkness)
4. `VolcanicAxe.cs` (Pit 20, Uncommon, Fire)
5. `ObsidianCleaver.cs` (Pit 24, Uncommon, Darkness)

#### Daggers/ (4 new files)
1. `SilentFang.cs` (Pit 8, Normal, Darkness)
2. `SerpentsTooth.cs` (Pit 12, Uncommon, Darkness)
3. `ShadowStiletto.cs` (Pit 17, Uncommon, Darkness)
4. `AssassinsEdge.cs` (Pit 22, Uncommon, Darkness)

### GearItems.cs Integration (23 methods)
Add 23 factory methods following pattern:
```csharp
/// <summary>Create Crystal Edge.</summary>
public static Gear CrystalEdge() => Swords.CrystalEdge.Create();
```

### Implementation Steps

1. **Create Sword Files** (14 files)
   - Location: `PitHero/RolePlayingFramework/Equipment/Swords/`
   - Use ShortSword.cs and LongSword.cs as templates
   - Follow EQUIPMENT_LIBRARY.md specifications exactly

2. **Create Axe Files** (5 files)
   - Location: `PitHero/RolePlayingFramework/Equipment/Axes/`
   - Use existing axes as templates

3. **Create Dagger Files** (4 files)
   - Location: `PitHero/RolePlayingFramework/Equipment/Daggers/`
   - Use existing daggers as templates

4. **Update GearItems.cs**
   - Add 23 factory methods in alphabetical order within weapon section
   - Maintain XML documentation format

5. **Build Verification**
   - Run: `dotnet build`
   - Fix any compilation errors

6. **Formula Validation**
   - Verify attack bonuses match EQUIPMENT_LIBRARY.md
   - Verify element assignments correct
   - Verify rarity settings correct

### Success Criteria
- ✅ 23 new weapon files created
- ✅ All files follow factory pattern
- ✅ GearItems.cs updated with 23 methods
- ✅ Project compiles without errors
- ✅ All weapons use correct BalanceConfig formulas

---

## Phase 2: Weapons Part 2 (New Folders)

**Objective**: Create new weapon type folders and implement spears, hammers, and staves.

### New Folders Required
1. `PitHero/RolePlayingFramework/Equipment/Spears/`
2. `PitHero/RolePlayingFramework/Equipment/Hammers/`
3. `PitHero/RolePlayingFramework/Equipment/Staves/`

### Deliverables (16 pieces)

#### Spears/ (6 new files)
1. `WoodenSpear.cs` (Pit 2, Normal, Neutral)
2. `StoneLance.cs` (Pit 6, Normal, Earth)
3. `CavePike.cs` (Pit 11, Uncommon, Earth)
4. `FlameLance.cs` (Pit 15, Uncommon, Fire)
5. `StalactiteSpear.cs` (Pit 19, Uncommon, Earth)
6. `InfernalPike.cs` (Pit 23, Uncommon, Fire)

**ItemKind**: Use `ItemKind.WeaponSword` (per EQUIPMENT_LIBRARY.md specification)

#### Hammers/ (5 new files)
1. `Mallet.cs` (Pit 3, Normal, Neutral)
2. `StoneCrusher.cs` (Pit 7, Normal, Earth)
3. `GeologistsHammer.cs` (Pit 12, Uncommon, Earth)
4. `QuakeHammer.cs` (Pit 18, Uncommon, Earth)
5. `MagmaMaul.cs` (Pit 25, Uncommon, Fire)

**ItemKind**: Use `ItemKind.WeaponKnuckle` (per EQUIPMENT_LIBRARY.md specification)

#### Staves/ (5 new files)
1. `WalkingStick.cs` (Pit 2, Normal, Neutral)
2. `TorchStaff.cs` (Pit 6, Normal, Fire)
3. `EarthenStaff.cs` (Pit 11, Uncommon, Earth)
4. `ShadowwoodStaff.cs` (Pit 16, Uncommon, Darkness)
5. `EmberRod.cs` (Pit 21, Uncommon, Fire)

**ItemKind**: Use `ItemKind.WeaponStaff` (per EQUIPMENT_LIBRARY.md specification)

### GearItems.cs Integration (16 methods)
Add 16 factory methods for new weapon types.

### Implementation Steps

1. **Create Spears/ Folder**
   - Location: `PitHero/RolePlayingFramework/Equipment/Spears/`
   - Create 6 factory files
   - Use sword pattern as template

2. **Create Hammers/ Folder**
   - Location: `PitHero/RolePlayingFramework/Equipment/Hammers/`
   - Create 5 factory files
   - Use sword pattern as template

3. **Create Staves/ Folder**
   - Location: `PitHero/RolePlayingFramework/Equipment/Staves/`
   - Create 5 factory files
   - Use sword pattern as template

4. **Update GearItems.cs**
   - Add 16 factory methods in logical grouping
   - Maintain alphabetical order within each weapon type

5. **Build Verification**
   - Run: `dotnet build`
   - Fix any compilation errors

6. **Formula Validation**
   - Verify attack bonuses match EQUIPMENT_LIBRARY.md
   - Verify ItemKind assignments correct

### Success Criteria
- ✅ 3 new folders created
- ✅ 16 new weapon files created
- ✅ All files follow factory pattern
- ✅ GearItems.cs updated with 16 methods
- ✅ Project compiles without errors
- ✅ Correct ItemKind used for each weapon type

---

## Phase 3: Defensive Equipment Part 1 (Armor)

**Objective**: Complete armor progression for Cave Biome (Pit 1-25).

### Deliverables (23 pieces)

#### Armor/ (23 new files)
1. `TatteredCloth.cs` (Pit 1, Normal, Neutral, ArmorRobe)
2. `BurlapTunic.cs` (Pit 2, Normal, Neutral, ArmorRobe)
3. `HideVest.cs` (Pit 3, Normal, Earth, ArmorGi)
4. `PaddedArmor.cs` (Pit 4, Normal, Neutral, ArmorGi)
5. `StuddedLeather.cs` (Pit 6, Normal, Neutral, ArmorGi)
6. `CaveExplorersVest.cs` (Pit 7, Normal, Earth, ArmorGi)
7. `HardenedLeather.cs` (Pit 8, Normal, Neutral, ArmorGi)
8. `ScaleMail.cs` (Pit 9, Normal, Neutral, ArmorMail)
9. `ChainShirt.cs` (Pit 10, Normal, Neutral, ArmorMail)
10. `StonePlate.cs` (Pit 12, Uncommon, Earth, ArmorMail)
11. `EmberguardMail.cs` (Pit 13, Uncommon, Fire, ArmorMail)
12. `ShadowVest.cs` (Pit 14, Uncommon, Darkness, ArmorGi)
13. `ReinforcedPlate.cs` (Pit 15, Uncommon, Neutral, ArmorMail)
14. `CrystalGuard.cs` (Pit 16, Uncommon, Earth, ArmorMail)
15. `LavaplateArmor.cs` (Pit 17, Uncommon, Fire, ArmorMail)
16. `Voidmail.cs` (Pit 18, Uncommon, Darkness, ArmorMail)
17. `SteelCuirass.cs` (Pit 19, Uncommon, Neutral, ArmorMail)
18. `GranitePlate.cs` (Pit 20, Uncommon, Earth, ArmorMail)
19. `VolcanicArmor.cs` (Pit 21, Uncommon, Fire, ArmorMail)
20. `AbyssPlate.cs` (Pit 22, Uncommon, Darkness, ArmorMail)
21. `DiamondMail.cs` (Pit 23, Uncommon, Earth, ArmorMail)
22. `MagmaForgedPlate.cs` (Pit 24, Uncommon, Fire, ArmorMail)
23. `PitLordsArmor.cs` (Pit 25, Uncommon, Darkness, ArmorMail)

**Note**: Pay attention to ItemKind variations:
- `ItemKind.ArmorRobe`: Cloth-based armor (Pit 1-2)
- `ItemKind.ArmorGi`: Leather-based armor (Pit 3-8, 14)
- `ItemKind.ArmorMail`: Metal-based armor (Pit 9+)

### GearItems.cs Integration (23 methods)
Add 23 factory methods for armor pieces.

### Implementation Steps

1. **Create Armor Files** (23 files)
   - Location: `PitHero/RolePlayingFramework/Equipment/Armor/`
   - Use LeatherArmor.cs and IronArmor.cs as templates
   - Use `BalanceConfig.CalculateEquipmentDefenseBonus()` for defense values

2. **Update GearItems.cs**
   - Add 23 factory methods in alphabetical order

3. **Build Verification**
   - Run: `dotnet build`

4. **Formula Validation**
   - Verify defense bonuses match EQUIPMENT_LIBRARY.md
   - Verify ItemKind assignments (Robe/Gi/Mail) correct

### Success Criteria
- ✅ 23 armor files created
- ✅ All armor uses defense bonus formula
- ✅ Correct ItemKind (Robe/Gi/Mail) for each piece
- ✅ GearItems.cs updated with 23 methods
- ✅ Project compiles without errors

---

## Phase 4: Defensive Equipment Part 2 (Shields & Helms)

**Objective**: Complete shields and helms progression for Cave Biome (Pit 1-25).

### Deliverables (46 pieces)

#### Shields/ (23 new files)
1. `WoodenPlank.cs` (Pit 1, Normal, Neutral)
2. `HideShield.cs` (Pit 3, Normal, Earth)
3. `ReinforcedBuckler.cs` (Pit 4, Normal, Neutral)
4. `RoundShield.cs` (Pit 5, Normal, Neutral)
5. `CaveGuard.cs` (Pit 6, Normal, Earth)
6. `StoneShield.cs` (Pit 7, Normal, Earth)
7. `IronBuckler.cs` (Pit 8, Normal, Neutral)
8. `KiteShield.cs` (Pit 9, Normal, Neutral)
9. `SteelShield.cs` (Pit 11, Uncommon, Neutral)
10. `GraniteGuard.cs` (Pit 12, Uncommon, Earth)
11. `EmberShield.cs` (Pit 13, Uncommon, Fire)
12. `ShadowGuard.cs` (Pit 14, Uncommon, Darkness)
13. `TowerShield.cs` (Pit 15, Uncommon, Neutral)
14. `CrystalBarrier.cs` (Pit 16, Uncommon, Earth)
15. `LavaShield.cs` (Pit 17, Uncommon, Fire)
16. `VoidBarrier.cs` (Pit 18, Uncommon, Darkness)
17. `HeaterShield.cs` (Pit 19, Uncommon, Neutral)
18. `QuartzWall.cs` (Pit 20, Uncommon, Earth)
19. `InfernoGuard.cs` (Pit 21, Uncommon, Fire)
20. `AbyssWall.cs` (Pit 22, Uncommon, Darkness)
21. `DiamondBarrier.cs` (Pit 23, Uncommon, Earth)
22. `MagmaWall.cs` (Pit 24, Uncommon, Fire)
23. `PitLordsAegis.cs` (Pit 25, Uncommon, Darkness)

**ItemKind**: All shields use `ItemKind.Shield`

#### Helms/ (23 new files)
1. `ClothCap.cs` (Pit 1, Normal, Neutral, HatHeadband)
2. `LeatherCap.cs` (Pit 2, Normal, Neutral, HatHeadband)
3. `HideHood.cs` (Pit 3, Normal, Earth, HatHeadband)
4. `PaddedCoif.cs` (Pit 4, Normal, Neutral, HatHeadband)
5. `ChainCoif.cs` (Pit 6, Normal, Neutral, HatHelm)
6. `CaveExplorersHood.cs` (Pit 7, Normal, Earth, HatHeadband)
7. `ReinforcedCap.cs` (Pit 8, Normal, Neutral, HatHelm)
8. `Bascinet.cs` (Pit 9, Normal, Neutral, HatHelm)
9. `SteelHelm.cs` (Pit 11, Uncommon, Neutral, HatHelm)
10. `StoneCrown.cs` (Pit 12, Uncommon, Earth, HatHelm)
11. `EmberHelm.cs` (Pit 13, Uncommon, Fire, HatHelm)
12. `ShadowCowl.cs` (Pit 14, Uncommon, Darkness, HatHeadband)
13. `GreatHelm.cs` (Pit 15, Uncommon, Neutral, HatHelm)
14. `CrystalCirclet.cs` (Pit 16, Uncommon, Earth, HatHeadband)
15. `LavaCrown.cs` (Pit 17, Uncommon, Fire, HatHelm)
16. `VoidMask.cs` (Pit 18, Uncommon, Darkness, HatHelm)
17. `WingedHelm.cs` (Pit 19, Uncommon, Neutral, HatHelm)
18. `QuartzHelm.cs` (Pit 20, Uncommon, Earth, HatHelm)
19. `InfernoCrown.cs` (Pit 21, Uncommon, Fire, HatHelm)
20. `AbyssHelm.cs` (Pit 22, Uncommon, Darkness, HatHelm)
21. `DiamondCirclet.cs` (Pit 23, Uncommon, Earth, HatHeadband)
22. `MagmaHelm.cs` (Pit 24, Uncommon, Fire, HatHelm)
23. `PitLordsCrown.cs` (Pit 25, Uncommon, Darkness, HatHelm)

**Note**: Pay attention to ItemKind variations:
- `ItemKind.HatHeadband`: Cloth/leather headgear (lighter protection)
- `ItemKind.HatHelm`: Metal helmets (heavier protection)

### GearItems.cs Integration (46 methods)
Add 46 factory methods for shields and helms.

### Implementation Steps

1. **Create Shield Files** (23 files)
   - Location: `PitHero/RolePlayingFramework/Equipment/Shields/`
   - Use WoodenShield.cs and IronShield.cs as templates
   - Use `BalanceConfig.CalculateEquipmentDefenseBonus()` for defense values

2. **Create Helm Files** (23 files)
   - Location: `PitHero/RolePlayingFramework/Equipment/Helms/`
   - Use SquireHelm.cs and IronHelm.cs as templates
   - Use `BalanceConfig.CalculateEquipmentDefenseBonus()` for defense values

3. **Update GearItems.cs**
   - Add 46 factory methods (23 shields + 23 helms)
   - Organize alphabetically within each section

4. **Build Verification**
   - Run: `dotnet build`

5. **Formula Validation**
   - Verify defense bonuses match EQUIPMENT_LIBRARY.md
   - Verify ItemKind assignments correct

### Success Criteria
- ✅ 23 shield files created
- ✅ 23 helm files created
- ✅ All equipment uses defense bonus formula
- ✅ Correct ItemKind for each piece
- ✅ GearItems.cs updated with 46 methods
- ✅ Project compiles without errors

---

## 6. Implementation Details

### Factory Pattern Template

All equipment files must follow this exact pattern:

```csharp
using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.[Category]
{
    /// <summary>Factory for creating [EquipmentName] gear.</summary>
    public static class [ClassName]
    {
        private const int PitLevel = [X];
        private const ItemRarity Rarity = ItemRarity.[Normal|Uncommon];

        public static Gear Create()
        {
            int [attackOrDefense]Bonus = BalanceConfig.Calculate[Attack|Defense]Bonus(PitLevel, Rarity);
            return new Gear(
                "[DisplayName]",
                ItemKind.[WeaponSword|Shield|ArmorMail|HatHelm|etc],
                Rarity,
                $"[Description from EQUIPMENT_LIBRARY.md]",
                [PriceInGold],
                new StatBlock(0, 0, 0, 0),
                atk: [attackBonus], // For weapons only
                def: [defenseBonus], // For armor/shields/helms only
                elementalProps: new ElementalProperties(ElementType.[Element])
            );
        }
    }
}
```

### ItemKind Mapping Reference

**Weapons**:
- Swords → `ItemKind.WeaponSword`
- Axes → `ItemKind.WeaponSword`
- Daggers → `ItemKind.WeaponSword`
- Spears → `ItemKind.WeaponSword`
- Hammers → `ItemKind.WeaponKnuckle`
- Staves → `ItemKind.WeaponStaff`

**Armor**:
- Cloth armor → `ItemKind.ArmorRobe`
- Leather armor → `ItemKind.ArmorGi`
- Metal armor → `ItemKind.ArmorMail`

**Shields**:
- All shields → `ItemKind.Shield`

**Helms**:
- Cloth/leather → `ItemKind.HatHeadband`
- Metal helmets → `ItemKind.HatHelm`

### Element Mapping

From EQUIPMENT_LIBRARY.md:
- **Neutral**: No elemental properties
- **Earth**: Defensive, stone-based
- **Fire**: Aggressive, flame-based
- **Darkness**: Stealth, shadow-based

### Naming Conventions

**File Names**: PascalCase, no spaces
- EQUIPMENT_LIBRARY: "Crystal Edge" → FILE: `CrystalEdge.cs`
- EQUIPMENT_LIBRARY: "Shadow Guard" → FILE: `ShadowGuard.cs`
- EQUIPMENT_LIBRARY: "Pit Lord's Sword" → FILE: `PitLordsSword.cs`

**Class Names**: Match file names exactly

**Display Names**: Match EQUIPMENT_LIBRARY.md exactly (keep spaces, apostrophes)

### GearItems.cs Integration Pattern

Add methods in this format:
```csharp
/// <summary>Create [Equipment Name].</summary>
public static Gear [MethodName]() => [Category].[ClassName].Create();
```

Example:
```csharp
/// <summary>Create Crystal Edge.</summary>
public static Gear CrystalEdge() => Swords.CrystalEdge.Create();

/// <summary>Create Pit Lord's Sword.</summary>
public static Gear PitLordsSword() => Swords.PitLordsSword.Create();
```

**Organization in GearItems.cs**:
1. Weapons section (group by type: Swords, Axes, Daggers, Spears, Hammers, Staves)
2. Armor section
3. Shields section
4. Helms section
5. Accessories section (existing)

Within each section: alphabetical order.

---

## 7. Testing Strategy

### Build Validation (After Each Phase)
1. Run: `dotnet build`
2. Fix compilation errors immediately
3. Verify no warnings about incorrect factory signatures

### Formula Validation (After Each Phase)
For each equipment piece, validate:
1. **Attack Bonus** (Weapons):
   - Formula: `(1 + pitLevel / 2) × rarity_multiplier`
   - Normal: 1.0x
   - Uncommon: 1.5x
   
2. **Defense Bonus** (Armor/Shields/Helms):
   - Formula: `(1 + pitLevel / 3) × rarity_multiplier`
   - Normal: 1.0x
   - Uncommon: 1.5x

3. **Element Assignment**: Matches EQUIPMENT_LIBRARY.md

4. **ItemKind**: Correct for equipment type/subtype

### Integration Testing (Final Phase)
1. Verify all 113 methods exist in GearItems.cs
2. Test calling each factory method doesn't throw exceptions
3. Verify equipment stats match expected values

### Recommended Unit Tests

Create test file: `PitHero.Tests/RolePlayingFramework/Equipment/CaveBiomeEquipmentTests.cs`

Test categories:
1. **Factory Creation Tests**: Each equipment creates without errors
2. **Stat Validation Tests**: Attack/defense bonuses match formulas
3. **Element Tests**: Correct elemental properties assigned
4. **ItemKind Tests**: Correct equipment types assigned
5. **Rarity Tests**: Correct rarity assigned
6. **Price Tests**: Prices reasonable and match EQUIPMENT_LIBRARY.md

Example test structure:
```csharp
[Fact]
public void CrystalEdge_HasCorrectStats()
{
    var gear = GearItems.CrystalEdge();
    Assert.Equal("CrystalEdge", gear.Name);
    Assert.Equal(ItemKind.WeaponSword, gear.Kind);
    Assert.Equal(ItemRarity.Uncommon, gear.Rarity);
    Assert.Equal(9, gear.Atk); // (1 + 11/2) * 1.5 = 9.75 → 9
    Assert.Equal(ElementType.Earth, gear.ElementalProps.PrimaryElement);
}
```

---

## 8. Risk Mitigation

### Identified Risks

| Risk | Impact | Probability | Mitigation Strategy |
|------|--------|-------------|---------------------|
| **Incorrect formula calculations** | High | Medium | Validate each phase against EQUIPMENT_LIBRARY.md; create unit tests |
| **ItemKind assignment errors** | High | Medium | Reference mapping table; verify during code review |
| **Naming inconsistencies** | Medium | High | Follow strict naming convention; automate checks |
| **Missing GearItems.cs integration** | High | Low | Checklist for each piece; build verification |
| **Element assignment errors** | Medium | Medium | Cross-reference EQUIPMENT_LIBRARY.md; visual review |
| **Compilation errors** | High | Low | Build after each phase; fix immediately |
| **Large file editing conflicts** | Medium | Low | Use multi_replace for GearItems.cs; work incrementally |

### Quality Assurance Checklist

**Per Equipment Piece**:
- [ ] File name matches class name (PascalCase, no spaces)
- [ ] XML documentation comment present
- [ ] PitLevel constant matches EQUIPMENT_LIBRARY.md
- [ ] Rarity constant matches EQUIPMENT_LIBRARY.md
- [ ] Correct BalanceConfig formula used (Attack vs Defense)
- [ ] DisplayName matches EQUIPMENT_LIBRARY.md (preserve spaces/apostrophes)
- [ ] ItemKind matches equipment type
- [ ] Price matches EQUIPMENT_LIBRARY.md
- [ ] Element matches EQUIPMENT_LIBRARY.md
- [ ] Description present and accurate

**Per Phase**:
- [ ] All files created in correct folders
- [ ] GearItems.cs updated with all factory methods
- [ ] Methods added in alphabetical order
- [ ] `dotnet build` succeeds
- [ ] No compiler warnings

**Final Validation**:
- [ ] 113 equipment files created
- [ ] 113 factory methods in GearItems.cs
- [ ] All formulas validated
- [ ] Project compiles successfully
- [ ] Unit tests pass (if created)

---

## 9. Dependencies & Prerequisites

### Technical Dependencies
- **FNA Framework**: Properly initialized
- **Nez Framework**: Properly initialized
- **BalanceConfig.cs**: Contains formula methods
- **ItemKind enum**: Contains all required values
- **ItemRarity enum**: Contains Normal, Uncommon
- **ElementType enum**: Contains Neutral, Earth, Fire, Darkness

### Knowledge Prerequisites
- Understanding of factory pattern
- Familiarity with BalanceConfig formulas
- Knowledge of C# integer division (for formula calculations)
- Understanding of equipment stat progression

### File Dependencies
- `EQUIPMENT_LIBRARY.md`: Complete equipment specifications
- Existing equipment files: Templates for new implementations
- `GearItems.cs`: Integration point for all equipment

---

## 10. Estimated Effort

### Time Estimates (Per Phase)

| Phase | Files | GearItems.cs Lines | Estimated Time |
|-------|-------|-------------------|----------------|
| Phase 1 | 23 | 23 | 3-4 hours |
| Phase 2 | 16 | 16 | 2-3 hours (includes folder creation) |
| Phase 3 | 23 | 23 | 3-4 hours |
| Phase 4 | 46 | 46 | 5-6 hours |
| **Total** | **113** | **108** | **13-17 hours** |

### Breakdown
- **File creation**: ~4-5 minutes per file (templating, data entry, validation)
- **GearItems.cs integration**: ~1 minute per method
- **Build verification**: ~5 minutes per phase
- **Formula validation**: ~10-15 minutes per phase
- **Testing**: ~2-3 hours (if unit tests created)

---

## 11. Deliverables Summary

### Code Deliverables
1. **113 Equipment Factory Files**:
   - 14 Swords (complete to 25 total)
   - 5 Axes (complete to 8 total)
   - 4 Daggers (complete to 6 total)
   - 6 Spears (new type)
   - 5 Hammers (new type)
   - 5 Staves (new type)
   - 23 Armor pieces (complete to 25 total)
   - 23 Shields (complete to 25 total)
   - 23 Helms (complete to 25 total)

2. **3 New Folders**:
   - `Spears/`
   - `Hammers/`
   - `Staves/`

3. **GearItems.cs Updates**:
   - 113 new factory methods
   - Organized by category and alphabetically

### Documentation Deliverables
1. This implementation plan (feature_cave_biome_equipment.md)
2. Unit test file (CaveBiomeEquipmentTests.cs) — optional but recommended

### Validation Deliverables
1. Build success confirmation
2. Formula validation report
3. Equipment inventory completion report

---

## 12. Next Agent Handoff

### Recommended Next Agent: Principal Game Engineer Agent

**Handoff Context**:
- **Input**: This implementation plan + EQUIPMENT_LIBRARY.md
- **Task**: Implement all 4 phases sequentially
- **Output**: 113 equipment files + GearItems.cs integration + build success

**Handoff Checklist**:
- [ ] Implementation plan reviewed and understood
- [ ] EQUIPMENT_LIBRARY.md accessible
- [ ] Template files identified (ShortSword.cs, LongSword.cs, etc.)
- [ ] Development environment ready (FNA/Nez initialized)
- [ ] Ready to execute Phase 1

### Alternative: Virtual Game Layer Engineer Agent

If integration with virtual game layer is required before equipment implementation:
- Update `VirtualGearItems.cs` (if exists)
- Create virtual layer factory pattern
- Ensure equipment accessible in virtual game context

---

## 13. Completion Criteria

### Phase Completion
Each phase is complete when:
- ✅ All files created with correct factory pattern
- ✅ GearItems.cs updated with all methods for that phase
- ✅ Project builds successfully (`dotnet build`)
- ✅ Formulas validated against EQUIPMENT_LIBRARY.md
- ✅ No compilation warnings

### Overall Feature Completion
Feature is complete when:
- ✅ All 4 phases completed
- ✅ 113 equipment files exist
- ✅ 113 factory methods in GearItems.cs
- ✅ Project builds without errors
- ✅ All equipment stats validated
- ✅ Cave Biome has complete equipment progression (Pit 1-25)
- ✅ (Optional) Unit tests created and passing

---

## 14. Appendix: Quick Reference

### Phase 1 File List (23 files)
**Swords/**: CrystalEdge, UndergroundRapier, EmberSword, VoidCutter, StalagmiteSword, GloomBlade, LavaForgedSword, DepthsReaver, QuartzSaber, InfernoEdge, AbyssFang, DiamondEdge, MagmaBlade, PitLordsSword

**Axes/**: FlameHatchet, CrystalCleaver, ShadowSplitter, VolcanicAxe, ObsidianCleaver

**Daggers/**: SilentFang, SerpentsTooth, ShadowStiletto, AssassinsEdge

### Phase 2 File List (16 files)
**Spears/**: WoodenSpear, StoneLance, CavePike, FlameLance, StalactiteSpear, InfernalPike

**Hammers/**: Mallet, StoneCrusher, GeologistsHammer, QuakeHammer, MagmaMaul

**Staves/**: WalkingStick, TorchStaff, EarthenStaff, ShadowwoodStaff, EmberRod

### Phase 3 File List (23 files)
**Armor/**: TatteredCloth, BurlapTunic, HideVest, PaddedArmor, StuddedLeather, CaveExplorersVest, HardenedLeather, ScaleMail, ChainShirt, StonePlate, EmberguardMail, ShadowVest, ReinforcedPlate, CrystalGuard, LavaplateArmor, Voidmail, SteelCuirass, GranitePlate, VolcanicArmor, AbyssPlate, DiamondMail, MagmaForgedPlate, PitLordsArmor

### Phase 4 File List (46 files)
**Shields/**: WoodenPlank, HideShield, ReinforcedBuckler, RoundShield, CaveGuard, StoneShield, IronBuckler, KiteShield, SteelShield, GraniteGuard, EmberShield, ShadowGuard, TowerShield, CrystalBarrier, LavaShield, VoidBarrier, HeaterShield, QuartzWall, InfernoGuard, AbyssWall, DiamondBarrier, MagmaWall, PitLordsAegis

**Helms/**: ClothCap, LeatherCap, HideHood, PaddedCoif, ChainCoif, CaveExplorersHood, ReinforcedCap, Bascinet, SteelHelm, StoneCrown, EmberHelm, ShadowCowl, GreatHelm, CrystalCirclet, LavaCrown, VoidMask, WingedHelm, QuartzHelm, InfernoCrown, AbyssHelm, DiamondCirclet, MagmaHelm, PitLordsCrown

---

## 15. Decisions / Findings

### Planning Decisions

1. **Four-Phase Structure**: Chosen to balance workload and provide natural breakpoints
   - Phase 1: Complete existing weapon folders (lowest risk)
   - Phase 2: New weapon folders (medium complexity)
   - Phase 3: Armor only (focused scope)
   - Phase 4: Shields + Helms (largest but straightforward)

2. **Formula Usage**: All equipment must use BalanceConfig methods
   - Weapons: `CalculateEquipmentAttackBonus(pitLevel, rarity)`
   - Defense: `CalculateEquipmentDefenseBonus(pitLevel, rarity)`
   - This ensures consistency and balance across all equipment

3. **File Organization**: Maintain existing folder structure
   - Existing folders: Swords, Axes, Daggers, Armor, Shields, Helms
   - New folders: Spears, Hammers, Staves
   - Each equipment piece in its own file (per Copilot instructions)

4. **Testing Strategy**: Build verification after each phase
   - Immediate feedback on errors
   - Prevents cascading issues
   - Optional unit tests for comprehensive validation

5. **GearItems.cs Updates**: Incremental per phase
   - Prevents merge conflicts
   - Allows progressive testing
   - Maintains organization (alphabetical within categories)

### Key Findings

1. **Implementation Pattern Established**: ShortSword.cs and LongSword.cs provide clear templates
2. **ItemKind Mapping**: EQUIPMENT_LIBRARY.md specifies exact ItemKind for each equipment subtype
3. **Naming Convention**: File names must be PascalCase with spaces removed, display names preserve original formatting
4. **Element Distribution**: Cave biome focuses on Neutral, Earth, Fire, and Darkness elements
5. **Rarity Progression**: Normal (Pit 1-10), Uncommon (Pit 11-25) for Cave Biome

---

## 16. Risks / Blockers

### Potential Blockers

1. **Missing ItemKind Values**: If ItemKind enum doesn't contain expected values
   - **Mitigation**: Verify enum before Phase 2 implementation

2. **BalanceConfig Method Signatures**: If formulas changed since last implementation
   - **Mitigation**: Test one equipment piece per type before batch creation

3. **GearItems.cs File Size**: Large file may be difficult to edit
   - **Mitigation**: Use multi_replace_string_in_file for efficiency

4. **Build Dependencies**: FNA/Nez not properly initialized
   - **Mitigation**: Verify `dotnet build` works before starting implementation

### Risk Tolerance

- **Low Risk**: Following established patterns, clear specifications
- **Medium Risk**: Large number of files, potential for copy-paste errors
- **High Risk**: None identified

---

## 17. Ready for Next Step

**Status**: ✅ **YES - READY FOR IMPLEMENTATION**

### Prerequisites Met
- ✅ Complete equipment specifications in EQUIPMENT_LIBRARY.md
- ✅ Factory pattern established and documented
- ✅ BalanceConfig formulas available
- ✅ Template files identified
- ✅ Folder structure understood
- ✅ Implementation plan complete and detailed

### What Principal Game Engineer Needs
1. This implementation plan (feature_cave_biome_equipment.md)
2. Access to EQUIPMENT_LIBRARY.md
3. Template files: ShortSword.cs, LongSword.cs, LeatherArmor.cs, etc.
4. Working development environment (FNA/Nez initialized)
5. Ability to run `dotnet build`

### Expected Outcome
- 113 equipment files created across 4 phases
- Complete Cave Biome equipment progression (Pit 1-25)
- All equipment integrated into GearItems.cs
- Project builds successfully
- Equipment ready for loot distribution and gameplay testing

---

**End of Implementation Plan**

*This plan provides comprehensive, actionable guidance for implementing the 113 missing Cave Biome equipment pieces. Follow phases sequentially for best results. Build verification after each phase ensures quality and prevents cascading errors.*
