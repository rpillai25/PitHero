# Feature: Cave Biome (Pit Levels 1-25)

**Feature ID**: CAVE_BIOME_001  
**Version**: 1.0  
**Status**: Planning Complete  
**Target Release**: Sprint 1

---

## 1. Feature Name

**Cave Biome - Complete Implementation for Pit Levels 1-25**

---

## 2. Objective

Fully implement and validate the Cave Biome system for pit levels 1-25, including enemy progression, boss encounters, equipment loot distribution, and complete virtual layer parity for automated testing.

### Success Metrics
- All 25 cave levels generate properly with appropriate content
- Boss floors (5/10/15/20/25) spawn correctly with scaled boss enemies
- Equipment loot follows cave-specific rarity bands and progression
- Virtual layer has 100% parity with graphical layer for all cave mechanics
- Comprehensive test coverage validates progression across all 25 levels

---

## 3. Research Summary

### Existing Implementation Status

**COMPLETED Components:**
1. ✅ `CaveBiomeConfig.cs` - Core configuration system
   - Cave level range: 1-25
   - Boss floor identification: 5, 10, 15, 20, 25
   - Enemy pools per level band
   - Scaled enemy level calculation
   - Treasure level determination
   - Equipment rarity bands

2. ✅ `PitGenerator.cs` - Cave-aware enemy generation
   - Detects cave levels using `CaveBiomeConfig.IsCaveLevel()`
   - Spawns scaled enemies using `GetScaledEnemyLevelForPitLevel()`
   - Spawns bosses on boss floors
   - Uses enemy pools from `GetEnemyPoolForLevel()`

3. ✅ `TreasureComponent.cs` - Cave-specific loot generation
   - `DetermineCaveTreasureLevel()` integrated
   - `GenerateCaveItemForTreasureLevel()` implemented
   - Common loot (Level 1): ShortSword, WoodenShield, SquireHelm, LeatherArmor, basic potions
   - Uncommon loot (Level 2): LongSword, IronShield, IronHelm, IronArmor, ProtectRing

4. ✅ Enemy Roster - 10 enemies defined
   - Tier 1 (Levels 1-4): Slime, Bat, Rat
   - Tier 2 (Levels 6-9): Goblin, Spider, Snake
   - Tier 3 (Levels 11-24): Skeleton, Orc, Wraith
   - Boss: PitLord (Levels 5/10/15/20/25)

5. ✅ Equipment Roster - Cave-appropriate items
   - Normal rarity (Pit 1-10): Short Sword, Wooden Shield, Squire Helm, Leather Armor
   - Uncommon rarity (Pit 11-25): Long Sword, Iron Shield, Iron Helm, Iron Armor, Protect Ring
   - Utility: HP/MP/Mix Potions

6. ⚠️ Partial Virtual Layer
   - `VirtualPitGenerator.cs` has boss floor detection
   - `VirtualWorldState.cs` has `AddBossMonster()` method
   - Basic tests exist in `CaveBiomeConfigTests.cs`

**GAPS & TODO:**
1. ❌ Limited test coverage - only basic config tests exist
2. ❌ No comprehensive progression tests for all 25 levels
3. ❌ Virtual layer tests incomplete (only 3 tests exist)
4. ❌ No integration tests for full cave playthrough
5. ❌ No documentation of cave biome design rationale
6. ❌ Equipment selection rules not formalized (hard-coded)

### Enemy Level Scaling Analysis

Based on `BalanceConfig.EstimatePlayerLevelForPitLevel()`:

| Pit Level | Player Level (Est.) | Boss Level | Enemy Pool |
|-----------|---------------------|------------|------------|
| 1-4       | 1-6                 | N/A        | Slime/Bat/Rat |
| 5         | 7-8                 | 9 (boss+2) | PitLord Boss |
| 6-9       | 9-13                | N/A        | Goblin/Spider/Snake + Tier1 |
| 10        | 15                  | 17 (boss+2)| PitLord Boss |
| 11-14     | 16-21               | N/A        | Skeleton/Orc/Wraith + Tier2 |
| 15        | 22-23               | 25 (boss+2)| PitLord Boss |
| 16-19     | 24-28               | N/A        | Rotating Tier2+Tier3 |
| 20        | 30                  | 32 (boss+2)| PitLord Boss |
| 21-24     | 31-36               | N/A        | Rotating Tier2+Tier3 |
| 25        | 37-38               | 40 (boss+2)| PitLord Boss (Big Boss) |

