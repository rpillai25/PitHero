---
name: nez-ai
description: "**DOMAIN SKILL** — Nez AI framework best practices and PitHero AI conventions. USE FOR: GOAP actions, action planning, state machines (SimpleStateMachine/SKStateMachine), behavior trees, utility AI, hero/mercenary AI, pathfinding integration (TileByTileMover, A*), GoapConstants, ActorState/LocationType, replanning/interrupts, virtual game layer AI testing (IGoapContext, VirtualHeroStateMachine), coroutines for GOAP animations. DO NOT USE FOR: UI code, rendering pipelines, non-AI ECS components."
applyTo: "**/AI/**,**/*StateMachine*,**/*Action*.cs,**/*Goap*,**/*GOAP*,**/VirtualGame/**"
---

# Nez AI — PitHero Conventions

PitHero uses **GOAP + SimpleStateMachine** following the F.E.A.R. pattern: a GOAP planner selects actions, a 3-state FSM (`Idle → GoTo → PerformAction`) orchestrates execution.

## CRITICAL RULES (Never Violate)

### GOAP Movement Rule (ABSOLUTE — NO EXCEPTIONS)

1. **ALL destination movement for GOAP actions MUST go through the GoTo state.** The GoTo state handles A* pathfinding and tile-by-tile movement via `TileByTileMover`. Never implement destination movement inside a GOAP action's `Execute()` method or in a separate coroutine.
2. **Once GoTo reaches the destination, THEN the action runs** in PerformAction. Flow is always: `Idle (plan) → GoTo (move) → PerformAction (execute)`.

**Why:** GoTo handles pathfinding, collision, enemy encounter interrupts, grid snapping, fog-of-war clearing, and enemy movement triggers. Bypassing it breaks the interrupt/replan system.

**Only exception:** Jump animations (JumpIntoPit, JumpOutOfPit) use coroutines for smooth arc movement — these are teleport-like transitions, not pathfinding.

### Other Critical Rules

3. **Use `GoapConstants` for all condition names** — strong-typed constants, never inline strings.
4. **Use `for` loops, never `foreach`** — AOT compliance.
5. **Pre-allocate collections** — avoid `new` during gameplay loops.
6. **Use `Nez.Time.DeltaTime`** for all timing.
7. **Use `Nez.Debug`** for all AI logging.

## State Flow

```
┌─────────┐  plan   ┌──────────┐  arrived  ┌────────────────┐
│  Idle   │ ──────→ │   GoTo   │ ────────→ │ PerformAction  │
│ (plan)  │         │ (move)   │           │   (execute)    │
└─────────┘         └──────────┘           └────────────────┘
     ↑                   │                         │
     │   interrupt       │     plan complete       │
     └───────────────────┴─────────────────────────┘
              (replan from Idle)
```

## What to Read Next (Progressive Disclosure)

| If you are working on… | Read |
|---|---|
| GOAP architecture, ActionPlanner setup, world/goal state, GoapConstants | `references/goap-architecture.md` |
| FSM patterns (SimpleStateMachine, SKStateMachine), replanning/interrupts | `references/state-machines.md` |
| Creating a new GOAP action, multi-frame phases, dynamic costs, coroutines, GoTo/CalculateTargetLocation | `references/action-types.md` |
| Mercenary AI — follow/jump/sleep patterns, freeze/unfreeze on hero death, follow-chain invariant | `references/mercenary-ai.md` |
| Behavior trees or Utility AI (Nez built-ins, not currently used in PitHero) | `references/behavior-trees.md` |
| Virtual game layer AI testing — `IGoapContext`, `VirtualHeroStateMachine`, dual execution | `references/virtual-layer-ai.md` |

## Quick Gotchas

| Gotcha | Fix |
|---|---|
| Action needs hero to walk somewhere | Add target to `CalculateTargetLocation()` — **never** move in `Execute()` |
| Planner can't find a plan | Check preconditions match current world state. Log with `LogActionPreconditions()` |
| Action never completes | Ensure `Execute()` eventually returns `true`. Check phase/timer/coroutine logic |
| Monster encounter during movement | GoTo_Tick handles this — transitions to Idle for replanning |
| New condition not working | Add to `GoapConstants`, set in `SetWorldState()`, check in `SetGoalState()` |
| Action cost ignored | Call `UpdateCost()` in `Idle_Enter()` before planning |
| Mercenary out of sync | Check `_expectedMercInPit` / `_expectedTargetInPit` replan detection |
| Virtual test doesn't run action | Implement `Execute(IGoapContext)` override on the action |

## File Reference

### State Machines & GOAP Infrastructure

| File | Purpose |
|---|---|
| `PitHero/AI/HeroStateMachine.cs` | Hero GOAP + FSM orchestrator |
| `PitHero/AI/MercenaryStateMachine.cs` | Mercenary GOAP + FSM |
| `PitHero/AI/HeroState.cs` | `ActorState` and `LocationType` enums |
| `PitHero/AI/GoapConstants.cs` | All GOAP condition/action name constants |
| `PitHero/AI/HeroActionBase.cs` | Base class for hero actions |
| `PitHero/AI/MercenaryActionBase.cs` | Base class for mercenary actions |

### Virtual Layer

| File | Purpose |
|---|---|
| `PitHero/VirtualGame/VirtualHeroStateMachine.cs` | Test FSM |
| `PitHero/VirtualGame/VirtualGoapContext.cs` | Test GOAP context |
| `PitHero/VirtualGame/VirtualHeroController.cs` | Test hero controller |
| `PitHero/VirtualGame/VirtualPathfinder.cs` | Test A* pathfinder |

### Hero Actions (12) / Mercenary Actions (5)

See `references/action-types.md` for the full table including costs and movement requirements.

## Nez AI System Comparison

| System | Complexity | Best For | Used in PitHero? |
|---|---|---|---|
| **SimpleStateMachine** | Low | Enum-driven FSMs (`{Enum}_{Enter\|Tick\|Exit}`) | ✅ Hero/Merc FSMs |
| **SKStateMachine** | Medium | States-as-objects pattern | ❌ Not used |
| **Behavior Trees** | Medium-High | Decision trees | ❌ Available |
| **GOAP** | High | Goal-driven planning | ✅ Action selection |
| **Utility AI** | Highest | Scoring-based decisions | ❌ Available |
