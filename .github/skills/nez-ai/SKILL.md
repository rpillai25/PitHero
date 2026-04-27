---
name: nez-ai
description: "**DOMAIN SKILL** — Nez AI framework best practices and PitHero AI conventions. USE FOR: GOAP actions, state machines (SimpleStateMachine/StateMachine), behavior trees, action planning, hero/mercenary AI, pathfinding integration, virtual game layer AI testing. DO NOT USE FOR: UI code, rendering, non-AI ECS components."
applyTo: "**/AI/**,**/*StateMachine*,**/*Action*.cs,**/*Goap*,**/*GOAP*,**/VirtualGame/**"
---

# Nez AI – Best Practices & PitHero Conventions

## Nez AI Framework Overview

Nez provides four AI systems of increasing complexity:

| System | Complexity | Best For |
|--------|-----------|----------|
| **SimpleStateMachine** | Low | Enum-driven FSMs with convention-based methods |
| **StateMachine (SKStateMachine)** | Medium | "States as objects" pattern for complex state logic |
| **Behavior Trees** | Medium-High | Decision trees with composites, decorators, and actions |
| **GOAP** | High | Goal-driven planning with preconditions/postconditions |
| **Utility AI** | Highest | Scoring-based decisions for many competing actions |

PitHero uses **GOAP + SimpleStateMachine** as its primary AI architecture, following the F.E.A.R. pattern of a GOAP planner with a 3-state FSM (Idle → GoTo → PerformAction).

---

## CRITICAL RULES (Never Violate)

### GOAP Movement Rule (ABSOLUTE — NO EXCEPTIONS)

1. **ALL destination movement for GOAP actions MUST go through the GoTo state in the state machine.** The GoTo state handles A* pathfinding and tile-by-tile movement via `TileByTileMover`. Never implement destination movement inside a GOAP action's `Execute()` method or in a separate coroutine.

2. **Once the GoTo state reaches the destination, THEN the action can be performed** in the PerformAction state. The state machine flow is always: `Idle (plan) → GoTo (move) → PerformAction (execute)`.

**Why:** The GoTo state handles pathfinding, collision detection, enemy encounter interrupts, grid snapping, fog-of-war clearing, and enemy movement triggers. Bypassing it creates movement bugs, collision issues, and breaks the interrupt/replan system.

**The ONLY exception** is jump animations (JumpIntoPit, JumpOutOfPit) which use coroutines for smooth arc movement — but these are teleport-like transitions, not pathfinding-based navigation.

### Other Critical Rules

3. **Use `GoapConstants` for all condition names** — strong-typed string constants, never inline strings.
4. **Use `for` loops instead of `foreach`** — AOT compliance requirement.
5. **Pre-allocate collections** — avoid `new` during gameplay loops.
6. **Use `Nez.Time.DeltaTime`** for all timing in AI code.
7. **Use `Nez.Debug`** for all AI logging.

---

## Architecture: GOAP + 3-State FSM (F.E.A.R. Pattern)

PitHero follows the F.E.A.R. AI architecture: a GOAP planner selects actions, and a 3-state FSM orchestrates execution.

### State Flow

```
┌─────────┐    plan found    ┌──────────┐   arrived    ┌────────────────┐
│  Idle   │ ──────────────→  │   GoTo   │ ──────────→  │ PerformAction  │
│ (plan)  │                  │ (move)   │              │   (execute)    │
└─────────┘                  └──────────┘              └────────────────┘
     ↑                            │                           │
     │         interrupt          │                           │
     │←──────────────────────────┘                           │
     │          (replan)                                      │
     │←──────────────────────────────────────────────────────┘
                            plan complete or empty
```

### ActorState Enum

```csharp
public enum ActorState
{
    Idle,           // Run GOAP planner, produce action stack
    GoTo,           // Navigate to target tile via A* + TileByTileMover
    PerformAction   // Execute the current action
}
```

### LocationType Enum

Used by `CalculateTargetLocation()` to map actions to destinations:


```csharp
public enum LocationType
{
    None, PitOutsideEdge, PitInsideEdge, PitWanderPoint,
    WizardOrb, PitRegenPoint, TownWanderPoint, Bed,
    Inn, ItemShop, WeaponShop, ArmorShop, TavernSeat
}
```

---

## SimpleStateMachine (Nez Built-in)

Enum-driven FSM where method names follow the convention `{EnumValue}_{Enter|Tick|Exit}`:

