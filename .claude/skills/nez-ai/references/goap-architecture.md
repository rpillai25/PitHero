# GOAP Architecture (F.E.A.R. Pattern)

PitHero follows the F.E.A.R. AI architecture: a GOAP planner selects actions, and a 3-state FSM orchestrates execution.

## ActorState Enum

```csharp
public enum ActorState
{
    Idle,           // Run GOAP planner, produce action stack
    GoTo,           // Navigate to target tile via A* + TileByTileMover
    PerformAction   // Execute the current action
}
```

## LocationType Enum

Used by `CalculateTargetLocation()` to map actions to destinations:

```csharp
public enum LocationType
{
    None, PitOutsideEdge, PitInsideEdge, PitWanderPoint,
    WizardOrb, PitRegenPoint, TownWanderPoint, Bed,
    Inn, ItemShop, WeaponShop, ArmorShop, TavernSeat
}
```

## GoapConstants — Strong-Typed Conditions

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

## ActionPlanner Setup

Register all actions in the state machine constructor:

```csharp
public HeroStateMachine()
{
    _planner = new ActionPlanner();

    // Order doesn't matter — planner uses cost + conditions
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

## Planning (Idle State)

```csharp
void Idle_Enter()
{
    UpdateHealingActionCosts();   // Update dynamic costs first

    var currentWorldState = GetWorldState();
    var goalState = GetGoalState();

    _actionPlan = _planner.Plan(currentWorldState, goalState);

    if (_actionPlan != null && _actionPlan.Count > 0)
        CurrentState = ActorState.GoTo;
}

private WorldState GetWorldState()
{
    var ws = WorldState.Create(_planner);
    _hero.SetWorldState(ref ws);   // Delegate to component
    return ws;
}

private WorldState GetGoalState()
{
    var goal = WorldState.Create(_planner);
    _hero.SetGoalState(ref goal);  // Priority-driven goal selection
    return goal;
}
```

## World State & Goal State (on HeroComponent)

```csharp
public override void SetWorldState(ref WorldState worldState)
{
    // Map boolean flags → GOAP atoms
    if (HeroInitialized)  worldState.Set(GoapConstants.HeroInitialized, true);
    if (InsidePit)        worldState.Set(GoapConstants.InsidePit, true);
    if (HPCritical)       worldState.Set(GoapConstants.HPCritical, true);
    // ...
}

public override void SetGoalState(ref WorldState goalState)
{
    // Priority-ordered goals (first match wins)
    if (StoppedAdventure) { goalState.Set(GoapConstants.SeatedInTavern, true); return; }
    if (NeedsCrystal)     { goalState.Set(GoapConstants.HasArrivedAtStatueForCrystal, true); return; }
    if (HPCritical && !AllHealingOptionsExhausted())
                          { goalState.Set(GoapConstants.HPCritical, false); return; }

    if (!ActivatedWizardOrb)
        goalState.Set(GoapConstants.ActivatedWizardOrb, true);
}
```

`WorldState` is a struct — pass by `ref` to methods that mutate it.
