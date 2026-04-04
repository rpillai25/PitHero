using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
using PitHero;

namespace PitHero.Tests
{
    [TestClass]
    public class EnemyLevelConfigTests
    {
        [TestMethod]
        public void GetPresetLevel_Slime_ReturnsLevelOne()
        {
            // Arrange & Act
            var level = EnemyLevelConfig.GetPresetLevel(MonsterTextKey.Monster_Slime);

            // Assert
            Assert.AreEqual(1, level);
        }

        [TestMethod]
        public void GetPresetLevel_UnknownEnemy_ReturnsDefaultLevelOne()
        {
            // Arrange & Act
            var level = EnemyLevelConfig.GetPresetLevel("UnknownEnemy");

            // Assert
            Assert.AreEqual(1, level);
        }

        [TestMethod]
        public void HasPresetLevel_Slime_ReturnsTrue()
        {
            // Arrange & Act
            var hasPreset = EnemyLevelConfig.HasPresetLevel(MonsterTextKey.Monster_Slime);

            // Assert
            Assert.IsTrue(hasPreset);
        }

        [TestMethod]
        public void HasPresetLevel_UnknownEnemy_ReturnsFalse()
        {
            // Arrange & Act
            var hasPreset = EnemyLevelConfig.HasPresetLevel("UnknownEnemy");

            // Assert
            Assert.IsFalse(hasPreset);
        }

        [TestMethod]
        public void GetAllEnemyLevels_ReturnsSlimeAtLevelOne()
        {
            // Arrange & Act
            var allLevels = EnemyLevelConfig.GetAllEnemyLevels();

            // Assert
            Assert.IsTrue(allLevels.ContainsKey(MonsterTextKey.Monster_Slime));
            Assert.AreEqual(1, allLevels[MonsterTextKey.Monster_Slime]);
        }

        [TestMethod]
        public void GetAllEnemyLevels_ReturnsDictionary_IsReadOnly()
        {
            // Arrange & Act
            var allLevels = EnemyLevelConfig.GetAllEnemyLevels();

            // Assert
            Assert.IsInstanceOfType(allLevels, typeof(System.Collections.Generic.IReadOnlyDictionary<string, int>));
        }
    }
}