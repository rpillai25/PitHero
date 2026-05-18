---
name: virtual-game-layer
description: "**DOMAIN SKILL** — Coverage analysis and implementation for the PitHero Virtual Game Layer (`PitHero.VirtualGame`). USE FOR: verifying that pit generation, monsters, equipment, items, mercenaries, and battle systems have headless/virtual counterparts before implementing or testing a new feature; identifying virtual-layer coverage gaps; implementing missing virtual components scoped to the current feature; producing Delta Plans for future virtual work; setting up inputs needed by the implementer and balance tester. DO NOT USE FOR: implementing gameplay code in the live (non-virtual) layer, designing monsters/equipment, running balance tests."
---

# Virtual Game Layer — Coverage Analysis

You verify that the Virtual Game Layer has **sufficient coverage** for testing a new feature, and implement the gaps when needed — but only the components relevant to the current feature. Don't speculatively virtualize unrelated systems.

## Core Constraints

- **Scope-bounded implementation.** Only fill gaps required by the current feature. Speculative virtual code creates maintenance load and bugs.
- **Coverage analysis is mandatory.** Even when no implementation is needed, you produce an explicit verdict (Delta Plan or All Clear).
- Creating/updating a planning artifact under `features/` is allowed.

## When to Run

After feature planning is complete and **before** the implementer writes gameplay code. The balance tester relies on you for a working virtual-layer.

## What to Read Next (Progressive Disclosure)

| If you are working on… | Read |
|---|---|
| Coverage checklist — what to inspect, what counts as "covered", priority order | `references/coverage-checklist.md` |
| Virtual-layer architecture — main types, where to add code, how state mirrors the live layer | `references/architecture.md` and the repo's `VIRTUAL_GAME_LOGIC_LAYER.md` |

## What Counts as "Covered"

A system has virtual-layer coverage when:

1. There is a virtual counterpart class (`VirtualX`) in `PitHero/VirtualGame/`.
2. That class exposes the same observable state used by tests.
3. State changes that matter for AI decision-making propagate to `VirtualWorldState` / `VirtualGoapContext`.
4. The `VirtualGameSimulation` can drive that subsystem deterministically (seeded RNG, no external dependencies).

Six systems are critical for balance testing:

1. **Pit generation** — `VirtualPitGenerator`-like surface
2. **Monsters** — virtual spawning + combat resolution
3. **Equipment** — virtual drops + stat application
4. **Items** — consumables in the virtual inventory
5. **Mercenaries** — virtual `MercenaryStateMachine` parity
6. **Battle** — virtual `EnhancedAttackResolver` parity

## Output

One of two outcomes:

### A. All Clear
The feature's components are already adequately covered. Produce a short verdict statement naming the components reviewed and explicit confirmation that no implementation is needed.

### B. Delta Plan
The feature has coverage gaps. Produce:

1. A list of **components added** in this iteration (scoped to the current feature only)
2. A **Delta Plan** for related coverage gaps that future features will need (don't implement these — record them)
3. Explicit **inputs** required by the implementer and balance tester:
   - File paths the implementer must call into
   - Method signatures available
   - Limitations to be aware of
4. The planning artifact path (typically `features/feature_<name>.md` virtual-layer section)

## Approach

1. Read `VIRTUAL_GAME_LOGIC_LAYER.md` for current scope.
2. Read the feature plan (in `features/`) to identify which systems are touched.
3. For each touched system, check the coverage checklist (`references/coverage-checklist.md`).
4. For each gap relevant to the feature:
   - Add the minimal virtual counterpart needed
   - Write a focused unit test or smoke test to confirm the virtual counterpart works
   - Update `VIRTUAL_GAME_LOGIC_LAYER.md` to reflect new coverage
5. Write the All Clear or Delta Plan output.

## Build/Test Validation

Always validate before handoff:

```bash
dotnet build PitHero.sln
dotnet test PitHero.Tests/PitHero.Tests.csproj
```

If any virtual-layer test fails, fix it before declaring coverage adequate.
