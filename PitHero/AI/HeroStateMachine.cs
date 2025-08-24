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

            // Setup our Actions and add them to the planner
            var moveToPit = new MoveToPitAction();
            _planner.AddAction(moveToPit);

            var jumpIntoPit = new JumpIntoPitAction();
            _planner.AddAction(jumpIntoPit);

            var wander = new WanderAction();
            _planner.AddAction(wander);

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
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();

            ws.Set(GoapConstants.HeroInitialized, true);

            if (_hero.PitInitialized)
                ws.Set(GoapConstants.PitInitialized, true);

            var tileMover = _hero.Entity.GetComponent<TileByTileMover>();
            if (tileMover != null && tileMover.IsMoving && !_hero.AdjacentToPitBoundaryFromOutside)
            {
                ws.Set(GoapConstants.MovingToPit, true);
            }

            if (_hero.AdjacentToPitBoundaryFromOutside)
                ws.Set(GoapConstants.AdjacentToPitBoundaryFromOutside, true);
            if (_hero.AdjacentToPitBoundaryFromInside)
                ws.Set(GoapConstants.AdjacentToPitBoundaryFromInside, true);
            if (_hero.EnteredPit)
                ws.Set(GoapConstants.EnteredPit, true);

            // Mark exploration complete when FogOfWar is fully cleared inside the pit rect
            var tms = Core.Services.GetService<TiledMapService>();
            if (tms?.CurrentMap != null)
            {
                var fogLayer = tms.CurrentMap.GetLayer<Nez.Tiled.TmxLayer>("FogOfWar");
                if (fogLayer != null)
                {
                    var anyFog = false;
                    int totalFogTiles = 0;
                    
                    // Use the same explorable area bounds as WanderAction
                    int explorationMinX, explorationMinY, explorationMaxX, explorationMaxY;
                    
                    if (pitWidthManager != null)
                    {
                        explorationMinX = GameConfig.PitRectX + 1; // x=2
                        explorationMinY = GameConfig.PitRectY + 1; // y=3  
                        explorationMaxX = pitWidthManager.CurrentPitRightEdge - 2; // Last explorable column
                        explorationMaxY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2; // y=9
                    }
                    else
                    {
                        explorationMinX = GameConfig.PitRectX + 1; // 2
                        explorationMinY = GameConfig.PitRectY + 1; // 3
                        explorationMaxX = GameConfig.PitRectX + GameConfig.PitRectWidth - 2; // 11
                        explorationMaxY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2; // 9
                    }
                    
                    for (var x = explorationMinX; x <= explorationMaxX && !anyFog; x++)
                    {
                        for (var y = explorationMinY; y <= explorationMaxY; y++)
                        {
                            if (x >= 0 && y >= 0 && x < fogLayer.Width && y < fogLayer.Height)
                            {
                                var fogTile = fogLayer.GetTile(x, y);
                                if (fogTile != null)
                                {
                                    totalFogTiles++;
                                    anyFog = true;
                                    break;
                                }
                            }
                        }
                    }

                    Debug.Log($"[HeroStateMachine] Exploration check in area ({explorationMinX},{explorationMinY}) to ({explorationMaxX},{explorationMaxY}): {totalFogTiles} fog tiles remaining, anyFog={anyFog}");

                    if (!anyFog)
                        ws.Set(GoapConstants.MapExplored, true);
                }
            }

            Debug.Log($"[HeroStateMachine] State: PitInitialized={_hero.PitInitialized}, " +
                      $"AdjOut={_hero.AdjacentToPitBoundaryFromOutside}, " +
                      $"AdjIn={_hero.AdjacentToPitBoundaryFromInside}, " +
                      $"EnteredPit={_hero.EnteredPit}");
            return ws;
        }

        /// <summary>
        /// Get the goal state for GOAP planning
        /// </summary>
        private WorldState GetGoalState()
        {
            var goal = WorldState.Create(_planner);
            goal.Set(GoapConstants.MapExplored, true); // goal is exploration, not just entering pit
            return goal;
        }

        #region State Methods

        void Idle_Enter()
        {
            Debug.Log("[HeroStateMachine] Entering Idle state - planning next actions");
            
            // Get a plan to run that will get us from our current state to our goal state
            _actionPlan = _planner.Plan(GetWorldState(), GetGoalState());

            if (_actionPlan != null && _actionPlan.Count > 0)
            {
                Debug.Log($"[HeroStateMachine] Got an action plan with {_actionPlan.Count} actions: {string.Join(" -> ", _actionPlan)}");
                CurrentState = HeroState.GoTo;
            }
            else
            {
                Debug.Log("[HeroStateMachine] No action plan satisfied our goals");
                // Stay in Idle and try again next update
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
    }
}