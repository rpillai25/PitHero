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

        private static System.Collections.Generic.List<KitchenTaskCoordinator.CarriedCrop> NewCarry()
            => new System.Collections.Generic.List<KitchenTaskCoordinator.CarriedCrop>();

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

            var carry = NewCarry();
            Assert.AreEqual(1, _coordinator.PreStockCollect(job, carry));
            Assert.AreEqual(0, _coordinator.FridgeCount(crop),
                "picking up at the storage door must NOT stock the fridge");
            Assert.AreEqual(25, _storage.CountTotal(crop),
                "held units stay physically in storage — a save mid-walk loses nothing");
            Assert.AreEqual(24, _storage.AvailableTotal(crop),
                "held units are invisible to every other consumer");

            _coordinator.PreStockDeliver(job, carry);
            Assert.AreEqual(1, _coordinator.FridgeCount(crop),
                "fridge stock rises only when the runner unloads");
            Assert.AreEqual(24, _storage.CountTotal(crop),
                "the unload is when units physically leave storage");
        }

        [TestMethod]
        public void FridgeStock_IncreasesOnlyAtDelivery_AndBusyHoldsThroughCarry()
        {
            var crop = FirstRecipeCrop;
            Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, crop, 25));

            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var job));

            var carry = NewCarry();
            Assert.IsTrue(_coordinator.PreStockCollect(job, carry) > 0);
            Assert.AreEqual(0, _coordinator.FridgeCount(crop), "cargo is in the runner's hands, not the fridge");

            // While the cargo is in transit, the deficit recompute must NOT dispatch a second
            // runner for the same crop
            _coordinator.RecomputePreStockDeficits();
            Assert.IsFalse(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out _),
                "a crop being carried must stay busy until it is unloaded");

            _coordinator.PreStockDeliver(job, carry);
            Assert.AreEqual(1, _coordinator.FridgeCount(crop));

            // After delivery the crop is claimable again (still below the 10-unit target)
            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var next));
            Assert.AreEqual(crop, next.Crops[0]);
        }

        [TestMethod]
        public void CarryLevel_GatesUnitsPerTrip()
        {
            Assert.AreEqual(1, GameConfig.GetRunnerCarryUnits(1));
            Assert.AreEqual(5, GameConfig.GetRunnerCarryUnits(2));
            Assert.AreEqual(10, GameConfig.GetRunnerCarryUnits(3));

            var crop = FirstRecipeCrop;
            Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, crop, 40));

            var carry = NewCarry();

            _gameState.RunnerCarryLevel = 2;
            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var job));
            Assert.AreEqual(5, job.Units[0], "carry level 2 hauls 5 units per crop");
            Assert.AreEqual(5, _coordinator.PreStockCollect(job, carry));
            _coordinator.PreStockDeliver(job, carry);

            _gameState.RunnerCarryLevel = 3;
            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out job));
            Assert.AreEqual(5, job.Units[0],
                "carry level 3 hauls up to 10 but only the 5 units still missing from the target");
            Assert.AreEqual(5, _coordinator.PreStockCollect(job, carry));
            _coordinator.PreStockDeliver(job, carry);
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

            Assert.AreEqual(0, _coordinator.PreStockCollect(job, NewCarry()),
                "collect must re-clamp against the live target, not the units planned at claim");
            Assert.AreEqual(25, _storage.AvailableTotal(crop), "no crops may be held or taken");
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

            // Pre-stock every recipe crop to its 1-stack target (collect + unload each trip)
            var carry = NewCarry();
            _coordinator.RecomputePreStockDeficits();
            while (_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var job))
            {
                Assert.IsTrue(_coordinator.PreStockCollect(job, carry) > 0);
                _coordinator.PreStockDeliver(job, carry);
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

        [TestMethod]
        public void HeldUnits_AreInvisibleToOrderReservation()
        {
            // Storage holds exactly one recipe's worth; a runner picks it all up
            var def = DishConfig.GetDefinition(Dish);
            _gameState.RunnerCarryLevel = 3;
            for (int i = 0; i < def.Recipe.Length; i++)
                Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, def.Recipe[i].Crop, def.Recipe[i].Qty));

            var carry = NewCarry();
            _coordinator.RecomputePreStockDeficits();
            while (_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var job))
                _coordinator.PreStockCollect(job, carry);

            // Everything is in a runner's hands — an order must be refused, not double-book
            // the same units
            Assert.IsNull(_coordinator.CreateTicket(Dish, false, -1, null, PatronSeat),
                "held-for-transfer units must not satisfy an order's reservation");

            // Once unloaded into the fridge, the same order succeeds from fridge stock
            _coordinator.DeliverCarriedTopUp(carry); // deliver path is identical for this purpose
            Assert.IsNotNull(_coordinator.CreateTicket(Dish, false, -1, null, PatronSeat));
        }

        [TestMethod]
        public void AbandonedCarry_ReleasesHoldsWithNothingLost()
        {
            var crop = FirstRecipeCrop;
            Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, crop, 25));

            var carry = NewCarry();
            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var job));
            Assert.IsTrue(_coordinator.PreStockCollect(job, carry) > 0);
            Assert.AreEqual(24, _storage.AvailableTotal(crop));

            // Runner despawns mid-walk: the holds release — the crops never moved, so the
            // full amount is available again and nothing entered the fridge
            _coordinator.ReleaseCarried(carry);
            _coordinator.ReleasePreStockJob(job);
            Assert.AreEqual(25, _storage.CountTotal(crop));
            Assert.AreEqual(25, _storage.AvailableTotal(crop));
            Assert.AreEqual(0, _coordinator.FridgeCount(crop));

            // And the trip is claimable again
            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var next));
            Assert.AreEqual(crop, next.Crops[0]);
        }

        [TestMethod]
        public void HeldUnits_ShortGracefullyWhenPlayerSellsThemFirst()
        {
            var crop = FirstRecipeCrop;
            _gameState.RunnerCarryLevel = 3;
            Assert.IsTrue(_storage.TryDeposit(StorageBuildingId, crop, 10));

            var carry = NewCarry();
            _coordinator.RecomputePreStockDeficits();
            Assert.IsTrue(_coordinator.TryClaimPreStockJob(KitchenTaskCoordinator.FridgeTile, out var job));
            Assert.AreEqual(10, _coordinator.PreStockCollect(job, carry));

            // The player force-sells the whole building while the runner walks (ClearBuilding
            // clamps the holds to the new physical reality)
            _storage.ClearBuilding(StorageBuildingId);

            // The unload shorts to zero — no crops conjured from nowhere, no crash
            _coordinator.PreStockDeliver(job, carry);
            Assert.AreEqual(0, _coordinator.FridgeCount(crop));
            Assert.AreEqual(0, _storage.CountTotal(crop));
        }
    }

    /// <summary>
    /// Unit tests for the held-for-transfer reservation ledger on
    /// <see cref="CropStorageInventoryService"/> (issue #386): holds hide units from every
    /// withdraw/count/display path while leaving them physically in place.
    /// </summary>
    [TestClass]
    public class CropStorageReservationTests
    {
        private const int BuildingA = 2;
        private const int BuildingB = 3;
        private static readonly CropType Crop = CropType.Wheat;

        private BuildingService _buildings;
        private CropStorageInventoryService _storage;

        [TestInitialize]
        public void Setup()
        {
            _buildings = new BuildingService();
            _buildings.AddBuilding(new PlacedBuilding
            {
                Type = BuildingType.CropStorage,
                TileX = GameConfig.NewGameCropStorageAnchorTileX,
                TileY = GameConfig.NewGameCropStorageAnchorTileY,
                UniqueId = BuildingA
            });
            _buildings.AddBuilding(new PlacedBuilding
            {
                Type = BuildingType.CropStorage,
                TileX = GameConfig.NewGameCropStorageAnchorTileX + 4,
                TileY = GameConfig.NewGameCropStorageAnchorTileY,
                UniqueId = BuildingB
            });
            _storage = new CropStorageInventoryService(_buildings);
        }

        [TestMethod]
        public void Reserve_HidesUnitsFromWithdrawAndCounts()
        {
            Assert.IsTrue(_storage.TryDeposit(BuildingA, Crop, 10));
            Assert.AreEqual(3, _storage.Reserve(BuildingA, Crop, 3));

            Assert.AreEqual(10, _storage.CountIn(BuildingA, Crop), "physical count is untouched");
            Assert.AreEqual(7, _storage.AvailableIn(BuildingA, Crop));
            Assert.AreEqual(7, _storage.AvailableTotal(Crop));

            Assert.AreEqual(7, _storage.WithdrawUpTo(BuildingA, Crop, 99),
                "withdraw must never touch held units");
            Assert.AreEqual(3, _storage.CountIn(BuildingA, Crop), "only the held units remain");

            Assert.IsFalse(_storage.TryWithdrawAcrossBuildings(Crop, 1),
                "all-or-nothing withdraw must not see held units");
        }

        [TestMethod]
        public void Reserve_GrantsOnlyWhatIsAvailable()
        {
            Assert.IsTrue(_storage.TryDeposit(BuildingA, Crop, 5));
            Assert.AreEqual(5, _storage.Reserve(BuildingA, Crop, 8), "grant caps at availability");
            Assert.AreEqual(0, _storage.Reserve(BuildingA, Crop, 1), "nothing left to hold");
        }

        [TestMethod]
        public void ReleaseReserved_RestoresAvailability()
        {
            Assert.IsTrue(_storage.TryDeposit(BuildingA, Crop, 10));
            _storage.Reserve(BuildingA, Crop, 4);
            _storage.ReleaseReserved(BuildingA, Crop, 4);
            Assert.AreEqual(10, _storage.AvailableIn(BuildingA, Crop));
        }

        [TestMethod]
        public void WithdrawReserved_MovesPhysicalUnitsAndNeverEatsAnotherHold()
        {
            Assert.IsTrue(_storage.TryDeposit(BuildingA, Crop, 10));
            _storage.Reserve(BuildingA, Crop, 3);  // runner 1
            _storage.Reserve(BuildingA, Crop, 4);  // runner 2 (same ledger, separate share)

            Assert.AreEqual(3, _storage.WithdrawReserved(BuildingA, Crop, 3));
            Assert.AreEqual(7, _storage.CountIn(BuildingA, Crop));
            Assert.AreEqual(3, _storage.AvailableIn(BuildingA, Crop),
                "runner 2's 4-unit hold must survive runner 1's unload");
        }

        [TestMethod]
        public void ClearSlot_ClampsHoldsSoUnloadShortsGracefully()
        {
            Assert.IsTrue(_storage.TryDeposit(BuildingA, Crop, 5));
            _storage.Reserve(BuildingA, Crop, 5);

            _storage.ClearSlot(BuildingA, 0); // player sells the stack out from under the hold

            Assert.AreEqual(0, _storage.WithdrawReserved(BuildingA, Crop, 5),
                "a hold whose units vanished must short, not conjure crops");
        }

        [TestMethod]
        public void CopyDisplaySlots_HidesHeldUnits()
        {
            Assert.IsTrue(_storage.TryDeposit(BuildingA, Crop, 10));
            _storage.Reserve(BuildingA, Crop, 4);

            var display = new HarvestSlot[CropStorageInventoryService.SlotsPerBuilding];
            _storage.CopyDisplaySlots(BuildingA, display);

            int shown = 0;
            for (int s = 0; s < display.Length; s++)
                if (!display[s].IsEmpty && display[s].Type == Crop)
                    shown += display[s].Count;
            Assert.AreEqual(6, shown, "the viewer must only show available units");
            Assert.IsTrue(_storage.HasAvailableCrops(BuildingA));

            _storage.Reserve(BuildingA, Crop, 6);
            Assert.IsFalse(_storage.HasAvailableCrops(BuildingA),
                "a fully held building displays as empty");
        }

        [TestMethod]
        public void TakeFromSlot_LeavesHeldUnitsInPlace()
        {
            Assert.IsTrue(_storage.TryDeposit(BuildingA, Crop, 10));
            _storage.Reserve(BuildingA, Crop, 4);

            Assert.AreEqual(6, _storage.TakeFromSlot(BuildingA, 0, 99),
                "selling a stack must only sell its available units");
            Assert.AreEqual(4, _storage.CountIn(BuildingA, Crop),
                "the held units survive the sale for the runner's unload");
            Assert.AreEqual(4, _storage.WithdrawReserved(BuildingA, Crop, 4));
        }
    }
}
