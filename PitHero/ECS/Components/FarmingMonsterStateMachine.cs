using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.FSM;
using PitHero.Farming;
using PitHero.Services;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Drives a single farming monster: emerge from the monster house door, claim till/plant/water
    /// actions from the FarmTaskCoordinator, walk to the stand tile beside the target, perform the
    /// action, and wander the farm when no work remains. RequestReturnHome sends it back into the house.
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
        private CropGrowthService _cropGrowthService;
        private WetTileService _wetTileService;

        /// <summary>Animator for the ForkedHoe swing; assigned by the coordinator at spawn.</summary>
        public PausableSpriteAnimator HoeAnimator;

        /// <summary>Body animator, used to match the hoe's layer depth while tilling.</summary>
        public EnemyAnimationComponent BodyAnimator;

        /// <summary>Animator for the WateringCan sprite; assigned by the coordinator at spawn.</summary>
        public PausableSpriteAnimator WateringCanAnimator;

        /// <summary>Animator for the Watering overlay animation; assigned by the coordinator at spawn.</summary>
        public PausableSpriteAnimator WateringAnimator;

        /// <summary>
        /// Normalized queue position this worker claims from (0 = front, 1 = back); assigned by
        /// the coordinator at spawn so concurrent workers spread across the field.
        /// </summary>
        public float QueuePick;

        private FarmAction _currentAction;
        private bool _hasAction;
        private bool _standRight;        // standing right of the target (preferred) vs left (fallback)
        private bool _goHome;
        private bool _returnReachedExit; // ReturnHome phase: walked to the exit tile, stepping into the door
        private float _tillDuration;
        private float _waterDuration;
        private float _plantDuration;

        // Watering can charges: 0 = empty (must fill before watering), max = WateringCanMaxCharges
        private int _wateringCanCharges = 0;
        private bool _fillAnimating;
        private float _fillDisplayTimer;

        private static readonly Point[] PondFillTiles =
        {
            new Point(118, 2), new Point(118, 3), new Point(118, 4),
            new Point(118, 5), new Point(118, 6), new Point(118, 7),
        };

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
            _cropGrowthService = Core.Services.GetService<CropGrowthService>();
            _wetTileService = Core.Services.GetService<WetTileService>();

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

            if (_coordinator.TryClaimAction(QueuePick, out _currentAction))
            {
                _hasAction = true;

                // Can is empty — must fill at the pond before watering any crop
                if (_currentAction.Type == FarmActionType.Water && _wateringCanCharges == 0)
                {
                    if (TryPathToNearestPondTile())
                    {
                        CurrentState = FarmingMonsterState.FillWateringCan;
                    }
                    else
                    {
                        _coordinator.ReleaseWaterAction(in _currentAction);
                        _hasAction = false;
                    }
                    return;
                }

                if (TryPathToStandTile())
                {
                    if (_currentAction.Type == FarmActionType.Water)
                        _mover.OffsetFinalWaypoint(new Vector2(-16f, -16f));
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

            var tile = _currentAction.TargetTile;
            var tileState = Core.Services.GetService<TileStateService>();

            switch (_currentAction.Type)
            {
                case FarmActionType.Till:
                {
                    bool stillValid = tileState != null && tileState.HasFlag(tile, TileStateFlag.ReadyToTill);
                    if (!stillValid)
                    {
                        _coordinator.CompleteAction(in _currentAction);
                        _hasAction = false;
                        CurrentState = FarmingMonsterState.Idle;
                        return;
                    }
                    CurrentState = FarmingMonsterState.PerformTill;
                    break;
                }
                case FarmActionType.Plant:
                {
                    var cropPlanting = Core.Services.GetService<CropPlantingService>();
                    bool hasPlan = cropPlanting != null && cropPlanting.HasPlan(tile);
                    bool alreadyPlanted = _cropGrowthService != null && _cropGrowthService.HasCrop(tile);
                    if (!hasPlan || alreadyPlanted)
                    {
                        _coordinator.CompletePlantAction(in _currentAction);
                        _hasAction = false;
                        CurrentState = FarmingMonsterState.Idle;
                        return;
                    }
                    CurrentState = FarmingMonsterState.PerformPlant;
                    break;
                }
                case FarmActionType.Water:
                {
                    bool hasCrop = _cropGrowthService != null && _cropGrowthService.HasCrop(tile);
                    bool alreadyWet = tileState != null && tileState.HasFlag(tile, TileStateFlag.Wet);
                    // Crop may have finished growing while the worker walked over; grown crops
                    // don't need water, so drop the action.
                    bool fullyGrown = tileState != null && tileState.HasFlag(tile, TileStateFlag.CropGrown);
                    if (!hasCrop || alreadyWet || fullyGrown)
                    {
                        _coordinator.CompleteWaterAction(in _currentAction);
                        _hasAction = false;
                        CurrentState = FarmingMonsterState.Idle;
                        return;
                    }
                    CurrentState = FarmingMonsterState.PerformWater;
                    break;
                }
                default:
                    _coordinator.CompleteAction(in _currentAction);
                    _hasAction = false;
                    CurrentState = FarmingMonsterState.Idle;
                    break;
            }
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

        // ---------------------------------------------------------------- PerformPlant

        private void PerformPlant_Enter()
        {
            _facing?.SetFacing(_standRight ? Direction.Left : Direction.Right);
            _plantDuration = GameConfig.PlantBaseDurationSeconds;
        }

        private void PerformPlant_Tick()
        {
            if (elapsedTimeInState < _plantDuration)
                return;

            var tile = _currentAction.TargetTile;
            var cropPlanting = Core.Services.GetService<CropPlantingService>();
            var cropType = cropPlanting?.GetPlanType(tile) ?? Farming.CropType.Wheat;

            // Load the crops atlas to get crop sprites
            var atlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
            _cropGrowthService?.PlantCrop(tile, cropType, Entity.Scene, atlas);

            _coordinator.CompletePlantAction(in _currentAction);
            _hasAction = false;
            CurrentState = FarmingMonsterState.Idle;
        }

        // ---------------------------------------------------------------- PerformWater

        private void PerformWater_Enter()
        {
            _facing?.SetFacing(_standRight ? Direction.Left : Direction.Right);

            float proficiencyScale = 1f - GameConfig.TillProficiencySpeedStep * (_monster.FarmingProficiency - 1);
            _waterDuration = GameConfig.WaterBaseDurationSeconds * proficiencyScale;

            if (WateringCanAnimator != null)
            {
                float quadrant = GameConfig.TileSize / 4f;
                WateringCanAnimator.SetLocalOffset(new Vector2(_standRight ? -quadrant : quadrant, quadrant));
                WateringCanAnimator.FlipX = !_standRight;
                if (BodyAnimator != null)
                    WateringCanAnimator.SetLayerDepth(BodyAnimator.LayerDepth - 0.0001f);
                WateringCanAnimator.SetEnabled(true);
                WateringCanAnimator.Play("WateringCan", Nez.Sprites.SpriteAnimator.LoopMode.Loop);
            }

            if (WateringAnimator != null)
            {
                float quadrant = GameConfig.TileSize / 4f;
                WateringAnimator.SetLocalOffset(new Vector2(_standRight ? -quadrant : quadrant, quadrant));
                WateringAnimator.FlipX = !_standRight;
                if (BodyAnimator != null)
                    WateringAnimator.SetLayerDepth(BodyAnimator.LayerDepth - 0.0002f);
                WateringAnimator.SetEnabled(true);
                WateringAnimator.Play("Watering", Nez.Sprites.SpriteAnimator.LoopMode.Loop);
            }
        }

        private void PerformWater_Tick()
        {
            if (elapsedTimeInState < _waterDuration)
                return;

            _wetTileService?.SetWet(_currentAction.TargetTile);
            _wateringCanCharges--;
            _coordinator.CompleteWaterAction(in _currentAction);
            _hasAction = false;
            CurrentState = FarmingMonsterState.Idle;
        }

        private void PerformWater_Exit()
        {
            WateringCanAnimator?.SetEnabled(false);
            WateringAnimator?.SetEnabled(false);
        }

        // ---------------------------------------------------------------- FillWateringCan

        private void FillWateringCan_Enter()
        {
            _fillAnimating = false;
            _fillDisplayTimer = 0f;
        }

        private void FillWateringCan_Tick()
        {
            if (_goHome)
            {
                AbandonCurrentAction();
                CurrentState = FarmingMonsterState.ReturnHome;
                return;
            }

            if (_mover.IsMoving)
                return;

            if (!_fillAnimating)
            {
                _fillAnimating = true;
                _fillDisplayTimer = 0f;
                _facing?.SetFacing(Direction.Left);

                if (WateringCanAnimator != null)
                {
                    float quadrant = GameConfig.TileSize / 4f;
                    WateringCanAnimator.SetLocalOffset(new Vector2(-quadrant, quadrant));
                    WateringCanAnimator.FlipX = false;
                    if (BodyAnimator != null)
                        WateringCanAnimator.SetLayerDepth(BodyAnimator.LayerDepth - 0.0001f);
                    WateringCanAnimator.SetEnabled(true);
                    WateringCanAnimator.Play("WateringCan", Nez.Sprites.SpriteAnimator.LoopMode.Loop);
                }
                return;
            }

            _fillDisplayTimer += Time.DeltaTime;
            if (_fillDisplayTimer < GameConfig.WateringCanFillDurationSeconds)
                return;

            _wateringCanCharges = GameConfig.WateringCanMaxCharges;

            if (TryPathToStandTile())
            {
                _mover.OffsetFinalWaypoint(new Vector2(-16f, -16f));
                CurrentState = FarmingMonsterState.MoveToTask;
            }
            else
            {
                _coordinator.ReleaseWaterAction(in _currentAction);
                _hasAction = false;
                CurrentState = FarmingMonsterState.Idle;
            }
        }

        private void FillWateringCan_Exit()
        {
            _fillAnimating = false;
            WateringCanAnimator?.SetEnabled(false);
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
            switch (_currentAction.Type)
            {
                case FarmActionType.Plant:
                    _coordinator.ReleasePlantAction(in _currentAction);
                    break;
                case FarmActionType.Water:
                    _coordinator.ReleaseWaterAction(in _currentAction);
                    break;
                default:
                    _coordinator.ReleaseAction(in _currentAction);
                    break;
            }
            _hasAction = false;
        }

        /// <summary>
        /// Paths to the stand tile beside the target: one tile to the right (preferred) or one
        /// tile to the left (mirrored tool). Returns false when neither is reachable.
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

            // Idle monsters hang around the field rather than roaming the whole farm
            if (_coordinator.TryGetNearestFieldTile(_mover.CurrentTile, out var fieldTile))
            {
                int r = GameConfig.FarmWanderRadiusTiles;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    int x = fieldTile.X + Nez.Random.Range(-r, r + 1);
                    int y = fieldTile.Y + Nez.Random.Range(-r, r + 1);
                    if (x < GameConfig.FarmMinWanderTileX) x = GameConfig.FarmMinWanderTileX;
                    else if (x >= pathfinder.Width) x = pathfinder.Width - 1;
                    if (y < 1) y = 1;
                    else if (y > pathfinder.Height - 2) y = pathfinder.Height - 2;
                    var goal = new Point(x, y);
                    if (goal == _mover.CurrentTile)
                        continue;
                    if (TrySetPathTo(goal))
                        return true;
                }
            }

            // No field yet (or all nearby spots unreachable) — wander anywhere on the farm
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

        private bool TryPathToNearestPondTile()
        {
            var pos = _mover.CurrentTile;
            Point best = default;
            long bestDist = long.MaxValue;
            foreach (var t in PondFillTiles)
            {
                if (!_coordinator.Pathfinder.IsPassable(t))
                    continue;
                long dx = t.X - pos.X;
                long dy = t.Y - pos.Y;
                long distSq = dx * dx + dy * dy;
                if (distSq < bestDist)
                {
                    bestDist = distSq;
                    best = t;
                }
            }
            if (bestDist == long.MaxValue)
                return false;
            return TrySetPathTo(best);
        }

        private static Vector2 TileCenter(Point tile)
        {
            return new Vector2(
                tile.X * GameConfig.TileSize + GameConfig.TileSize / 2f,
                tile.Y * GameConfig.TileSize + GameConfig.TileSize / 2f);
        }
    }
}
