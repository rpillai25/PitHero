# PitHero GOAP Cycle

This document describes the full goal/action chain for the hero in the PitHero.AI namespace, including exact GOAP preconditions, postconditions, and the action that advances each step.

---

## 1. Start at Spawn Point
- **Initial facts set by HeroStateMachine:**
  - `HeroInitialized=true`, and `PitInitialized=true` once PitWidthManager finishes.
- No action needed yet.

## 2. Move to Outside Pit Edge
- **Goal:** `AdjacentToPitBoundaryFromOutside=true`
- **Action:** `MoveToPitAction`
- **Preconditions:** `HeroInitialized=true`, `PitInitialized=true`
- **Postconditions:** `AdjacentToPitBoundaryFromOutside=true`
- **Behavior:** A* path to a candidate tile just outside the pit boundary.

## 3. Jump Into Pit
- **Goal:** `InsidePit=true`
- **Action:** `JumpIntoPitAction`
- **Preconditions:** `AdjacentToPitBoundaryFromOutside=true`
- **Postconditions:** `InsidePit=true`, `AdjacentToPitBoundaryFromInside=true`, `AdjacentToPitBoundaryFromOutside=false`
- **Behavior:** Short “jump” into interior using a coroutine to avoid collider issues. Sets hero flags on completion.

## 4. Wander and Explore Pit
- **Goal:** `MapExplored=true`
- **Action:** `WanderAction`
- **Preconditions:** `InsidePit=true`
- **Postconditions:** `MapExplored=true` (achieved once fog cleared from explorable area)
- **Behavior:** Picks nearest fog tile within pit inner bounds, A* moves, clears fog via TiledMapService around each tile.

## 5. Activate Wizard Orb
- This is a two-step target before activation:
  - **5a) Move to Wizard Orb**
    - **Goal:** `AtWizardOrb=true`
    - **Action:** `MoveToWizardOrbAction`
    - **Preconditions:** `FoundWizardOrb=true`, `MapExplored=true`
    - **Postconditions:** `AtWizardOrb=true`
    - **Note:** `FoundWizardOrb` is set in HeroStateMachine.GetWorldState via CheckWizardOrbFound (now passed by ref so it persists).
  - **5b) Activate It**
    - **Goal:** `ActivatedWizardOrb=true`
    - **Action:** `ActivateWizardOrbAction`
    - **Preconditions:** `AtWizardOrb=true`
    - **Postconditions:** `ActivatedWizardOrb=true`, `MovingToInsidePitEdge=true`, `PitInitialized=false` (queued regen)
    - **Behavior:** Tints orb, queues next pit level in PitLevelQueueService, sets hero flags.

## 6. Move to Inside Pit Edge
- **Goal:** `MovingToInsidePitEdge=true` then `ReadyToJumpOutOfPit=true`
- **Action:** `MovingToInsidePitEdgeAction`
- **Preconditions:** `ActivatedWizardOrb=true`
- **Postconditions:** `ReadyToJumpOutOfPit=true` (and typically positions hero on inner edge ready for jump)

## 7. Jump Out of Pit
- **Goal:** `OutsidePit=true`
- **Action:** `JumpOutOfPitAction`
- **Preconditions:** `ReadyToJumpOutOfPit=true`
- **Postconditions:** `OutsidePit=true`, `MovingToPitGenPoint=true`
- **Behavior:** Short jump to outside boundary and sets flag to proceed to regen spot.

## 8. Move to Pit Regen Spot (Regenerates Pit)
- **Goal:** `AtPitGenPoint=true` (tile 34,6)
- **Action:** `MoveToPitGenPointAction`
- **Preconditions:** `OutsidePit=true`
- **Postconditions:** `AtPitGenPoint=true` (and triggers pit regen via queued level), `PitInitialized=true` after regeneration
- **Behavior:** Moves to 34,6 and performs regeneration by consuming PitLevelQueueService.

## 9. Move to Outside Pit Edge (Starts Cycle Again at Step 2)
- With pit regenerated and `PitInitialized=true`, planner sets goal `OutsidePit`/`AdjacentToPitBoundaryFromOutside` and repeats:
  - `MoveToPitAction` ? `JumpIntoPitAction` ? `WanderAction` ? `MoveToWizardOrbAction` ? `ActivateWizardOrbAction` ? `MovingToInsidePitEdgeAction` ? `JumpOutOfPitAction` ? `MoveToPitGenPointAction`

---

## Important Implementation Notes
- `HeroStateMachine.GetWorldState` sets:
  - `FoundWizardOrb=true` when FogOfWar at orb tile is cleared. This now persists because `CheckWizardOrbFound` and `CheckAdditionalStates` take `ref WorldState`.
  - `AtWizardOrb`, `AtPitGenPoint`, `OutsidePit` are also set in `CheckAdditionalStates(ref ws)`.
- Progressive goal selection in `GetGoalState`:
  - Not explored ? `MapExplored`
  - Explored, not at orb ? `AtWizardOrb`
  - At orb, not activated ? `ActivatedWizardOrb`
  - Activated, not at regen ? `AtPitGenPoint`
  - Else ? `OutsidePit` (to cycle)
