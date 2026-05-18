# Virtual Game Layer Architecture

Authoritative deep-dive: `VIRTUAL_GAME_LOGIC_LAYER.md` at the repo root. Below is a quick orientation.

## Folder

```
PitHero/VirtualGame/
├── VirtualGameSimulation.cs   — headless harness, the main entry point
├── VirtualHeroStateMachine.cs — virtual GOAP + FSM
├── VirtualGoapContext.cs      — IGoapContext implementation for tests
├── VirtualHeroController.cs   — virtual hero control surface
├── VirtualPathfinder.cs       — virtual A* implementation
└── ...                        — additional virtual components added per feature
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

- **Seeded RNG only.** `Nez.Random.SetSeed(seed)` at simulation start; never use `System.Random`.
- **No wall-clock time.** `Time.DeltaTime` is fine because the virtual layer advances it manually in `VirtualGameSimulation.Tick()`.
- **No coroutines** for headless tests — the virtual layer ticks synchronously. If an action uses coroutines for visual animation (e.g. jump arcs), gate them with a virtual-mode check or short-circuit them in the virtual context.

## Validating Changes

```bash
dotnet build PitHero.sln
dotnet test PitHero.Tests/PitHero.Tests.csproj
```

Look at the test count diff — your new virtual component should add focused tests, not blanket coverage of unrelated areas.

## Documenting Coverage

When you add or extend virtual-layer code, update `VIRTUAL_GAME_LOGIC_LAYER.md`:
- Note the new file(s) added
- Update the coverage table for the affected subsystem
- If new GOAP actions gained `Execute(IGoapContext)` support, list them

This documentation drift is the #1 source of "I thought we had coverage" confusion. Keep it accurate.
