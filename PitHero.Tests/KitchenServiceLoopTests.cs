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
    /// Headless end-to-end verification of the tavern service loop through the REAL coordinator
    /// and patron component (no Nez scene): order taken → ingredients fetched → cooked → plated →
    /// delivered → eaten → paid → ticket retired, plus the early-leave cases (patience expiry
    /// before/after ordering, leaving after cooking started, hire mid-dining).
    /// Worker WALKING is live-only; the walk routes are covered by KitchenFlowPathTests.
    /// </summary>
    [TestClass]
    public class KitchenServiceLoopTests
    {
        private const int StorageBuildingId = 2;
        private static readonly DishType Dish = DishType.RoastedOnionSkewers;

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

        private int TotalStockedUnits()
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

        /// <summary>Creates a seated patron entity with its component wired for headless use.</summary>
        private Entity CreatePatron(out TavernPatronComponent patron)
        {
            var entity = new Entity("test-patron");
            patron = new TavernPatronComponent { SeatTile = new Point(96, 7) };
            patron.SetHeadlessServices(_coordinator, _gameState);
            entity.AddComponent(patron);
            return entity;
        }

        private static void Tick(TavernPatronComponent patron, float seconds)
        {
            Time.DeltaTime = seconds;
            patron.Update();
        }

        [TestMethod]
        public void FullServiceLoop_OrderCookDeliverEatPayLeave()
        {
            StockRecipe();
            var entity = CreatePatron(out var patron);
            Assert.AreEqual(PatronState.WaitingToOrder, patron.State);

            // Server takes the order → ticket created, crops withdrawn as the reservation
            var ticket = _coordinator.CreateTicket(Dish, false, -1, entity);
            Assert.IsNotNull(ticket, "ticket refused despite stocked recipe");
            patron.OnOrderTaken(ticket);
            Assert.AreEqual(PatronState.Ordered, patron.State);
            Assert.AreEqual(0, TotalStockedUnits(), "crops not withdrawn at order time");
            Assert.AreEqual(TicketState.AwaitingIngredients, ticket.State, "storage exists — runner trip expected");

            // Runner fetches ingredients
            var fetch = _coordinator.TryClaimFetchTicket();
            Assert.AreSame(ticket, fetch);
            _coordinator.OnIngredientsFetched(ticket);
            Assert.AreEqual(TicketState.ReadyToCook, ticket.State);

            // Cook claims and cooks on stove 0
            var cooking = _coordinator.BeginCooking(0, cookProficiency: 5);
            Assert.AreSame(ticket, cooking);
            Assert.AreEqual(TicketState.Cooking, ticket.State);
            Assert.IsFalse(ticket.CropsRefundable, "crops must be non-refundable once cooking starts");

            // Cook plates the dish (dish entity is live-only; null headless)
            _coordinator.OnDishPlated(ticket, null);
            Assert.AreEqual(TicketState.Plated, ticket.State);

            // Server picks it up and delivers — patron is notified through the entity lookup
            var delivery = _coordinator.TryClaimDeliveryTicket();
            Assert.AreSame(ticket, delivery);
            Assert.AreEqual(TicketState.Delivering, ticket.State);
            _coordinator.OnTicketDelivered(ticket, null);
            Assert.AreEqual(TicketState.Delivered, ticket.State);
            Assert.AreEqual(PatronState.FoodDelivered, patron.State, "delivery did not notify the patron");

            // Patron eats: first tick transitions to Eating, then the eat timer runs down
            Tick(patron, 0.01f);
            Assert.AreEqual(PatronState.Eating, patron.State);
            Tick(patron, DishConfig.GetEatSeconds(Dish) + 0.1f);
            Assert.AreEqual(PatronState.FinishedEating, patron.State);

            // Paid: price always, tip at most ceil(price × max tip percent)
            int price = DishConfig.GetPrice(Dish);
            int maxTip = (int)System.Math.Ceiling(price * GameConfig.DishTipMaxPercent);
            Assert.IsTrue(_gameState.Funds >= price && _gameState.Funds <= price + maxTip,
                $"funds {_gameState.Funds} outside [{price}, {price + maxTip}]");

            // Ticket fully retired: nothing claimable, stove free for the next order
            Assert.IsNull(_coordinator.TryClaimDeliveryTicket());
            Assert.IsNull(_coordinator.TryClaimFetchTicket());
            StockRecipe();
            var next = _coordinator.CreateTicket(Dish, false, -1, null);
            _coordinator.OnIngredientsFetched(next);
            Assert.IsNotNull(_coordinator.BeginCooking(0, 5), "stove 0 claim was not released");
        }

        [TestMethod]
        public void PatronPatienceExpires_BeforeOrdering_NothingChargedNothingLost()
        {
            StockRecipe();
            CreatePatron(out var patron);

            Tick(patron, GameConfig.PatronPatiencePreOrderSeconds + 1f);

            Assert.AreEqual(PatronState.FinishedEating, patron.State, "patron should give up and leave");
            Assert.AreEqual(0, _gameState.Funds);
            Assert.AreEqual(RecipeUnitCount(), TotalStockedUnits(), "storage must be untouched");
        }

        [TestMethod]
        public void PatronPatienceExpires_AfterOrdering_BeforeCooking_CropsRefunded()
        {
            StockRecipe();
            var entity = CreatePatron(out var patron);

            var ticket = _coordinator.CreateTicket(Dish, false, -1, entity);
            patron.OnOrderTaken(ticket);
            Assert.AreEqual(0, TotalStockedUnits());

            Tick(patron, GameConfig.PatronPatiencePostOrderSeconds + 1f);

            Assert.AreEqual(PatronState.FinishedEating, patron.State);
            Assert.AreEqual(TicketState.Canceled, ticket.State);
            Assert.AreEqual(0, _gameState.Funds, "uncooked order must not be charged");
            Assert.AreEqual(RecipeUnitCount(), TotalStockedUnits(), "crops must be refunded pre-cooking");
            Assert.IsNull(_coordinator.TryClaimFetchTicket(), "canceled ticket still claimable by runner");
        }

        [TestMethod]
        public void PatronLeaves_AfterCookingStarted_PaymentStandsNoRefund()
        {
            StockRecipe();
            var entity = CreatePatron(out var patron);

            var ticket = _coordinator.CreateTicket(Dish, false, -1, entity);
            patron.OnOrderTaken(ticket);
            _coordinator.OnIngredientsFetched(ticket);
            Assert.IsNotNull(_coordinator.BeginCooking(0, 5));

            // Patience runs out mid-cook — patron walks
            Tick(patron, GameConfig.PatronPatiencePostOrderSeconds + 1f);

            Assert.AreEqual(PatronState.FinishedEating, patron.State);
            Assert.AreEqual(0, TotalStockedUnits(), "spent crops must NOT be refunded after cooking started");
            Assert.AreEqual(DishConfig.GetPrice(Dish), _gameState.Funds,
                "cooked-but-abandoned dish must still be paid for (no tip)");

            // Stove claim released so the kitchen isn't wedged
            StockRecipe();
            var next = _coordinator.CreateTicket(Dish, false, -1, null);
            _coordinator.OnIngredientsFetched(next);
            Assert.IsNotNull(_coordinator.BeginCooking(0, 5), "stove claim leaked after mid-cook cancel");
        }

        [TestMethod]
        public void PatronHiredMidDining_TicketCanceledByEntity_CropsRefunded()
        {
            StockRecipe();
            var entity = CreatePatron(out var patron);
            var ticket = _coordinator.CreateTicket(Dish, false, -1, entity);
            patron.OnOrderTaken(ticket);

            // HireMercenary path: cancel by patron entity before cooking
            _coordinator.CancelTicketForPatron(entity);

            Assert.AreEqual(TicketState.Canceled, ticket.State);
            Assert.AreEqual(RecipeUnitCount(), TotalStockedUnits(), "pre-cook hire must refund crops");
            Assert.AreEqual(0, _gameState.Funds);
            Assert.IsNull(_coordinator.TryClaimFetchTicket());
        }

        [TestMethod]
        public void PartyTicket_ClaimedBeforeEarlierPatronTicket()
        {
            StockRecipe(servings: 2);

            var patronTicket = _coordinator.CreateTicket(Dish, false, -1, null);
            var partyTicket = _coordinator.CreateTicket(Dish, true, 0, null);
            _coordinator.OnIngredientsFetched(patronTicket);
            _coordinator.OnIngredientsFetched(partyTicket);

            var first = _coordinator.BeginCooking(0, 5);
            Assert.AreSame(partyTicket, first, "party orders must cook before patron orders");
            var second = _coordinator.BeginCooking(1, 5);
            Assert.AreSame(patronTicket, second);
        }

        [TestMethod]
        public void CookInterruptedMidCook_TicketRequeuedAndReclaimable()
        {
            StockRecipe();
            var ticket = _coordinator.CreateTicket(Dish, false, -1, null);
            _coordinator.OnIngredientsFetched(ticket);

            Assert.AreSame(ticket, _coordinator.BeginCooking(0, 5));

            // Shift boundary: cook walks home mid-cook
            _coordinator.ReleaseTicket(ticket);
            Assert.AreEqual(TicketState.ReadyToCook, ticket.State);
            Assert.AreEqual(-1, ticket.StoveIndex);

            // Another cook (different stove) picks it up
            Assert.AreSame(ticket, _coordinator.BeginCooking(1, 5));
        }

        [TestMethod]
        public void InsufficientStock_OrderRefused_NothingWithdrawn()
        {
            // No crops stocked at all
            var ticket = _coordinator.CreateTicket(Dish, false, -1, null);
            Assert.IsNull(ticket);
            Assert.AreEqual(0, TotalStockedUnits());
        }
    }
}
