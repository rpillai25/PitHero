---
name: pit-balance-test
description: "**DOMAIN SKILL** — Testing PitHero balance by traversing the pit on the Virtual Game Layer. USE FOR: validating difficulty curve, monster/equipment interactions, elemental matchups, job performance across pit levels 1–100+; running automated balance tests via VirtualGameSimulation; producing balance reports; recommending rebalance changes; verifying boss-floor scaling and Cave-biome treasure transitions; spotting curve discontinuities. Must ACTUALLY run tests — not just plan them. DO NOT USE FOR: designing new monsters or equipment (use monster-design / equipment-design skills), implementing virtual layer coverage gaps (use virtual-game-layer skill), writing gameplay code."
---

# Pit Balance Testing — PitHero

You test balance by **actually running** the virtual game layer — not by describing what testing would look like. Output is a balance report written to `features/reports/feature_<name>_balance_report.md`.

## Core Constraints

- **No gameplay source-code implementation.** Testing, simulation, and reporting only.
- **You must run tests.** Don't just plan them.
- Creating/updating a balance report under `features/reports/` is allowed.

## When to Run

After monsters and equipment have been **designed AND implemented** for a feature, traverse the pit to verify difficulty curve, elemental interactions, and job performance across pit levels 1–100+.

## What to Read Next (Progressive Disclosure)

| If you are working on… | Read |
|---|---|
| Step-by-step traversal procedure, level-by-level checks, what to log | `references/test-procedure.md` |
| How to invoke `VirtualGameSimulation`, available test entry points, simulation knobs | `references/virtual-game-runner.md` |
| Report structure, sections required, severity levels for issues | `references/report-format.md` |

## Quick Formula Reference

| Component | Formula | Example (L=50) |
|---|---|---|
| Monster HP | `(10 + L * 5) * archetype_mult` | Balanced: 260 |
| Monster Stat | `(1 + L * 2/3) * archetype_mult` | Balanced: 34 |
| Monster XP | `10 + L * 8` | 410 |
| Weapon Attack | `(1 + P / 2) * rarity_mult` | Normal: 26 |
| Armor Defense | `(1 + P / 3) * rarity_mult` | Normal: 17 |
| Accessory Stat | `(P / 5) * rarity_mult` | Normal: 10 |

## Job Stat Benchmarks (Level 99) — Sanity-Check Reference

| Job | STR | AGI | VIT | MAG | HP | MP | Role |
|---|---|---|---|---|---|---|---|
| Knight | 68 | 42 | 78 | 28 | 415 | 94 | Tank |
| Monk | 73 | 62 | 58 | 37 | 315 | 121 | Balanced Fighter |
| Thief | 58 | 82 | 43 | 32 | 240 | 106 | Speed / Evasion |
| Archer | 62 | 72 | 48 | 37 | 265 | 121 | Ranged |
| Mage | 33 | 48 | 33 | 88 | 190 | 274 | Magic DPS |
| Priest | 38 | 53 | 43 | 78 | 240 | 244 | Healer |

## Standard Test Procedure (high-level)

1. **Read** `MONSTER_LIBRARY.md`, `EQUIPMENT_LIBRARY.md`, `JOB_STAT_CURVES.md`, `MONSTER_BALANCE_GUIDE.md`, `EQUIPMENT_BALANCE_GUIDE.md`, `VIRTUAL_GAME_LOGIC_LAYER.md`.
2. **Run** virtual-layer traversal from pit level 1 → 100+ for one or more jobs.
3. **Capture metrics** at sampling points (levels 1, 5, 10, 15, 20, 25, 30, 50, 75, 99) and at every boss floor:
   - HP-after-encounter, damage dealt vs taken, elemental advantage usage, healing-pool consumption
   - XP-to-next-level pacing
   - Equipment drops the hero actually used
4. **Compare** against expected curve in `JOB_STAT_CURVES.md` and the balance guides.
5. **Identify** imbalances: spikes/cliffs in difficulty, jobs that fail certain levels, dead elemental matchups, useless equipment tiers, overpowered Rare drops, etc.
6. **Write** the report. Pass/fail verdict + prioritized rebalance recommendations.

## Unit Tests for Balance

Always also run:

```bash
dotnet test PitHero.Tests/
```

Particularly: `BalanceSystemTests.cs`, `GearItemsTests.cs`, `*JobStatGrowthTests.cs`.

Validate formulas produce smooth progression curves without sudden jumps.

## Output

Write the report to: `features/reports/feature_<name>_balance_report.md`. Required sections:

1. Test scope (which jobs, which level range, which feature triggered the test)
2. Methodology (which virtual-layer scenarios were run)
3. Findings — per level/biome, with severity (Critical / Major / Minor)
4. Elemental matchup matrix (advantage utilized vs available)
5. Equipment usage matrix (drop tier → utilization rate)
6. Overall verdict — Pass / Fail / Pass-with-caveats
7. Prioritized rebalance recommendations (with concrete numbers — proposed multiplier changes, etc.)

## Acceptance Criteria

A balance pass means:
- Every level 1–99 is beatable by every job at that level when played intelligently
- Difficulty curve is smooth — no >2× jumps in encounter time/HP loss between adjacent non-boss levels
- Boss floors feel like bosses (notably harder than the surrounding 4 non-boss floors)
- Elemental advantage is meaningful (>30% damage swing when used correctly)
- No tier of equipment is uniformly skipped (Uncommon should not be strictly worse than Normal at the same level)
