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

- **`VirtualHeroStateMachine`** — `BattleRunner` property; after each `TeleportTo` in wander/connectivity phases, `HandleTrapAtTile` + `RunAdjacentBattlesIfAny` are called.  A third wander phase sweeps any remaining living monsters (teleport adjacent + battle) before exploration is declared complete, mirroring the live Battle priority.  `ExecuteActivateWizardOrbAction` gates on `!HasLivingBoss()` and navigates to any surviving boss before the gate check.

- **`VirtualGameSimulation`** — `ConfigureHero`, `ConfigureMercenaries`, `RunPitLevel(int)`, `World`, `Metrics` properties. `RunPitLevel` uses `VirtualPitGenerator` to populate real `IEnemy` instances, builds a `VirtualBattleRunner`, runs the state machine, and accumulates battle metrics into `VirtualRunMetrics`.

- **`VirtualGoapContext`** — `BattleRunner { get; set; }` property.

- **`AttackMonsterAction.Execute(IGoapContext)`** — stub replaced: if context is `VirtualGoapContext` with a `BattleRunner`, calls `RunAdjacentBattle()` and logs results; otherwise falls through to the original exploration-only no-op.

### Boss-mapping fix side-effect

`CaveBiomeBalanceTests.CaveBiome_BossEncounters_ValidateAllFiveBosses` expected old wrong display strings.  Updated to MonsterTextKey values matching live mapping.

## Deterministic Seeding (Phase C, issue #296)

- `new VirtualGameSimulation(rngSeed)` calls `Nez.Random.SetSeed(rngSeed)` so **all combat
  rolls** (turn order, evasion/variance, target picks, crit/deflect) are reproducible.
  The default constructor captures the ambient `Nez.Random.GetSeed()` instead.
- The seed is recorded in `VirtualRunMetrics.RngSeed` so every balance report can cite it.
- Pit **layout** randomness is separate and already deterministic per level
  (`VirtualPitGenerator`/`VirtualWorldState` use a local `Random(level)`).
- `VirtualRunMetrics.HpLossPercent` = damage taken ÷ party max-HP pool at level start.
- `PitHero.Tests/VirtualBalanceTraversalTests.cs` (`[TestCategory("BalanceTraversal")]`)
  runs a seeded sampled traversal over levels 1/5/10/15/20/25 (all Cave boss floors),
  prints the per-level CSV table, and pins the same-seed ⇒ identical-CSV contract.

## Future Enhancements

Potential extensions to the virtual layer:

- **Multiple Heroes**: Support for testing multi-hero scenarios
- **Custom Scenarios**: Scripted test scenarios for specific edge cases
- **Performance Metrics**: Detailed timing and efficiency analysis
- **Save/Load States**: Checkpoint and restore world states
- **Interactive Mode**: Step-by-step execution for debugging

This virtual game logic layer provides GitHub Copilot with the exact testing capability requested, enabling independent verification of the complete GOAP workflow without graphics dependencies.

## Delta Plan — Phase C: Chest loot + auto-equip (issue #296 follow-up)

Phase C gives virtual chest positions real `IItem` instances, opens them during
traversal, adds items to the party bag, and auto-equips gear exactly like the live
`OpenChestAction` flow.

### New APIs

**`VirtualWorldState`** — Phase C adds a `Dictionary<Point, IItem> _treasureInstances`
running in parallel with the string-based parity lists
(`LastGeneratedTreasureLevels` / `LastGeneratedEquipmentTypes`):

| Method | Behaviour |
|--------|-----------|
| `AddTreasure(Point, IItem)` | Stores the real item; infers treasure level from rarity (Normal→1, Uncommon→2, Rare→3, Epic→4, Legendary→5); calls the string-based overload for parity-list parity |
| `TryGetTreasureAt(Point, out IItem)` | Non-destructive lookup |
| `RemoveTreasure(Point)` | Clears both `_treasureInstances` and the `_entities["Treasures"]` position list |
| `HasUnopenedTreasures()` | True when any chest remains unvisited |
| `GetNearestTreasurePosition(Point)` | Manhattan-distance nearest chest |