### Equipment Progression Analysis

**Treasure Level Distribution:**
- **Pit 1-10**: 100% Level 1 (Normal rarity)
- **Pit 11-14** (non-boss): 65% Level 1, 35% Level 2
- **Pit 15-25** (boss): 60% Level 2, 40% Level 1

**Item Selection:**
- Level 1 (Common): 4 gear items + 3 potion types = 7 options
- Level 2 (Uncommon): 5 gear items + 1 utility bag = 6 options

---

## 4. Scope

### In Scope
1. **Configuration Validation**
   - Verify all 25 levels map to correct enemy pools
   - Validate boss floor logic (5/10/15/20/25)
   - Confirm enemy level scaling follows balance formulas
   - Test treasure level distribution curves

2. **Enemy Generation**
   - Ensure PitGenerator correctly spawns cave enemies
   - Verify boss spawn logic on boss floors
   - Validate enemy level calculation with balance config
   - Test enemy pool sliding window (10-enemy window)

3. **Equipment Generation**
   - Validate treasure level probabilities
   - Test cave loot generation for all treasure levels
   - Verify rarity band transitions (Normal → Uncommon at Pit 11)
   - Ensure potion vs gear distribution is balanced

4. **Virtual Layer Parity**
   - Implement complete cave biome support in VirtualPitGenerator
   - Add boss detection and tracking to VirtualWorldState
   - Create comprehensive virtual layer tests for all 25 levels
   - Validate virtual layer matches graphical layer behavior

5. **Testing Infrastructure**
   - Expand CaveBiomeConfigTests with full progression tests
   - Add integration tests for complete cave playthrough
   - Create balance validation tests for enemy/equipment scaling
   - Test edge cases (level boundaries, boss floors, transitions)

6. **Documentation**
   - Document cave biome design decisions
   - Create equipment selection reference guide
   - Update MONSTER_BALANCE_GUIDE with cave-specific notes
   - Document testing strategy and coverage

### Out of Scope
1. Visual assets for cave biome (using existing sprites)
2. Cave-specific sound effects or music
3. New enemy types beyond existing 10 enemies
4. New equipment beyond existing cave-appropriate items
5. Cave-specific environmental hazards or mechanics
6. Narrative or story elements for cave biome
7. Future biomes (Forest/Castle/Underworld)
8. Difficulty tuning or balance adjustments (use existing formulas)
9. UI changes for biome indication
10. Achievement or progression tracking for cave completion

---

## 5. Constraints & Standards

### Technical Constraints
1. **Existing Balance System**: Must use `BalanceConfig` formulas without modification
2. **StatConstants Caps**: All stats must respect hard caps (HP: 9999, MP: 999, Stats: 99)
3. **AOT Compliance**: No dynamic code generation, pre-allocate collections, use `for` loops
4. **Nez Framework**: Must work within Nez ECS architecture
5. **Single Biome Focus**: Only Cave (1-25), no cross-biome dependencies
6. **Existing Enemies**: Reuse 10 existing enemy implementations
7. **Existing Equipment**: Reuse existing gear items (no new items needed)

### Code Standards
1. Use `Nez.Random` for all randomization
2. Component per file (except small structs)
3. XML summary comments on all public methods
4. Pass `WorldState` struct by reference (`ref` keyword)
5. No reflection usage
6. All combat must use `EnhancedAttackResolver`
7. Follow existing naming conventions

### Test Standards
1. All tests must pass `dotnet test PitHero.Tests/`
2. MSTest framework for all test classes
3. Test names follow pattern: `Component_Method_ExpectedBehavior`
4. Test all boundary conditions (level 1, 25, transitions)
5. Virtual layer must have 100% parity with graphical layer
6. Use `Assert` methods from MSTest

### Documentation Standards
1. Update MONSTER_BALANCE_GUIDE.md if needed
2. Update EQUIPMENT_BALANCE_GUIDE.md if needed
3. Add cave biome notes to relevant guides
4. Document all design decisions in feature file
5. Keep copilot-instructions.md in sync

