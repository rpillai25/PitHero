using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class GrowthCurveCalculatorTests
    {
        #region CalculateLinearGrowth Tests

        [TestMethod]
        public void CalculateLinearGrowth_Level1_ReturnsBaseValue()
        {
            int result = GrowthCurveCalculator.CalculateLinearGrowth(10, 2, 1);
            Assert.AreEqual(10, result);
        }

        [TestMethod]
        public void CalculateLinearGrowth_Level10_ReturnsCorrectValue()
        {
            // Base 10, Growth 2, Level 10 = 10 + (2 * 9) = 28
            int result = GrowthCurveCalculator.CalculateLinearGrowth(10, 2, 10);
            Assert.AreEqual(28, result);
        }

        [TestMethod]
        public void CalculateLinearGrowth_MaxLevel_ReturnsCorrectValue()
        {
            // Base 5, Growth 1, Level 99 = 5 + (1 * 98) = 103
            int result = GrowthCurveCalculator.CalculateLinearGrowth(5, 1, 99);
            Assert.AreEqual(103, result);
        }

        [TestMethod]
        public void CalculateLinearGrowth_ZeroGrowth_ReturnsBaseValue()
        {
            int result = GrowthCurveCalculator.CalculateLinearGrowth(10, 0, 50);
            Assert.AreEqual(10, result);
        }

        [TestMethod]
        public void CalculateLinearGrowth_NegativeLevel_TreatsAsLevel1()
        {
            int result = GrowthCurveCalculator.CalculateLinearGrowth(10, 2, -5);
            Assert.AreEqual(10, result);
        }

        #endregion

        #region CalculateExponentialGrowth Tests

        [TestMethod]
        public void CalculateExponentialGrowth_Level1_ReturnsBaseValue()
        {
            int result = GrowthCurveCalculator.CalculateExponentialGrowth(100, 1.1f, 1);
            Assert.AreEqual(100, result);
        }

        [TestMethod]
        public void CalculateExponentialGrowth_Level5_ReturnsCorrectValue()
        {
            // Base 100, Rate 1.1, Level 5 = 100 * (1.1 ^ 4) ≈ 146
            int result = GrowthCurveCalculator.CalculateExponentialGrowth(100, 1.1f, 5);
            Assert.IsTrue(result >= 146 && result <= 147); // Allow rounding variance
        }

        [TestMethod]
        public void CalculateExponentialGrowth_Level10_ReturnsCorrectValue()
        {
            // Base 100, Rate 1.05, Level 10 = 100 * (1.05 ^ 9) ≈ 155
            int result = GrowthCurveCalculator.CalculateExponentialGrowth(100, 1.05f, 10);
            Assert.IsTrue(result >= 155 && result <= 156); // Allow rounding variance
        }

        [TestMethod]
        public void CalculateExponentialGrowth_RateOne_ReturnsBaseValue()
        {
            int result = GrowthCurveCalculator.CalculateExponentialGrowth(50, 1.0f, 50);
            Assert.AreEqual(50, result);
        }

        [TestMethod]
        public void CalculateExponentialGrowth_NegativeLevel_TreatsAsLevel1()
        {
            int result = GrowthCurveCalculator.CalculateExponentialGrowth(100, 1.1f, -5);
            Assert.AreEqual(100, result);
        }

        #endregion

        #region CalculateHP Tests

        [TestMethod]
        public void CalculateHP_DefaultValues_ReturnsCorrectHP()
        {
            // Base 25 + (10 vitality * 5) = 75
            int result = GrowthCurveCalculator.CalculateHP(10);
            Assert.AreEqual(75, result);
        }

        [TestMethod]
        public void CalculateHP_CustomBaseHP_ReturnsCorrectHP()
        {
            // Base 50 + (10 vitality * 5) = 100
            int result = GrowthCurveCalculator.CalculateHP(10, baseHP: 50);
            Assert.AreEqual(100, result);
        }

        [TestMethod]
        public void CalculateHP_CustomMultiplier_ReturnsCorrectHP()
        {
            // Base 25 + (10 vitality * 10) = 125
            int result = GrowthCurveCalculator.CalculateHP(10, vitalityMultiplier: 10);
            Assert.AreEqual(125, result);
        }

        [TestMethod]
        public void CalculateHP_ExceedsMax_ClampsToMax()
        {
            // Should clamp to 9999
            int result = GrowthCurveCalculator.CalculateHP(2000, baseHP: 100, vitalityMultiplier: 10);
            Assert.AreEqual(9999, result);
        }

        [TestMethod]
        public void CalculateHP_NegativeVitality_ReturnsMinimumHP()
        {
            // Should handle negative vitality gracefully
            int result = GrowthCurveCalculator.CalculateHP(-10, baseHP: 25, vitalityMultiplier: 5);
            Assert.IsTrue(result >= 0); // Should not go negative
        }

        [TestMethod]
        public void CalculateHP_ZeroVitality_ReturnsBaseHP()
        {
            int result = GrowthCurveCalculator.CalculateHP(0, baseHP: 25);
            Assert.AreEqual(25, result);
        }

        #endregion

        #region CalculateMP Tests

        [TestMethod]
        public void CalculateMP_DefaultValues_ReturnsCorrectMP()
        {
            // Base 10 + (10 magic * 3) = 40
            int result = GrowthCurveCalculator.CalculateMP(10);
            Assert.AreEqual(40, result);
        }

        [TestMethod]
        public void CalculateMP_CustomBaseMP_ReturnsCorrectMP()
        {
            // Base 20 + (10 magic * 3) = 50
            int result = GrowthCurveCalculator.CalculateMP(10, baseMP: 20);
            Assert.AreEqual(50, result);
        }

        [TestMethod]
        public void CalculateMP_CustomMultiplier_ReturnsCorrectMP()
        {
            // Base 10 + (10 magic * 5) = 60
            int result = GrowthCurveCalculator.CalculateMP(10, magicMultiplier: 5);
            Assert.AreEqual(60, result);
        }

        [TestMethod]
        public void CalculateMP_ExceedsMax_ClampsToMax()
        {
            // Should clamp to 999
            int result = GrowthCurveCalculator.CalculateMP(200, baseMP: 100, magicMultiplier: 10);
            Assert.AreEqual(999, result);
        }

        [TestMethod]
        public void CalculateMP_NegativeMagic_ReturnsMinimumMP()
        {
            // Should handle negative magic gracefully
            int result = GrowthCurveCalculator.CalculateMP(-10, baseMP: 10, magicMultiplier: 3);
            Assert.IsTrue(result >= 0); // Should not go negative
        }

        [TestMethod]
        public void CalculateMP_ZeroMagic_ReturnsBaseMP()
        {
            int result = GrowthCurveCalculator.CalculateMP(0, baseMP: 10);
            Assert.AreEqual(10, result);
        }

        #endregion

        #region CalculateRequiredGrowth Tests

        [TestMethod]
        public void CalculateRequiredGrowth_SimpleCase_ReturnsCorrectGrowth()
        {
            // To go from 5 to 103 in 99 levels requires growth of 1 per level
            // (103 - 5) / (99 - 1) = 98 / 98 = 1
            int result = GrowthCurveCalculator.CalculateRequiredGrowth(5, 103, 99);
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public void CalculateRequiredGrowth_WithRounding_ReturnsCorrectGrowth()
        {
            // To go from 10 to 99 in 99 levels
            // (99 - 10) / (99 - 1) = 89 / 98 ≈ 0.9 rounds to 1
            int result = GrowthCurveCalculator.CalculateRequiredGrowth(10, 99, 99);
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public void CalculateRequiredGrowth_SameValue_ReturnsZero()
        {
            int result = GrowthCurveCalculator.CalculateRequiredGrowth(50, 50, 99);
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void CalculateRequiredGrowth_CustomMaxLevel_ReturnsCorrectGrowth()
        {
            // To go from 10 to 50 in 50 levels
            // (50 - 10) / (50 - 1) = 40 / 49 ≈ 0.8 rounds to 1
            int result = GrowthCurveCalculator.CalculateRequiredGrowth(10, 50, 50);
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public void CalculateRequiredGrowth_LargeGrowth_ReturnsCorrectGrowth()
        {
            // To go from 10 to 500 in 99 levels
            // (500 - 10) / (99 - 1) = 490 / 98 = 5
            int result = GrowthCurveCalculator.CalculateRequiredGrowth(10, 500, 99);
            Assert.AreEqual(5, result);
        }

        #endregion

        #region ValidateGrowthCurve Tests

        [TestMethod]
        public void ValidateGrowthCurve_ValidCurve_ReturnsTrue()
        {
            // Base 5, Growth 1, Target 103, Cap 150
            // At level 99: 5 + (1 * 98) = 103 (matches target exactly)
            bool result = GrowthCurveCalculator.ValidateGrowthCurve(5, 1, 103, 150);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ValidateGrowthCurve_WithinTolerance_ReturnsTrue()
        {
            // Base 5, Growth 1, Target 100, Cap 150, Tolerance 5
            // At level 99: 5 + (1 * 98) = 103 (within 5 of target 100)
            bool result = GrowthCurveCalculator.ValidateGrowthCurve(5, 1, 100, 150, 5);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ValidateGrowthCurve_ExceedsTolerance_ReturnsFalse()
        {
            // Base 5, Growth 1, Target 100, Cap 150, Tolerance 2
            // At level 99: 5 + (1 * 98) = 103 (3 away from target, exceeds tolerance of 2)
            bool result = GrowthCurveCalculator.ValidateGrowthCurve(5, 1, 100, 150, 2);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateGrowthCurve_ExceedsCap_ReturnsFalse()
        {
            // Base 5, Growth 2, Target 100, Cap 100
            // At level 99: 5 + (2 * 98) = 201 (exceeds cap of 100)
            bool result = GrowthCurveCalculator.ValidateGrowthCurve(5, 2, 100, 100);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateGrowthCurve_BaseExceedsCap_ReturnsFalse()
        {
            // Base 150 exceeds cap of 100
            bool result = GrowthCurveCalculator.ValidateGrowthCurve(150, 1, 200, 100);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateGrowthCurve_ZeroGrowth_ValidatesTarget()
        {
            // Base 50, Growth 0, Target 50, Cap 100
            bool result = GrowthCurveCalculator.ValidateGrowthCurve(50, 0, 50, 100);
            Assert.IsTrue(result);
        }

        #endregion

        #region CalculateTotalStatsAtLevel Tests

        [TestMethod]
        public void CalculateTotalStatsAtLevel_Level1_ReturnsBaseWithJobBonus()
        {
            var baseStats = new StatBlock(10, 10, 10, 10);
            var jobBase = new StatBlock(5, 5, 5, 5);
            var jobGrowth = new StatBlock(1, 1, 1, 1);

            var result = GrowthCurveCalculator.CalculateTotalStatsAtLevel(baseStats, jobBase, jobGrowth, 1);

            Assert.AreEqual(15, result.Strength);
            Assert.AreEqual(15, result.Agility);
            Assert.AreEqual(15, result.Vitality);
            Assert.AreEqual(15, result.Magic);
        }

        [TestMethod]
        public void CalculateTotalStatsAtLevel_Level10_ReturnsCorrectStats()
        {
            var baseStats = new StatBlock(10, 10, 10, 10);
            var jobBase = new StatBlock(5, 5, 5, 5);
            var jobGrowth = new StatBlock(1, 1, 1, 1);

            // At level 10: base(10) + jobBase(5) + jobGrowth(1) * 9 = 24
            var result = GrowthCurveCalculator.CalculateTotalStatsAtLevel(baseStats, jobBase, jobGrowth, 10);

            Assert.AreEqual(24, result.Strength);
            Assert.AreEqual(24, result.Agility);
            Assert.AreEqual(24, result.Vitality);
            Assert.AreEqual(24, result.Magic);
        }

        [TestMethod]
        public void CalculateTotalStatsAtLevel_MaxLevel_ReturnsCorrectStats()
        {
            var baseStats = new StatBlock(5, 5, 5, 5);
            var jobBase = new StatBlock(3, 3, 3, 3);
            var jobGrowth = new StatBlock(1, 1, 1, 1);

            // At level 99: base(5) + jobBase(3) + jobGrowth(1) * 98 = 106 -> clamped to 99
            var result = GrowthCurveCalculator.CalculateTotalStatsAtLevel(baseStats, jobBase, jobGrowth, 99);

            Assert.AreEqual(99, result.Strength);
            Assert.AreEqual(99, result.Agility);
            Assert.AreEqual(99, result.Vitality);
            Assert.AreEqual(99, result.Magic);
        }

        [TestMethod]
        public void CalculateTotalStatsAtLevel_DifferentGrowthRates_ReturnsCorrectStats()
        {
            var baseStats = new StatBlock(5, 10, 15, 20);
            var jobBase = new StatBlock(2, 3, 4, 5);
            var jobGrowth = new StatBlock(1, 2, 1, 0);

            // At level 5: 
            // Str: 5 + 2 + (1 * 4) = 11
            // Agi: 10 + 3 + (2 * 4) = 21
            // Vit: 15 + 4 + (1 * 4) = 23
            // Mag: 20 + 5 + (0 * 4) = 25
            var result = GrowthCurveCalculator.CalculateTotalStatsAtLevel(baseStats, jobBase, jobGrowth, 5);

            Assert.AreEqual(11, result.Strength);
            Assert.AreEqual(21, result.Agility);
            Assert.AreEqual(23, result.Vitality);
            Assert.AreEqual(25, result.Magic);
        }

        [TestMethod]
        public void CalculateTotalStatsAtLevel_NegativeLevel_TreatsAsLevel1()
        {
            var baseStats = new StatBlock(10, 10, 10, 10);
            var jobBase = new StatBlock(5, 5, 5, 5);
            var jobGrowth = new StatBlock(1, 1, 1, 1);

            var result = GrowthCurveCalculator.CalculateTotalStatsAtLevel(baseStats, jobBase, jobGrowth, -5);

            Assert.AreEqual(15, result.Strength);
            Assert.AreEqual(15, result.Agility);
            Assert.AreEqual(15, result.Vitality);
            Assert.AreEqual(15, result.Magic);
        }

        [TestMethod]
        public void CalculateTotalStatsAtLevel_ClampsSingleStat_OthersUnaffected()
        {
            var baseStats = new StatBlock(90, 10, 10, 10);
            var jobBase = new StatBlock(5, 5, 5, 5);
            var jobGrowth = new StatBlock(1, 1, 1, 1);

            // At level 10: Str would be 90 + 5 + 9 = 104, clamped to 99
            // Others: 10 + 5 + 9 = 24
            var result = GrowthCurveCalculator.CalculateTotalStatsAtLevel(baseStats, jobBase, jobGrowth, 10);

            Assert.AreEqual(99, result.Strength); // Clamped
            Assert.AreEqual(24, result.Agility);
            Assert.AreEqual(24, result.Vitality);
            Assert.AreEqual(24, result.Magic);
        }

        #endregion
    }
}
