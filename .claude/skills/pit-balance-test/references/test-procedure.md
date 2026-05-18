# Detailed Test Procedure

## Pre-flight

1. Confirm the relevant monsters and equipment from `MONSTER_LIBRARY.md` / `EQUIPMENT_LIBRARY.md` are **implemented** in the codebase. If they are not, halt and report — testing un-implemented designs is not the job.
2. Run unit tests first:
   ```bash
   dotnet test PitHero.Tests/
   ```
   If anything in `BalanceSystemTests`, `GearItemsTests`, or `*JobStatGrowthTests` fails, fix or report before traversing.

## Traversal Sampling Plan

Cover at minimum these pit levels per job:

| Level | Why |
|---|---|
| 1 | Baseline / first encounter sanity |
| 5 | First small boss |
| 10 | Pre-treasure-transition (Cave) |
| 11 | Post-treasure-transition (Cave) |
| 15 | Mid-Cave |
| 20 | Late Cave, small boss |
| 25 | Cave end / big boss |
| 26 | Biome transition to Forest |
| 50 | Forest end / big boss |
| 51 | Biome transition to Castle |
| 75 | Castle end / big boss |
| 76 | Biome transition to Underworld |
| 99 | Hero max level |
| 100+ | Endless / late-game scaling |

Plus **every boss floor** (multiples of 5).

## Jobs to Test

At minimum cover one job per role:

- **Knight** (Tank) — verifies HP-pool survivability vs DPS encounters
- **Monk** (Balanced) — baseline reference
- **Mage** (Magic DPS) — verifies elemental matchups + MP pacing
- **Priest** (Healer) — verifies healing economy

Time permitting, add Thief and Archer for full coverage.

## Per-Encounter Capture

For each fight, record:

| Metric | Why |
|---|---|
| Pre-fight HP/MP, post-fight HP/MP | Damage taken; healing pressure |
| Damage dealt per round | DPS curve |
| Rounds to kill | Pacing |
| Elemental multiplier applied | Whether elements mattered |
| Healing items / skills used | Consumable economy |
| Death? (Y/N) | Critical failure |

## Per-Level Capture

At the level summary:

| Metric | Why |
|---|---|
| XP gained vs XP-to-next-level | Progression pacing |
| Equipment drops + tier | Loot distribution |
| Equipment swapped this level | Drops actually used |
| Healing-pool % at end of level | Sustainability into next |

## Per-Biome Capture

End-of-biome (levels 25/50/75):

| Metric | Why |
|---|---|
| Hero level vs biome-end pit level | Are levels rising in sync? |
| Treasure-tier distribution observed | Loot progression by tier |
| Average elemental advantage / encounter | Matchup richness |
| Job-specific fail rate | Job viability |

## Identifying Issues

| Symptom | Likely cause | Severity |
|---|---|---|
| Job dies at level N with optimal play | Stat curve too steep for that job at N | Critical |
| >2× HP loss between non-boss levels N and N+1 | Spike in monster stats or new high-damage monster | Major |
| Boss feels indistinguishable from non-boss | Boss +2 level shift not applied or archetype too weak | Major |
| Uncommon drop never replaces Normal | Uncommon multiplier wrong or Normal too strong | Minor |
| Elemental advantage <30% damage swing | Resistance/weakness values too small | Minor |
| Healing items always exhausted by level N | Healing curve doesn't match damage curve | Major |

## Pass / Fail Criteria

**Pass** = all of:
- No deaths with optimal play in level range 1–99
- No >2× difficulty jumps between adjacent non-boss levels
- Boss floors notably harder than the 4 surrounding non-boss floors
- Every elemental advantage produces >30% damage swing when used
- No equipment tier uniformly skipped

**Fail** = critical issue found (job-level death w/ optimal play, broken curve, etc.).

**Pass-with-caveats** = minor issues only; report and recommend follow-up tuning.