```csharp
public class MyAI : SimpleStateMachine<ActorState>
{
    void OnAddedToEntity()
    {
        InitialState = ActorState.Idle;
    }

    void Idle_Enter() { /* plan actions */ }
    void Idle_Tick()  { /* retry planning if needed */ }
    void Idle_Exit()  { }

    void GoTo_Enter() { /* calculate path */ }
    void GoTo_Tick()  { /* step along path */ }
    void GoTo_Exit()  { /* snap to grid, clear path */ }

    void PerformAction_Enter() { /* start executing action */ }
    void PerformAction_Tick()  { /* poll action completion */ }
    void PerformAction_Exit()  { }
}
```

### StateMachine ("States as Objects")

For more complex scenarios where each state needs its own class:

```csharp
var machine = new SKStateMachine<SomeClass>(context, new PatrollingState());
machine.AddState(new AttackState());
machine.AddState(new ChaseState());

// In update loop:
machine.Update(Time.DeltaTime);

// Change state:
machine.ChangeState<ChasingState>();
```

---

## GOAP System

### GoapConstants — Strong-Typed Conditions

All GOAP state/condition names are centralized in `GoapConstants.cs`:

```csharp
public class GoapConstants
{
    // World states
    public const string HeroInitialized = "HeroInitialized";
    public const string PitInitialized = "PitInitialized";
    public const string InsidePit = "InsidePit";
    public const string OutsidePit = "OutsidePit";
    public const string ExploredPit = "ExploredPit";
    public const string FoundWizardOrb = "FoundWizardOrb";
    public const string ActivatedWizardOrb = "ActivatedWizardOrb";
    public const string AdjacentToMonster = "AdjacentToMonster";
    public const string AdjacentToChest = "AdjacentToChest";
    public const string HPCritical = "HPCritical";
    public const string HealingItemExhausted = "HealingItemExhausted";
    public const string HealingSkillExhausted = "HealingSkillExhausted";
    public const string InnExhausted = "InnExhausted";
    public const string NeedsCrystal = "NeedsCrystal";
    public const string StoppedAdventure = "StoppedAdventure";
    public const string SeatedInTavern = "SeatedInTavern";

    // Action names (used as constructor argument)
    public const string JumpIntoPitAction = "JumpIntoPitAction";
    public const string WanderPitAction = "WanderPitAction";
    // ... etc
}
```

**Rule:** When adding new GOAP conditions, always add them to `GoapConstants` first.

### ActionPlanner Setup

Register all actions in the state machine constructor:

```csharp
public HeroStateMachine()
{
    _planner = new ActionPlanner();

    // Register actions (order doesn't matter — planner uses cost + conditions)
    _planner.AddAction(new JumpIntoPitAction());
    _planner.AddAction(new WanderPitAction());
    _planner.AddAction(new ActivateWizardOrbAction());
    _planner.AddAction(new AttackMonsterAction());
    _planner.AddAction(new OpenChestAction());

    // Actions with dynamic costs — keep references for updates
    _useHealingItemAction = new UseHealingItemAction();
    _planner.AddAction(_useHealingItemAction);

    InitialState = ActorState.Idle;
}
```

### Planning (Idle State)

```csharp
void Idle_Enter()
{
    // Update dynamic costs before planning
    UpdateHealingActionCosts();

    var currentWorldState = GetWorldState();
    var goalState = GetGoalState();

    // Plan returns Stack<Action> (ordered sequence)
    _actionPlan = _planner.Plan(currentWorldState, goalState);

    if (_actionPlan != null && _actionPlan.Count > 0)
        CurrentState = ActorState.GoTo;
}

private WorldState GetWorldState()
{
    var ws = WorldState.Create(_planner);
    _hero.SetWorldState(ref ws);  // Delegate to component
    return ws;
}

private WorldState GetGoalState()
{
    var goal = WorldState.Create(_planner);
    _hero.SetGoalState(ref goal);  // Priority-driven goal selection
    return goal;
}
```

### World State & Goal State (on HeroComponent)

```csharp
public override void SetWorldState(ref WorldState worldState)
{
    // Map boolean flags → GOAP atoms
    if (HeroInitialized)
        worldState.Set(GoapConstants.HeroInitialized, true);
    if (InsidePit)
        worldState.Set(GoapConstants.InsidePit, true);
    if (HPCritical)
        worldState.Set(GoapConstants.HPCritical, true);
    // ... etc
}

public override void SetGoalState(ref WorldState goalState)
{
    // Priority-ordered goals (first match wins)
    if (StoppedAdventure)
    {
        goalState.Set(GoapConstants.SeatedInTavern, true);
        return;
    }
    if (NeedsCrystal)
    {
        goalState.Set(GoapConstants.HasArrivedAtStatueForCrystal, true);
        return;
    }
    if (HPCritical && !AllHealingOptionsExhausted())
    {
        goalState.Set(GoapConstants.HPCritical, false);
        return;
    }
    // Default: explore + activate orb
    if (!ActivatedWizardOrb)
        goalState.Set(GoapConstants.ActivatedWizardOrb, true);
}
```

