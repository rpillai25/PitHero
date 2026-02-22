using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Enemies;
using PitHero.Config;

namespace PitHero.Tests
{
    [TestClass]
    public class EnemyLevelSystemIntegrationTests
    {
        [TestMethod]
        public void SlimeEnemy_UsesRequestedLevel_WhenProvided()
        {
            // Arrange
            int requestedLevel1 = 1;
            int requestedLevel2 = 5;
            int requestedLevel3 = 10;

            // Act
            var slime1 = new Slime(1);
            var slime2 = new Slime(5);
            var slime3 = new Slime(10);

            // Assert
            Assert.AreEqual(requestedLevel1, slime1.Level);
            Assert.AreEqual(requestedLevel2, slime2.Level);
            Assert.AreEqual(requestedLevel3, slime3.Level);
        }

        [TestMethod]
        public void SlimeEnemy_UsesPresetLevel_WhenRequestedLevelIsInvalid()
        {
            // Arrange
            var presetLevel = EnemyLevelConfig.GetPresetLevel("Slime");

            // Act
            var slime = new Slime(0);

            // Assert
            Assert.AreEqual(presetLevel, slime.Level);
        }

        [TestMethod]
        public void SlimeEnemy_StatsConsistentAtPresetLevel()
        {
            // Arrange
            var presetLevel = EnemyLevelConfig.GetPresetLevel("Slime");

            // Act - Create multiple slimes at preset level
            var slime1 = new Slime(presetLevel);
            var slime2 = new Slime(presetLevel);

            // Assert - All slimes should have consistent stats
            Assert.AreEqual(slime1.MaxHP, slime2.MaxHP);
            Assert.AreEqual(slime1.Stats.Strength, slime2.Stats.Strength);
            Assert.AreEqual(slime1.Stats.Agility, slime2.Stats.Agility);
            Assert.AreEqual(slime1.Stats.Vitality, slime2.Stats.Vitality);
            Assert.AreEqual(slime1.Stats.Magic, slime2.Stats.Magic);
            Assert.AreEqual(slime1.ExperienceYield, slime2.ExperienceYield);
        }

        [TestMethod]
        public void EnemyLevelConfig_SlimeLevel_IsAlwaysOne()
        {
            // Arrange & Act
            var slimeLevel = EnemyLevelConfig.GetPresetLevel("Slime");

            // Assert
            Assert.AreEqual(1, slimeLevel, "Slimes should always be level 1 according to the design requirements");
        }
    }
}