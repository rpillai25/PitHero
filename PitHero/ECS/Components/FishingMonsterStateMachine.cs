using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.FSM;
using PitHero.Config;
using PitHero.Services;
using PitHero.Util;
using PitHero.Util.SoundEffectTypes;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.ECS.Components
{
    /// <summary>States of a fishing monster (see <see cref="FishingMonsterStateMachine"/>).</summary>
    public enum FishingMonsterState
    {
        EmergeFromHouse,
        ClaimSpot,
        WaitForSpot,
        WalkToSpot,
        Fishing,
        Reel,
        ReturnHome,
    }

    /// <summary>
    /// Drives one fishing monster: emerge from its monster house, claim one of the pond's four spots,
    /// walk there and sit fishing (pole and line drawn by <see cref="FishingLineRenderer"/>). Each cast
    /// waits a random <see cref="GameConfig.FishingCastSecondsMin"/>..<see cref="GameConfig.FishingCastSecondsMax"/>
    /// sim seconds, then lands a catch from the shared marble bag (<see cref="FishingService"/>), which
    /// sells on the spot; the reel that follows lasts <see cref="GameConfig.FishingReelSeconds"/> and is
    /// timed by this state machine, never by the cosmetic catch effect. RequestReturnHome sends it back
    /// into its house (job change, shift end, pond gone).
    ///
    /// Simulation: runs in the fixed step, rolls <c>Nez.Random</c> only at a cast and a catch, and
    /// keeps its timers on elapsedTimeInState. Fishers are not saved: after a load they re-emerge.
    /// </summary>
    public class FishingMonsterStateMachine : SimpleStateMachine<FishingMonsterState>, IPausableComponent
    {
        private readonly AlliedMonster _monster;
        private readonly FishingTaskCoordinator _coordinator;
        private Point _houseAnchorTile;

        private FarmMonsterMover _mover;
        private ActorFacingComponent _facing;
        private PauseService _pauseService;
        private BuildingService _buildingService;

        /// <summary>The fisher's body animator: paused while seated.</summary>
        public EnemyAnimationComponent BodyAnimator;

        /// <summary>The pole and line renderer on the same entity.</summary>
        public FishingLineRenderer Line;

        private int _spot = -1;
        private Point _standTile;     // the claimed spot's walkable stand tile
        private bool _hasStandTile;
        private bool _goHome;
        private bool _returnReachedExit;
        private float _castSeconds;
        private FishCatchType _catch;

        public bool ShouldPause => true;

        /// <summary>True once the monster has been asked to walk home and despawn.</summary>
        public bool IsReturningHome => _goHome;

        /// <summary>The pond spot this fisher holds, or -1.</summary>
        public int Spot => _spot;

        private Point DoorTile => new Point(_houseAnchorTile.X, _houseAnchorTile.Y + 2);
        private Point ExitTile => new Point(_houseAnchorTile.X, _houseAnchorTile.Y + 3);

        public FishingMonsterStateMachine(AlliedMonster monster, FishingTaskCoordinator coordinator, Point houseAnchorTile)
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
            _buildingService = Core.Services.GetService<BuildingService>();
            if (_buildingService != null)
                _buildingService.BuildingMoved += OnBuildingMoved;
            InitialState = FishingMonsterState.EmergeFromHouse;
        }

        public override void OnRemovedFromEntity()
        {
            if (_buildingService != null)
                _buildingService.BuildingMoved -= OnBuildingMoved;
            _coordinator.ReleaseSpot(this);
            _spot = -1;
            base.OnRemovedFromEntity();
        }

        public override void Update()
        {
            if (_pauseService?.IsPaused == true)
                return;
            base.Update();
        }

        /// <summary>Asks the fisher to finish its reel, walk back into its house and despawn.</summary>
        public void RequestReturnHome() => _goHome = true;

        /// <summary>Cancels a pending return (the job came back before the fisher got home).</summary>
        public void CancelReturnHome()
        {
            if (!_goHome)
                return;
            _goHome = false;
            if (CurrentState == FishingMonsterState.ReturnHome)
            {
                _mover.Stop();
                CurrentState = FishingMonsterState.ClaimSpot;
            }
        }

        /// <summary>
        /// The pond moved: give up the old spot and claim again around the new pond. The fisher's own
        /// monster house moved: adopt the new door (and re-path if already heading home).
        /// </summary>
        private void OnBuildingMoved(PlacedBuilding building)
        {
            if (building == null)
                return;

            if (building.Type == BuildingType.FishingPond)
            {
                if (CurrentState == FishingMonsterState.WalkToSpot || CurrentState == FishingMonsterState.Fishing
                    || CurrentState == FishingMonsterState.Reel || CurrentState == FishingMonsterState.WaitForSpot)
                {
                    LeaveSpot();
                    CurrentState = FishingMonsterState.ClaimSpot;
                }
                return;
            }

            if (building.UniqueId == _monster.MonsterHouseId)
            {
                _houseAnchorTile = new Point(building.TileX, building.TileY);
                if (CurrentState == FishingMonsterState.EmergeFromHouse)
                {
                    Entity.Transform.Position = TileCenter(DoorTile);
                    _mover.Stop();
                    if (_coordinator.Pathfinder.IsPassable(ExitTile))
                        _mover.SetSingleTarget(TileCenter(ExitTile));
                }
                else if (CurrentState == FishingMonsterState.ReturnHome)
                {
                    _returnReachedExit = false;
                    if (!TrySetPathTo(ExitTile))
                        Entity.Destroy();
                }
            }
        }

        // ---------------------------------------------------------------- EmergeFromHouse

        private void EmergeFromHouse_Enter()
        {
            _facing?.SetFacing(Direction.Down);
            if (_coordinator.Pathfinder.IsPassable(ExitTile))
                _mover.SetSingleTarget(TileCenter(ExitTile));
        }

        private void EmergeFromHouse_Tick()
        {
            if (_mover.IsMoving)
                return;
            SpeechBubbleDialogue.SayWorkerShiftStart(Entity);
            CurrentState = FishingMonsterState.ClaimSpot;
        }

        // ---------------------------------------------------------------- ClaimSpot / WaitForSpot

        private void ClaimSpot_Tick()
        {
            if (_goHome)
            {
                CurrentState = FishingMonsterState.ReturnHome;
                return;
            }
            if (_coordinator.TryClaimSpot(this, out _spot) && TryWalkToSpot())
            {
                CurrentState = FishingMonsterState.WalkToSpot;
                return;
            }
            LeaveSpot();
            CurrentState = FishingMonsterState.WaitForSpot;
        }

        private void WaitForSpot_Tick()
        {
            if (_goHome)
            {
                CurrentState = FishingMonsterState.ReturnHome;
                return;
            }
            if (elapsedTimeInState >= GameConfig.FishingSpotRetrySeconds)
                CurrentState = FishingMonsterState.ClaimSpot;
        }

        // ---------------------------------------------------------------- WalkToSpot

        private void WalkToSpot_Tick()
        {
            if (_goHome)
            {
                CurrentState = FishingMonsterState.ReturnHome;
                return;
            }
            if (!_mover.IsMoving)
                CurrentState = FishingMonsterState.Fishing;
        }

        // ---------------------------------------------------------------- Fishing

        private void Fishing_Enter()
        {
            var pond = _coordinator.GetPond();
            if (pond == null || _spot < 0)
                return; // Fishing_Tick sends it back to ClaimSpot

            var sit = FishingPondSpots.GetSitPosition(_spot, pond.TileX, pond.TileY);
            Entity.Transform.Position = sit;
            bool right = FishingPondSpots.FacesRight(_spot);
            _facing?.SetFacing(right ? Direction.Right : Direction.Left);
            // Seated: hold still on the first frame of the animation (a bare Pause would freeze
            // whatever mid-stride frame happened to be showing)
            if (BodyAnimator != null)
            {
                if (BodyAnimator.CurrentAnimation != null && BodyAnimator.CurrentAnimation.Sprites.Length > 0)
                    BodyAnimator.SetFrame(0);
                BodyAnimator.Pause();
            }

            if (Line != null)
            {
                Line.FacingRight = right;
                Line.Bobber = FishingPondSpots.GetBobberPosition(_spot, pond.TileX, pond.TileY);
                Line.ReelProgress = -1f;
                Line.Active = true;
            }

            _castSeconds = Nez.Random.Range(GameConfig.FishingCastSecondsMin, GameConfig.FishingCastSecondsMax);
        }

        private void Fishing_Tick()
        {
            if (_coordinator.GetPond() == null || _spot < 0)
            {
                LeaveSpot();
                CurrentState = FishingMonsterState.ClaimSpot;
                return;
            }
            if (_goHome)
            {
                CurrentState = FishingMonsterState.ReturnHome;
                return;
            }
            if (elapsedTimeInState >= _castSeconds)
                CurrentState = FishingMonsterState.Reel;
        }

        // ---------------------------------------------------------------- Reel

        private void Reel_Enter()
        {
            var fishing = Core.Services.GetService<FishingService>();
            var gameState = Core.Services.GetService<GameStateService>();
            _catch = fishing != null
                ? fishing.ResolveCatch(gameState, _monster, _spot, Nez.Random.NextFloat())
                : FishCatchType.JunkCan;

            var bobber = Line != null ? Line.Bobber : Entity.Transform.Position;
            bool right = Line == null || Line.FacingRight;
            if (Line != null)
                Line.ReelProgress = 0f;

            Core.GetGlobalManager<SoundEffectManager>()?.PlaySoundAt(SoundEffectType.Watering, bobber);
            _coordinator.SpawnCatchEffect(_catch, bobber,
                FishingLineRenderer.CatchTarget(Entity.Transform.Position, right), right);
        }

        private void Reel_Tick()
        {
            float p = elapsedTimeInState / GameConfig.FishingReelSeconds;
            if (Line != null)
                Line.ReelProgress = p > 1f ? 1f : p;
            if (p < 1f)
                return;

            if (FishingConfig.GetGold(_catch) > 0)
                Core.GetGlobalManager<SoundEffectManager>()?.PlaySoundAt(SoundEffectType.PayGold, Entity.Transform.Position);

            if (_goHome)
            {
                CurrentState = FishingMonsterState.ReturnHome;
                return;
            }
            // Re-enter Fishing for the next cast (a same-state assignment would be ignored)
            CurrentState = FishingMonsterState.Fishing;
        }

        private void Reel_Exit()
        {
            if (Line != null)
                Line.ReelProgress = -1f;
        }

        // ---------------------------------------------------------------- ReturnHome

        private void ReturnHome_Enter()
        {
            LeaveSpot();
            _returnReachedExit = false;
            if (!TrySetPathTo(ExitTile))
            {
                Entity.Destroy();
                return;
            }
            var timeService = Core.Instance != null ? Core.Services.GetService<InGameTimeService>() : null;
            if (timeService != null && MonsterScheduleConfig.IsAsleep(_monster.MonsterTypeName, timeService))
                SpeechBubbleDialogue.SayWorkerShiftEnd(Entity);
        }

        private void ReturnHome_Tick()
        {
            if (_mover.IsMoving)
                return;
            if (!_returnReachedExit)
            {
                _returnReachedExit = true;
                _facing?.SetFacing(Direction.Up);
                _mover.SetSingleTarget(TileCenter(DoorTile));
                return;
            }
            Entity.Destroy();
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Stands up: puts the pole away, frees the spot and resumes the walk animation. A fisher whose
        /// sitting point lies inside the walled pond footprint (the top spot sits on the rocks) steps
        /// back onto its stand tile first, so the next path starts from a walkable tile.
        /// </summary>
        private void LeaveSpot()
        {
            if (_hasStandTile && !_coordinator.Pathfinder.IsPassable(_mover.CurrentTile))
            {
                _mover.Stop();
                Entity.Transform.Position = TileCenter(_standTile);
            }
            _hasStandTile = false;
            if (Line != null)
            {
                Line.Active = false;
                Line.ReelProgress = -1f;
            }
            BodyAnimator?.UnPause();
            _coordinator.ReleaseSpot(this);
            _spot = -1;
        }

        /// <summary>Paths to the claimed spot's stand tile, ending at the exact sitting point.</summary>
        private bool TryWalkToSpot()
        {
            var pond = _coordinator.GetPond();
            if (pond == null || _spot < 0)
                return false;
            var stand = FishingPondSpots.GetStandTile(_spot, pond.TileX, pond.TileY);
            var sit = FishingPondSpots.GetSitPosition(_spot, pond.TileX, pond.TileY);
            if (!_coordinator.Pathfinder.IsPassable(stand))
                return false;
            _standTile = stand;
            _hasStandTile = true;

            var start = _mover.CurrentTile;
            if (start == stand)
            {
                _mover.SetSingleTarget(sit);
                return true;
            }
            var path = _coordinator.Pathfinder.Search(start, stand);
            if (path == null)
                return false;
            _mover.SetPath(_coordinator.Pathfinder.SmoothPath(start, path));
            _mover.OffsetFinalWaypoint(FishingPondSpots.GetSitOffset(_spot));
            return true;
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

        private static Vector2 TileCenter(Point tile) => new Vector2(
            tile.X * GameConfig.TileSize + GameConfig.TileSize / 2f,
            tile.Y * GameConfig.TileSize + GameConfig.TileSize / 2f);
    }
}
