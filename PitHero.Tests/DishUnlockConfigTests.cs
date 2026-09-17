using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Dining;
using PitHero.Farming;
using PitHero.Services;

namespace PitHero.Tests
{
    /// <summary>
    /// Dish progression table (issue #417): a dish's tier is its highest recipe-crop tier, tier 0
    /// dishes are free, soft unlock follows the crops, full unlock needs the cooking requirements.
    /// </summary>
    [TestClass]
    public class DishUnlockConfigTests
    {
        private static int[] CropTotalsUnlockingTier(int tier)
        {
            // Harvest totals meeting every crop requirement up to and including the given crop tier
            var totals = new int[CropTypeInfo.Count];
            for (int c = 0; c < CropTypeInfo.Count; c++)
            {
                var reqs = CropUnlockConfig.GetRequirements((CropType)c);
                if (CropUnlockConfig.GetTier((CropType)c) > tier)
                    continue;
                for (int r = 0; r < reqs.Length; r++)
                    if (totals[(int)reqs[r].Crop] < reqs[r].Required)
                        totals[(int)reqs[r].Crop] = reqs[r].Required;
            }
            return totals;
        }

        private static int[] DishTotalsMeeting(DishType dish)
        {
            var totals = new int[DishTypeInfo.Count];
            var reqs = DishUnlockConfig.GetRequirements(dish);
            for (int r = 0; r < reqs.Length; r++)
                totals[(int)reqs[r].Dish] = reqs[r].Required;
            return totals;
        }

        [TestMethod]
        public void ProgressionOrder_ListsEveryDishOnce_ByNonDecreasingTier()
        {
            var order = DishUnlockConfig.ProgressionOrder;
            Assert.AreEqual(DishTypeInfo.Count, order.Length);
            var seen = new bool[DishTypeInfo.Count];
            int lastTier = 0;
            for (int i = 0; i < order.Length; i++)
            {
                Assert.IsFalse(seen[(int)order[i]], $"{order[i]} listed twice");
                seen[(int)order[i]] = true;
                int tier = DishUnlockConfig.GetTier(order[i]);
                Assert.IsTrue(tier >= lastTier, $"{order[i]} (tier {tier}) is out of order");
                lastTier = tier;
            }
        }

        [TestMethod]
        public void GetTier_EqualsMaxRecipeCropTier_ForEveryDish()
        {
            for (int d = 0; d < DishTypeInfo.Count; d++)
            {
                var dish = (DishType)d;
                Assert.AreEqual(DishUnlockConfig.ComputeTier(dish), DishUnlockConfig.GetTier(dish));
                var def = DishConfig.GetDefinition(dish);
                int max = 0;
                for (int i = 0; i < def.Recipe.Length; i++)
                    max = System.Math.Max(max, CropUnlockConfig.GetTier(def.Recipe[i].Crop));
                Assert.AreEqual(max, DishUnlockConfig.GetTier(dish), $"{dish}");
            }
        }

        [TestMethod]
        public void StarterDishes_ParallelTheStarterCrops()
        {
            Assert.AreEqual(0, DishUnlockConfig.GetTier(DishType.ButteredBread));
            Assert.AreEqual(0, DishUnlockConfig.GetTier(DishType.GrilledCornWithButter));
            var noCrops = new int[CropTypeInfo.Count];
            var noDishes = new int[DishTypeInfo.Count];
            Assert.IsTrue(DishUnlockConfig.IsFullyUnlocked(DishType.ButteredBread, noCrops, noDishes));
            Assert.IsTrue(DishUnlockConfig.IsFullyUnlocked(DishType.GrilledCornWithButter, noCrops, noDishes));
            Assert.AreEqual(0, DishUnlockConfig.GetRequirements(DishType.ButteredBread).Length);

            int mask = DishUnlockConfig.GetFullyUnlockedMask(noCrops, noDishes);
            Assert.AreEqual((1 << (int)DishType.ButteredBread) | (1 << (int)DishType.GrilledCornWithButter), mask,
                "only the two starter dishes are orderable on a fresh farm");
        }