---

## Creating GOAP Actions

### Hero Action Pattern

Inherit from `HeroActionBase`. Set preconditions and postconditions in the constructor. Implement `Execute()` to return `true` when complete, `false` when in progress.

```csharp
public class MyNewAction : HeroActionBase
{
    public MyNewAction() : base(GoapConstants.MyNewAction, cost: 3)
    {
        // Preconditions: what must be true for planner to consider this action
        SetPrecondition(GoapConstants.InsidePit, true);
        SetPrecondition(GoapConstants.SomeCondition, true);

        // Postconditions: what becomes true after this action completes
        SetPostcondition(GoapConstants.SomeCondition, false);
        SetPostcondition(GoapConstants.AnotherCondition, true);
    }

    /// <summary>Executes the action. Returns true when complete.</summary>
    public override bool Execute(HeroComponent hero)
    {
        // Perform the action (hero is already at the target location)
        DoSomething(hero);
        return true;  // Complete in one frame
    }

    /// <summary>Virtual layer execution for testing.</summary>
    public override bool Execute(IGoapContext context)
    {
        // Interface-based execution for virtual testing
        context.LogDebug("[MyNewAction] Virtual execution");
        return true;
    }
}
```

### Registering a New Action

1. Add condition constants to `GoapConstants.cs`
2. Create the action class inheriting `HeroActionBase`
3. Register in `HeroStateMachine` constructor: `_planner.AddAction(new MyNewAction())`
4. Add destination mapping in `CalculateTargetLocation()` if the action needs movement
5. Set the corresponding flags on `HeroComponent` in `SetWorldState()` / `SetGoalState()`

### Multi-Frame Actions (Phase Pattern)

For actions that span multiple frames, use internal phase tracking:

```csharp
public class OpenChestAction : HeroActionBase
{
    private enum Phase { NotStarted, FacingWait, OpenedWait, Done }
    private Phase _phase = Phase.NotStarted;
    private float _timer;

    public override bool Execute(HeroComponent hero)
    {
        switch (_phase)
        {
            case Phase.NotStarted:
                // Setup phase
                FaceTarget(hero, _chestEntity.Transform.Position);
                _phase = Phase.FacingWait;
                _timer = GameConfig.TreasureOpenWait;
                return false;  // In progress

            case Phase.FacingWait:
                _timer -= Time.DeltaTime;
                if (_timer <= 0f)
                {
                    PerformAction();
                    _phase = Phase.OpenedWait;
                    _timer = GameConfig.TreasureOpenWait;
                }
                return false;  // In progress

            case Phase.OpenedWait:
                _timer -= Time.DeltaTime;
                if (_timer <= 0f)
                {
                    Reset();
                    return true;  // Complete!
                }
                return false;

            default:
                return true;
        }
    }

    private void Reset()
    {
        _phase = Phase.NotStarted;
        _timer = 0f;
    }
}
```

### Dynamic Cost Actions

Some actions have costs that change based on player priorities:

```csharp
// Keep reference for cost updates
_sleepInBedAction = new SleepInBedAction();
_planner.AddAction(_sleepInBedAction);

// Update costs before planning (in Idle_Enter)
private void UpdateHealingActionCosts()
{
    // Lower cost = higher priority in the plan
    _useHealingItemAction.Cost = hero.HealPriority1 == HealOption.Item ? 1 : 3;
    _useHealingSkillAction.Cost = hero.HealPriority1 == HealOption.Skill ? 1 : 3;
    _sleepInBedAction.Cost = hero.HealPriority1 == HealOption.Inn ? 1 : 3;
}
```

### Preventing Action Interruption

Override `ShouldNotOverride()` for actions that must complete without replanning:

```csharp
public override bool ShouldNotOverride() => true;  // Don't interrupt this action
```

---

## GoTo State — Navigation

The GoTo state handles ALL destination movement. It uses A* pathfinding and `TileByTileMover` for tile-by-tile grid movement.

### CalculateTargetLocation

Maps action names to destination tiles:

