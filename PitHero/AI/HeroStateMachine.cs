using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.FSM;
using Nez.AI.GOAP;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Hero state machine that uses GOAP for planning and SimpleStateMachine for execution.
    /// Based on the Nez GoapMiner sample pattern.
    /// </summary>
    public class HeroStateMachine : SimpleStateMachine<HeroState>, IPausableComponent
    {
        private HeroComponent _hero;
        private ActionPlanner _planner;
        private Stack<Action> _actionPlan;
        private HeroActionBase _currentAction;

        /// <summary>
        /// Gets whether this component should respect the global pause state
        /// </summary>
        public bool ShouldPause => true;

        public HeroStateMachine()
        {
            // Initialize the ActionPlanner
            _planner = new ActionPlanner();

            // Setup our Actions and add them to the planner - only the 5 required actions
            var jumpIntoPit = new JumpIntoPitAction();
            _planner.AddAction(jumpIntoPit);

            var wander = new WanderAction();
            _planner.AddAction(wander);
            
            var activateWizardOrb = new ActivateWizardOrbAction();
            _planner.AddAction(activateWizardOrb);
            
            var jumpOutOfPit = new JumpOutOfPitAction();
            _planner.AddAction(jumpOutOfPit);
            
            var activatePitRegen = new ActivatePitRegenAction();
            _planner.AddAction(activatePitRegen);

            // Don't set initial state here - wait for OnAddedToEntity
        }

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            _hero = Entity.GetComponent<HeroComponent>();
            
            // Slow things down a bit. We don't need to tick every frame
            Entity.UpdateInterval = 10;
            
            // Set initial state to Idle - when it enters Idle it will ask the ActionPlanner for a new plan
            InitialState = HeroState.Idle;
        }

        public override void Update()
        {
            // Check if game is paused
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService?.IsPaused == true)
                return;

            if (_hero == null)
                return;

            base.Update();
        }

        /// <summary>
        /// Get the current world state for GOAP planning
        /// </summary>
        private WorldState GetWorldState()
        {
            var ws = WorldState.Create(_planner);
            
            // Safety check for null hero
            if (_hero == null)
            {
                Debug.Warn("[HeroStateMachine] GetWorldState: Hero component is null");
                return ws;
            }

            // Use the HeroComponent's SetWorldState method for consistency
            _hero.SetWorldState(ref ws);

            // Check additional positional states like wizard orb found
            CheckAdditionalStates(ref ws);
            
            return ws;
        }

        /// <summary>
        /// Get the goal state for GOAP planning - Progressive goals based on current state  
        /// </summary>
        private WorldState GetGoalState()
        {
            var goal = WorldState.Create(_planner);
            
            // Safety check for null hero
            if (_hero == null)
            {
                Debug.Warn("[HeroStateMachine] GetGoalState: Hero component is null");
                return goal;
            }

            // Use the HeroComponent's SetGoalState method for consistency
            _hero.SetGoalState(ref goal);
            
            return goal;
        }

        /// <summary>
        /// Check additional states like wizard orb found
        /// </summary>
        private void CheckAdditionalStates(ref WorldState ws)
        {
            if (_hero?.Entity == null)
                return;

            var tileMover = _hero.Entity.GetComponent<TileByTileMover>();
            var currentTile = tileMover?.GetCurrentTileCoordinates() ?? 
                new Point((int)(_hero.Entity.Transform.Position.X / GameConfig.TileSize),
                         (int)(_hero.Entity.Transform.Position.Y / GameConfig.TileSize));

            // Check if wizard orb has been found (fog cleared around it)
            var tms = Core.Services.GetService<TiledMapService>();
            CheckWizardOrbFound(ref ws, tms);
        }

        /// <summary>
        /// Check if wizard orb has been found (fog cleared around its position)
        /// </summary>
        private void CheckWizardOrbFound(ref WorldState ws, TiledMapService tms)
        {
            if (tms?.CurrentMap == null)
            {
                Debug.Log("[HeroStateMachine] CheckWizardOrbFound: No current map available");
                return;
            }

            var scene = Core.Scene;
            if (scene == null)
            {
                Debug.Log("[HeroStateMachine] CheckWizardOrbFound: No active scene");
                return;
            }

            // Find wizard orb entities
            var wizardOrbEntities = scene.FindEntitiesWithTag(GameConfig.TAG_WIZARD_ORB);
            if (wizardOrbEntities.Count == 0)
            {
                Debug.Log("[HeroStateMachine] CheckWizardOrbFound: No wizard orb entities found");
                return;
            }

            var wizardOrbEntity = wizardOrbEntities[0]; // Should only be one
            var worldPos = wizardOrbEntity.Transform.Position;
            var tilePos = new Point((int)(worldPos.X / GameConfig.TileSize), (int)(worldPos.Y / GameConfig.TileSize));

            Debug.Log($"[HeroStateMachine] CheckWizardOrbFound: Wizard orb entity found at world pos {worldPos.X},{worldPos.Y}, tile {tilePos.X},{tilePos.Y}");

            var fogLayer = tms.CurrentMap.GetLayer<Nez.Tiled.TmxLayer>("FogOfWar");
            if (fogLayer != null)
            {
                // Check if fog is cleared around the wizard orb position
                if (tilePos.X >= 0 && tilePos.Y >= 0 && tilePos.X < fogLayer.Width && tilePos.Y < fogLayer.Height)
                {
                    var fogTile = fogLayer.GetTile(tilePos.X, tilePos.Y);
                    Debug.Log($"[HeroStateMachine] CheckWizardOrbFound: Fog tile at wizard orb position {tilePos.X},{tilePos.Y}: {(fogTile == null ? "NULL (cleared)" : "EXISTS (not cleared)")}");

                    if (fogTile == null) // No fog means it's been discovered
                    {
                        // Set FoundWizardOrb on the HeroComponent directly
                        _hero.FoundWizardOrb = true;
                        Debug.Log($"[HeroStateMachine] *** WIZARD ORB FOUND *** Setting FoundWizardOrb=true at tile {tilePos.X},{tilePos.Y}");
                    }
                    else
                    {
                        Debug.Log($"[HeroStateMachine] Wizard orb at {tilePos.X},{tilePos.Y} still covered by fog - not yet discovered");
                    }
                }
                else
                {
                    Debug.Warn($"[HeroStateMachine] CheckWizardOrbFound: Wizard orb tile {tilePos.X},{tilePos.Y} is outside fog layer bounds ({fogLayer.Width}x{fogLayer.Height})");
                }
            }
            else
            {
                Debug.Log("[HeroStateMachine] CheckWizardOrbFound: No FogOfWar layer found - assuming wizard orb is discovered");
                _hero.FoundWizardOrb = true;
                Debug.Log($"[HeroStateMachine] *** VERIFICATION *** FoundWizardOrb set to true (no fog layer)");
            }
        }

        #region State Methods

        void Idle_Enter()
        {
            Debug.Log("[HeroStateMachine] Entering Idle state - planning next actions");
            
            // Get a plan to run that will get us from our current state to our goal state
            var currentWorldState = GetWorldState();
            var goalState = GetGoalState();
            
            // Add comprehensive debug logging for world state and goal state
            LogWorldStateDetails(currentWorldState, "Current World State");
            LogWorldStateDetails(goalState, "Goal State");
            
            // CRITICAL DEBUG: Log what states we expect to see
            Debug.Log($"[HeroStateMachine] *** CRITICAL GOAP CHECK *** About to call GOAP planner");

            _actionPlan = _planner.Plan(currentWorldState, goalState);

            if (_actionPlan != null && _actionPlan.Count > 0)
            {
                Debug.Log($"[HeroStateMachine] Got an action plan with {_actionPlan.Count} actions: {string.Join(" -> ", _actionPlan)}");
                CurrentState = HeroState.GoTo;
            }
            else
            {
                Debug.Log("[HeroStateMachine] No action plan satisfied our goals");
                LogActionPreconditions(); // Log what each action needs
                // Stay in Idle and try again in Idle_Tick
            }
        }

        void Idle_Tick()
        {
            // If we don't have a plan, try planning again
            if (_actionPlan == null || _actionPlan.Count == 0)
            {
                // Only retry every few updates to avoid excessive planning
                if (elapsedTimeInState > 1.0f) // Wait 1 second before retry
                {
                    Debug.Log("[HeroStateMachine] Retrying action planning...");
                    
                    var currentWorldState = GetWorldState();
                    var goalState = GetGoalState();
                    
                    // Add comprehensive debug logging for world state and goal state
                    LogWorldStateDetails(currentWorldState, "Retry Current World State");
                    LogWorldStateDetails(goalState, "Retry Goal State");
                    
                    // CRITICAL DEBUG: Log retry attempt
                    Debug.Log($"[HeroStateMachine] *** CRITICAL RETRY CHECK *** About to retry GOAP planner");
                    
                    _actionPlan = _planner.Plan(currentWorldState, goalState);

                    if (_actionPlan != null && _actionPlan.Count > 0)
                    {
                        Debug.Log($"[HeroStateMachine] Got an action plan with {_actionPlan.Count} actions: {string.Join(" -> ", _actionPlan)}");
                        CurrentState = HeroState.GoTo;
                    }
                    else
                    {
                        LogActionPreconditions(); // Log what each action needs
                    }
                }
            }
        }

        void GoTo_Enter()
        {
            Debug.Log("[HeroStateMachine] Entering GoTo state");
            
            if (_actionPlan == null || _actionPlan.Count == 0)
            {
                Debug.Warn("[HeroStateMachine] GoTo_Enter: No action plan available");
                CurrentState = HeroState.Idle;
                return;
            }

            // For now, we immediately transition to PerformAction since our actions handle their own movement
            // In a more complex implementation, we might have separate destination logic here
            CurrentState = HeroState.PerformAction;
        }

        void PerformAction_Enter()
        {
            Debug.Log("[HeroStateMachine] Entering PerformAction state");
            
            if (_actionPlan == null || _actionPlan.Count == 0)
            {
                Debug.Warn("[HeroStateMachine] PerformAction_Enter: No action plan available");
                CurrentState = HeroState.Idle;
                return;
            }

            // Get the next action to execute
            var action = _actionPlan.Peek();
            if (action is HeroActionBase heroAction)
            {
                _currentAction = heroAction;
                Debug.Log($"[HeroStateMachine] Starting execution of action: {action.Name}");
            }
            else
            {
                Debug.Warn($"[HeroStateMachine] Action {action.Name} is not a HeroActionBase");
                _actionPlan.Pop(); // Remove invalid action
                CurrentState = HeroState.Idle;
            }
        }

        void PerformAction_Tick()
        {
            if (_currentAction == null)
            {
                Debug.Warn("[HeroStateMachine] PerformAction_Tick: No current action");
                CurrentState = HeroState.Idle;
                return;
            }

            // Execute the current action
            bool actionComplete = _currentAction.Execute(_hero);
            
            if (actionComplete)
            {
                Debug.Log($"[HeroStateMachine] Action {_currentAction.Name} completed");
                
                // Remove the completed action from the plan
                if (_actionPlan != null && _actionPlan.Count > 0)
                    _actionPlan.Pop();
                
                _currentAction = null;
                
                // Check if we have more actions to execute
                if (_actionPlan != null && _actionPlan.Count > 0)
                {
                    CurrentState = HeroState.PerformAction; // This will trigger PerformAction_Enter for next action
                }
                else
                {
                    Debug.Log("[HeroStateMachine] Action plan completed, returning to Idle");
                    CurrentState = HeroState.Idle;
                }
            }
        }

        void PerformAction_Exit()
        {
            Debug.Log("[HeroStateMachine] Exiting PerformAction state");
            // _currentAction is kept for potential cleanup in next state
        }

        #endregion
        
        #region Debug Logging Methods
        
        /// <summary>
        /// Log detailed world state for debugging GOAP planning issues
        /// </summary>
        private void LogWorldStateDetails(WorldState ws, string prefix)
        {
            Debug.Log($"[HeroStateMachine] {prefix}: {ws.Describe(_planner)}");
        }
        
        /// <summary>
        /// Log action preconditions to debug why no action plan can be found
        /// </summary>
        private void LogActionPreconditions()
        {
            Debug.Log($"[HeroStateMachine] Planner Describe:");
            Debug.Log(_planner.Describe());
            //Debug.Log("[HeroStateMachine] Available actions and their preconditions:");
            //Debug.Log("  MoveToPitAction: Requires PitInitialized=true, HeroInitialized=true, OutsidePit=true");
            //Debug.Log("  JumpIntoPitAction: Requires AdjacentToPitBoundaryFromOutside=true");
            //Debug.Log("  WanderAction: Requires InsidePit=true");
            //Debug.Log("  MoveToWizardOrbAction: Requires FoundWizardOrb=true, MapExplored=true");
            //Debug.Log("  ActivateWizardOrbAction: Requires AtWizardOrb=true");
            //Debug.Log("  MovingToInsidePitEdgeAction: Requires MovingToInsidePitEdge=true");
            //Debug.Log("  JumpOutOfPitAction: Requires ReadyToJumpOutOfPit=true");
            //Debug.Log("  MoveToPitGenPointAction: Requires MovingToPitGenPoint=true");
        }
        
        #endregion
    }
}