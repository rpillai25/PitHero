# Economy Balance Guide

## Introduction

This guide documents the idle-economy balance decisions made in issue #287. The goal is to keep
per-plant gold income in a 20–40 g/real-hour band so that progression feels earned: a small early
farm generates pocket change; a mature, expanded farm generates meaningful wealth.

---

## Time Scale

| Unit | Equivalent |
|---|---|
| 1 real second | 1 in-game minute |
| 1 real minute | 1 in-game hour |
| 1 real hour | 60 in-game hours |

Implementation: `InGameTimeService.cs:9`, `CropGrowthService.cs:27`.

---

## Design Targets

| Metric | Target |
|---|---|
| Net gold per real hour per established plant | 20–40 g |
| Net gold per in-game growth hour (same band in game units) | 0.33–0.67 g |
| 10-plant early farm | ~300 g/real-hr |
| 200-plant expanded farm | ~6,000 g/real-hr |

**Prices only.** Growth times, regrow times, yields, stack sizes, and seed prices are out of scope
for this rebalance. See the "Out-of-Scope Items" section.

---

## Crop Formula

### Constants (`CropConfig.cs`)

| Constant | Value | Meaning |
|---|---|---|
| `HarvestGoldPerGrowthHour` | 0.5 g | Base gold per in-game growth hour at tier 1.0 |
| `HarvestUnitSellFloor` | 1 g | Minimum sell value for any single harvested unit |

### Income Cycle

The **income cycle** (`GetIncomeCycleHours`) is the number of in-game hours that one harvest
pays for:

- **Repeat-harvest crops** (Corn, Tomato, Eggplant, Grapes, AppleTree): the steady-state regrow
  cycle from revert frame to fully grown, scaled by `GetRegrowthRateMultiplier`.
- **One-shot crops** (all others): the full seed-to-mature growth time.

### Unit Sell Price

```
cycleGold = HarvestGoldPerGrowthHour × tier × cycleHours
if one-shot: cycleGold += seedPrice      // recover seed cost each cycle
unitPrice = max(cycleGold / yield, HarvestUnitSellFloor)
```

Because one-shot crops include seed recovery, their **net profit** per cycle equals
`cycleGold − seedPrice = HarvestGoldPerGrowthHour × tier × cycleHours`, the same expression as
regrow crops. Net rate is therefore `0.5 × tier` for every crop, giving a linear,
tier-ordered income band.

### Stack Sell Price

`GetHarvestStackSellPrice(crop, count)` = `ceil(unitPrice × count)`. The ceiling ensures small
fractional unit prices (Corn 2.25, Tomato 2.14, AppleTree 3.90) add up correctly over a stack.

### Why a 1g Floor?

The old 5g floor would push Corn's per-unit price above formula value, inflating its real-hour
income well past the 30 g/hr target. The 1g floor guards only against pathological future data
(e.g., an extremely high-yield crop with a very short cycle) while leaving all 13 current crops
untouched — the cheapest is AppleTree at 3.90 g/unit.

---

## Crop Results Table

Net g/real-hr = `(0.5 × tier) × 60`. All 13 crops land in the 21–39 g/real-hr band.

| Crop | Type | Tier | Cycle (in-game hrs) | Unit sell (g) | Net g/real-hr |
|---|---|---|---|---|---|
| Wheat | one-shot | 0.70 | 8 | 27.80 | 21.0 |
| Lettuce | one-shot | 0.75 | 8 | 13.25 | 22.5 |
| Turnip | one-shot | 0.75 | 12 | 6.06 | 22.5 |
| Sugarcane | one-shot | 0.80 | 21 | 29.20 | 24.0 |
| Onion | one-shot | 0.85 | 20 | 6.50 | 25.5 |
| Potato | one-shot | 0.85 | 18 | 14.41 | 25.5 |
| Tomato | regrow | 0.95 | 18 | 2.14 | 28.5 |
| Corn | regrow | 1.00 | 13.5 | 2.25 | 30.0 |
| Eggplant | regrow | 1.05 | 30 | 15.75 | 31.5 |
| Grapes | regrow | 1.10 | 32 | 17.60 | 33.0 |
| Pumpkin | one-shot | 1.15 | 80 | 146.00 | 34.5 |
| Watermelon | one-shot | 1.20 | 110 | 166.00 | 36.0 |
| AppleTree | regrow | 1.30 | 24 | 3.90 | 39.0 |

Full Turnip harvest (×9) sells for **55 g**; full Corn harvest (×20) sells for **45 g**.

---

## Regrow Crop Establishment Payback

Repeat-harvest crops require an upfront seed investment that is never directly recovered in the
sell price (seed recovery only applies to one-shot crops). Players pay the seed cost once and
earn steady-state income thereafter.

| Crop | Seed cost | Steady-state g/real-hr | Payback time |
|---|---|---|---|
| Corn | 50 g | 30 g/hr | ~1.7 real hrs |
| AppleTree | 200 g | 39 g/hr | ~5.1 real hrs |

After payback, every subsequent income cycle is pure profit. This gives late crops (AppleTree)
a meaningful establishment cost relative to earlier crops (Corn), matching the "reward patience
and expansion" design intent.

---

## Gear Sell Fractions

Replacing the old flat 50% sell price with rarity-scaled fractions (implemented in
`ItemExtensions.GetSellPrice`). Normal-rarity loot flooding is reduced; finding a rare or epic
item is meaningfully more valuable.

| Rarity | Sell fraction | Example: 500 g item |
|---|---|---|
| Normal | 20% | 100 g |
| Uncommon | 35% | 175 g |
| Rare | 50% | 250 g |
| Epic | 60% | 300 g |
| Legendary | 75% | 375 g |

**Sell < buy invariant:** every rarity sells below buy price, so purchasing an item at full price
and immediately selling it is always a net loss. This invariant holds at all price points.

---

## Consumable Exception

Consumables always sell for **50% of buy price** (`item.Price / 2`), regardless of their listed
rarity. All potions are `ItemRarity.Normal`, but their potency is encoded in restore amounts, not
rarity — using rarity-scaled fractions would make FullMixPotion (900 g buy, `Normal` rarity)
sell for only 180 g instead of 450 g, breaking the potion buyback economy in the Second Chance
Shop.

---

## Out-of-Scope Items

The following balance values were deliberately **not changed** in issue #287:

- **Monster gold drop formula** (`BalanceConfig.cs:449-455`) — enemy gold is a separate tuning pass.
- **Seed prices** (`CropConfig.GetSeedPrice`) — seed cost is a one-time acquisition barrier, not the ongoing income driver under the rate-based formula.
- **Growth times** (`CropConfig.GetHoursPerStage`, `GetFrameCount`) — these define the game's pacing and are frozen for this rebalance.
- **HeroCrystal sell values** (`Permadeath.md`) — handled by a separate subsystem.

---

## Related Documentation

- `PitHero/docs/EquipmentBalanceGuide.md` — Equipment stat formulas and rarity multipliers
- `PitHero/docs/EquipmentLibrary.md` — Full gear library
- `CropConfig.cs` — All crop data and sell-price helpers
- `ItemExtensions.cs` — `GetSellPrice` implementation
