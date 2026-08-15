# Shuffle Bag System

Issue #382. Controlled randomness via the classic **shuffle bag** ("marble bag"): a weighted
finite pool drawn without replacement, refilled automatically when exhausted. Over any full
cycle the draw counts exactly match the bag's composition, so streaks and droughts are
bounded while individual draws still feel random. Average rates are unchanged from the
pure-random constants the bags replaced — only the variance is tamed.

Four systems are bag-driven: **battle crits**, **treasure-chest loot**, the **biome-boss epic
chest**, and **tavern patron dish orders**.

## The primitive: `ShuffleBag<T>`

`PitHero/RolePlayingFramework/Utils/ShuffleBag.cs` (namespace `RolePlayingFramework.Utils` —
usable from both RPF and PitHero code; RPF must not reference `PitHero.*`).

- `Add(T item, int count = 1)` — adds marbles. **Adding resets the cursor to a full bag**
  (restarts the current cycle), so build compositions once, up front.
- `T NextFromRoll(float roll01)` — **the core primitive. Consumes no RNG.** The caller
  supplies a [0,1) roll; the bag maps it onto its remaining marbles (swap-to-boundary, cursor
  decrements, auto-refill on exhaustion). This is what lets bags sit behind existing RNG call
  sites without changing the *number or order* of global RNG calls.
- `T Next()` — convenience, draws via `Nez.Random.NextFloat()`. Only for streams that are
  NOT determinism-contract-bound (e.g. the tavern dish bag).
- `T Next(System.Random rng)` — convenience for caller-owned RNGs (virtual layer).
- `Count` / `Remaining` / `Clear()`.

AOT-safe: no LINQ, no per-draw allocation.

### The one rule that must never be broken

**In any RNG-contract-bound path, bags consume caller-supplied rolls (`NextFromRoll`), never
their own.** The battle `Nez.Random` call sequence is a compatibility contract (see
`VirtualGameLogicLayer.md` § behavior-preservation contract). A bag that draws RNG itself, or
a call site that draws RNG conditionally, silently changes every downstream battle outcome.
The structural guard is `BattleEngineTests` (`MercQueue_EmptyOrNull_ProducesIdenticalBattle`,
same-seed determinism) — if those fail after a bag change, the RNG call count drifted.

## Battle crits — `CritBagSet`

Files: `RolePlayingFramework/Combat/CritBagSet.cs`, `BattleReactionHelper.RollCrit`,
`PitHero/Combat/BattleEngine.cs` (three ally-attack sites),
`RolePlayingFramework/Balance/BalanceConfig.cs` (`BaseCritChance = 0.05f`, `CritBagSize = 20`).

- Every hero/merc attack (physical or attack skill) has a 5% base crit chance, enforced as
  **exactly 1 crit per 20 attacks** by a per-combatant base bag. Monsters never crit. Crit
  damage stays ×2.
