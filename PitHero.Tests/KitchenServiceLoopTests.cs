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

        /// <summary>Runs the runner leg for a ticket (claim → collect at storage → deposit at fridge).</summary>
        private void RunRunnerLeg(KitchenTicket expected)
        {
            var job = _coordinator.TryClaimFetchJob();
            Assert.AreSame(expected, job, "runner did not claim the queued fetch job");
            _coordinator.RunnerCollectAtStorage(job);
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
        public void FridgeParStocking_SecondOrderSkipsRunnerTrip()
        {
            var def = DishConfig.GetDefinition(Dish);
            // Stock plenty: the runner's par top-up should leave fridge stock for the next order
            StockRecipe(servings: 4);

            var t1 = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
            _coordinator.PostTicket(t1);
            Assert.AreEqual(TicketState.AwaitingIngredients, t1.State, "fridge starts empty");
            RunRunnerLeg(t1);

            // Par top-up filled the fridge from remaining storage
            for (int i = 0; i < def.Recipe.Length; i++)
            {
                int expected = System.Math.Min(GameConfig.KitchenFridgeParPerCrop, def.Recipe[i].Qty * 3);
                Assert.AreEqual(expected, _coordinator.FridgeCount(def.Recipe[i].Crop),
                    $"fridge par top-up wrong for {def.Recipe[i].Crop}");
            }

            // If the par covers the recipe, the second order needs no runner trip
            bool parCoversRecipe = true;
            for (int i = 0; i < def.Recipe.Length; i++)
                if (def.Recipe[i].Qty > GameConfig.KitchenFridgeParPerCrop)
                    parCoversRecipe = false;

            var t2 = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
            Assert.IsNotNull(t2);
            if (parCoversRecipe)
            {
                Assert.IsTrue(t2.IngredientsFetched, "fridge stock should cover the second order");
                Assert.AreEqual(TicketState.ReadyToCook, t2.State);
                Assert.IsNull(_coordinator.TryClaimFetchJob(), "runner dispatched despite full fridge");
            }
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
    }
}
