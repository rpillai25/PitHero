using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez;
using PitHero;
using PitHero.Dining;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.Tests
{
    /// <summary>
    /// Headless tests for the fridge pre-stock job system (issue #386): deficit detection queues
    /// runner trips for recipe crops available in storage, tickets that drain the fridge re-queue
    /// immediately, the busy mask prevents duplicate trips, and a full fridge stops the loop.
    /// </summary>
    [TestClass]
    public class FridgePreStockTests
    {
        private const int StorageBuildingId = 2;
        private static readonly DishType Dish = DishType.RoastedOnionSkewers;
        private static readonly Point PatronSeat = new Point(96, 7);

        private BuildingService _buildings;
        private CropStorageInventoryService _storage;
        private GameStateService _gameState;
        private FridgeInventoryService _fridge;
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
            _fridge = new FridgeInventoryService();
            _coordinator = new KitchenTaskCoordinator(null, _buildings, 240, 12);
            _coordinator.SetHeadlessServices(_storage, _gameState, _fridge);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Time.DeltaTime = 0f;
            Time.TotalTime = 0f;
        }

        private static CropType FirstRecipeCrop => DishConfig.GetDefinition(Dish).Recipe[0].Crop;

        /// <summary>A crop no dish recipe uses, or null when every crop is a recipe ingredient.</summary>
        private static CropType? FindNonRecipeCrop()
        {
            var used = new bool[CropTypeInfo.Count];
            for (int d = 0; d < DishTypeInfo.Count; d++)
            {
                var def = DishConfig.GetDefinition((DishType)d);
                for (int i = 0; i < def.Recipe.Length; i++)
                    used[(int)def.Recipe[i].Crop] = true;
            }
            for (int c = 0; c < used.Length; c++)
                if (!used[c])
                    return (CropType)c;
            return null;
        }

        [TestMethod]
        public void Deficit_QueuesOneStackTripForARecipeCropInStorage()
        {
            var crop = FirstRecipeCrop;
            Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, crop, 25));

            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.PreStockQueueDepth > 0, "a below-target recipe crop must queue a trip");

            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var job));
            Assert.AreEqual(crop, job.Crop);
            Assert.AreEqual(StorageBuildingId, job.BuildingId);
            Assert.AreEqual(GameConfig.KitchenFridgeStackSize, job.Units, "one flat fridge stack per trip");

            int taken = _coordinator.PreStockCollect(job);
            Assert.AreEqual(GameConfig.KitchenFridgeStackSize, taken);
            Assert.AreEqual(GameConfig.KitchenFridgeStackSize, _coordinator.FridgeCount(crop));
            Assert.AreEqual(15, _storage.CountTotal(crop), "collect must draw only from storage");
        }

        [TestMethod]
        public void Deficit_SkipsCropsWithNoStorageStock()
        {
            _coordinator.RecomputePreStockDeficits();
            Assert.AreEqual(0, _coordinator.PreStockQueueDepth, "nothing in storage — nothing to fetch");
            Assert.IsFalse(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out _));
        }

        [TestMethod]
        public void Deficit_SkipsCropsNoRecipeUses()
        {
            var nonRecipe = FindNonRecipeCrop();
            if (nonRecipe == null)
                return; // every crop is a recipe ingredient — the mask has nothing to filter

            Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, nonRecipe.Value, 25));
            _coordinator.RecomputePreStockDeficits();
            Assert.AreEqual(0, _coordinator.PreStockQueueDepth,
                "a crop no dish uses must never be pre-stocked");
        }

        [TestMethod]
        public void Deficit_DoesNotQueueDuplicateWhileAJobIsPending()
        {
            var crop = FirstRecipeCrop;
            Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, crop, 25));

            _coordinator.RecomputePreStockDeficits();
            _coordinator.RecomputePreStockDeficits();
            int depth = _coordinator.PreStockQueueDepth;

            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var job));
            Assert.AreEqual(job.Crop, crop);

            // Still claimed (not collected): the busy mask must block a re-queue for the same crop
            _coordinator.RecomputePreStockDeficits();
            bool duplicate = false;
            while (_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var other))
                if (other.Crop == crop)
                    duplicate = true;
            Assert.IsFalse(duplicate, "a crop with an in-flight trip must not queue a second one");
            Assert.IsTrue(depth >= 1);
        }

        [TestMethod]
        public void ReleasedJob_CanBeReQueuedAndReclaimed()
        {
            var crop = FirstRecipeCrop;
            Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, crop, 25));

            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var job));

            // Runner despawned mid-trip
            _coordinator.ReleasePreStockJob(job);
            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var reclaimed));
            Assert.AreEqual(crop, reclaimed.Crop);
        }

        [TestMethod]
        public void TicketFridgeTake_ReQueuesTheDrainedCropImmediately()
        {
            var def = DishConfig.GetDefinition(Dish);
            for (int i = 0; i < def.Recipe.Length; i++)
                Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, def.Recipe[i].Crop, 40));

            // Pre-stock every recipe crop to its 1-stack target
            _coordinator.RecomputePreStockDeficits();
            while (_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var job))
            {
                Assert.IsTrue(_coordinator.PreStockCollect(job) > 0);
                _coordinator.RecomputePreStockDeficits();
            }
            for (int i = 0; i < def.Recipe.Length; i++)
                Assert.AreEqual(GameConfig.KitchenFridgeStackSize, _coordinator.FridgeCount(def.Recipe[i].Crop));

            // The order draws from the fridge; CreateTicket itself must re-queue the deficit —
            // no waiting for the coordinator's throttled poll
            var ticket = _coordinator.CreateTicket(Dish, false, -1, null, PatronSeat);
            Assert.IsNotNull(ticket);
            Assert.IsTrue(ticket.IngredientsFetched, "a pre-stocked fridge covers the order with no runner trip");
            Assert.IsTrue(_coordinator.PreStockQueueDepth > 0,
                "taking fridge stock below target must queue a refill immediately");
        }

        [TestMethod]
        public void PreStock_StopsWhenTheFridgeIsFull()
        {
            var cropA = FirstRecipeCrop;
            // Fill every fridge slot with a different crop so cropA has zero capacity
            var cropB = (CropType)(((int)cropA + 1) % CropTypeInfo.Count);
            _fridge.Deposit(cropB, FridgeInventoryService.SlotCount * GameConfig.KitchenFridgeStackSize);

            Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, cropA, 25));
            _coordinator.RecomputePreStockDeficits();
            Assert.IsFalse(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out _),
                "a full fridge must not spawn undepositable runner trips");
        }

        [TestMethod]
        public void StaleJob_SkippedWhenStorageEmptiedAfterQueueing()
        {
            var crop = FirstRecipeCrop;
            Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, crop, 10));

            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.PreStockQueueDepth > 0);

            // Storage emptied between queueing and claiming (e.g. auto-sell)
            _storage.WithdrawUpTo(StorageBuildingId, crop, 10);
            Assert.IsFalse(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out _),
                "a stale job must be dropped, not handed to a runner");

            // And the crop is claimable again once stock returns
            Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, crop, 10));
            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var job));
            Assert.AreEqual(crop, job.Crop);
        }
    }
}
