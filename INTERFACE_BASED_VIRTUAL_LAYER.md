# Interface-Based Virtual Game Logic Layer

## Overview

The interface-based virtual game logic layer allows real GOAP actions to execute on virtual data without Nez dependencies. This ensures that testing the virtual layer actually tests the real GOAP implementation.

## Architecture

### Core Interfaces

- **`IWorldState`**: Provides world state information (pit bounds, wizard orb, fog of war, etc.)
- **`IHeroController`**: Manages hero movement and state flags
- **`IPathfinder`**: Handles A* pathfinding calculations
- **`IPitLevelManager`**: Manages pit level queuing and regeneration
- **`IGoapContext`**: Unified interface combining all the above

### Virtual Implementations

- **`VirtualWorldState`**: Complete world simulation without graphics
- **`VirtualHeroController`**: Hero movement and state management
- **`VirtualPathfinder`**: A* pathfinding implementation
- **`VirtualPitLevelManager`**: Pit level queue management
- **`VirtualGoapContext`**: Unified context for GOAP actions

### GOAP Action Integration

All GOAP actions now support dual execution modes:
- **Legacy**: `Execute(HeroComponent hero)` - works with existing Nez-based game
- **Interface-based**: `Execute(IGoapContext context)` - works with virtual layer

## Usage Examples

### Basic Virtual World Setup
```csharp
var virtualWorld = new VirtualWorldState();
virtualWorld.RegeneratePit(40);
var context = new VirtualGoapContext(virtualWorld);
```

### Execute Real GOAP Actions
```csharp
var wanderAction = new WanderAction();
bool completed = wanderAction.Execute(context); // Uses real GOAP logic!
```

### Complete Workflow Testing
```csharp
// Test the entire workflow: MoveToPit → JumpIn → Wander → MoveToOrb → Activate
dotnet test --filter CompleteGoapWorkflow_FromStartToWizardOrbActivation_ShouldExecuteCorrectly
```

## Benefits

1. **Real Logic Testing**: Virtual layer tests actual GOAP implementation, not simulations
2. **Graphics-Free**: Runs in CI/CD without graphics dependencies
3. **Complete Coverage**: Tests pathfinding, state management, pit regeneration, fog of war
4. **Debugging**: Detailed console output shows exact execution flow
5. **Validation**: Ensures virtual behavior matches real game behavior

## Test Categories

- **`InterfaceBasedGoapTests`**: Basic interface functionality
- **`RealGoapOnVirtualLayerTests`**: Real actions on virtual data
- **`CompleteGoapWorkflowTests`**: End-to-end workflow validation

This architecture solves the problem of having tests that don't reflect actual game behavior by ensuring the virtual layer uses the exact same GOAP logic as the real game.