        [TestMethod]
        public void TierOne_NeedsTenBreadAndTenCorn()
        {
            var reqs = DishUnlockConfig.GetRequirements(DishType.TomatoCheeseBisque);
            Assert.AreEqual(2, reqs.Length);
            Assert.AreEqual(DishType.ButteredBread, reqs[0].Dish);
            Assert.AreEqual(GameConfig.DishUnlockServingsBase, reqs[0].Required);
            Assert.AreEqual(DishType.GrilledCornWithButter, reqs[1].Dish);
            Assert.AreEqual(GameConfig.DishUnlockServingsBase, reqs[1].Required);
        }

        [TestMethod]
        public void Requirements_EscalateWithTierGap_CappedAtThree()
        {
            // Apple Pie (tier 6) needs 30 of each tier 0-3 dish, 20 of each tier 4, 10 of each tier 5
            var reqs = DishUnlockConfig.GetRequirements(DishType.ApplePie);
            for (int i = 0; i < reqs.Length; i++)
            {
                int gap = 6 - DishUnlockConfig.GetTier(reqs[i].Dish);
                int expected = GameConfig.DishUnlockServingsBase * System.Math.Min(gap, GameConfig.DishUnlockTierGapCap);
                Assert.AreEqual(expected, reqs[i].Required, $"{reqs[i].Dish}");
            }
            Assert.AreEqual(DishTypeInfo.Count - 1, reqs.Length, "every other dish is a prerequisite of the last one");
        }

        [TestMethod]
        public void SoftUnlock_RequiresEveryRecipeCrop()
        {
            var noDishes = new int[DishTypeInfo.Count];
            var tier0Crops = CropTotalsUnlockingTier(0);
            Assert.IsFalse(DishUnlockConfig.IsSoftUnlocked(DishType.TomatoCheeseBisque, tier0Crops), "Tomato is locked");
            var tier1Crops = CropTotalsUnlockingTier(1);
            Assert.IsTrue(DishUnlockConfig.IsSoftUnlocked(DishType.TomatoCheeseBisque, tier1Crops));
            Assert.IsFalse(DishUnlockConfig.IsFullyUnlocked(DishType.TomatoCheeseBisque, tier1Crops, noDishes),
                "soft-unlocked but nothing served yet");
        }

        [TestMethod]
        public void FullUnlock_NeedsCropsAndServings()
        {
            var tier1Crops = CropTotalsUnlockingTier(1);
            var served = DishTotalsMeeting(DishType.TomatoCheeseBisque);
            Assert.IsTrue(DishUnlockConfig.IsFullyUnlocked(DishType.TomatoCheeseBisque, tier1Crops, served));
            served[(int)DishType.ButteredBread]--;
            Assert.IsFalse(DishUnlockConfig.IsFullyUnlocked(DishType.TomatoCheeseBisque, tier1Crops, served));
            // Servings alone never unlock a dish whose crops are locked
            Assert.IsFalse(DishUnlockConfig.IsFullyUnlocked(DishType.TomatoCheeseBisque, CropTotalsUnlockingTier(0),
                DishTotalsMeeting(DishType.TomatoCheeseBisque)));
        }

        [TestMethod]
        public void Tracker_RecordsServings_AndHeadlessIsNeverLocked()
        {
            var gameState = new GameStateService();
            DishUnlockTracker.RecordDishServed(gameState, DishType.ButteredBread);
            Assert.AreEqual(1, gameState.DishesServedTotals[(int)DishType.ButteredBread]);
            DishUnlockTracker.RecordDishServed(null, DishType.ButteredBread); // no-op, no throw

            Assert.IsTrue(DishUnlockTracker.IsFullyUnlocked(DishType.ApplePie), "no scene => nothing is locked");
            Assert.IsFalse(DishUnlockTracker.IsFullyUnlocked(gameState, DishType.ApplePie), "explicit state => gated");
        }
    }
}