---

## 6. Implementation Phases

### Phase 1: Core Configuration & Validation
**Objective**: Ensure CaveBiomeConfig is complete and correct

**Task 1.1: Validate Enemy Pool Mappings**
- **Files**: `PitHero.Tests/CaveBiomeConfigTests.cs`
- **Subtasks**:
  - Add test: `CaveBiome_EnemyPools_MatchExpectedProgressionPattern()`
    - Verify levels 1-4 have Slime/Bat/Rat
    - Verify levels 6-9 have both Tier1 + Tier2 (6 enemies)
    - Verify levels 11-14 have Tier2 + Tier3 (6 enemies)
    - Verify levels 16-19 have Tier2 + Tier3 (6 enemies)
    - Verify levels 21-24 have Tier2 + Tier3 (6 enemies)
    - Verify boss floors (5/10/15/20/25) have empty pools
  - Add test: `CaveBiome_EnemyPoolWindow_MaintainsTenEnemySlot()`
    - Verify sliding window behavior (previous 5 levels + current 5 levels)
    - Each non-boss level band should have exactly 3 or 6 enemy types available

**Task 1.2: Validate Enemy Level Scaling**
- **Files**: `PitHero.Tests/CaveBiomeConfigTests.cs`
- **Subtasks**:
  - Add test: `CaveBiome_ScaledLevels_FollowBalanceFormula()`
    - For each pit level 1-25, verify `GetScaledEnemyLevelForPitLevel()` matches `BalanceConfig.EstimatePlayerLevelForPitLevel()`
    - Verify boss floors add +2 level bonus
    - Verify all levels stay within 1-99 range (StatConstants.ClampLevel)
  - Add test: `CaveBiome_BossLevelProgression_MatchesExpectedCurve()`
    - Pit 5: Boss level 9
    - Pit 10: Boss level 17
    - Pit 15: Boss level 25
    - Pit 20: Boss level 32
    - Pit 25: Boss level 40

**Task 1.3: Validate Treasure Level Distribution**
- **Files**: `PitHero.Tests/CaveBiomeConfigTests.cs`
- **Subtasks**:
  - Add test: `CaveBiome_TreasureLevels_Pit1To10_OnlyLevel1()`
    - Test all rolls (0.0, 0.5, 0.99) for pit levels 1-10 always return 1
  - Add test: `CaveBiome_TreasureLevels_Pit11To25_CorrectDistribution()`
    - Non-boss floors (11-14, 16-19, 21-24): 65% Level1, 35% Level2
    - Boss floors (15, 20, 25): 60% Level2, 40% Level1
  - Add test: `CaveBiome_TreasureLevels_NeverExceedTwo()`
    - Validate all cave treasure levels are 1 or 2 only

**Task 1.4: Validate Rarity Band Logic**
- **Files**: `PitHero.Tests/CaveBiomeConfigTests.cs`
- **Subtasks**:
  - Add test: `CaveBiome_RarityBand_Pit1To10_Normal()`
  - Add test: `CaveBiome_RarityBand_Pit11To25_Uncommon()`

**Deliverables**:
- 8-10 new test methods in `CaveBiomeConfigTests.cs`
- All tests passing with 100% coverage of `CaveBiomeConfig.cs`

---

### Phase 2: Equipment Generation Testing
**Objective**: Validate treasure system generates correct cave loot

**Task 2.1: Test Cave Loot Generation**
- **Files**: `PitHero.Tests/TreasureComponentTests.cs` (NEW FILE)
- **Subtasks**:
  - Add test: `CaveTreasure_Level1_GeneratesCommonLoot()`
    - Run 100 iterations of `GenerateCaveItemForTreasureLevel(1)`
    - Verify all items are from common pool: ShortSword, WoodenShield, SquireHelm, LeatherArmor, HP/MP/Mix Potions
    - Verify no uncommon items appear
  - Add test: `CaveTreasure_Level2_GeneratesUncommonLoot()`
    - Run 100 iterations of `GenerateCaveItemForTreasureLevel(2)`
    - Verify all items are from uncommon pool: LongSword, IronShield, IronHelm, IronArmor, ProtectRing
    - Verify no common-only items appear
  - Add test: `CaveTreasure_Distribution_CoversAllItems()`
    - Run 1000 iterations for each treasure level
    - Verify all items in pool appear at least once

