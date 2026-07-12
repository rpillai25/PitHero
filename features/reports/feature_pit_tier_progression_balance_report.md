# Balance Report — Pit Tier Progression (issue #291)

**Date:** 2026-07-12
**Seeds:** 12345 (primary), 777, 424242 (natural-pacing probes)
**Jobs tested:** Knight (solo + Knight/Priest/Archer party)
**Pit-level range:** depths 1–50 (tier 1 pits 1–25, tier 2 pits 1(2)–25(2))
**Tested-by:** Pit Balance Tester (skill)

---

## 1. Test Scope

Issue #291 changed three things that this report validates:

1. **Enemy constructors honor requested levels** — 9 previously-frozen types (Bat, Goblin, Orc, PitLord, Rat, Skeleton, Snake, Spider, Wraith) now spawn at the floor's scaled level instead of preset L1–L10. This restores intended difficulty at pits 7–24 and the pit-20 boss.
2. **Pit tier system** — after pit 25 the pit loops to 1 and the tier increments (permanent). Monster levels follow the continuous-depth curve `EstimatePlayerLevelForPitLevel((tier−1)×25 + pitLevel)`; gear drops at tier ≥ 2 get scaled stats and a `+N` name suffix; tavern mercenaries get a `TierBaseLevel` floor at tier ≥ 2; respawned heroes start at `TierBaseLevel`.
3. **Tier-2 loot** — chests at tier 2 use the cave gear pools (previously: potions only past pit 25).

## 2. Methodology

- Entry points: `VirtualGameSimulation.RunPitLevel(depth)` (fresh level-appropriate hero per floor) and `RunLevelRange(from, to)` (persistent run, natural XP, gold economy, auto-hire + inn rest). Fully-equipped convention throughout: JP-loaded crystal + full skill kit, mercs with `LearnAllJobSkills()`, 5 HP potions.
- Sampled floors: tier 1 {1, 5, 10, 15, 20, 25}, tier 2 depths {26, 30, 40, 45, 50} (displayed 1(2), 5(2), 15(2), 20(2), 25(2)).
- Persistent runs: depths 1–50 from a level-1 Knight (seeds 12345, 777, 424242); crossing probe depths 20–30 from a curve-level (L36) Knight with 5000g.
- Respawn scenario: crossing sim records `TierBaseLevel` (46); fresh party (Knight+Priest+Archer, all L46) re-runs depth 26.
- New metrics columns used: `pitTier`, `displayedLevel`, `heroLevel` (hero level at end of each floor).
- Unit tests: full suite 1367 passed / **12 failed = exactly the pre-existing baseline** (no regressions). BalanceTraversal: 6/6 pass. New coverage: monster level within ±2 of curve at 7 (pit, tier) samples; tier-2 boss identity; tier-2 chest gear; save v13 round-trip + migration; merc floor distribution.

## 3. Findings

### Critical Issues

None.

### Major Issues

| Pit Level | Job | Issue | Notes |
|---|---|---|---|
| 16–23 (tier 1) | All (natural pacing) | Greedy single-pass descent wipes at pit 23 in 2/3 seeds (depth 33 in the third) | `heroLevel` column shows the gap: natural XP pace reaches L11–13 by pit 23 while monsters follow the curve at L41. Level-appropriate parties clear these floors comfortably (sampled runs), so floors are beatable as designed — but a hero descending every floor without farming falls ~30 levels behind by pit 23. Pre-#291 this was masked because half the spawns were frozen at L6. This is an XP-pacing characteristic of the grind loop, not a tier-system defect — see recommendation 1. |
| 22→23 (tier 1) | All | hpLossPct jump 0.04–0.45 → 1.24–2.85 (>2×) across all seeds | Depth-23's deterministic layout produces the hardest tier-1 encounter cluster (2 back-to-back multi-monster battles, 13–18 rounds). Same-layout-every-run is by design (layout seeds on depth); combined with the pacing gap above it is the consistent wipe point. |

### Minor Issues

| Pit Level | Job | Issue | Notes |
|---|---|---|---|
| 26 = 1(2) | Solo Knight | Curve-level (L46) solo Knight wipes at tier-2 entry | The same floor is cleared by a 3-member L46 party (respawn scenario: wiped=0, battles=3, healing=463). Tier 2 is party-gated — consistent with the tavern + tier-base-level respawn design, but a solo idle hero will stall here. |

### What passed

