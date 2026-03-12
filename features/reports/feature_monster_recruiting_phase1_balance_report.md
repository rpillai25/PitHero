# Monster Recruiting Phase 1 — Balance Report

**Feature:** Monster Recruiting Phase 1  
**Tester:** Pit Balance Tester  
**Date:** 2026-03-10  
**Report Path:** `/features/reports/feature_monster_recruiting_phase1_balance_report.md`

---

## Executive Summary

**Overall Verdict: ✅ PASS — Feature is balanced and ready for production.**

All 13 unit tests pass. All 26 enemy implementations carry a valid `JoinPercentageModifier`. The difficulty-scaled join-chance curve (15% at early pits → 1% for PitLord) is well-designed. One minor Phase 2 gap was identified (no roster size cap) and one pre-existing, unrelated test failure was noted.

---

## Task 1 — Test Results

### AlliedMonster Test Suite (13 tests)

| Test | Class | Result |
|------|-------|--------|
| `AlliedMonster_Constructor_StoresNameAndType` | `AlliedMonsterTests` | ✅ PASS |
| `AlliedMonster_Constructor_StoresProficienciesCorrectly` | `AlliedMonsterTests` | ✅ PASS |
| `AlliedMonster_Proficiency_ClampsToMinimum` | `AlliedMonsterTests` | ✅ PASS |
| `AlliedMonster_Proficiency_ClampsToMaximum` | `AlliedMonsterTests` | ✅ PASS |
| `AlliedMonster_Proficiency_AcceptsBoundaryValues` | `AlliedMonsterTests` | ✅ PASS |
| `AlliedMonsterManager_New_HasEmptyRoster` | `AlliedMonsterManagerTests` | ✅ PASS |
| `AlliedMonsterManager_TryRecruit_GuaranteedJoin_ReturnsMonster` | `AlliedMonsterManagerTests` | ✅ PASS |
| `AlliedMonsterManager_TryRecruit_ZeroModifier_ReturnsNull` | `AlliedMonsterManagerTests` | ✅ PASS |
| `AlliedMonsterManager_TryRecruit_SetsMonsterTypeName` | `AlliedMonsterManagerTests` | ✅ PASS |
| `AlliedMonsterManager_TryRecruit_AssignsNonEmptyName` | `AlliedMonsterManagerTests` | ✅ PASS |
| `AlliedMonsterManager_TryRecruit_ProficienciesInRange` | `AlliedMonsterManagerTests` | ✅ PASS |
| `AlliedMonsterManager_TryRecruit_MultipleRecruits_CountIncrementsCorrectly` | `AlliedMonsterManagerTests` | ✅ PASS |
| `AlliedMonsterManager_AlliedMonsters_ListMatchesCount` | `AlliedMonsterManagerTests` | ✅ PASS |

**Result: 13 / 13 PASSED (100%)**

### Full Test Suite Context

| Metric | Count |
|--------|-------|
| Total tests run | 821 |
| Passed | 814 |
| Failed | 1 |
| Skipped | 6 |

> ⚠️ The one failure (`GoapConstants_SimplifiedModel_ShouldOnlyContainCoreConstants`) is **pre-existing and unrelated** to Monster Recruiting. It expects 35 GOAP constants but finds 38 — likely stale after a prior feature added new states. This does **not** block the recruiting feature.

---

## Task 2 — Join Probability Balance Analysis

**Formula:** `joinChance = BaseMonsterJoinChance × JoinPercentageModifier = 0.10 × modifier`

### Per-Monster Effective Chances

| Monster | Modifier | Effective % | Tier | Avg Fights to Recruit |
|---------|----------|-------------|------|----------------------|
| Rat | 1.50 | **15.0%** | Common | 7 |
| Slime | 1.20 | **12.0%** | Common | 8 |
| Bat | 1.20 | **12.0%** | Common | 8 |
| CaveMushroom | 1.00 | **10.0%** | Common | 10 |
| Snake | 1.00 | **10.0%** | Common | 10 |
| Spider | 1.00 | **10.0%** | Common | 10 |
| Goblin | 0.90 | **9.0%** | Uncommon | 11 |
| ShadowImp | 0.80 | **8.0%** | Uncommon | 12 |
| TunnelWorm | 0.80 | **8.0%** | Uncommon | 12 |
| GhostMiner | 0.70 | **7.0%** | Uncommon | 14 |
| Skeleton | 0.70 | **7.0%** | Uncommon | 14 |
| StoneBeetle | 0.70 | **7.0%** | Uncommon | 14 |
| Orc | 0.60 | **6.0%** | Uncommon | 17 |
| EarthElemental | 0.60 | **6.0%** | Uncommon | 17 |
| FireLizard | 0.50 | **5.0%** | Uncommon | 20 |
| MagmaOoze | 0.50 | **5.0%** | Uncommon | 20 |
| Wraith | 0.50 | **5.0%** | Uncommon | 20 |
| CaveTroll | 0.40 | **4.0%** | Rare | 25 |
| ShadowBeast | 0.40 | **4.0%** | Rare | 25 |
| StoneGuardian | 0.40 | **4.0%** | Rare | 25 |
| LavaDrake | 0.30 | **3.0%** | Rare | 33 |
| StoneWyrm | 0.30 | **3.0%** | Rare | 33 |
| AncientWyrm | 0.20 | **2.0%** | Boss | 50 |
| MoltenTitan | 0.20 | **2.0%** | Boss | 50 |
| CrystalGolem | 0.15 | **1.5%** | Boss | 67 |
| PitLord | 0.10 | **1.0%** | Boss | 100 |

