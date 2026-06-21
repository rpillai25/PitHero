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
        private TilledTileService _tilledTileService;

        private readonly List<ActiveWorker> _workers = new List<ActiveWorker>(16);
        private Scene _scene;

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
            wateringCanAnimator.SetRenderLayer(GameConfig.RenderLayerActors);
            wateringCanAnimator.SetEnabled(false);
            var wateringCanSprite = cropsAtlas?.GetSprite("WateringCan");
            if (wateringCanSprite != null)
            {
                var singleFrameAnim = new Nez.Sprites.SpriteAnimation(new[] { wateringCanSprite }, 1);
                wateringCanAnimator.AddAnimation("WateringCan", singleFrameAnim);
            }

            // Watering effect — 6-frame animation played on top of the can
            var wateringAnimator = entity.AddComponent(new PausableSpriteAnimator());
            wateringAnimator.SetRenderLayer(GameConfig.RenderLayerActors);
            wateringAnimator.SetEnabled(false);
            var wateringAnim = cropsAtlas?.GetAnimation("Watering");
            if (wateringAnim != null)
                wateringAnimator.AddAnimation("Watering", wateringAnim);

            fsm.WateringCanAnimator = wateringCanAnimator;
            fsm.WateringAnimator = wateringAnimator;

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

        // Capacity limit matches AlliedMonsterManager.TryRecruit (16 monsters per house)
        private PlacedBuilding FindMonsterHouseWithCapacity()
        {
            var roster = _alliedMonsters.AlliedMonsters;
            var all = _buildingService.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b.Type != BuildingType.MonsterHouse)
                    continue;
                int linked = 0;
                for (int m = 0; m < roster.Count; m++)
                    if (roster[m].MonsterHouseId == b.UniqueId)
                        linked++;
                if (linked < 16)
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

        /// <summary>Claims the next valid action from the queues (priority: Till > Plant > Water).</summary>
        public bool TryClaimAction(out FarmAction action) => TryClaimAction(0f, out action);

        /// <summary>
        /// Pops the next valid action near the given normalized queue position (0 = front,
        /// 1 = back), with priority: Till first, then Plant, then Water.
        /// Workers are given different positions so they spread across the field instead of clustering.
        /// A returned action is considered claimed until Complete/Release/ReportBlocked.
        /// Returns false when all queues are empty.
        /// </summary>
        public bool TryClaimAction(float queuePick, out FarmAction action)
        {
            // Priority 1: Till
            if (TryClaimFromQueue(_queue, _tracked, queuePick, ValidateTill, out action))
                return true;
            // Priority 2: Plant
            if (TryClaimFromQueue(_plantQueue, _plantTracked, queuePick, ValidatePlant, out action))
                return true;
            // Priority 3: Water — only when no plant work remains (queued or in-progress)
            if (_plantTracked.Count == 0)
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
            var cropPlanting = Core.Services.GetService<CropPlantingService>();
            var cropGrowth = Core.Services.GetService<CropGrowthService>();
            return cropPlanting != null && cropPlanting.HasPlan(tile)
                && (cropGrowth == null || !cropGrowth.HasCrop(tile));
        }

        private bool ValidateWater(Point tile)
        {
            var cropGrowth = Core.Services.GetService<CropGrowthService>();
            return cropGrowth != null && cropGrowth.HasCrop(tile)
                && !_tileState.HasFlag(tile, TileStateFlag.Wet)
                // Skip fully-grown crops: watering does nothing for them, so workers prioritize
                // crops that are still growing.
                && !_tileState.HasFlag(tile, TileStateFlag.CropGrown);
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

        /// <summary>Reports a claimed action as unreachable; retried when buildings change.</summary>
        public void ReportBlocked(in FarmAction action)
        {
            _tracked.Remove(action.TargetTile);
            if (!_blocked.Contains(action.TargetTile))
                _blocked.Add(action.TargetTile);
        }

        /// <summary>
        /// Called when a crop plan is placed on a tile that is already Tilled.
        /// In that case HandleTileTilled won't fire again, so we enqueue the Plant action here.
        /// </summary>
        public void NotifyPlanAddedOnTilledTile(Point tile)
        {
            var cropGrowth = Core.Services.GetService<CropGrowthService>();
            if (cropGrowth != null && cropGrowth.HasCrop(tile))
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
            var cropPlanting = Core.Services.GetService<CropPlantingService>();
            var cropGrowth = Core.Services.GetService<CropGrowthService>();
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
            var cropGrowth = Core.Services.GetService<CropGrowthService>();
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

        private void HandleReadyToTillSet(Point tile)
        {
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
        }

        private void HandleTileTilled(Point tile)
        {
            var cropPlanting = Core.Services.GetService<CropPlantingService>();
            if (cropPlanting == null || !cropPlanting.HasPlan(tile))
                return;
            if (_plantTracked.Add(tile))
                _plantQueue.AddBack(new FarmAction { Type = FarmActionType.Plant, TargetTile = tile });
        }

        private void HandleBuildingsChanged()
        {
            Pathfinder.RebuildWalls(_buildingService);

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
