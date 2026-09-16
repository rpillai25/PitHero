# Economy Balance Guide

## Introduction

This guide documents the gold economy as rebalanced in issue #417 (which supersedes the
#287 rate model). The goals: the player must eventually make millions of gold (artifacts cost
100k–4M), a brand-new farm must feel modest, every harvest must comfortably fund replanting,
income must scale linearly with plot count, unlocking later crops must be what raises income,
and a *diversified* farm must out-earn a monoculture without the player ever replanning.

Pacing anchor (owner decision): a **diversified 100-plot late farm, fully automated, earns
1,000,000 g in ~10 real hours** from crops alone; the kitchen and pit gold stack on top.

Verified by the headless economy simulation — see
`features/reports/feature_economy_417_balance_report.md` and
`PitHero.Tests/EconomySimulationTests.cs`.

---

## Time Scale

| Unit | Equivalent |
|---|---|
| 1 real second | 1 in-game minute |
| 1 real minute | 1 in-game hour |
| 1 real hour | 60 in-game hours |

Crops grow only while their tile is Wet; Wet clears farm-wide at 6 AM and workers re-water,
so a plant accrues about **55 wet growth hours per real hour**. Implementation:
`InGameTimeService.cs`, `CropGrowthService.cs`.

---

## Crop Formula (`CropConfig.cs`)

Profit is a pure function of the crop's **progression tier** (`CropUnlockConfig.GetTier`):

```
profitPerGrowthHour = TierProfitPerGrowthHour[tier]      // { 1.25, 2.2, 4, 7, 12, 25, 45 }
cycleProfit         = profitPerGrowthHour × cycleHours    // GetIncomeCycleHours
unitSell (one-shot) = (cycleProfit + seedPrice) / yield   // seed recovered every harvest
unitSell (regrow)   = cycleProfit / yield
stackSell           = ceil(unitSell × count)
```

- `HarvestUnitSellFloor = 1 g` guards degenerate data only.
- **Seed prices** (`GetSeedPrice`): a one-shot seed is at most half of its first-cycle profit,
  so every harvest funds at least two more plantings; a regrow seed costs roughly one income
  cycle (a one-time establishment fee). `EconomyBalanceTests` pins both rules.
- **Stack sizes** (`GetMaxHarvestStack`): auto-sell only moves full stacks, so Wheat (20),
  Grapes (10) and Turnip (27) were shrunk so a modest patch fills a stack within about a real
  hour. Old saves holding a bigger stack still sell it (`Count >= max`).

### Crop table (base prices, before market demand)

| Crop | Tier | Type | Cycle h | Yield | Profit/cycle | Seed | Unit sell | g per plot-real-hour |
|---|---|---|---|---|---|---|---|---|
| Wheat | 0 | one-shot | 8 | 1 | 10 | 5 | 15 | 69 |
| Corn | 0 | regrow | 13.5 | 3 | 16.9 | 15 | 5.6 | 69 |
| Tomato | 1 | regrow | 18 | 4 | 39.6 | 40 | 9.9 | 121 |
| Eggplant | 1 | regrow | 30 | 1 | 66 | 65 | 66 | 121 |
| Sugarcane | 2 | one-shot | 21 | 2 | 84 | 40 | 62 | 220 |
| Lettuce | 2 | one-shot | 8 | 4 | 32 | 15 | 11.75 | 220 |
| Turnip | 3 | one-shot | 12 | 9 | 84 | 40 | 13.8 | 385 |
| Onion | 3 | one-shot | 20 | 9 | 140 | 65 | 22.8 | 385 |
| Potato | 4 | one-shot | 18 | 4 | 216 | 100 | 79 | 660 |
| Grapes | 4 | regrow | 32 | 1 | 384 | 350 | 384 | 660 |
| Watermelon | 5 | one-shot | 110 | 1 | 2,750 | 1,100 | 3,850 | 1,375 |
| Pumpkin | 5 | one-shot | 80 | 1 | 2,000 | 800 | 2,800 | 1,375 |
| AppleTree | 6 | regrow | 24 | 4 | 1,080 | 900 | 270 | 2,475 |

Regrow crops pay their establishment time first (Apple Tree: 160 in-game hours ≈ 2.7 real
hours before the first harvest).

---

## Market Saturation (`CropMarketService`, issue #417)

Each crop has a demand multiplier in `[MarketDemandFloor, 1]`, starting at 1. Selling a crop
lowers its demand in proportion to the **base** gold sold; demand recovers toward 1 at a
constant rate. Nothing is random and nothing moves on its own, so a farm planned once settles
into a steady income and never needs replanning.

```
on sale:   demand -= baseUnitPrice × units / depth       depth = MarketSaturationPlots × profitPerGrowthHour / MarketRecoveryPerHour
per hour:  demand += (1 - demand) × MarketRecoveryPerHour
price:     baseUnitPrice × demand
```

