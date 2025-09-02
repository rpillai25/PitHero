using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.FSM;
using Nez.AI.GOAP;
using Nez.Tiled;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;
using System;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Hero state machine that uses GOAP for planning and SimpleStateMachine for execution.
    /// Based on the Nez GoapMiner sample pattern.
    /// </summary>
    public class HeroStateMachine : SimpleStateMachine<ActorState>, IPausableComponent
    {
        private HeroComponent _hero;
        private ActionPlanner _planner;
        private Stack<Nez.AI.GOAP.Action> _actionPlan;
        private HeroActionBase _currentAction;

        // GoTo state tracking
        private List<Point> _currentPath;
        private int _pathIndex;
        private LocationType _targetLocationType;
        private Point _targetTile;

        // WanderPitAction exploration tracking  
        private HashSet<Point> _failedWanderTargets;

        /// <summary>
        /// Gets whether this component should respect the global pause state
        /// </summary>
        public bool ShouldPause => true;

        public HeroStateMachine()
        {
            // Initialize the ActionPlanner
            _planner = new ActionPlanner();

            // Initialize wander exploration tracking
            _failedWanderTargets = new HashSet<Point>(8); // Pre-allocate small capacity

            // Setup our Actions and add them to the planner - only the 5 required actions
            var jumpIntoPit = new JumpIntoPitAction();
            _planner.AddAction(jumpIntoPit);

            var wander = new WanderPitAction();
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
            
            // Set initial state to Idle - when it enters Idle it will ask the ActionPlanner for a new plan
            InitialState = ActorState.Idle;
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
                CurrentState = ActorState.GoTo;
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
                        CurrentState = ActorState.GoTo;
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
                CurrentState = ActorState.Idle;
                return;
            }

            // Peek the next action from the action plan
            var nextAction = _actionPlan.Peek();
            Debug.Log($"[HeroStateMachine] GoTo_Enter: Planning movement for action {nextAction.Name}");

            // Select location based on action name
            Point? targetLocation = CalculateTargetLocation(nextAction.Name);
            if (!targetLocation.HasValue)
            {
                Debug.Warn($"[HeroStateMachine] GoTo_Enter: Could not calculate target location for action {nextAction.Name}");
                CurrentState = ActorState.PerformAction; // Skip to action execution
                return;
            }

            _targetTile = targetLocation.Value;
            Debug.Log($"[HeroStateMachine] GoTo_Enter: Target location calculated as ({_targetTile.X},{_targetTile.Y})");

            // Get current tile position
            var tileMover = _hero.Entity.GetComponent<TileByTileMover>();
            var currentTile = tileMover?.GetCurrentTileCoordinates() ?? 
                new Point((int)(_hero.Entity.Transform.Position.X / GameConfig.TileSize),
                         (int)(_hero.Entity.Transform.Position.Y / GameConfig.TileSize));

            Debug.Log($"[HeroStateMachine] GoTo_Enter: Current position ({currentTile.X},{currentTile.Y})");

            // Calculate AStar path
            _currentPath = _hero.CalculatePath(currentTile, _targetTile);
            _pathIndex = 0;

            if (_currentPath == null || _currentPath.Count == 0)
            {
                Debug.Log($"[HeroStateMachine] GoTo_Enter: No path needed or found to target ({_targetTile.X},{_targetTile.Y})");
                
                // If this was for a WanderPitAction and pathfinding failed, track the failed target
                if (_actionPlan.Count > 0 && _actionPlan.Peek().Name == GoapConstants.WanderPitAction)
                {
                    Debug.Log($"[HeroStateMachine] GoTo_Enter: Pathfinding failed for WanderPitAction target ({_targetTile.X},{_targetTile.Y}), marking as failed and restarting planning");
                    AddFailedWanderTarget(_targetTile);
                    CurrentState = ActorState.Idle; // Restart planning
                    return;
                }
                
                Debug.Log("[HeroStateMachine] GoTo_Enter: Proceeding to action");
                CurrentState = ActorState.PerformAction;
                return;
            }

            Debug.Log($"[HeroStateMachine] GoTo_Enter: Found path with {_currentPath.Count} steps to ({_targetTile.X},{_targetTile.Y})");
        }

        void GoTo_Tick()
        {
            var tileMover = _hero.Entity.GetComponent<TileByTileMover>();
            if (tileMover == null)
            {
                Debug.Warn("[HeroStateMachine] GoTo_Tick: No TileByTileMover component found");
                CurrentState = ActorState.PerformAction;
                return;
            }

            // If currently moving, wait until the step completes
            if (tileMover.IsMoving)
            {
                return;
            }

            // If we consumed the path, only proceed if we truly arrived at the target tile
            if (_currentPath == null || _pathIndex >= _currentPath.Count)
            {
                var currentTile = tileMover.GetCurrentTileCoordinates();
                if (currentTile.X == _targetTile.X && currentTile.Y == _targetTile.Y)
                {
                    Debug.Log("[HeroStateMachine] GoTo_Tick: Arrived at target, transitioning to PerformAction");
                    CurrentState = ActorState.PerformAction;
                }
                else
                {
                    // Recalculate a short corrective path to the target
                    Debug.Log($"[HeroStateMachine] GoTo_Tick: Path consumed but not at target. Recalculating from ({currentTile.X},{currentTile.Y}) to ({_targetTile.X},{_targetTile.Y})");
                    _currentPath = _hero.CalculatePath(currentTile, _targetTile);
                    _pathIndex = 0;
                }
                return;
            }

            // Start the next step toward the next tile in the path
            var nextTile = _currentPath[_pathIndex];
            var curTile = tileMover.GetCurrentTileCoordinates();

            var direction = CalculateDirection(curTile, nextTile);
            if (direction.HasValue)
            {
                Debug.Log($"[HeroStateMachine] GoTo_Tick: Moving {direction.Value} to tile ({nextTile.X},{nextTile.Y}) [step {_pathIndex + 1}/{_currentPath.Count}]");
                if (tileMover.StartMoving(direction.Value))
                {
                    // Advance to the next path index; we will wait for IsMoving to clear before starting another step
                    _pathIndex++;
                }
                else
                {
                    Debug.Warn($"[HeroStateMachine] GoTo_Tick: Failed to start moving {direction.Value}");
                    // Try to recover by recalculating path from current tile
                    _currentPath = _hero.CalculatePath(curTile, _targetTile);
                    _pathIndex = 0;
                }
            }
            else
            {
                Debug.Warn($"[HeroStateMachine] GoTo_Tick: Cannot calculate direction from ({curTile.X},{curTile.Y}) to ({nextTile.X},{nextTile.Y})");
                _pathIndex++; // Skip this step
            }
        }

        void GoTo_Exit()
        {
            Debug.Log("[HeroStateMachine] Exiting GoTo state");
            
            // Snap to tile grid for precision
            var tileMover = _hero.Entity.GetComponent<TileByTileMover>();
            if (tileMover != null)
            {
                tileMover.SnapToTileGrid();
            }

            // Clear path data
            _currentPath = null;
            _pathIndex = 0;
        }

        void PerformAction_Enter()
        {
            Debug.Log("[HeroStateMachine] Entering PerformAction state");
            
            if (_actionPlan == null || _actionPlan.Count == 0)
            {
                Debug.Warn("[HeroStateMachine] PerformAction_Enter: No action plan available");
                CurrentState = ActorState.Idle;
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
                CurrentState = ActorState.Idle;
            }
        }

        void PerformAction_Tick()
        {
            if (_currentAction == null)
            {
                Debug.Warn("[HeroStateMachine] PerformAction_Tick: No current action");
                CurrentState = ActorState.Idle;
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
                    CurrentState = ActorState.PerformAction; // This will trigger PerformAction_Enter for next action
                }
                else
                {
                    Debug.Log("[HeroStateMachine] Action plan completed, returning to Idle");
                    CurrentState = ActorState.Idle;
                }
            }
        }

        void PerformAction_Exit()
        {
            Debug.Log("[HeroStateMachine] Exiting PerformAction state");
            // _currentAction is kept for potential cleanup in next state
        }

        #endregion

        #region Location Calculation Methods

        /// <summary>
        /// Calculate target location based on action name
        /// </summary>
        private Point? CalculateTargetLocation(string actionName)
        {
            switch (actionName)
            {
                case GoapConstants.JumpIntoPitAction:
                    _targetLocationType = LocationType.PitOutsideEdge;
                    return CalculatePitOutsideEdgeLocation();

                case GoapConstants.WanderPitAction:
                    _targetLocationType = LocationType.PitWanderPoint;
                    return CalculatePitWanderPointLocation();

                case GoapConstants.ActivateWizardOrbAction:
                    _targetLocationType = LocationType.WizardOrb;
                    return CalculateWizardOrbLocation();

                case GoapConstants.JumpOutOfPitAction:
                    _targetLocationType = LocationType.PitInsideEdge;
                    return CalculatePitInsideEdgeLocation();

                case GoapConstants.ActivatePitRegenAction:
                    _targetLocationType = LocationType.PitRegenPoint;
                    return CalculatePitRegenPointLocation();

                default:
                    _targetLocationType = LocationType.None;
                    Debug.Warn($"[HeroStateMachine] Unknown action name for location calculation: {actionName}");
                    return null;
            }
        }

        /// <summary>
        /// Calculate PitOutsideEdge location - position outside pit boundary for jumping in
        /// </summary>
        private Point? CalculatePitOutsideEdgeLocation()
        {
            // For jumping into pit, we want to be at the right edge outside the pit
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            var pitRightEdge = pitWidthManager?.CurrentPitRightEdge ?? (GameConfig.PitRectX + GameConfig.PitRectWidth);
            
            return new Point(pitRightEdge, GameConfig.PitCenterTileY); // Just outside right edge
        }

        /// <summary>
        /// Calculate PitWanderPoint location - find the next fog tile to explore in the pit
        /// Sets ExploredPit=true and transitions to PerformAction if no more tiles found
        /// </summary>
        private Point? CalculatePitWanderPointLocation()
        {
            if (_hero == null)
            {
                Debug.Warn("[HeroStateMachine] CalculatePitWanderPointLocation: Hero component is null");
                return null;
            }

            // Get current hero position
            var tileMover = _hero.Entity.GetComponent<TileByTileMover>();
            var heroTile = tileMover?.GetCurrentTileCoordinates() ?? 
                new Point((int)(_hero.Entity.Transform.Position.X / GameConfig.TileSize),
                         (int)(_hero.Entity.Transform.Position.Y / GameConfig.TileSize));

            var tms = Core.Services.GetService<TiledMapService>();
            if (tms?.CurrentMap == null)
            {
                Debug.Warn("[HeroStateMachine] CalculatePitWanderPointLocation: No tilemap service available");
                return null;
            }

            var fogLayer = tms.CurrentMap.GetLayer<TmxLayer>("FogOfWar");
            if (fogLayer == null)
            {
                Debug.Warn("[HeroStateMachine] CalculatePitWanderPointLocation: No FogOfWar layer found");
                return null;
            }

            Point? nearestUnknownTile = null;
            float shortestDistance = float.MaxValue;

            // Get pit bounds from PitWidthManager
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            int pitMinX, pitMinY, pitMaxX, pitMaxY;
            
            if (pitWidthManager != null)
            {
                // The explorable area is the inner area, excluding walls
                // Left wall is at PitRectX (x=1), so explorable starts at PitRectX + 1 (x=2)
                // Right wall is at CurrentPitRightEdge - 1, so explorable ends at CurrentPitRightEdge - 2
                pitMinX = GameConfig.PitRectX + 1; // x=2 (first explorable column)
                pitMinY = GameConfig.PitRectY + 1; // y=3 (first explorable row)
                pitMaxX = pitWidthManager.CurrentPitRightEdge - 2; // Last explorable column (before inner wall)
                pitMaxY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2; // y=9 (last explorable row)
                Debug.Log($"[HeroStateMachine] CalculatePitWanderPointLocation: Using dynamic pit bounds: explorable area ({pitMinX},{pitMinY}) to ({pitMaxX},{pitMaxY}), pit right edge={pitWidthManager.CurrentPitRightEdge}");
            }
            else
            {
                // Use default pit bounds
                pitMinX = GameConfig.PitRectX + 1; // 2
                pitMinY = GameConfig.PitRectY + 1; // 3
                pitMaxX = GameConfig.PitRectX + GameConfig.PitRectWidth - 2; // 11
                pitMaxY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2; // 9
                Debug.Log($"[HeroStateMachine] CalculatePitWanderPointLocation: Using default pit bounds: explorable area ({pitMinX},{pitMinY}) to ({pitMaxX},{pitMaxY})");
            }

            int fogTileCount = 0;
            int passableFogTileCount = 0;
            int skippedFailedCount = 0;

            // Scan the pit area for tiles still covered by fog
            for (int x = pitMinX; x <= pitMaxX; x++)
            {
                for (int y = pitMinY; y <= pitMaxY; y++)
                {
                    // Check if this tile is within bounds and has fog
                    if (x >= 0 && y >= 0 && x < fogLayer.Width && y < fogLayer.Height)
                    {
                        var tile = fogLayer.GetTile(x, y);
                        if (tile != null) // Tile has fog (unknown)
                        {
                            fogTileCount++;
                            
                            // Skip failed targets to avoid loops
                            var tilePoint = new Point(x, y);
                            if (_failedWanderTargets.Contains(tilePoint))
                            {
                                skippedFailedCount++;
                                continue;
                            }

                            // Check if tile is passable using hero's pathfinding component
                            bool isPassable = true;
                            if (_hero.IsPathfindingInitialized)
                            {
                                isPassable = _hero.IsPassable(new Point(x, y));
                            }
                            
                            if (!isPassable)
                            {
                                Debug.Log($"[HeroStateMachine] CalculatePitWanderPointLocation: Tile ({x},{y}) has fog but is not passable");
                                continue;
                            }
                            
                            passableFogTileCount++;

                            // Calculate distance from hero to this tile
                            var distance = Vector2.Distance(
                                new Vector2(heroTile.X, heroTile.Y),
                                new Vector2(x, y)
                            );

                            if (distance < shortestDistance)
                            {
                                shortestDistance = distance;
                                nearestUnknownTile = new Point(x, y);
                            }
                        }
                    }
                }
            }

            Debug.Log($"[HeroStateMachine] CalculatePitWanderPointLocation: Found {fogTileCount} fog tiles total, {passableFogTileCount} passable fog tiles, skipped {skippedFailedCount} failed targets");

            if (nearestUnknownTile.HasValue)
            {
                Debug.Log($"[HeroStateMachine] CalculatePitWanderPointLocation: Found nearest unknown tile at {nearestUnknownTile.Value.X},{nearestUnknownTile.Value.Y} distance {shortestDistance}");
                return nearestUnknownTile;
            }
            else
            {
                Debug.Log("[HeroStateMachine] CalculatePitWanderPointLocation: No unknown tiles found in pit area");
                
                // If we have failed targets and no valid targets, clear failed targets and try once more
                if (_failedWanderTargets.Count > 0)
                {
                    Debug.Log($"[HeroStateMachine] CalculatePitWanderPointLocation: Clearing {_failedWanderTargets.Count} failed targets to retry exploration");
                    _failedWanderTargets.Clear();
                    // Return null to trigger retry - this will cause the action to be re-planned
                    return null;
                }
                
                // No more tiles to explore - mark exploration as complete
                Debug.Log("[HeroStateMachine] CalculatePitWanderPointLocation: Pit exploration complete, setting ExploredPit=true");
                _hero.ExploredPit = true;
                
                // Transition to PerformAction so the final WanderPitAction can run to clear fog and check wizard orb
                Debug.Log("[HeroStateMachine] CalculatePitWanderPointLocation: Transitioning to PerformAction for final WanderPitAction execution");
                CurrentState = ActorState.PerformAction;
                return null;
            }
        }

        /// <summary>
        /// Add a failed wander target to prevent future attempts to reach it
        /// </summary>
        public void AddFailedWanderTarget(Point target)
        {
            _failedWanderTargets.Add(target);
            Debug.Log($"[HeroStateMachine] Added failed wander target ({target.X},{target.Y}), total failed targets: {_failedWanderTargets.Count}");
        }

        /// <summary>
        /// Calculate WizardOrb location - position of the wizard orb entity
        /// </summary>
        private Point? CalculateWizardOrbLocation()
        {
            var scene = Core.Scene;
            if (scene == null) return null;

            var wizardOrbEntities = scene.FindEntitiesWithTag(GameConfig.TAG_WIZARD_ORB);
            if (wizardOrbEntities.Count == 0) return null;

            var wizardOrbEntity = wizardOrbEntities[0];
            var worldPos = wizardOrbEntity.Transform.Position;
            var tilePos = new Point((int)(worldPos.X / GameConfig.TileSize), (int)(worldPos.Y / GameConfig.TileSize));
            
            return tilePos;
        }

        /// <summary>
        /// Calculate PitInsideEdge location - position inside pit near edge for jumping out
        /// </summary>
        private Point? CalculatePitInsideEdgeLocation()
        {
            // For jumping out, be near the right inside edge of the pit
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            var pitRightEdge = pitWidthManager?.CurrentPitRightEdge ?? (GameConfig.PitRectX + GameConfig.PitRectWidth);
            
            return new Point(pitRightEdge - 2, GameConfig.PitCenterTileY); // Just inside right edge
        }

        /// <summary>
        /// Calculate PitRegenPoint location - predefined location for pit regeneration
        /// </summary>
        private Point? CalculatePitRegenPointLocation()
        {
            // Use map center as the pit regeneration point (outside pit area)
            return new Point(GameConfig.MapCenterTileX, GameConfig.MapCenterTileY);
        }

        /// <summary>
        /// Calculate direction from current tile to target tile
        /// </summary>
        private Direction? CalculateDirection(Point from, Point to)
        {
            var deltaX = to.X - from.X;
            var deltaY = to.Y - from.Y;

            // Only support cardinal directions (no diagonals for now)
            if (deltaX == 0 && deltaY == -1) return Direction.Up;
            if (deltaX == 0 && deltaY == 1) return Direction.Down;
            if (deltaX == -1 && deltaY == 0) return Direction.Left;
            if (deltaX == 1 && deltaY == 0) return Direction.Right;

            // For diagonals, pick the stronger component or default to horizontal
            if (deltaX != 0 && deltaY != 0)
            {
                return Math.Abs(deltaX) >= Math.Abs(deltaY) 
                    ? (deltaX > 0 ? Direction.Right : Direction.Left)
                    : (deltaY > 0 ? Direction.Down : Direction.Up);
            }

            // If tiles are the same, no movement needed
            if (deltaX == 0 && deltaY == 0) return null;

            // Handle multi-tile movements by picking primary direction
            if (Math.Abs(deltaX) > Math.Abs(deltaY))
                return deltaX > 0 ? Direction.Right : Direction.Left;
            else
                return deltaY > 0 ? Direction.Down : Direction.Up;
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
            //Debug.Log("  WanderPitAction: Requires InsidePit=true");
            //Debug.Log("  MoveToWizardOrbAction: Requires FoundWizardOrb=true, MapExplored=true");
            //Debug.Log("  ActivateWizardOrbAction: Requires AtWizardOrb=true");
            //Debug.Log("  MovingToInsidePitEdgeAction: Requires MovingToInsidePitEdge=true");
            //Debug.Log("  JumpOutOfPitAction: Requires ReadyToJumpOutOfPit=true");
            //Debug.Log("  MoveToPitGenPointAction: Requires MovingToPitGenPoint=true");
        }
        
        #endregion
    }
}