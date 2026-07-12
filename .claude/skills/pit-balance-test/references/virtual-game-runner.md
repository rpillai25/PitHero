# Running the Virtual Game Layer for Balance Tests

`PitHero/VirtualGame/VirtualGameSimulation.cs` is the headless harness — the full game loop without graphics. It's the engine that drives balance tests.

## Authoritative Source

Always read `PitHero/docs/VirtualGameLogicLayer.md` for the full overview of the virtual layer (entry points, supported components, how `VirtualWorldState` mirrors the live `WorldState`).

## Test Entry Points

| File | Purpose |
|---|---|
| `PitHero/VirtualGame/VirtualGameSimulation.cs` | Top-level harness |
| `PitHero/VirtualGame/VirtualHeroStateMachine.cs` | Runs GOAP plans without Nez rendering |
| `PitHero/VirtualGame/VirtualGoapContext.cs` | GOAP context for virtual execution |
| `PitHero/VirtualGame/VirtualHeroController.cs` | Hero controller for virtual layer |
| `PitHero/VirtualGame/VirtualPathfinder.cs` | A* pathfinder for virtual layer |

## Running Unit Tests First

Always run before traversal:

```bash
dotnet test PitHero.Tests/
```

Critical test classes for balance:
- `BalanceSystemTests` — formula correctness
- `GearItemsTests` — equipment factory output
- `*JobStatGrowthTests` — per-job stat curves
- `CaveBiomeMonsterTests` — Cave-specific encounter checks
- Existing balance-related tests under `PitHero.Tests/`

## Driving the Simulation

The simulation is invoked from C# test code. **Read the Quick Start + gotchas in
`PitHero/docs/VirtualGameLogicLayer.md` first** — in particular the "fully-equipped party"
requirements (hero needs a JP-loaded `HeroCrystal` + purchased skills; manually-built
mercs need `LearnAllJobSkills()`; stock potions in `sim.Bag`) or results will show
`healing=0` and understate survivability.

Canonical single-level pattern (real combat via the shared `BattleEngine`, issue #296):

```csharp
// Seeded constructor => deterministic combat rolls (turn order, evasion/variance,
// target picks, crit/deflect). Pit layout is already deterministic per level.
var sim = new VirtualGameSimulation(rngSeed: 12345);

// Hero level should track BalanceConfig.EstimatePlayerLevelForPitLevel(pitLevel);
// stats scale with level automatically via GrowthCurveCalculator.
sim.ConfigureHero(new Knight(), level: 8, new StatBlock(10, 8, 10, 4), crystal);
sim.ConfigureMercenaries(mercList); // optional, up to 2 real Mercenary instances

var metrics = sim.RunPitLevel(5);   // full traversal: explore, fight, loot, boss gate, orb
```

Preferred for balance curves — a persistent full-game run (gold economy: auto-hire up to
2 mercs + inn rest between levels, chest loot + auto-equip during levels):

```csharp
var sim = new VirtualGameSimulation(rngSeed: 12345);
sim.ConfigureHero(new Knight(), level: 1, new StatBlock(10, 8, 10, 4), crystal);
for (int i = 0; i < 5; i++) sim.Bag.TryAdd(PotionItems.HPPotion());
List<VirtualRunMetrics> perLevel = sim.RunLevelRange(1, 25); // stops early on wipe

VirtualRunMetrics.WriteCsvHeader(writer);
for (int i = 0; i < perLevel.Count; i++) perLevel[i].WriteRow(writer);
// Columns: pitLevel,battles,rounds,dmgDealt,dmgTaken,hpLossPct,healing,deaths,wiped,
//          treasures,gearEquipped,goldEarned,wallet,innRested,mercsHired
```

See `PitHero.Tests/VirtualBalanceTraversalTests.cs` for working examples of all three
configurations (solo / party / persistent run) and the same-seed ⇒ identical-CSV
reproducibility contract. Run it with:
`dotnet test PitHero.Tests/PitHero.Tests.csproj --filter "TestCategory=BalanceTraversal"`

## When Coverage is Missing

If the virtual layer **doesn't support** a piece of functionality you need to test (a new boss type, a new item interaction, a new battle mechanic), STOP and report the gap. The `virtual-game-layer` skill is responsible for filling coverage gaps; you don't add new virtual code in this skill.

## Repeatability

- Use the **seeded constructor** — `new VirtualGameSimulation(rngSeed)` — which seeds
  `Nez.Random` for all combat and hire rolls. Pit layout/loot is separately deterministic
  per level (local `Random(level)`). Same seed ⇒ byte-identical metrics CSV (pinned by test).
- The seed is recorded in `VirtualRunMetrics.RngSeed` — capture seed + job + traversal
  range at the top of every balance report.
- Re-run the same seed after rebalance changes to verify the fix (before/after diff).
- Test-suite baseline: **12 known pre-existing failures** in `dotnet test` — anything
  above that is a regression introduced by the change under test.

## Performance Note

Balance traversals can be long. Prefer:
- Running multiple jobs in parallel (separate `VirtualGameSimulation` instances)
- Sampling at chosen pit levels rather than logging every frame
- Streaming a CSV/log file rather than keeping all events in memory
