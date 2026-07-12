# Virtual Game Logic Layer

## Overview

The Virtual Game Logic Layer (`PitHero/VirtualGame/`, namespace `PitHero.VirtualGame`) is a
complete headless simulation of the game loop: pit generation, fog-of-war exploration, GOAP
planning, **real combat** (shared `BattleEngine`), traps, chest loot with auto-equip, and a
spendable gold economy (inn rest + mercenary hiring). It runs with no graphics, no Nez
`Core` instance, and no wall-clock time, and is the engine behind automated balance testing
(see the `pit-balance-test` skill).

Everything that affects combat balance is mirrored from the live layer as of issue #296
(PR #297). The simulation operates on **real** RPG-framework objects — `Hero`, `Mercenary`,
`IEnemy` via `EnemyFactory`, `IItem`/`IGear`, `ItemBag` — not stand-ins, so stat math, skills,
buffs, DoT, elements, and rewards are identical to live play by construction.

## Quick Start — Balance Runs

### Single pit level

```csharp
var sim = new VirtualGameSimulation(rngSeed: 12345);           // seeded => reproducible
sim.ConfigureHero(new Knight(), level: 8, new StatBlock(10, 8, 10, 4), crystal);
var metrics = sim.RunPitLevel(5);                              // explore, fight, loot, boss gate, orb
```

### Persistent full-game run (recommended for balance curves)

```csharp
var sim = new VirtualGameSimulation(rngSeed: 12345);
sim.ConfigureHero(new Knight(), level: 1, new StatBlock(10, 8, 10, 4), crystal);
// Wallet defaults to GameConfig.NewGameStartingGold; potions default to none — stock them:
for (int i = 0; i < 5; i++) sim.Bag.TryAdd(PotionItems.HPPotion());
var perLevel = sim.RunLevelRange(1, 25);                       // hires mercs + inn rests between levels
```

`RunLevelRange` reuses the same hero/mercs/bag/wallet across levels; between levels it
prunes dead mercenaries, hires random mercenaries while affordable (max 2), and takes an
inn rest when the party isn't at full HP/MP. It stops early on a party wipe.

### ⚠ Fully-equipped party — REQUIRED for representative results

Freshly constructed characters are **not** representative of live play. Without the steps
below, traversals show `healing=0`, understate survivability, and misstate difficulty:

1. **Hero skills** — a bare `new Hero(...)` can never learn skills. Bind a JP-loaded
   `HeroCrystal` and purchase the job kit:
   ```csharp
   var crystal = new HeroCrystal("RefCrystal", new Knight(), level, baseStats);
   crystal.EarnJP(1_000_000);
   sim.ConfigureHero(new Knight(), level, baseStats, crystal);
   var hero = sim.Hero.LinkedHero;
   for (int i = 0; i < hero.Job.Skills.Count; i++) hero.TryPurchaseSkill(hero.Job.Skills[i]);
   ```
2. **Mercenary skills** — call `merc.LearnAllJobSkills()` on any manually-constructed
   `Mercenary` (live `MercenaryManager` does this on every tavern spawn).
   Mercs hired via `TryHireRandomMercenary()` already have their skills.
3. **Potions** — stock `sim.Bag` (a live new game grants HP Potions).
4. **Hero level** — `BalanceConfig.EstimatePlayerLevelForPitLevel(pitLevel)` gives the
   expected level for a pit; stats scale with level automatically via
   `GrowthCurveCalculator`, so a fixed base `StatBlock` is fine at any level.

The canonical working example is `PitHero.Tests/VirtualBalanceTraversalTests.cs`
(`[TestCategory("BalanceTraversal")]`): solo, party, and persistent-run CSV tables plus the
same-seed reproducibility contract. Run it with:

```bash
dotnet test PitHero.Tests/PitHero.Tests.csproj --filter "TestCategory=BalanceTraversal"
```

## Determinism Model

Two independent RNG streams — both reproducible, by different mechanisms:

| Stream | Source | Determinism |
|--------|--------|-------------|
| Pit layout + loot contents | local `new Random(level)` inside `VirtualPitGenerator` | Deterministic **per pit level**, always |
| Combat rolls (turn order, evasion/variance, target picks, crit/deflect) + hire rolls | global `Nez.Random` | Deterministic when constructed via `VirtualGameSimulation(rngSeed)`; the default ctor captures the ambient seed |

The seed is recorded in `VirtualRunMetrics.RngSeed`; cite it in every balance report.
Same seed ⇒ byte-identical metrics CSV (pinned by test). Note: live play uses unseeded
`Nez.Random` for pit generation, so virtual layouts intentionally differ from any specific
live session while following the same formulas.

## Architecture

### Simulation components (`PitHero/VirtualGame/`)

| Component | Responsibility |
|-----------|---------------|
| `VirtualGameSimulation` | Top-level harness: `ConfigureHero/ConfigureMercenaries/ConfigureStartingGold`, `RunPitLevel`, `RunLevelRange`, `TryInnRest`, `TryHireRandomMercenary`, `Gold`, `Bag`, `Metrics`, `RngSeed` |
| `VirtualWorldState` | Tile grid, fog, collision, entity positions **plus instances**: `Dictionary<Point, IEnemy>` monsters, `Dictionary<Point, IItem>` treasures, `HashSet<Point>` traps; queries (`GetLivingMonstersAdjacentTo`, `HasLivingBoss`, `GetNearestTreasurePosition`, …) |
| `VirtualPitGenerator` | Live-parity content: `EnemyFactory` monsters with `CaveBiomeConfig` level scaling, boss floors (5=StoneGuardian, 10=EarthElemental, 15=AncientWyrm, 20=PitLord, 25=MoltenTitan), real chest items with live treasure formulas **and party-job loot weighting** (`LootContext`), traps |
| `VirtualHero` | GOAP flags + position + `LinkedHero` (real `Hero`) via `ConfigureHero` |
| `VirtualHeroStateMachine` | Idle→GoTo→PerformAction mirror; wander phases: fog sweep → connectivity sweep → **monster sweep** → **chest sweep**; per-tile trap handling; boss-gated orb activation |
| `VirtualBattleRunner` | Drives `BattleEngine` headlessly: `RunAdjacentBattle()`, `CollectChestItem()` (bag + auto-equip cascade), `ApplyTrapDamageToHero()`, counters (`TreasuresOpened`, `GearEquipped`) |
| `VirtualBattlePartyView` | `IBattlePartyView` — replicates `HeroComponent` critical-HP/burst thresholds with the same `GameConfig` constants |
| `VirtualBattleSink` | `IBattleEventSink` — aggregates `VirtualBattleMetrics`, removes dead monsters from world state, credits gold |
| `VirtualBattleMetrics` / `VirtualRunMetrics` | Per-battle / per-level aggregates + CSV export |
| `VirtualMercenaryLevelRoller` | Mirrors `MercenaryManager`'s hire-level distribution + six-job pool (via `Nez.Random`, AOT-safe switch) |
| `VirtualGoapContext` + `Virtual*Manager/Service` | `IGoapContext` wiring so GOAP actions run dual-path (`Execute(IGoapContext)`) |

### The headless BattleEngine (`PitHero/Combat/`)

The battle round loop lives in `PitHero.Combat.BattleEngine`, extracted from
`AttackMonsterAction` (which is now only the live Nez adapter). One code path serves both
layers:

- The engine is a coroutine (`IEnumerator Run(hero, mercs, monsters, heroActionQueue)`).
  Every yield is either an enumerator returned by the `IBattleEventSink` (display/pacing)
  or null. **Live**: `Core.StartCoroutine` via `LiveBattleAdapter` — real pacing/animations.
  **Virtual**: `HeadlessCoroutineRunner.RunToCompletion` — the virtual sink returns null
  everywhere, so battles resolve instantly.
- `IBattlePartyView` supplies party settings (tactic, bag, heal priorities, critical/burst
  checks); `IBattleAlly` wraps combatants (`IsPresent` = in-pit, deliberately WITHOUT an
  HP check — see contract below); monsters are plain `IEnemy`.
- Pure reward math (XP/JP/SP, merc XP) is applied inside the engine; side effects
  (gold, services, analytics, display) go through the sink.

**⚠ Behavior-preservation contract:** the engine is a verbatim retype of the original live
loop. The **sequence of `Nez.Random` calls is a compatibility contract** — adding, removing,
or reordering any RNG call (turn values, target picks, crit/deflect rolls, resolver
evasion/variance) changes live gameplay outcomes. Preserved quirks that must not be
"fixed" casually: a hero who dies mid-round still takes their queued turn that round
(hero turn-skip is pit-presence only); mercenary heals use `UseMP` while the hero uses
`SpendMP`; merc kills award rewards with `heroKill=false` (no `InnExhausted` reset);
`DEBUG_DAMAGE_MULT` applies to monster→ally damage only. `BattleEngineTests` pins a
same-seed ⇒ identical-event-stream determinism test that catches accidental drift.

## Gold Economy

- **Wallet**: `Gold` starts at `GameConfig.NewGameStartingGold`; battle gold credits it
  after each level; `ConfigureStartingGold` overrides for mid-game scenarios.
- **`TryInnRest()`** mirrors `SleepInBedAction`: `GameConfig.InnCostGold` (10 g) deducted;
  hero + all mercenaries restored to full HP **and MP**; `HealingItemExhausted` /
  `HealingSkillExhausted` reset. Instant — walking is not simulated. Returns false when
  unaffordable (live `InnExhausted` semantics).
- **`TryHireRandomMercenary()`** mirrors `MercenaryManager.SpawnMercenary` + hire:
  weighted level distribution around the hero's level, random job from the six primary
  jobs, `StatBlock(4, 3, 5, 1)`, `LearnAllJobSkills()`, cost via
  `BalanceConfig.CalculateMercenaryHireCost`, max 2 hired. Rolls use seeded `Nez.Random`.
  Names are deterministic (`"Merc1"`, `"Merc2"`, …).

## Chest Loot + Auto-Equip

- Chest **items are real `IItem`s** generated at pit-gen time with the live treasure-level
  formulas (`CaveBiomeConfig.DetermineCaveTreasureLevel` routing) and the live
  **party-job loot weighting** (`TreasureComponent.SelectWeightedPoolIndexDeterministic`
  with a `LootJobContext` built from the configured party).
- During traversal the hero collects chests adjacent to each landing tile; the chest-sweep
  wander phase guarantees every chest is opened before a level completes.
- `VirtualBattleRunner.CollectChestItem` mirrors live `OpenChestAction`: bag insert with
  consumable stacking, `HealingItemExhausted` reset on healing pickups, auto-equip hero
  first then mercenaries (`GearAutoEquipService`), with the recursive hand-me-down cascade
  for displaced gear. Toggles: `AutoEquipHero` / `AutoEquipMercenaries` (default true).

## Metrics & CSV Reference

`VirtualRunMetrics.WriteCsvHeader/WriteRow` emit one row per pit level:

| Column | Meaning |
|--------|---------|
| `pitLevel` | Level this row covers |
| `battles` | Battles fought |
| `rounds` | Total combat rounds |
| `dmgDealt` / `dmgTaken` | Ally→monster / monster→ally damage totals |
| `hpLossPct` | `dmgTaken ÷ party max-HP pool` at level start |
| `healing` | HP restored by skills + consumables |
| `deaths` | Party deaths (hero + mercs) |
| `wiped` | 1 when the hero died before completing the level |
| `treasures` / `gearEquipped` | Chests opened / gear pieces auto-equipped |
| `goldEarned` / `wallet` | Gold from kills this level / balance after crediting |
| `innRested` / `mercsHired` | Between-level actions taken **before** this level (`RunLevelRange` only) |

Run-level fields: `RngSeed`, `JobName`, `LevelRangeMin/Max`.

## Known Gotchas (learned the hard way)

1. **Per-level GOAP flags**: `RunPitLevel` resets `ExploredPit` / `FoundWizardOrb` /
   `ActivatedWizardOrb` at level start. Any new per-level flag must be reset the same way,
   or persistent runs will silently skip levels 2+ (guarded by per-level `BattleCount > 0`
   assertions in `VirtualBalanceTraversalTests`).
2. **Headless text lookups**: `Core.Services` throws without a game instance;
   `GetTextService()` in `Consumable`/`Gear`/`BaseJob`/`BaseSkill`/`CompositeJob` guards on
   `Core.Instance` and falls back to raw text keys. New localized-name lookups in
   RolePlayingFramework must follow the same pattern or headless runs crash.
3. **Fully-equipped party**: see Quick Start — bare heroes/mercs know no skills.
4. **No inn between manually-chained `RunPitLevel` calls**: HP/MP/bag/wallet persist;
   use `RunLevelRange` (or call `TryInnRest` yourself) for realistic multi-level runs.

## What Is Still Unmirrored

| Live feature | Status |
|--------------|--------|
| Walking/travel time (pit↔tavern↔inn), animations | Intentionally skipped — instant in virtual |
| Seed-crop chest contents (farming) | Level-2 chests never roll seeds virtually |
| Shop purchases (buying potions/gear) | No virtual shop; stock `Bag` manually |
| Night sleep (free time-of-day rest) | No `InGameTimeService` headlessly |
| `SecondChanceMerchantVault` on merc death/dismissal | Gear is not recovered virtually |
| Analytics JSONL (`AnalyticsService`) | Virtual sink aggregates metrics instead — same event payloads, comparable columns |
| Tavern seat management, `DeferredMercenary` | Not applicable headlessly |

New live-layer features should be checked against this document (see the
`virtual-game-layer` skill) and added here when they lack a virtual counterpart.

## Test Map

| Test file | Covers |
|-----------|--------|
| `BattleEngineTests` | Headless engine: rewards, turn ordering, Untargetable anti-stall, deflect/counter, Quickdraw crit, DoT, buff-leak guard, same-seed determinism |
| `VirtualBattleSimulationTests` | Traversal combat, boss orb-gating, wipes, merc participation, traps + TrapSense, chest collection/auto-equip, inn rest, hiring, `RunLevelRange` |
| `VirtualBalanceTraversalTests` | Balance curves (solo / party / persistent run), CSV output, same-seed reproducibility |
| `VirtualGameSimulationTests`, `VirtualWorldStateTests`, `CaveBiomeBalanceTests`, `InterfaceBased*Tests` | Exploration, world state, generation parity, GOAP dual execution |

Full suite: `dotnet test PitHero.Tests/PitHero.Tests.csproj`. Baseline: **12 known
pre-existing failures** (environment-dependent tests) — anything above that is a regression.

## Depth Semantics and Pit Tiers (issue #291)

`VirtualPitGenerator.RegenerateForLevel` and `VirtualGameSimulation.RunPitLevel` /
`RunLevelRange` accept a **cumulative depth** argument — the absolute depth since the start of
the game, not a per-tier counter.

- Depths **≤ 25** are tier 1 and behave identically to the pre-#291 behaviour.
- Crossing depth 25 (i.e. the argument is 26) causes the simulation to auto-increment its
  internal tier counter and record the tier base level (`TierBaseLevel = 26`). Subsequent
  levels continue counting up, with enemy scaling derived from the full cumulative depth.
- `RunLevelRange(1, 50)` therefore spans tier 1 (depths 1–25) and tier 2 (depths 26–50)
  without any special configuration.
- The mercenary hire floor activates at tier ≥ 2: `RunLevelRange` will attempt to hire
  mercenaries from depth 26 onward even if the party is already full from tier 1, because
  mercenaries from earlier tiers may have died or been dismissed.

The effective depth formula used for balance joins is `(tier-1)*25 + pitLevel`, where
`pitLevel` is the within-tier counter (1–25). Both values are present in analytics events
(see `AnalyticsSchema.md`).

## Legacy Exploration Harness

`RunCompleteSimulation()` and the ASCII visualization (`GetVisualRepresentation()`)
predate the combat integration: a scripted level-40 explore→orb→regenerate cycle used by
`CompleteWorkflowDemonstrationTests`. They still work and are useful for debugging
exploration/GOAP issues, but balance work should use `RunPitLevel`/`RunLevelRange`.
