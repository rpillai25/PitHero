# Balance Report — Skill System Fixes (Phases 1–3)

**Branch:** `feature/skillGapFixes` · **Date:** 2026-07-11 · **Trigger:** skill-system audit fixes (merc skills now run real formulas, elemental multipliers apply to skill damage, self/ally skills became data-driven, heals normalized through `SkillHealCalculator`)

## 1. Test scope

Requested: before/after comparison of clear rates and party survivability across pit levels for the Phase 2–3 combat changes.

## 2. Methodology + coverage gap (read this first)

**A full virtual pit traversal could not measure these changes.** The virtual game layer (`PitHero/VirtualGame/`) has **no combat simulation** — battles resolve as goal conditions without damage math (confirmed by code audit; see `VirtualGameLogicLayer.md` scope). Every change in this feature is a battle mechanic, so a traversal-based before/after comparison would report identical results regardless of the changes. Per the pit-balance-test coverage rule, this is reported as a **gap for the `virtual-game-layer` skill**: add battle resolution (attack resolver + skill execution + buffs) to `VirtualGameSimulation` before the next combat-affecting balance pass.

What WAS run:
1. **Full unit suite:** 1108 passed / 107 failed / 6 skipped — the 107 failures are all the pre-existing `Nez.Core.Services` NullReference environment issue (localized name lookups in tests), byte-identical to the pre-change baseline of 109 failed / 1047 passed (2 fixed, 0 introduced).
2. **Balance-relevant classes** (`BalanceSystemTests`, `GearItemsTests`, `*StatGrowth*`, plus new `SkillExecutionTests`, `BattleBuffTests`, `SkillBuffDataTests`, `EnhancedBattleSystemTests`): 155 passed / 13 failed — all 13 are the same pre-existing environment failure pattern (verified individually).
3. **Static before/after quantification** of every deliberate number shift (below).

## 3. Findings — quantified balance shifts

| Change | Before | After | Direction |
|---|---|---|---|
| Merc attack skills | generic weapon-swing damage | real formulas (PowerShot ×1.5, HeavyStrike +STR, LifeLeech drain, etc.) | **Merc DPS up**, size depends on skill |
| Skill elemental multipliers | always Neutral | skill Element vs enemy `ElementalProperties` | Swing both ways; matchup play now real |
| AoE skills | skipped primary target | hit primary + surrounding | AoE DPS up vs 2+ enemy packs |
| Fire-bonus consistency | 4 Fire skills ignored `FireDamageBonus` | all apply it | Up for Heart-of-Fire builds |
| AuraHeal | (25 + MAG×2.5)×(1+bonus) | (25 + MAG×2)×(1+bonus) | Slight nerf at high MAG |
| SoulWard | (30 + MAG×3)×(1+bonus) | (30 + MAG×2)×(1+bonus) | Nerf at high MAG |
| Purify | (15 + MAG×2)×(1+bonus) | identical via `SkillHealCalculator` | Unchanged |
| Piercing Arrow | flat ×1.4 | ×1.0 vs half defense | Better vs armored, worse vs squishy |
| defup/KiCloak/SmokeBomb/Fade | did nothing in battle | real temporary buffs (capped stacks) | Survivability up under Defensive tactic |
| counter/deflect (Monk) | dead fields | functional | Monk survivability/DPS up |
| calm_spirit | dead | +1 MP/round in battle | Sustained caster uptime up |

Net expectation: **party power increases**, mostly on mercenary-heavy parties and AoE/elemental play. Monster-side power is unchanged. If post-change play data shows clear rates rising noticeably, the first rebalance lever is monster archetype multipliers, not reverting skill correctness.

## 4./5. Elemental & equipment matrices

Not measurable without the virtual combat sim (see §2 gap). Elemental advantage magnitude is exercised by unit test (`ResolveHit_ViaFireSkill_ElementalMultiplierApplied`) confirming the multiplier path works; a full matchup utilization matrix needs traversal.

## 6. Verdict

**Pass-with-caveats.** No formula/regression failures; all deliberate shifts enumerated and directionally sane. The caveats: (a) virtual-layer combat coverage gap blocks a true clear-rate comparison; (b) merc DPS uplift is real and unquantified in aggregate.

## 7. Prioritized recommendations

1. **(Major)** Implement virtual-layer combat simulation (delta plan for `virtual-game-layer` skill) so the next combat change gets a real before/after traversal.
2. **(Major)** After Phases 4–6 land, play 2–3 debug sessions with a merc party and compare analytics (`attack` dmg distributions, battle durations, `char_killed` counts) against pre-change session logs (e.g. `session_20260710_234639.jsonl`) — live data is currently the only end-to-end balance signal.
3. **(Minor)** Watch AuraHeal/SoulWard users at MAG 60+ — the ×3→×2 normalization is a real nerf there; if priest-synergy builds underperform, raise those skills' `hpRestoreAmount` rather than re-forking the formula.
