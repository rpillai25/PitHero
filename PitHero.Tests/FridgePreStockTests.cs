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
        public void Deficit_QueuesCarryLimitedTripForARecipeCropInStorage()
        {
            var crop = FirstRecipeCrop;
            Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, crop, 25));

            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.PreStockQueueDepth > 0, "a below-target recipe crop must queue a trip");

            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var job));
            Assert.AreEqual(1, job.CropCount);
            Assert.AreEqual(crop, job.Crops[0]);
            Assert.AreEqual(StorageBuildingId, job.BuildingId);
            Assert.AreEqual(GameConfig.GetRunnerCarryUnits(1), job.Units[0],
                "at carry level 1 a runner hauls a single unit of the crop per trip");

            int taken = _coordinator.PreStockCollect(job);
            Assert.AreEqual(1, taken);
            Assert.AreEqual(1, _coordinator.FridgeCount(crop));
            Assert.AreEqual(24, _storage.CountTotal(crop), "collect must draw only from storage");
        }

        [TestMethod]
        public void CarryLevel_GatesUnitsPerTrip()
        {
            Assert.AreEqual(1, GameConfig.GetRunnerCarryUnits(1));
            Assert.AreEqual(5, GameConfig.GetRunnerCarryUnits(2));
            Assert.AreEqual(10, GameConfig.GetRunnerCarryUnits(3));

            var crop = FirstRecipeCrop;
            Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, crop, 40));

            _gameState.RunnerCarryLevel = 2;
            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var job));
            Assert.AreEqual(5, job.Units[0], "carry level 2 hauls 5 units per crop");
            Assert.AreEqual(5, _coordinator.PreStockCollect(job));

            _gameState.RunnerCarryLevel = 3;
            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out job));
            Assert.AreEqual(5, job.Units[0],
                "carry level 3 hauls up to 10 but only the 5 units still missing from the target");
            Assert.AreEqual(5, _coordinator.PreStockCollect(job));
            Assert.AreEqual(GameConfig.KitchenFridgeStackSize, _coordinator.FridgeCount(crop));
        }

        [TestMethod]
        public void RunnerCarryLevel_ClampsToValidRange()
        {
            _gameState.RunnerCarryLevel = 0;
            Assert.AreEqual(GameConfig.KitchenRunnerCarryLevelMin, _gameState.RunnerCarryLevel);
            _gameState.RunnerCarryLevel = 99;
            Assert.AreEqual(GameConfig.KitchenRunnerCarryLevelMax, _gameState.RunnerCarryLevel);
        }

        [TestMethod]
        public void BatchTrip_CarriesUpToThreeCropsFromOneStorage()
        {
            // Find four distinct recipe crops so the fourth must wait for a second trip
            var recipeCrops = new System.Collections.Generic.List<CropType>();
            for (int d = 0; d < DishTypeInfo.Count && recipeCrops.Count < 4; d++)
            {
                var def = DishConfig.GetDefinition((DishType)d);
                for (int i = 0; i < def.Recipe.Length && recipeCrops.Count < 4; i++)
                    if (!recipeCrops.Contains(def.Recipe[i].Crop))
                        recipeCrops.Add(def.Recipe[i].Crop);
            }
            Assert.IsTrue(recipeCrops.Count >= 4, "this test needs four distinct recipe crops");

            for (int i = 0; i < recipeCrops.Count; i++)
                Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, recipeCrops[i], 20));

            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var job));
            Assert.AreEqual(GameConfig.KitchenRunnerCarryCropTypes, job.CropCount,
                "one trip fills all three hand slots when one storage holds three deficit crops");

            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var second));
            Assert.AreEqual(1, second.CropCount, "the fourth crop rides the next trip");
        }

        [TestMethod]
        public void PreStockCollect_NeverOvershootsTheTarget()
        {
            var crop = FirstRecipeCrop;
            _gameState.RunnerCarryLevel = 3; // 10 units planned per trip
            Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, crop, 25));

            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var job));
            Assert.AreEqual(GameConfig.KitchenFridgeStackSize, job.Units[0]);

            // While the runner walks, another actor tops this crop up to the target
            _fridge.Deposit(crop, GameConfig.KitchenFridgeStackSize);

            Assert.AreEqual(0, _coordinator.PreStockCollect(job),
                "collect must re-clamp against the live target, not the units planned at claim");
            Assert.AreEqual(25, _storage.CountTotal(crop), "no extra crops may leave storage");
            Assert.AreEqual(GameConfig.KitchenFridgeStackSize, _coordinator.FridgeCount(crop));
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
            Assert.AreEqual(job.Crops[0], crop);

            // Still claimed (not collected): the busy mask must block a re-queue for the same crop
            _coordinator.RecomputePreStockDeficits();
            bool duplicate = false;
            while (_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var other))
                for (int i = 0; i < other.CropCount; i++)
                    if (other.Crops[i] == crop)
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
            Assert.AreEqual(crop, reclaimed.Crops[0]);
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
            Assert.AreEqual(crop, job.Crops[0]);
        }
    }
}
