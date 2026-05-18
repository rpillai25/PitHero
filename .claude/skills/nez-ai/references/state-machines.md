# State Machines, Replanning & Mercenary AI

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

## SKStateMachine ("States as Objects")

For more complex scenarios where each state needs its own class:

```csharp
var machine = new SKStateMachine<SomeClass>(context, new PatrollingState());
machine.AddState(new AttackState());
machine.AddState(new ChaseState());

machine.Update(Time.DeltaTime);
machine.ChangeState<ChasingState>();
```

## Replanning & Interrupts

The state machine monitors for world state changes that require replanning:

```csharp
void PerformAction_Tick()
{
    // Healing priorities changed?
    if (HasHealingPrioritiesChanged() && !_currentAction.ShouldNotOverride())
    {
        _actionPlan.Clear();
        CurrentState = ActorState.Idle;   // Replan
        return;
    }

    // Stop-adventuring toggled?
    if (HasStoppedAdventureChanged() && !_currentAction.ShouldNotOverride())
    {
        UpdateStopAdventuringActionCosts();
        _actionPlan.Clear();
        CurrentState = ActorState.Idle;
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

**In `GoTo_Tick`:** Adjacent monster detection during movement triggers `CurrentState = ActorState.Idle` for replanning. This interrupts movement mid-path.

## Mercenary AI

Mercenaries use the same GOAP + 3-state FSM pattern with `MercenaryStateMachine`. The action set, follow/jump/sleep behaviors, and freeze/unfreeze-on-hero-death pattern are documented in **`references/mercenary-ai.md`**.
