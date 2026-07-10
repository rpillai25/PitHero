using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;
using PitHero.Farming;
using PitHero.Util;

namespace PitHero.Tests
{
    [TestClass]
    public class EconomyBalanceTests
    {
        private static readonly CropType[] AllCrops = new CropType[]
        {
            CropType.AppleTree,
            CropType.Corn,
            CropType.Eggplant,
            CropType.Grapes,
            CropType.Lettuce,
            CropType.Onion,
            CropType.Potato,
            CropType.Pumpkin,
            CropType.Sugarcane,
            CropType.Tomato,
            CropType.Turnip,
            CropType.Watermelon,
            CropType.Wheat,
        };

        [TestMethod]
        public void AllCrops_SteadyStateNetGoldPerGrowthHour_WithinIdleTargetBand()
        {
            for (int i = 0; i < AllCrops.Length; i++)
            {
                CropType crop = AllCrops[i];
                float unit = CropConfig.GetHarvestUnitSellPrice(crop);
                int yield = CropConfig.GetHarvestYield(crop);
                float gross = unit * yield;
                float net = gross - (CropConfig.IsRepeatHarvest(crop) ? 0f : CropConfig.GetSeedPrice(crop));
                float cycleHours = CropConfig.GetIncomeCycleHours(crop);
                float rate = net / cycleHours;
                Assert.IsTrue(rate >= 0.33f, $"{crop}: net rate {rate} is below 0.33");
                Assert.IsTrue(rate <= 0.67f, $"{crop}: net rate {rate} exceeds 0.67");
            }
        }

        [TestMethod]
        public void OneShotCrops_NetProfitPerCycle_IsPositive()
        {
            for (int i = 0; i < AllCrops.Length; i++)
            {
                CropType crop = AllCrops[i];
                if (CropConfig.IsRepeatHarvest(crop))
                    continue;
                float unit = CropConfig.GetHarvestUnitSellPrice(crop);
                int yield = CropConfig.GetHarvestYield(crop);
                int seedPrice = CropConfig.GetSeedPrice(crop);
                Assert.IsTrue(unit * yield > seedPrice, $"{crop}: harvest revenue {unit * yield} does not exceed seed cost {seedPrice}");
            }
        }

        [TestMethod]
        public void RegrowCrops_IncomeCycleHours_MatchGrowthMechanics()
        {
            Assert.AreEqual(13.5f, CropConfig.GetIncomeCycleHours(CropType.Corn));
            Assert.AreEqual(18f, CropConfig.GetIncomeCycleHours(CropType.Tomato));
            Assert.AreEqual(30f, CropConfig.GetIncomeCycleHours(CropType.Eggplant));
            Assert.AreEqual(32f, CropConfig.GetIncomeCycleHours(CropType.Grapes));
            Assert.AreEqual(24f, CropConfig.GetIncomeCycleHours(CropType.AppleTree));
        }

        [TestMethod]
        public void CropNetRates_OrderedByGrowthTier()
        {
            for (int i = 0; i < AllCrops.Length; i++)
            {
                for (int j = 0; j < AllCrops.Length; j++)
                {
                    CropType a = AllCrops[i];
                    CropType b = AllCrops[j];
                    if (CropConfig.GetGrowthTier(a) >= CropConfig.GetGrowthTier(b))
                        continue;

                    float unitA = CropConfig.GetHarvestUnitSellPrice(a);
                    float netA = unitA * CropConfig.GetHarvestYield(a)
                                 - (CropConfig.IsRepeatHarvest(a) ? 0f : CropConfig.GetSeedPrice(a));
                    float rateA = netA / CropConfig.GetIncomeCycleHours(a);

                    float unitB = CropConfig.GetHarvestUnitSellPrice(b);
                    float netB = unitB * CropConfig.GetHarvestYield(b)
                                 - (CropConfig.IsRepeatHarvest(b) ? 0f : CropConfig.GetSeedPrice(b));
                    float rateB = netB / CropConfig.GetIncomeCycleHours(b);

                    Assert.IsTrue(rateA <= rateB + 0.001f,
                        $"{a} (tier {CropConfig.GetGrowthTier(a)}, rate {rateA}) should have rate <= {b} (tier {CropConfig.GetGrowthTier(b)}, rate {rateB})");
                }
            }
        }

        [TestMethod]
        public void HarvestUnitSellPrice_NeverBelowFloor()
        {
            for (int i = 0; i < AllCrops.Length; i++)
            {
                CropType crop = AllCrops[i];
                float unit = CropConfig.GetHarvestUnitSellPrice(crop);
                Assert.IsTrue(unit >= CropConfig.HarvestUnitSellFloor,
                    $"{crop}: unit sell price {unit} is below floor {CropConfig.HarvestUnitSellFloor}");
            }
        }

        [TestMethod]
        public void HarvestStackSellPrice_CeilsFractionalUnits()
        {
            Assert.AreEqual(55, CropConfig.GetHarvestStackSellPrice(CropType.Turnip, 9));
            Assert.AreEqual(45, CropConfig.GetHarvestStackSellPrice(CropType.Corn, 20));
        }

        [TestMethod]
        public void GearSellPrice_IncreasesWithRarity_AndStaysBelowBuyPrice()
        {
            ItemRarity[] rarities = new ItemRarity[]
            {
                ItemRarity.Normal,
                ItemRarity.Uncommon,
                ItemRarity.Rare,
                ItemRarity.Epic,
                ItemRarity.Legendary,
            };
            int[] expected = new int[] { 100, 175, 250, 300, 375 };
            int prev = -1;
            for (int i = 0; i < rarities.Length; i++)
            {
                var gear = new Gear("T", ItemKind.WeaponSword, rarities[i], "D", 500, new StatBlock(0, 0, 0, 0));
                int sell = gear.GetSellPrice();
                Assert.AreEqual(expected[i], sell, $"Rarity {rarities[i]}: expected sell {expected[i]} but got {sell}");
                Assert.IsTrue(sell < 500, $"Rarity {rarities[i]}: sell price {sell} is not below buy price 500");
                Assert.IsTrue(sell > prev, $"Rarity {rarities[i]}: sell price {sell} did not increase from previous {prev}");
                prev = sell;
            }
        }

        [TestMethod]
        public void RepresentativeGear_SellValues()
        {
            Assert.AreEqual(10, GearItems.RustyBlade().GetSellPrice());
            Assert.AreEqual(192, GearItems.GloomBlade().GetSellPrice());
            Assert.AreEqual(337, GearItems.AbyssFang().GetSellPrice());
            Assert.AreEqual(450, GearItems.PitLordsSword().GetSellPrice());
        }

        [TestMethod]
        public void ConsumableSellPrice_RemainsHalfBuyPrice()
        {
            var hp = new HPPotion();
            var fullMix = new FullMixPotion();
            Assert.AreEqual(10, hp.GetSellPrice());
            Assert.AreEqual(450, fullMix.GetSellPrice());
        }
    }
}
