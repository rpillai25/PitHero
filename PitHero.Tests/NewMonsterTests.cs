using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Enemies;
using PitHero.Config;

namespace PitHero.Tests
{
    [TestClass]
    public class NewMonsterTests
    {
        [TestMethod]
        public void AllNewMonsters_CanBeCreated()
        {
            // Act - Create all new monsters
            var bat = new Bat();
            var rat = new Rat();
            var goblin = new Goblin();
            var spider = new Spider();
            var snake = new Snake();
            var skeleton = new Skeleton();
            var orc = new Orc();
            var wraith = new Wraith();
            var pitLord = new PitLord();

            // Assert - All monsters should be created successfully
            Assert.IsNotNull(bat);
            Assert.IsNotNull(rat);
            Assert.IsNotNull(goblin);
            Assert.IsNotNull(spider);
            Assert.IsNotNull(snake);
            Assert.IsNotNull(skeleton);
            Assert.IsNotNull(orc);
            Assert.IsNotNull(wraith);
            Assert.IsNotNull(pitLord);
        }

        [TestMethod]
        public void AllMonsters_HaveCorrectLevels()
        {
            // Act - Create monsters
            var slime = new Slime();
            var bat = new Bat();
            var rat = new Rat();
            var goblin = new Goblin();
            var spider = new Spider();
            var snake = new Snake();
            var skeleton = new Skeleton();
            var orc = new Orc();
            var wraith = new Wraith();
            var pitLord = new PitLord();

            // Assert - Check levels match config
            Assert.AreEqual(1, slime.Level, "Slime should be level 1");
            Assert.AreEqual(1, bat.Level, "Bat should be level 1");
            Assert.AreEqual(1, rat.Level, "Rat should be level 1");
            Assert.AreEqual(3, goblin.Level, "Goblin should be level 3");
            Assert.AreEqual(3, spider.Level, "Spider should be level 3");
            Assert.AreEqual(3, snake.Level, "Snake should be level 3");
            Assert.AreEqual(6, skeleton.Level, "Skeleton should be level 6");
            Assert.AreEqual(6, orc.Level, "Orc should be level 6");
            Assert.AreEqual(6, wraith.Level, "Wraith should be level 6");
            Assert.AreEqual(10, pitLord.Level, "Pit Lord should be level 10");
        }

        [TestMethod]
        public void AllMonsters_HaveCorrectStats()
        {
            // Act - Create monsters
            var slime = new Slime();
            var bat = new Bat();
            var rat = new Rat();
            var goblin = new Goblin();
            var spider = new Spider();
            var snake = new Snake();
            var skeleton = new Skeleton();
            var orc = new Orc();
            var wraith = new Wraith();
            var pitLord = new PitLord();

            // Assert - Check HP values
            Assert.AreEqual(15, slime.MaxHP, "Slime should have HP: 15");
            Assert.AreEqual(12, bat.MaxHP, "Bat should have HP: 12");
            Assert.AreEqual(13, rat.MaxHP, "Rat should have HP: 13");
            Assert.AreEqual(20, goblin.MaxHP, "Goblin should have HP: 20");
            Assert.AreEqual(16, spider.MaxHP, "Spider should have HP: 16");
            Assert.AreEqual(15, snake.MaxHP, "Snake should have HP: 15");
            Assert.AreEqual(24, skeleton.MaxHP, "Skeleton should have HP: 24");
            Assert.AreEqual(28, orc.MaxHP, "Orc should have HP: 28");
            Assert.AreEqual(18, wraith.MaxHP, "Wraith should have HP: 18");
            Assert.AreEqual(70, pitLord.MaxHP, "Pit Lord should have HP: 70");

            // Assert - Check some specific attack values (Strength)
            Assert.AreEqual(3, slime.Stats.Strength, "Slime should have Attack: 3");
            Assert.AreEqual(4, bat.Stats.Strength, "Bat should have Attack: 4");
            Assert.AreEqual(18, pitLord.Stats.Strength, "Pit Lord should have Attack: 18");
        }

        [TestMethod]
        public void EnemyLevelConfig_ContainsAllMonsters()
        {
            // Assert - All monsters should be in the config
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Slime"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Bat"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Rat"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Goblin"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Spider"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Snake"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Skeleton"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Orc"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Wraith"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Pit Lord"));
        }
    }
}
