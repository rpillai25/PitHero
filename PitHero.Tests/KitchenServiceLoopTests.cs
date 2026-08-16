using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez;
using PitHero;
using PitHero.Dining;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.Tests
{
    /// <summary>
    /// Headless end-to-end verification of the reworked tavern service loop through the REAL
    /// coordinator and patron component (no Nez scene):
    /// server takes order → posts at the ticket board → runner hauls the shortfall to the
    /// fridge → cook reads the board, claims a station, cooks → dish placed on a serving
    /// table → the zone server picks it up and delivers → patron eats → pays → ticket retired.
    /// Plus the early-leave cases at every stage and the zone/board/fridge invariants.
    /// Worker WALKING is live-only; the walk routes are covered by KitchenFlowPathTests.
    /// </summary>
    [TestClass]
    public class KitchenServiceLoopTests
    {
        private const int StorageBuildingId = 2;
        private static readonly DishType Dish = DishType.RoastedOnionSkewers;
        // Seat (96,7) belongs to the bottom-right table (97,7) — a BOTTOM zone table
        private static readonly Point PatronSeat = new Point(96, 7);

        private BuildingService _buildings;
        private CropStorageInventoryService _storage;
        private GameStateService _gameState;
        private KitchenTaskCoordinator _coordinator;

        [TestInitialize]
        public void Setup()
        {
            _buildings = new BuildingService();
            _buildings.AddBuilding(new PlacedBuilding
            {
                Type = BuildingType.CropStorage,
                TileX = GameConfig.NewGameCropStorageAnchorTileX,
                TileY = GameConfig.NewGameCropStorageAnchorTileY,
                UniqueId = StorageBuildingId
            });
            _storage = new CropStorageInventoryService(_buildings);
            _gameState = new GameStateService();
            _coordinator = new KitchenTaskCoordinator(null, _buildings, 240, 12);
            _coordinator.SetHeadlessServices(_storage, _gameState);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Time.DeltaTime = 0f;
            Time.TotalTime = 0f;
        }

        /// <summary>Deposits exactly enough crops in storage to cover N servings of the dish.</summary>
        private void StockRecipe(int servings = 1)
        {
            var def = DishConfig.GetDefinition(Dish);
            for (int i = 0; i < def.Recipe.Length; i++)
                Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, def.Recipe[i].Crop, def.Recipe[i].Qty * servings));
        }

        private int TotalStorageUnits()
        {
            var def = DishConfig.GetDefinition(Dish);
            int total = 0;
            for (int i = 0; i < def.Recipe.Length; i++)
                total += _storage.CountTotal(def.Recipe[i].Crop);
            return total;
        }

        private int RecipeUnitCount()
        {
            var def = DishConfig.GetDefinition(Dish);
            int total = 0;
            for (int i = 0; i < def.Recipe.Length; i++)
                total += def.Recipe[i].Qty;
            return total;
        }

        private Entity CreatePatron(out TavernPatronComponent patron)
        {
            var entity = new Entity("test-patron");
            patron = new TavernPatronComponent { SeatTile = PatronSeat };
            patron.SetHeadlessServices(_coordinator, _gameState);
            entity.AddComponent(patron);
            return entity;
        }

        private static void Tick(TavernPatronComponent patron, float seconds)
        {
            Time.DeltaTime = seconds;
            patron.Update();
        }

        /// <summary>Runs the runner leg for a ticket (claim → collect at storage → unload at fridge).</summary>
        private void RunRunnerLeg(KitchenTicket expected)
        {
            var job = _coordinator.TryClaimFetchJob();
            Assert.AreSame(expected, job, "runner did not claim the queued fetch job");
            var carried = new int[Farming.CropTypeInfo.Count];
            _coordinator.RunnerCollectAtStorage(job, carried);
            _coordinator.DeliverCarriedTopUp(carried);
            _coordinator.CompleteFetch(job);
        }

        [TestMethod]
        public void FullServiceLoop_OrderPostFetchCookServeEatPay()
        {
            StockRecipe();
            var entity = CreatePatron(out var patron);

            // ── Server takes the order at the seat ──
            var ticket = _coordinator.CreateTicket(Dish, false, -1, entity, PatronSeat);
            Assert.IsNotNull(ticket, "ticket refused despite stocked recipe");
            patron.OnOrderTaken(ticket);
            Assert.AreEqual(new Point(97, 7), ticket.TableTile, "seat (96,7) must map to table (97,7)");
            Assert.AreEqual(0, TotalStorageUnits(), "ingredients not reserved at order time");
            Assert.AreEqual(TicketState.AwaitingIngredients, ticket.State, "fridge was empty — runner trip expected");

            // ── Board gating: cooks can't see unposted orders ──
            Assert.IsNull(_coordinator.TryReadNextTicket(), "cook read a ticket that was never posted");
            _coordinator.PostTicket(ticket);

            // ── Cook reads the board — only ONE cook can hold a given ticket ──
            var read = _coordinator.TryReadNextTicket();
            Assert.AreSame(ticket, read);
            Assert.IsNull(_coordinator.TryReadNextTicket(), "second cook read the same ticket");

            // ── Runner hauls the shortfall (proactively queued at order time) ──
            RunRunnerLeg(ticket);
            Assert.AreEqual(TicketState.ReadyToCook, ticket.State);
            Assert.IsNull(_coordinator.TryClaimFetchJob(), "fetch job claimable twice");

            // ── Cook: station claim → cook → serving table ──
            Assert.IsTrue(_coordinator.TryClaimStation(ticket, out int station));
            Assert.AreEqual(0, station);
            _coordinator.BeginCookingAtStation(ticket, cookProficiency: 5);
            Assert.AreEqual(TicketState.Cooking, ticket.State);
            Assert.IsFalse(ticket.CropsRefundable, "ingredients must be non-refundable once cooking starts");

            _coordinator.FinishCooking(ticket);
            Assert.IsTrue(_coordinator.TryReserveServingSlot(ticket, out int slot));
            _coordinator.PlaceDishOnServing(ticket, null);
            Assert.AreEqual(TicketState.Plated, ticket.State);

            // ── Zone ownership: the top-tables server must NOT touch this bottom-table dish ──
            Assert.IsFalse(_coordinator.HasReadyDishForZone(ServerZone.TopTables),
                "top server sees a bottom-table dish");
            Assert.IsFalse(_coordinator.TryPickupReadyDish(ServerZone.TopTables, out _, out _, out _),
                "top server picked up a bottom-table dish");

            // ── The bottom server (its zone owns table (97,7)) delivers ──
            Assert.IsTrue(_coordinator.TryPickupReadyDish(ServerZone.BottomTables, out var picked, out var dish, out bool toSink));
            Assert.AreSame(ticket, picked);
            Assert.AreEqual(Dish, dish);
            Assert.IsFalse(toSink);
            Assert.AreEqual(TicketState.Delivering, ticket.State);

            _coordinator.OnTicketDelivered(ticket, null);
            Assert.AreEqual(TicketState.Delivered, ticket.State);
            Assert.AreEqual(PatronState.FoodDelivered, patron.State, "delivery did not notify the patron");

            // ── Patron eats and pays ──
            Tick(patron, 0.01f);
            Assert.AreEqual(PatronState.Eating, patron.State);
            Tick(patron, DishConfig.GetEatSeconds(Dish) + 0.1f);
            Assert.AreEqual(PatronState.FinishedEating, patron.State);

            int price = DishConfig.GetPrice(Dish);
            int maxTip = (int)System.Math.Ceiling(price * GameConfig.DishTipMaxPercent);
            Assert.IsTrue(_gameState.Funds >= price && _gameState.Funds <= price + maxTip,
                $"funds {_gameState.Funds} outside [{price}, {price + maxTip}]");

            // ── Everything retired: board empty, no dishes waiting, station reusable ──
            Assert.IsNull(_coordinator.TryReadNextTicket());
            Assert.IsFalse(_coordinator.HasReadyDishForZone(ServerZone.AllTables));
            StockRecipe();
            var next = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
            Assert.IsTrue(_coordinator.TryClaimStation(next, out int station2));
            Assert.AreEqual(0, station2, "station 0 was not released after the first order");
        }

        [TestMethod]
        public void FridgePreStock_SecondOrderSkipsRunnerTrip()
        {
            var def = DishConfig.GetDefinition(Dish);
            // Max carry level: the trip top-up is hand-carried, so only level 3 (10 units per
            // crop) can fill a whole fridge stack in one run
            _gameState.RunnerCarryLevel = 3;
            // Stock plenty: the runner's pre-stock top-up should leave fridge stock for the next order
            StockRecipe(servings: 4);

            var t1 = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
            _coordinator.PostTicket(t1);
            Assert.AreEqual(TicketState.AwaitingIngredients, t1.State, "fridge starts empty");
            RunRunnerLeg(t1);

            // Pre-stock top-up filled the fridge from remaining storage toward the target
            // (default 1 stack of KitchenFridgeStackSize units per crop)
            int target = GameConfig.KitchenFridgeStackSize;
            for (int i = 0; i < def.Recipe.Length; i++)
            {
                int expected = System.Math.Min(target, def.Recipe[i].Qty * 3);
                Assert.AreEqual(expected, _coordinator.FridgeCount(def.Recipe[i].Crop),
                    $"fridge pre-stock top-up wrong for {def.Recipe[i].Crop}");
            }

            // If the target covers the recipe, the second order needs no runner trip
            bool targetCoversRecipe = true;
            for (int i = 0; i < def.Recipe.Length; i++)
                if (def.Recipe[i].Qty > target)
                    targetCoversRecipe = false;

            var t2 = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
            Assert.IsNotNull(t2);
            if (targetCoversRecipe)
            {
                Assert.IsTrue(t2.IngredientsFetched, "fridge stock should cover the second order");
                Assert.AreEqual(TicketState.ReadyToCook, t2.State);
                Assert.IsNull(_coordinator.TryClaimFetchJob(), "runner dispatched despite full fridge");
            }
        }

        [TestMethod]
        public void FridgeTopUp_AtCarryLevelOne_MovesOneUnitPerCropPerTrip()
        {
            var def = DishConfig.GetDefinition(Dish);
            StockRecipe(servings: 4);

            var t1 = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
            _coordinator.PostTicket(t1);
            RunRunnerLeg(t1);

            // Default carry level 1: the runner's hands hold one unit of each recipe crop, so a
            // single trip barely dents the pre-stock target — constant storage runs are expected
            for (int i = 0; i < def.Recipe.Length; i++)
                Assert.AreEqual(1, _coordinator.FridgeCount(def.Recipe[i].Crop),
                    $"carry level 1 must top up exactly one unit of {def.Recipe[i].Crop}");
        }

        [TestMethod]
        public void PatronPatienceExpires_BeforeOrdering_NothingChargedNothingLost()
        {
            StockRecipe();
            CreatePatron(out var patron);

            Tick(patron, GameConfig.PatronPatiencePreOrderSeconds + 1f);

            Assert.AreEqual(PatronState.FinishedEating, patron.State, "patron should give up and leave");
            Assert.AreEqual(0, _gameState.Funds);
            Assert.AreEqual(RecipeUnitCount(), TotalStorageUnits(), "storage must be untouched");
        }

        [TestMethod]
        public void PatronLeaves_AfterOrdering_BeforeCooking_IngredientsRefunded()
        {
            StockRecipe();
            var entity = CreatePatron(out var patron);

            var ticket = _coordinator.CreateTicket(Dish, false, -1, entity, PatronSeat);
            patron.OnOrderTaken(ticket);
            _coordinator.PostTicket(ticket);
            Assert.AreEqual(0, TotalStorageUnits());

            Tick(patron, GameConfig.PatronPatiencePostOrderSeconds + 1f);

            Assert.AreEqual(PatronState.FinishedEating, patron.State);
            Assert.AreEqual(TicketState.Canceled, ticket.State);
            Assert.AreEqual(0, _gameState.Funds, "uncooked order must not be charged");
            Assert.AreEqual(RecipeUnitCount(), TotalStorageUnits(), "storage-taken units must be refunded to storage");
            Assert.IsNull(_coordinator.TryClaimFetchJob(), "canceled ticket still in the fetch queue");
            Assert.IsNull(_coordinator.TryReadNextTicket(), "canceled ticket still on the board");
        }

        [TestMethod]
        public void PatronLeaves_AfterCookingStarted_PaymentStandsNoRefund()
        {
            StockRecipe();
            var entity = CreatePatron(out var patron);

            var ticket = _coordinator.CreateTicket(Dish, false, -1, entity, PatronSeat);
            patron.OnOrderTaken(ticket);
            _coordinator.PostTicket(ticket);
            RunRunnerLeg(ticket);
            Assert.IsTrue(_coordinator.TryClaimStation(ticket, out _));
            _coordinator.BeginCookingAtStation(ticket, 5);

            Tick(patron, GameConfig.PatronPatiencePostOrderSeconds + 1f);

            Assert.AreEqual(PatronState.FinishedEating, patron.State);
            Assert.AreEqual(0, TotalStorageUnits(), "spent ingredients must NOT be refunded after cooking started");
            Assert.AreEqual(DishConfig.GetPrice(Dish), _gameState.Funds,
                "cooked-but-abandoned dish must still be paid for (no tip)");

            // Station released so the kitchen isn't wedged
            StockRecipe();
            var next = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
            Assert.IsTrue(_coordinator.TryClaimStation(next, out int st));
            Assert.AreEqual(0, st, "station claim leaked after mid-cook cancel");
        }

        [TestMethod]
        public void PatronLeaves_WhileDishOnServingTable_ServerSinksIt()
        {
            StockRecipe();
            var entity = CreatePatron(out var patron);

            var ticket = _coordinator.CreateTicket(Dish, false, -1, entity, PatronSeat);
            patron.OnOrderTaken(ticket);
            _coordinator.PostTicket(ticket);
            RunRunnerLeg(ticket);
            Assert.IsTrue(_coordinator.TryClaimStation(ticket, out _));
            _coordinator.BeginCookingAtStation(ticket, 5);
            _coordinator.FinishCooking(ticket);
            Assert.IsTrue(_coordinator.TryReserveServingSlot(ticket, out int slot));
            _coordinator.PlaceDishOnServing(ticket, null);

            // Patron hired away while the dish waits on the serving table
            _coordinator.CancelTicketForPatron(entity);

            Assert.AreEqual(TicketState.Canceled, ticket.State);
            Assert.AreEqual(DishConfig.GetPrice(Dish), _gameState.Funds, "cooked dish still paid for");

            // Any server takes the orphan to the sink; the slot frees afterwards
            Assert.IsTrue(_coordinator.HasReadyDishForZone(ServerZone.TopTables),
                "orphaned dish invisible to servers");
            Assert.IsTrue(_coordinator.TryPickupReadyDish(ServerZone.TopTables, out var picked, out _, out bool toSink));
            Assert.IsNull(picked, "orphan pickup should have no ticket");
            Assert.IsTrue(toSink, "orphaned dish must go to the sink");

            // Slot is free again for the next cook
            StockRecipe();
            var next = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
            Assert.IsTrue(_coordinator.TryReserveServingSlot(next, out int slot2));
            Assert.AreEqual(slot, slot2, "serving slot leaked after orphan pickup");
        }

        [TestMethod]
        public void PartyTicket_ReadFromBoardBeforeEarlierPatronTicket()
        {
            StockRecipe(servings: 2);

            var patronTicket = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
            var partySeat = KitchenTaskCoordinator.GetPartySeatTile(0);
            var partyTicket = _coordinator.CreateTicket(Dish, true, 0, null, partySeat);
            _coordinator.PostTicket(patronTicket);
            _coordinator.PostTicket(partyTicket);

            Assert.AreSame(partyTicket, _coordinator.TryReadNextTicket(), "party orders must cook first");
            Assert.AreSame(patronTicket, _coordinator.TryReadNextTicket());
            Assert.AreEqual(new Point(93, 7), partyTicket.TableTile, "party table must be the bottom-left table");
        }

        [TestMethod]
        public void CookInterruptedMidCook_TicketBackOnBoardAndReclaimable()
        {
            StockRecipe();
            var ticket = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
            _coordinator.PostTicket(ticket);
            RunRunnerLeg(ticket);

            Assert.AreSame(ticket, _coordinator.TryReadNextTicket());
            Assert.IsTrue(_coordinator.TryClaimStation(ticket, out _));
            _coordinator.BeginCookingAtStation(ticket, 5);

            // Shift boundary: cook walks home mid-cook
            _coordinator.ReleaseCookTicket(ticket);
            Assert.AreEqual(TicketState.ReadyToCook, ticket.State);
            Assert.AreEqual(-1, ticket.StationIndex);
            Assert.IsFalse(ticket.CookClaimed);

            // The next cook reads it off the board and claims a station again
            Assert.AreSame(ticket, _coordinator.TryReadNextTicket());
            Assert.IsTrue(_coordinator.TryClaimStation(ticket, out int st2));
            Assert.AreEqual(0, st2);
        }

        [TestMethod]
        public void RunnerInterrupted_FetchJobRequeued()
        {
            StockRecipe();
            var ticket = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);

            var job = _coordinator.TryClaimFetchJob();
            Assert.AreSame(ticket, job);

            // Runner goes home before reaching storage — job returns to the queue
            _coordinator.ReleaseFetchJob(job);
            Assert.AreSame(ticket, _coordinator.TryClaimFetchJob(), "released fetch job not reclaimable");
        }

        [TestMethod]
        public void ServingSlots_AllThreeFillThenBlock()
        {
            StockRecipe(servings: 4);

            var tickets = new KitchenTicket[4];
            for (int i = 0; i < 4; i++)
            {
                tickets[i] = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
                Assert.IsNotNull(tickets[i]);
            }

            // Three dishes reserve the three serving slots; the fourth cook must hold
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(_coordinator.TryReserveServingSlot(tickets[i], out int slot));
                Assert.AreEqual(i, slot);
                _coordinator.PlaceDishOnServing(tickets[i], null);
            }
            Assert.IsFalse(_coordinator.TryReserveServingSlot(tickets[3], out _),
                "fourth dish reserved a slot while all three are occupied");

            // A server picks one up → the slot frees for the waiting cook
            Assert.IsTrue(_coordinator.TryPickupReadyDish(ServerZone.AllTables, out _, out _, out _));
            Assert.IsTrue(_coordinator.TryReserveServingSlot(tickets[3], out int freed));
            Assert.AreEqual(0, freed);
        }

        [TestMethod]
        public void InsufficientStock_OrderRefused_NothingWithdrawn()
        {
            var ticket = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
            Assert.IsNull(ticket);
            Assert.AreEqual(0, TotalStorageUnits());
        }

        [TestMethod]
        public void ZoneRules_TopAndBottomTablesSplitCorrectly()
        {
            // Tables: (93,3)/(97,3) top, (93,7)/(97,7) bottom
            Assert.IsTrue(KitchenTaskCoordinator.ZoneContainsTable(ServerZone.TopTables, new Point(93, 3)));
            Assert.IsTrue(KitchenTaskCoordinator.ZoneContainsTable(ServerZone.TopTables, new Point(97, 3)));
            Assert.IsFalse(KitchenTaskCoordinator.ZoneContainsTable(ServerZone.TopTables, new Point(93, 7)));
            Assert.IsFalse(KitchenTaskCoordinator.ZoneContainsTable(ServerZone.TopTables, new Point(97, 7)));

            Assert.IsFalse(KitchenTaskCoordinator.ZoneContainsTable(ServerZone.BottomTables, new Point(93, 3)));
            Assert.IsTrue(KitchenTaskCoordinator.ZoneContainsTable(ServerZone.BottomTables, new Point(93, 7)));

            Assert.IsTrue(KitchenTaskCoordinator.ZoneContainsTable(ServerZone.AllTables, new Point(93, 3)));
            Assert.IsTrue(KitchenTaskCoordinator.ZoneContainsTable(ServerZone.AllTables, new Point(97, 7)));
        }

        // ── Role mix (issue #327: runners bus plates, so the third one is worth staffing) ──

        private static System.Collections.Generic.List<KitchenRole> RoleMix(int postCount)
        {
            var roles = new System.Collections.Generic.List<KitchenRole>();
            KitchenTaskCoordinator.FillRoleMix(postCount, roles);
            return roles;
        }

        [TestMethod]
        public void RoleMix_GrowsCookServerRunnerAndEndsWithAThirdRunner()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    KitchenRole.Cook, KitchenRole.Server, KitchenRole.Runner,
                    KitchenRole.Cook, KitchenRole.Server, KitchenRole.Runner,
                    KitchenRole.Cook, KitchenRole.Runner,
                },
                RoleMix(KitchenTaskCoordinator.MaxWorkerPosts));
        }

        [TestMethod]
        public void RoleMix_FirstTwoPostsOpenTheKitchen()
        {
            // IsKitchenOpen needs a cook AND a server, so those must be posts 0 and 1
            CollectionAssert.AreEqual(new[] { KitchenRole.Cook }, RoleMix(1));
            CollectionAssert.AreEqual(new[] { KitchenRole.Cook, KitchenRole.Server }, RoleMix(2));
            CollectionAssert.AreEqual(
                new[] { KitchenRole.Cook, KitchenRole.Server, KitchenRole.Runner }, RoleMix(3));
        }

        [TestMethod]
        public void RoleMix_RespectsPerRoleCapsAndNeverExceedsMaxPosts()
        {
            var roles = RoleMix(99);
            Assert.AreEqual(KitchenTaskCoordinator.MaxWorkerPosts, roles.Count,
                "role mix must clamp to the total number of posts");
            Assert.AreEqual(GameConfig.MaxKitchenCooks, roles.FindAll(r => r == KitchenRole.Cook).Count);
            Assert.AreEqual(GameConfig.MaxKitchenServers, roles.FindAll(r => r == KitchenRole.Server).Count);
            Assert.AreEqual(GameConfig.MaxKitchenRunners, roles.FindAll(r => r == KitchenRole.Runner).Count);
            Assert.AreEqual(GameConfig.AutoJobKitchenMaxWorkers, KitchenTaskCoordinator.MaxWorkerPosts,
                "the auto-job cap must mirror the coordinator's post cap");
        }

        // ── Demand-weighted role mix (issue #375) ──

        private static System.Collections.Generic.List<KitchenRole> WeightedRoleMix(
            int postCount, int cookPressure, int serverPressure, int runnerPressure)
        {
            var roles = new System.Collections.Generic.List<KitchenRole>();
            KitchenTaskCoordinator.FillRoleMix(postCount, cookPressure, serverPressure,
                runnerPressure, roles);
            return roles;
        }

        [TestMethod]
        public void WeightedRoleMix_BaseCrewInvariantUnderAllWeights()
        {
            // Whatever the pressure skew, posts 0-2 stay Cook, Server, Runner — the crew that
            // opens the kitchen and keeps it fed and cleared.
            var roles = WeightedRoleMix(3, 0, 0, 99);
            CollectionAssert.AreEqual(
                new[] { KitchenRole.Cook, KitchenRole.Server, KitchenRole.Runner }, roles);
        }

        [TestMethod]
        public void WeightedRoleMix_HeavyRunnerPressure_FillsRunnersFirst()
        {
            // A backlog of ingredient fetches and dirty plates: posts 3+ go to runners until
            // their cap, then fall back to the neutral cycle.
            var roles = WeightedRoleMix(5, 0, 0, 10);
            CollectionAssert.AreEqual(
                new[]
                {
                    KitchenRole.Cook, KitchenRole.Server, KitchenRole.Runner,
                    KitchenRole.Runner, KitchenRole.Runner,
                }, roles);
        }

        [TestMethod]
        public void WeightedRoleMix_ServerPressure_CapsAtTwoServers()
        {
            // Server zoning only supports 2 servers (ServerZone design limit); pressure past the
            // cap spills into the neutral cycle.
            var roles = WeightedRoleMix(8, 0, 50, 0);
            Assert.AreEqual(GameConfig.MaxKitchenServers,
                roles.FindAll(r => r == KitchenRole.Server).Count);
        }

        [TestMethod]
        public void WeightedRoleMix_SplitsProportionallyByPressurePerWorker()
        {
            // D'Hondt greedy: cook pressure 6 vs server 3 vs runner 2 — the second and third
            // extra posts still favor cooks (6/2 then 6/3 beat 3/2 and 2/2) before the server.
            var roles = WeightedRoleMix(6, 6, 3, 2);
            CollectionAssert.AreEqual(
                new[]
                {
                    KitchenRole.Cook, KitchenRole.Server, KitchenRole.Runner,
                    KitchenRole.Cook, KitchenRole.Cook, KitchenRole.Server,
                }, roles);
        }

        [TestMethod]
        public void WeightedRoleMix_ZeroPressures_MatchesNeutralCycle()
        {
            var neutral = RoleMix(KitchenTaskCoordinator.MaxWorkerPosts);
            var weighted = WeightedRoleMix(KitchenTaskCoordinator.MaxWorkerPosts, 0, 0, 0);
            CollectionAssert.AreEqual(neutral, weighted,
                "The weighted mix with no pressure is exactly the legacy Cook→Server→Runner cycle");
        }

        [TestMethod]
        public void WeightedRoleMix_IsDeterministic()
        {
            var first = WeightedRoleMix(8, 4, 4, 4);
            var second = WeightedRoleMix(8, 4, 4, 4);
            CollectionAssert.AreEqual(first, second);
        }

        // ── Role retention (issue #375 follow-up: no cook-leaves-cook-arrives shuffles) ──

        private static System.Collections.Generic.List<KitchenRole> Retained(
            KitchenRole[] mix, int[] currentRoles)
        {
            var mixList = new System.Collections.Generic.List<KitchenRole>(mix);
            var currentList = new System.Collections.Generic.List<int>(currentRoles);
            var into = new System.Collections.Generic.List<KitchenRole>();
            KitchenTaskCoordinator.AssignRolesWithRetention(mixList, currentList, into);
            return into;
        }

        private const int NoWorker = -1;
        private const int C = (int)KitchenRole.Cook;
        private const int S = (int)KitchenRole.Server;
        private const int R = (int)KitchenRole.Runner;

        [TestMethod]
        public void Retention_SameCounts_NobodyChangesRole()
        {
            // The mix pattern flipped positions (C,S,R,C,R → recompute) but the COUNTS are the
            // same — every live worker keeps its current role, zero churn.
            var roles = Retained(
                new[] { KitchenRole.Cook, KitchenRole.Server, KitchenRole.Runner, KitchenRole.Cook, KitchenRole.Runner },
                new[] { R, C, S, R, C });
            CollectionAssert.AreEqual(
                new[] { KitchenRole.Runner, KitchenRole.Cook, KitchenRole.Server, KitchenRole.Runner, KitchenRole.Cook },
                roles);
        }

        [TestMethod]
        public void Retention_RoleShrinks_OnlyWorstHolderReassigned()
        {
            // Three cooks on shift but the mix now wants one: quota is consumed in post order
            // (best proficiency first), so the two lower posts are the ones reassigned.
            var roles = Retained(
                new[] { KitchenRole.Cook, KitchenRole.Server, KitchenRole.Runner },
                new[] { C, C, C });
            CollectionAssert.AreEqual(
                new[] { KitchenRole.Cook, KitchenRole.Server, KitchenRole.Runner }, roles);
        }

        [TestMethod]
        public void Retention_NewJoiner_TakesOnlyTheNewPost()
        {
            // Crew of three grows to four: the incumbents keep their roles, the joiner takes
            // exactly the added quota.
            var roles = Retained(
                new[] { KitchenRole.Cook, KitchenRole.Server, KitchenRole.Runner, KitchenRole.Runner },
                new[] { S, C, R, NoWorker });
            CollectionAssert.AreEqual(
                new[] { KitchenRole.Server, KitchenRole.Cook, KitchenRole.Runner, KitchenRole.Runner },
                roles);
        }

        [TestMethod]
        public void Retention_FreshSpawn_AssignsBaseRolesInOrder()
        {
            var roles = Retained(
                new[] { KitchenRole.Cook, KitchenRole.Server, KitchenRole.Runner },
                new[] { NoWorker, NoWorker, NoWorker });
            CollectionAssert.AreEqual(
                new[] { KitchenRole.Cook, KitchenRole.Server, KitchenRole.Runner }, roles);
        }

        [TestMethod]
        public void Retention_AlwaysPreservesTheMixRoleCounts()
        {
            // Whatever the current holders look like, the output multiset is exactly the mix's —
            // the kitchen-open guarantees (≥1 cook, ≥1 server) ride on the counts.
            var roles = Retained(
                new[] { KitchenRole.Cook, KitchenRole.Server, KitchenRole.Runner, KitchenRole.Cook },
                new[] { R, R, R, R });
            Assert.AreEqual(2, roles.FindAll(r => r == KitchenRole.Cook).Count);
            Assert.AreEqual(1, roles.FindAll(r => r == KitchenRole.Server).Count);
            Assert.AreEqual(1, roles.FindAll(r => r == KitchenRole.Runner).Count);
        }

        // ── Incremental role-mix reconcile (issue #375 follow-up: no pulse-chasing flips) ──

        private static System.Collections.Generic.List<KitchenRole> Reconciled(int postCount,
            float cookPressure, float serverPressure, float runnerPressure,
            int prevCooks, int prevServers, int prevRunners)
        {
            var into = new System.Collections.Generic.List<KitchenRole>();
            KitchenTaskCoordinator.ReconcileRoleMix(postCount, cookPressure, serverPressure,
                runnerPressure, prevCooks, prevServers, prevRunners, into);
            return into;
        }

        private static (int Cooks, int Servers, int Runners) Counts(
            System.Collections.Generic.List<KitchenRole> roles)
            => (roles.FindAll(r => r == KitchenRole.Cook).Count,
                roles.FindAll(r => r == KitchenRole.Server).Count,
                roles.FindAll(r => r == KitchenRole.Runner).Count);

        [TestMethod]
        public void Reconcile_FromEmpty_MatchesNeutralMixCounts()
        {
            // First-ever recompute (no previous mix) with quiet pressures reproduces the
            // neutral cycle's counts at every crew size.
            for (int postCount = 1; postCount <= KitchenTaskCoordinator.MaxWorkerPosts; postCount++)
            {
                var neutral = RoleMix(postCount);
                var reconciled = Reconciled(postCount, 0f, 0f, 0f, 0, 0, 0);
                Assert.AreEqual(Counts(neutral), Counts(reconciled),
                    $"neutral counts diverged at {postCount} posts");
            }
        }

        [TestMethod]
        public void Reconcile_BaseCrewPrefixInvariant()
        {
            // Posts 0-2 stay Cook, Server, Runner no matter what the previous mix looked like.
            var roles = Reconciled(5, 0f, 0f, 99f, 0, 0, 5);
            Assert.AreEqual(KitchenRole.Cook, roles[0]);
            Assert.AreEqual(KitchenRole.Server, roles[1]);
            Assert.AreEqual(KitchenRole.Runner, roles[2]);
        }

        [TestMethod]
        public void Reconcile_Growth_NeverReshufflesExistingPosts()
        {
            // The observed 06:30 thrash: crew grows 4 → 6 while runner pressure reads 1 and cook
            // 0. From-scratch recompute demoted a live cook; incremental growth only ADDS posts.
            var roles = Reconciled(6, 0f, 0f, 1f, 2, 1, 1);
            Assert.AreEqual((2, 1, 3), Counts(roles),
                "growth must keep incumbent role counts and give the new posts to the pressured role");
        }

        [TestMethod]
        public void Reconcile_PulseBelowMargin_ChangesNothing()
        {
            // A lone ticket moving through the pipeline pulses each role's pressure by 1 —
            // below KitchenRoleMixSwitchMargin, so an occupied post never flips.
            var roles = Reconciled(4, 1f, 0f, 0f, 1, 1, 2);
            Assert.AreEqual((1, 1, 2), Counts(roles));
            roles = Reconciled(4, 0f, 0f, 1f, 2, 1, 1);
            Assert.AreEqual((2, 1, 1), Counts(roles));
        }

        [TestMethod]
        public void Reconcile_SustainedImbalance_MovesOnePostPerRecompute()
        {
            // A real multi-ticket skew clears the margin, but the crew re-skews one post per
            // dwell period — never a wholesale reshuffle.
            var step1 = Reconciled(5, 0f, 0f, 3f, 3, 1, 1);
            Assert.AreEqual((2, 1, 2), Counts(step1));
            var step2 = Reconciled(5, 0f, 0f, 3f, 2, 1, 2);
            Assert.AreEqual((1, 1, 3), Counts(step2));
            var step3 = Reconciled(5, 0f, 0f, 3f, 1, 1, 3);
            Assert.AreEqual((1, 1, 3), Counts(step3),
                "cook is at the base-crew floor — the drain must stop");
        }

        [TestMethod]
        public void Reconcile_Shrink_RemovesTheLowestPressureRole()
        {
            var roles = Reconciled(5, 0f, 0f, 2f, 2, 1, 3);
            Assert.AreEqual((1, 1, 3), Counts(roles));
        }

        [TestMethod]
        public void Reconcile_Shrink_NeverBreaksTheBaseCrew()
        {
            var roles = Reconciled(2, 0f, 0f, 99f, 1, 1, 1);
            CollectionAssert.AreEqual(new[] { KitchenRole.Cook, KitchenRole.Server }, roles,
                "a two-monster kitchen is always the cook + server that open it");
        }

        [TestMethod]
        public void Reconcile_RepairsAnUnderfilledBaseRole()
        {
            // Stale mix lost its server (roster churn) — the floor is refilled from the
            // lowest-pressure surplus role.
            var roles = Reconciled(4, 0f, 0f, 2f, 2, 0, 2);
            var counts = Counts(roles);
            Assert.AreEqual(1, counts.Servers);
            Assert.AreEqual((1, 1, 2), counts,
                "the spare cook (zero pressure) is the one converted, not a pressured runner");
        }

        [TestMethod]
        public void Reconcile_IsDeterministic()
        {
            var first = Reconciled(7, 2.5f, 1f, 4f, 2, 2, 2);
            var second = Reconciled(7, 2.5f, 1f, 4f, 2, 2, 2);
            CollectionAssert.AreEqual(first, second);
        }

        // ── Bus queue (issue #327: runners own plate clearing) ──

        private static KitchenTaskCoordinator.BusJob MakeBusJob(Vector2 pos, float enqueuedTime)
        {
            var plate = new Entity("test-plate");
            plate.SetPosition(pos);
            return new KitchenTaskCoordinator.BusJob
            {
                DishEntity = plate,
                WorldPos = pos,
                EnqueuedTime = enqueuedTime,
            };
        }

        [TestMethod]
        public void BusJob_ReleasedByDepartingRunner_IsReclaimableAndStillBlocksTheSeat()
        {
            var pos = new Vector2(100f, 200f);
            var job = MakeBusJob(pos, 0f);

            // The runner walking to the plate is sent home before picking it up
            _coordinator.ReleaseBusJob(job);
            Assert.IsTrue(_coordinator.HasPendingBusJob);
            Assert.IsTrue(_coordinator.HasPendingBusJobAt(pos),
                "a released plate is still on the table, so arriving patrons must still see it");

            Assert.IsTrue(_coordinator.TryClaimBusJob(out var reclaimed));
            Assert.AreSame(job.DishEntity, reclaimed.DishEntity, "released bus job not reclaimable");
            Assert.AreEqual(0f, reclaimed.EnqueuedTime,
                "the original enqueue time must survive so the plate keeps its place in line");
            Assert.IsFalse(_coordinator.HasPendingBusJob);
        }

        [TestMethod]
        public void BusJob_ReleaseIsIdempotentAndIgnoresAlreadyCarriedPlates()
        {
            var job = MakeBusJob(new Vector2(100f, 200f), 0f);
            _coordinator.ReleaseBusJob(job);
            _coordinator.ReleaseBusJob(job);

            Assert.IsTrue(_coordinator.TryClaimBusJob(out _));
            Assert.IsFalse(_coordinator.TryClaimBusJob(out _), "double release must not duplicate the plate");

            // A plate already in hand had its entity destroyed at pickup — nothing to put back
            _coordinator.ReleaseBusJob(default);
            Assert.IsFalse(_coordinator.HasPendingBusJob);
        }

        // ── Runner fetch route (issue #327 follow-up: visit the storages that hold the crops) ──

        /// <summary>Adds a second Crop Storage further from the kitchen than the default one.</summary>
        private const int FarStorageBuildingId = 3;

        private void AddFarStorage()
        {
            _buildings.AddBuilding(new PlacedBuilding
            {
                Type = BuildingType.CropStorage,
                TileX = GameConfig.NewGameCropStorageAnchorTileX + 20,
                TileY = GameConfig.NewGameCropStorageAnchorTileY,
                UniqueId = FarStorageBuildingId
            });
        }

        private static Point DoorOf(int anchorX, int anchorY)
            => Util.BuildingConfig.GetDoorTile(BuildingType.CropStorage, new Point(anchorX, anchorY));

        private static Point NearStorageDoor => DoorOf(
            GameConfig.NewGameCropStorageAnchorTileX, GameConfig.NewGameCropStorageAnchorTileY);

        private static Point FarStorageDoor => DoorOf(
            GameConfig.NewGameCropStorageAnchorTileX + 20, GameConfig.NewGameCropStorageAnchorTileY);

        private System.Collections.Generic.List<KitchenTaskCoordinator.FetchStop> PlanRoute(KitchenTicket t)
        {
            var route = new System.Collections.Generic.List<KitchenTaskCoordinator.FetchStop>();
            _coordinator.PlanFetchRoute(t, KitchenTaskCoordinator.FridgeTile, route);
            return route;
        }

        [TestMethod]
        public void FetchRoute_SkipsTheNearStorageWhenOnlyTheFarOneHoldsTheCrops()
        {
            AddFarStorage();
            // Everything the recipe needs sits in the FAR storage; the near one is empty
            var def = DishConfig.GetDefinition(Dish);
            for (int i = 0; i < def.Recipe.Length; i++)
                Assert.IsTrue(_storage.TryDeposit(FarStorageBuildingId, def.Recipe[i].Crop, def.Recipe[i].Qty));

            var ticket = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
            Assert.IsNotNull(ticket);
            CollectionAssert.AreEqual(new[] { FarStorageBuildingId }, ticket.SourceBuildingIds,
                "the shortfall came from the far storage, so that's what the ticket must remember");

            var route = PlanRoute(ticket);
            Assert.AreEqual(1, route.Count, "the empty near storage is not worth a stop");
            Assert.AreEqual(FarStorageBuildingId, route[0].BuildingId);
            Assert.AreEqual(FarStorageDoor, route[0].DoorTile);
        }

        [TestMethod]
        public void FetchRoute_VisitsBothStoragesWhenTheRecipeIsSplitAcrossThem()
        {
            AddFarStorage();
            // TurnipOnionStew is turnip + onion — one crop per storage
            var splitDish = DishType.TurnipOnionStew;
            var def = DishConfig.GetDefinition(splitDish);
            Assert.IsTrue(def.Recipe.Length >= 2, "this test needs a multi-crop recipe");

            // First crop only in the near storage, the rest only in the far one
            Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, def.Recipe[0].Crop, def.Recipe[0].Qty));
            for (int i = 1; i < def.Recipe.Length; i++)
                Assert.IsTrue(_storage.TryDeposit(FarStorageBuildingId, def.Recipe[i].Crop, def.Recipe[i].Qty));

            var ticket = _coordinator.CreateTicket(splitDish, false, -1, null, PatronSeat);
            Assert.IsNotNull(ticket);
            CollectionAssert.AreEquivalent(new[] { StorageBuildingId, FarStorageBuildingId },
                ticket.SourceBuildingIds);

            var route = PlanRoute(ticket);
            Assert.AreEqual(2, route.Count, "a recipe split across two storages must tour both");
            Assert.AreEqual(StorageBuildingId, route[0].BuildingId, "nearest storage first");
            Assert.AreEqual(NearStorageDoor, route[0].DoorTile);
            Assert.AreEqual(FarStorageBuildingId, route[1].BuildingId);
        }

        [TestMethod]
        public void FetchRoute_NeverExceedsTheStopCap()
        {
            for (int extra = 0; extra < GameConfig.RunnerMaxStorageStops + 3; extra++)
            {
                _buildings.AddBuilding(new PlacedBuilding
                {
                    Type = BuildingType.CropStorage,
                    TileX = GameConfig.NewGameCropStorageAnchorTileX + 4 * (extra + 1),
                    TileY = GameConfig.NewGameCropStorageAnchorTileY,
                    UniqueId = 10 + extra
                });
            }

            // Spread one recipe crop across every storage so they all look worth visiting
            var def = DishConfig.GetDefinition(Dish);
            for (int extra = 0; extra < GameConfig.RunnerMaxStorageStops + 3; extra++)
                Assert.IsTrue(_storage.TryDeposit(10 + extra, def.Recipe[0].Crop, 1));
            for (int i = 0; i < def.Recipe.Length; i++)
                Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, def.Recipe[i].Crop, def.Recipe[i].Qty));

            var ticket = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
            Assert.IsNotNull(ticket);
            Assert.IsTrue(PlanRoute(ticket).Count <= GameConfig.RunnerMaxStorageStops,
                "a runner must not tour every storage on the farm");
        }

        [TestMethod]
        public void RunnerCollect_AtOneStorageOnlyDrawsOnThatBuilding()
        {
            AddFarStorage();
            var crop = DishConfig.GetDefinition(Dish).Recipe[0].Crop;
            Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, crop, 2));
            Assert.IsTrue(_storage.TryDeposit(FarStorageBuildingId, crop, 5));

            StockRecipe();
            var ticket = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
            int nearBefore = _storage.CountIn(StorageBuildingId, crop);
            int farBefore = _storage.CountIn(FarStorageBuildingId, crop);

            _coordinator.RunnerCollectAtStorage(ticket, new int[Farming.CropTypeInfo.Count], FarStorageBuildingId);

            Assert.AreEqual(nearBefore, _storage.CountIn(StorageBuildingId, crop),
                "collecting at the far storage must not teleport crops out of the near one");
            Assert.IsTrue(_storage.CountIn(FarStorageBuildingId, crop) < farBefore,
                "the far storage should have supplied the fridge top-up");
        }

        [TestMethod]
        public void HasReadableTicket_MirrorsWhatACookCanClaim()
        {
            StockRecipe();
            Assert.IsFalse(_coordinator.HasReadableTicket(), "no orders yet");

            var ticket = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
            Assert.IsFalse(_coordinator.HasReadableTicket(),
                "an unposted ticket is invisible to cooks, so a wandering cook must stay put");

            _coordinator.PostTicket(ticket);
            Assert.IsTrue(_coordinator.HasReadableTicket(), "a posted ticket must call the cook back");

            Assert.AreSame(ticket, _coordinator.TryReadNextTicket());
            Assert.IsFalse(_coordinator.HasReadableTicket(), "a claimed ticket must not call a second cook back");
        }

        [TestMethod]
        public void BusJob_AgeGateClaimsTheOldestWaitingPlateFirst()
        {
            Time.TotalTime = 1000f;
            _coordinator.ReleaseBusJob(MakeBusJob(new Vector2(10f, 10f), 900f));  // 100s old
            _coordinator.ReleaseBusJob(MakeBusJob(new Vector2(20f, 20f), 990f));  // 10s old

            // Fallback bussing only takes plates past the age gate, oldest first
            Assert.IsTrue(_coordinator.TryClaimBusJob(GameConfig.ServerBusPlateMaxWaitSeconds, out var aged));
            Assert.AreEqual(900f, aged.EnqueuedTime);
            Assert.IsFalse(_coordinator.TryClaimBusJob(GameConfig.ServerBusPlateMaxWaitSeconds, out _),
                "the fresh plate must not pass the age gate");

            // A runner (no age gate) takes it immediately
            Assert.IsTrue(_coordinator.TryClaimBusJob(out var fresh));
            Assert.AreEqual(990f, fresh.EnqueuedTime);
        }
    }
}
