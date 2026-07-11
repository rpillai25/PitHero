# Virtual Game Logic Layer

## Overview

The Virtual Game Logic Layer is a complete simulation system that allows testing and verification of the PitHero GOAP workflow without requiring graphics or the full Nez environment. This addresses the need for GitHub Copilot to test and verify game logic independently.

## Architecture

### Core Components

1. **IVirtualWorld**: Interface for world representation without graphics
2. **VirtualWorldState**: Console-based world implementation that tracks:
   - Hero position and state
   - Pit layout and dynamic sizing
   - Wizard orb location and activation status
   - Fog of war status across the map
   - Entity positions and collision detection

3. **VirtualHero**: Hero simulation that maintains:
   - All GOAP state flags (matching HeroComponent)
   - Position-based state detection
   - Movement pathfinding and execution
   - World state generation for GOAP planning

4. **VirtualGameSimulation**: Main orchestrator that executes:
   - Complete pit generation at specified levels
   - Full GOAP action sequences
   - State transitions and validation
   - Console visualization and logging

## Complete Workflow Simulation

The system simulates the exact workflow described in the GitHub issue:

1. **Pit Generation**: Creates pit at level 40 with dynamic width calculation
2. **MoveToPitAction**: Hero moves from spawn to pit edge
3. **JumpIntoPitAction**: Hero enters pit interior
4. **WanderAction**: Complete exploration until all fog is cleared
5. **Wizard Orb Workflow**:
   - MoveToWizardOrbAction → ActivateWizardOrbAction
   - MovingToInsidePitEdgeAction → JumpOutOfPitAction
   - MoveToPitGenPointAction → Pit regeneration
6. **Cycle Restart**: New pit ready for next iteration

## Usage

### Running Tests

```bash
# Run all virtual game simulation tests
dotnet test PitHero.Tests/ --filter VirtualGameSimulationTests

# Run the complete workflow demonstration
dotnet test PitHero.Tests/ --filter CompleteVirtualGameWorkflow_Demonstration
```

### Console Simulation

```csharp
// Create and run a complete simulation
var simulation = new VirtualGameSimulation();
simulation.RunCompleteSimulation();
```

### Testing Specific Scenarios

```csharp
// Test pit generation at different levels
var world = new VirtualWorldState();
world.RegeneratePit(40); // Level 40 pit with wider bounds

// Test hero state management
var hero = new VirtualHero(world);
hero.MoveTo(new Point(2, 3)); // Move inside pit
var worldState = hero.GetWorldState(); // Get GOAP states
```

## Console Output

The simulation provides detailed logging:

```
=== Starting Virtual Game Simulation ===
STEP 1: Generating pit at level 40
[VirtualWorld] Regenerated pit at level 40, bounds: (1,2,16,9)

STEP 2: Hero spawns and begins MoveToPitAction  
[MoveToPitAction] Moving from (33,6) to (0,5)
[MoveToPitAction] Completed. Hero adjacent to pit: True

STEP 3: Hero jumps into pit
[JumpIntoPitAction] Jumping from (0,5) to (3,4)
[JumpIntoPitAction] Completed. Hero inside pit: True

STEP 4: Hero wanders and explores pit completely
[WanderAction] Starting systematic exploration of pit interior
[WanderAction] Tick 50: Explored (8,7), fog remaining: 12
[WanderAction] Completed. Fog remaining: 0, MapExplored: True

STEP 5: Execute complete wizard orb workflow
[MoveToWizardOrbAction] Moving from (8,7) to wizard orb at (9,6)
[ActivateWizardOrbAction] Activating wizard orb and queuing next pit level
[MovingToInsidePitEdgeAction] Moving to inside pit edge at (1,5)
[JumpOutOfPitAction] Jumping out of pit to (0,5)
[MoveToPitGenPointAction] Moving to pit generation point (34,6)
[MoveToPitGenPointAction] Regenerated pit at level 50
```

