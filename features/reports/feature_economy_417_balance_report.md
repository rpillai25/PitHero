# Balance Report — Game Economy (issue #417)

**Date:** 2026-09-16
**Seeds:** 417 (economy simulation), 12345 (pit traversal)
**Scenarios:** starter, mid-game, late-game diverse, late-game monoculture, pit depths 1–100
**Tested-by:** `PitHero.Tests/EconomySimulationTests.cs` (`TestCategory=EconomySim`) on the new `VirtualEconomySimulation`

---

## 1. Test Scope

Issue #417 rebuilt the gold economy. This report validates:

1. **Crop retune** — profit per growth hour is a function of the crop's progression tier (`{1.25, 2.2, 4, 7, 12, 25, 45}` g/h for tiers 0–6); seed prices cap at half of a one-shot's first-cycle profit; Wheat/Grapes/Turnip stacks shrunk so auto-sell fires within a real hour.
2. **Market saturation** — per-crop demand (floor 15%, saturation 80 plots, 5%/in-game-hour recovery) that settles to a steady state and punishes monoculture without any replanning.
3. **Dish progression** — dishes soft-unlock with their recipe crops and fully unlock on lifetime servings; patrons and the party only order unlocked dishes; the patron dish bag's inverse-price weights are clamped to 3 marbles.
4. **Chest gold** — one item chest in two carries `(30 + 22 × depth) × [0.75, 1.25]` gold, ×2 on boss floors, cap 2,500.

Owner pacing anchor: a diversified 100-plot late farm earns **1,000,000 g in ~10 real hours** from crops alone.

## 2. Methodology

- `VirtualEconomySimulation` steps one in-game minute (= one real second) and reuses the real `CropConfig`, `CropUnlockConfig`, `DishConfig`, `DishUnlockConfig`, `CropMarketService`, `CropStorageInventoryService`, `AutoCropSellService` and `DishBagBuilder`. Modelled: wet-gated growth (exact `CropGrowthService` math, Wet cleared at 6 AM), workers (water → harvest → plant, `GameConfig` durations + 4 s travel, can charges, 6 AM–10 PM shift), the auto-seed rule, and the tavern (schedule-driven arrivals, 9 seats, 600 s patience, cook/server throughput with 8 s legs, 50% tips of 5–15%, three party meals a day with the hero paying).
- Scenarios: **starter** (12 wheat + 6 corn, 200 g, one level-1 worker, no kitchen, auto-sell + auto-seed on, buffer 200), **mid-game** (30 plots tiers 1–3, 2 level-4 workers, 2 cooks + 1 server, party eating out), **late diverse** (20 apple, 15 pumpkin, 15 watermelon, 12 potato, 12 grapes, 8 turnip, 8 onion, 5 sugarcane, 5 lettuce; 6 level-9 workers; 3 cooks + 2 servers; 30,000 g; everything unlocked), **late monoculture** (100 apple trees, same crew).
- Pit gold: `VirtualGameSimulation.RunPitLevel(depth)` with a level-appropriate fully-equipped Knight at sampled depths across tiers 1–4.
- Unit tests: full suite **2,348 passed / 0 failed** (baseline 0). New: `EconomyBalanceTests` (rewritten), `CropMarketServiceTests`, `DishUnlockConfigTests`, chest gold tests in `ShuffleBagLootTests` / `BalanceConfigTests` / `VirtualBalanceTraversalTests`, save v37 layout tests, `EconomySimulationTests`.

## 3. Findings

### Gold curves (net gold per real hour)

| Scenario | g / real hour | 1,000 g | 10,000 g | 100,000 g | 1,000,000 g |
|---|---|---|---|---|---|
| Starter | **596** | 1h 37m | — | — | — |
| Mid-game | **5,400** | start | 1h 39m | — | — |
| Late diverse | **91,600** | start | start | 2h 01m | **10h 33m** |
| Late monoculture | 53,100 | start | start | 3h 17m | never (12 h) |

Late diverse, hour by hour (gold in wallet): 42k · 87k · 191k · 263k · 393k · 477k · 606k · 696k · 844k · 928k · 1,025k · 1,129k. Hours 5–8 and 9–12 earn within a third of each other — the market has settled and the farm keeps printing with no replanning.

### Income by source (late diverse, 12 real hours)

| Source | Gold | Share of gross |
|---|---|---|
| Crop sales (after demand) | 1,043,256 | 76% |
| Dish sales | 310,590 | 23% |
| Tips | 15,023 | 1% |
| Seeds bought | −252,285 | |
| Hero meals | −17,250 | |

Seeds are the one big sink (24% of crop sales): top-tier one-shots (Watermelon 1,100 g, Pumpkin 800 g) are replanted every 80–110 in-game hours. That is by design (harvest ≥ 2× seed) and keeps auto-purchase meaningful.

### Market saturation

| Crop (late diverse) | Plots | Realized price vs base | Final demand |
|---|---|---|---|
| AppleTree | 20 | 80% | 76% |
| Turnip / Onion | 8 | 91% / 92% | 94% |
| Potato / Grapes | 12 | 95% / 96% | 94% / 96% |
| Watermelon / Pumpkin | 15 | 100% / 99% | 100% / 93% |

