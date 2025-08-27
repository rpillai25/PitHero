# PitHero GOAP Cycle (Updated)

This document reflects the current, simplified GOAP setup in PitHero.AI.

Summary
- World states (only these 7 exist):
  - HeroInitialized
  - PitInitialized
  - InsidePit
  - OutsidePit (derived from InsidePit in runtime state, but can also appear in planner state)
  - ExploredPit
  - FoundWizardOrb
  - ActivatedWizardOrb
- Actions (only these 5 exist):
  - JumpIntoPitAction
  - WanderAction
  - ActivateWizardOrbAction
  - JumpOutOfPitAction
  - ActivatePitRegenAction
- Goal selection (HeroComponent.SetGoalState):
  - If PitInitialized == true and ActivatedWizardOrb == false -> goal ActivatedWizardOrb == true
  - Else if PitInitialized == false and ActivatedWizardOrb == true -> goal PitInitialized == true

World states: intended meaning
- HeroInitialized: hero entity/components are ready
- PitInitialized: current pit is generated and active
- InsidePit: hero is in the pit area
- OutsidePit: hero is outside the pit area (computed as !InsidePit at runtime)
- ExploredPit: all reachable FogOfWar in the pit interior was cleared (set by WanderAction)
- FoundWizardOrb: hero has discovered the Wizard Orb’s tile (set during wander when standing on the orb tile)
- ActivatedWizardOrb: the wizard orb has been activated (and the next pit level queued)

Actions (with exact preconditions/postconditions as implemented)
- JumpIntoPitAction
  - Preconditions: HeroInitialized == true, PitInitialized == true
  - Postconditions: InsidePit == true
  - Notes: Performs a 2-tile jump coroutine into the pit. Does not explicitly change OutsidePit in planner state; runtime flips Inside/Outside.

- WanderAction
  - Preconditions: InsidePit == true, ExploredPit == false
  - Postconditions: ExploredPit == true (when no unknown tiles remain)
  - Notes: Repeatedly picks nearest fog tile in pit bounds, A* moves, clears fog. If the hero lands exactly on the Wizard Orb tile during exploration, it sets FoundWizardOrb == true at runtime. This side-effect is not declared as a GOAP postcondition.

- ActivateWizardOrbAction
  - Preconditions: InsidePit == true, ExploredPit == true, FoundWizardOrb == true
  - Postconditions: ActivatedWizardOrb == true, PitInitialized == false (also queues next pit level)
  - Notes: Changes the orb’s tint and enqueues next level via PitLevelQueueService.

- JumpOutOfPitAction
  - Preconditions: InsidePit == true, ActivatedWizardOrb == true
  - Postconditions: OutsidePit == true
  - Notes: Performs a 2-tile jump coroutine out of the pit and sets InsidePit=false at runtime.

- ActivatePitRegenAction
  - Preconditions: ActivatedWizardOrb == true, OutsidePit == true
  - Postconditions: PitInitialized == true
  - Notes: Dequeues the queued level and regenerates the pit. Also resets ExploredPit=false and ActivatedWizardOrb=false at runtime.

Planner goal flow
- Cycle A (activate the orb)
  - Goal: ActivatedWizardOrb == true (selected while PitInitialized==true and orb not yet activated)
  - Typical plan from initial state HeroInitialized=true, PitInitialized=true, OutsidePit=true:
    1) JumpIntoPitAction -> InsidePit=true
    2) WanderAction -> ExploredPit=true (and runtime may set FoundWizardOrb=true when stepping on orb tile)
    3) ActivateWizardOrbAction -> ActivatedWizardOrb=true, PitInitialized=false
  - Important: ActivateWizardOrbAction requires FoundWizardOrb==true. WanderAction does not declare FoundWizardOrb as a postcondition, but sets it at runtime only if the hero reaches the orb tile.

- Cycle B (regenerate the pit)
  - Goal: PitInitialized == true (selected while PitInitialized==false and orb already activated)
  - Typical plan:
    1) JumpOutOfPitAction -> OutsidePit=true
    2) ActivatePitRegenAction -> PitInitialized=true (also clears ExploredPit and ActivatedWizardOrb at runtime)

Notes and caveats
- Only the 7 states and 5 actions listed above are used by GOAP. Older states such as adjacency flags or AtWizardOrb are not present.
- Because FoundWizardOrb is not a declared postcondition of WanderAction, the planner cannot rely on it being achieved by WanderAction. In practice, exploration should eventually take the hero to the orb tile to satisfy the ActivateWizardOrbAction precondition.
- OutsidePit in planner state is not explicitly flipped by JumpIntoPitAction; the runtime state (HeroComponent) derives OutsidePit from InsidePit.
