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
    /// Central coordinator for kitchen/tavern work. Owns the worker lifecycle (cooks, servers,
    /// runners), the ticket board, the fridge inventory, cooking stations, serving table slots,
    /// and patron notifications.
    ///
    /// Flow: a server takes up to 3 orders from patrons at its tables, posts them on the ticket
    /// board (82,2); the shortfall of any recipe not covered by the fridge (87,2) is fetched
    /// proactively by a runner from Crop Storage; a cook reads one ticket at a time from the
    /// board, gathers ingredients at the fridge (waiting for the runner if short), cooks at a
    /// free station (83-85,2), and places the dish on a serving table (87,3-5) — holding it if
    /// all three are full. The server whose zone owns the table picks up (up to 2 dishes) and
    /// delivers; dishes whose patron left go to the sink (86,2).
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
        }

        public struct BusJob
        {
            public Entity DishEntity;  // plate entity on the table to be bussed
            public Vector2 WorldPos;   // where to pick it up from
            public int TableTileY;     // zone filter (top tables y<=4, bottom y>=5)
            public float EnqueuedTime; // Time.TotalTime when queued — drives anti-starvation priority
        }

        private struct OrphanDish
        {
            public int Slot;          // serving slot the dish sits on
            public Entity DishEntity; // dish entity on the serving table (patron left)
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
        private float _hatCheckElapsed;

        // Scratch arrays for role assignment (pre-allocated, reset each reconcile)
        private readonly List<AlliedMonster> _wantedAssignments = new List<AlliedMonster>(8);
        private readonly List<KitchenRole> _wantedRoles = new List<KitchenRole>(8);
        private readonly List<bool> _matchedWorkerScratch = new List<bool>(8);

        // ── Pathfinder ──────────────────────────────────────────────────────────
        /// <summary>Shared A* grid for all kitchen monsters.</summary>
        public FarmPathfinder Pathfinder { get; }

        // ── Tickets / board ─────────────────────────────────────────────────────
        private readonly List<KitchenTicket> _tickets = new List<KitchenTicket>(16);
        private int _nextTicketId;

        // ── Fridge inventory (kitchen-local crop stock) ─────────────────────────
        private readonly Dictionary<CropType, int> _fridge = new Dictionary<CropType, int>(16);

        // ── Runner fetch queue (tickets whose storage-taken share needs transport) ──
        private readonly List<KitchenTicket> _fetchQueue = new List<KitchenTicket>(8);

        // ── Cooking stations ────────────────────────────────────────────────────
        private readonly KitchenTicket[] _stationTicket = new KitchenTicket[GameConfig.MaxKitchenCooks];

        // ── Serving table orphans (cooked dishes whose patron left) ─────────────
        private readonly List<OrphanDish> _orphanServing = new List<OrphanDish>(4);

        // ── Bus queue ───────────────────────────────────────────────────────────
        private readonly List<BusJob> _busJobs = new List<BusJob>(8);

        // ── Party order source ──────────────────────────────────────────────────
        private IPartyOrderSource _partyOrderSource;

        // ── Kitchen open/closed ─────────────────────────────────────────────────
        private int _cook1WorkerIdx = -1;
        private int _server1WorkerIdx = -1;

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
            EnsureServices();
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

            var roster = _alliedMonsters.AlliedMonsters;
            for (int i = 0; i < roster.Count; i++)
            {
                var m = roster[i];
                if (m.Job != MonsterJob.Cooking)
                    continue;
                if (MonsterScheduleConfig.IsAsleep(m.MonsterTypeName, timeService))
                    continue;

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

            // Assign roles in order: cook1, server1, runner1, cook2, server2, runner2, cook3.
            // Stations and zones are claimed dynamically by the FSMs.
            int postCount = _wantedAssignments.Count < 7 ? _wantedAssignments.Count : 7;
            for (int i = 0; i < postCount; i++)
            {
                KitchenRole role;
                switch (i)
                {
                    case 0: role = KitchenRole.Cook;   break;
                    case 1: role = KitchenRole.Server; break;
                    case 2: role = KitchenRole.Runner; break;
                    case 3: role = KitchenRole.Cook;   break;
                    case 4: role = KitchenRole.Server; break;
                    case 5: role = KitchenRole.Runner; break;
                    default: role = KitchenRole.Cook;  break;
                }
                _wantedRoles.Add(role);
            }

            // Track which pre-existing workers keep their assignment. SpawnWorker appends to
            // _workers mid-pass, so snapshot the count and never index past it.
            int existingWorkerCount = _workers.Count;
            _matchedWorkerScratch.Clear();
            for (int wi = 0; wi < existingWorkerCount; wi++)
                _matchedWorkerScratch.Add(false);

            for (int wi = 0; wi < existingWorkerCount; wi++)
            {
                var w = _workers[wi];
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
                    w.Fsm.RequestReturnHome();
                }
                else if (w.Role == _wantedRoles[wantedIdx])
                {
                    w.Fsm.CancelReturnHome();
                    _matchedWorkerScratch[wi] = true;
                }
                else
                {
                    // Role changed — send home; will be respawned next reconcile
                    w.Fsm.RequestReturnHome();
                }
            }

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
                    SpawnWorker(monster, _wantedRoles[j]);
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

            // Periodic hat sweep: shift overlaps can leave a worker hatless at spawn
            _hatCheckElapsed += Time.DeltaTime;
            if (_hatCheckElapsed >= GameConfig.KitchenHatCheckIntervalSeconds)
            {
                _hatCheckElapsed = 0f;
                for (int wi = 0; wi < _workers.Count; wi++)
                    _workers[wi].Fsm.EnsureHat();
            }
        }

        // ── Worker spawning ──────────────────────────────────────────────────────

        private void SpawnWorker(AlliedMonster monster, KitchenRole role)
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

            // Carry renderers: center dish/crop plus left/right side crops for runner hauls
            var carryRenderer = entity.AddComponent(new Nez.Sprites.SpriteRenderer());
            carryRenderer.SetRenderLayer(GameConfig.RenderLayerActorPropOverlay);
            carryRenderer.SetEnabled(false);
            var carryLeft = entity.AddComponent(new Nez.Sprites.SpriteRenderer());
            carryLeft.SetRenderLayer(GameConfig.RenderLayerActorPropOverlay);
            carryLeft.SetEnabled(false);
            var carryRight = entity.AddComponent(new Nez.Sprites.SpriteRenderer());
            carryRight.SetRenderLayer(GameConfig.RenderLayerActorPropOverlay);
            carryRight.SetEnabled(false);

            var fsm = entity.AddComponent(new KitchenMonsterStateMachine(
                monster, this, new Point(house.TileX, house.TileY), role));
            fsm.BodyAnimator = bodyAnimator;
            fsm.CarryRenderer = carryRenderer;
            fsm.CarryLeftRenderer = carryLeft;
            fsm.CarryRightRenderer = carryRight;

            var worker = new ActiveWorker
            {
                Monster = monster,
                Entity = entity,
                Fsm = fsm,
                Role = role,
            };
            _workers.Add(worker);

            Debug.Log($"[KitchenTaskCoordinator] Spawned {role} monster '{monster.Name}' ({typeName})");
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

        // ── Server zones ─────────────────────────────────────────────────────────

        /// <summary>
        /// The zone a server currently works: one active server works all 4 tables; with two,
        /// the first (staffing order) works the top tables and the second the bottom tables.
        /// Recomputed on demand, so zone handoffs on staffing changes are automatic — the
        /// current zone owner finishes whatever the previous owner started there.
        /// </summary>
        public ServerZone GetServerZone(KitchenMonsterStateMachine fsm)
        {
            int myOrder = -1;
            int activeServers = 0;
            for (int i = 0; i < _workers.Count; i++)
            {
                if (_workers[i].Role != KitchenRole.Server || _workers[i].Fsm.IsReturningHome)
                    continue;
                if (ReferenceEquals(_workers[i].Fsm, fsm))
                    myOrder = activeServers;
                activeServers++;
            }
            if (activeServers <= 1)
                return ServerZone.AllTables;
            return myOrder == 0 ? ServerZone.TopTables : ServerZone.BottomTables;
        }

        /// <summary>True when the zone covers the given table tile.</summary>
        public static bool ZoneContainsTable(ServerZone zone, Point tableTile)
        {
            switch (zone)
            {
                case ServerZone.TopTables:    return tableTile.Y <= GameConfig.TavernTopZoneMaxTileY;
                case ServerZone.BottomTables: return tableTile.Y >= GameConfig.TavernBottomZoneMinTileY;
                default:                      return true;
            }
        }

        // ── Fridge inventory ─────────────────────────────────────────────────────

        /// <summary>Units of the crop currently in the kitchen fridge.</summary>
        public int FridgeCount(CropType crop)
            => _fridge.TryGetValue(crop, out int n) ? n : 0;

        private void FridgeAdd(CropType crop, int amount)
        {
            if (amount <= 0) return;
            _fridge.TryGetValue(crop, out int n);
            _fridge[crop] = n + amount;
        }

        private int FridgeTake(CropType crop, int amount)
        {
            if (amount <= 0) return 0;
            _fridge.TryGetValue(crop, out int n);
            int take = n < amount ? n : amount;
            _fridge[crop] = n - take;
            return take;
        }

        // ── Ticket API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a ticket for an order taken at <paramref name="seatTile"/>, reserving the
        /// ingredients (fridge stock first, storage shortfall withdrawn all-or-nothing). If any
        /// shortfall was withdrawn from storage, the ticket enters the runner fetch queue
        /// immediately (proactive fetch — the runner starts as soon as the order is taken).
        /// The ticket is NOT visible to cooks until the server posts it at the board.
        /// Returns null if ingredients cannot be covered or the queue is full.
        /// </summary>
        public KitchenTicket CreateTicket(DishType dish, bool isParty, int partySlot,
            Entity patronEntity, Point seatTile)
        {
            if (_tickets.Count >= 16)
                return null;

            EnsureServices();
            var def = DishConfig.GetDefinition(dish);

            // All-or-nothing availability check (fridge + storage; dairy is free and not in Recipe)
            for (int i = 0; i < def.Recipe.Length; i++)
            {
                int available = FridgeCount(def.Recipe[i].Crop)
                    + (_cropStorage?.CountTotal(def.Recipe[i].Crop) ?? 0);
                if (available < def.Recipe[i].Qty)
                    return null;
            }

            var fridgeTaken = new int[def.Recipe.Length];
            var storageTaken = new int[def.Recipe.Length];
            int storageTotal = 0;

            for (int i = 0; i < def.Recipe.Length; i++)
            {
                var crop = def.Recipe[i].Crop;
                int need = def.Recipe[i].Qty;
                fridgeTaken[i] = FridgeTake(crop, need);
                int shortfall = need - fridgeTaken[i];
                if (shortfall > 0)
                {
                    if (!(_cropStorage?.TryWithdrawAcrossBuildings(crop, shortfall) ?? false))
                    {
                        // Availability changed mid-withdraw — roll everything back
                        for (int r = 0; r <= i; r++)
                        {
                            FridgeAdd(def.Recipe[r].Crop, fridgeTaken[r]);
                            if (storageTaken[r] > 0)
                                _cropStorage?.DepositAcrossBuildings(def.Recipe[r].Crop, storageTaken[r]);
                        }
                        return null;
                    }
                    storageTaken[i] = shortfall;
                    storageTotal += shortfall;
                }
            }

            var ticket = new KitchenTicket
            {
                TicketId = ++_nextTicketId,
                Dish = dish,
                IsPartyTicket = isParty,
                PartySlot = partySlot,
                PatronEntity = patronEntity,
                SeatTile = seatTile,
                TableTile = TavernSeatConfig.GetTableTile(seatTile),
                FridgeTakenQty = fridgeTaken,
                StorageTakenQty = storageTaken,
                IngredientsFetched = storageTotal == 0,
            };
            ticket.State = ticket.IngredientsFetched
                ? TicketState.ReadyToCook
                : TicketState.AwaitingIngredients;
            _tickets.Add(ticket);

            // Proactive runner: queue the transport as soon as the order exists
            if (!ticket.IngredientsFetched)
                _fetchQueue.Add(ticket);

            return ticket;
        }

        /// <summary>
        /// Creates a ticket WITHOUT reserving ingredients (save-reload path — crops were already
        /// deducted before the save). Enters the board immediately as ReadyToCook.
        /// </summary>
        public KitchenTicket CreateTicketPreReserved(DishType dish, int partySlot)
        {
            if (_tickets.Count >= 16)
                return null;

            var def = DishConfig.GetDefinition(dish);
            var storageTaken = new int[def.Recipe.Length];
            for (int i = 0; i < def.Recipe.Length; i++)
                storageTaken[i] = def.Recipe[i].Qty; // cancel refunds the full recipe to storage

            var seat = GetPartySeatTile(partySlot);
            var ticket = new KitchenTicket
            {
                TicketId = ++_nextTicketId,
                Dish = dish,
                IsPartyTicket = true,
                PartySlot = partySlot,
                SeatTile = seat,
                TableTile = TavernSeatConfig.GetTableTile(seat),
                FridgeTakenQty = new int[def.Recipe.Length],
                StorageTakenQty = storageTaken,
                IngredientsFetched = true,
                PostedToBoard = true,
                State = TicketState.ReadyToCook,
            };
            _tickets.Add(ticket);
            return ticket;
        }

        /// <summary>Server posts a taken order on the ticket board — cooks can now read it.</summary>
        public void PostTicket(KitchenTicket t)
        {
            if (t == null || t.State == TicketState.Canceled)
                return;
            t.PostedToBoard = true;
        }

        /// <summary>
        /// Cancels a ticket at any stage. Pre-cooking: refunds fridge-taken units to the fridge
        /// and storage-taken units to storage. After cooking started: the patron still pays.
        /// A plated dish becomes an orphan the zone server carries to the sink; a delivered dish
        /// becomes a bus job.
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
                {
                    if (t.FridgeTakenQty != null && t.FridgeTakenQty[i] > 0)
                        FridgeAdd(def.Recipe[i].Crop, t.FridgeTakenQty[i]);
                    if (t.StorageTakenQty != null && t.StorageTakenQty[i] > 0)
                        _cropStorage?.DepositAcrossBuildings(def.Recipe[i].Crop, t.StorageTakenQty[i]);
                }
            }
            else if (!t.IsPartyTicket)
            {
                // Patron left after cooking started (patience expired or hired mid-dining):
                // the ingredients are spent, the dish is made — payment is still collected (no tip)
                EnsureServices();
                _gameState?.AddFunds(DishConfig.GetPrice(t.Dish), "dish_sale");
            }

            // Dish sitting on a serving table → orphan for the servers to sink (the entity may
            // already be gone; the orphan entry still frees the slot once a server "collects" it)
            if (t.State == TicketState.Plated && t.ServingSlot >= 0)
            {
                var dishEntity = t.PlatedDishEntity != null && !t.PlatedDishEntity.IsDestroyed
                    ? t.PlatedDishEntity : null;
                _orphanServing.Add(new OrphanDish { Slot = t.ServingSlot, DishEntity = dishEntity });
                t.PlatedDishEntity = null;
            }
            // Dish on the patron's table → bus job
            else if (t.State == TicketState.Delivered
                && t.PlatedDishEntity != null && !t.PlatedDishEntity.IsDestroyed)
            {
                _busJobs.Add(new BusJob
                {
                    DishEntity = t.PlatedDishEntity,
                    WorldPos = t.PlatedDishEntity.Transform.Position,
                    TableTileY = t.TableTile.Y,
                    EnqueuedTime = Time.TotalTime,
                });
                t.PlatedDishEntity = null;
            }
            // Delivering / carried-by-cook: the carrying FSM sees Canceled and diverts to the sink.

            // Release claims
            if (t.StationIndex >= 0 && t.StationIndex < _stationTicket.Length
                && ReferenceEquals(_stationTicket[t.StationIndex], t))
            {
                _stationTicket[t.StationIndex] = null;
            }
            _fetchQueue.Remove(t);

            t.State = TicketState.Canceled;
            _tickets.Remove(t);
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

        /// <summary>True if fridge + storage can cover every recipe entry for the dish.</summary>
        public bool CanCoverRecipe(DishType dish)
        {
            EnsureServices();
            var def = DishConfig.GetDefinition(dish);
            for (int i = 0; i < def.Recipe.Length; i++)
            {
                int available = FridgeCount(def.Recipe[i].Crop)
                    + (_cropStorage?.CountTotal(def.Recipe[i].Crop) ?? 0);
                if (available < def.Recipe[i].Qty)
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

        /// <summary>Registers the party order source.</summary>
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
        /// EmptyPlate, and enqueues a bus job for the zone server.
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
                {
                    _busJobs.Add(new BusJob
                    {
                        DishEntity = emptyPlate,
                        WorldPos = platePos,
                        TableTileY = t.TableTile.Y,
                        EnqueuedTime = Time.TotalTime,
                    });
                }
            }

            _tickets.Remove(t);
        }

        /// <summary>Called when a party member finishes eating. Same as patron, different caller.</summary>
        public void NotifyPartyMemberFinishedEating(KitchenTicket t) => NotifyPatronFinishedEating(t);

        // ── Cook API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Cook reads the next unclaimed posted ticket from the board (party tickets first, then
        /// FIFO). Only one cook holds any given ticket. Returns null when the board is empty.
        /// </summary>
        public KitchenTicket TryReadNextTicket()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                bool wantParty = pass == 0;
                for (int i = 0; i < _tickets.Count; i++)
                {
                    var t = _tickets[i];
                    if (t.IsPartyTicket != wantParty)
                        continue;
                    if (!t.PostedToBoard || t.CookClaimed || t.State == TicketState.Canceled)
                        continue;
                    if (t.State != TicketState.AwaitingIngredients && t.State != TicketState.ReadyToCook)
                        continue;
                    t.CookClaimed = true;
                    return t;
                }
            }
            return null;
        }

        /// <summary>
        /// Claims the first free cooking station for the ticket. Stations are free when no other
        /// cook is using them. Always succeeds while cooks ≤ stations; returns false otherwise.
        /// </summary>
        public bool TryClaimStation(KitchenTicket t, out int station)
        {
            for (int i = 0; i < _stationTicket.Length; i++)
            {
                if (_stationTicket[i] == null)
                {
                    _stationTicket[i] = t;
                    t.StationIndex = i;
                    station = i;
                    return true;
                }
            }
            station = -1;
            return false;
        }

        /// <summary>
        /// Cook abandons its claimed ticket (shift end / interruption). The ticket returns to the
        /// board for the next cook; a mid-cook abandon resets it to ReadyToCook.
        /// </summary>
        public void ReleaseCookTicket(KitchenTicket t)
        {
            if (t == null) return;
            t.CookClaimed = false;
            if (t.State == TicketState.Cooking)
                t.State = TicketState.ReadyToCook;
            if (t.StationIndex >= 0 && t.StationIndex < _stationTicket.Length
                && ReferenceEquals(_stationTicket[t.StationIndex], t))
            {
                _stationTicket[t.StationIndex] = null;
            }
            t.StationIndex = -1;
            t.ServingSlot = -1;
        }

        /// <summary>Cook starts cooking at its station: rolls deluxe, locks the reservation.</summary>
        public void BeginCookingAtStation(KitchenTicket t, int cookProficiency)
        {
            if (t == null) return;
            t.State = TicketState.Cooking;
            t.CropsRefundable = false;
            t.IsDeluxe = Nez.Random.Chance(DishConfig.GetDeluxeChance(cookProficiency));
        }

        /// <summary>Cook finished cooking: frees the station (the cook now holds the dish).</summary>
        public void FinishCooking(KitchenTicket t)
        {
            if (t == null) return;
            if (t.StationIndex >= 0 && t.StationIndex < _stationTicket.Length
                && ReferenceEquals(_stationTicket[t.StationIndex], t))
            {
                _stationTicket[t.StationIndex] = null;
            }
            t.StationIndex = -1;
        }

        /// <summary>
        /// Reserves a free serving table slot for the ticket. A slot is occupied while any
        /// ticket or orphaned dish sits on (or is headed to) it.
        /// </summary>
        public bool TryReserveServingSlot(KitchenTicket t, out int slot)
        {
            for (int i = 0; i < GameConfig.KitchenServingSlotCount; i++)
            {
                if (!IsServingSlotOccupied(i))
                {
                    t.ServingSlot = i;
                    slot = i;
                    return true;
                }
            }
            slot = -1;
            return false;
        }

        /// <summary>
        /// Last-resort placement when a cook must go home while every slot is full: reuse the
        /// least-loaded slot. Pickups scan tickets, not slots, so this self-heals.
        /// </summary>
        public int ForceReserveServingSlot(KitchenTicket t)
        {
            t.ServingSlot = 0;
            return 0;
        }

        private bool IsServingSlotOccupied(int slot)
        {
            for (int i = 0; i < _tickets.Count; i++)
                if (_tickets[i].ServingSlot == slot)
                    return true;
            for (int i = 0; i < _orphanServing.Count; i++)
                if (_orphanServing[i].Slot == slot)
                    return true;
            return false;
        }

        /// <summary>Cook placed the dish entity on its reserved serving slot.</summary>
        public void PlaceDishOnServing(KitchenTicket t, Entity dishEntity)
        {
            if (t == null) return;
            t.PlatedDishEntity = dishEntity;
            t.State = TicketState.Plated;
        }

        // ── Server API ───────────────────────────────────────────────────────────

        /// <summary>True when a plated dish (or orphan) is waiting that this zone's server should handle.</summary>
        public bool HasReadyDishForZone(ServerZone zone)
        {
            if (_orphanServing.Count > 0)
                return true;
            for (int i = 0; i < _tickets.Count; i++)
            {
                var t = _tickets[i];
                if (t.State == TicketState.Plated && ZoneContainsTable(zone, t.TableTile))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Server picks up one item from the serving tables: orphaned dishes first (to the sink),
        /// then plated dishes for tables in the server's zone. The dish entity is despawned —
        /// the server is now carrying it. Returns false when nothing is available.
        /// </summary>
        public bool TryPickupReadyDish(ServerZone zone, out KitchenTicket ticket,
            out DishType dish, out bool toSink)
        {
            if (_orphanServing.Count > 0)
            {
                var orphan = _orphanServing[0];
                _orphanServing.RemoveAt(0);
                if (orphan.DishEntity != null && !orphan.DishEntity.IsDestroyed)
                    _dishService?.Despawn(orphan.DishEntity);
                ticket = null;
                dish = default;
                toSink = true;
                return true;
            }

            for (int i = 0; i < _tickets.Count; i++)
            {
                var t = _tickets[i];
                if (t.State != TicketState.Plated || !ZoneContainsTable(zone, t.TableTile))
                    continue;
                if (t.PlatedDishEntity != null && !t.PlatedDishEntity.IsDestroyed)
                    _dishService?.Despawn(t.PlatedDishEntity);
                t.PlatedDishEntity = null;
                t.ServingSlot = -1;
                t.State = TicketState.Delivering;
                ticket = t;
                dish = t.Dish;
                toSink = false;
                return true;
            }

            ticket = null;
            dish = default;
            toSink = false;
            return false;
        }

        /// <summary>Marks the ticket Delivered and notifies party or patron.</summary>
        public void OnTicketDelivered(KitchenTicket ticket, Entity dishEntity)
        {
            if (ticket == null) return;
            ticket.State = TicketState.Delivered;
            ticket.PlatedDishEntity = dishEntity;

            if (ticket.IsPartyTicket)
            {
                NotifyPartyDishDelivered(ticket.PartySlot, ticket);
            }
            else if (ticket.PatronEntity != null && !ticket.PatronEntity.IsDestroyed)
            {
                var patron = ticket.PatronEntity.GetComponent<ECS.Components.TavernPatronComponent>();
                patron?.OnDishDelivered();
            }
        }

        /// <summary>Next pending bus job for the zone's server. Removes it from the queue.</summary>
        public bool TryClaimBusJob(ServerZone zone, out BusJob job)
            => TryClaimBusJob(zone, 0f, out job);

        /// <summary>
        /// Claims the oldest bus job in the zone that has waited at least minAgeSeconds
        /// (0 = any). Removes it from the queue. The age gate lets servers bump long-waiting
        /// plates ahead of order-taking so a busy tavern can't starve bussing forever.
        /// </summary>
        public bool TryClaimBusJob(ServerZone zone, float minAgeSeconds, out BusJob job)
        {
            int oldest = -1;
            float oldestTime = float.MaxValue;
            for (int i = 0; i < _busJobs.Count; i++)
            {
                var tableTile = new Point(0, _busJobs[i].TableTileY);
                if (!ZoneContainsTable(zone, tableTile))
                    continue;
                if (Time.TotalTime - _busJobs[i].EnqueuedTime < minAgeSeconds)
                    continue;
                if (_busJobs[i].EnqueuedTime < oldestTime)
                {
                    oldestTime = _busJobs[i].EnqueuedTime;
                    oldest = i;
                }
            }
            if (oldest < 0)
            {
                job = default;
                return false;
            }
            job = _busJobs[oldest];
            _busJobs.RemoveAt(oldest);
            return true;
        }

        // ── Runner API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Runner claims the next transport job (FIFO). The claimed ticket leaves the queue so
        /// two runners never fetch the same ingredients. Returns null when nothing is queued.
        /// </summary>
        public KitchenTicket TryClaimFetchJob()
        {
            while (_fetchQueue.Count > 0)
            {
                var t = _fetchQueue[0];
                _fetchQueue.RemoveAt(0);
                if (t.State == TicketState.Canceled || t.IngredientsFetched)
                    continue; // stale entry
                return t;
            }
            return null;
        }

        /// <summary>Runner abandons a claimed fetch (shift end) — the job re-enters the queue.</summary>
        public void ReleaseFetchJob(KitchenTicket t)
        {
            if (t == null || t.State == TicketState.Canceled || t.IngredientsFetched)
                return;
            if (!_fetchQueue.Contains(t))
                _fetchQueue.Add(t);
        }

        /// <summary>
        /// Runner is at the storage door: opportunistically tops the fridge up to par for each
        /// crop in the ticket's recipe (withdrawn atomically into the fridge — the walk back is
        /// cosmetic, so a crash never loses crops).
        /// </summary>
        public void RunnerCollectAtStorage(KitchenTicket t)
        {
            if (t == null) return;
            EnsureServices();
            if (_cropStorage == null) return;

            var def = DishConfig.GetDefinition(t.Dish);
            for (int i = 0; i < def.Recipe.Length; i++)
            {
                var crop = def.Recipe[i].Crop;
                int want = GameConfig.KitchenFridgeParPerCrop - FridgeCount(crop);
                if (want <= 0)
                    continue;
                int available = _cropStorage.CountTotal(crop);
                int take = want < available ? want : available;
                if (take > 0 && _cropStorage.TryWithdrawAcrossBuildings(crop, take))
                    FridgeAdd(crop, take);
            }
        }

        /// <summary>
        /// Runner arrived at the fridge: the ticket's ingredients are now complete and the
        /// ticket becomes cookable (if posted, a cook can start immediately).
        /// </summary>
        public void CompleteFetch(KitchenTicket t)
        {
            if (t == null || t.State == TicketState.Canceled)
                return;
            t.IngredientsFetched = true;
            if (t.State == TicketState.AwaitingIngredients)
                t.State = TicketState.ReadyToCook;
        }

        // ── Static tile helpers ──────────────────────────────────────────────────

        /// <summary>Ticket board tile (servers post, cooks read).</summary>
        public static Point TicketBoardTile
            => new Point(GameConfig.KitchenTicketBoardTileX, GameConfig.KitchenTicketBoardTileY);

        /// <summary>Fridge tile (cooks gather here; runners restock it).</summary>
        public static Point FridgeTile
            => new Point(GameConfig.KitchenFridgeTileX, GameConfig.KitchenFridgeTileY);

        /// <summary>A tile inside the runners' wander area (kitchen south corridor).</summary>
        public static Point RunnerWanderAnchorTile
            => new Point(GameConfig.KitchenRunnerWanderMinTileX, GameConfig.KitchenRunnerWanderMinTileY + 1);

        /// <summary>Sink tile (dirty plates and orphaned dishes go here).</summary>
        public static Point SinkTile => new Point(GameConfig.KitchenSinkTileX, GameConfig.KitchenSinkTileY);

        /// <summary>World center of the sink tile.</summary>
        public static Vector2 SinkWorldPos => new Vector2(
            GameConfig.KitchenSinkTileX * GameConfig.TileSize + GameConfig.TileSize / 2f,
            GameConfig.KitchenSinkTileY * GameConfig.TileSize + GameConfig.TileSize / 2f);

        /// <summary>Returns the cooking station tile for the given station index (cook stands here).</summary>
        public static Point GetStationTile(int stationIndex)
        {
            int x;
            switch (stationIndex)
            {
                case 0: x = GameConfig.KitchenStove1TileX; break;
                case 1: x = GameConfig.KitchenStove2TileX; break;
                default: x = GameConfig.KitchenStove3TileX; break;
            }
            return new Point(x, GameConfig.KitchenStoveTileY);
        }

        /// <summary>Returns the serving table tile for the given slot index.</summary>
        public static Point GetServingTile(int slot)
            => new Point(GameConfig.KitchenServingTableTileX, GameConfig.KitchenServingTableFirstTileY + slot);

        /// <summary>
        /// Tile a worker stands on to place/take a dish at the given serving slot — one tile
        /// left of the table so they work beside it instead of on top of it.
        /// </summary>
        public static Point GetServingApproachTile(int slot)
            => new Point(GameConfig.KitchenServingTableTileX - 1, GameConfig.KitchenServingTableFirstTileY + slot);

        /// <summary>Seat tile for a party slot (0 = hero, 1/2 = hired mercs).</summary>
        public static Point GetPartySeatTile(int partySlot)
        {
            switch (partySlot)
            {
                case 0:  return new Point(GameConfig.TavernHeroSeatTileX, GameConfig.TavernHeroSeatTileY);
                case 1:  return new Point(GameConfig.TavernMercenary1SeatTileX, GameConfig.TavernMercenary1SeatTileY);
                case 2:  return new Point(GameConfig.TavernMercenary2SeatTileX, GameConfig.TavernMercenary2SeatTileY);
                default: return new Point(GameConfig.TavernHeroSeatTileX, GameConfig.TavernHeroSeatTileY);
            }
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

        // ── Party order source pass-through ──────────────────────────────────────

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

        private void HandleBuildingsChanged()
        {
            Pathfinder.RebuildWalls(_buildingService);
        }

        /// <summary>Exposes the DishEntityService for use by the FSM when spawning dishes.</summary>
        public DishEntityService DishService => _dishService;
    }
}