The monoculture's 100 apple trees realized **28%** of base price (final snapshot 46%, mid-recovery between synchronized harvest bursts) and earned **58%** of the diverse farm's net gold — and its kitchen sold nothing, because no dish can be made from apples alone (423 of 423 patrons left hungry, 90 of 90 hero meals skipped). Analytic steady state `1 − plots / 80` matches within 5 points wherever harvests are not bursty.

### Fertilizer artifacts (Fast Grow 2×, Lightning Grow 3×)

The owner asked whether the 3× growth artifact had been considered: it had not, and without
compensation the market read a fertilized farm as a farm 3× larger, so Lightning Grow delivered
only 1.83× income (Fast Grow 1.58×). `CropMarketService.SaturationScale` now follows the active
growth multiplier each fixed step, deepening the market by the same factor. Measured after the
fix (`EconomyFertilizerProbeTests`, 12 real hours):

| Farm | Growth | g / real hour | Crop sales | 1,000,000 g | Apple realized price | Worker util |
|---|---|---|---|---|---|---|
| Diverse 100 | 1× | 91,611 | 1,043,256 | 10h 33m | 80% | 31% |
| Diverse 100 | 2× | 170,776 | 2,161,380 | 6h 01m | 80% | 45% |
| Diverse 100 | 3× | 229,060 | 3,075,568 | 4h 25m | 85% | 53% |
| 100 apple | 1× | 53,112 | 637,340 | never | 28% | 25% |
| 100 apple | 2× | 109,041 | 1,308,495 | 9h 02m | 27% | 36% |
| 100 apple | 3× | 146,631 | 1,759,569 | 6h 36m | 29% | 42% |

Crop sales scale by 2.07× and 2.95×; net income by 1.86× and 2.5× because the kitchen is
patron-limited and seed spend scales with harvests. The monoculture penalty is unchanged.

### Progression pacing

- Starter unlocks Tomato and Eggplant (tier 1 crops) at **31 real minutes**; 1,000 g at 1h 37m. One level-1 worker keeps 18 plots 99% wet at 50% utilization.
- Mid-game: the kitchen (2 cooks, 1 server) served 78 of 151 patrons; dish sales are 34% of crop sales. Hero meals were skipped 29 of 30 times because the favorite (Turnip Onion Stew) rarely had turnips in stock — turnips were being auto-sold. Recommendation 2 below.
- Late diverse: 277 of 420 patrons served; dishes are 23% of gross. Hero ate 75 of 90 meals.

### Pit gold per level (fresh level-appropriate Knight, seed 12345)

| Depth | Tier | Level | Battle gold | Chest gold | Chests |
|---|---|---|---|---|---|
| 1 | 1 | 1 | 11 | 49 | 1 |
| 10 | 1 | 10 | 59 | 424 | 2 |
| 20 | 1 | 20 | 119 | 1,870 | 3 |
| 30 | 2 | 5 | 170 | 1,164 | 3 |
| 40 | 2 | 15 | 0 | 3,497 | 2 |
| 50 | 2 | 25 | 245 | 2,272 | 5 |
| 75 | 3 | 25 | 296 | 12,500 | 9 |

Chest pouches are already the larger pit income by tier 2, cap at 2,500 each from depth ~75, and the boss-floor double shows at depths 20/25/50/75. Pit gold stays an order of magnitude below a mature farm, which is the intent: the pit funds the early game and the farm prints the millions. (Rows with 0 battle gold are levels where the single-level traversal wiped or fought nothing before the orb — a pre-existing traversal characteristic, not a gold change.)

### Worker throughput

Six level-9 workers sat at 25–31% utilization on 100 plots with 98% wet time; two or three would suffice. One level-1 worker holds 18 plots at 50%. Watering is not the bottleneck the plan expected once the day shift is modelled.

## 4. Elemental Matchup Matrix

Not applicable — no combat balance changed.

## 5. Equipment Usage Matrix

Not applicable — loot pools unchanged; chest gold is additive.

## 6. Overall Verdict

**Pass.** The pacing anchor lands at 10h 33m for a diversified 100-plot farm, the starter stays modest (~600 g/h), every tier out-earns the last, a monoculture earns a little over half of a mixed farm at a 28% realized price without any player intervention, the kitchen is a material secondary income that scales with unlocks, and chest gold gives the pit a second faucet that caps cleanly.

## 7. Recommendations

1. **None required for the anchor.** If the owner wants the mix at exactly 10 h, raise tier 5 (`TierProfitPerGrowthHour[5]`, 25 → 27); if the starter should feel richer, raise tier 0 (1.25 → 1.5 ≈ 720 g/h).
2. **Auto-sell vs the kitchen (follow-up):** the mid-game party's favorite was starved because auto-sell shipped full turnip stacks before the fridge could pull them. A per-crop "keep stacks" default of 1 for crops used by the hero's favorite dish, or a fridge-first reservation, would fix it; out of scope here (Keep Stacks already exists as a player setting).
3. **Seed spend visibility:** seeds are 24% of a top-tier farm's gross. Consider showing "seeds bought today" next to the auto-purchase toggle so the sink is legible.
4. **Fidelity caveats for future runs:** no pathing (4 s flat travel), no fridge pre-stock or runner, no worker sleep beyond the day shift, and every plot planted at minute 0 (synchronized harvest bursts make hourly income lumpy and demand snapshots swing — judge realized average price, not the final demand reading).
