using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.Farming;
using PitHero.Util;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services
{
    /// <summary>
    /// Central coordinator for farm work. Fills a shared action queue from ReadyToTill tile flags
    /// (event-driven — no per-frame scans) and hands actions out to farming monsters so multiple
    /// workers never target the same tile. Also owns the shared farm pathfinder.
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

        private readonly List<ActiveWorker> _workers = new List<ActiveWorker>(16);
        private Scene _scene;

        /// <summary>Shared A* grid for all farming monsters.</summary>
        public FarmPathfinder Pathfinder { get; }

        /// <summary>Number of actions waiting in the queue (excludes claimed actions).</summary>
        public int PendingActionCount => _queue.Count;

        public FarmTaskCoordinator(TileStateService tileState, BuildingService buildingService,
            int mapWidthTiles, int mapHeightTiles, AlliedMonsterManager alliedMonsters = null)
        {
            _tileState = tileState;
            _buildingService = buildingService;
            _alliedMonsters = alliedMonsters;

            Pathfinder = new FarmPathfinder(mapWidthTiles, mapHeightTiles);
            Pathfinder.RebuildWalls(buildingService);

            _tileState.OnReadyToTillSet += HandleReadyToTillSet;
            _tileState.OnReadyToTillCleared += HandleReadyToTillCleared;
            _buildingService.BuildingsChanged += HandleBuildingsChanged;

            RescanReadyToTill();
        }

        /// <summary>Unsubscribes from service events. Call when the scene is torn down.</summary>
        public void Detach()
        {
            _tileState.OnReadyToTillSet -= HandleReadyToTillSet;
            _tileState.OnReadyToTillCleared -= HandleReadyToTillCleared;
            _buildingService.BuildingsChanged -= HandleBuildingsChanged;
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

            var roster = _alliedMonsters.AlliedMonsters;
            for (int i = 0; i < roster.Count; i++)
            {
                var monster = roster[i];
                int workerIndex = FindWorkerIndex(monster);

                if (monster.Job == MonsterJob.Farming)
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

        /// <summary>
        /// Pops the next valid till action, skipping entries invalidated since they were queued.
        /// A returned action is considered claimed until CompleteAction/ReleaseAction/ReportBlocked.
        /// Returns false when the queue is empty.
        /// </summary>
        public bool TryClaimAction(out FarmAction action)
        {
            while (_queue.Count > 0)
            {
                action = _queue.RemoveFront();
                var tile = action.TargetTile;
                if (!_tracked.Contains(tile))
                    continue;   // unmarked while queued
                if (!_tileState.HasFlag(tile, TileStateFlag.ReadyToTill))
                {
                    _tracked.Remove(tile);
                    continue;
                }
                if (_buildingService.IsTileOccupied(tile.X, tile.Y))
                {
                    _tracked.Remove(tile);
                    continue;
                }
                return true;
            }
            action = default;
            return false;
        }

        /// <summary>
        /// Marks a claimed action finished. Call BEFORE TilledTileService.TillTile so the
        /// ReadyToTill-cleared event from the flag change is a no-op for the queue.
        /// </summary>
        public void CompleteAction(in FarmAction action) => _tracked.Remove(action.TargetTile);

        /// <summary>Returns a claimed action to the front of the queue (e.g. job unassigned mid-walk).</summary>
        public void ReleaseAction(in FarmAction action) => _queue.AddFront(action);

        /// <summary>Reports a claimed action as unreachable; retried when buildings change.</summary>
        public void ReportBlocked(in FarmAction action)
        {
            _tracked.Remove(action.TargetTile);
            if (!_blocked.Contains(action.TargetTile))
                _blocked.Add(action.TargetTile);
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