- **RNG contract (since #382): every ally attack consumes exactly TWO `Nez.Random.NextFloat()`
  calls** — a base-crit roll, then a quickdraw roll — *both always consumed*, even when the
  attacker can't crit (precedent: `RollDodge` always rolls at 0 dodge). The floats feed
  `BattleReactionHelper.RollCrit(caster, isFirstAction, baseRoll, quickdrawRoll)`, which
  drives the bags via `NextFromRoll`.
- **Quickdraw** (Archer first-attack +50% crit) is its own bag —
  `round(FirstAttackCritChance × 20)` crit marbles of 20 — drawn only on the caster's first
  offensive action of a battle, OR-ed with the base result. `RollQuickdraw` returns false
  without advancing the bag when the chance is 0 (bag advancement is not contract-bound; the
  caller's RNG consumption is).
- **Persistence**: `CritBagSet` lives on the domain objects (`Hero`/`Mercenary`, via
  `ICombatant.CritBags`), so pity carries across battles. It deliberately survives
  `ClearBattleState` (clears only buffs) and `CombatantPassiveApplier.ResetAndApply` (zeroes
  only scalar passive fields). The quickdraw bag rebuilds lazily whenever the observed
  `FirstAttackCritChance` differs from the chance it was built for — that is how it tracks
  equip/level/skill changes without any hook into the passive applier.
- Virtual sim needs no special wiring: `VirtualGameSimulation` holds persistent
  `Hero`/`Mercenary` objects and `BattleEngine` is shared verbatim.

Tuning knob: `BalanceConfig.BaseCritChance` (single constant; bag size 20 means the useful
granularity is multiples of 5%).

## Treasure-chest loot — `LootBagSet` + `LootShuffleService`

Files: `PitHero/Services/LootBagSet.cs`, `PitHero/Services/LootShuffleService.cs`,
`PitHero/ECS/Components/TreasureComponent.cs`, `PitHero/VirtualGame/VirtualPitGenerator.cs`.

`LootBagSet` owns the compositions and no RNG (all draws via `NextFromRoll`), so **one class
serves both layers**: live feeds it `Nez.Random` rolls, virtual feeds it per-run
`System.Random` rolls.

| Bag | Composition | Replaces |
|---|---|---|
| Cave rarity 11–14 | 7×L2 + 13×L1 (of 20) | `CaveBiomeConfig.DetermineCaveTreasureLevel` 35/65 |
| Cave rarity boss 15 | 12×L2 + 8×L1 | 60/40 |
| Cave rarity boss 20/25 | 4×L3 + 10×L2 + 6×L1 | 20/50/30 |
| Cave rarity 16–25 non-boss | 2×L3 + 7×L2 + 11×L1 | 10/35/55 |
| Seed gate | 1×true + 9×false | `SeedChestDropRate` 10% |
| Seed type | 1 marble per `CropType` | uniform pick (now full rotation) |
| Consumable gate (L1 cave) | 3×true + 2×false | `CaveConsumableDropRate` 60% |
| Potion type | HP/MP/Mix, 1 each | uniform pick (now strict rotation) |
| Accessory share (per rarity pool) | 1×true + 9×false | **new** — `BalanceConfig.AccessoryLootShare` 10% |
| Uncommon accessory pick | MagicChain/RingOfPower, 1 each | — |
| Epic index | 0–3 (PitLord set), 1 each | — |

Pit levels ≤ 10 are always L1 (no bag). `DrawCaveTreasureLevel` picks the band internally.

**Session ownership + null fallback**: `LootShuffleService` is registered in
`MainGameScene`'s service block. `LootShuffleService.LiveBags` is the access pattern
everywhere in `TreasureComponent`:

```csharp
var bags = LootShuffleService.LiveBags; // null when Core.Instance == null or service absent
```

**Null means fall back to the legacy pure-random path** — headless unit tests that construct
`TreasureComponent` without a scene rely on this. Never assume `LiveBags` is non-null, and
never let the two paths consume different RNG call counts.

**Accessory starvation fix**: cave equipment rolls consult the accessory-share bag first
(one extra `NextFloat` per cave equipment roll — chest generation is NOT contract-bound). On
true: common → ProtectRing, uncommon → MagicChain/RingOfPower rotation, rare →
NecklaceOfHealth (added to `_rarePoolKinds` as index 12 by #382 — it was previously in no
pool at all). On false: the existing job-weighted pool walk runs with accessory weights
zeroed (`excludeAccessories: true`) so accessories don't double-dip.

**Deterministic twins**: `GenerateCaveItemForTreasureLevelDeterministic` (and friends) take an
optional `LootBagSet` parameter and roll via `(float)rng.NextDouble()` — this is the virtual
path.

## Biome-boss epic chest

Files: `PitHero/AI/LiveBattleAdapter.cs` (`SpawnBossEpicChest`), `PitHero/TreasureSpawner.cs`,
`PitHero/VirtualGame/VirtualBattleRunner.cs`.

Every **Molten Titan** kill (the Cave biome main boss, all tier repeats) spawns a purple
chest at the boss tile containing one Epic PitLord item; the epic bag guarantees all four
pieces (Sword/Armor/Aegis/Crown) before any repeat. Tier ≥ 2 runs tier-scale the gear
exactly like chest loot (`Gear.CreateTierScaledCopy`).

Sharp edges, in order of sharpness:

1. **`OnEnemyDefeated` runs mid-battle inside the engine coroutine, so the epic draw must
   consume ZERO `Nez.Random`.** `LootShuffleService.DrawEpicItem()` uses a private
   `System.Random` (same rule as `SpeechBubbleSystem.md`). Anything else added to this hook
   must obey the same rule.
2. The boss entity is still alive at this point (destroy happens later in
   `ShowMonsterDeath`), so `GetEntityForEnemy(enemy)` anchors the chest tile; fallback is the
   hero's tile.
3. **GOAP retarget**: after spawning, `_heroComponent.ExploredPit = false` (it's a latch) and
   `AdjacentToChest = CheckAdjacentToChest()` so the post-battle replan (Idle_Enter/Idle_Tick)
   sees the new CLOSED chest and goes to open it.
4. `TreasureSpawner.SpawnTreasureChestAtTile(scene, tile, item)` sets `ContainedItem` +
   `Level` (from rarity — Epic ⇒ 4, purple) directly and does **not** call
   `InitializeForPitLevel`; `TreasureComponent.OnAddedToEntity` self-installs renderers,
   compositor, and `FogHideableComponent`.

**Virtual parity**: `VirtualBattleRunner` captures the Molten Titan + tile before the run
(the sink removes dead monsters — `VirtualWorldState.TryGetMonsterPosition`), then after
`RunToCompletion` draws from the shared `LootBagSet` with a runner-owned seeded
`System.Random` and calls `_world.AddTreasure(bossTile, item)`. `LootBags == null` ⇒ no epic
drop (keeps legacy tests untouched). `CurrentPitTier` must be set by the caller for scaling.

## Tavern patron dishes

Files: `PitHero/Services/KitchenTaskCoordinator.cs` (`PickPatronDish`),
`PitHero/ECS/Components/KitchenMonsterStateMachine.cs` (`TakeOrderAtTarget`).

Walk-in patrons draw from a persistent full-menu bag weighted **inversely to price**:
`marbles(d) = max(1, round(maxPrice / price(d)))`. Cheap dishes dominate; the priciest dish
still cycles through on a bounded cadence. Selection is **bounded draw-and-skip**: up to
`Count` draws of `_dishBag.Next()` (this stream is not contract-bound, so `Next()` is fine),
first drawn dish present in the orderable list wins; skipped unorderable marbles restore next
cycle so pricey-dish pity persists across stock fluctuations; a fully unorderable cycle falls
back to a uniform pick.

## Persistence: none, by design

All bags are transient in-memory state — they refill fresh each session and are **never
saved** (save format untouched by #382, stays v27). Crit pity resets on load; chest bags
reset with the scene (chests themselves aren't persisted — the pit regenerates); the dish bag
rebuilds lazily. Do not add bag state to the save format without revisiting this decision.

## Recipe: adding a new bag-driven drop

1. **Decide which RNG stream the call site lives on.**
   - Battle path (`BattleEngine`, anything reachable from the engine coroutine): the call
     count per action is a contract. Consume the same fixed number of `Nez.Random` floats on
     every code path (even when the result is ignored) and feed them to `NextFromRoll`.
     Update the contract paragraph in `VirtualGameLogicLayer.md`.
   - Mid-battle side effects (`OnEnemyDefeated` etc.): zero `Nez.Random` — use a private
     `System.Random`.
   - Everything else (chest spawn, kitchen, cosmetics): free to add rolls; `Next()` is fine.
2. **Pick the owner for the bag's lifetime** — pity scope is ownership scope: per-combatant
   (`CritBagSet` on Hero/Merc), per-session (`LootShuffleService`), per-sim-run
   (`VirtualGameSimulation`'s shared `LootBagSet`), per-coordinator (dish bag).
3. **Mirror the legacy rate exactly** in the composition (denominator 20 or the rate's
   natural denominator) and keep a pure-random fallback when the owner can be absent
   (headless tests) — follow the `LiveBags` null-fallback pattern.
4. **Wire the virtual layer** if the live path has a virtual counterpart: share ONE bag set
   per sim run (generators/runners are recreated per level — inject, don't construct) and
   feed it deterministic `System.Random` rolls.
5. **Write a conformance test** asserting the exact per-cycle composition (see
   `ShuffleBagLootTests` / `ShuffleBagTests`; mark `[DoNotParallelize]` if any path touches
   the shared `Nez.Random` stream).

## Tests

- `PitHero.Tests/ShuffleBagTests.cs` — primitive: exact multiset per cycle, refill,
  `NextFromRoll` determinism/boundaries, `Add` cursor reset.
- `PitHero.Tests/StubSkillTests.cs` — `RollCrit`: quickdraw fold-in, exactly-1-per-20 base
  cycle, bag survival across `ClearBattleState`, lazy quickdraw rebuild.
- `PitHero.Tests/ShuffleBagLootTests.cs` — every `LootBagSet` rate, epic 4-before-repeat,
  virtual Molten Titan chest spawn (+ negative test for non-main bosses), dish-bag
  inverse-price conformance.
- `BattleEngineTests` — the unweakened structural guards for the RNG contract.
