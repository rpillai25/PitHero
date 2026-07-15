using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for AutoSeedPurchaseService. All tests call TryPurchasePass() directly to bypass the
    /// 1-second Time.DeltaTime throttle, which never elapses in a headless test context.
    /// The Enabled=false path is verified via Update() (which returns before touching the timer).
    /// Services are constructed directly (not via Core.Services) for headless safety.
    /// </summary>
    [TestClass]
    public class AutoSeedPurchaseServiceTests
    {
        private CropPlantingService    _cropPlanting  = null!;
        private CropGrowthService      _cropGrowth    = null!;
        private GameStateService       _gameState     = null!;
        private AutoSeedPurchaseService _service      = null!;

        [TestInitialize]
        public void Setup()
        {
            _cropPlanting = new CropPlantingService();
            _cropGrowth   = new CropGrowthService(_cropPlanting);
            _gameState    = new GameStateService();

            // coordinator is null — constructor accepts null for headless tests
            _service = new AutoSeedPurchaseService(_cropPlanting, _cropGrowth, _gameState, coordinator: null);

            // Initialise an empty seed inventory
            _cropPlanting.SeedInventory = new int[CropTypeInfo.Count];
        }

        // ── Default state ────────────────────────────────────────────────────

        [TestMethod]
        public void Service_DisabledByDefault()
        {
            Assert.IsFalse(_service.Enabled, "AutoSeedPurchaseService should be disabled by default");
        }

        [TestMethod]
        public void Service_GoldBufferDefaultIs200()
        {
            Assert.AreEqual(200, _service.GoldBuffer, "GoldBuffer should default to 200");
        }

        // ── Update() when disabled ───────────────────────────────────────────

        [TestMethod]
        public void Update_WhenDisabled_BuysNothing()
        {
            // Place a plan and give the player enough gold
            _cropPlanting.AddPlan(new PlacedCropPlan { Type = CropType.Wheat, TileX = 10, TileY = 5 });
            _gameState.Funds = 10000;

            // Enabled is false (default) — Update should exit immediately
            _service.Update();

            Assert.AreEqual(0, _cropPlanting.SeedInventory[(int)CropType.Wheat],
                "No seeds should be purchased when Enabled=false");
            Assert.AreEqual(10000, _gameState.Funds, "Funds should be unchanged when Enabled=false");
        }

        // ── TryPurchasePass() ────────────────────────────────────────────────

        [TestMethod]
        public void TryPurchasePass_BuysUntilCoverageMet()
        {
            // 2 Wheat plans, 0 seeds, plenty of gold
            // Wheat price = 25; 2 seeds needed
            _cropPlanting.AddPlan(new PlacedCropPlan { Type = CropType.Wheat, TileX = 10, TileY = 5 });
            _cropPlanting.AddPlan(new PlacedCropPlan { Type = CropType.Wheat, TileX = 11, TileY = 5 });
            _gameState.Funds = 10000;

            _service.TryPurchasePass();

            Assert.AreEqual(2, _cropPlanting.SeedInventory[(int)CropType.Wheat],
                "Should buy exactly as many seeds as there are unplanted plans");
            Assert.AreEqual(10000 - 2 * 25, _gameState.Funds, "Should deduct 2× seed price from funds");
        }

        [TestMethod]
        public void TryPurchasePass_RespectsGoldBuffer_NoPurchaseWhenFundsTooLow()
        {
            // Wheat price=25; GoldBuffer=200 (default); Funds=220 → Funds-price=195 < 200 → no buy
            _cropPlanting.AddPlan(new PlacedCropPlan { Type = CropType.Wheat, TileX = 10, TileY = 5 });
            _gameState.Funds = 220;

            _service.TryPurchasePass();

            Assert.AreEqual(0, _cropPlanting.SeedInventory[(int)CropType.Wheat],
                "Should not purchase when Funds - price < GoldBuffer");
            Assert.AreEqual(220, _gameState.Funds, "Funds should be unchanged");
        }

        [TestMethod]
        public void TryPurchasePass_RespectsGoldBuffer_PurchasesWhenFundsExceedBuffer()
        {
            // Wheat price=25; GoldBuffer=200; Funds=226 → Funds-price=201 >= 200 → buy 1
            _cropPlanting.AddPlan(new PlacedCropPlan { Type = CropType.Wheat, TileX = 10, TileY = 5 });
            _gameState.Funds = 226;

            _service.TryPurchasePass();

            Assert.AreEqual(1, _cropPlanting.SeedInventory[(int)CropType.Wheat],
                "Should purchase when Funds - price >= GoldBuffer");
            Assert.AreEqual(201, _gameState.Funds, "Funds should be reduced by the seed price");
        }

        [TestMethod]
        public void TryPurchasePass_TerminatesWhenGoldRunsOutMidDeficit()
        {
            // 5 Wheat plans, only enough gold for 2 seeds (price=25 each, buffer=0)
            _service.GoldBuffer = 0;
            for (int i = 0; i < 5; i++)
                _cropPlanting.AddPlan(new PlacedCropPlan { Type = CropType.Wheat, TileX = 10 + i, TileY = 5 });

            _gameState.Funds = 50; // exactly 2 seeds worth

            _service.TryPurchasePass();

            Assert.AreEqual(2, _cropPlanting.SeedInventory[(int)CropType.Wheat],
                "Should buy as many seeds as funds allow (not infinite loop)");
            Assert.AreEqual(0, _gameState.Funds, "All available funds should be spent");
        }

        [TestMethod]
        public void TryPurchasePass_WithNullCoordinator_DoesNotThrow()
        {
            // coordinator is null (set in Setup); verify no exception is thrown
            _cropPlanting.AddPlan(new PlacedCropPlan { Type = CropType.Wheat, TileX = 10, TileY = 5 });
            _gameState.Funds = 10000;
            _service.GoldBuffer = 0;

            // Should complete without throwing even though coordinator is null
            _service.TryPurchasePass();

            Assert.AreEqual(1, _cropPlanting.SeedInventory[(int)CropType.Wheat],
                "Purchase should succeed even with a null coordinator");
        }

        [TestMethod]
        public void TryPurchasePass_NoPlan_BuysNothing()
        {
            _gameState.Funds = 10000;

            _service.TryPurchasePass();

            // No plans → CountUnplantedPlans returns 0 for all crops → nothing bought
            for (int i = 0; i < CropTypeInfo.Count; i++)
                Assert.AreEqual(0, _cropPlanting.SeedInventory[i],
                    $"No seeds should be bought when there are no plans (crop {i})");
            Assert.AreEqual(10000, _gameState.Funds, "Funds should be unchanged with no plans");
        }

        [TestMethod]
        public void TryPurchasePass_StopsAtSeedInventoryCap()
        {
            // Planned demand exceeds the per-crop cap while owned sits 2 below it — only the
            // 2 seeds of headroom may be bought; the rest would be clamped away and waste gold.
            _service.GoldBuffer = 0;
            int plans = GameConfig.SeedInventoryMaxPerCrop + 6;
            for (int i = 0; i < plans; i++)
                _cropPlanting.AddPlan(new PlacedCropPlan { Type = CropType.Wheat, TileX = i % 100, TileY = i / 100 });
            _cropPlanting.SeedInventory[(int)CropType.Wheat] = GameConfig.SeedInventoryMaxPerCrop - 2;
            _gameState.Funds = 100000;

            _service.TryPurchasePass();

            Assert.AreEqual(GameConfig.SeedInventoryMaxPerCrop, _cropPlanting.SeedInventory[(int)CropType.Wheat],
                "Seed count should stop exactly at the per-crop cap");
            Assert.AreEqual(100000 - 2 * 25, _gameState.Funds,
                "Only the 2 seeds of headroom should have been paid for");
        }

        [TestMethod]
        public void TryPurchasePass_AlreadyCoveredByExistingCrop_BuysNothing()
        {
            // Plan exists but the same-type crop is already growing → CountUnplantedPlans = 0
            var tile = new Point(10, 5);
            _cropPlanting.AddPlan(new PlacedCropPlan { Type = CropType.Wheat, TileX = tile.X, TileY = tile.Y });
            _cropGrowth.PlantCrop(tile, CropType.Wheat, null, null);
            _gameState.Funds = 10000;
            _service.GoldBuffer = 0;

            _service.TryPurchasePass();

            Assert.AreEqual(0, _cropPlanting.SeedInventory[(int)CropType.Wheat],
                "Should not buy seeds when the plan's tile is already occupied by the same crop");
        }
    }
}
