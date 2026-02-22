# Cave Biome Design Reference (Pit Levels 1-25)

## Purpose

This document captures the implemented Cave biome design for pit levels 1-25, including enemy progression, treasure transition behavior, and boss cadence. It is intended as a concise source of truth for tuning, implementation checks, and test validation.

---

## Enemy Progression Tiers and Rationale

### Tier Model

- **Tier 1 (Pit 1-4 regular floors):** 5-entry starter pool for early onboarding and low-complexity encounters.
- **Tier 2 (Pit 6-9 regular floors):** First 10-entry sliding window that mixes Tier 1 carryover with new mid-Cave enemies.
- **Tier 3 (Pit 11-14 regular floors):** 10-entry window shifts toward stronger Deep Cave enemies while preserving roster continuity.
- **Tier 4 (Pit 16-19 regular floors):** 10-entry Ancient Cave window raises threat density and durability expectations.
- **Tier 5 (Pit 21-24 regular floors):** 10-entry Abyssal Cave window stabilizes end-of-biome pressure before final boss.

### Rationale

- Early floors intentionally use a smaller roster to reduce variance while players establish baseline gear.
- Mid and late floors use 10-entry windows to increase encounter diversity without abrupt composition swaps.
- Sliding windows retain some prior-tier enemies so progression feels continuous instead of segmented.

---

## Treasure Rarity Band Transitions

### Band Rules

- **Pit 1-10:** Always treasure level 1.
- **Pit 11-25 non-boss floors:** 35% treasure level 2, 65% treasure level 1.
- **Pit 11-25 boss floors:** 60% treasure level 2, 40% treasure level 1.

### Rarity Band Intent

- Pit 1-10 aligns with **Normal** gear onboarding.
- Pit 11+ introduces **Uncommon** progression opportunities without fully replacing baseline drops.
- Boss floors accelerate progression with a higher level 2 chance while preserving fallback level 1 outcomes.

---

## Boss Floor Placement Logic

Boss floors occur every fifth pit level within Cave:

- 5, 10, 15, 20, 25

Design behavior:

- Regular enemy pools are intentionally empty on boss floors.
- Boss selection/instantiation is handled by boss-specific flow.
- Boss spawn level uses estimated level plus a +2 bonus, then clamps to valid global level bounds.

---

## Cave Progression Table (Levels 1-25)

| Pit Level | Floor Type | Regular Pool Behavior | Scaled Level Rule | Treasure Rule | Rarity Band |
|-----------|------------|-----------------------|-------------------|---------------|-------------|
| 1 | Regular | Pool 1 (5 entries) | EstimatePlayerLevelForPitLevel | Level 1 only | Normal |
| 2 | Regular | Pool 1 (5 entries) | EstimatePlayerLevelForPitLevel | Level 1 only | Normal |
| 3 | Regular | Pool 1 (5 entries) | EstimatePlayerLevelForPitLevel | Level 1 only | Normal |
| 4 | Regular | Pool 1 (5 entries) | EstimatePlayerLevelForPitLevel | Level 1 only | Normal |
| 5 | Boss | Empty regular pool | EstimatePlayerLevelForPitLevel + 2 (clamped) | Level 1 only | Normal |
| 6 | Regular | Pool 2 (10-entry window) | EstimatePlayerLevelForPitLevel | Level 1 only | Normal |
| 7 | Regular | Pool 2 (10-entry window) | EstimatePlayerLevelForPitLevel | Level 1 only | Normal |
| 8 | Regular | Pool 2 (10-entry window) | EstimatePlayerLevelForPitLevel | Level 1 only | Normal |
| 9 | Regular | Pool 2 (10-entry window) | EstimatePlayerLevelForPitLevel | Level 1 only | Normal |
| 10 | Boss | Empty regular pool | EstimatePlayerLevelForPitLevel + 2 (clamped) | Level 1 only | Normal |
| 11 | Regular | Pool 3 (10-entry window) | EstimatePlayerLevelForPitLevel | 35% L2 / 65% L1 | Uncommon |
| 12 | Regular | Pool 3 (10-entry window) | EstimatePlayerLevelForPitLevel | 35% L2 / 65% L1 | Uncommon |
| 13 | Regular | Pool 3 (10-entry window) | EstimatePlayerLevelForPitLevel | 35% L2 / 65% L1 | Uncommon |
| 14 | Regular | Pool 3 (10-entry window) | EstimatePlayerLevelForPitLevel | 35% L2 / 65% L1 | Uncommon |
| 15 | Boss | Empty regular pool | EstimatePlayerLevelForPitLevel + 2 (clamped) | 60% L2 / 40% L1 | Uncommon |
| 16 | Regular | Pool 4 (10-entry window) | EstimatePlayerLevelForPitLevel | 35% L2 / 65% L1 | Uncommon |
| 17 | Regular | Pool 4 (10-entry window) | EstimatePlayerLevelForPitLevel | 35% L2 / 65% L1 | Uncommon |
| 18 | Regular | Pool 4 (10-entry window) | EstimatePlayerLevelForPitLevel | 35% L2 / 65% L1 | Uncommon |
| 19 | Regular | Pool 4 (10-entry window) | EstimatePlayerLevelForPitLevel | 35% L2 / 65% L1 | Uncommon |
| 20 | Boss | Empty regular pool | EstimatePlayerLevelForPitLevel + 2 (clamped) | 60% L2 / 40% L1 | Uncommon |
| 21 | Regular | Pool 5 (10-entry window) | EstimatePlayerLevelForPitLevel | 35% L2 / 65% L1 | Uncommon |
| 22 | Regular | Pool 5 (10-entry window) | EstimatePlayerLevelForPitLevel | 35% L2 / 65% L1 | Uncommon |
| 23 | Regular | Pool 5 (10-entry window) | EstimatePlayerLevelForPitLevel | 35% L2 / 65% L1 | Uncommon |
| 24 | Regular | Pool 5 (10-entry window) | EstimatePlayerLevelForPitLevel | 35% L2 / 65% L1 | Uncommon |
| 25 | Boss | Empty regular pool | EstimatePlayerLevelForPitLevel + 2 (clamped) | 60% L2 / 40% L1 | Uncommon |

---

## Testing Strategy Summary

The current strategy is aligned to cave-focused test coverage in the test project:

- **Configuration tests:** verify cave bounds, boss floor identification, pool mappings, and empty regular pools on 5/10/15/20/25.
- **Scaling tests:** verify enemy level equals estimated level on regular floors, and estimated +2 with clamp behavior on boss floors.
- **Treasure tests:** verify pit 1-10 always returns level 1, non-boss pit 11+ uses 35% level 2 threshold, and boss floors use 60% level 2 threshold.
- **Rarity band tests:** verify Normal band through pit 10 and Uncommon from pit 11 onward.
- **Progression parity tests:** verify level-by-level behavior for 1-25 remains deterministic and stable across refactors.

This summary is intended to stay consistent with implemented tests and cave runtime behavior.
