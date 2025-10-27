using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class StatConstantsTests
    {
        [TestMethod]
        public void StatConstants_DefinedCorrectly()
        {
            Assert.AreEqual(9999, StatConstants.MaxHP);
            Assert.AreEqual(999, StatConstants.MaxMP);
            Assert.AreEqual(99, StatConstants.MaxStat);
            Assert.AreEqual(99, StatConstants.MaxLevel);
        }

        [TestMethod]
        public void ClampHP_ValidValue_ReturnsValue()
        {
            Assert.AreEqual(500, StatConstants.ClampHP(500));
            Assert.AreEqual(1, StatConstants.ClampHP(1));
            Assert.AreEqual(9999, StatConstants.ClampHP(9999));
        }

        [TestMethod]
        public void ClampHP_ExceedsMax_ReturnsMax()
        {
            Assert.AreEqual(9999, StatConstants.ClampHP(10000));
            Assert.AreEqual(9999, StatConstants.ClampHP(99999));
        }

        [TestMethod]
        public void ClampHP_BelowZero_ReturnsZero()
        {
            Assert.AreEqual(0, StatConstants.ClampHP(-1));
            Assert.AreEqual(0, StatConstants.ClampHP(-100));
        }

        [TestMethod]
        public void ClampMP_ValidValue_ReturnsValue()
        {
            Assert.AreEqual(50, StatConstants.ClampMP(50));
            Assert.AreEqual(1, StatConstants.ClampMP(1));
            Assert.AreEqual(999, StatConstants.ClampMP(999));
        }

        [TestMethod]
        public void ClampMP_ExceedsMax_ReturnsMax()
        {
            Assert.AreEqual(999, StatConstants.ClampMP(1000));
            Assert.AreEqual(999, StatConstants.ClampMP(9999));
        }

        [TestMethod]
        public void ClampMP_BelowZero_ReturnsZero()
        {
            Assert.AreEqual(0, StatConstants.ClampMP(-1));
            Assert.AreEqual(0, StatConstants.ClampMP(-100));
        }

        [TestMethod]
        public void ClampStat_ValidValue_ReturnsValue()
        {
            Assert.AreEqual(50, StatConstants.ClampStat(50));
            Assert.AreEqual(1, StatConstants.ClampStat(1));
            Assert.AreEqual(99, StatConstants.ClampStat(99));
        }

        [TestMethod]
        public void ClampStat_ExceedsMax_ReturnsMax()
        {
            Assert.AreEqual(99, StatConstants.ClampStat(100));
            Assert.AreEqual(99, StatConstants.ClampStat(999));
        }

        [TestMethod]
        public void ClampStat_BelowZero_ReturnsZero()
        {
            Assert.AreEqual(0, StatConstants.ClampStat(-1));
            Assert.AreEqual(0, StatConstants.ClampStat(-100));
        }

        [TestMethod]
        public void ClampLevel_ValidValue_ReturnsValue()
        {
            Assert.AreEqual(1, StatConstants.ClampLevel(1));
            Assert.AreEqual(50, StatConstants.ClampLevel(50));
            Assert.AreEqual(99, StatConstants.ClampLevel(99));
        }

        [TestMethod]
        public void ClampLevel_ExceedsMax_ReturnsMax()
        {
            Assert.AreEqual(99, StatConstants.ClampLevel(100));
            Assert.AreEqual(99, StatConstants.ClampLevel(999));
        }

        [TestMethod]
        public void ClampLevel_BelowMin_ReturnsMin()
        {
            Assert.AreEqual(1, StatConstants.ClampLevel(0));
            Assert.AreEqual(1, StatConstants.ClampLevel(-100));
        }

        [TestMethod]
        public void ClampStatBlock_ValidStats_ReturnsStats()
        {
            var stats = new StatBlock(50, 60, 70, 80);
            var clamped = StatConstants.ClampStatBlock(stats);
            
            Assert.AreEqual(50, clamped.Strength);
            Assert.AreEqual(60, clamped.Agility);
            Assert.AreEqual(70, clamped.Vitality);
            Assert.AreEqual(80, clamped.Magic);
        }

        [TestMethod]
        public void ClampStatBlock_ExceedsMax_ClampsToMax()
        {
            var stats = new StatBlock(100, 150, 200, 999);
            var clamped = StatConstants.ClampStatBlock(stats);
            
            Assert.AreEqual(99, clamped.Strength);
            Assert.AreEqual(99, clamped.Agility);
            Assert.AreEqual(99, clamped.Vitality);
            Assert.AreEqual(99, clamped.Magic);
        }

        [TestMethod]
        public void ClampStatBlock_BelowZero_ClampsToZero()
        {
            var stats = new StatBlock(-10, -20, -30, -40);
            var clamped = StatConstants.ClampStatBlock(stats);
            
            Assert.AreEqual(0, clamped.Strength);
            Assert.AreEqual(0, clamped.Agility);
            Assert.AreEqual(0, clamped.Vitality);
            Assert.AreEqual(0, clamped.Magic);
        }

        [TestMethod]
        public void ClampStatBlock_MixedValues_ClampsCorrectly()
        {
            var stats = new StatBlock(-5, 50, 150, 0);
            var clamped = StatConstants.ClampStatBlock(stats);
            
            Assert.AreEqual(0, clamped.Strength);
            Assert.AreEqual(50, clamped.Agility);
            Assert.AreEqual(99, clamped.Vitality);
            Assert.AreEqual(0, clamped.Magic);
        }
    }
}
