# Mercenary AI — GOAP, Follow, Jump, Sleep, Freeze

Mercenaries use the same GOAP + 3-state FSM pattern as the hero (`Idle → GoTo → PerformAction`), driven by `MercenaryStateMachine` with a small action set.

## Action Set (5 total)

| Action | Purpose | Key Pre/Post |
|---|---|---|
| `FollowTargetAction` | Ensures `MercenaryFollowComponent` is attached; continuous (never returns true) | Post: `MercenaryFollowingTarget = true` |
| `WalkToPitEdgeAction` | Walk to pit boundary tile via direct pathfinding | Pre: `!MercenaryInsidePit && TargetInsidePit` → Post: `MercenaryAtPitEdge = true` |
| `MercenaryJumpIntoPitAction` | Coroutine jump arc (2 tiles left), clears fog at landing | Pre: `MercenaryAtPitEdge && !MercenaryInsidePit` → Post: `MercenaryInsidePit = true` |
| `MercenaryJumpOutOfPitAction` | Coroutine jump arc (2 tiles right), clears fog | Pre: `MercenaryInsidePit && !TargetInsidePit` → Post: `MercenaryInsidePit = false` |
| `WalkToHeroStatueAction` | Walk to hero statue for promotion | (Used by promotion flow) |

GOAP constants live in `GoapConstants.cs`: `MercenaryInsidePit`, `TargetInsidePit`, `MercenaryFollowingTarget`, `MercenaryAtPitEdge`.

## Single-Responsibility Components

- **`MercenaryFollowComponent`** — A* pathfinding only. Tracks the target's `LastTilePosition` (works for hero-following or merc-follows-merc chains). No pit logic, no jump logic, no state.
- **`MercenaryStateMachine`** — High-level GOAP planning. Decides follow vs walk-to-edge vs jump. Tracks pit status of self and target.

## `FollowTargetAction` — The "Continuous Action" Pattern

`FollowTargetAction.Execute` **never returns `true`** — it's a continuous state, not a one-shot:

```csharp
public override bool Execute(MercenaryComponent mercenary)
{
    var followComponent = mercenary.Entity.GetComponent<MercenaryFollowComponent>();
    if (followComponent == null)
        followComponent = mercenary.Entity.AddComponent(new MercenaryFollowComponent());
    return false;   // Continuous — replanning is what ends this state
}
```

This means the action stays active until the state machine replans (e.g., target jumps into pit).

## Dynamic Replanning

`MercenaryStateMachine.PerformAction_Tick` watches for unexpected world-state changes and forces replanning:

```csharp
if (_expectedTargetInPit != targetActuallyInPit)
    CurrentState = ActorState.Idle;   // Replan immediately
```

Replans trigger when:
- Following and target jumps into/out of pit
- Walking to pit edge and target exits pit
- Jumping and target changes location

## `WorldState` Initialization (Critical)

`WorldState` is a struct that holds a `planner` field. Always construct via the factory:

```csharp
var state = WorldState.Create(_planner);   // CORRECT
var state = new WorldState();              // NRE on state.Set() — planner is null
```

## Sleep Behavior

`SleepInBedAction.SleepCoroutine` positions hired mercenaries in beds, restores HP/MP, and re-enables follow:

1. **Disable follow first** — `MercenaryFollowComponent.Enabled = false` before any position change
2. **Force-stop in-flight movement** — call `TileByTileMover.StopMoving()` if `IsMoving` (otherwise a queued tile-step completes after teleport and the merc drifts off the bed)
3. **Teleport to bed** — assign `Transform.Position`, call `SnapToTileGrid()`, then update `LastTilePosition` to the bed tile so the follow component doesn't pathfind from a stale position later
4. **Restore HP/MP** — `Mercenary.Heal(deficit)` and `Mercenary.RestoreMP(deficit)` after the 10-second sleep
5. **Re-enable follow** — set `Enabled = true` and call `ResetPathfinding()` to clear stale path data; the merc will recalc on next tick

Bed positions (constants in `GameConfig`-style code): Mercenary 1 → (76, 3), Mercenary 2 → (73, 7), Hero → (73, 3).

The `MercenaryFollowComponent.Update()` early-returns when `Enabled == false`, so the sleeping merc stays put without competing pathfinding.

## Freeze / Unfreeze on Hero Death

Hired mercenaries persist across hero deaths via a freeze + reassign pattern.

### `MercenaryManager` API

| Method | When Called | Effect |
|---|---|---|
| `BlockHiring()` | `HeroDeathComponent.ExecuteDeathAnimation` after vault transfer | `CanHireMore()` returns false; click handlers no-op |
| `FreezeAllHiredMercenaries()` | Same call site, immediately after `BlockHiring()` | Disables state machine + follow component + tile mover; clears follow target |
| `UnblockHiring()` | `HeroPromotionService.ExecutePromotionSequence` after promotion completes | Re-enables hiring |
| `UnfreezeAndReassignMercenaries(Entity newHero)` | Same call site | Reassigns: merc 1 → new hero, merc 2 → merc 1 (chain preserved); re-enables state machine + follow + tile mover |

### Why disable three components, not just one

- **State machine** disabled → AI stops planning (no actions queued during dead-hero period)
- **Follow component** disabled → no pathfinding attempts
- **Tile mover** disabled → no in-flight movement completes

If you only disabled one, the others would keep ticking and the merc would drift or stutter while the death animation plays.

### Follow-Chain Invariant

After unfreeze, the chain is always `new hero ← merc1 ← merc2`. The freeze preserves the merc's *position* (they stay where they were when the hero died) but reassigns *follow targets* on unfreeze — they don't try to remember the old chain.

## Hiring During Tavern Walk

When a mercenary is hired while still walking to its tavern position, the `WalkToTavern` coroutine checks `IsHired` and yields-break early so the `MercenaryStateMachine` can take control immediately:

```csharp
if (mercComponent.IsHired)
    yield break;
```

Without this, the merc would finish walking to the tavern *after* being hired, which looks broken.

## Logging Prefix

All `MercenaryManager` calls use `[MercenaryManager]` as the debug prefix. State machine uses `[MercenaryStateMachine]`. Follow component uses `[MercenaryFollow]`.

## Key Files

| File | Role |
|---|---|
| `PitHero/AI/MercenaryStateMachine.cs` | GOAP + FSM orchestrator |
| `PitHero/AI/MercenaryActionBase.cs` | Base class with `Execute(MercenaryComponent)` |
| `PitHero/AI/FollowTargetAction.cs` | Continuous follow (returns false) |
| `PitHero/AI/WalkToPitEdgeAction.cs` | Path to pit edge |
| `PitHero/AI/MercenaryJumpIntoPitAction.cs` | Coroutine jump arc |
| `PitHero/AI/MercenaryJumpOutOfPitAction.cs` | Coroutine jump arc |
| `PitHero/ECS/Components/MercenaryFollowComponent.cs` | Pure pathfinding component |
| `PitHero/Services/MercenaryManager.cs` | Hiring, freeze/unfreeze, chain assignment |