**`VirtualBattleRunner`** — new chest-loot surface:

| Member | Behaviour |
|--------|-----------|
| `AutoEquipHero` (`bool`, default `true`) | Mirrors `HeroComponent.AutoEquipHero` |
| `AutoEquipMercenaries` (`bool`, default `true`) | Mirrors `HeroComponent.AutoEquipMercenaries` |
| `TreasuresOpened` (`int`) | Incremented by each `CollectChestItem` call |
| `GearEquipped` (`int`) | Incremented each time a piece of gear is slotted |
| `BagCount()` | Returns current `Bag.Count` for test observability |
| `CollectChestItem(IItem)` | Adds to bag → if gear: `TryAutoEquipOnHero` → per-merc with hand-me-down cascade |

**`VirtualHeroStateMachine`** — new fourth wander phase:
1. Fog sweep (existing phase 1)
2. Connectivity sweep (existing phase 2)
3. Monster sweep (existing phase 3)
4. **Chest sweep** — teleports to each remaining chest one at a time (one per tick);
   calls `CollectChestItem` + `RemoveTreasure` on the hero's tile, then
   `CollectAdjacentTreasures()` for Chebyshev-1 neighbours.
   Only returns `true` (action complete) once `HasUnopenedTreasures()` is false.

`CollectAdjacentTreasures()` is also called after every `TeleportTo` in the fog,
connectivity, and monster phases so nearby chests are collected opportunistically.

### Deterministic item generation

`TreasureComponent` gains `internal static` deterministic overloads:

- `GenerateCaveItemForTreasureLevelDeterministic(int treasureLevel, System.Random rng)` — mirrors `GenerateCaveItemForTreasureLevel` but uses `rng.NextDouble()` / `rng.Next()` instead of `Nez.Random`.  Pool-selection switch bodies are shared via private `Get*ItemAtIndex(int)` helpers.
- `GenerateItemForTreasureLevelDeterministic(int treasureLevel, System.Random rng)` — for non-cave levels (Normal/Mid/Full potions by treasure level).

`VirtualPitGenerator.RegenerateForLevel` now calls these overloads with the per-level
`new Random(level)` instance, so loot is deterministic per level just like mob spawns.

### Metrics

`VirtualRunMetrics` adds `TreasuresOpened` and `GearEquipped` fields; both are written
to the CSV export (`treasures,gearEquipped` columns).  `VirtualGameSimulation.RunPitLevel`
copies them from the runner after the state machine completes.

### Test coverage

Three new tests in `VirtualBattleSimulationTests`:

| Test | Assertion |
|------|-----------|
| `ChestAdjacentToHero_WhenCollected_ItemLandsInBag` | Bag grows by 1 after `CollectChestItem`; `TreasuresOpened` = 1 |
| `GearChestItem_BetterThanHeroSlot_GetsAutoEquipped` | `GearEquipped` = 1; `hero.WeaponShield1` is non-null after auto-equip |
| `RunPitLevel1_AllChestsCollected_MetricsMatch` | `HasUnopenedTreasures()` false; `metrics.TreasuresOpened == LastGeneratedTreasureLevels.Count` |

## Delta Plan — Unmirrored features

All previously listed gaps are now closed:

- **Combat** — mirrored as of issue #296 Phase B (see "Delta Plan — Phase B" above).
- **Traps (Phase 6)** — mirrored as of issue #296 Phase B: `VirtualWorldState.TrapTiles`
  spawned by `VirtualPitGenerator`, `TriggerTrap` chip damage (`5 + pitLevel * 2`,
  1-HP floor, live `TrapComponent` parity) applied via
  `VirtualBattleRunner.ApplyTrapDamageToHero`, TrapSense auto-disarm via `DisarmTrap`.
  Covered by `VirtualBattleSimulationTests`.
- **Chest loot + auto-equip** — mirrored as of issue #296 Phase C (see above).

New live-layer features should be checked against this document and added here when
they lack a virtual counterpart.