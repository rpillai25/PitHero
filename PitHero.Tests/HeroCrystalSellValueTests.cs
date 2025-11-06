using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class HeroCrystalSellValueTests
    {
        [TestMethod]
        public void HeroCrystal_PrimaryJob_Level1_ShouldHaveCorrectSellValue()
        {
            // Arrange
            var knight = new Knight();
            var baseStats = new StatBlock(strength: 5, agility: 3, vitality: 5, magic: 1);
            var crystal = new HeroCrystal("Test Knight", knight, 1, baseStats);

            // Act
            var sellValue = crystal.CalculateSellValue();

            // Assert
            // Base value per level = 50, Level = 1, Tier multiplier = 1.0
            // Expected: 50 * 1 * 1.0 = 50
            Assert.AreEqual(50, sellValue);
        }

        [TestMethod]
        public void HeroCrystal_PrimaryJob_Level10_ShouldHaveCorrectSellValue()
        {
            // Arrange
            var mage = new Mage();
            var baseStats = new StatBlock(strength: 1, agility: 3, vitality: 3, magic: 7);
            var crystal = new HeroCrystal("Test Mage", mage, 10, baseStats);

            // Act
            var sellValue = crystal.CalculateSellValue();

            // Assert
            // Base value per level = 50, Level = 10, Tier multiplier = 1.0
            // Expected: 50 * 10 * 1.0 = 500
            Assert.AreEqual(500, sellValue);
        }

        [TestMethod]
        public void HeroCrystal_HighLevelPrimary_ShouldHaveHigherSellValue()
        {
            // Arrange
            var priest = new Priest();
            var baseStats = new StatBlock(strength: 2, agility: 3, vitality: 4, magic: 6);
            var crystal = new HeroCrystal("Test Priest", priest, 50, baseStats);

            // Act
            var sellValue = crystal.CalculateSellValue();

            // Assert
            // Base value per level = 50, Level = 50, Tier multiplier = 1.0
            // Expected: 50 * 50 * 1.0 = 2500
            Assert.AreEqual(2500, sellValue);
        }

        [TestMethod]
        public void HeroCrystal_CompositeJob_ShouldUseTierFromComponent()
        {
            // Arrange - Create a composite job from two primary jobs
            var knight = new Knight();
            var mage = new Mage();
            var compositeJob = new CompositeJob(knight, mage);
            var baseStats = new StatBlock(strength: 6, agility: 4, vitality: 6, magic: 8);
            var crystal = new HeroCrystal("Test Composite", compositeJob, 10, baseStats);

            // Act
            var sellValue = crystal.CalculateSellValue();

            // Assert
            // CompositeJob tier should be the max of component tiers (both Primary = 1)
            // Base value per level = 50, Level = 10, Tier multiplier = 1.0
            // Expected: 50 * 10 * 1.0 = 500
            Assert.AreEqual(500, sellValue);
        }
    }
}
