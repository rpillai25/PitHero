using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Enemies;
using PitHero.Config;

namespace PitHero.Tests
{
    [TestClass]
    public class EnemyLevelSystemIntegrationTests
    {
        [TestMethod]
        public void SlimeEnemy_AlwaysCreatedAtPresetLevel()
        {
            // Arrange
            var expectedLevel = EnemyLevelConfig.GetPresetLevel("Slime");

            // Act - Create slimes with different requested levels
            var slime1 = new Slime(1);
            var slime2 = new Slime(5);
            var slime3 = new Slime(10);

            // Assert - All slimes should be at the preset level (1), not the requested level
            Assert.AreEqual(expectedLevel, slime1.Level);
            Assert.AreEqual(expectedLevel, slime2.Level);  
            Assert.AreEqual(expectedLevel, slime3.Level);
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
            // This test ensures slimes are always level 1 as per the design requirement
            
            // Arrange & Act
            var slimeLevel = EnemyLevelConfig.GetPresetLevel("Slime");

            // Assert
            Assert.AreEqual(1, slimeLevel, "Slimes should always be level 1 according to the design requirements");
        }
    }
}