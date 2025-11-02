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

            // Assert - Check HP values (calculated using BalanceConfig formulas)
            Assert.AreEqual(15, slime.MaxHP, "Slime should have HP: 15");
            Assert.AreEqual(10, bat.MaxHP, "Bat should have HP: 10");
            Assert.AreEqual(15, rat.MaxHP, "Rat should have HP: 15");
            Assert.AreEqual(25, goblin.MaxHP, "Goblin should have HP: 25");
            Assert.AreEqual(17, spider.MaxHP, "Spider should have HP: 17");
            Assert.AreEqual(17, snake.MaxHP, "Snake should have HP: 17");
            Assert.AreEqual(60, skeleton.MaxHP, "Skeleton should have HP: 60");
            Assert.AreEqual(60, orc.MaxHP, "Orc should have HP: 60");
            Assert.AreEqual(28, wraith.MaxHP, "Wraith should have HP: 28");
            Assert.AreEqual(90, pitLord.MaxHP, "Pit Lord should have HP: 90");

            // Assert - Check all stats (calculated using BalanceConfig formulas)
            Assert.AreEqual(1, slime.Stats.Strength, "Slime Strength");
            Assert.AreEqual(1, slime.Stats.Agility, "Slime Agility");
            Assert.AreEqual(1, slime.Stats.Vitality, "Slime Vitality");
            Assert.AreEqual(1, slime.Stats.Magic, "Slime Magic");
            
            Assert.AreEqual(2, bat.Stats.Strength, "Bat Strength");
            Assert.AreEqual(2, bat.Stats.Agility, "Bat Agility");
            Assert.AreEqual(1, bat.Stats.Vitality, "Bat Vitality");
            Assert.AreEqual(1, bat.Stats.Magic, "Bat Magic");
            
            Assert.AreEqual(3, goblin.Stats.Strength, "Goblin Strength");
            Assert.AreEqual(3, spider.Stats.Strength, "Spider Strength");
            Assert.AreEqual(4, spider.Stats.Agility, "Spider Agility");
            
            Assert.AreEqual(4, skeleton.Stats.Strength, "Skeleton Strength");
            Assert.AreEqual(6, skeleton.Stats.Vitality, "Skeleton Vitality");
            
            Assert.AreEqual(4, orc.Stats.Strength, "Orc Strength");
            Assert.AreEqual(6, orc.Stats.Vitality, "Orc Vitality");
            
            Assert.AreEqual(6, wraith.Stats.Strength, "Wraith Strength");
            Assert.AreEqual(7, wraith.Stats.Agility, "Wraith Agility");
            
            Assert.AreEqual(6, pitLord.Stats.Strength, "Pit Lord Strength");
            Assert.AreEqual(9, pitLord.Stats.Vitality, "Pit Lord Vitality");
            
            // Assert - Check experience values (calculated using BalanceConfig formulas)
            Assert.AreEqual(18, slime.ExperienceYield, "Slime should give XP: 18");
            Assert.AreEqual(18, bat.ExperienceYield, "Bat should give XP: 18");
            Assert.AreEqual(34, goblin.ExperienceYield, "Goblin should give XP: 34");
            Assert.AreEqual(58, skeleton.ExperienceYield, "Skeleton should give XP: 58");
            Assert.AreEqual(90, pitLord.ExperienceYield, "Pit Lord should give XP: 90");
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