**Task 2.2: Test Pit Level Integration**
- **Files**: `PitHero.Tests/TreasureComponentTests.cs`
- **Subtasks**:
  - Add test: `CaveTreasure_InitializeForPitLevel_Pit1To10_OnlyCommon()`
    - For each pit level 1-10, initialize treasure 20 times
    - Verify all generated items are common (Level 1)
  - Add test: `CaveTreasure_InitializeForPitLevel_Pit11To25_MixedRarity()`
    - For pit levels 11-25, initialize treasure 100 times
    - Track distribution of Level 1 vs Level 2
    - Verify distribution roughly matches expected probabilities (±10% tolerance)
  - Add test: `CaveTreasure_InitializeForPitLevel_BossFloors_HigherUncommonRate()`
    - For boss floors (15, 20, 25), verify Level 2 appears at ~60% rate

**Deliverables**:
- New test file: `PitHero.Tests/TreasureComponentTests.cs`
- 5-6 test methods validating treasure generation
- Coverage of all cave treasure logic paths

---

### Phase 3: Enemy Generation Testing
**Objective**: Validate PitGenerator creates correct enemies for cave levels

**Task 3.1: Test Enemy Pool Selection**
- **Files**: `PitHero.Tests/PitGeneratorTests.cs` (NEW FILE)
- **Subtasks**:
  - Add test: `CaveEnemies_Pit1To4_OnlyTier1()`
    - Generate 50 enemies for each of pit levels 1-4
    - Verify only Slime, Bat, Rat appear
  - Add test: `CaveEnemies_Pit6To9_Tier1AndTier2()`
    - Generate 50 enemies for levels 6-9
    - Verify Slime, Bat, Rat, Goblin, Spider, Snake appear
  - Add test: `CaveEnemies_Pit11Plus_IncludesTier3()`
    - Generate 50 enemies for levels 11-24 (non-boss)
    - Verify Skeleton, Orc, Wraith appear in pools

**Task 3.2: Test Boss Spawn Logic**
- **Files**: `PitHero.Tests/PitGeneratorTests.cs`
- **Subtasks**:
  - Add test: `CaveEnemies_BossFloors_SpawnPitLord()`
    - Verify boss floors (5, 10, 15, 20, 25) spawn PitLord
    - Verify boss level matches expected values (9, 17, 25, 32, 40)
  - Add test: `CaveEnemies_NonBossFloors_NoPitLord()`
    - Verify non-boss floors never spawn PitLord from pool

**Task 3.3: Test Enemy Level Scaling**
- **Files**: `PitHero.Tests/PitGeneratorTests.cs`
- **Subtasks**:
  - Add test: `CaveEnemies_LevelScaling_FollowsBalanceConfig()`
    - For each cave level, generate 10 enemies
    - Verify enemy level matches `CaveBiomeConfig.GetScaledEnemyLevelForPitLevel()`

**Deliverables**:
- New test file: `PitHero.Tests/PitGeneratorTests.cs`
- 5-6 test methods validating enemy generation
- Coverage of all cave enemy spawn logic

---

### Phase 4: Virtual Layer Parity
**Objective**: Ensure virtual layer has 100% parity with graphical layer

**Task 4.1: Expand Virtual Pit Generator Tests**
- **Files**: `PitHero.Tests/CaveBiomeConfigTests.cs`
- **Subtasks**:
  - Add test: `VirtualPit_AllCaveLevels_GenerateCorrectly()`
    - For each pit level 1-25:
      - Regenerate virtual pit
      - Verify monsters exist (count > 0)
      - Verify treasures exist (count > 0)
      - Verify fog of war exists
  - Add test: `VirtualPit_BossFloors_BossMarkerPresent()`
    - For each boss floor (5, 10, 15, 20, 25):
      - Regenerate virtual pit
      - Verify `LastGeneratedBossMonsterCount == 1`
      - Verify total monster count >= 1
  - Add test: `VirtualPit_NonBossFloors_NoBossMarker()`
    - For each non-boss floor (sample: 1, 6, 11, 16, 21):
      - Regenerate virtual pit
      - Verify `LastGeneratedBossMonsterCount == 0`

