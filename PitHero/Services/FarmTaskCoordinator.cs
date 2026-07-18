using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.Config;
using PitHero.ECS.Components;
using PitHero.Farming;
using PitHero.Util;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services
{
    /// <summary>
    /// Central coordinator for farm work. Fills shared action queues from tile flags (event-driven
    /// — no per-frame scans) and hands actions out to farming monsters so multiple workers never
    /// target the same tile. Also owns the shared farm pathfinder.
    /// </summary>
    public class FarmTaskCoordinator
    {
        private struct ActiveWorker
        {
            public AlliedMonster Monster;
            public Entity Entity;
            public FarmingMonsterStateMachine Fsm;
        }

        private readonly TileStateService _tileState;
        private readonly BuildingService _buildingService;
        private readonly AlliedMonsterManager _alliedMonsters;
        private readonly Deque<FarmAction> _queue = new Deque<FarmAction>(64);

        // Tiles that are queued OR claimed by a worker; prevents duplicate enqueues.
        private readonly HashSet<Point> _tracked = new HashSet<Point>();

        // Tiles whose stand positions were unreachable; retried when buildings change.
        private readonly List<Point> _blocked = new List<Point>(16);

        // Plant and water queues
        private readonly Deque<FarmAction> _plantQueue = new Deque<FarmAction>(64);
        private readonly HashSet<Point> _plantTracked = new HashSet<Point>();
        private readonly Deque<FarmAction> _waterQueue = new Deque<FarmAction>(64);
        private readonly HashSet<Point> _waterTracked = new HashSet<Point>();

        // Harvest queue — fully-grown crops awaiting collection
        private readonly Deque<FarmAction> _harvestQueue = new Deque<FarmAction>(64);
        private readonly HashSet<Point> _harvestTracked = new HashSet<Point>();

        // Destroy queue — repeat-harvest crops that are <20% grown with a missing/different plan
        private readonly Deque<FarmAction> _destroyQueue = new Deque<FarmAction>(64);
        private readonly HashSet<Point> _destroyTracked = new HashSet<Point>();

        // Pickup queue — crops dropped on the ground awaiting recovery to storage
        private readonly Deque<FarmAction> _pickupQueue = new Deque<FarmAction>(16);
        private readonly HashSet<Point> _pickupTracked = new HashSet<Point>();

        private TilledTileService _tilledTileService;
        private DroppedCropService _droppedCropService;

        private readonly List<ActiveWorker> _workers = new List<ActiveWorker>(16);
        private Scene _scene;

        /// <summary>Provides the dropped-crop service used to recover crops dropped on the ground.</summary>
        public void SetDroppedCropService(DroppedCropService service) => _droppedCropService = service;

        // Core.Services requires a running game instance; headless tests construct the coordinator
        // without one, so validation/populate paths resolve services through this null-safe lookup.
        private static T GetService<T>() where T : class
            => Core.Instance != null ? Core.Services.GetService<T>() : null;

        /// <summary>Shared A* grid for all farming monsters.</summary>
        public FarmPathfinder Pathfinder { get; }

        /// <summary>Number of actions waiting in the till queue (excludes claimed actions).</summary>
        public int PendingActionCount => _queue.Count;

        public FarmTaskCoordinator(TileStateService tileState, BuildingService buildingService,
            int mapWidthTiles, int mapHeightTiles, AlliedMonsterManager alliedMonsters = null,
            TilledTileService tilledTileService = null)
        {
            _tileState = tileState;
            _buildingService = buildingService;
            _alliedMonsters = alliedMonsters;

            Pathfinder = new FarmPathfinder(mapWidthTiles, mapHeightTiles);
            Pathfinder.RebuildWalls(buildingService);

            _tileState.OnReadyToTillSet += HandleReadyToTillSet;
            _tileState.OnReadyToTillCleared += HandleReadyToTillCleared;
            _buildingService.BuildingsChanged += HandleBuildingsChanged;

            _tilledTileService = tilledTileService;
            if (_tilledTileService != null)
                _tilledTileService.OnTileTilled += HandleTileTilled;

            RescanReadyToTill();
            RecalculateRightmostFarmObject();
        }

        /// <summary>Unsubscribes from service events. Call when the scene is torn down.</summary>
        public void Detach()
        {
            _tileState.OnReadyToTillSet -= HandleReadyToTillSet;
            _tileState.OnReadyToTillCleared -= HandleReadyToTillCleared;
            _buildingService.BuildingsChanged -= HandleBuildingsChanged;
            if (_tilledTileService != null)
                _tilledTileService.OnTileTilled -= HandleTileTilled;
        }

        /// <summary>Provides the scene used to spawn farming monster entities.</summary>
        public void Initialize(Scene scene) => _scene = scene;

        /// <summary>
        /// Per-frame tick: keeps world entities in sync with job assignments. Spawns a worker for
        /// every Farming-job allied monster, asks workers whose job changed to walk home, and reaps
        /// despawned entities. One code path covers UI assignment, load restore, and reassignment.
        /// </summary>
        public void Update()
        {
            if (_alliedMonsters == null || _scene == null)
                return;

            var timeService = Core.Services.GetService<InGameTimeService>();

            var roster = _alliedMonsters.AlliedMonsters;
            for (int i = 0; i < roster.Count; i++)
            {
                var monster = roster[i];
                int workerIndex = FindWorkerIndex(monster);

                // Asleep monsters (outside their day/night work window) retreat into the house.
                bool awake = !MonsterScheduleConfig.IsAsleep(monster.MonsterTypeName, timeService);

                if (monster.Job == MonsterJob.Farming && awake)
                {
                    if (workerIndex < 0)
                        SpawnWorker(monster);
                    else
                        _workers[workerIndex].Fsm.CancelReturnHome();
                }
                else if (workerIndex >= 0)
                {
                    _workers[workerIndex].Fsm.RequestReturnHome();
                }
            }

            // Reap workers whose entities finished despawning
            for (int i = _workers.Count - 1; i >= 0; i--)
            {
                if (_workers[i].Entity.IsDestroyed)
                    _workers.RemoveAt(i);
            }
        }

        private int FindWorkerIndex(AlliedMonster monster)
        {
            for (int i = 0; i < _workers.Count; i++)
                if (ReferenceEquals(_workers[i].Monster, monster))
                    return i;
            return -1;
        }

        private void SpawnWorker(AlliedMonster monster)
        {
            var house = FindMonsterHouse(monster.MonsterHouseId);
            if (house == null)
            {
                // Monsters recruited before house-linking (and v7-save migrations) carry
                // MonsterHouseId -1, and stale ids can outlive their house. Re-home to a house
                // with capacity; the new id persists on the next save.
                house = FindMonsterHouseWithCapacity();
                if (house == null)
                    return;   // no monster house available — retried next frame
                monster.MonsterHouseId = house.UniqueId;
                Debug.Log($"[FarmTaskCoordinator] Re-homed '{monster.Name}' to monster house {house.UniqueId}");
            }

            // Door is at the bottom-center of the 5x5 monster house footprint
            var doorTile = new Point(house.TileX, house.TileY + 2);
            var position = new Vector2(
                doorTile.X * GameConfig.TileSize + GameConfig.TileSize / 2f,
                doorTile.Y * GameConfig.TileSize + GameConfig.TileSize / 2f);

            // Animations in Actors.atlas are named without the "Monster_" prefix (see MonsterUI)
            string typeName = monster.MonsterTypeName.StartsWith("Monster_")
                ? monster.MonsterTypeName.Substring("Monster_".Length)
                : monster.MonsterTypeName;

            var entity = _scene.CreateEntity("farm-monster-" + monster.Name);
            entity.SetPosition(position);

            // Deliberately no collider and no TAG_MONSTER: farm workers must never trigger battles
            var bodyAnimator = entity.AddComponent(new NamedMonsterAnimationComponent(typeName, Color.White));
            bodyAnimator.SetRenderLayer(GameConfig.RenderLayerActors);

            entity.AddComponent(new ActorFacingComponent());
            entity.AddComponent(new FarmMonsterMover());

            var hoeAnimator = entity.AddComponent(new PausableSpriteAnimator());
            hoeAnimator.SetRenderLayer(GameConfig.RenderLayerActors);
            hoeAnimator.SetEnabled(false);
            var cropsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
            var hoeAnimation = cropsAtlas?.GetAnimation("ForkedHoe");
            if (hoeAnimation != null)
                hoeAnimator.AddAnimation("ForkedHoe", hoeAnimation);

            var fsm = entity.AddComponent(new FarmingMonsterStateMachine(monster, this, new Point(house.TileX, house.TileY)));
            fsm.HoeAnimator = hoeAnimator;
            fsm.BodyAnimator = bodyAnimator;

            // Watering can — single sprite, shown during PerformWater
            var wateringCanAnimator = entity.AddComponent(new PausableSpriteAnimator());
            wateringCanAnimator.SetRenderLayer(GameConfig.RenderLayerActorPropOverlay);
            wateringCanAnimator.SetEnabled(false);
            var wateringCanSprite = cropsAtlas?.GetSprite("WateringCan");
            if (wateringCanSprite != null)
            {
                var singleFrameAnim = new Nez.Sprites.SpriteAnimation(new[] { wateringCanSprite }, 1);
                wateringCanAnimator.AddAnimation("WateringCan", singleFrameAnim);
            }

            // Watering effect — 6-frame animation played on top of the can
            var wateringAnimator = entity.AddComponent(new PausableSpriteAnimator());
            wateringAnimator.SetRenderLayer(GameConfig.RenderLayerActorPropOverlay);
            wateringAnimator.SetEnabled(false);
            var wateringAnim = cropsAtlas?.GetAnimation("Watering");
            if (wateringAnim != null)
                wateringAnimator.AddAnimation("Watering", wateringAnim);

            fsm.WateringCanAnimator = wateringCanAnimator;
            fsm.WateringAnimator = wateringAnimator;

            // Harvest carry sprite — the harvested crop shown in the worker's hands while delivering.
            // Sprite is set per-harvest in the FSM; starts hidden.
            var harvestCarryRenderer = entity.AddComponent(new Nez.Sprites.SpriteRenderer());
            harvestCarryRenderer.SetRenderLayer(GameConfig.RenderLayerActorPropOverlay);
            harvestCarryRenderer.SetEnabled(false);
            fsm.HarvestCarryRenderer = harvestCarryRenderer;

            // Spread concurrent workers across the queue so they don't all till side by side:
            // first worker claims from the front, second from the back, the rest from a fixed
            // random spot in between.
            fsm.QueuePick = _workers.Count == 0 ? 0f
                : _workers.Count == 1 ? 1f
                : Nez.Random.NextFloat();

            var worker = new ActiveWorker { Monster = monster, Entity = entity, Fsm = fsm };
            _workers.Add(worker);

            Debug.Log($"[FarmTaskCoordinator] Spawned farming monster '{monster.Name}' ({typeName}) at house {house.UniqueId}");
        }

        // Capacity limit matches AlliedMonsterManager (GameConfig.MonsterHouseCapacity per house)
        private PlacedBuilding FindMonsterHouseWithCapacity()
        {
            var all = _buildingService.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b.Type != BuildingType.MonsterHouse)
                    continue;
                if (!_alliedMonsters.IsHouseFull(b.UniqueId))
                    return b;
            }
            return null;
        }

        private PlacedBuilding FindMonsterHouse(int uniqueId)
        {
            if (uniqueId < 0)
                return null;
            var all = _buildingService.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b.UniqueId == uniqueId && b.Type == BuildingType.MonsterHouse)
                    return b;
            }
            return null;
        }

        /// <summary>
        /// Enqueues every ReadyToTill tile that is not already tracked. Called at construction and
        /// after a save restores tile states. Idempotent thanks to the tracked set.
        /// </summary>
        public void RescanReadyToTill()
        {
            var enumerator = _tileState.GetAllStates().GetEnumerator();
            while (enumerator.MoveNext())
            {
                if ((enumerator.Current.Value & TileStateFlag.ReadyToTill) != 0)
                    HandleReadyToTillSet(enumerator.Current.Key);
            }
            enumerator.Dispose();
        }

        /// <summary>Claims the next valid action from the queues (priority: Pickup > Till > Destroy > Plant > Harvest > Water).</summary>
        public bool TryClaimAction(out FarmAction action) => TryClaimAction(0f, out action);

        /// <summary>
        /// Pops the next valid action near the given normalized queue position (0 = front,
        /// 1 = back), with priority: Pickup > Till > Destroy > Plant > Harvest > Water.
        /// Workers are given different positions so they spread across the field instead of clustering.
        /// A returned action is considered claimed until Complete/Release/ReportBlocked.
        /// Returns false when all queues are empty.
        /// </summary>
        public bool TryClaimAction(float queuePick, out FarmAction action)
        {
            // Priority 0: recover dropped crops back into storage before starting new work
            PopulatePickupQueue();
            if (TryClaimFromQueue(_pickupQueue, _pickupTracked, queuePick, ValidatePickup, out action))
                return true;
            // Priority 1: Till
            if (TryClaimFromQueue(_queue, _tracked, queuePick, ValidateTill, out action))
                return true;
            // Priority 2: Destroy — remove repeat crops whose plan changed (frees tile for swap-plant)
            PopulateDestroyQueue();
            if (TryClaimFromQueue(_destroyQueue, _destroyTracked, queuePick, ValidateDestroy, out action))
                return true;
            // Priority 3: Plant
            if (TryClaimFromQueue(_plantQueue, _plantTracked, queuePick, ValidatePlant, out action))
                return true;
            // Priority 4: Harvest — collect fully-grown crops before watering still-growing ones
            PopulateHarvestQueue();
            if (TryClaimFromQueue(_harvestQueue, _harvestTracked, queuePick, ValidateHarvest, out action))
                return true;
            // Priority 5: Water — only when no plant or destroy work remains (queued or in-progress);
            // guards against watering a crop that is about to be destroyed for a swap.
            if (_plantTracked.Count == 0 && _destroyTracked.Count == 0)
            {
                PopulateWaterQueue();
                if (TryClaimFromQueue(_waterQueue, _waterTracked, queuePick, ValidateWater, out action))
                    return true;
            }
            action = default;
            return false;
        }

        private bool TryClaimFromQueue(Deque<FarmAction> queue, HashSet<Point> tracked, float queuePick,
            System.Func<Point, bool> validate, out FarmAction action)
        {
            while (queue.Count > 0)
            {
                int index = queue.Count == 1 ? 0 : (int)(queuePick * (queue.Count - 1) + 0.5f);
                action = queue[index];
                queue.RemoveAt(index);
                var tile = action.TargetTile;
                if (!tracked.Contains(tile))
                    continue;
                if (_buildingService.IsTileOccupied(tile.X, tile.Y))
                {
                    tracked.Remove(tile);
                    continue;
                }
                if (!validate(tile))
                {
                    tracked.Remove(tile);
                    continue;
                }
                return true;
            }
            action = default;
            return false;
        }

        private bool ValidateTill(Point tile) => _tileState.HasFlag(tile, TileStateFlag.ReadyToTill);

        private bool ValidatePlant(Point tile)
        {
            var cropPlanting = GetService<CropPlantingService>();
            var cropGrowth = GetService<CropGrowthService>();
            if (cropPlanting == null || !cropPlanting.HasPlan(tile))
                return false;
            if (cropGrowth != null && cropGrowth.HasCrop(tile))
                return false;
            var planType = cropPlanting.GetPlanType(tile);
            return planType.HasValue && cropPlanting.HasSeeds(planType.Value);
        }

        /// <summary>
        /// Returns true when a crop at the tile is a candidate for early removal: the plan is
        /// absent or designates a different crop type, and the crop is less than the swap-destroy
        /// threshold grown (so destroying and replanting is cheaper than waiting to harvest).
        /// Crops past the threshold are left to be harvested first. Null-safe for headless tests.
        /// </summary>
        private bool ValidateDestroy(Point tile)
        {
            var cropGrowth = GetService<CropGrowthService>();
            if (cropGrowth == null)
                return false;

            var cropType = cropGrowth.GetCropType(tile);
            if (!cropType.HasValue)
                return false;

            // No-op when the plan matches the growing crop (no swap pending)
            var cropPlanting = GetService<CropPlantingService>();
            var planType = cropPlanting?.GetPlanType(tile);
            if (planType.HasValue && planType.Value == cropType.Value)
                return false;

            // Only eligible while the crop is less than the threshold fraction grown
            float progress = cropGrowth.GetGrowthProgress(tile);
            if (progress < 0f || progress >= GameConfig.CropSwapDestroyProgressThreshold)
                return false;

            return true;
        }

        private bool ValidateWater(Point tile)
        {
            var cropGrowth = GetService<CropGrowthService>();
            return cropGrowth != null && cropGrowth.HasCrop(tile)
                && !_tileState.HasFlag(tile, TileStateFlag.Wet)
                // Skip fully-grown crops: watering does nothing for them, so workers prioritize
                // crops that are still growing.
                && !_tileState.HasFlag(tile, TileStateFlag.CropGrown);
        }

        private bool ValidateHarvest(Point tile)
        {
            var cropGrowth = GetService<CropGrowthService>();
            if (cropGrowth == null)
                return false;
            var cropType = cropGrowth.GetCropType(tile);
            if (cropType == null)
                return false;
            if (!_tileState.HasFlag(tile, TileStateFlag.CropGrown))
                return false;
            // Only claim if some Crop Storage can actually accept the harvested crop.
            return TryFindNearestStorageWithCapacity(tile, cropType.Value, out _, out _);
        }

        private bool ValidatePickup(Point tile)
        {
            if (_droppedCropService == null || !_droppedCropService.TryGetAt(tile, out var drop))
                return false;
            // Only claim if some Crop Storage can actually accept the dropped crop; otherwise the
            // drop stays on the ground until a storage frees up.
            return TryFindNearestStorageWithCapacity(tile, drop.Type, out _, out _);
        }

        /// <summary>
        /// Finds the Crop Storage building (with room for this crop) whose door is nearest the crop
        /// tile. Returns false when none exists or none has capacity.
        /// </summary>
        public bool TryFindNearestStorageWithCapacity(Point fromTile, Farming.CropType crop,
            out PlacedBuilding building, out Point doorTile)
        {
            building = null;
            doorTile = default;

            var storage = GetService<CropStorageInventoryService>();
            var all = _buildingService.GetAll();
            long best = long.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b.Type != BuildingType.CropStorage)
                    continue;
                if (storage != null && !storage.HasCapacityFor(b.UniqueId, crop))
                    continue;

                var door = BuildingConfig.GetDoorTile(b.Type, new Point(b.TileX, b.TileY));
                long dx = door.X - fromTile.X;
                long dy = door.Y - fromTile.Y;
                long distSq = dx * dx + dy * dy;
                if (distSq < best)
                {
                    best = distSq;
                    building = b;
                    doorTile = door;
                }
            }
            return building != null;
        }

        /// <summary>
        /// Marks a claimed till action finished. Call BEFORE TilledTileService.TillTile so the
        /// ReadyToTill-cleared event from the flag change is a no-op for the queue.
        /// </summary>
        public void CompleteAction(in FarmAction action) => _tracked.Remove(action.TargetTile);

        /// <summary>Returns a claimed till action to the front of the queue (e.g. job unassigned mid-walk).</summary>
        public void ReleaseAction(in FarmAction action) => _queue.AddFront(action);

        /// <summary>Marks a claimed plant action finished.</summary>
        public void CompletePlantAction(in FarmAction action) => _plantTracked.Remove(action.TargetTile);

        /// <summary>Returns a claimed plant action to the front of the queue.</summary>
        public void ReleasePlantAction(in FarmAction action) => _plantQueue.AddFront(action);

        /// <summary>Marks a claimed water action finished.</summary>
        public void CompleteWaterAction(in FarmAction action) => _waterTracked.Remove(action.TargetTile);

        /// <summary>Returns a claimed water action to the front of the queue.</summary>
        public void ReleaseWaterAction(in FarmAction action) => _waterQueue.AddFront(action);

        /// <summary>Marks a claimed harvest action finished.</summary>
        public void CompleteHarvestAction(in FarmAction action) => _harvestTracked.Remove(action.TargetTile);

        /// <summary>Returns a claimed harvest action to the front of the queue.</summary>
        public void ReleaseHarvestAction(in FarmAction action) => _harvestQueue.AddFront(action);

        /// <summary>Marks a claimed destroy-crop action finished.</summary>
        public void CompleteDestroyAction(in FarmAction action) => _destroyTracked.Remove(action.TargetTile);

        /// <summary>Returns a claimed destroy-crop action to the front of the queue.</summary>
        public void ReleaseDestroyAction(in FarmAction action) => _destroyQueue.AddFront(action);

        /// <summary>Marks a claimed pickup action finished (the drop was recovered or removed).</summary>
        public void CompletePickupAction(in FarmAction action) => _pickupTracked.Remove(action.TargetTile);

        /// <summary>Returns a claimed pickup action to the front of the queue (worker gave up).</summary>
        public void ReleasePickupAction(in FarmAction action) => _pickupQueue.AddFront(action);

        /// <summary>Reports a claimed action as unreachable; retried when buildings change.</summary>
        public void ReportBlocked(in FarmAction action)
        {
            _tracked.Remove(action.TargetTile);
            if (!_blocked.Contains(action.TargetTile))
                _blocked.Add(action.TargetTile);
        }

        /// <summary>
        /// Called when a crop plan is placed on a tile that is already Tilled, or after a destroy/
        /// harvest completes to schedule the waiting plan. Guards against duplicate enqueues and
        /// no-op calls (no plan, crop still present). Safe to call unconditionally.
        /// </summary>
        public void NotifyPlanAddedOnTilledTile(Point tile)
        {
            var cropGrowth = GetService<CropGrowthService>();
            if (cropGrowth != null && cropGrowth.HasCrop(tile))
                return;
            var cropPlanting = GetService<CropPlantingService>();
            if (cropPlanting == null || !cropPlanting.HasPlan(tile))
                return;
            if (_plantTracked.Add(tile))
                _plantQueue.AddBack(new FarmAction { Type = FarmActionType.Plant, TargetTile = tile });
        }

        /// <summary>
        /// Enqueues Plant actions for all Tilled tiles that have a crop plan but no planted crop.
        /// Called at load restore and on demand. Idempotent thanks to the tracked set.
        /// </summary>
        public void RescanForPlanting()
        {
            var cropPlanting = GetService<CropPlantingService>();
            var cropGrowth = GetService<CropGrowthService>();
            if (cropPlanting == null)
                return;

            var enumerator = _tileState.GetAllStates().GetEnumerator();
            while (enumerator.MoveNext())
            {
                if ((enumerator.Current.Value & TileStateFlag.Tilled) == 0)
                    continue;
                var tile = enumerator.Current.Key;
                if (!cropPlanting.HasPlan(tile))
                    continue;
                if (cropGrowth != null && cropGrowth.HasCrop(tile))
                    continue;
                if (_plantTracked.Add(tile))
                    _plantQueue.AddBack(new FarmAction { Type = FarmActionType.Plant, TargetTile = tile });
            }
            enumerator.Dispose();
        }

        /// <summary>
        /// Enqueues Water actions for all tiles with planted crops that are not currently wet.
        /// Called each morning and at load restore.
        /// </summary>
        public void PopulateWaterQueue()
        {
            var cropGrowth = GetService<CropGrowthService>();
            if (cropGrowth == null)
                return;

            foreach (var tile in cropGrowth.GetAllCropTiles())
            {
                if (_tileState.HasFlag(tile, TileStateFlag.Wet))
                    continue;
                // Fully-grown crops don't benefit from watering; skip them so workers prioritize
                // crops that are still growing.
                if (_tileState.HasFlag(tile, TileStateFlag.CropGrown))
                    continue;
                if (_waterTracked.Add(tile))
                    _waterQueue.AddBack(new FarmAction { Type = FarmActionType.Water, TargetTile = tile });
            }
        }

        /// <summary>
        /// Enqueues DestroyCrop actions for crops that are less than
        /// <see cref="GameConfig.CropSwapDestroyProgressThreshold"/> grown and whose plan is absent
        /// or designates a different crop type. Called from TryClaimAction (cheap scan).
        /// </summary>
        public void PopulateDestroyQueue()
        {
            var cropGrowth = GetService<CropGrowthService>();
            if (cropGrowth == null)
                return;

            foreach (var tile in cropGrowth.GetAllCropTiles())
            {
                if (!ValidateDestroy(tile))
                    continue;
                if (_destroyTracked.Add(tile))
                    _destroyQueue.AddBack(new FarmAction { Type = FarmActionType.DestroyCrop, TargetTile = tile });
            }
        }

        /// <summary>
        /// Enqueues Harvest actions for all fully-grown crop tiles not already tracked. Called from
        /// TryClaimAction (cheap scan, mirrors PopulateWaterQueue).
        /// </summary>
        public void PopulateHarvestQueue()
        {
            var cropGrowth = GetService<CropGrowthService>();
            if (cropGrowth == null)
                return;

            foreach (var tile in cropGrowth.GetAllCropTiles())
            {
                if (!_tileState.HasFlag(tile, TileStateFlag.CropGrown))
                    continue;
                if (_harvestTracked.Add(tile))
                    _harvestQueue.AddBack(new FarmAction { Type = FarmActionType.Harvest, TargetTile = tile });
            }
        }

        /// <summary>
        /// Enqueues PickupDrop actions for all ground drops not already tracked. Called from
        /// TryClaimAction (cheap scan, mirrors PopulateHarvestQueue).
        /// </summary>
        public void PopulatePickupQueue()
        {
            if (_droppedCropService == null)
                return;

            var all = _droppedCropService.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                var drop = all[i];
                var tile = drop.Tile;

                // Drops can sit on tiles a pickup can't target — dropped at the worker's
                // inside-the-building deposit position (older saves), or a building placed on top.
                // Relocate them to the nearest reachable tile so they don't stall in the queue forever.
                if (_buildingService.IsTileOccupied(tile.X, tile.Y) || !Pathfinder.IsPassable(tile))
                {
                    if (!TryFindDropRelocationTile(tile, out tile))
                        continue; // fully enclosed — retried next poll (e.g. after a building moves)
                    _droppedCropService.MoveDrop(drop, tile);
                }

                if (_pickupTracked.Add(tile))
                    _pickupQueue.AddBack(new FarmAction { Type = FarmActionType.PickupDrop, TargetTile = tile });
            }
        }

        /// <summary>
        /// Ring-searches outward for the nearest tile a pickup can target: passable, not covered
        /// by a building, and not already holding another drop.
        /// </summary>
        private bool TryFindDropRelocationTile(Point origin, out Point result)
        {
            for (int r = 1; r < 8; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (System.Math.Abs(dx) != r && System.Math.Abs(dy) != r)
                            continue; // ring perimeter only
                        var t = new Point(origin.X + dx, origin.Y + dy);
                        if (!Pathfinder.IsPassable(t))
                            continue;
                        if (_buildingService.IsTileOccupied(t.X, t.Y))
                            continue;
                        if (_droppedCropService.TryGetAt(t, out _))
                            continue;
                        result = t;
                        return true;
                    }
                }
            }
            result = origin;
            return false;
        }

        /// <summary>
        /// Finds the field tile (tilled or planned-for-tilling) closest to the given tile. Used to
        /// keep idle monsters wandering near the field instead of across the whole farm. Returns
        /// false when no field tiles exist.
        /// </summary>
        public bool TryGetNearestFieldTile(Point from, out Point nearest)
        {
            nearest = default;
            long best = long.MaxValue;
            var enumerator = _tileState.GetAllStates().GetEnumerator();
            while (enumerator.MoveNext())
            {
                if ((enumerator.Current.Value & (TileStateFlag.Tilled | TileStateFlag.ReadyToTill)) == 0)
                    continue;
                var tile = enumerator.Current.Key;
                long dx = tile.X - from.X;
                long dy = tile.Y - from.Y;
                long distSq = dx * dx + dy * dy;
                if (distSq < best)
                {
                    best = distSq;
                    nearest = tile;
                }
            }
            enumerator.Dispose();
            return best != long.MaxValue;
        }

        /// <summary>
        /// Rightmost tile X occupied by any farm object — a placed building (using its footprint's
        /// east edge) or a tilled/ready-to-till tile. Governs how far east idle monsters may wander.
        /// -1 when no farm objects exist. Maintained incrementally from building/tile events so the
        /// wander logic can read it every tick without scanning.
        /// </summary>
        public int RightmostFarmObjectTileX => _rightmostFarmObjectTileX;

        private int _rightmostFarmObjectTileX = -1;

        /// <summary>
        /// Full rescan of buildings and tilled/ready-to-till tiles. Called from the constructor,
        /// on shrink events (building changes, rightmost designation cleared), and after a save is
        /// loaded (tile flags restored from a save don't raise per-tile events).
        /// </summary>
        public void RecalculateRightmostFarmObject()
        {
            int max = -1;

            var buildings = _buildingService.GetAll();
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                var bounds = Util.BuildingConfig.GetFootprintBounds(b.Type);
                int right = b.TileX + bounds.dxMax;
                if (right > max)
                    max = right;
            }

            var enumerator = _tileState.GetAllStates().GetEnumerator();
            while (enumerator.MoveNext())
            {
                if ((enumerator.Current.Value & (TileStateFlag.Tilled | TileStateFlag.ReadyToTill)) == 0)
                    continue;
                if (enumerator.Current.Key.X > max)
                    max = enumerator.Current.Key.X;
            }
            enumerator.Dispose();

            _rightmostFarmObjectTileX = max;
        }

        private void HandleReadyToTillSet(Point tile)
        {
            if (tile.X > _rightmostFarmObjectTileX)
                _rightmostFarmObjectTileX = tile.X;
            if (_tracked.Add(tile))
                _queue.AddBack(new FarmAction { Type = FarmActionType.Till, TargetTile = tile });
        }

        private void HandleReadyToTillCleared(Point tile)
        {
            // Stale queue entries are dropped by validation in TryClaimAction; claimed tiles stay
            // tracked so the worker can re-validate on arrival.
            bool claimed = _tracked.Contains(tile) && !IsQueued(tile);
            if (!claimed)
                _tracked.Remove(tile);

            // Only a clear at the cached east edge can shrink the wander bound; anything left of
            // it can't change the max. (Till completion clears here then re-raises via OnTileTilled.)
            if (tile.X >= _rightmostFarmObjectTileX)
                RecalculateRightmostFarmObject();
        }

        private void HandleTileTilled(Point tile)
        {
            if (tile.X > _rightmostFarmObjectTileX)
                _rightmostFarmObjectTileX = tile.X;

            var cropPlanting = GetService<CropPlantingService>();
            if (cropPlanting == null || !cropPlanting.HasPlan(tile))
                return;
            if (_plantTracked.Add(tile))
                _plantQueue.AddBack(new FarmAction { Type = FarmActionType.Plant, TargetTile = tile });
        }

        private void HandleBuildingsChanged()
        {
            Pathfinder.RebuildWalls(_buildingService);

            // Building placement/removal is rare, so a full rescan of the wander bound is fine here
            RecalculateRightmostFarmObject();

            // Retry tiles that were unreachable — a new building can't help, but this also covers
            // future building moves/removals; tiles still ReadyToTill simply re-enter the queue.
            for (int i = _blocked.Count - 1; i >= 0; i--)
            {
                var tile = _blocked[i];
                _blocked.RemoveAt(i);
                if (_tileState.HasFlag(tile, TileStateFlag.ReadyToTill))
                    HandleReadyToTillSet(tile);
            }
        }

        private bool IsQueued(Point tile)
        {
            for (int i = 0; i < _queue.Count; i++)
                if (_queue[i].TargetTile == tile)
                    return true;
            return false;
        }
    }
}
