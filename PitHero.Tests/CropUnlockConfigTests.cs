using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Farming;

namespace PitHero.Tests
{
    /// <summary>Crop progression table (issue #413): tiers, requirements and the unlocked mask.</summary>
    [TestClass]
    public class CropUnlockConfigTests
    {
        private static int[] Totals(params (CropType crop, int units)[] entries)
        {
            var totals = new int[CropTypeInfo.Count];
            for (int i = 0; i < entries.Length; i++)
                totals[(int)entries[i].crop] = entries[i].units;
            return totals;
        }

        [TestMethod]
        public void StartingCrops_AreUnlockedWithNothingHarvested()
        {
            Assert.IsTrue(CropUnlockConfig.IsUnlocked(CropType.Wheat, null));
            Assert.IsTrue(CropUnlockConfig.IsUnlocked(CropType.Corn, new int[0]));
            Assert.IsFalse(CropUnlockConfig.IsUnlocked(CropType.Tomato, null));
            Assert.IsFalse(CropUnlockConfig.IsUnlocked(CropType.AppleTree, null));
            Assert.AreEqual(0, CropUnlockConfig.GetTier(CropType.Wheat));
            Assert.AreEqual(0, CropUnlockConfig.GetRequirements(CropType.Corn).Length);
        }

        [TestMethod]
        public void TierOne_NeedsNineWheatAndNineCorn()
        {
            Assert.IsFalse(CropUnlockConfig.IsUnlocked(CropType.Tomato, Totals((CropType.Wheat, 9), (CropType.Corn, 8))));
            Assert.IsTrue(CropUnlockConfig.IsUnlocked(CropType.Tomato, Totals((CropType.Wheat, 9), (CropType.Corn, 9))));
            Assert.IsTrue(CropUnlockConfig.IsUnlocked(CropType.Eggplant, Totals((CropType.Wheat, 50), (CropType.Corn, 9))));
            Assert.AreEqual(1, CropUnlockConfig.GetTier(CropType.Eggplant));
        }

        [TestMethod]
        public void EveryTier_MatchesTheIssueTable()
        {
            Assert.AreEqual(7, CropUnlockConfig.TierCount);
            Assert.AreEqual(13, CropUnlockConfig.ProgressionOrder.Length);
            Assert.AreEqual(6, CropUnlockConfig.GetTier(CropType.AppleTree));

            var apple = CropUnlockConfig.GetRequirements(CropType.AppleTree);
            Assert.AreEqual(12, apple.Length);
            Assert.AreEqual(CropType.Pumpkin, apple[11].Crop);
            Assert.AreEqual(20, apple[11].Required);
            Assert.AreEqual(64, CropUnlockConfig.GetRequirements(CropType.AppleTree)[4].Required, "Sugarcane 64 for Apple Tree");

            var grapes = CropUnlockConfig.GetRequirements(CropType.Grapes);
            Assert.AreEqual(8, grapes.Length);
            Assert.AreEqual(20, grapes[7].Required, "Onion 20 for Potato/Grapes");

            var full = Totals((CropType.Wheat, 54), (CropType.Corn, 54), (CropType.Tomato, 60), (CropType.Eggplant, 60),
                (CropType.Sugarcane, 64), (CropType.Lettuce, 64), (CropType.Turnip, 60), (CropType.Onion, 60),
                (CropType.Potato, 48), (CropType.Grapes, 48), (CropType.Watermelon, 20), (CropType.Pumpkin, 20));
            Assert.AreEqual((1 << CropTypeInfo.Count) - 1, CropUnlockConfig.GetUnlockedMask(full), "Everything unlocks at the final thresholds");
            full[(int)CropType.Watermelon] = 19;
            Assert.IsFalse(CropUnlockConfig.IsUnlocked(CropType.AppleTree, full));
            Assert.IsTrue(CropUnlockConfig.IsUnlocked(CropType.Pumpkin, full), "Lower tiers stay unlocked");
        }

        [TestMethod]
        public void UnlockedMask_StartsWithOnlyWheatAndCorn()
        {
            int mask = CropUnlockConfig.GetUnlockedMask(null);
            Assert.AreEqual((1 << (int)CropType.Wheat) | (1 << (int)CropType.Corn), mask);
        }
    }
}
