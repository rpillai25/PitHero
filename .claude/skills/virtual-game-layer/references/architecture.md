# Virtual Game Layer Architecture

Authoritative deep-dive: `PitHero/docs/VirtualGameLogicLayer.md`. Below is a quick orientation.

## Folder

```
PitHero/VirtualGame/
├── VirtualGameSimulation.cs   — headless harness, the main entry point
├── VirtualHeroStateMachine.cs — virtual GOAP + FSM (wander/monster/chest sweeps)
├── VirtualGoapContext.cs      — IGoapContext implementation for tests
├── VirtualHeroController.cs   — virtual hero control surface
├── VirtualPathfinder.cs       — virtual A* implementation
├── VirtualBattle*.cs          — combat: runner/sink/party view/allies/metrics
└── ...                        — additional virtual components added per feature

PitHero/Combat/                — the SHARED headless BattleEngine + sink interfaces
                                 (used by both live play and the virtual layer)
```

## State Mirroring

The virtual layer mirrors live state via interfaces:

| Live Concept | Interface | Virtual Implementation |
|---|---|---|
| Hero component | `IHeroController` | `VirtualHeroController` |
| World state | `IWorldState` | `VirtualWorldState` |
| Pathfinder | `IPathfinder` | `VirtualPathfinder` |
| Pit level manager | `IPitLevelManager` | virtual implementation |
| Tiled map service | `ITiledMapService` | virtual implementation |
| Pit generator | `IPitGenerator` | virtual implementation |
| Pit width manager | `IPitWidthManager` | virtual implementation |

GOAP actions consume these via `IGoapContext` and never directly reach into Nez/FNA APIs — that's what makes them testable.

## Adding a Virtual Counterpart

When adding a new virtual class:

1. **Mirror the interface.** Find the live class that exposes the relevant state. Decide which methods/properties need an interface and which test code will actually call.
2. **Extract an interface** if one doesn't exist yet. The live class implements it; the virtual class implements it.
3. **Implement deterministically.** No `System.Random` (use `Nez.Random` with a seed). No filesystem reads. No real-time delays.
4. **Wire into `VirtualGoapContext`** if AI actions need access. Add the property and route it from `VirtualGameSimulation`.
5. **Test it.** Add a unit test under `PitHero.Tests/` that exercises the new virtual class in isolation.

## Determinism Rules

- **Two sanctioned RNG streams** (see the determinism model in `VirtualGameLogicLayer.md`):
  combat/hire rolls use the global `Nez.Random`, seeded via `new VirtualGameSimulation(rngSeed)`;
  pit layout + loot contents use a local `Random(level)` inside the generators
  (deterministic per level). Don't introduce a third source of randomness.
- **No wall-clock time.** `Time.DeltaTime` is fine because the virtual layer advances it manually in `VirtualGameSimulation.Tick()`.
- **Coroutines**: the shared `BattleEngine` is a coroutine drained synchronously by
  `HeadlessCoroutineRunner.RunToCompletion` (the virtual sink returns null for every
  display/timing hook). Follow that pattern for shared live/virtual logic; for
  virtual-only code, tick synchronously and short-circuit visual-animation coroutines.

## Validating Changes

```bash
dotnet build PitHero.sln
dotnet test PitHero.Tests/PitHero.Tests.csproj
```

Look at the test count diff — your new virtual component should add focused tests, not blanket coverage of unrelated areas.

## Documenting Coverage

When you add or extend virtual-layer code, update `PitHero/docs/VirtualGameLogicLayer.md`:
- Note the new file(s) added
- Update the coverage table for the affected subsystem
- If new GOAP actions gained `Execute(IGoapContext)` support, list them

This documentation drift is the #1 source of "I thought we had coverage" confusion. Keep it accurate.