Steady state for N plots of one crop is `demand ≈ 1 − N / MarketSaturationPlots` (floored),
**independent of tier and of the recovery rate** — the recovery rate only sets how fast the
market settles and how much a synchronized harvest burst can ride the recovery.

| Constant (`GameConfig`) | Value | Meaning |
|---|---|---|
| `MarketDemandFloor` | 0.15 | A wall of one crop still pays 15% of base |
| `MarketSaturationPlots` | 80 | Plots of one crop that would zero its demand without the floor |
| `MarketRecoveryPerHour` | 0.05 | Fraction of the gap to full demand recovered per in-game hour |

So ~16 plots of a crop sell near 80%, 40 plots near 50%, 68+ plots at the floor. Crops the
kitchen consumes never touch demand, so cooking is the outlet for a surplus crop. Simulation
result: 100 apple trees realize a **28%** average price and earn 58% of what a mixed 100-plot
farm earns; the mixed farm's apples (20 plots) sell at 80%.

Every path that pays the player for crops goes through `CropSellPricing` (`PitHero/Util/`),
which resolves the scene's market (base price headlessly) and records the sale. A direct
`CropConfig.GetHarvestStackSellPrice` call in a sell path is a bug. Demand is sim state:
registered per scene, ticked from the fixed step, persisted in save v37 (§52), shown as
"Market demand: N%" in the crop viewer's description window. Dish and seed prices are not
affected by demand.

---

## Dish Pricing and Progression

Menu prices derive from crop base prices (`DishConfig.ComputePrice`):
`ingredientSellValue × 1.25 + effect premium (15 g per ATK/DEF/AGI point, 10 g per MAG,
3 g per EVA, 30 g per regen point)`, rounded to 5 g, min 10 g, with a monotonicity pass.
Cooking therefore always beats raw selling and the fixed premium makes early dishes a strong
multiplier (a 15 g wheat becomes a 35 g Buttered Bread). The Harvest Feast Platter now also
takes a Watermelon so it stays the priciest dish.

Dishes unlock in tiers that mirror the crops — see `TavernDiningSystem.md` "Dish
progression". Walk-in patrons draw from a shuffle bag whose inverse-price weights are clamped
to `DishBagMaxMarbles = 3`, so the whole unlocked menu cycles through and kitchen income
scales with unlocks (`ShuffleBagSystem.md`).

Approximate menu after #417: Bread 35 · Grilled Corn 60 · Bisque 90 · Salad 85 · Skewers 100
· Stew 110 · Parmesan 185 · Chowder 200 · Mash 230 · Steak 430 · Grape Tart 635 · Grape Juice
1,070 · Apple Pie 1,525 · Pumpkin Soup 3,590 · Sorbet 5,030 · Feast ≈ 9,300.

---

## Pit Gold

- **Monster kills**: `BalanceConfig.CalculateMonsterGoldYield(level) = 5 + level × 3`
  (caps at 302 when the monster level caps at 99).
- **Chest pouches** (issue #417): one item chest in two (a 10-of-20 shuffle bag in
  `LootBagSet`) also carries gold: `(30 + 22 × effectiveDepth) × [0.75, 1.25]`, doubled on
  boss floors, capped at `ChestGoldCap = 2,500` (`BalanceConfig.CalculateChestGold`). Seed,
  stencil and boss epic chests never carry gold. Roughly four kills' worth at any depth.

| Effective depth | 1 | 10 | 25 (boss) | 50 (boss) | 75+ |
|---|---|---|---|---|---|
| Pouch range | 39–65 | 188–313 | 870–1,450 | 1,700–2,500 | cap |

---

## Design Targets (measured)

| Scenario (economy simulation, seed 417) | Net gold per real hour | Notes |
|---|---|---|
| Starter: 12 wheat + 6 corn, 1 level-1 worker | ~600 | 1,000 g at 1h 37m; Tomato/Eggplant unlock at 31 min |
| Mid-game: 30 plots (tiers 1–3), 2 workers, small kitchen | ~5,400 | 10,000 g at 1h 39m |
| Late diverse: 100 plots over 9 crops, 6 workers, full kitchen | ~92,000 | 1,000,000 g at 10h 33m |
| Late monoculture: 100 apple trees | ~53,000 | apples realize 28% of base |

---

## Gear Sell Fractions

Rarity-scaled fractions (`ItemExtensions.GetSellPrice`): Normal 20%, Uncommon 35%, Rare 50%,
Epic 60%, Legendary 75%. **Sell < buy** at every rarity. Consumables always sell for 50% of
buy price regardless of rarity (potency is in the restore amount, not the rarity).

---

## Related Documentation

- `PitHero/docs/TavernDiningSystem.md` — dish progression, kitchen income
- `PitHero/docs/ShuffleBagSystem.md` — chest gold gate, dish bag weights
- `PitHero/docs/VirtualGameLogicLayer.md` — economy simulation
- `features/reports/feature_economy_417_balance_report.md` — measured curves
- `CropConfig.cs`, `CropMarketService.cs`, `CropSellPricing.cs`, `DishUnlockConfig.cs`
