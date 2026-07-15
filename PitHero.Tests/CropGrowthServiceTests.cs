using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for CropGrowthService.GetGrowthProgress.
    /// RestoreAll is used with null scene/atlas for headless setup (no Nez Core required).
    /// </summary>
    [TestClass]
    public class CropGrowthServiceTests
    {
        private CropPlantingService _cropPlanting = null!;
        private CropGrowthService _growth = null!;

        [TestInitialize]
        public void Setup()
        {
            _cropPlanting = new CropPlantingService();
            _growth       = new CropGrowthService(_cropPlanting);
        }

        // ── Helper: Restore a crop into the service without a Scene or SpriteAtlas ──

        private void RestoreCrop(Point tile, CropType type, float accumulatedHours,
            int currentFrame, float regrowthMultiplier = 1f)
        {
            var states = new List<SavedCropGrowthState>
            {
                new SavedCropGrowthState
                {
                    TileX = tile.X,
                    TileY = tile.Y,
                    CropTypeId = (int)type,
                    AccumulatedHours = accumulatedHours,
                    CurrentFrame = currentFrame,
                    RegrowthRateMultiplier = regrowthMultiplier,
                }
            };
            _growth.RestoreAll(states, scene: null, atlas: null);
        }

        // ── GetGrowthProgress ────────────────────────────────────────────────

        [TestMethod]
        public void GetGrowthProgress_NoCrop_ReturnsNegativeOne()
        {
            float progress = _growth.GetGrowthProgress(new Point(5, 5));

            Assert.AreEqual(-1f, progress, "GetGrowthProgress should return -1 when no crop exists at the tile");
        }

        [TestMethod]
        public void GetGrowthProgress_FreshlyPlanted_ReturnsZero()
        {
            // Wheat: maxFrame=5, hoursPerStage=2, totalHours=8
            var tile = new Point(5, 5);
            RestoreCrop(tile, CropType.Wheat, accumulatedHours: 0f, currentFrame: 1);

            float progress = _growth.GetGrowthProgress(tile);

            Assert.AreEqual(0f, progress, "Freshly planted crop (AccumulatedHours=0) should report 0 progress");
        }

        [TestMethod]
        public void GetGrowthProgress_FullyGrown_ReturnsOne()
        {
            // Wheat: maxFrame=5, hoursPerStage=2, totalHours=(5-1)*2*1=8
            var tile = new Point(5, 5);
            int maxFrame = CropConfig.GetFrameCount(CropType.Wheat);
            float hoursPerStage = CropConfig.GetHoursPerStage(CropType.Wheat);
            float totalHours = (maxFrame - 1) * hoursPerStage;  // 8f
            RestoreCrop(tile, CropType.Wheat, accumulatedHours: totalHours, currentFrame: maxFrame);

            float progress = _growth.GetGrowthProgress(tile);

            Assert.AreEqual(1f, progress, 0.001f, "Crop at full hours should report progress 1");
        }

        [TestMethod]
        public void GetGrowthProgress_HalfGrown_ReturnsHalf()
        {
            // Wheat: totalHours=8, half=4
            var tile = new Point(6, 5);
            int maxFrame = CropConfig.GetFrameCount(CropType.Wheat);
            float hoursPerStage = CropConfig.GetHoursPerStage(CropType.Wheat);
            float totalHours = (maxFrame - 1) * hoursPerStage; // 8f
            RestoreCrop(tile, CropType.Wheat, accumulatedHours: totalHours / 2f, currentFrame: 3);

            float progress = _growth.GetGrowthProgress(tile);

            Assert.AreEqual(0.5f, progress, 0.001f, "Crop at half the total hours should report 0.5 progress");
        }

        [TestMethod]
        public void GetGrowthProgress_RespectsRegrowthRateMultiplier()
        {
            // Corn: maxFrame=10, hoursPerStage=3, multiplier=1.5
            // totalHours = (10-1)*3*1.5 = 40.5
            // At 20.25 hours → progress ≈ 0.5
            var tile = new Point(7, 5);
            int maxFrame = CropConfig.GetFrameCount(CropType.Corn);
            float hoursPerStage = CropConfig.GetHoursPerStage(CropType.Corn);
            float multiplier = 1.5f;
            float totalHours = (maxFrame - 1) * hoursPerStage * multiplier; // 40.5

            RestoreCrop(tile, CropType.Corn, accumulatedHours: totalHours / 2f, currentFrame: 1,
                regrowthMultiplier: multiplier);

            float progress = _growth.GetGrowthProgress(tile);

            Assert.AreEqual(0.5f, progress, 0.001f,
                "GetGrowthProgress should scale by RegrowthRateMultiplier (Corn at 1.5×)");
        }

        [TestMethod]
        public void GetGrowthProgress_RegrowthMultiplierOne_MatchesBaseline()
        {
            // Lettuce: maxFrame=5, hoursPerStage=2, multiplier=1 (no regrowth override)
            // totalHours = (5-1)*2*1 = 8; at 8h → progress=1
            var tile = new Point(8, 5);
            int maxFrame = CropConfig.GetFrameCount(CropType.Lettuce);
            float hoursPerStage = CropConfig.GetHoursPerStage(CropType.Lettuce);
            float totalHours = (maxFrame - 1) * hoursPerStage;

            RestoreCrop(tile, CropType.Lettuce, accumulatedHours: totalHours, currentFrame: maxFrame,
                regrowthMultiplier: 1f);

            float progress = _growth.GetGrowthProgress(tile);

            Assert.AreEqual(1f, progress, 0.001f, "Multiplier=1 should produce same result as no multiplier");
        }
    }
}