## Visual Representation

The system provides ASCII visualization of the world state:

```
=== Virtual World - Pit Level 40 ===
Hero: (34,6), Wizard Orb: (9,6) [ACTIVATED]
Pit Bounds: (1,2,16,9)

   01234567890123456789
02 ....████████████....
03 ...█..........█.....
04 ...█..........█.....
05 ...█....w.....█.....
06 ...█..........█.....
07 ...█..........█.....
08 ...█..........█.....
09 ...█..........█.....
10 ....████████████....

Legend: H=Hero, w=Wizard Orb, W=Activated Orb, #=Obstacle, █=Wall, ?=Fog, .=Empty
```

## Benefits for Development

### For GitHub Copilot
- **Independent Testing**: Verify game logic without graphics setup
- **Rapid Iteration**: Test changes instantly in console environment
- **Debug Visibility**: See exact state changes and action execution
- **Regression Prevention**: Catch logic errors before they reach the full game

### For Developers
- **Unit Testing**: Test individual GOAP actions in isolation
- **Integration Testing**: Verify complete workflow sequences
- **Performance Testing**: Measure action execution times
- **State Validation**: Confirm proper state transitions

## Integration with Existing Code

The virtual layer integrates seamlessly with existing systems:

- **GOAP Actions**: All existing actions work without modification
- **State Management**: Uses identical state flags as HeroComponent
- **Pathfinding**: Reuses AStar algorithms for movement
- **Service Layer**: Compatible with existing service patterns

## Test Coverage

The virtual game simulation includes comprehensive tests for:

- ✅ World state initialization and pit generation
- ✅ Hero movement and state detection
- ✅ GOAP world state generation
- ✅ Complete workflow execution
- ✅ Pit level progression and regeneration
- ✅ Wizard orb discovery and activation
- ✅ Visual representation rendering
- ✅ Edge cases and error handling

## Delta Plan — Phase B: Combat + Traps Mirrored (issue #296)

Phase B wires up the headless `BattleEngine` (extracted in Phase A) to the virtual
exploration loop so that pit traversal is now a complete simulation: monsters are
fought, traps are triggered or disarmed, and aggregated metrics are exported for
balance analysis.

### New files added (all in `PitHero.VirtualGame`)

| File | Responsibility |
|------|---------------|
| `VirtualBattleAlly.cs` | `IBattleAlly` over a real `Hero` or `Mercenary`; `IsPresent` always true (mirrors `LiveHeroAlly`) |
| `VirtualBattlePartyView.cs` | `IBattlePartyView` replicating `HeroComponent` burst/critical-HP math using the same `GameConfig` constants |
| `VirtualBattleSink.cs` | `BattleEventSinkBase` that accumulates `VirtualBattleMetrics` and removes defeated monsters from `VirtualWorldState` in `ShowMonsterDeath` |
| `VirtualBattleRunner.cs` | Owns `BattleEngine` + `VirtualBattleSink` + a persistent `ActionQueue`; exposes `RunAdjacentBattle()`, `ApplyTrapDamageToHero()`, `PartyHasTrapSense()` |
| `VirtualBattleMetrics.cs` | Per-battle: rounds, damageDealt/Taken, healing, potions, heroDied, mercDeaths, monstersDefeated, XP/gold |
| `VirtualRunMetrics.cs` | Per-pit-level aggregate with `WriteCsv(TextWriter)` for balance reporting |

### Key modifications

- **`VirtualWorldState`** — adds `Dictionary<Point, IEnemy>` + reverse map, `HashSet<Point> TrapTiles`, `AddMonster(Point, IEnemy)` overload (auto-routes to `AddBossMonster` when `IsBoss=true`), adjacency query, `HasLivingBoss/Monsters`, trap methods `AddTrapTile / TriggerTrap / DisarmTrap`.  `ClearAllEntities()` clears the new collections.