```csharp
private Point? CalculateTargetLocation(string actionName)
{
    switch (actionName)
    {
        case GoapConstants.JumpIntoPitAction:
            return new Point(pitRightEdge, GameConfig.PitCenterTileY);

        case GoapConstants.WanderPitAction:
            return CalculatePitWanderPointLocation();  // Next fog tile

        case GoapConstants.ActivateWizardOrbAction:
            return GetWizardOrbTile();

        case GoapConstants.SleepInBedAction:
            return new Point(GameConfig.InnPaymentTileX, GameConfig.InnPaymentTileY);

        // Actions that don't need movement
        case GoapConstants.AttackMonster:
        case GoapConstants.OpenChest:
        case GoapConstants.UseHealingItemAction:
        case GoapConstants.UseHealingSkillAction:
            return null;  // Skip GoTo, go straight to PerformAction

        default:
            return null;
    }
}
```

**When adding a new action:** If it requires the hero to move to a location first, add a case to `CalculateTargetLocation()` returning the target tile. If no movement is needed, return `null`.

### GoTo_Enter — Path Calculation

```csharp
void GoTo_Enter()
{
    var nextAction = _actionPlan.Peek();
    Point? target = CalculateTargetLocation(nextAction.Name);

    if (!target.HasValue)
    {
        CurrentState = ActorState.PerformAction;  // No movement needed
        return;
    }

    _targetTile = target.Value;
    _currentPath = _hero.CalculatePath(currentTile, _targetTile);  // A*
    _pathIndex = 0;

    if (_currentPath == null || _currentPath.Count == 0)
        CurrentState = ActorState.PerformAction;
}
```

### GoTo_Tick — Step-by-Step Movement

```csharp
void GoTo_Tick()
{
    if (IsBattleInProgress) return;  // Pause during combat

    var tileMover = _hero.Entity.GetComponent<TileByTileMover>();
    if (tileMover.IsMoving) return;  // Wait for current step

    // Check for adjacent enemies (interrupt → replan)
    if (_hero.AdjacentToMonster && !wasAdjacentBefore)
    {
        CurrentState = ActorState.Idle;  // Replan!
        return;
    }

    // Take next step
    if (_pathIndex < _currentPath.Count)
    {
        var nextTile = _currentPath[_pathIndex];
        var direction = CalculateDirection(curTile, nextTile);
        if (tileMover.StartMoving(direction))
            _pathIndex++;
    }
    else if (currentTile == _targetTile)
    {
        CurrentState = ActorState.PerformAction;  // Arrived!
    }
    else
    {
        // Recalculate path (blocked or drift)
        _currentPath = _hero.CalculatePath(currentTile, _targetTile);
        _pathIndex = 0;
    }
}
```

### GoTo_Exit — Cleanup

```csharp
void GoTo_Exit()
{
    tileMover?.SnapToTileGrid();  // Ensure grid alignment
    _currentPath = null;
    _pathIndex = 0;
}
```

---

## TileByTileMover — Core Movement

The foundational movement component used by GoTo. Handles smooth tile-to-tile interpolation.

**Key API:**
- `StartMoving(Direction)` — begin moving one tile in the given direction
- `IsMoving` — true while interpolating between tiles
- `StopMoving()` — halt and snap to nearest grid position
- `SnapToTileGrid()` — force grid alignment (accounts for collider offset)
- `GetCurrentTileCoordinates()` — current tile position (accounts for collider offset)
- `UpdateTriggersAfterTeleport()` — recalculate triggers after non-standard movement

**Movement speed:** `GameConfig.HeroMovementSpeed` (tiles per second)

---

## Replanning & Interrupts

The state machine monitors for world state changes that require replanning:

```csharp
void PerformAction_Tick()
{
    // Healing priorities changed?
    if (HasHealingPrioritiesChanged() && !_currentAction.ShouldNotOverride())
    {
        _actionPlan.Clear();
        CurrentState = ActorState.Idle;  // Replan
        return;
    }

    // Stop adventuring changed?
    if (HasStoppedAdventureChanged() && !_currentAction.ShouldNotOverride())
    {
        UpdateStopAdventuringActionCosts();
        _actionPlan.Clear();
        CurrentState = ActorState.Idle;  // Replan
        return;
    }

    // Execute current action
    bool complete = _currentAction.Execute(_hero);
    if (complete)
    {
        _actionPlan.Pop();
        CurrentState = _actionPlan.Count > 0 ? ActorState.PerformAction : ActorState.Idle;
    }
}
```

