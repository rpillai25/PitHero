# Balance Report Format

Write reports to: `features/reports/feature_<name>_balance_report.md`.

## Required Structure

```markdown
# Balance Report — <feature name>

**Date:** YYYY-MM-DD
**Seed:** <Nez.Random seed used>
**Jobs tested:** Knight, Monk, Mage, Priest (etc.)
**Pit-level range:** 1–100
**Tested-by:** Pit Balance Tester (skill)

---

## 1. Test Scope

- Which feature triggered the test
- Which monsters / equipment / mechanics were exercised
- Which jobs were covered and why

## 2. Methodology

- Virtual-layer entry points used
- Sampling pit levels
- Per-encounter and per-level metrics captured
- Unit-test results (passed / failed counts)

## 3. Findings

Tabulate findings by pit-level range. Each finding has a severity.

### Critical Issues
| Pit Level | Job | Issue | Notes |
|---|---|---|---|
| 25 | Knight | Dies to Cave Big Boss with optimal play | HP curve undershoots boss damage |

### Major Issues
| Pit Level | Job | Issue | Notes |
|---|---|---|---|
| 11→12 | All | 2.3× damage jump | New Cave Drake too high stats |

### Minor Issues
| Pit Level | Job | Issue | Notes |
|---|---|---|---|
| 18 | Mage | Uncommon Staff strictly worse than Normal Staff at L=15 | Multiplier off |

## 4. Elemental Matchup Matrix

| Hero Element | Monster Element | Damage Multiplier Observed | Notes |
|---|---|---|---|
| Fire weapon | Water monster | 2.0× | Working as designed |
| Light weapon | Dark monster | 1.6× | Lower than expected — check resistances |

## 5. Equipment Tier Utilization

| Pit Range | Normal % | Uncommon % | Rare % | Epic % | Legendary % |
|---|---|---|---|---|---|
| 1–25 | 70 | 25 | 5 | 0 | 0 |
| 26–50 | 50 | 30 | 18 | 2 | 0 |
| … | … | … | … | … | … |

## 6. Verdict

**[Pass / Fail / Pass-with-caveats]**

(One-paragraph summary of state of balance for this feature.)

## 7. Prioritized Rebalance Recommendations

| Priority | Change | Affected | Rationale |
|---|---|---|---|
| 1 (Critical) | Reduce Cave Big Boss STR multiplier from 1.5 → 1.3 | Cave Big Boss | Knight cannot survive at L=25 |
| 2 (Major) | Drop Cave Drake stat archetype from MagicUser → Balanced | Cave Drake | Difficulty spike at L=12 |
| 3 (Minor) | Increase Uncommon weapon multiplier 1.2 → 1.25 | All Uncommon weapons | Uncommon tier is being skipped |

---

## Handoff Summary

- Report path: `features/reports/feature_<name>_balance_report.md`
- Verdict: <Pass / Fail / Pass-with-caveats>
- Prioritized recommendations (count): N
```

## Tone & Specificity

- Use concrete numbers. "Knight dies at level 25" beats "Knight feels weak in Cave."
- Tie every issue to a metric captured during traversal.
- Cite which monster/equipment entries in `PitHero/docs/MonsterLibrary.md` / `PitHero/docs/EquipmentLibrary.md` need adjustment.
- For each recommendation, propose a **specific number change** (multiplier from X → Y, stat from N → M). Vague recommendations are not actionable.
