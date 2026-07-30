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
        private HeroComponent _heroComponent;
        private ActionPlanner _planner;
        private Stack<Nez.AI.GOAP.Action> _actionPlan;
        private MercenaryActionBase _currentAction;

        // Track expected world state when plan was created to detect unexpected changes
        private bool _expectedMercInPit;
        private bool _expectedTargetInPit;

        // Throttles replan attempts while legitimately waiting without a plan (e.g. the merc
        // jumped in ahead of the hero and following is blocked until the hero lands)
        private float _planRetryTimer;

        public bool ShouldPause => true;

        /// <summary>
        /// True only while the GOAP executor is actively running FollowTargetAction. This is
        /// the sole window in which MercenaryFollowComponent may move the merc — during any
        /// other action (walk-to-edge, jumps) or a planless wait, following must not fight the
        /// current action for control of the mover/transform.
        /// </summary>
        public bool IsExecutingFollowAction => CurrentState == ActorState.PerformAction
            && _currentAction != null
            && _currentAction.Name == GoapConstants.FollowTargetAction;

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

            if (_mercenary == null)
                return;

            // Recover if the follow target was destroyed (e.g. the mercenary ahead in the
            // follow chain died in battle) — fall back to following the hero and replan
            if (_mercenary.FollowTarget != null && _mercenary.FollowTarget.IsDestroyed)
            {
                _mercenary.FollowTarget = Entity.Scene?.FindEntity("hero");
                Debug.Log($"[MercenaryStateMachine] {Entity.Name} follow target was destroyed, falling back to hero");

                _currentAction = null;
                _actionPlan = null;
                if (_mercenary.FollowTarget != null)
                {
                    if (CurrentState != ActorState.Idle)
                        CurrentState = ActorState.Idle;   // Idle_Enter replans with the new target
                    else
                        Idle_Enter();                     // already Idle — replan directly
                }
            }

            if (!_mercenary.IsHired || _mercenary.FollowTarget == null)
            {
                return;
            }

            base.Update();
        }

        private void Idle_Enter()
        {
            _planRetryTimer = 0f;
            _actionPlan = _planner.Plan(GetCurrentState(), GetGoalState());

            if (_actionPlan != null && _actionPlan.Count > 0)
            {
                // Store the expected world state when this plan was created
                _expectedMercInPit = IsMercenaryInsidePit();
                _expectedTargetInPit = IsHeroEffectivelyInsidePit();

                CurrentState = ActorState.PerformAction;
            }
            else
            {
                // A planless wait is legitimate mid-transition: e.g. the merc already jumped in
                // but the hero is still walking to the rim, so following is blocked until the
                // pit states match again. Idle_Tick retries until the world catches up.
                Debug.Log($"[MercenaryStateMachine] {Entity.Name} no action plan found — waiting. " +
                    $"MercInPit={IsMercenaryInsidePit()}, HeroEffectiveInPit={IsHeroEffectivelyInsidePit()}, " +
                    $"HeroActualInPit={IsHeroActuallyInsidePit()}, AtPitEdge={IsAtPitEdge()}");
            }
        }

        private void Idle_Tick()
        {
            if (_actionPlan != null && _actionPlan.Count > 0)
                return;

            _planRetryTimer += Time.DeltaTime;
            if (_planRetryTimer < 1f)
                return;

            Idle_Enter();
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
        }

        private void PerformAction_Tick()
        {
            if (_currentAction == null)
            {
                CurrentState = ActorState.Idle;
                return;
            }

            // Check if world state has changed significantly (e.g., target jumped into/out of pit)
            // This allows us to interrupt continuous actions like FollowTargetAction
            if (ShouldReplan() && !_currentAction.ShouldNotOverride())
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
                _currentAction = null;

                if (_actionPlan == null || _actionPlan.Count == 0)
                {
                    CurrentState = ActorState.Idle;
                }
                else
                {
                    CurrentState = ActorState.PerformAction;
                }
            }
        }

        private void PerformAction_Exit()
        {
            // Snap to tile grid for precision before transitioning to next state
            var tileMover = Entity.GetComponent<TileByTileMover>();
            if (tileMover != null)
            {
                tileMover.SnapToTileGrid();
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

            // Get current pit states (hero intent counts — see IsHeroEffectivelyInsidePit)
            bool mercInPit = IsMercenaryInsidePit();
            bool targetInPit = IsHeroEffectivelyInsidePit();

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

                // Following is only allowed while merc and hero are on the same side of the pit
                // boundary — if the states diverged (e.g. a stranded-recovery flag flip), replan
                // so the merc jumps rather than follows across
                if (mercInPit != IsHeroActuallyInsidePit())
                {
                    Debug.Log($"[MercenaryStateMachine] {Entity.Name} pit state no longer matches hero while following — replanning");
                    return true;
                }

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
            state.Set(GoapConstants.IsAlive, true);

            bool mercInPit = IsMercenaryInsidePit();
            bool targetInPit = IsHeroEffectivelyInsidePit();
            bool atPitEdge = IsAtPitEdge();

            state.Set(GoapConstants.MercenaryInsidePit, mercInPit);
            state.Set(GoapConstants.TargetInsidePit, targetInPit);
            state.Set(GoapConstants.MercenaryAtPitEdge, atPitEdge);
            state.Set(GoapConstants.PitStateMatchesHero, mercInPit == IsHeroActuallyInsidePit());

            return state;
        }

        private WorldState GetGoalState()
        {
            var goal = WorldState.Create(_planner);

            // The hero's plan intent drives the goal: when the hero decides to jump in or out,
            // every merc adopts the same destination immediately and gets there on its own
            bool targetInPit = IsHeroEffectivelyInsidePit();

            goal.Set(GoapConstants.MercenaryFollowingTarget, true);
            goal.Set(GoapConstants.MercenaryInsidePit, targetInPit);

            return goal;
        }

        private bool IsMercenaryInsidePit()
        {
            // The flag is authoritative: it flips only when a jump completes (or the follow
            // component warps across the pit boundary). Deriving it from position misclassifies
            // a merc standing over the pit rect that never jumped in (e.g. the pit widened
            // underneath it), which silently disables the walk-to-edge -> jump plan.
            return _mercenary != null && _mercenary.InsidePit;
        }

        /// <summary>
        /// Pit reads always target the HERO, never the follow target: merc #2 follows merc #1
        /// for formation only, and keying pit decisions off another merc would chain the party's
        /// pit entry/exit instead of keeping each member independent.
        /// </summary>
        private HeroComponent GetHeroComponent()
        {
            // Hero promotion destroys the old hero entity and spawns a new one — re-resolve when stale
            if (_heroComponent == null || _heroComponent.Entity == null || _heroComponent.Entity.IsDestroyed)
                _heroComponent = Entity?.Scene?.FindEntity("hero")?.GetComponent<HeroComponent>();
            return _heroComponent;
        }

        private bool IsHeroActuallyInsidePit()
        {
            // Flag-based read (never tile geometry): a position-derived answer flickers while
            // the hero lerps over the pit wall mid-jump, aborting this merc's plan every time.
            var hero = GetHeroComponent();
            return hero != null && hero.InsidePit;
        }

        private bool IsHeroEffectivelyInsidePit()
        {
            // The hero's plan intent counts as much as the flag: when the hero *plans* a jump
            // the mercs adopt the same goal immediately instead of waiting for the landing
            var hero = GetHeroComponent();
            return hero != null && hero.IntendsInsidePit;
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
    }
}