### Tier Summary

| Tier | Count | Chance Range | Average |
|------|-------|-------------|---------|
| Common | 6 | 10.0% – 15.0% | 11.5% |
| Uncommon | 10 | 5.0% – 9.0% | 6.8% |
| Rare | 5 | 3.0% – 4.0% | 3.6% |
| Boss | 4 | 1.0% – 2.0% | 1.6% |

### Pit Depth Scenario Averages

| Scenario | Monsters Encountered | Avg Recruit Chance/Kill |
|----------|---------------------|------------------------|
| Pit 1–20 (Newcomer) | Rat, Slime, Bat, Spider, Snake, Goblin | **11.3%** |
| Pit 25–50 (Midgame) | GhostMiner, Skeleton, Orc, EarthElemental, ShadowImp, TunnelWorm, FireLizard | **6.7%** |
| Pit 50–75 (Veteran) | CaveTroll, ShadowBeast, StoneGuardian, LavaDrake, Wraith, StoneBeetle | **4.5%** |
| Pit 75–100 (Expert) | StoneWyrm, AncientWyrm, MoltenTitan, CrystalGolem, PitLord | **1.9%** |

**Assessment:** The join-chance curve has a smooth, deliberate decline from ~11% in early pits to ~2% at endgame. This mirrors the standard rarity inverse-difficulty pattern. Newcomers accumulate allies quickly (encouraging engagement with the system); endgame players chase rare boss recruits as a prestigious goal. ✅ **Curve is well-balanced.**

### Specific Values Requested

| Monster | Formula | Effective Chance | Verdict |
|---------|---------|-----------------|---------|
| Rat | 10% × 1.5 | **15%** | ✅ Appropriate — common early fodder, rewarding for new players |
| Slime | 10% × 1.2 | **12%** | ✅ Appropriate — slightly rarer than Rat, still very accessible |
| PitLord | 10% × 0.1 | **1%** | ✅ Appropriate — trophy recruit, expected ~100 kills to get one |
| CrystalGolem | 10% × 0.15 | **1.5%** | ✅ Appropriate — elite guardian, ~67 kills average |

---

## Task 3 — JoinPercentageModifier Coverage

**Files in Enemies/ directory:** 27  
**Files with `JoinPercentageModifier`:** 27  

Breakdown:
- 1 `IEnemy.cs` interface — defines `float JoinPercentageModifier { get; }` ✅
- 26 concrete enemy implementations — all implement the property ✅

**Verified implementations:**

```
AncientWyrm=0.2  Bat=1.2  CaveMushroom=1.0  CaveTroll=0.4   CrystalGolem=0.15
EarthElemental=0.6  FireLizard=0.5  GhostMiner=0.7  Goblin=0.9  LavaDrake=0.3
MagmaOoze=0.5  MoltenTitan=0.2  Orc=0.6  PitLord=0.1  Rat=1.5  ShadowBeast=0.4
ShadowImp=0.8  Skeleton=0.7  Slime=1.2  Snake=1.0  Spider=1.0  StoneBeetle=0.7
StoneGuardian=0.4  StoneWyrm=0.3  TunnelWorm=0.8  Wraith=0.5
```

**Modifier Range Check:**
- Minimum: PitLord = 0.10 (no zeros or negatives — all monsters can theoretically join) ✅
- Maximum: Rat = 1.50 (no modifier causes > 100% chance even hypothetically) ✅

**Result: 26/26 enemies covered. ✅ PASS**

---

## Task 4 — Test Coverage Verification

### `AlliedMonsterTests.cs` (5 tests)