**In GoTo_Tick:** Adjacent monster detection during movement triggers `CurrentState = ActorState.Idle` for replanning. This interrupts movement mid-path.

---

## Mercenary AI

Mercenaries use the same GOAP + 3-state FSM pattern with `MercenaryStateMachine`:

### MercenaryActionBase

```csharp
public abstract class MercenaryActionBase : Action
{
    protected MercenaryActionBase(string name, int cost = 1) : base(name, cost) { }
    public abstract bool Execute(MercenaryComponent mercenary);
}
```

### Mercenary Actions (5 total)

| Action | Purpose |
|--------|---------|
| `FollowTargetAction` | Follow hero/other merc via pathfinding |
| `WalkToPitEdgeAction` | Walk to pit boundary |
| `MercenaryJumpIntoPitAction` | Jump into pit (coroutine) |
| `MercenaryJumpOutOfPitAction` | Jump out of pit (coroutine) |
| `WalkToHeroStatueAction` | Walk to hero statue for promotion |

### Mercenary Replanning

Mercenary state machine watches for target location changes:

```csharp
// If target's pit status changed unexpectedly, replan
if (_expectedTargetInPit != targetActuallyInPit)
{
    CurrentState = ActorState.Idle;  // Replan
}
```

---

## Coroutine Usage in GOAP Actions

**Coroutines are used ONLY for visual animations and non-pathfinding movement** (jumps, sleep sequences, seating animations). They are NOT used for destination navigation.

| Action | Coroutine Purpose |
|--------|------------------|
| `JumpIntoPitAction` | Smooth jump arc animation |
| `JumpOutOfPitForInnAction` | Smooth jump-out arc animation |
| `SleepInBedAction` | Sleep duration timer with Z animation |
| `AttackMonsterAction` | Multi-participant battle sequence |
| `ActivateWizardOrbAction` | Re-enable mover after delay |
| `WalkToTavernForStopAction` | Party seating animation |
| `MercenaryJumpIntoPitAction` | Mercenary jump arc |
| `MercenaryJumpOutOfPitAction` | Mercenary jump-out arc |

**Pattern for coroutine tracking in actions:**

```csharp
private ICoroutine _myCoroutine;
private bool _coroutineComplete;

public override bool Execute(HeroComponent hero)
{
    if (_myCoroutine != null)
        return _coroutineComplete;  // Wait for coroutine

    _myCoroutine = Core.StartCoroutine(MyAnimation(hero));
    return false;  // In progress
}

private IEnumerator MyAnimation(HeroComponent hero)
{
    // ... animation logic using Time.DeltaTime ...
    yield return null;
    _coroutineComplete = true;
}
```

---

## Virtual Game Layer Integration

GOAP actions support dual execution: live game (`Execute(HeroComponent)`) and virtual testing (`Execute(IGoapContext)`).

### IGoapContext Interface

```csharp
public interface IGoapContext
{
    IWorldState WorldState { get; }
    IHeroController HeroController { get; }
    IPathfinder Pathfinder { get; }
    IPitLevelManager PitLevelManager { get; }
    ITiledMapService TiledMapService { get; }
    IPitGenerator PitGenerator { get; }
    IPitWidthManager PitWidthManager { get; }

    Dictionary<string, bool> GetGoapWorldState();
    void UpdateHeroPositionStates();
    void LogDebug(string message);
    void LogWarning(string message);
}
```

### VirtualGoapContext

Used by `VirtualHeroStateMachine` to run GOAP plans without Nez rendering:

```csharp
var context = new VirtualGoapContext(virtualWorldState, virtualHero);
action.Execute(context);  // Virtual execution
context.SyncBackToHero();  // Sync state changes back
```

### Adding Virtual Support to New Actions

```csharp
public override bool Execute(IGoapContext context)
{
    context.LogDebug("[MyAction] Virtual execution");
    // Modify virtual world state directly
    context.HeroController.SomeFlag = true;
    return true;
}
```

---

## Behavior Trees (Nez Built-in)

PitHero doesn't currently use behavior trees, but Nez provides a full implementation with a fluent builder:

### Composites
- **Sequence** — runs children in order, fails on first failure
- **Selector** — runs children in order, succeeds on first success
- **Parallel** — runs all children every tick, fails on first failure
- **RandomSequence** / **RandomSelector** — shuffled versions

### Decorators
- **AlwaysFail** / **AlwaysSucceed** — override child result
- **ConditionalDecorator** — gate child execution with a condition
- **Inverter** — flip child result
- **Repeater** — repeat N times
- **UntilFail** / **UntilSuccess** — loop until condition