**Task 4.2: Validate Virtual World State Tracking**
- **Files**: `PitHero.Tests/VirtualWorldStateTests.cs` (NEW FILE)
- **Subtasks**:
  - Add test: `VirtualWorld_CavePit_TracksAllEntities()`
    - Generate pit at level 10
    - Verify entity counts match expected ranges
    - Verify boss marker tracked correctly
  - Add test: `VirtualWorld_CaveTreasure_TracksTreasureLevels()`
    - Generate pit at various cave levels
    - Verify `LastGeneratedTreasureLevels` contains only 1 or 2
  - Add test: `VirtualWorld_CaveBounds_MatchDynamicWidth()`
    - Verify pit bounds expand correctly for cave levels

**Task 4.3: Integration Test - Full Cave Playthrough**
- **Files**: `PitHero.Tests/CaveProgressionIntegrationTests.cs` (NEW FILE)
- **Subtasks**:
  - Add test: `CaveProgression_CompletePlaythrough_Levels1To25()`
    - Simulate hero progressing through all 25 cave levels
    - For each level:
      - Regenerate pit
      - Verify boss floors have boss
      - Verify treasure levels match cave bands
      - Verify hero can explore and find wizard orb
    - Track level-up progression and equipment improvements
  - Add test: `CaveProgression_BossEncounters_AppearAtCorrectLevels()`
    - Verify 5 boss encounters occur at exactly levels 5, 10, 15, 20, 25
  - Add test: `CaveProgression_EquipmentGraduation_NormalToUncommon()`
    - Track equipment drops across levels 1-25
    - Verify transition from Normal (1-10) to Uncommon (11-25) rarity

**Deliverables**:
- Enhanced `CaveBiomeConfigTests.cs` with virtual pit tests
- New test file: `VirtualWorldStateTests.cs`
- New test file: `CaveProgressionIntegrationTests.cs`
- Total: 8-10 new test methods for virtual layer

---

### Phase 5: Edge Cases & Boundary Testing
**Objective**: Validate behavior at level boundaries and special cases

**Task 5.1: Boundary Condition Tests**
- **Files**: `PitHero.Tests/CaveBiomeEdgeCaseTests.cs` (NEW FILE)
- **Subtasks**:
  - Add test: `CaveBiome_Level0_NotCave()`
  - Add test: `CaveBiome_Level1_IsCave_FirstLevel()`
  - Add test: `CaveBiome_Level25_IsCave_LastLevel()`
  - Add test: `CaveBiome_Level26_NotCave()`
  - Add test: `CaveBiome_Transitions_Level10To11_RarityChange()`
    - Verify treasure drops change from 100% Level1 to mixed Level1/Level2
  - Add test: `CaveBiome_Transitions_Level4To5_BossFloor()`
    - Verify enemy pool changes from Tier1 to Boss
  - Add test: `CaveBiome_Transitions_Level5To6_NonBossFloor()`
    - Verify enemy pool changes from Boss to Tier1+Tier2

**Task 5.2: Error Handling Tests**
- **Files**: `PitHero.Tests/CaveBiomeEdgeCaseTests.cs`
- **Subtasks**:
  - Add test: `CaveBiome_GetEnemyPool_LevelOutOfRange_ReturnsEmpty()`
  - Add test: `CaveBiome_IsBossFloor_LevelOutOfRange_ReturnsFalse()`
  - Add test: `CaveBiome_ScaledLevel_AlwaysClamped_1To99()`

**Deliverables**:
- New test file: `CaveBiomeEdgeCaseTests.cs`
- 9-10 test methods covering edge cases

---

### Phase 6: Documentation & Finalization
**Objective**: Complete all documentation and ensure maintainability

**Task 6.1: Code Documentation**
- **Files**: `PitHero/Config/CaveBiomeConfig.cs`
- **Subtasks**:
  - Add XML summary to `CreateCaveEnemyPools()` explaining pool design
  - Add inline comments explaining sliding window behavior
  - Document boss level calculation rationale

