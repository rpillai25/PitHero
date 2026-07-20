# Creating GOAP Actions

## Hero Action Pattern

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
        // Hero is already at the target location (GoTo got us here)
        DoSomething(hero);
        return true;   // Complete in one frame
    }

    /// <summary>Virtual layer execution for testing.</summary>
    public override bool Execute(IGoapContext context)
    {
        context.LogDebug("[MyNewAction] Virtual execution");
        return true;
    }
}
```

## Registering a New Action

1. Add condition constants to `GoapConstants.cs`
2. Create the action class inheriting `HeroActionBase`
3. Register in `HeroStateMachine` constructor: `_planner.AddAction(new MyNewAction())`
4. Add destination mapping in `CalculateTargetLocation()` if the action needs movement
5. Set the corresponding flags on `HeroComponent` in `SetWorldState()` / `SetGoalState()`

## Multi-Frame Actions (Phase Pattern)

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
                FaceTarget(hero, _chestEntity.Transform.Position);
                _phase = Phase.FacingWait;
                _timer = GameConfig.TreasureOpenWait;
                return false;

            case Phase.FacingWait:
                _timer -= Time.DeltaTime;
                if (_timer <= 0f)
                {
                    PerformAction();
                    _phase = Phase.OpenedWait;
                    _timer = GameConfig.TreasureOpenWait;
                }
                return false;

            case Phase.OpenedWait:
                _timer -= Time.DeltaTime;
                if (_timer <= 0f)
                {
                    Reset();
                    return true;
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

## Dynamic Cost Actions

Some actions have costs that change based on player priorities:

```csharp
// Keep reference for cost updates
_sleepInBedAction = new SleepInBedAction();
_planner.AddAction(_sleepInBedAction);

// Update costs before planning (in Idle_Enter)
private void UpdateHealingActionCosts()
{
    // Lower cost = higher priority in the plan
    _useHealingItemAction.Cost  = hero.HealPriority1 == HealOption.Item  ? 1 : 3;
    _useHealingSkillAction.Cost = hero.HealPriority1 == HealOption.Skill ? 1 : 3;
    _sleepInBedAction.Cost      = hero.HealPriority1 == HealOption.Inn   ? 1 : 3;
}
```

## Preventing Action Interruption

```csharp
public override bool ShouldNotOverride() => true;  // Don't interrupt this action
```

## Service-Integrated Multi-Phase Actions

When an action mutates a shared service (e.g., spends gold, restores HP) between phases, gate the entire action on the precondition **before** starting the coroutine, and re-verify funds at the actual mutation point.

Example: `SleepInBedAction` (Inn flow)

- **Precondition gate (`Execute` entry):** check `GameStateService.Funds >= InnCostCalculator.GetCurrentPartyCost(hero.LinkedHero)` (level-scaled, per party member). If not satisfied, return `true` immediately so the planner picks a different action — don't enter the coroutine to discover this.
- **`CalculateTargetLocation`** returns the *payment* tile (`InnPaymentTileX/Y`), not the bed tile — so GoTo walks the hero to the innkeeper first.
- **Coroutine phases:** face innkeeper (`Direction.Right`) → 0.5s animation delay → `GameStateService.Funds -= InnCostCalculator.GetCurrentPartyCost(...)` → pathfind to bed → sleep → restore HP/MP.
- Innkeeper entity is spawned at a fixed tile in `MainGameScene.SpawnInnkeeper()` with `TAG_INNKEEPER`; he never moves.

The lesson: any action that spends a resource (gold, MP, items) must verify availability in `Execute` *before* starting visual coroutines — otherwise you can spend a frame walking to the NPC only to abort, which looks broken.

## GoTo State — Navigation

The GoTo state handles ALL destination movement. Uses A* and `TileByTileMover`.

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
            return null;   // Skip GoTo, go straight to PerformAction

        default:
            return null;
    }
}
```

**When adding a new action:** If it requires movement, add a case returning the target tile. If not, return `null`.

### GoTo_Enter — Path Calculation

```csharp
void GoTo_Enter()
{
    var nextAction = _actionPlan.Peek();
    Point? target = CalculateTargetLocation(nextAction.Name);

    if (!target.HasValue)
    {
        CurrentState = ActorState.PerformAction;
        return;
    }

    _targetTile  = target.Value;
    _currentPath = _hero.CalculatePath(currentTile, _targetTile);
    _pathIndex   = 0;

    if (_currentPath == null || _currentPath.Count == 0)
        CurrentState = ActorState.PerformAction;
}
```

### GoTo_Tick — Step-by-Step Movement

```csharp
void GoTo_Tick()
{
    if (IsBattleInProgress) return;

    var tileMover = _hero.Entity.GetComponent<TileByTileMover>();
    if (tileMover.IsMoving) return;

    // Adjacent enemy → replan
    if (_hero.AdjacentToMonster && !wasAdjacentBefore)
    {
        CurrentState = ActorState.Idle;
        return;
    }

    if (_pathIndex < _currentPath.Count)
    {
        var nextTile  = _currentPath[_pathIndex];
        var direction = CalculateDirection(curTile, nextTile);
        if (tileMover.StartMoving(direction))
            _pathIndex++;
    }
    else if (currentTile == _targetTile)
    {
        CurrentState = ActorState.PerformAction;
    }
    else
    {
        // Recalculate (blocked or drift)
        _currentPath = _hero.CalculatePath(currentTile, _targetTile);
        _pathIndex   = 0;
    }
}
```

### GoTo_Exit — Cleanup

```csharp
void GoTo_Exit()
{
    tileMover?.SnapToTileGrid();
    _currentPath = null;
    _pathIndex   = 0;
}
```

## TileByTileMover — Core Movement

| API | Purpose |
|---|---|
| `StartMoving(Direction)` | Begin moving one tile |
| `IsMoving` | True while interpolating |
| `StopMoving()` | Halt and snap to grid |
| `SnapToTileGrid()` | Force grid alignment |
| `GetCurrentTileCoordinates()` | Current tile (accounts for collider offset) |
| `UpdateTriggersAfterTeleport()` | Recalculate triggers after non-standard movement |

Movement speed: `GameConfig.HeroMovementSpeed` (tiles/sec).

## Coroutine Usage in GOAP Actions

**Coroutines are ONLY for visual animations and non-pathfinding movement** (jumps, sleep, seating). NOT for destination navigation.

| Action | Coroutine Purpose |
|---|---|
| `JumpIntoPitAction` | Smooth jump arc |
| `JumpOutOfPitForInnAction` | Smooth jump-out arc |
| `SleepInBedAction` | Sleep duration + Z animation |
| `AttackMonsterAction` | Multi-participant battle sequence |
| `ActivateWizardOrbAction` | Re-enable mover after delay |
| `WalkToTavernForStopAction` | Party seating |
| `MercenaryJumpIntoPitAction` | Merc jump arc |
| `MercenaryJumpOutOfPitAction` | Merc jump-out arc |

### Pattern for Coroutine Tracking

```csharp
private ICoroutine _myCoroutine;
private bool _coroutineComplete;

public override bool Execute(HeroComponent hero)
{
    if (_myCoroutine != null)
        return _coroutineComplete;

    _myCoroutine = Core.StartCoroutine(MyAnimation(hero));
    return false;
}

private IEnumerator MyAnimation(HeroComponent hero)
{
    // ... animation logic using Time.DeltaTime ...
    yield return null;
    _coroutineComplete = true;
}
```

## Hero Actions (12 total)

| File | Cost | Movement? |
|---|---|---|
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
