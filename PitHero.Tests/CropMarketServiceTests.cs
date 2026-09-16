using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.Tests
{
    /// <summary>
    /// Crop market saturation (issue #417): demand drops with the player's own sales, recovers at
    /// a constant rate, floors, and settles to a steady state that depends only on how many plots'
    /// worth of a crop is being sold — never on the crop's tier.
    /// </summary>
    [TestClass]
    public class CropMarketServiceTests
    {
        [TestMethod]
        public void NewMarket_EveryCropAtFullDemand_PaysBasePrice()
        {
            var market = new CropMarketService();
            for (int i = 0; i < CropTypeInfo.Count; i++)
            {
                var crop = (CropType)i;
                Assert.AreEqual(1f, market.GetDemand(crop));
                Assert.AreEqual(CropConfig.GetHarvestStackSellPrice(crop, 7), market.GetStackSellPrice(crop, 7));
            }
        }

        [TestMethod]
        public void RecordSale_LowersDemand_ProportionalToBaseGold()
        {
            var market = new CropMarketService();
            int units = 10;
            float baseGold = CropConfig.GetHarvestUnitSellPrice(CropType.Wheat) * units;
            float expected = 1f - baseGold / CropMarketService.GetDepthGold(CropType.Wheat);

            market.RecordSale(CropType.Wheat, units);

            Assert.AreEqual(expected, market.GetDemand(CropType.Wheat), 0.0001f);
            Assert.AreEqual(1f, market.GetDemand(CropType.Corn), "other crops are untouched");
        }

        [TestMethod]
        public void RecordSale_NeverDropsBelowFloor()
        {
            var market = new CropMarketService();
            market.RecordSale(CropType.Pumpkin, 100_000);
            Assert.AreEqual(GameConfig.MarketDemandFloor, market.GetDemand(CropType.Pumpkin));
            Assert.IsTrue(market.GetStackSellPrice(CropType.Pumpkin, 1) >= 1, "a unit always sells for at least 1 gold");
        }

        [TestMethod]
        public void Update_RecoversTowardFullDemand()
        {
            var market = new CropMarketService();
            market.RecordSale(CropType.Wheat, 1000);
            float before = market.GetDemand(CropType.Wheat);
            market.Update(1f);
            float afterOneHour = market.GetDemand(CropType.Wheat);
            Assert.IsTrue(afterOneHour > before);
            Assert.AreEqual(before + (1f - before) * GameConfig.MarketRecoveryPerHour, afterOneHour, 0.0001f);

            for (int h = 0; h < 200; h++)
                market.Update(1f);
            Assert.AreEqual(1f, market.GetDemand(CropType.Wheat), 0.001f, "demand converges back to full");
        }

        /// <summary>
        /// Sells N plots' worth of a crop every in-game hour for three days and returns the settled demand.
        /// One plant grosses about its profit-per-growth-hour every hour, so the base gold sold per
        /// hour is N x profitPerGrowthHour.
        /// </summary>
        private static float SettledDemandForPlots(CropType crop, int plots)
        {
            var market = new CropMarketService();
            float unit = CropConfig.GetHarvestUnitSellPrice(crop);
            float unitsPerMinute = plots * CropConfig.GetProfitPerGrowthHour(crop) / unit / 60f;
            float pending = 0f;
            for (int minute = 0; minute < 72 * 60; minute++)
            {
                pending += unitsPerMinute;
                if (pending >= 1f)
                {
                    int units = (int)pending;
                    market.RecordSale(crop, units);
                    pending -= units;
                }
                market.Update(1f / 60f);
            }
            return market.GetDemand(crop);
        }

        [TestMethod]
        public void SteadyState_MatchesOneMinusPlotsOverSaturation()
        {
            int plots = 20;
            float expected = 1f - plots / GameConfig.MarketSaturationPlots;
            float settled = SettledDemandForPlots(CropType.Turnip, plots);
            Assert.AreEqual(expected, settled, 0.05f, $"20 turnip plots should settle near {expected}, got {settled}");
        }

        [TestMethod]
        public void SteadyState_IsIndependentOfTier()
        {
            float wheat = SettledDemandForPlots(CropType.Wheat, 30);
            float apple = SettledDemandForPlots(CropType.AppleTree, 30);
            Assert.AreEqual(wheat, apple, 0.05f, "30 plots of any crop settle to the same demand");
        }

        [TestMethod]
        public void Monoculture_SettlesOnTheFloor()
        {
            float settled = SettledDemandForPlots(CropType.AppleTree, 100);
            Assert.AreEqual(GameConfig.MarketDemandFloor, settled, 0.02f);
        }

        [TestMethod]
        public void SetDemand_ClampsAndTolerantOfShortArrays()
        {
            var market = new CropMarketService();
            market.SetDemand(new float[] { 5f, -1f, 0.5f });
            Assert.AreEqual(1f, market.GetDemand((CropType)0));
            Assert.AreEqual(GameConfig.MarketDemandFloor, market.GetDemand((CropType)1));
            Assert.AreEqual(0.5f, market.GetDemand((CropType)2));
            Assert.AreEqual(1f, market.GetDemand((CropType)3), "missing entries read as full demand");
            market.SetDemand(null);
            Assert.AreEqual(1f, market.GetDemand((CropType)2));
        }

        [TestMethod]
        public void CopyDemand_RoundTripsThroughSetDemand()
        {
            var market = new CropMarketService();
            market.RecordSale(CropType.Onion, 50);
            var copy = market.CopyDemand();
            var restored = new CropMarketService();
            restored.SetDemand(copy);
            Assert.AreEqual(market.GetDemand(CropType.Onion), restored.GetDemand(CropType.Onion));
        }

        [TestMethod]
        public void CropSellPricing_Headless_PaysBasePrice_AndRecordsNothing()
        {
            Assert.AreEqual(CropConfig.GetHarvestStackSellPrice(CropType.Wheat, 20), CropSellPricing.GetStackSellPrice(CropType.Wheat, 20));
            Assert.AreEqual(100, CropSellPricing.GetDemandPercent(CropType.Wheat));
            CropSellPricing.RecordSale(CropType.Wheat, 20); // no market: must not throw
        }

        [TestMethod]
        public void AutoCropSell_UsesMarketPrice_AndRecordsSale()
        {
            var storage = new CropStorageInventoryService(buildingService: null);
            var gameState = new GameStateService();
            var market = new CropMarketService();
            var service = new AutoCropSellService(storage, gameState, market);
            service.Enabled = true;
            market.RecordSale(CropType.Wheat, 5000); // saturate first so the discount is visible
            float demandBefore = market.GetDemand(CropType.Wheat);

            int max = CropConfig.GetMaxHarvestStack(CropType.Wheat);
            for (int i = 0; i < max; i++)
                storage.TryDeposit(1, CropType.Wheat);
            service.TrySellPass();

            Assert.IsTrue(gameState.Funds > 0 && gameState.Funds < CropConfig.GetHarvestStackSellPrice(CropType.Wheat, max),
                "a saturated crop sells below base price");
            Assert.IsTrue(market.GetDemand(CropType.Wheat) <= demandBefore, "the sale is recorded against demand");
        }
    }
}