**Task 6.2: Feature Documentation**
- **Files**: `features/CAVE_BIOME_DESIGN.md` (NEW FILE)
- **Subtasks**:
  - Document enemy progression tiers and rationale
  - Explain treasure rarity band transitions
  - Document boss floor placement logic
  - Add level-by-level breakdown table
  - Include testing strategy summary

**Task 6.3: Update Balance Guides**
- **Files**: `MONSTER_BALANCE_GUIDE.md`, `EQUIPMENT_BALANCE_GUIDE.md`
- **Subtasks**:
  - Add cave biome section to MONSTER_BALANCE_GUIDE
    - Reference enemy tiers and progression
    - Document boss level scaling
  - Add cave biome section to EQUIPMENT_BALANCE_GUIDE
    - Document treasure level probabilities
    - Explain rarity band transitions

**Task 6.4: Update Copilot Instructions**
- **Files**: `.github/copilot-instructions.md`
- **Subtasks**:
  - Add cave biome reference to Architecture Guidelines
  - Document `CaveBiomeConfig` usage patterns
  - Add cave-specific testing examples

**Deliverables**:
- New file: `features/CAVE_BIOME_DESIGN.md`
- Updated: `MONSTER_BALANCE_GUIDE.md`
- Updated: `EQUIPMENT_BALANCE_GUIDE.md`
- Updated: `.github/copilot-instructions.md`

---

## 7. Test Strategy

### Test Coverage Goals
- **Configuration**: 100% coverage of `CaveBiomeConfig.cs`
- **Integration**: All 25 levels tested individually
- **Virtual Layer**: 100% parity with graphical layer
- **Edge Cases**: All boundary conditions tested
- **Progression**: Full playthrough simulation tested

### Test Categories

**Unit Tests** (25-30 tests)
- `CaveBiomeConfigTests.cs` (expanded): 12-15 tests
- `TreasureComponentTests.cs` (new): 5-6 tests
- `PitGeneratorTests.cs` (new): 5-6 tests
- `CaveBiomeEdgeCaseTests.cs` (new): 9-10 tests

**Integration Tests** (10-12 tests)
- `VirtualWorldStateTests.cs` (new): 2-3 tests
- `CaveProgressionIntegrationTests.cs` (new): 3-4 tests
- `CaveBiomeConfigTests.cs` (virtual layer): 5 tests

**Total Test Count**: 35-42 new/updated tests

### Test Execution
```bash
# Run all cave biome tests
dotnet test PitHero.Tests/ --filter CaveBiome

# Run specific test files
dotnet test PitHero.Tests/ --filter CaveBiomeConfigTests
dotnet test PitHero.Tests/ --filter TreasureComponentTests
dotnet test PitHero.Tests/ --filter PitGeneratorTests
dotnet test PitHero.Tests/ --filter CaveProgressionIntegrationTests

# Run full test suite
dotnet test PitHero.Tests/
```

### Manual Testing Checklist
- [ ] Launch game and jump into pit levels 1-25
- [ ] Verify visual spawning of enemies matches cave pools
- [ ] Verify boss appears on floors 5, 10, 15, 20, 25
- [ ] Verify treasure chests contain cave-appropriate loot
- [ ] Verify equipment upgrades from Normal to Uncommon at level 11
- [ ] Verify wizard orb progression through all 25 levels
- [ ] Verify fog of war clears normally
- [ ] Verify no exceptions or errors in console

---

## 8. Acceptance Criteria

### Must Have (P0)
- [x] `CaveBiomeConfig.cs` exists with all methods implemented
- [ ] All 25 cave levels have correct enemy pools defined
- [ ] Boss floors (5/10/15/20/25) correctly spawn PitLord boss
- [ ] Enemy level scaling follows `BalanceConfig.EstimatePlayerLevelForPitLevel()`
- [ ] Treasure levels are 1 (Normal) or 2 (Uncommon) only in cave
- [ ] Pit 1-10 drops only Level 1 (100%)
- [ ] Pit 11-25 drops Level 1/2 with correct probability distribution
- [ ] `TreasureComponent` generates cave-specific loot
- [ ] Virtual layer has boss floor detection and tracking
- [ ] All existing tests pass: `dotnet test PitHero.Tests/`
- [ ] At least 30 new tests added for cave biome coverage
- [ ] Manual playthrough of levels 1-25 completes without errors

