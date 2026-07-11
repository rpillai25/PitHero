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

The simulation is invoked from C# test code. Canonical pattern (real combat via the
shared `BattleEngine`, issue #296):

```csharp
// Seeded constructor => deterministic combat rolls (turn order, evasion/variance,
// target picks, crit/deflect). Pit layout is already deterministic per level.
var sim = new VirtualGameSimulation(rngSeed: 12345);

// Hero level should track BalanceConfig.EstimatePlayerLevelForPitLevel(pitLevel);
// stats scale with level automatically via GrowthCurveCalculator.
sim.ConfigureHero(new Knight(), level: 8, new StatBlock(10, 8, 10, 4));
sim.ConfigureMercenaries(mercList); // optional, up to 2 real Mercenary instances

var metrics = sim.RunPitLevel(5);   // full traversal: explore, fight, boss gate, orb

// metrics: BattleCount, TotalRounds, DamageDealt/Taken, HpLossPercent,
//          HealingConsumed, PartyDeaths, Wiped, RngSeed, JobName
VirtualRunMetrics.WriteCsvHeader(writer);
metrics.WriteRow(writer);
```

See `PitHero.Tests/VirtualBalanceTraversalTests.cs` for a working sampled traversal
(levels 1/5/10/15/20/25 including all Cave boss floors) and the same-seed
reproducibility contract. Run it with:
`dotnet test PitHero.Tests/PitHero.Tests.csproj --filter "TestCategory=BalanceTraversal"`

## When Coverage is Missing

If the virtual layer **doesn't support** a piece of functionality you need to test (a new boss type, a new item interaction, a new battle mechanic), STOP and report the gap. The `virtual-game-layer` skill is responsible for filling coverage gaps; you don't add new virtual code in this skill.

## Repeatability

- Use a **deterministic seed** for `Nez.Random` when running tests so runs are reproducible.
- Capture seed + job + traversal range at the top of every balance report.
- Re-run the same seed after rebalance changes to verify the fix.

## Performance Note

Balance traversals can be long. Prefer:
- Running multiple jobs in parallel (separate `VirtualGameSimulation` instances)
- Sampling at chosen pit levels rather than logging every frame
- Streaming a CSV/log file rather than keeping all events in memory
