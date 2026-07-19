using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.Config;
using PitHero.Dining;
using PitHero.ECS.Components;
using PitHero.Farming;
using PitHero.Util;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services
{
    /// <summary>
    /// Central coordinator for kitchen/tavern work. Manages worker lifecycles (cooks, servers,
    /// runners), the ticket queue, crop withdrawals, and patron notifications.
    /// </summary>
    public class KitchenTaskCoordinator
    {
        // ── Internal types ──────────────────────────────────────────────────────

        private struct ActiveWorker
        {
            public AlliedMonster Monster;
            public Entity Entity;
            public KitchenMonsterStateMachine Fsm;
            public KitchenRole Role;
            public int StoveIndex; // -1 for server/runner
        }

        public struct BusJob
        {
            public Entity DishEntity; // entity on the table to be bussed (may be null if already gone)
            public Vector2 WorldPos;  // where to pick it up from
        }

        // ── Services ────────────────────────────────────────────────────────────
        private readonly AlliedMonsterManager _alliedMonsters;
        private readonly BuildingService _buildingService;
        private CropStorageInventoryService _cropStorage;
        private DroppedCropService _droppedCrops;
        private DishEntityService _dishService;
        private GameStateService _gameState;

        // ── Workers ─────────────────────────────────────────────────────────────
        private readonly List<ActiveWorker> _workers = new List<ActiveWorker>(8);
        private Scene _scene;

        // Scratch arrays for role assignment (pre-allocated, reset each reconcile)
        private readonly List<AlliedMonster> _wantedAssignments = new List<AlliedMonster>(8);
        private readonly List<KitchenRole> _wantedRoles = new List<KitchenRole>(8);
        private readonly List<int> _wantedStoves = new List<int>(8);
        private readonly List<bool> _matchedWorkerScratch = new List<bool>(8);

        // ── Pathfinder ──────────────────────────────────────────────────────────
        /// <summary>Shared A* grid for all kitchen monsters.</summary>
        public FarmPathfinder Pathfinder { get; }

        // ── Tickets ─────────────────────────────────────────────────────────────
        private readonly List<KitchenTicket> _tickets = new List<KitchenTicket>(16);
        private int _nextTicketId;

        // Claim tokens (null = not claimed)
        // CookClaim[stoveIndex] = ticket being cooked on that stove
        private readonly KitchenTicket[] _cookClaim = new KitchenTicket[GameConfig.MaxKitchenCooks];
        // RunnerClaim = ticket a runner is fetching for
        private KitchenTicket _runnerClaim;

        // ── Bus queue ───────────────────────────────────────────────────────────
        private readonly List<BusJob> _busJobs = new List<BusJob>(8);

        // ── Party order source ──────────────────────────────────────────────────
        private IPartyOrderSource _partyOrderSource;

        // ── Kitchen open/closed ─────────────────────────────────────────────────
        // Indices into _workers for the designated post workers (set during reconcile)
        private int _cook1WorkerIdx = -1;
        private int _server1WorkerIdx = -1;

        // ── Public state ────────────────────────────────────────────────────────

        /// <summary>True when ≥1 cook and ≥1 server are assigned and awake.</summary>
        public bool IsKitchenOpen => _cook1WorkerIdx >= 0 && _server1WorkerIdx >= 0;

        // ── Constructor ─────────────────────────────────────────────────────────

        public KitchenTaskCoordinator(AlliedMonsterManager alliedMonsters,
            BuildingService buildingService,
            int mapWidthTiles, int mapHeightTiles,
            Nez.Tiled.TmxLayer collisionLayer = null)
        {
            _alliedMonsters = alliedMonsters;
            _buildingService = buildingService;
            Pathfinder = new FarmPathfinder(mapWidthTiles, mapHeightTiles);
            Pathfinder.SeedStaticWalls(collisionLayer);
            if (buildingService != null)
            {
                Pathfinder.RebuildWalls(buildingService);
                buildingService.BuildingsChanged += HandleBuildingsChanged;
            }
        }

        /// <summary>Provides the scene used to spawn kitchen worker entities.</summary>
        public void Initialize(Scene scene)
        {
            _scene = scene;
            _cropStorage = Core.Services.GetService<CropStorageInventoryService>();
            _droppedCrops = Core.Services.GetService<DroppedCropService>();
            _dishService = Core.Services.GetService<DishEntityService>();
        }

        /// <summary>Unsubscribes from service events. Call when the scene is torn down.</summary>
        public void Detach()
        {
            if (_buildingService != null)
                _buildingService.BuildingsChanged -= HandleBuildingsChanged;
        }

        // ── Per-frame tick ───────────────────────────────────────────────────────

        /// <summary>Per-frame tick: reconciles worker assignments and reaps destroyed entities.</summary>
        public void Update()
        {
            if (_alliedMonsters == null || _scene == null)
                return;

            var timeService = Core.Services.GetService<InGameTimeService>();

            // Build the wanted assignment list (sorted by CookingProficiency descending).
            // Insertion sort — allocation-free, small list.
            _wantedAssignments.Clear();
            _wantedRoles.Clear();
            _wantedStoves.Clear();

            var roster = _alliedMonsters.AlliedMonsters;
            for (int i = 0; i < roster.Count; i++)
            {
                var m = roster[i];
                if (m.Job != MonsterJob.Cooking)
                    continue;
                if (MonsterScheduleConfig.IsAsleep(m.MonsterTypeName, timeService))
                    continue;

                // Insertion sort by CookingProficiency descending
                int insertPos = _wantedAssignments.Count;
                for (int j = 0; j < _wantedAssignments.Count; j++)
                {
                    if (m.CookingProficiency > _wantedAssignments[j].CookingProficiency)
                    {
                        insertPos = j;
                        break;
                    }
                }
                _wantedAssignments.Insert(insertPos, m);
            }

            // Assign roles in order: cook1(stove0), server1, runner1, cook2(stove1), server2, runner2, cook3(stove2)
            // Max 7 workers; extras stay home.
            // Post order: cook1, server1, runner1, cook2, server2, runner2, cook3
            int postCount = _wantedAssignments.Count < 7 ? _wantedAssignments.Count : 7;
            for (int i = 0; i < postCount; i++)
            {
                KitchenRole role;
                int stove = -1;
                switch (i)
                {
                    case 0: role = KitchenRole.Cook;   stove = 0; break;
                    case 1: role = KitchenRole.Server;             break;
                    case 2: role = KitchenRole.Runner;             break;
                    case 3: role = KitchenRole.Cook;   stove = 1; break;
                    case 4: role = KitchenRole.Server;             break;
                    case 5: role = KitchenRole.Runner;             break;
                    default: role = KitchenRole.Cook;  stove = 2; break;
                }
                _wantedRoles.Add(role);
                _wantedStoves.Add(stove);
            }

            // Track which pre-existing workers keep their assignment. SpawnWorker appends to
            // _workers mid-pass, so snapshot the count and never index past it.
            int existingWorkerCount = _workers.Count;
            _matchedWorkerScratch.Clear();
            for (int wi = 0; wi < existingWorkerCount; wi++)
                _matchedWorkerScratch.Add(false);

            // For monsters with an existing worker: check if (monster, role) changed
            for (int wi = 0; wi < existingWorkerCount; wi++)
            {
                var w = _workers[wi];
                // Find this worker's monster in the wanted list
                int wantedIdx = -1;
                for (int j = 0; j < _wantedAssignments.Count; j++)
                {
                    if (ReferenceEquals(_wantedAssignments[j], w.Monster))
                    {
                        wantedIdx = j;
                        break;
                    }
                }

                if (wantedIdx < 0 || wantedIdx >= postCount)
                {
                    // Monster no longer in rotation — send home
                    w.Fsm.RequestReturnHome();
                }
                else
                {
                    var wantedRole = _wantedRoles[wantedIdx];
                    var wantedStove = _wantedStoves[wantedIdx];
                    if (w.Role == wantedRole && w.StoveIndex == wantedStove)
                    {
                        // Assignment unchanged — cancel any pending return
                        w.Fsm.CancelReturnHome();
                        _matchedWorkerScratch[wi] = true;
                    }
                    else
                    {
                        // Role changed — send home; will be respawned next reconcile
                        w.Fsm.RequestReturnHome();
                        // Don't mark as matched — new worker will be spawned
                    }
                }
            }

            // Spawn workers for wanted monsters that have no matching worker. Scan only the
            // pre-existing workers — SpawnWorker grows _workers within this loop.
            for (int j = 0; j < postCount; j++)
            {
                var monster = _wantedAssignments[j];
                bool hasWorker = false;
                for (int wi = 0; wi < existingWorkerCount; wi++)
                {
                    if (_matchedWorkerScratch[wi] && ReferenceEquals(_workers[wi].Monster, monster))
                    {
                        hasWorker = true;
                        break;
                    }
                }
                if (!hasWorker)
                    SpawnWorker(monster, _wantedRoles[j], _wantedStoves[j]);
            }

            // Reap workers whose entities finished despawning
            for (int wi = _workers.Count - 1; wi >= 0; wi--)
            {
                if (_workers[wi].Entity.IsDestroyed)
                    _workers.RemoveAt(wi);
            }

            // Update kitchen-open post indices (first cook, first server)
            _cook1WorkerIdx = -1;
            _server1WorkerIdx = -1;
            for (int wi = 0; wi < _workers.Count; wi++)
            {
                if (_cook1WorkerIdx < 0 && _workers[wi].Role == KitchenRole.Cook && !_workers[wi].Fsm.IsReturningHome)
                    _cook1WorkerIdx = wi;
                if (_server1WorkerIdx < 0 && _workers[wi].Role == KitchenRole.Server && !_workers[wi].Fsm.IsReturningHome)
                    _server1WorkerIdx = wi;
            }
        }

        // ── Worker spawning ──────────────────────────────────────────────────────

        private void SpawnWorker(AlliedMonster monster, KitchenRole role, int stoveIndex)
        {
            var house = FindMonsterHouse(monster.MonsterHouseId);
            if (house == null)
                house = FindMonsterHouseWithCapacity();
            if (house == null)
                return;

            var doorTile = new Point(house.TileX, house.TileY + 2);
            var position = new Vector2(
                doorTile.X * GameConfig.TileSize + GameConfig.TileSize / 2f,
                doorTile.Y * GameConfig.TileSize + GameConfig.TileSize / 2f);

            string typeName = monster.MonsterTypeName.StartsWith("Monster_")
                ? monster.MonsterTypeName.Substring("Monster_".Length)
                : monster.MonsterTypeName;

            var entity = _scene.CreateEntity("kitchen-monster-" + monster.Name);
            entity.SetPosition(position);

            // No collider, no TAG_MONSTER — kitchen workers must never trigger battles
            var bodyAnimator = entity.AddComponent(new NamedMonsterAnimationComponent(typeName, Color.White));
            bodyAnimator.SetRenderLayer(GameConfig.RenderLayerActors);

            entity.AddComponent(new ActorFacingComponent());
            entity.AddComponent(new FarmMonsterMover());

            // Carry renderer for holding a dish while delivering
            var carryRenderer = entity.AddComponent(new Nez.Sprites.SpriteRenderer());
            carryRenderer.SetRenderLayer(GameConfig.RenderLayerActorPropOverlay);
            carryRenderer.SetEnabled(false);

            var fsm = entity.AddComponent(new KitchenMonsterStateMachine(
                monster, this, new Point(house.TileX, house.TileY), role, stoveIndex));
            fsm.BodyAnimator = bodyAnimator;
            fsm.CarryRenderer = carryRenderer;

            var worker = new ActiveWorker
            {
                Monster = monster,
                Entity = entity,
                Fsm = fsm,
                Role = role,
                StoveIndex = stoveIndex,
            };
            _workers.Add(worker);

            Debug.Log($"[KitchenTaskCoordinator] Spawned {role} monster '{monster.Name}' ({typeName}) stove={stoveIndex}");
        }

        private PlacedBuilding FindMonsterHouse(int uniqueId)
        {
            if (uniqueId < 0 || _buildingService == null)
                return null;
            var all = _buildingService.GetAll();
            for (int i = 0; i < all.Count; i++)
                if (all[i].UniqueId == uniqueId && all[i].Type == BuildingType.MonsterHouse)
                    return all[i];
            return null;
        }

        private PlacedBuilding FindMonsterHouseWithCapacity()
        {
            if (_buildingService == null)
                return null;
            var all = _buildingService.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Type != BuildingType.MonsterHouse)
                    continue;
                if (!_alliedMonsters.IsHouseFull(all[i].UniqueId))
                    return all[i];
            }
            return null;
        }

        // ── Ticket API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a ticket and withdraws the recipe crops (all-or-nothing).
        /// Returns null if ingredients cannot be covered or the kitchen queue is full.
        /// </summary>
        public KitchenTicket CreateTicket(DishType dish, bool isParty, int partySlot, Entity patronEntity)
        {
            if (_tickets.Count >= 16)
                return null;

            EnsureServices();
            var def = DishConfig.GetDefinition(dish);

            // All-or-nothing availability check first
            for (int i = 0; i < def.Recipe.Length; i++)
            {
                if (_cropStorage == null || _cropStorage.CountTotal(def.Recipe[i].Crop) < def.Recipe[i].Qty)
                    return null;
            }

            // Withdraw each ingredient
            for (int i = 0; i < def.Recipe.Length; i++)
            {
                if (!(_cropStorage?.TryWithdrawAcrossBuildings(def.Recipe[i].Crop, def.Recipe[i].Qty) ?? false))
                {
                    // Partial withdrawal already happened — refund what we took
                    for (int r = 0; r < i; r++)
                        _cropStorage?.DepositAcrossBuildings(def.Recipe[r].Crop, def.Recipe[r].Qty);
                    return null;
                }
            }

            var ticket = MakeTicket(dish, isParty, partySlot, patronEntity);

            // If no Crop Storage buildings exist, skip runner trip
            if (!HasAnyCropStorage())
                ticket.IngredientsFetched = true;

            if (ticket.IngredientsFetched)
                ticket.State = TicketState.ReadyToCook;

            return ticket;
        }

        /// <summary>
        /// Creates a ticket WITHOUT withdrawing crops (save-reload path — crops already deducted).
        /// </summary>
        public KitchenTicket CreateTicketPreReserved(DishType dish, int partySlot)
        {
            if (_tickets.Count >= 16)
                return null;
            var ticket = MakeTicket(dish, true, partySlot, null);
            ticket.IngredientsFetched = true;
            ticket.State = TicketState.ReadyToCook;
            return ticket;
        }

        private KitchenTicket MakeTicket(DishType dish, bool isParty, int partySlot, Entity patronEntity)
        {
            var ticket = new KitchenTicket
            {
                TicketId = ++_nextTicketId,
                Dish = dish,
                IsPartyTicket = isParty,
                PartySlot = partySlot,
                PatronEntity = patronEntity,
                State = TicketState.AwaitingIngredients,
            };
            _tickets.Add(ticket);
            return ticket;
        }

        /// <summary>
        /// Cancels a ticket. Pre-cooking: refunds crops. Plated/Delivering: marks Canceled
        /// and leaves the dish entity in place for a server bus job.
        /// </summary>
        public void CancelTicket(KitchenTicket t)
        {
            if (t == null || t.State == TicketState.Canceled)
                return;

            if (t.CropsRefundable)
            {
                EnsureServices();
                var def = DishConfig.GetDefinition(t.Dish);
                for (int i = 0; i < def.Recipe.Length; i++)
                    _cropStorage?.DepositAcrossBuildings(def.Recipe[i].Crop, def.Recipe[i].Qty);
            }
            else if (!t.IsPartyTicket)
            {
                // Patron left after cooking started (patience expired or hired mid-dining):
                // the crops are spent, the dish is made — payment is still collected (no tip)
                EnsureServices();
                _gameState?.AddFunds(DishConfig.GetPrice(t.Dish), "dish_sale");
            }

            // If dish is on a table (Delivered state), enqueue a bus job
            if ((t.State == TicketState.Delivered || t.State == TicketState.Plated
                || t.State == TicketState.Delivering) && t.PlatedDishEntity != null)
            {
                Vector2 pickupPos;
                if (t.PlatedDishEntity.IsDestroyed)
                    pickupPos = Vector2.Zero;
                else
                    pickupPos = t.PlatedDishEntity.Transform.Position;

                if (!t.PlatedDishEntity.IsDestroyed)
                    _busJobs.Add(new BusJob { DishEntity = t.PlatedDishEntity, WorldPos = pickupPos });
            }

            t.State = TicketState.Canceled;
            _tickets.Remove(t);

            // Release cook claim if this stove was cooking it
            if (t.StoveIndex >= 0 && t.StoveIndex < _cookClaim.Length
                && ReferenceEquals(_cookClaim[t.StoveIndex], t))
            {
                _cookClaim[t.StoveIndex] = null;
            }
            // Release runner claim
            if (ReferenceEquals(_runnerClaim, t))
                _runnerClaim = null;
        }

        /// <summary>Finds and cancels the ticket belonging to the given patron entity.</summary>
        public void CancelTicketForPatron(Entity patronEntity)
        {
            if (patronEntity == null)
                return;
            for (int i = _tickets.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_tickets[i].PatronEntity, patronEntity))
                {
                    CancelTicket(_tickets[i]);
                    return;
                }
            }
        }

        /// <summary>True if storage can cover every recipe entry for the dish.</summary>
        public bool CanCoverRecipe(DishType dish)
        {
            EnsureServices();
            var def = DishConfig.GetDefinition(dish);
            for (int i = 0; i < def.Recipe.Length; i++)
            {
                if (_cropStorage == null || _cropStorage.CountTotal(def.Recipe[i].Crop) < def.Recipe[i].Qty)
                    return false;
            }
            return true;
        }

        /// <summary>Clears <paramref name="results"/> and fills it with dishes whose recipe is coverable.</summary>
        public void GetOrderableDishes(List<DishType> results)
        {
            results.Clear();
            for (int d = 0; d < DishTypeInfo.Count; d++)
            {
                var dish = (DishType)d;
                if (CanCoverRecipe(dish))
                    results.Add(dish);
            }
        }

        /// <summary>Registers the party order source (Phase 6).</summary>
        public void SetPartyOrderSource(IPartyOrderSource source) => _partyOrderSource = source;

        /// <summary>Returns the live (non-canceled) party ticket for the given slot, or null.</summary>
        public KitchenTicket GetPartyTicket(int partySlot)
        {
            for (int i = 0; i < _tickets.Count; i++)
            {
                var t = _tickets[i];
                if (t.IsPartyTicket && t.PartySlot == partySlot && t.State != TicketState.Canceled)
                    return t;
            }
            return null;
        }

        /// <summary>
        /// Called when a patron finishes eating. Despawns the table dish entity, spawns an
        /// EmptyPlate, and enqueues a bus job.
        /// </summary>
        public void NotifyPatronFinishedEating(KitchenTicket t)
        {
            if (t == null) return;

            Vector2 platePos = Vector2.Zero;
            bool hasPos = false;
            if (t.PlatedDishEntity != null && !t.PlatedDishEntity.IsDestroyed)
            {
                platePos = t.PlatedDishEntity.Transform.Position;
                hasPos = true;
                _dishService?.Despawn(t.PlatedDishEntity);
                t.PlatedDishEntity = null;
            }

            if (hasPos)
            {
                var emptyPlate = _dishService?.SpawnEmptyPlateAtWorldPos(platePos);
                if (emptyPlate != null)
                    _busJobs.Add(new BusJob { DishEntity = emptyPlate, WorldPos = platePos });
            }

            _tickets.Remove(t);
        }

        /// <summary>
        /// Called when a party member finishes eating. Same as patron, different caller.
        /// </summary>
        public void NotifyPartyMemberFinishedEating(KitchenTicket t) => NotifyPatronFinishedEating(t);

        // ── Internal FSM claim API ────────────────────────────────────────────────

        /// <summary>
        /// Cook tries to claim a ReadyToCook ticket for its stove. Party tickets have priority.
        /// Returns null when nothing is available.
        /// </summary>
        public KitchenTicket TryClaimCookTicket(int stoveIndex)
        {
            if (stoveIndex < 0 || stoveIndex >= _cookClaim.Length)
                return null;
            if (_cookClaim[stoveIndex] != null)
                return null; // stove already claimed

            // Party tickets first
            for (int i = 0; i < _tickets.Count; i++)
            {
                var t = _tickets[i];
                if (t.State == TicketState.ReadyToCook && t.IsPartyTicket && t.StoveIndex < 0)
                {
                    t.StoveIndex = stoveIndex;
                    t.State = TicketState.Cooking;
                    t.CropsRefundable = false;
                    _cookClaim[stoveIndex] = t;
                    return t;
                }
            }
            // Then patron tickets (FIFO)
            for (int i = 0; i < _tickets.Count; i++)
            {
                var t = _tickets[i];
                if (t.State == TicketState.ReadyToCook && !t.IsPartyTicket && t.StoveIndex < 0)
                {
                    t.StoveIndex = stoveIndex;
                    t.State = TicketState.Cooking;
                    t.CropsRefundable = false;
                    _cookClaim[stoveIndex] = t;
                    return t;
                }
            }
            return null;
        }

        /// <summary>
        /// Runner tries to claim an AwaitingIngredients ticket not already being fetched.
        /// Returns null when nothing is available.
        /// </summary>
        public KitchenTicket TryClaimFetchTicket()
        {
            if (_runnerClaim != null)
                return null; // runner already has a job

            for (int i = 0; i < _tickets.Count; i++)
            {
                var t = _tickets[i];
                if (t.State == TicketState.AwaitingIngredients && !t.IngredientsFetched)
                {
                    _runnerClaim = t;
                    return t;
                }
            }
            return null;
        }

        /// <summary>
        /// Worker was interrupted mid-cook: release the stove claim and reset ticket to ReadyToCook
        /// (ingredients already fetched). Runner interruption: release runner claim.
        /// </summary>
        public void ReleaseTicket(KitchenTicket t)
        {
            if (t == null) return;

            if (t.State == TicketState.Cooking)
            {
                t.State = TicketState.ReadyToCook;
                if (t.StoveIndex >= 0 && t.StoveIndex < _cookClaim.Length)
                    _cookClaim[t.StoveIndex] = null;
                t.StoveIndex = -1;
            }
            if (ReferenceEquals(_runnerClaim, t))
                _runnerClaim = null;
        }

        /// <summary>
        /// Returns the next pending bus job for a server to work on.
        /// Removes the job from the queue.
        /// </summary>
        public bool TryClaimBusJob(out BusJob job)
        {
            if (_busJobs.Count == 0)
            {
                job = default;
                return false;
            }
            job = _busJobs[0];
            _busJobs.RemoveAt(0);
            return true;
        }

        /// <summary>Sink tile position for busing (carry dish here and despawn).</summary>
        public static Point SinkTile => new Point(GameConfig.KitchenSinkTileX, GameConfig.KitchenSinkTileY);

        /// <summary>World center of the sink tile.</summary>
        public static Vector2 SinkWorldPos => new Vector2(
            GameConfig.KitchenSinkTileX * GameConfig.TileSize + GameConfig.TileSize / 2f,
            GameConfig.KitchenSinkTileY * GameConfig.TileSize + GameConfig.TileSize / 2f);

        /// <summary>Returns the stove tile for the given stove index (cook stands here).</summary>
        public static Point GetStoveTile(int stoveIndex)
        {
            int x;
            switch (stoveIndex)
            {
                case 0: x = GameConfig.KitchenStove1TileX; break;
                case 1: x = GameConfig.KitchenStove2TileX; break;
                default: x = GameConfig.KitchenStove3TileX; break;
            }
            return new Point(x, GameConfig.KitchenStoveTileY);
        }

        /// <summary>Returns the tile above the stove where a plated dish is placed (and server picks it up).</summary>
        public static Point GetPlateTile(int stoveIndex)
        {
            var stove = GetStoveTile(stoveIndex);
            return new Point(stove.X, stove.Y - 1);
        }

        /// <summary>Nearest CropStorage door tile from the given origin. Returns false if none exists.</summary>
        public bool TryFindNearestStorageDoor(Point fromTile, out Point doorTile)
        {
            doorTile = default;
            if (_buildingService == null)
                return false;

            var all = _buildingService.GetAll();
            long best = long.MaxValue;
            bool found = false;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Type != BuildingType.CropStorage)
                    continue;
                var door = Util.BuildingConfig.GetDoorTile(all[i].Type, new Point(all[i].TileX, all[i].TileY));
                long dx = door.X - fromTile.X;
                long dy = door.Y - fromTile.Y;
                long distSq = dx * dx + dy * dy;
                if (distSq < best)
                {
                    best = distSq;
                    doorTile = door;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>True if the party order source is set and has a pending order.</summary>
        public bool TryGetNextPartyOrder(out int partySlot, out DishType dish)
        {
            if (_partyOrderSource != null)
                return _partyOrderSource.TryGetNextPartyOrder(out partySlot, out dish);
            partySlot = -1;
            dish = default;
            return false;
        }

        /// <summary>Notifies party order source that a server took the order.</summary>
        public void NotifyPartyOrderTaken(int partySlot, KitchenTicket ticket)
            => _partyOrderSource?.OnPartyOrderTaken(partySlot, ticket);

        /// <summary>Notifies party order source that a dish was delivered to the table.</summary>
        public void NotifyPartyDishDelivered(int partySlot, KitchenTicket ticket)
            => _partyOrderSource?.OnPartyDishDelivered(partySlot, ticket);

        // ── Helpers ──────────────────────────────────────────────────────────────

        private void EnsureServices()
        {
            // Core.Services requires a running game instance; headless tests inject via SetHeadlessServices.
            if (Core.Instance == null)
                return;
            if (_cropStorage == null)
                _cropStorage = Core.Services.GetService<CropStorageInventoryService>();
            if (_droppedCrops == null)
                _droppedCrops = Core.Services.GetService<DroppedCropService>();
            if (_dishService == null)
                _dishService = Core.Services.GetService<DishEntityService>();
            if (_gameState == null)
                _gameState = Core.Services.GetService<GameStateService>();
        }

        /// <summary>
        /// Injects service instances directly for headless tests (no running game instance).
        /// The live path resolves these through Core.Services in EnsureServices.
        /// </summary>
        public void SetHeadlessServices(CropStorageInventoryService cropStorage, GameStateService gameState)
        {
            _cropStorage = cropStorage;
            _gameState = gameState;
        }

        private bool HasAnyCropStorage()
        {
            if (_buildingService == null) return false;
            var all = _buildingService.GetAll();
            for (int i = 0; i < all.Count; i++)
                if (all[i].Type == BuildingType.CropStorage)
                    return true;
            return false;
        }

        private void HandleBuildingsChanged()
        {
            Pathfinder.RebuildWalls(_buildingService);
        }

        /// <summary>
        /// Called by KitchenMonsterStateMachine to mark that a plated dish entity has been
        /// created above the stove after cooking.
        /// </summary>
        public void OnDishPlated(KitchenTicket ticket, Entity dishEntity)
        {
            if (ticket == null) return;
            ticket.PlatedDishEntity = dishEntity;
            ticket.State = TicketState.Plated;
            if (ticket.StoveIndex >= 0 && ticket.StoveIndex < _cookClaim.Length)
                _cookClaim[ticket.StoveIndex] = null;
        }

        /// <summary>
        /// Cook claims a ticket to cook. Called by KitchenMonsterStateMachine.
        /// Rolls the deluxe flag using the cook's proficiency.
        /// </summary>
        public KitchenTicket BeginCooking(int stoveIndex, int cookProficiency)
        {
            var ticket = TryClaimCookTicket(stoveIndex);
            if (ticket == null) return null;
            ticket.IsDeluxe = Nez.Random.Chance(DishConfig.GetDeluxeChance(cookProficiency));
            return ticket;
        }

        /// <summary>
        /// Server claims a Plated ticket to deliver. Returns null when none exists.
        /// </summary>
        public KitchenTicket TryClaimDeliveryTicket()
        {
            for (int i = 0; i < _tickets.Count; i++)
            {
                var t = _tickets[i];
                if (t.State == TicketState.Plated)
                {
                    t.State = TicketState.Delivering;
                    return t;
                }
            }
            return null;
        }

        /// <summary>
        /// Marks the ticket Delivered and notifies party or patron.
        /// </summary>
        public void OnTicketDelivered(KitchenTicket ticket, Entity dishEntity)
        {
            if (ticket == null) return;
            ticket.State = TicketState.Delivered;
            ticket.PlatedDishEntity = dishEntity;

            if (ticket.IsPartyTicket)
            {
                NotifyPartyDishDelivered(ticket.PartySlot, ticket);
            }
            else if (ticket.PatronEntity != null)
            {
                var patron = ticket.PatronEntity.GetComponent<ECS.Components.TavernPatronComponent>();
                patron?.OnDishDelivered();
            }
        }

        /// <summary>
        /// Server abandons delivery mid-walk (ticket canceled). Returns the delivery ticket
        /// so the server can divert to the sink.
        /// </summary>
        public void AbortDelivery(KitchenTicket ticket)
        {
            if (ticket == null) return;
            if (ticket.State == TicketState.Delivering)
                ticket.State = TicketState.Canceled;
            _tickets.Remove(ticket);
        }

        /// <summary>
        /// Runner marks the ticket's ingredients as fetched and advances to ReadyToCook.
        /// </summary>
        public void OnIngredientsFetched(KitchenTicket ticket)
        {
            if (ticket == null) return;
            ticket.IngredientsFetched = true;
            if (ticket.State == TicketState.AwaitingIngredients)
                ticket.State = TicketState.ReadyToCook;
            if (ReferenceEquals(_runnerClaim, ticket))
                _runnerClaim = null;
        }

        /// <summary>Exposes the DishEntityService for use by the FSM when spawning dishes.</summary>
        public DishEntityService DishService => _dishService;
    }
}
