using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;
using PitHero.Farming;
using PitHero.Util;

namespace PitHero.Tests
{
    /// <summary>
    /// Crop economy invariants (issue #417): profit per growth hour is a pure function of the
    /// crop's progression tier, every one-shot harvest funds at least two more plantings, and the
    /// pacing anchors (modest starter patch, top-tier farm printing gold) hold before market demand.
    /// </summary>
    [TestClass]
    public class EconomyBalanceTests
    {
        /// <summary>Wet growth hours one plant accrues per real hour (Wet clears at 6 AM, re-watered promptly).</summary>
        private const float GrowthHoursPerRealHour = 55f;

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

        private static float NetRatePerGrowthHour(CropType crop)
        {
            float unit = CropConfig.GetHarvestUnitSellPrice(crop);
            int yield = CropConfig.GetHarvestYield(crop);
            float net = unit * yield - (CropConfig.IsRepeatHarvest(crop) ? 0f : CropConfig.GetSeedPrice(crop));
            return net / CropConfig.GetIncomeCycleHours(crop);
        }

        private static float GoldPerPlantRealHour(CropType crop) => NetRatePerGrowthHour(crop) * GrowthHoursPerRealHour;

        [TestMethod]
        public void AllCrops_NetGoldPerGrowthHour_EqualsTierEntry()
        {
            for (int i = 0; i < AllCrops.Length; i++)
            {
                CropType crop = AllCrops[i];
                float expected = CropConfig.TierProfitPerGrowthHour[CropUnlockConfig.GetTier(crop)];
                Assert.AreEqual(expected, NetRatePerGrowthHour(crop), 0.01f, $"{crop}: net rate must equal its tier entry");
            }
        }

        [TestMethod]
        public void TierProfitTable_CoversEveryTier_AndRisesMonotonically()
        {
            Assert.AreEqual(CropUnlockConfig.TierCount, CropConfig.TierProfitPerGrowthHour.Length);
            for (int t = 1; t < CropConfig.TierProfitPerGrowthHour.Length; t++)
                Assert.IsTrue(CropConfig.TierProfitPerGrowthHour[t] > CropConfig.TierProfitPerGrowthHour[t - 1],
                    $"tier {t} must out-earn tier {t - 1}");
        }

        [TestMethod]
        public void OneShotCrops_FirstHarvestFundsAtLeastTwoReplantings()
        {
            for (int i = 0; i < AllCrops.Length; i++)
            {
                CropType crop = AllCrops[i];
                if (CropConfig.IsRepeatHarvest(crop))
                    continue;
                float profit = CropConfig.GetCycleProfit(crop);
                int seedPrice = CropConfig.GetSeedPrice(crop);
                Assert.IsTrue(profit >= 2f * seedPrice - 0.01f,
                    $"{crop}: cycle profit {profit} must be at least twice the seed price {seedPrice}");
            }
        }

        [TestMethod]
        public void RegrowCrops_SeedPaysBackWithinTwoCycles()
        {
            for (int i = 0; i < AllCrops.Length; i++)
            {
                CropType crop = AllCrops[i];
                if (!CropConfig.IsRepeatHarvest(crop))
                    continue;
                float profit = CropConfig.GetCycleProfit(crop);
                Assert.IsTrue(CropConfig.GetSeedPrice(crop) <= 2f * profit,
                    $"{crop}: seed {CropConfig.GetSeedPrice(crop)} should pay back within two cycles of {profit}");
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
        public void CropNetRates_OrderedByProgressionTier()
        {
            for (int i = 0; i < AllCrops.Length; i++)
            {
                for (int j = 0; j < AllCrops.Length; j++)
                {
                    CropType a = AllCrops[i];
                    CropType b = AllCrops[j];
                    if (CropUnlockConfig.GetTier(a) >= CropUnlockConfig.GetTier(b))
                        continue;
                    Assert.IsTrue(NetRatePerGrowthHour(a) < NetRatePerGrowthHour(b),
                        $"{a} (tier {CropUnlockConfig.GetTier(a)}) must earn less per hour than {b} (tier {CropUnlockConfig.GetTier(b)})");
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
            // Wheat: (10 profit + 5 seed) / 1 = 15 per unit
            Assert.AreEqual(15, CropConfig.GetHarvestStackSellPrice(CropType.Wheat, 1));
            Assert.AreEqual(300, CropConfig.GetHarvestStackSellPrice(CropType.Wheat, 20));
            // Corn: 16.875 / 3 = 5.625 per unit -> 20 units = 112.5 -> 113
            Assert.AreEqual(113, CropConfig.GetHarvestStackSellPrice(CropType.Corn, 20));
        }

        [TestMethod]
        public void StarterPatch_IsModest()
        {
            // 12 wheat + 6 corn at perfect uptime and full demand: pocket money, not wealth
            float perHour = 12 * GoldPerPlantRealHour(CropType.Wheat) + 6 * GoldPerPlantRealHour(CropType.Corn);
            Assert.IsTrue(perHour >= 600f && perHour <= 2000f, $"starter patch earns {perHour} g per real hour");
        }

        [TestMethod]
        public void TopTierFarm_HitsPacingAnchor()
        {
            // 100 plots of the best crops, before market demand, must clear ~100k g per real hour
            // (a diversified farm lands near 0.8 demand, which the economy simulation verifies)
            float perHour = 100 * GoldPerPlantRealHour(CropType.AppleTree);
            Assert.IsTrue(perHour >= 100_000f, $"100 apple trees earn {perHour} g per real hour at full demand");
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
