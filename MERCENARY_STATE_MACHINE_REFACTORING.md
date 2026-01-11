# Mercenary State Machine Refactoring

## Overview
The mercenary follow logic has been completely refactored to use a clean GOAP-based state machine pattern, similar to how the hero's AI works. This eliminates the previous overly complicated and buggy system.

## Architecture

### Single Responsibility Components

#### 1. **MercenaryFollowComponent** (ECS/Components/MercenaryFollowComponent.cs)
- **Single Responsibility**: A* pathfinding to the target's last known position
- **What it does**:
  - Continuously pathfinds to the follow target's `LastTilePosition`
  - Handles movement along the calculated path
  - Updates mercenary's `LastTilePosition` for chain following
  - Works for both hero-following and mercenary-following-mercenary scenarios
- **What it doesn't do**:
  - No pit detection logic
  - No jumping logic
  - No state management
  - Just pure pathfinding and movement

#### 2. **MercenaryStateMachine** (AI/MercenaryStateMachine.cs)
- **Single Responsibility**: High-level decision making for mercenary behavior
- **What it does**:
  - Uses GOAP to plan actions based on current state
  - Determines whether to follow, walk to pit edge, jump in, or jump out
  - Manages state transitions (Idle ? PerformAction)
  - Tracks whether mercenary is inside/outside pit
  - Tracks whether target is inside/outside pit
- **States tracked**:
  - `MercenaryInsidePit`: Is the mercenary currently in the pit?
  - `TargetInsidePit`: Is the follow target in the pit?
  - `MercenaryAtPitEdge`: Is the mercenary at the pit edge (ready to jump)?
  - `MercenaryFollowingTarget`: Goal state - mercenary should be following

### GOAP Actions

#### 1. **FollowTargetAction** (AI/FollowTargetAction.cs)
- Ensures `MercenaryFollowComponent` is added to the entity
- Lets the follow component handle all pathfinding
- **Preconditions**: Hero and pit initialized
- **Postconditions**: `MercenaryFollowingTarget = true`

#### 2. **WalkToPitEdgeAction** (AI/WalkToPitEdgeAction.cs)
- Walks the mercenary to the pit edge using direct pathfinding
- Completes when mercenary reaches pit edge tile
- **Preconditions**: 
  - `MercenaryInsidePit = false`
  - `TargetInsidePit = true`
- **Postconditions**: `MercenaryAtPitEdge = true`

#### 3. **MercenaryJumpIntoPitAction** (AI/MercenaryJumpIntoPitAction.cs)
- Performs the actual jump from pit edge into the pit (2 tiles left)
- Uses coroutine-based smooth movement
- Handles jump animation via `HeroJumpComponent`
- Clears fog of war at landing position
- **Preconditions**:
  - `MercenaryInsidePit = false`
  - `TargetInsidePit = true`
  - `MercenaryAtPitEdge = true`
- **Postconditions**: 
  - `MercenaryInsidePit = true`
  - `MercenaryAtPitEdge = false`

#### 4. **MercenaryJumpOutOfPitAction** (AI/MercenaryJumpOutOfPitAction.cs)
- Performs the jump from inside pit to outside (2 tiles right)
- Uses coroutine-based smooth movement
- Handles jump animation via `HeroJumpComponent`
- Clears fog of war at landing position
- **Preconditions**:
  - `MercenaryInsidePit = true`
  - `TargetInsidePit = false`
- **Postconditions**: `MercenaryInsidePit = false`

## How It Works

### Scenario 1: Hero is outside pit, mercenary hired
1. State machine detects: `MercenaryInsidePit = false`, `TargetInsidePit = false`
2. GOAP plans: **FollowTargetAction**
3. Mercenary follows hero using `MercenaryFollowComponent`

### Scenario 2: Hero jumps into pit
1. State machine detects: `MercenaryInsidePit = false`, `TargetInsidePit = true`
2. GOAP plans: **WalkToPitEdgeAction** ? **MercenaryJumpIntoPitAction** ? **FollowTargetAction**
3. Mercenary walks to pit edge
4. Mercenary jumps into pit
5. Mercenary follows hero inside pit

### Scenario 3: Hero jumps out of pit
1. State machine detects: `MercenaryInsidePit = true`, `TargetInsidePit = false`
2. GOAP plans: **MercenaryJumpOutOfPitAction** ? **FollowTargetAction**
3. Mercenary jumps out of pit
4. Mercenary follows hero outside pit

