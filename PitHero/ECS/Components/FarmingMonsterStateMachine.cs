using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.FSM;
using PitHero.Farming;
using PitHero.Services;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Drives a single farming monster: emerge from the monster house door, claim till actions from
    /// the FarmTaskCoordinator, walk to the stand tile beside the target, swing the forked hoe, and
    /// wander the farm when no work remains. RequestReturnHome sends it back into the house.
    /// </summary>
    public class FarmingMonsterStateMachine : SimpleStateMachine<FarmingMonsterState>, IPausableComponent
    {
        private readonly AlliedMonster _monster;
        private readonly FarmTaskCoordinator _coordinator;
        private readonly Point _houseAnchorTile;

        private FarmMonsterMover _mover;
        private ActorFacingComponent _facing;
        private PauseService _pauseService;
        private TilledTileService _tilledTileService;

        /// <summary>Animator for the ForkedHoe swing; assigned by the coordinator at spawn.</summary>
        public PausableSpriteAnimator HoeAnimator;

        /// <summary>Body animator, used to match the hoe's layer depth while tilling.</summary>
        public EnemyAnimationComponent BodyAnimator;

        private FarmAction _currentAction;
        private bool _hasAction;
        private bool _standRight;        // standing right of the target (preferred) vs left (fallback)
        private bool _goHome;
        private bool _returnReachedExit; // ReturnHome phase: walked to the exit tile, stepping into the door
        private float _tillDuration;

        public bool ShouldPause => true;

        /// <summary>True once the monster has been asked to walk home and despawn.</summary>
        public bool IsReturningHome => _goHome;

        private Point DoorTile => new Point(_houseAnchorTile.X, _houseAnchorTile.Y + 2);
        private Point ExitTile => new Point(_houseAnchorTile.X, _houseAnchorTile.Y + 3);

        public FarmingMonsterStateMachine(AlliedMonster monster, FarmTaskCoordinator coordinator, Point houseAnchorTile)
        {
            _monster = monster;
            _coordinator = coordinator;
            _houseAnchorTile = houseAnchorTile;
        }

        public override void OnAddedToEntity()
        {
            _mover = Entity.GetComponent<FarmMonsterMover>();
            _facing = Entity.GetComponent<ActorFacingComponent>();
            _pauseService = Core.Services.GetService<PauseService>();
            _tilledTileService = Core.Services.GetService<TilledTileService>();

            InitialState = FarmingMonsterState.EmergeFromHouse;
        }

        public override void Update()
        {
            if (_pauseService?.IsPaused == true)
                return;
            base.Update();
        }

        /// <summary>Asks the monster to finish what it's doing, walk back into its house, and despawn.</summary>
        public void RequestReturnHome() => _goHome = true;

        /// <summary>Cancels a pending return (job re-assigned to Farming before the monster got home).</summary>
        public void CancelReturnHome()
        {
            if (!_goHome)
                return;
            _goHome = false;
            if (CurrentState == FarmingMonsterState.ReturnHome)
            {
                _mover.Stop();
                CurrentState = FarmingMonsterState.Idle;
            }
        }

        // ---------------------------------------------------------------- EmergeFromHouse

        private void EmergeFromHouse_Enter()
        {
            _facing?.SetFacing(Direction.Down);

            // Scripted single step out of the door; the door row is solid in the pathfinding graph,
            // so this bypasses A*. Near the map's bottom edge there may be no exit row — stay put.
            if (_coordinator.Pathfinder.IsPassable(ExitTile))
                _mover.SetSingleTarget(TileCenter(ExitTile));
        }

        private void EmergeFromHouse_Tick()
        {
            if (!_mover.IsMoving)
                CurrentState = FarmingMonsterState.Idle;
        }

        // ---------------------------------------------------------------- Idle

        private void Idle_Tick()
        {
            if (_goHome)
            {
                CurrentState = FarmingMonsterState.ReturnHome;
                return;
            }

            if (elapsedTimeInState < GameConfig.FarmMonsterIdlePollInterval)
                return;

            if (_coordinator.TryClaimAction(out _currentAction))
            {
                _hasAction = true;
                if (TryPathToStandTile())
                {
                    CurrentState = FarmingMonsterState.MoveToTask;
                }
                else
                {
                    _coordinator.ReportBlocked(in _currentAction);
                    _hasAction = false;
                }
                return;
            }

            CurrentState = FarmingMonsterState.Wander;
        }

        // ---------------------------------------------------------------- MoveToTask

        private void MoveToTask_Tick()
        {
            if (_goHome)
            {
                AbandonCurrentAction();
                CurrentState = FarmingMonsterState.ReturnHome;
                return;
            }

            if (_mover.IsMoving)
                return;

            // Arrived — re-validate before swinging the hoe
            var tile = _currentAction.TargetTile;
            var tileState = Core.Services.GetService<TileStateService>();
            bool stillValid = tileState != null && tileState.HasFlag(tile, TileStateFlag.ReadyToTill);
            if (!stillValid)
            {
                _coordinator.CompleteAction(in _currentAction);   // drop the stale claim
                _hasAction = false;
                CurrentState = FarmingMonsterState.Idle;
                return;
            }

            CurrentState = FarmingMonsterState.PerformTill;
        }

        // ---------------------------------------------------------------- PerformTill

        private void PerformTill_Enter()
        {
            _facing?.SetFacing(_standRight ? Direction.Left : Direction.Right);

            float proficiencyScale = 1f - GameConfig.TillProficiencySpeedStep * (_monster.FarmingProficiency - 1);
            _tillDuration = GameConfig.TillBaseDurationSeconds * proficiencyScale;

            if (HoeAnimator != null)
            {
                // Lower-left quadrant of the 32px monster sprite (mirrored when standing left of the target)
                float quadrant = GameConfig.TileSize / 4f;
                HoeAnimator.SetLocalOffset(new Vector2(_standRight ? -quadrant : quadrant, quadrant));
                HoeAnimator.FlipX = !_standRight;
                if (BodyAnimator != null)
                    HoeAnimator.SetLayerDepth(BodyAnimator.LayerDepth - 0.0001f);
                HoeAnimator.SetEnabled(true);
                HoeAnimator.Play("ForkedHoe", Nez.Sprites.SpriteAnimator.LoopMode.Loop);
            }
        }

        private void PerformTill_Tick()
        {
            if (elapsedTimeInState < _tillDuration)
                return;

            // Complete before TillTile so the ReadyToTill-cleared event is a no-op for the queue
            _coordinator.CompleteAction(in _currentAction);
            _tilledTileService?.TillTile(_currentAction.TargetTile);
            _hasAction = false;
            CurrentState = FarmingMonsterState.Idle;
        }

        private void PerformTill_Exit()
        {
            HoeAnimator?.SetEnabled(false);
        }

        // ---------------------------------------------------------------- Wander

        private void Wander_Enter()
        {
            if (!TryPathToWanderTarget())
                CurrentState = FarmingMonsterState.Idle;
        }

        private void Wander_Tick()
        {
            if (_goHome)
            {
                _mover.Stop();
                CurrentState = FarmingMonsterState.ReturnHome;
                return;
            }

            if (!_mover.IsMoving)
                CurrentState = FarmingMonsterState.Idle;
        }

        // ---------------------------------------------------------------- ReturnHome

        private void ReturnHome_Enter()
        {
            _returnReachedExit = false;

            // No way back (shouldn't happen on the open farm) — vanish in place
            if (!TrySetPathTo(ExitTile))
                Entity.Destroy();
        }

        private void ReturnHome_Tick()
        {
            if (_mover.IsMoving)
                return;

            if (!_returnReachedExit)
            {
                // Scripted final step up into the door, mirroring EmergeFromHouse
                _returnReachedExit = true;
                _facing?.SetFacing(Direction.Up);
                _mover.SetSingleTarget(TileCenter(DoorTile));
                return;
            }

            Entity.Destroy();
        }

        // ---------------------------------------------------------------- helpers

        private void AbandonCurrentAction()
        {
            if (!_hasAction)
                return;
            _mover.Stop();
            _coordinator.ReleaseAction(in _currentAction);
            _hasAction = false;
        }

        /// <summary>
        /// Paths to the stand tile beside the till target: one tile to the right (preferred) or one
        /// tile to the left (mirrored hoe). Returns false when neither is reachable.
        /// </summary>
        private bool TryPathToStandTile()
        {
            var target = _currentAction.TargetTile;

            var rightStand = new Point(target.X + 1, target.Y);
            if (TrySetPathTo(rightStand))
            {
                _standRight = true;
                return true;
            }

            var leftStand = new Point(target.X - 1, target.Y);
            if (TrySetPathTo(leftStand))
            {
                _standRight = false;
                return true;
            }

            return false;
        }

        private bool TrySetPathTo(Point goal)
        {
            if (!_coordinator.Pathfinder.IsPassable(goal))
                return false;

            var start = _mover.CurrentTile;
            if (start == goal)
            {
                _mover.Stop();
                return true;
            }

            var path = _coordinator.Pathfinder.Search(start, goal);
            if (path == null)
                return false;

            _mover.SetPath(_coordinator.Pathfinder.SmoothPath(start, path));
            return true;
        }

        private bool TryPathToWanderTarget()
        {
            var pathfinder = _coordinator.Pathfinder;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int x = Nez.Random.Range(GameConfig.FarmMinWanderTileX, pathfinder.Width);
                int y = Nez.Random.Range(1, pathfinder.Height - 1);
                var goal = new Point(x, y);
                if (goal == _mover.CurrentTile)
                    continue;
                if (TrySetPathTo(goal))
                    return true;
            }
            return false;
        }

        private static Vector2 TileCenter(Point tile)
        {
            return new Vector2(
                tile.X * GameConfig.TileSize + GameConfig.TileSize / 2f,
                tile.Y * GameConfig.TileSize + GameConfig.TileSize / 2f);
        }
    }
}