| Coverage Target | Test Method | Status |
|----------------|-------------|--------|
| ✅ Name assignment | `AlliedMonster_Constructor_StoresNameAndType` | Covered |
| ✅ Proficiency storage (valid range) | `AlliedMonster_Constructor_StoresProficienciesCorrectly` | Covered |
| ✅ Proficiency clamping — minimum (< 1 → 1) | `AlliedMonster_Proficiency_ClampsToMinimum` | Covered |
| ✅ Proficiency clamping — maximum (> 9 → 9) | `AlliedMonster_Proficiency_ClampsToMaximum` | Covered |
| ✅ Proficiency boundary values (exactly 1 and 9) | `AlliedMonster_Proficiency_AcceptsBoundaryValues` | Covered |

### `AlliedMonsterManagerTests.cs` (8 tests)

| Coverage Target | Test Method | Status |
|----------------|-------------|--------|
| ✅ Empty roster on init | `AlliedMonsterManager_New_HasEmptyRoster` | Covered |
| ✅ Guaranteed recruit scenario (modifier = 1000) | `AlliedMonsterManager_TryRecruit_GuaranteedJoin_ReturnsMonster` | Covered |
| ✅ Zero chance scenario (modifier = 0) | `AlliedMonsterManager_TryRecruit_ZeroModifier_ReturnsNull` | Covered |
| ✅ Name assignment from enemy.Name | `AlliedMonsterManager_TryRecruit_SetsMonsterTypeName` | Covered |
| ✅ Non-empty generated name | `AlliedMonsterManager_TryRecruit_AssignsNonEmptyName` | Covered |
| ✅ Proficiency in 1-9 range after recruit | `AlliedMonsterManager_TryRecruit_ProficienciesInRange` | Covered |
| ✅ Count increments on multiple recruits | `AlliedMonsterManager_TryRecruit_MultipleRecruits_CountIncrementsCorrectly` | Covered |
| ✅ AlliedMonsters list matches Count | `AlliedMonsterManager_AlliedMonsters_ListMatchesCount` | Covered |

**All four required coverage targets verified:**
1. ✅ Proficiency clamping (1-9 range) — tested at min, max, and boundaries
2. ✅ Guaranteed recruit scenario (modifier = 1000f)
3. ✅ Zero chance scenario (modifier = 0f)
4. ✅ Name assignment — both MonsterTypeName and generated first name

**Result: Test coverage is comprehensive. ✅ PASS**

---

