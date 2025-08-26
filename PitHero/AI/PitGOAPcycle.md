# PitHero GOAP Cycle

This document describes the full goal/action chain for the hero in the PitHero.AI namespace, including exact GOAP preconditions, postconditions, and the action that advances each step.

---

## 1. Start at Spawn Point
- Initial facts set by HeroStateMachine:
  - HeroInitialized=true, and PitInitialized=true once PitWidthManager finishes.
- No action needed yet.

## 2. Move to Outside Pit Edge
- Goal: AdjacentToPitBoundaryFromOutside=true
- Action: MoveToPitAction
- Preconditions: HeroInitialized=true, PitInitialized=true
- Postconditions: AdjacentToPitBoundaryFromOutside=true
- Behavior: A* path to a candidate tile just outside the pit boundary.

## 3. Jump Into Pit
- Goal: InsidePit=true
- Action: JumpIntoPitAction
- Preconditions: AdjacentToPitBoundaryFromOutside=true
- Postconditions: InsidePit=true, AdjacentToPitBoundaryFromOutside=false (cleared on entry)
- Behavior: Short “jump” into interior using a coroutine to avoid collider issues. Does NOT set AdjacentToPitBoundaryFromInside; that is reserved for the inside-edge target.

## 4. Wander and Explore Pit
- Goal: MapExplored=true
- Action: WanderAction
- Preconditions: InsidePit=true
- Postconditions: MapExplored=true (achieved once fog cleared from explorable area)
- Behavior: Picks nearest fog tile within pit inner bounds, A* moves, clears fog via TiledMapService around each tile.

## 5. Activate Wizard Orb
- Two-step target:
  - 5a) Move to Wizard Orb
    - Goal: AtWizardOrb=true
    - Action: MoveToWizardOrbAction
    - Preconditions: FoundWizardOrb=true, MapExplored=true
    - Postconditions: AtWizardOrb=true (only while standing on the orb tile)
  - 5b) Activate It
    - Goal: ActivatedWizardOrb=true
    - Action: ActivateWizardOrbAction
    - Preconditions: AtWizardOrb=true
    - Postconditions: ActivatedWizardOrb=true, MovingToInsidePitEdge=true, PitInitialized=false (queued regen)

## 6. Move to Inside Pit Edge
- Goal: ReadyToJumpOutOfPit=true
- Action: MovingToInsidePitEdgeAction
- Preconditions: MovingToInsidePitEdge=true
- Postconditions: ReadyToJumpOutOfPit=true and AdjacentToPitBoundaryFromInside=true only when at the exact inside-edge target tile
- Note: AdjacentToPitBoundaryFromInside is true only at this target and cleared as soon as the hero leaves it.

## 7. Jump Out of Pit
- Goal: OutsidePit=true
- Action: JumpOutOfPitAction
- Preconditions: ReadyToJumpOutOfPit=true
- Postconditions: OutsidePit=true, MovingToPitGenPoint=true
- Behavior: Short jump to outside boundary and sets flag to proceed to regen spot.

## 8. Move to Pit Regen Spot (Regenerates Pit)
- Goal: AtPitGenPoint=true (tile 34,6)
- Action: MoveToPitGenPointAction
- Preconditions: OutsidePit=true
- Postconditions: AtPitGenPoint=true (and triggers pit regen via queued level), PitInitialized=true after regeneration

## 9. Move to Outside Pit Edge (Starts Cycle Again at Step 2)
- With pit regenerated and PitInitialized=true, planner sets goal OutsidePit/AdjacentToPitBoundaryFromOutside and repeats:
  - MoveToPitAction ? JumpIntoPitAction ? WanderAction ? MoveToWizardOrbAction ? ActivateWizardOrbAction ? MovingToInsidePitEdgeAction ? JumpOutOfPitAction ? MoveToPitGenPointAction

---

## Important Implementation Notes
- World state consistency adjustments:
  - MOVINGTOPIT is only set while the hero is outside the pit and moving toward it. It is not set when InsidePit=true.
  - AdjacentToPitBoundaryFromInside is only true when the hero is at the exact inside-edge target tile. It is cleared when the hero moves away.
  - AtWizardOrb is only true while standing exactly on the wizard orb tile; it is not persisted once the hero moves off the tile.
- Progressive goal selection in GetGoalState:
  - Not explored ? MapExplored
  - Explored, not at orb ? AtWizardOrb
  - At orb, not activated ? ActivatedWizardOrb
  - Activated, not ready to jump ? ReadyToJumpOutOfPit (selects MovingToInsidePitEdgeAction)
  - Not at regen after jump out ? AtPitGenPoint
  - Else ? OutsidePit (to cycle)