### Should Have (P1)
- [ ] 35+ comprehensive tests covering all cave mechanics
- [ ] `CaveProgressionIntegrationTests.cs` validates full playthrough
- [ ] Virtual layer has 100% parity for all 25 levels
- [ ] Edge case tests cover all boundary conditions
- [ ] Documentation file `CAVE_BIOME_DESIGN.md` created
- [ ] `MONSTER_BALANCE_GUIDE.md` updated with cave sections
- [ ] `EQUIPMENT_BALANCE_GUIDE.md` updated with cave sections

### Nice to Have (P2)
- [ ] 40+ tests with extensive edge case coverage
- [ ] Performance benchmarks for pit generation at each level
- [ ] Automated visual regression tests (if tooling available)
- [ ] Balance validation reports comparing expected vs actual drop rates
- [ ] Developer guide for adding new biomes based on cave pattern

---

## 9. Risks/Dependencies

### Risks

**High Risk**
1. **Virtual Layer Parity Gaps**
   - **Risk**: Virtual layer may not fully replicate graphical layer behavior
   - **Mitigation**: Side-by-side testing, comprehensive integration tests
   - **Owner**: Test Phase 4

2. **Balance Formula Changes**
   - **Risk**: BalanceConfig formulas might change, breaking cave scaling
   - **Mitigation**: Use interface abstraction, keep cave logic decoupled
   - **Owner**: Phase 1 validation

**Medium Risk**
3. **Test Coverage Gaps**
   - **Risk**: Edge cases or rare scenarios not tested
   - **Mitigation**: Dedicated edge case testing phase (Phase 5)
   - **Owner**: Test strategy review

4. **Equipment Pool Exhaustion**
   - **Risk**: Limited equipment variety may feel repetitive
   - **Mitigation**: Document as known limitation, future biome will add variety
   - **Owner**: Documentation (out of scope for item creation)

**Low Risk**
5. **Performance at Higher Levels**
   - **Risk**: Pit generation may slow down with larger pits
   - **Mitigation**: Existing optimization patterns should handle this
   - **Owner**: Validation during integration testing

### Dependencies

**Internal Dependencies**
1. `BalanceConfig.cs` - Must remain stable (no changes planned)
2. `StatConstants.cs` - Caps must remain consistent
3. Existing enemy implementations (Slime, Bat, Rat, etc.) - Already complete
4. Existing equipment items - Already complete
5. `PitGenerator.cs` - Core generation logic intact
6. `TreasureComponent.cs` - Loot generation logic intact
7. Virtual layer infrastructure (`VirtualWorldState`, `VirtualPitGenerator`) - Exists

**External Dependencies**
- None (fully self-contained feature)

**Blocking Issues**
- None identified

---

## 10. Downstream Handoff Summary

### For Feature Builder Agent

**Ready for Implementation**: ✅ YES

**Implementation Order**:
1. Start with **Phase 1** (Configuration validation tests)
2. Then **Phase 2** (Equipment generation tests)
3. Then **Phase 3** (Enemy generation tests)
4. Then **Phase 4** (Virtual layer parity)
5. Then **Phase 5** (Edge cases)
6. Finally **Phase 6** (Documentation)

**Key Files to Modify**:
- `PitHero.Tests/CaveBiomeConfigTests.cs` (expand)
- `PitHero.Tests/TreasureComponentTests.cs` (new)
- `PitHero.Tests/PitGeneratorTests.cs` (new)
- `PitHero.Tests/VirtualWorldStateTests.cs` (new)
- `PitHero.Tests/CaveProgressionIntegrationTests.cs` (new)
- `PitHero.Tests/CaveBiomeEdgeCaseTests.cs` (new)
- `features/CAVE_BIOME_DESIGN.md` (new)
- `MONSTER_BALANCE_GUIDE.md` (update)
- `EQUIPMENT_BALANCE_GUIDE.md` (update)

**No Code Changes Required** (Only Testing & Documentation):
- `CaveBiomeConfig.cs` is complete ✅
- `PitGenerator.cs` integration is complete ✅
- `TreasureComponent.cs` integration is complete ✅
- Enemy implementations are complete ✅
- Equipment implementations are complete ✅