- **`VirtualPitGenerator`** — boss mapping fixed to live `PitGenerator.cs` (5=StoneGuardian, 10=EarthElemental, 15=AncientWyrm, 20=PitLord, 25=MoltenTitan).  All monsters are created via `EnemyFactory.Create()` as real `IEnemy` instances, then stored through the new `AddMonster(Point, IEnemy)` overload.  Traps spawned per `GameConfig.TrapMin/MaxPerFloor`.

- **`VirtualHero`** — exposes `LinkedHero` and `ConfigureHero(IJob, int, StatBlock)`.  `AreAllMonstersDefeated` now checks `VirtualWorldState.HasLivingMonsters()`.

- **`VirtualHeroController.BossDefeated`** — computed from `!world.HasLivingBoss()` (was hardcoded `true`).

- **`VirtualHeroStateMachine`** — `BattleRunner` property; after each `TeleportTo` in wander/connectivity phases, `HandleTrapAtTile` + `RunAdjacentBattlesIfAny` are called.  `ExecuteActivateWizardOrbAction` gates on `!HasLivingBoss()` and navigates to any surviving boss before the gate check.

- **`VirtualGameSimulation`** — `ConfigureHero`, `ConfigureMercenaries`, `RunPitLevel(int)`, `World`, `Metrics` properties. `RunPitLevel` uses `VirtualPitGenerator` to populate real `IEnemy` instances, builds a `VirtualBattleRunner`, runs the state machine, and accumulates battle metrics into `VirtualRunMetrics`.

- **`VirtualGoapContext`** — `BattleRunner { get; set; }` property.

- **`AttackMonsterAction.Execute(IGoapContext)`** — stub replaced: if context is `VirtualGoapContext` with a `BattleRunner`, calls `RunAdjacentBattle()` and logs results; otherwise falls through to the original exploration-only no-op.

### Boss-mapping fix side-effect

`CaveBiomeBalanceTests.CaveBiome_BossEncounters_ValidateAllFiveBosses` expected old wrong display strings.  Updated to MonsterTextKey values matching live mapping.

## Future Enhancements

Potential extensions to the virtual layer:

- **Multiple Heroes**: Support for testing multi-hero scenarios
- **Custom Scenarios**: Scripted test scenarios for specific edge cases
- **Performance Metrics**: Detailed timing and efficiency analysis
- **Save/Load States**: Checkpoint and restore world states
- **Interactive Mode**: Step-by-step execution for debugging

This virtual game logic layer provides GitHub Copilot with the exact testing capability requested, enabling independent verification of the complete GOAP workflow without graphics dependencies.

## Delta Plan — Unmirrored features

The following game features added after the initial virtual layer implementation have **no virtual mirror** and are not simulated in `VirtualGameSimulation` or `VirtualTiledMapService`:

### Traps (Phase 6)
- **What exists:** `TrapComponent` spawns hidden trap entities in the pit. They trigger chip damage when the hero steps on them, or are auto-disarmed when revealed by a party member with `TrapSense`.
- **What is not mirrored:** The virtual layer has no concept of trap entities, trap tile positions, or TrapSense passive resolution during fog clearing.
- **What would be needed to add virtual coverage:**
  1. Extend `VirtualWorldState` to track trap tile positions (a `HashSet<Point>` of trap locations).
  2. Add trap spawning to `VirtualWorldState.RegeneratePit` following `GameConfig.TrapMinPerFloor`/`TrapMaxPerFloor`.
  3. Add a `CheckTrapSense(VirtualHero hero)` helper called from `VirtualTiledMapService.ClearFogOfWarAroundTile` when fog is removed over a trap tile and any party member has `TrapSense`.
  4. Add a `TriggerTrap(Point tile, VirtualHero hero)` that reduces `VirtualHero.HP` by the formula `5 + pitLevel * 2` (clamped to 1 HP minimum) for testing the damage path.
  5. Add tests in `VirtualGameSimulationTests` covering trap triggering and TrapSense disarm.