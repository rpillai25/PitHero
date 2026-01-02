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
    /// Mercenary state machine that uses GOAP for planning and SimpleStateMachine for execution.
    /// Handles following the target (hero or another mercenary) and jumping in/out of the pit.
    /// </summary>
    public class MercenaryStateMachine : SimpleStateMachine<ActorState>, IPausableComponent
    {
        private MercenaryComponent _mercenary;
        private ActionPlanner _planner;
        private Stack<Nez.AI.GOAP.Action> _actionPlan;
        private MercenaryActionBase _currentAction;

        // Track expected world state when plan was created to detect unexpected changes
        private bool _expectedMercInPit;
        private bool _expectedTargetInPit;

        public bool ShouldPause => true;

        public MercenaryStateMachine()
        {
            _planner = new ActionPlanner();

            var followTarget = new FollowTargetAction();
            _planner.AddAction(followTarget);

            var walkToPitEdge = new WalkToPitEdgeAction();
            _planner.AddAction(walkToPitEdge);

            var jumpIntoPit = new MercenaryJumpIntoPitAction();
            _planner.AddAction(jumpIntoPit);

            var jumpOutOfPit = new MercenaryJumpOutOfPitAction();
            _planner.AddAction(jumpOutOfPit);
        }

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            _mercenary = Entity.GetComponent<MercenaryComponent>();
            InitialState = ActorState.Idle;
        }

        public override void Update()
        {
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService?.IsPaused == true)
                return;

            if (_mercenary == null || !_mercenary.IsHired || _mercenary.FollowTarget == null)
            {
                return;
            }

            base.Update();
        }

        private void Idle_Enter()
        {
            Debug.Log($"[MercenaryStateMachine] {Entity.Name} entering Idle state - planning actions");

            // Debug current state before planning
            var currentTile = GetCurrentTile();
            var atPitEdge = IsAtPitEdge();
            var mercInPit = IsMercenaryInsidePit();
            var targetInPit = IsTargetInsidePit();
            Debug.Log($"[MercenaryStateMachine] {Entity.Name} current state: tile=({currentTile.X},{currentTile.Y}), atPitEdge={atPitEdge}, mercInPit={mercInPit}, targetInPit={targetInPit}");

            _actionPlan = _planner.Plan(GetCurrentState(), GetGoalState());

            if (_actionPlan != null && _actionPlan.Count > 0)
            {
                Debug.Log($"[MercenaryStateMachine] {Entity.Name} got action plan with {_actionPlan.Count} actions: {string.Join(" -> ", _actionPlan)}");
                
                // Store the expected world state when this plan was created
                _expectedMercInPit = IsMercenaryInsidePit();
                _expectedTargetInPit = IsTargetInsidePit();
                Debug.Log($"[MercenaryStateMachine] {Entity.Name} plan expects: merc in pit={_expectedMercInPit}, target in pit={_expectedTargetInPit}");

                CurrentState = ActorState.PerformAction;
            }
            else
            {
                Debug.Warn($"[MercenaryStateMachine] {Entity.Name} no action plan found!");
                Debug.Log($"[MercenaryStateMachine] Current state: MercInPit={IsMercenaryInsidePit()}, TargetInPit={IsTargetInsidePit()}, AtPitEdge={IsAtPitEdge()}");
                Debug.Log($"[MercenaryStateMachine] Goal: MercFollowingTarget=true, MercInPit={IsTargetInsidePit()}");
            }
        }

        private void Idle_Tick()
        {
        }

        private void PerformAction_Enter()
        {
            if (_actionPlan == null || _actionPlan.Count == 0)
            {
                Debug.Warn($"[MercenaryStateMachine] {Entity.Name} PerformAction_Enter with no action plan");
                CurrentState = ActorState.Idle;
                return;
            }

            var nextAction = _actionPlan.Pop();
            _currentAction = nextAction as MercenaryActionBase;

            if (_currentAction == null)
            {
                Debug.Warn($"[MercenaryStateMachine] {Entity.Name} action is not MercenaryActionBase");
                CurrentState = ActorState.Idle;
            }
            else
            {
                Debug.Log($"[MercenaryStateMachine] {Entity.Name} starting action: {_currentAction.Name}");
            }
        }

        private void PerformAction_Tick()
        {
            if (_currentAction == null)
            {
                Debug.Log($"[MercenaryStateMachine] {Entity.Name} no current action, returning to Idle");
                CurrentState = ActorState.Idle;
                return;
            }

            // Check if world state has changed significantly (e.g., target jumped into/out of pit)
            // This allows us to interrupt continuous actions like FollowTargetAction
            if (ShouldReplan())
            {
                Debug.Log($"[MercenaryStateMachine] {Entity.Name} world state changed, interrupting {_currentAction.Name} and replanning");
                _currentAction = null;
                _actionPlan = null;
                CurrentState = ActorState.Idle;
                return;
            }

            var isComplete = _currentAction.Execute(_mercenary);

            if (isComplete)
            {
                Debug.Log($"[MercenaryStateMachine] {Entity.Name} action {_currentAction.Name} completed");
                _currentAction = null;

                if (_actionPlan == null || _actionPlan.Count == 0)
                {
                    Debug.Log($"[MercenaryStateMachine] {Entity.Name} action complete, no more actions, returning to Idle");
                    CurrentState = ActorState.Idle;
                }
                else
                {
                    Debug.Log($"[MercenaryStateMachine] {Entity.Name} action complete, moving to next action");
                    CurrentState = ActorState.PerformAction;
                }
            }
        }

        private void PerformAction_Exit()
        {
            Debug.Log($"[MercenaryStateMachine] {Entity.Name} exiting PerformAction state");

            // Snap to tile grid for precision before transitioning to next state
            var tileMover = Entity.GetComponent<TileByTileMover>();
            if (tileMover != null)
            {
                tileMover.SnapToTileGrid();
                Debug.Log($"[MercenaryStateMachine] {Entity.Name} snapped to tile grid at ({GetCurrentTile().X},{GetCurrentTile().Y})");
            }
        }

        /// <summary>
        /// Check if we should replan due to significant world state changes
        /// </summary>
        private bool ShouldReplan()
        {
            // Don't replan if we have no current action
            if (_currentAction == null)
                return false;

            // Get current pit states
            bool mercInPit = IsMercenaryInsidePit();
            bool targetInPit = IsTargetInsidePit();

            // If currently following and both are in same location, no need to replan
            if (_currentAction.Name == GoapConstants.FollowTargetAction)
            {
                // Only replan if there's an UNEXPECTED change from when the plan was created
                // The plan may have been created expecting them to be in different locations
                // (e.g., merc outside, target inside, plan is to walk to edge -> jump in -> follow)
                // We should only replan if the target's location changed unexpectedly
                bool targetLocationChanged = targetInPit != _expectedTargetInPit;
                if (targetLocationChanged)
                {
                    Debug.Log($"[MercenaryStateMachine] {Entity.Name} target location changed unexpectedly: expected={_expectedTargetInPit}, actual={targetInPit}");
                    return true;
                }
                
                // If mercenary and target are in same location now, we're good (plan succeeded)
                if (mercInPit == targetInPit)
                {
                    return false;
                }
                
                // If mercenary hasn't reached target's location yet, but target location hasn't changed,
                // continue with current plan (don't replan)
                return false;
            }

            // If currently walking to pit edge or jumping, check if we should abort
            if (_currentAction.Name == GoapConstants.WalkToPitEdgeAction ||
                _currentAction.Name == GoapConstants.MercenaryJumpIntoPitAction)
            {
                // Abort if target's location changed unexpectedly
                bool targetLocationChanged = targetInPit != _expectedTargetInPit;
                if (targetLocationChanged)
                {
                    Debug.Log($"[MercenaryStateMachine] {Entity.Name} target location changed during {_currentAction.Name}: expected={_expectedTargetInPit}, actual={targetInPit}");
                    return true;
                }
                return false;
            }

            if (_currentAction.Name == GoapConstants.MercenaryJumpOutOfPitAction)
            {
                // Abort if target's location changed unexpectedly
                bool targetLocationChanged = targetInPit != _expectedTargetInPit;
                if (targetLocationChanged)
                {
                    Debug.Log($"[MercenaryStateMachine] {Entity.Name} target location changed during {_currentAction.Name}: expected={_expectedTargetInPit}, actual={targetInPit}");
                    return true;
                }
                return false;
            }

            return false;
        }

        private WorldState GetCurrentState()
        {
            var state = WorldState.Create(_planner);
            state.Set(GoapConstants.HeroInitialized, true);
            state.Set(GoapConstants.PitInitialized, true);

            bool mercInPit = IsMercenaryInsidePit();
            bool targetInPit = IsTargetInsidePit();
            bool atPitEdge = IsAtPitEdge();

            state.Set(GoapConstants.MercenaryInsidePit, mercInPit);
            state.Set(GoapConstants.TargetInsidePit, targetInPit);
            state.Set(GoapConstants.MercenaryAtPitEdge, atPitEdge);

            return state;
        }

        private WorldState GetGoalState()
        {
            var goal = WorldState.Create(_planner);

            bool targetInPit = IsTargetInsidePit();
            
            goal.Set(GoapConstants.MercenaryFollowingTarget, true);
            goal.Set(GoapConstants.MercenaryInsidePit, targetInPit);

            return goal;
        }

        private bool IsMercenaryInsidePit()
        {
            var myTile = GetCurrentTile();
            return IsInsidePit(myTile);
        }

        private bool IsTargetInsidePit()
        {
            if (_mercenary?.FollowTarget == null)
                return false;

            var targetHero = _mercenary.FollowTarget.GetComponent<HeroComponent>();
            if (targetHero != null)
            {
                return targetHero.InsidePit;
            }

            var targetMerc = _mercenary.FollowTarget.GetComponent<MercenaryComponent>();
            if (targetMerc != null)
            {
                var targetTile = new Point(
                    (int)(targetMerc.Entity.Transform.Position.X / GameConfig.TileSize),
                    (int)(targetMerc.Entity.Transform.Position.Y / GameConfig.TileSize)
                );
                return IsInsidePit(targetTile);
            }

            return false;
        }

        private bool IsAtPitEdge()
        {
            var currentTile = GetCurrentTile();
            var pitEdgeTile = FindPitEdge();
            return currentTile == pitEdgeTile;
        }

        private Point FindPitEdge()
        {
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager == null)
                return Point.Zero;

            var pitLeft = GameConfig.PitRectX;
            var pitWidth = pitWidthManager.CurrentPitRectWidthTiles;
            var pitRight = pitLeft + pitWidth - 1;

            return new Point(pitRight, GameConfig.PitCenterTileY);
        }

        private Point GetCurrentTile()
        {
            var pos = Entity.Transform.Position;
            return new Point(
                (int)(pos.X / GameConfig.TileSize),
                (int)(pos.Y / GameConfig.TileSize)
            );
        }

        private bool IsInsidePit(Point tile)
        {
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager == null)
                return false;

            var pitLeft = GameConfig.PitRectX;
            var pitTop = GameConfig.PitRectY;
            var pitWidth = pitWidthManager.CurrentPitRectWidthTiles;
            var pitHeight = GameConfig.PitRectHeight;
            var pitRight = pitLeft + pitWidth - 1;
            var pitBottom = pitTop + pitHeight - 1;

            // Use exclusive boundaries - pit edge is NOT inside the pit
            return tile.X > pitLeft && tile.X < pitRight && tile.Y > pitTop && tile.Y < pitBottom;
        }
    }
}

