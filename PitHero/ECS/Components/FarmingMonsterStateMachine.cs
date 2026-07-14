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
        private Point _houseAnchorTile; // updated reactively if this worker's monster house is moved

        private FarmMonsterMover _mover;
        private ActorFacingComponent _facing;
        private PauseService _pauseService;
        private TilledTileService _tilledTileService;
        private CropGrowthService _cropGrowthService;
        private WetTileService _wetTileService;
        private BuildingService _buildingService;

        /// <summary>Animator for the ForkedHoe swing; assigned by the coordinator at spawn.</summary>
        public PausableSpriteAnimator HoeAnimator;

        /// <summary>Body animator, used to match the hoe's layer depth while tilling.</summary>
        public EnemyAnimationComponent BodyAnimator;

        /// <summary>Animator for the WateringCan sprite; assigned by the coordinator at spawn.</summary>
        public PausableSpriteAnimator WateringCanAnimator;

        /// <summary>Animator for the Watering overlay animation; assigned by the coordinator at spawn.</summary>
        public PausableSpriteAnimator WateringAnimator;

        /// <summary>Renderer for the harvested crop carried in the worker's hands; assigned at spawn.</summary>
        public Nez.Sprites.SpriteRenderer HarvestCarryRenderer;

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

        // Harvest state
        private Farming.CropType _harvestCropType;
        private int _harvestCarryCount;       // units currently carried (harvest yield, or a picked-up drop's count)
        private int _harvestBuildingId;
        private Point _harvestDoorTile;
        private bool _harvestPickedUp;        // crop has been removed/reverted and is now carried
        private bool _appleJumping;
        private float _appleJumpStartElapsed;
        private float _appleJumpPeakPx;
        private float _bodyBaseOffsetY;
        private Nez.Sprites.SpriteAtlas _cropsAtlas;

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
            _buildingService = Core.Services.GetService<BuildingService>();
            if (_buildingService != null)
            {
                _buildingService.BuildingMoved += OnBuildingMoved;
                _buildingService.BuildingRemoved += OnBuildingRemoved;
            }

            InitialState = FarmingMonsterState.EmergeFromHouse;
        }

        public override void OnRemovedFromEntity()
        {
            if (_buildingService != null)
            {
                _buildingService.BuildingMoved -= OnBuildingMoved;
                _buildingService.BuildingRemoved -= OnBuildingRemoved;
            }
            base.OnRemovedFromEntity();
        }

        /// <summary>
        /// Reacts to a building relocating: a Crop Storage this worker is delivering to, or this
        /// worker's own Monster House. Building UniqueIds are globally unique, so id matches are
        /// unambiguous across types.
        /// </summary>
        private void OnBuildingMoved(Services.PlacedBuilding building)
        {
            if (building == null)
                return;

            // Crop Storage we're carrying a harvest to has moved — follow it to the new doorway.
            if (_harvestPickedUp && building.UniqueId == _harvestBuildingId)
            {
                RetargetMovedStorage(building);
                return;
            }

            // This worker's Monster House has moved — update its home anchor and re-path if needed.
            if (building.UniqueId == _monster.MonsterHouseId)
            {
                _houseAnchorTile = new Point(building.TileX, building.TileY);
                OnHomeRelocated();
            }
        }

        /// <summary>Retargets the moved Crop Storage's new doorway (with a nearest-with-room fallback).</summary>
        private void RetargetMovedStorage(Services.PlacedBuilding building)
        {
            _harvestDoorTile = Util.BuildingConfig.GetDoorTile(building.Type,
                new Point(building.TileX, building.TileY));

            // Only actively re-path when already en route; earlier phases (e.g. the apple jump) will
            // read the refreshed door tile when they transition into CarryHarvestToStorage.
            if (CurrentState != FarmingMonsterState.CarryHarvestToStorage)
                return;

            if (TryWalkToStorageDoor())
                return;

            // New location unreachable — redirect to any storage that still has room.
            if (_coordinator.TryFindNearestStorageWithCapacity(_mover.CurrentTile, _harvestCropType,
                    out var fallback, out var fallbackDoor))
            {
                _harvestBuildingId = fallback.UniqueId;
                _harvestDoorTile = fallbackDoor;
                TryWalkToStorageDoor();
            }
        }

        /// <summary>
        /// Reacts to a Crop Storage being removed (sold). If this worker is carrying a harvest to it,
        /// redirect to another storage with room; if none has room, drop the crop on the ground (it
        /// will be picked up once a storage frees up) and return to normal work.
        /// </summary>
        private void OnBuildingRemoved(Services.PlacedBuilding building)
        {
            if (building == null || !_harvestPickedUp || building.UniqueId != _harvestBuildingId)
                return;

            if (_coordinator.TryFindNearestStorageWithCapacity(_mover.CurrentTile, _harvestCropType,
                    out var fallback, out var fallbackDoor))
            {
                _harvestBuildingId = fallback.UniqueId;
                _harvestDoorTile = fallbackDoor;
                if (CurrentState == FarmingMonsterState.CarryHarvestToStorage)
                    TryWalkToStorageDoor();
                return;
            }

            // Nowhere left to deliver — drop the carried crop and resume other work.
            Core.Services.GetService<DroppedCropService>()?.Drop(_harvestCropType, _harvestCarryCount, _mover.CurrentTile);
            _mover.Stop();
            ResetHarvestVisuals();
            _harvestPickedUp = false;
            CurrentState = FarmingMonsterState.Idle;
        }

        /// <summary>
        /// Handles this worker's Monster House relocating. A worker still emerging is repositioned to
        /// the new doorway; a worker walking home re-paths to the new house. Other states just adopt
        /// the refreshed anchor and use it the next time they head home.
        /// </summary>
        private void OnHomeRelocated()
        {
            if (CurrentState == FarmingMonsterState.EmergeFromHouse)
            {
                // Pop out at the new doorway instead of the old, now-empty spot.
                Entity.Transform.Position = TileCenter(DoorTile);
                _mover.Stop();
                if (_coordinator.Pathfinder.IsPassable(ExitTile))
                    _mover.SetSingleTarget(TileCenter(ExitTile));
            }
            else if (CurrentState == FarmingMonsterState.ReturnHome)
            {
                // Restart the walk-home sequence toward the new house exit.
                _returnReachedExit = false;
                if (!TrySetPathTo(ExitTile))
                    Entity.Destroy();
            }
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

                // Harvest: walk exactly onto the crop tile (no adjacent stand offset)
                if (_currentAction.Type == FarmActionType.Harvest)
                {
                    if (TrySetPathTo(_currentAction.TargetTile))
                    {
                        CurrentState = FarmingMonsterState.MoveToTask;
                    }
                    else
                    {
                        // Unreachable for now — drop it; PopulateHarvestQueue re-adds it later
                        _coordinator.CompleteHarvestAction(in _currentAction);
                        _hasAction = false;
                    }
                    return;
                }

                // Pickup a dropped crop: walk exactly onto the drop tile, then carry it to storage
                if (_currentAction.Type == FarmActionType.PickupDrop)
                {
                    if (TrySetPathTo(_currentAction.TargetTile))
                    {
                        CurrentState = FarmingMonsterState.MoveToDroppedCrop;
                    }
                    else
                    {
                        // Unreachable for now — release it; PopulatePickupQueue re-adds it later
                        _coordinator.CompletePickupAction(in _currentAction);
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
                    // DestroyCrop tiles are released back to the queue (retried later) rather than
                    // added to the building-change retry list which is only for till actions.
                    if (_currentAction.Type == FarmActionType.DestroyCrop)
                        _coordinator.ReleaseDestroyAction(in _currentAction);
                    else
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
                case FarmActionType.Harvest:
                {
                    bool hasCrop = _cropGrowthService != null && _cropGrowthService.HasCrop(tile);
                    bool grown = tileState != null && tileState.HasFlag(tile, TileStateFlag.CropGrown);
                    if (!hasCrop || !grown)
                    {
                        _coordinator.CompleteHarvestAction(in _currentAction);
                        _hasAction = false;
                        CurrentState = FarmingMonsterState.Idle;
                        return;
                    }
                    _harvestCropType = _cropGrowthService.GetCropType(tile).Value;
                    CurrentState = FarmingMonsterState.PerformHarvest;
                    break;
                }
                case FarmActionType.DestroyCrop:
                {
                    // Revalidate: crop still a swap candidate (plan may have been re-placed same-type,
                    // or the crop may have grown past the threshold while walking).
                    var cropPlanning = Core.Services.GetService<CropPlantingService>();
                    var cropTypeAtTile = _cropGrowthService?.GetCropType(tile);
                    bool planMatches = cropTypeAtTile.HasValue
                        && cropPlanning != null
                        && cropPlanning.GetPlanType(tile) == cropTypeAtTile.Value;
                    float arrivalProgress = _cropGrowthService != null
                        ? _cropGrowthService.GetGrowthProgress(tile)
                        : -1f;
                    bool stillValid = cropTypeAtTile.HasValue
                        && Util.CropConfig.IsRepeatHarvest(cropTypeAtTile.Value)
                        && !planMatches
                        && arrivalProgress >= 0f
                        && arrivalProgress < GameConfig.CropSwapDestroyProgressThreshold;
                    if (!stillValid)
                    {
                        _coordinator.CompleteDestroyAction(in _currentAction);
                        _hasAction = false;
                        CurrentState = FarmingMonsterState.Idle;
                        return;
                    }
                    CurrentState = FarmingMonsterState.PerformDestroyCrop;
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

        // ---------------------------------------------------------------- PerformDestroyCrop

        private void PerformDestroyCrop_Enter()
        {
            _facing?.SetFacing(_standRight ? Direction.Left : Direction.Right);

            float proficiencyScale = 1f - GameConfig.TillProficiencySpeedStep * (_monster.FarmingProficiency - 1);
            _tillDuration = GameConfig.TillBaseDurationSeconds * proficiencyScale;

            if (HoeAnimator != null)
            {
                float quadrant = GameConfig.TileSize / 4f;
                HoeAnimator.SetLocalOffset(new Vector2(_standRight ? -quadrant : quadrant, quadrant));
                HoeAnimator.FlipX = !_standRight;
                if (BodyAnimator != null)
                    HoeAnimator.SetLayerDepth(BodyAnimator.LayerDepth - 0.0001f);
                HoeAnimator.SetEnabled(true);
                HoeAnimator.Play("ForkedHoe", Nez.Sprites.SpriteAnimator.LoopMode.Loop);
            }
        }

        private void PerformDestroyCrop_Tick()
        {
            if (elapsedTimeInState < _tillDuration)
                return;

            var tile = _currentAction.TargetTile;

            // Re-check whether this tile still needs a destroy. If a same-type plan was placed
            // while we were hoeing, leave the crop in place and just complete the action.
            var cropPlanning = Core.Services.GetService<CropPlantingService>();
            var cropTypeAtTile = _cropGrowthService?.GetCropType(tile);
            bool planNowMatches = cropTypeAtTile.HasValue
                && cropPlanning != null
                && cropPlanning.GetPlanType(tile) == cropTypeAtTile.Value;

            if (!planNowMatches && cropTypeAtTile.HasValue)
            {
                var tileState = Core.Services.GetService<TileStateService>();
                _cropGrowthService?.RemoveCrop(tile);
                tileState?.ClearFlag(tile, TileStateFlag.CropGrown);
                tileState?.ClearFlag(tile, TileStateFlag.CropGrowing);
                tileState?.ClearFlag(tile, TileStateFlag.Wet);
                _wetTileService?.ClearWet(tile);
            }

            _coordinator.CompleteDestroyAction(in _currentAction);
            _hasAction = false;
            // Always notify: if crop was destroyed this schedules the waiting plan; if abort,
            // HasCrop guard in NotifyPlanAddedOnTilledTile makes it a no-op.
            _coordinator.NotifyPlanAddedOnTilledTile(tile);
            CurrentState = FarmingMonsterState.Idle;
        }

        private void PerformDestroyCrop_Exit()
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
            var planType = cropPlanting?.GetPlanType(tile);

            // Abort if the plan was removed while we were walking or waiting
            if (!planType.HasValue)
            {
                _coordinator.CompletePlantAction(in _currentAction);
                _hasAction = false;
                CurrentState = FarmingMonsterState.Idle;
                return;
            }

            // Abort if seeds ran out before we got here (another worker may have consumed them)
            if (cropPlanting == null || !cropPlanting.ConsumeSeed(planType.Value))
            {
                _coordinator.CompletePlantAction(in _currentAction);
                _hasAction = false;
                CurrentState = FarmingMonsterState.Idle;
                return;
            }

            // Load the crops atlas to get crop sprites; plan is kept after planting (permanent blueprint)
            var atlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
            _cropGrowthService?.PlantCrop(tile, planType.Value, Entity.Scene, atlas);

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

        // ---------------------------------------------------------------- PerformHarvest

        private void PerformHarvest_Enter()
        {
            _harvestPickedUp = false;
            _appleJumping = false;
            bool isApple = _harvestCropType == Farming.CropType.AppleTree;
            _facing?.SetFacing(isApple ? Direction.Up : Direction.Down);
            if (BodyAnimator != null)
                _bodyBaseOffsetY = BodyAnimator.LocalOffset.Y;
        }

        private void PerformHarvest_Tick()
        {
            // Safe to abandon only before the crop is picked up (field unchanged)
            if (_goHome && !_harvestPickedUp)
            {
                AbandonCurrentAction();
                ResetHarvestVisuals();
                CurrentState = FarmingMonsterState.ReturnHome;
                return;
            }

            if (_harvestCropType == Farming.CropType.AppleTree)
            {
                PerformAppleHarvest_Tick();
                return;
            }

            if (elapsedTimeInState < GameConfig.HarvestWaitSeconds)
                return;

            if (TryBeginCarry())
            {
                TryWalkToStorageDoor();
                CurrentState = FarmingMonsterState.CarryHarvestToStorage;
            }
            else
            {
                // No storage with room right now — leave the crop grown and retry later
                _coordinator.CompleteHarvestAction(in _currentAction);
                _hasAction = false;
                CurrentState = FarmingMonsterState.Idle;
            }
        }

        /// <summary>Apple trees: wait, jump to reach the apples, pick them at the apex, land, then carry.</summary>
        private void PerformAppleHarvest_Tick()
        {
            // Phase 1: wait under the tree
            if (!_appleJumping && elapsedTimeInState < GameConfig.AppleHarvestWaitSeconds)
                return;

            // Begin the jump
            if (!_appleJumping)
            {
                _appleJumping = true;
                _appleJumpStartElapsed = elapsedTimeInState;
                if (BodyAnimator != null)
                    _bodyBaseOffsetY = BodyAnimator.LocalOffset.Y;
                _appleJumpPeakPx = ComputeAppleJumpPeak(_currentAction.TargetTile);
            }

            float jumpElapsed = elapsedTimeInState - _appleJumpStartElapsed;
            float progress = jumpElapsed / GameConfig.AppleHarvestJumpDurationSeconds;
            if (progress > 1f) progress = 1f;

            float arc = 4f * progress * (1f - progress);
            float offsetY = _bodyBaseOffsetY - _appleJumpPeakPx * arc;
            if (BodyAnimator != null)
                BodyAnimator.SetLocalOffset(new Vector2(BodyAnimator.LocalOffset.X, offsetY));

            // At the apex, pick the apples (revert the tree) — they appear in hand on the way down
            if (!_harvestPickedUp && progress >= 0.5f)
            {
                if (!TryBeginCarry())
                {
                    // No storage with room — land and leave the tree grown
                    if (BodyAnimator != null)
                        BodyAnimator.SetLocalOffset(new Vector2(BodyAnimator.LocalOffset.X, _bodyBaseOffsetY));
                    _appleJumping = false;
                    _coordinator.CompleteHarvestAction(in _currentAction);
                    _hasAction = false;
                    CurrentState = FarmingMonsterState.Idle;
                    return;
                }
            }

            // Carry sprite tracks the body arc while descending
            if (_harvestPickedUp && HarvestCarryRenderer != null)
                HarvestCarryRenderer.SetLocalOffset(new Vector2(0f, offsetY - _bodyBaseOffsetY));

            if (progress >= 1f)
            {
                // Landed — reset body, centre the carry sprite, and walk to storage
                if (BodyAnimator != null)
                    BodyAnimator.SetLocalOffset(new Vector2(BodyAnimator.LocalOffset.X, _bodyBaseOffsetY));
                HarvestCarryRenderer?.SetLocalOffset(Vector2.Zero);
                _appleJumping = false;
                TryWalkToStorageDoor();
                CurrentState = FarmingMonsterState.CarryHarvestToStorage;
            }
        }

        // ---------------------------------------------------------------- MoveToDroppedCrop

        private void MoveToDroppedCrop_Tick()
        {
            if (_goHome)
            {
                _coordinator.ReleasePickupAction(in _currentAction);
                _hasAction = false;
                CurrentState = FarmingMonsterState.ReturnHome;
                return;
            }

            if (_mover.IsMoving)
                return;

            var tile = _currentAction.TargetTile;
            _coordinator.CompletePickupAction(in _currentAction);
            _hasAction = false;

            var dropped = Core.Services.GetService<DroppedCropService>();
            if (dropped == null || !dropped.TryGetAt(tile, out var drop))
            {
                // Drop vanished (recovered by another worker) — nothing to carry.
                CurrentState = FarmingMonsterState.Idle;
                return;
            }

            // Pick it up and reuse the storage-delivery flow.
            _harvestCropType = drop.Type;
            _harvestCarryCount = drop.Count;
            dropped.RemoveAt(tile);
            ShowCarrySprite();
            _harvestPickedUp = true;

            if (_coordinator.TryFindNearestStorageWithCapacity(_mover.CurrentTile, _harvestCropType,
                    out var building, out var door))
            {
                _harvestBuildingId = building.UniqueId;
                _harvestDoorTile = door;
                TryWalkToStorageDoor();
                CurrentState = FarmingMonsterState.CarryHarvestToStorage;
            }
            else
            {
                // Storage disappeared between claim and arrival — put the crop back on the ground.
                dropped.Drop(_harvestCropType, _harvestCarryCount, _mover.CurrentTile);
                ResetHarvestVisuals();
                _harvestPickedUp = false;
                CurrentState = FarmingMonsterState.Idle;
            }
        }

        // ---------------------------------------------------------------- CarryHarvestToStorage

        private void CarryHarvestToStorage_Tick()
        {
            if (_mover.IsMoving)
                return;
            DepositAndFinish();
        }

        /// <summary>
        /// Paths to the storage door tile, then continues one extra tile (32px) north so the worker
        /// reaches the building's doorway before stepping inside. Returns false if the door is unreachable.
        /// </summary>
        private bool TryWalkToStorageDoor()
        {
            if (!TrySetPathTo(_harvestDoorTile))
                return false;

            var northStep = new Vector2(0f, -GameConfig.TileSize);
            if (_mover.IsMoving)
                _mover.OffsetFinalWaypoint(northStep);
            else
                _mover.SetSingleTarget(TileCenter(_harvestDoorTile) + northStep);
            return true;
        }

        // ---------------------------------------------------------------- harvest helpers

        /// <summary>
        /// Finds storage with room, applies the field result (remove or revert), and shows the carry
        /// sprite. Returns false (without touching the field) when no Crop Storage has room.
        /// </summary>
        private bool TryBeginCarry()
        {
            var tile = _currentAction.TargetTile;
            if (!_coordinator.TryFindNearestStorageWithCapacity(tile, _harvestCropType, out var building, out var door))
                return false;

            _harvestBuildingId = building.UniqueId;
            _harvestDoorTile = door;
            _harvestCarryCount = Util.CropConfig.GetHarvestYield(_harvestCropType);
            ApplyHarvestResult(tile);
            ShowCarrySprite();
            _harvestPickedUp = true;
            return true;
        }

        private void ApplyHarvestResult(Point tile)
        {
            EnsureCropsAtlas();
            var tileState = Core.Services.GetService<TileStateService>();
            var cropPlanting = Core.Services.GetService<CropPlantingService>();

            if (Util.CropConfig.IsRepeatHarvest(_harvestCropType))
            {
                // Only revert for regrowth when the plan still designates the same crop type.
                // A different-type plan or no plan means the next cycle should be a swap/removal;
                // taking the removal branch here ("after next harvest") is the cheapest path.
                var planType = cropPlanting?.GetPlanType(tile);
                if (planType.HasValue && planType.Value == _harvestCropType)
                {
                    int revertFrame = Util.CropConfig.GetRevertFrame(_harvestCropType);
                    float mult = Util.CropConfig.GetRegrowthRateMultiplier(_harvestCropType);
                    _cropGrowthService?.RevertCropForRegrowth(tile, revertFrame, mult, _cropsAtlas);
                    tileState?.ClearFlag(tile, TileStateFlag.CropGrown);
                    tileState?.SetFlag(tile, TileStateFlag.CropGrowing);
                    // Soil dries out; the crop must be re-watered to regrow (re-enters the water queue)
                    tileState?.ClearFlag(tile, TileStateFlag.Wet);
                    _wetTileService?.ClearWet(tile);
                }
                else
                {
                    // Plan absent or changed — remove the repeat crop and schedule whatever comes next
                    _cropGrowthService?.RemoveCrop(tile);
                    tileState?.ClearFlag(tile, TileStateFlag.CropGrown);
                    tileState?.ClearFlag(tile, TileStateFlag.CropGrowing);
                    _coordinator.NotifyPlanAddedOnTilledTile(tile);
                }
            }
            else
            {
                // One-shot crops are permanently removed — clean tilled slate for replanting
                _cropGrowthService?.RemoveCrop(tile);
                tileState?.ClearFlag(tile, TileStateFlag.CropGrown);
                tileState?.ClearFlag(tile, TileStateFlag.CropGrowing);
                _coordinator.NotifyPlanAddedOnTilledTile(tile);
            }
        }

        private void DepositAndFinish()
        {
            var storage = Core.Services.GetService<CropStorageInventoryService>();
            int carried = _harvestCarryCount;
            int stored = storage != null ? storage.DepositReturningStored(_harvestBuildingId, _harvestCropType, carried) : 0;
            int remaining = carried - stored;

            if (remaining > 0 && storage != null)
            {
                // Destination filled (or was sold) while we walked — redirect to any storage with room
                if (_coordinator.TryFindNearestStorageWithCapacity(_mover.CurrentTile, _harvestCropType,
                        out var building, out var door))
                {
                    _harvestBuildingId = building.UniqueId;
                    _harvestCarryCount = remaining;
                    _harvestDoorTile = door;
                    if (TryWalkToStorageDoor())
                        return; // keep carrying the remainder to the new destination
                    stored = storage.DepositReturningStored(_harvestBuildingId, _harvestCropType, remaining); // adjacent fallback
                    remaining -= stored;
                }

                // Still holding crops with nowhere to go — drop them on the ground for later pickup.
                // Drop at the approach tile, not CurrentTile: the worker stands one tile inside the
                // building footprint here, and drops on footprint tiles can never be picked up.
                if (remaining > 0)
                    Core.Services.GetService<DroppedCropService>()?.Drop(_harvestCropType, remaining, _harvestDoorTile);
            }

            // Delivered: the crop and the worker both vanish "into" the storage building.
            HideCarrySprite();
            BodyAnimator?.SetEnabled(false);
            _mover?.Stop();
            _coordinator.CompleteHarvestAction(in _currentAction);
            _hasAction = false;
            _harvestPickedUp = false;
            CurrentState = FarmingMonsterState.DepositHarvest;
        }

        // ---------------------------------------------------------------- DepositHarvest

        private void DepositHarvest_Tick()
        {
            // Worker is "inside" the storage building; reappear after the wait.
            if (elapsedTimeInState < GameConfig.HarvestDepositSeconds)
                return;

            // The storage may have been moved while the worker was hidden inside — reappear at the
            // building's current doorway rather than the now-empty original spot.
            var storage = _buildingService?.GetBuildingById(_harvestBuildingId);
            if (storage != null)
            {
                var door = Util.BuildingConfig.GetDoorTile(storage.Type,
                    new Point(storage.TileX, storage.TileY));
                Entity.Transform.Position = TileCenter(door) + new Vector2(0f, -GameConfig.TileSize);
            }

            BodyAnimator?.SetEnabled(true);
            CurrentState = FarmingMonsterState.Idle;
        }

        private float ComputeAppleJumpPeak(Point tile)
        {
            EnsureCropsAtlas();
            var treeSprite = _cropsAtlas?.GetSprite(Util.CropConfig.GetFullyGrownSpriteName(Farming.CropType.AppleTree));
            float treeH = treeSprite != null ? treeSprite.SourceRect.Height : GameConfig.TileSize;
            float treeTopY = tile.Y * GameConfig.TileSize + GameConfig.TileSize - treeH;
            float targetY = treeTopY + GameConfig.AppleTreeTopHarvestOffsetPx;
            float groundCenterY = Entity.Transform.Position.Y + _bodyBaseOffsetY;
            float peak = groundCenterY - targetY;
            return peak < 0f ? 0f : peak;
        }

        private void ShowCarrySprite()
        {
            if (HarvestCarryRenderer == null)
                return;
            EnsureCropsAtlas();
            var sprite = _cropsAtlas?.GetSprite(Util.CropConfig.GetHarvestSpriteName(_harvestCropType));
            if (sprite == null)
                return;
            HarvestCarryRenderer.Sprite = sprite;
            HarvestCarryRenderer.SetLocalOffset(Vector2.Zero); // centre of the worker
            // Renders on RenderLayerActorPropOverlay (set at spawn), so it always sits above the worker.
            HarvestCarryRenderer.SetEnabled(true);
        }

        private void HideCarrySprite() => HarvestCarryRenderer?.SetEnabled(false);

        private void ResetHarvestVisuals()
        {
            HideCarrySprite();
            if (BodyAnimator != null)
                BodyAnimator.SetLocalOffset(new Vector2(BodyAnimator.LocalOffset.X, _bodyBaseOffsetY));
            _appleJumping = false;
        }

        private void EnsureCropsAtlas()
        {
            if (_cropsAtlas == null)
                _cropsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
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
                case FarmActionType.Harvest:
                    _coordinator.ReleaseHarvestAction(in _currentAction);
                    break;
                case FarmActionType.DestroyCrop:
                    _coordinator.ReleaseDestroyAction(in _currentAction);
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