**Expected Effort**:
- Phase 1: 2-3 hours (config tests)
- Phase 2: 2-3 hours (equipment tests)
- Phase 3: 2-3 hours (enemy tests)
- Phase 4: 3-4 hours (virtual layer tests)
- Phase 5: 2-3 hours (edge case tests)
- Phase 6: 2-3 hours (documentation)
- **Total**: ~15-20 hours

**Success Validation**:
```bash
# All tests must pass
dotnet test PitHero.Tests/

# Cave-specific tests must pass
dotnet test PitHero.Tests/ --filter CaveBiome

# Visual validation (manual)
# - Launch game
# - Test pit levels 1, 5, 10, 15, 20, 25
# - Verify boss spawns and loot drops
```

---

## Appendix A: Level-by-Level Reference

| Pit Level | Enemy Pool | Boss? | Enemy Level | Treasure Level | Rarity Band |
|-----------|------------|-------|-------------|----------------|-------------|
| 1 | Slime/Bat/Rat | No | 1-2 | 1 (100%) | Normal |
| 2 | Slime/Bat/Rat | No | 3 | 1 (100%) | Normal |
| 3 | Slime/Bat/Rat | No | 4-5 | 1 (100%) | Normal |
| 4 | Slime/Bat/Rat | No | 6 | 1 (100%) | Normal |
| 5 | PitLord | **YES** | **9** | 1 (100%) | Normal |
| 6 | Tier1+Tier2 (6) | No | 9 | 1 (100%) | Normal |
| 7 | Tier1+Tier2 (6) | No | 10-11 | 1 (100%) | Normal |
| 8 | Tier1+Tier2 (6) | No | 12 | 1 (100%) | Normal |
| 9 | Tier1+Tier2 (6) | No | 13-14 | 1 (100%) | Normal |
| 10 | PitLord | **YES** | **17** | 1 (100%) | Normal |
| 11 | Tier2+Tier3 (6) | No | 16 | 1 (65%) / 2 (35%) | Uncommon |
| 12 | Tier2+Tier3 (6) | No | 18 | 1 (65%) / 2 (35%) | Uncommon |
| 13 | Tier2+Tier3 (6) | No | 19-20 | 1 (65%) / 2 (35%) | Uncommon |
| 14 | Tier2+Tier3 (6) | No | 21 | 1 (65%) / 2 (35%) | Uncommon |
| 15 | PitLord | **YES** | **25** | 1 (40%) / 2 (60%) | Uncommon |
| 16 | Tier2+Tier3 (6) | No | 24 | 1 (65%) / 2 (35%) | Uncommon |
| 17 | Tier2+Tier3 (6) | No | 25-26 | 1 (65%) / 2 (35%) | Uncommon |
| 18 | Tier2+Tier3 (6) | No | 27 | 1 (65%) / 2 (35%) | Uncommon |
| 19 | Tier2+Tier3 (6) | No | 28-29 | 1 (65%) / 2 (35%) | Uncommon |
| 20 | PitLord | **YES** | **32** | 1 (40%) / 2 (60%) | Uncommon |
| 21 | Tier2+Tier3 (6) | No | 31 | 1 (65%) / 2 (35%) | Uncommon |
| 22 | Tier2+Tier3 (6) | No | 33 | 1 (65%) / 2 (35%) | Uncommon |
| 23 | Tier2+Tier3 (6) | No | 34-35 | 1 (65%) / 2 (35%) | Uncommon |
| 24 | Tier2+Tier3 (6) | No | 36 | 1 (65%) / 2 (35%) | Uncommon |
| 25 | PitLord | **YES** | **40** | 1 (40%) / 2 (60%) | Uncommon |

**Notes**:
- Enemy levels are approximate based on `BalanceConfig.EstimatePlayerLevelForPitLevel()`
- Tier1 = Slime/Bat/Rat
- Tier2 = Goblin/Spider/Snake
- Tier3 = Skeleton/Orc/Wraith
- Boss levels include +2 bonus
- Pit 25 is the final cave level (big boss)

---

**END OF FEATURE PLAN**
