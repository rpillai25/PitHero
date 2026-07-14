using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.Farming;
using PitHero.Services;

namespace PitHero.Tests
{
    [TestClass]
    public class CropPlantingServiceTests
    {
        private CropPlantingService _service = null!;
        private CropGrowthService _growth = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new CropPlantingService();
            _growth  = new CropGrowthService(_service);
        }

        // ── CountUnplantedPlans ───────────────────────────────────────────────

        /// <summary>A plan on a tile that has no crop at all counts as an unplanted plan.</summary>
        [TestMethod]
        public void CountUnplantedPlans_PlanWithNoCrop_Counts()
        {
            _service.AddPlan(new PlacedCropPlan { Type = CropType.Wheat, TileX = 10, TileY = 5 });

            int count = _service.CountUnplantedPlans(CropType.Wheat, _growth);

            Assert.AreEqual(1, count, "Plan on empty tile should count as unplanted");
        }

        /// <summary>A plan whose tile already has the same-type crop growing does not count.</summary>
        [TestMethod]
        public void CountUnplantedPlans_PlanWithSameTypeCrop_DoesNotCount()
        {
            var tile = new Point(10, 5);
            _service.AddPlan(new PlacedCropPlan { Type = CropType.Wheat, TileX = tile.X, TileY = tile.Y });
            // Plant the same crop type (null scene/atlas is fine for headless)
            _growth.PlantCrop(tile, CropType.Wheat, null, null);

            int count = _service.CountUnplantedPlans(CropType.Wheat, _growth);

            Assert.AreEqual(0, count, "Plan whose tile already has the same-type crop should not count");
        }

        /// <summary>A plan whose tile has a DIFFERENT-type crop still counts (needs a seed for the swap).</summary>
        [TestMethod]
        public void CountUnplantedPlans_PlanWithDifferentTypeCrop_Counts()
        {
            var tile = new Point(10, 5);
            _service.AddPlan(new PlacedCropPlan { Type = CropType.Corn, TileX = tile.X, TileY = tile.Y });
            // Plant a different crop type at the same tile
            _growth.PlantCrop(tile, CropType.Wheat, null, null);

            int count = _service.CountUnplantedPlans(CropType.Corn, _growth);

            Assert.AreEqual(1, count, "Plan whose tile has a different-type crop should still count");
        }

        // ── ConsumeSeed ───────────────────────────────────────────────────────

        [TestMethod]
        public void ConsumeSeed_WithSufficientSeeds_DecrementsAndReturnsTrue()
        {
            _service.SeedInventory = new int[CropTypeInfo.Count];
            _service.SeedInventory[(int)CropType.Wheat] = 3;

            bool result = _service.ConsumeSeed(CropType.Wheat);

            Assert.IsTrue(result, "ConsumeSeed should return true when seeds are available");
            Assert.AreEqual(2, _service.SeedInventory[(int)CropType.Wheat], "Seed count should be decremented by 1");
        }

        [TestMethod]
        public void ConsumeSeed_WithZeroSeeds_ReturnsFalseWithoutDecrementing()
        {
            _service.SeedInventory = new int[CropTypeInfo.Count];
            _service.SeedInventory[(int)CropType.Corn] = 0;

            bool result = _service.ConsumeSeed(CropType.Corn);

            Assert.IsFalse(result, "ConsumeSeed should return false when count is 0");
            Assert.AreEqual(0, _service.SeedInventory[(int)CropType.Corn], "Seed count must not go below 0");
        }

        [TestMethod]
        public void ConsumeSeed_WithNullInventory_ReturnsFalse()
        {
            // SeedInventory is null by default
            bool result = _service.ConsumeSeed(CropType.Wheat);

            Assert.IsFalse(result, "ConsumeSeed should return false when SeedInventory is null");
        }

        // ── HasSeeds ─────────────────────────────────────────────────────────

        [TestMethod]
        public void HasSeeds_WithSeedInInventory_ReturnsTrue()
        {
            _service.SeedInventory = new int[CropTypeInfo.Count];
            _service.SeedInventory[(int)CropType.Potato] = 5;

            Assert.IsTrue(_service.HasSeeds(CropType.Potato));
        }

        [TestMethod]
        public void HasSeeds_WithZeroCount_ReturnsFalse()
        {
            _service.SeedInventory = new int[CropTypeInfo.Count];
            _service.SeedInventory[(int)CropType.Potato] = 0;

            Assert.IsFalse(_service.HasSeeds(CropType.Potato));
        }

        [TestMethod]
        public void HasSeeds_WithNullInventory_ReturnsFalse()
        {
            Assert.IsFalse(_service.HasSeeds(CropType.Potato));
        }

        // ── RemovePlan ───────────────────────────────────────────────────────

        [TestMethod]
        public void RemovePlan_ReturnsTheCropType()
        {
            _service.AddPlan(new PlacedCropPlan { Type = CropType.Tomato, TileX = 7, TileY = 3 });

            CropType? removed = _service.RemovePlan(new Point(7, 3));

            Assert.AreEqual(CropType.Tomato, removed, "RemovePlan should return the plan's crop type");
        }

        [TestMethod]
        public void RemovePlan_DoesNotTouchSeedInventory()
        {
            _service.SeedInventory = new int[CropTypeInfo.Count];
            _service.SeedInventory[(int)CropType.Tomato] = 4;
            _service.AddPlan(new PlacedCropPlan { Type = CropType.Tomato, TileX = 7, TileY = 3 });

            _service.RemovePlan(new Point(7, 3));

            // Plans are permanent blueprints — removing one must not refund seeds
            Assert.AreEqual(4, _service.SeedInventory[(int)CropType.Tomato],
                "Removing a plan must not modify the seed inventory");
        }

        [TestMethod]
        public void RemovePlan_NonExistentTile_ReturnsNull()
        {
            CropType? result = _service.RemovePlan(new Point(99, 99));

            Assert.IsNull(result, "RemovePlan on a tile with no plan should return null");
        }
    }
}