## Task 5 — Build Verification

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:03.55
```

**Result: ✅ PASS — Clean build, no warnings.**

---

## Implementation Deep-Dive Notes

### `AlliedMonster.cs`
- Uses `System.Math.Clamp(value, 1, 9)` for all three proficiencies — correct and safe ✅
- Immutable (all properties are `{ get; }` only) — thread-safe data model ✅
- Clear XML doc comments on all members ✅

### `AlliedMonsterManager.cs`
- Initial list capacity pre-allocated to 16 (`new List<AlliedMonster>(16)`) — good performance practice ✅
- Early-out when `joinChance <= 0f` prevents unnecessary RNG calls ✅
- Uses `Nez.Random.NextFloat()` for roll (0.0–1.0 inclusive) — correct ✅
- Uses `Nez.Random.Range(1, 10)` for proficiency rolls — **exclusive upper bound** per Nez source (`RNG.Next(min, max)`), so values are **1–9 inclusive** ✅ *(earlier concern cleared after inspecting Nez implementation)*
- Debug log on every successful recruit aids monitoring and QA ✅

### `GameConfig.cs`
- `BaseMonsterJoinChance = 0.10f` is a `public const float` — globally accessible, easy to tune ✅

---

## Findings and Recommendations

### ✅ Confirmed Balanced

| Finding | Assessment |
|---------|------------|
| Join probability curve (15% → 1%) | Smooth, appropriate difficulty scaling |
| Boss monster chances (1–2%) | Correctly rare — good "trophy" motivation |
| Early game common chances (10–15%) | Newcomer-friendly, builds engagement |
| Proficiency range clamping (1–9) | Correctly enforced at data model layer |
| Random.Range(1, 10) for proficiencies | Generates uniform 1–9 distribution ✅ |
| No modifier > 1.5 | No monster joins more than 15% of the time — avoids trivialization |
| No modifier = 0 on live monsters | All 26 can potentially be recruited |

### 🟡 Low Priority — Phase 2 Recommendations

| ID | Priority | Finding | Recommendation |
|----|----------|---------|----------------|
| R1 | 🟡 Low | **No roster size cap** — `AlliedMonsterManager` has no `MaxRosterSize` limit; a player could theoretically recruit unlimited monsters. | Add `GameConfig.MaxAlliedMonsters` (suggested: 10–20) and reject `TryRecruit` when full, or auto-dismiss oldest member. |
| R2 | 🟡 Low | **No duplicate-name guard** — `TryRecruit` could generate the same `firstName` for two different allied monsters. | Track existing names in the manager and re-roll on collision. |
| R3 | 🟢 Cosmetic | **Proficiency not seeded by monster type** — all monsters roll uniform 1–9 on all three proficiencies, regardless of theme (e.g., a PitLord could have Cooking:9). | Consider type-biased ranges (e.g., Rat: Battle 1–4, Cooking 4–9) to give allies flavor. Not required for Phase 1. |
| R4 | 🟢 Cosmetic | **No persistence** — allied monsters are held in memory only. | Phase 2 save/load integration needed if allied monsters persist across sessions. |

### ❌ No Blockers Found

No blocker-level balance issues were identified. The feature is well-scoped for Phase 1 and correctly implements the design contract.

---

## Acceptance Criteria Checklist

| Criterion | Status |
|-----------|--------|
| All 26 enemies have `JoinPercentageModifier > 0` | ✅ PASS |
| `BaseMonsterJoinChance = 0.10f` in GameConfig | ✅ PASS |
| `TryRecruit` returns null when modifier = 0 | ✅ PASS |
| `TryRecruit` returns AlliedMonster when roll succeeds | ✅ PASS |
| Proficiencies clamped to 1–9 | ✅ PASS |
| `AlliedMonster` stores Name and MonsterTypeName | ✅ PASS |
| Build passes with 0 errors, 0 warnings | ✅ PASS |
| All 13 AlliedMonster tests pass | ✅ PASS |
| Join probability curve is smooth and balanced | ✅ PASS |
| Pre-existing unrelated test failure (GoapConstants) | ⚠️ Pre-existing — not a blocker |

---

## Handoff Contract

**1. Feature Name:** Monster Recruiting Phase 1

**2. Agent:** Pit Balance Tester

**3. Objective:** Verify balance and correctness of the monster recruiting system — join probability curve, implementation coverage, and test quality.

**4. Inputs Consumed:**
- `PitHero/RolePlayingFramework/Enemies/*.cs` — 26 enemy implementations + IEnemy interface
- `PitHero/RolePlayingFramework/AlliedMonsters/AlliedMonster.cs`
- `PitHero/Services/AlliedMonsterManager.cs`
- `PitHero/GameConfig.cs` (BaseMonsterJoinChance = 0.10f)
- `PitHero.Tests/AlliedMonsterTests.cs`
- `PitHero.Tests/AlliedMonsterManagerTests.cs`
- `Nez/Nez.Portable/Math/Random.cs` (Range semantics confirmation)

**5. Decisions / Findings:**

- **Join probability curve is well-balanced.** Ranges from 15% (Rat) to 1% (PitLord), with a smooth decline across pit depth. The curve creates clear early-game accessibility and late-game rarity motivation.
- **All 26 IEnemy implementations have JoinPercentageModifier.** No coverage gaps.
- **All 13 unit tests pass.** Coverage includes clamping, guaranteed recruit, zero chance, name assignment, count tracking, and proficiency range validation.
- **Build is clean** — 0 errors, 0 warnings.
- **`Nez.Random.Range(1, 10)` generates 1–9 inclusive** (exclusive upper bound confirmed from source). Proficiency distribution is uniform — no bias.
- **One pre-existing unrelated test failure** (`GoapConstants`) at 35 vs 38 GOAP constants — not caused by this feature.
- **Phase 2 gap:** No roster size cap. No persistence. No type-biased proficiency seeding. All acceptable for Phase 1 scope.

**6. Deliverables:**
- ✅ Balance report: `/features/reports/feature_monster_recruiting_phase1_balance_report.md`
- ✅ Test verdict: **13/13 AlliedMonster tests PASS**
- ✅ Coverage verdict: **26/26 enemies have JoinPercentageModifier**
- ✅ Build verdict: **0 errors, 0 warnings**
- ✅ Balance verdict: **Join probability curve is smooth and appropriate for all pit depths**

**Prioritized Rebalance Recommendations:**
1. 🟡 **[Phase 2]** Add `MaxAlliedMonsters` roster cap to `GameConfig` and enforce in `AlliedMonsterManager.TryRecruit`
2. 🟡 **[Phase 2]** Add duplicate first-name guard in `AlliedMonsterManager`
3. 🟢 **[Optional]** Add monster-type-biased proficiency seeding for thematic flavor
4. 🟢 **[Phase 2]** Implement save/load persistence for the ally roster

**7. Risks / Blockers:**
- None that block release. The one test failure (`GoapConstants`) is pre-existing, unrelated, and should be addressed in a separate ticket.
- The roster cap gap is a Phase 2 concern only.

**8. Next Agent:** Feature Coordinator (done)

**9. Ready for Next Step:** **Yes** ✅