### Actions
- **ExecuteAction** — wrap a `Func` as a leaf node
- **WaitAction** — delay for duration
- **LogAction** — debug logging
- **BehaviorTreeReference** — reference another tree

---

## Utility AI (Nez Built-in)

Best for dynamic environments with many competing actions. PitHero doesn't currently use this.

### Components
- **Reasoner** — root that selects best consideration
- **Consideration** — contains appraisals + an action, produces a utility score
- **Appraisal** — calculates a sub-score for a consideration
- **Action** — what to execute when a consideration wins

---

## File Reference

### State Machines
| File | Purpose |
|------|---------|
| `PitHero/AI/HeroStateMachine.cs` | Hero GOAP + FSM orchestrator |
| `PitHero/AI/MercenaryStateMachine.cs` | Mercenary GOAP + FSM |
| `PitHero/AI/HeroState.cs` | ActorState and LocationType enums |

### GOAP Infrastructure
| File | Purpose |
|------|---------|
| `PitHero/AI/GoapConstants.cs` | All GOAP condition/action name constants |
| `PitHero/AI/HeroActionBase.cs` | Base class for hero actions |
| `PitHero/AI/MercenaryActionBase.cs` | Base class for mercenary actions |

### Hero Actions (12)
| File | Cost | Movement? |
|------|------|-----------|
| `JumpIntoPitAction.cs` | 1 | GoTo → pit edge, then jump coroutine |
| `WanderPitAction.cs` | 1 | GoTo → fog tile |
| `ActivateWizardOrbAction.cs` | 99 | GoTo → wizard orb tile |
| `JumpOutOfPitForInnAction.cs` | varies | GoTo → pit inner edge, then jump coroutine |
| `AttackMonsterAction.cs` | 3 | None (already adjacent) |
| `OpenChestAction.cs` | 2 | None (already adjacent) |
| `UseHealingItemAction.cs` | varies | None |
| `UseHealingSkillAction.cs` | varies | None |
| `SleepInBedAction.cs` | varies | GoTo → inn bed |
| `WalkToStatueForCrystalAction.cs` | 1 | GoTo → hero statue |
| `JumpOutOfPitForStopAction.cs` | varies | GoTo → pit inner edge, then jump coroutine |
| `WalkToTavernForStopAction.cs` | varies | GoTo → tavern seat |

### Mercenary Actions (5)
| File | Purpose |
|------|---------|
| `FollowTargetAction.cs` | Follow hero/merc via pathfinding |
| `WalkToPitEdgeAction.cs` | Walk to pit boundary |
| `MercenaryJumpIntoPitAction.cs` | Jump into pit |
| `MercenaryJumpOutOfPitAction.cs` | Jump out of pit |
| `WalkToHeroStatueAction.cs` | Walk to hero statue |

### Virtual Layer
| File | Purpose |
|------|---------|
| `PitHero/VirtualGame/VirtualHeroStateMachine.cs` | Test FSM |
| `PitHero/VirtualGame/VirtualGoapContext.cs` | Test GOAP context |
| `PitHero/VirtualGame/VirtualHeroController.cs` | Test hero controller |
| `PitHero/VirtualGame/VirtualPathfinder.cs` | Test A* pathfinder |

### GOAP Interfaces
| File | Purpose |
|------|---------|
| `IGoapContext.cs` | Unified GOAP execution interface |
| `IWorldState.cs` | World state provider |
| `IHeroController.cs` | Hero control abstraction |
| `IPathfinder.cs` | A* pathfinding abstraction |

---

## Quick Reference: Common Gotchas

| Gotcha | Fix |
|--------|-----|
| Action needs hero to walk somewhere | Add target to `CalculateTargetLocation()` — **never** move in `Execute()` |
| Planner can't find a plan | Check preconditions match current world state. Log with `LogActionPreconditions()` |
| Action never completes | Ensure `Execute()` eventually returns `true`. Check phase/timer/coroutine logic |
| Monster encounter during movement | GoTo_Tick handles this — transitions to Idle for replanning |
| New condition not working | Add to `GoapConstants`, set in `SetWorldState()`, check in `SetGoalState()` |
| Action cost ignored | Ensure `UpdateCost()` is called in `Idle_Enter()` before planning |
| Mercenary out of sync | Check `_expectedMercInPit` / `_expectedTargetInPit` replan detection |
| Virtual test doesn't run action | Implement `Execute(IGoapContext)` override on the action |