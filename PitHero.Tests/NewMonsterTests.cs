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
            // Formula: (25 + level * 8) * archetype_multiplier
            Assert.AreEqual(33, slime.MaxHP, "Slime should have HP: 33 (L1, Balanced)");
            Assert.AreEqual(23, bat.MaxHP, "Bat should have HP: 23 (L1, FastFragile)");
            Assert.AreEqual(33, rat.MaxHP, "Rat should have HP: 33 (L1, Balanced)");
            Assert.AreEqual(49, goblin.MaxHP, "Goblin should have HP: 49 (L3, Balanced)");
            Assert.AreEqual(34, spider.MaxHP, "Spider should have HP: 34 (L3, FastFragile)");
            Assert.AreEqual(34, snake.MaxHP, "Snake should have HP: 34 (L3, FastFragile)");
            Assert.AreEqual(109, skeleton.MaxHP, "Skeleton should have HP: 109 (L6, Tank)");
            Assert.AreEqual(109, orc.MaxHP, "Orc should have HP: 109 (L6, Tank)");
            Assert.AreEqual(51, wraith.MaxHP, "Wraith should have HP: 51 (L6, FastFragile)");
            Assert.AreEqual(157, pitLord.MaxHP, "Pit Lord should have HP: 157 (L10, Tank)");

            // Assert - Check all stats (calculated using BalanceConfig formulas)
            // Formula: (3 + level * 1.0) * archetype_stat_multiplier
            Assert.AreEqual(4, slime.Stats.Strength, "Slime Strength");
            Assert.AreEqual(4, slime.Stats.Agility, "Slime Agility");
            Assert.AreEqual(4, slime.Stats.Vitality, "Slime Vitality");
            Assert.AreEqual(4, slime.Stats.Magic, "Slime Magic");
            
            Assert.AreEqual(4, bat.Stats.Strength, "Bat Strength (L1 FastFragile: 4*1.2=4.8?4)");
            Assert.AreEqual(6, bat.Stats.Agility, "Bat Agility (L1 FastFragile: 4*1.5=6)");
            Assert.AreEqual(2, bat.Stats.Vitality, "Bat Vitality (L1 FastFragile: 4*0.6=2.4?2)");
            Assert.AreEqual(3, bat.Stats.Magic, "Bat Magic (L1 FastFragile: 4*0.9=3.6?3)");
            
            Assert.AreEqual(6, goblin.Stats.Strength, "Goblin Strength (L3 Balanced: 3+3=6)");
            Assert.AreEqual(7, spider.Stats.Strength, "Spider Strength (L3 FastFragile: 6*1.2=7.2?7)");
            Assert.AreEqual(9, spider.Stats.Agility, "Spider Agility (L3 FastFragile: 6*1.5=9)");
            
            Assert.AreEqual(7, skeleton.Stats.Strength, "Skeleton Strength (L6 Tank: 9*0.8=7.2?7)");
            Assert.AreEqual(11, skeleton.Stats.Vitality, "Skeleton Vitality (L6 Tank: 9*1.3=11.7?11)");
            
            Assert.AreEqual(7, orc.Stats.Strength, "Orc Strength (L6 Tank: 9*0.8=7.2?7)");
            Assert.AreEqual(11, orc.Stats.Vitality, "Orc Vitality (L6 Tank: 9*1.3=11.7?11)");
            
            Assert.AreEqual(10, wraith.Stats.Strength, "Wraith Strength (L6 FastFragile: 9*1.2=10.8?10)");
            Assert.AreEqual(13, wraith.Stats.Agility, "Wraith Agility (L6 FastFragile: 9*1.5=13.5?13)");
            
            Assert.AreEqual(10, pitLord.Stats.Strength, "Pit Lord Strength (L10 Tank: 13*0.8=10.4?10)");
            Assert.AreEqual(16, pitLord.Stats.Vitality, "Pit Lord Vitality (L10 Tank: 13*1.3=16.9?16)");
            
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