### Scenario 4: Chain following (Mercenary #2 follows Mercenary #1)
- Works exactly the same as following the hero
- The `FollowTarget` is set to Mercenary #1's entity
- State machine checks Mercenary #1's position to determine pit state
- All the same logic applies

## Key Implementation Details

### Continuous Following Behavior
The `FollowTargetAction` is designed as a **continuous action** that never completes:
```csharp
public override bool Execute(MercenaryComponent mercenary)
{
    // Ensure follow component exists
    var followComponent = mercenary.Entity.GetComponent<MercenaryFollowComponent>();
    if (followComponent == null)
    {
        followComponent = mercenary.Entity.AddComponent(new MercenaryFollowComponent());
    }

    // Never return true - this is a continuous following state
    return false;
}
```

### Dynamic Replanning
The `MercenaryStateMachine` monitors world state changes in `PerformAction_Update()` and triggers replanning when:
- Mercenary is following and target jumps into/out of pit
- Mercenary is walking to pit edge and target exits pit
- Mercenary is jumping and target changes location

This allows the mercenary to react immediately to changes in the target's location without waiting for the current action to complete.

### WorldState Initialization
The `MercenaryStateMachine` properly initializes `WorldState` instances using the factory method:
```csharp
var state = WorldState.Create(_planner);
```

This is critical because `WorldState` is a struct that requires the `planner` field to be initialized. Using the default constructor `new WorldState()` would leave the planner null, causing a `NullReferenceException` when calling `state.Set()`.

### Mercenary Hiring During Tavern Walk
When a mercenary is hired while they're still walking to their tavern position, the `WalkToTavern` coroutine checks for the `IsHired` flag and exits early:
```csharp
// Check if mercenary was hired during the walk
if (mercComponent.IsHired)
{
    Debug.Log($"Mercenary was hired during walk to tavern - stopping tavern walk");
    yield break;
}
```

This prevents the mercenary from continuing to walk to the tavern after being hired, allowing the `MercenaryStateMachine` to take control immediately.

## Benefits of This Architecture

1. **Separation of Concerns**: Each component/action has one job
2. **Reusability**: Same logic works for following hero or following another mercenary
3. **Testability**: Each action can be tested independently
4. **Maintainability**: Easy to understand what each part does
5. **Extensibility**: Easy to add new actions (e.g., combat, healing, etc.)
6. **Reliability**: Uses the same proven patterns as the hero's AI

## Removed Components

- **MercenaryJoinComponent**: Removed - functionality replaced by state machine + actions

## Key Files

### New Files Created
- `PitHero/AI/MercenaryStateMachine.cs`
- `PitHero/AI/MercenaryActionBase.cs`
- `PitHero/AI/FollowTargetAction.cs`
- `PitHero/AI/WalkToPitEdgeAction.cs`
- `PitHero/AI/MercenaryJumpIntoPitAction.cs`
- `PitHero/AI/MercenaryJumpOutOfPitAction.cs`

### Modified Files
- `PitHero/ECS/Components/MercenaryFollowComponent.cs` - Simplified to only handle pathfinding
- `PitHero/AI/GoapConstants.cs` - Added mercenary-specific GOAP constants
- `PitHero/Services/MercenaryManager.cs` - Updated to add state machine when hiring
- `PitHero/AI/ActivateWizardOrbAction.cs` - Removed reference to old join component

### Deleted Files
- `PitHero/ECS/Components/MercenaryJoinComponent.cs` - No longer needed

## GOAP Constants Added

```csharp
// States
public const string MercenaryInsidePit = "MercenaryInsidePit";
public const string TargetInsidePit = "TargetInsidePit";
public const string MercenaryFollowingTarget = "MercenaryFollowingTarget";
public const string MercenaryAtPitEdge = "MercenaryAtPitEdge";

// Actions
public const string FollowTargetAction = "FollowTargetAction";
public const string MercenaryJumpIntoPitAction = "MercenaryJumpIntoPitAction";
public const string MercenaryJumpOutOfPitAction = "MercenaryJumpOutOfPitAction";
public const string WalkToPitEdgeAction = "WalkToPitEdgeAction";
```

## Future Enhancements

This architecture makes it easy to add new behaviors:
- Combat actions (attack monsters)
- Support actions (heal hero/other mercenaries)
- Interaction actions (open chests, activate objects)
- Tactical positioning (flank enemies, protect hero)

All you need to do is create a new action class extending `MercenaryActionBase`, define its preconditions and postconditions, and add it to the `MercenaryStateMachine`'s action planner.
