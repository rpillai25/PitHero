using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
using RolePlayingFramework.Enemies;

namespace PitHero.Tests
{
    [TestClass]
    public class EnemyLevelConfigTests
    {
        [TestMethod]
        public void GetPresetLevel_Slime_ReturnsLevelOne()
        {
            // Arrange & Act
            var level = EnemyLevelConfig.GetPresetLevel(EnemyId.Slime);

            // Assert
            Assert.AreEqual(1, level);
        }

        [TestMethod]
        public void HasPresetLevel_Slime_ReturnsTrue()
        {
            // Arrange & Act
            var hasPreset = EnemyLevelConfig.HasPresetLevel(EnemyId.Slime);

            // Assert
            Assert.IsTrue(hasPreset);
        }

        [TestMethod]
        public void GetAllEnemyLevels_ReturnsSlimeAtLevelOne()
        {
            // Arrange & Act
            var allLevels = EnemyLevelConfig.GetAllEnemyLevels();

            // Assert
            Assert.IsTrue(allLevels.ContainsKey(EnemyId.Slime));
            Assert.AreEqual(1, allLevels[EnemyId.Slime]);
        }

        [TestMethod]
        public void GetAllEnemyLevels_ReturnsDictionary_IsReadOnly()
        {
            // Arrange & Act
            var allLevels = EnemyLevelConfig.GetAllEnemyLevels();

            // Assert
            Assert.IsInstanceOfType(allLevels, typeof(System.Collections.Generic.IReadOnlyDictionary<EnemyId, int>));
        }
    }
}
