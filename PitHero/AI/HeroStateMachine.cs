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

            // Setup our Actions and add them to the planner
            var moveToPit = new MoveToPitAction();
            _planner.AddAction(moveToPit);

            var jumpIntoPit = new JumpIntoPitAction();
            _planner.AddAction(jumpIntoPit);

            var wander = new WanderAction();
            _planner.AddAction(wander);
            
            // New actions for wizard orb and pit regeneration workflow
            var moveToWizardOrb = new MoveToWizardOrbAction();
            _planner.AddAction(moveToWizardOrb);
            
            var activateWizardOrb = new ActivateWizardOrbAction();
            _planner.AddAction(activateWizardOrb);
            
            var movingToInsidePitEdge = new MovingToInsidePitEdgeAction();
            _planner.AddAction(movingToInsidePitEdge);
            
            var jumpOutOfPit = new JumpOutOfPitAction();
            _planner.AddAction(jumpOutOfPit);
            
            var moveToPitGenPoint = new MoveToPitGenPointAction();
            _planner.AddAction(moveToPitGenPoint);

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
            if (_hero.InsidePit)
                ws.Set(GoapConstants.InsidePit, true);

            // Wizard orb workflow states
            if (_hero.ActivatedWizardOrb)
                ws.Set(GoapConstants.ActivatedWizardOrb, true);
            if (_hero.MovingToInsidePitEdge)
                ws.Set(GoapConstants.MovingToInsidePitEdge, true);
            if (_hero.ReadyToJumpOutOfPit)
                ws.Set(GoapConstants.ReadyToJumpOutOfPit, true);
            if (_hero.MovingToPitGenPoint)
                ws.Set(GoapConstants.MovingToPitGenPoint, true);

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

            // Check if wizard orb has been found (fog cleared around it)
            CheckWizardOrbFound(ws, tms);

            // Check additional positional states
            CheckAdditionalStates(ws);

            Debug.Log($"[HeroStateMachine] State: PitInitialized={_hero.PitInitialized}, " +
                      $"AdjOut={_hero.AdjacentToPitBoundaryFromOutside}, " +
                      $"AdjIn={_hero.AdjacentToPitBoundaryFromInside}, " +
                      $"InsidePit={_hero.InsidePit}");
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

        /// <summary>
        /// Check if wizard orb has been found (fog cleared around its position)
        /// </summary>
        private void CheckWizardOrbFound(WorldState ws, TiledMapService tms)
        {
            if (tms?.CurrentMap == null)
                return;

            var scene = Core.Scene;
            if (scene == null)
                return;

            // Find wizard orb entities
            var wizardOrbEntities = scene.FindEntitiesWithTag(GameConfig.TAG_WIZARD_ORB);
            if (wizardOrbEntities.Count == 0)
                return;

            var wizardOrbEntity = wizardOrbEntities[0]; // Should only be one
            var worldPos = wizardOrbEntity.Transform.Position;
            var tilePos = new Point((int)(worldPos.X / GameConfig.TileSize), (int)(worldPos.Y / GameConfig.TileSize));

            var fogLayer = tms.CurrentMap.GetLayer<Nez.Tiled.TmxLayer>("FogOfWar");
            if (fogLayer != null)
            {
                // Check if fog is cleared around the wizard orb position
                if (tilePos.X >= 0 && tilePos.Y >= 0 && tilePos.X < fogLayer.Width && tilePos.Y < fogLayer.Height)
                {
                    var fogTile = fogLayer.GetTile(tilePos.X, tilePos.Y);
                    if (fogTile == null) // No fog means it's been discovered
                    {
                        ws.Set(GoapConstants.FoundWizardOrb, true);
                        Debug.Log($"[HeroStateMachine] Wizard orb found at tile {tilePos.X},{tilePos.Y}");
                    }
                }
            }
        }

        /// <summary>
        /// Check additional positional states like AtWizardOrb and AtPitGenPoint
        /// </summary>
        private void CheckAdditionalStates(WorldState ws)
        {
            if (_hero?.Entity == null)
                return;

            var tileMover = _hero.Entity.GetComponent<TileByTileMover>();
            var currentTile = tileMover?.GetCurrentTileCoordinates() ?? 
                new Point((int)(_hero.Entity.Transform.Position.X / GameConfig.TileSize),
                         (int)(_hero.Entity.Transform.Position.Y / GameConfig.TileSize));

            // Check if at wizard orb
            CheckAtWizardOrb(ws, currentTile);

            // Check if at pit generation point (34, 6)
            if (currentTile.X == 34 && currentTile.Y == 6)
            {
                ws.Set(GoapConstants.AtPitGenPoint, true);
            }

            // Check if outside pit (not inside pit area)
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            var pitBounds = GetPitBounds(pitWidthManager);
            if (!pitBounds.Contains(currentTile))
            {
                ws.Set(GoapConstants.OutsidePit, true);
            }
        }

        /// <summary>
        /// Check if hero is at wizard orb position
        /// </summary>
        private void CheckAtWizardOrb(WorldState ws, Point heroTile)
        {
            var scene = Core.Scene;
            if (scene == null)
                return;

            var wizardOrbEntities = scene.FindEntitiesWithTag(GameConfig.TAG_WIZARD_ORB);
            if (wizardOrbEntities.Count == 0)
                return;

            var wizardOrbEntity = wizardOrbEntities[0];
            var orbWorldPos = wizardOrbEntity.Transform.Position;
            var orbTile = new Point((int)(orbWorldPos.X / GameConfig.TileSize), 
                                  (int)(orbWorldPos.Y / GameConfig.TileSize));

            if (heroTile.X == orbTile.X && heroTile.Y == orbTile.Y)
            {
                ws.Set(GoapConstants.AtWizardOrb, true);
            }
        }

        /// <summary>
        /// Get pit bounds rectangle for checking inside/outside
        /// </summary>
        private Rectangle GetPitBounds(PitWidthManager pitWidthManager)
        {
            if (pitWidthManager != null)
            {
                var width = pitWidthManager.CurrentPitRectWidthTiles;
                return new Rectangle(GameConfig.PitRectX, GameConfig.PitRectY, width, GameConfig.PitRectHeight);
            }
            else
            {
                return new Rectangle(GameConfig.PitRectX, GameConfig.PitRectY, GameConfig.PitRectWidth, GameConfig.PitRectHeight);
            }
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
                    _actionPlan = _planner.Plan(GetWorldState(), GetGoalState());

                    if (_actionPlan != null && _actionPlan.Count > 0)
                    {
                        Debug.Log($"[HeroStateMachine] Got an action plan with {_actionPlan.Count} actions: {string.Join(" -> ", _actionPlan)}");
                        CurrentState = HeroState.GoTo;
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
    }
}