- **Tier crossing is smooth (the core #291 acceptance).** Seed 424242's natural run: pit 25 boss hpLoss 0.06 → depth 26 hpLoss 0.43 → progressed to depth 33 (displayed 8, tier 2). No difficulty cliff or reset at the boundary; monster levels are monotonic across the crossing (unit-verified ±2 of curve).
- **Tier-2 sampled floors (level-appropriate hero):** depths 30/40/45/50 all cleared solo, including three tier-2 boss floors; boss identity stays keyed to the displayed floor (pit 20(2) is still PitLord, at the scaled level).
- **Tier-2 loot:** chests yield cave gear with `+2` scaled stats (no more potion-only chests); auto-equip picked up 3–4 pieces per floor in tier-2 runs (`gearEquipped` column).
- **Respawn-at-TierBaseLevel:** a wiped tier-2 party restarting at TierBaseLevel 46 with floor-46 mercenaries re-clears depth 26 — the permanent-tier death loop is viable.
- **Rewards scale again:** pit-23 kills yield 128–384g per floor vs the flat 23g/58xp of the frozen-level bug.

## 4. Elemental Matchup Matrix

Not re-tested. This change does not touch elemental properties, resistances, or damage multipliers; monster elemental assignments are unchanged (see `PitHero/docs/CaveBiomeBalanceReport.md` for the standing matchup validation). Tier-scaled gear preserves the source item's element and resistances (`CreateTierScaledCopy` copies `ElementalProps`).

## 5. Equipment Tier Utilization

Observed across the persistent and sampled runs (auto-equip decisions):

| Depth range | Drops seen | Equipped | Notes |
|---|---|---|---|
| 1–15 (tier 1) | Normal cave gear | 0–1 per floor | Unchanged from pre-#291 behavior |
| 16–25 (tier 1) | Normal/Uncommon cave gear | 1–6 per floor | Higher equip rate — better gear now matters against correctly-leveled monsters |
| 26–50 (tier 2) | Cave gear `+2` (tier-scaled) | 2–4 per floor | `+2` gear strictly upgrades same-slot tier-1 gear (atk +12, def +8, stat +5 at Normal rarity for the 25-depth delta); no tier is skipped |

## 6. Verdict

**Pass-with-caveats.**

The #291 core is validated: monster levels, rewards, and loot now scale continuously through the tier boundary with no flat spots and no discontinuity at 25→1(2); the tier-base-level respawn loop works. The caveats are pacing, not scaling: a greedily-descending natural-pace party hits a wall around pit 16–23 of tier 1 (hero ~L11 vs curve L41), and tier-2 entry requires a party. Both are grind-loop tuning questions, not defects in the tier system — every floor is beatable by a level-appropriate party, which is the design contract.

## 7. Prioritized Rebalance Recommendations

| Priority | Change | Affected | Rationale |
|---|---|---|---|
| 1 (Major, follow-up issue suggested) | Close the natural-pacing gap at pits 16–25. Concrete options, pick one: (a) raise monster XP yield `10 + L*8` → `10 + L*12` so curve-level kills close the level gap faster; (b) floor tier-1 mercenary rolls at `EstimatePlayerLevelForPitLevel(currentPit)/2` (mirrors the tier-≥2 TierBaseLevel floor); (c) soften `EstimatePlayerLevelForPitLevel` for pits 21–25 from `36+(p−20)*1.75` → `36+(p−20)*1.25`. Re-run `TestCategory=BalanceProbe` after any change — wipe depth should move from 23 to ≥25. | Tier-1 pits 16–25 | 2/3 natural-pacing seeds wipe at pit 23 with hero 30 levels under curve |
| 2 (Minor) | If solo play should remain viable into tier 2, floor `DetermineMercenaryLevel`'s tier-1 rolls (option 1b) so a depth-26 hero can field a credible party for ~200–600g | Tier-2 entry | Solo L46 Knight wipes at depth 26; L46 party clears it |
| 3 (Info) | Keep `BalanceProbeTests` (TestCategory=BalanceProbe) as the standing pacing probe; it prints seed-stamped 1–50 CSVs with the `heroLevel` column for before/after comparison | Balance workflow | Deterministic, <1s, reusable for the follow-up tuning |

---

## Handoff Summary

- Report path: `features/reports/feature_pit_tier_progression_balance_report.md`
- Verdict: **Pass-with-caveats**
- Prioritized recommendations: 3 (1 major pacing follow-up, 1 minor solo-viability option, 1 workflow note)
