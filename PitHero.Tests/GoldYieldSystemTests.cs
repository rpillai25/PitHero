using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Enemies;

namespace PitHero.Tests
{
    [TestClass]
    public class GoldYieldSystemTests
    {
        [TestMethod]
        public void CalculateMonsterGoldYield_Level1_Returns8Gold()
        {
            // Act
            var gold = BalanceConfig.CalculateMonsterGoldYield(1);

            // Assert
            Assert.AreEqual(8, gold, "Level 1 monster should yield 8 gold (5 + 1*3)");
        }

        [TestMethod]
        public void CalculateMonsterGoldYield_Level5_Returns20Gold()
        {
            // Act
            var gold = BalanceConfig.CalculateMonsterGoldYield(5);

            // Assert
            Assert.AreEqual(20, gold, "Level 5 monster should yield 20 gold (5 + 5*3)");
        }

        [TestMethod]
        public void CalculateMonsterGoldYield_Level10_Returns35Gold()
        {
            // Act
            var gold = BalanceConfig.CalculateMonsterGoldYield(10);

            // Assert
            Assert.AreEqual(35, gold, "Level 10 monster should yield 35 gold (5 + 10*3)");
        }

        [TestMethod]
        public void CalculateMonsterGoldYield_Level25_Returns80Gold()
        {
            // Act
            var gold = BalanceConfig.CalculateMonsterGoldYield(25);

            // Assert
            Assert.AreEqual(80, gold, "Level 25 monster should yield 80 gold (5 + 25*3)");
        }

        [TestMethod]
        public void CalculateMonsterGoldYield_Level50_Returns155Gold()
        {
            // Act
            var gold = BalanceConfig.CalculateMonsterGoldYield(50);

            // Assert
            Assert.AreEqual(155, gold, "Level 50 monster should yield 155 gold (5 + 50*3)");
        }

        [TestMethod]
        public void CalculateMonsterGoldYield_Level99_Returns302Gold()
        {
            // Act
            var gold = BalanceConfig.CalculateMonsterGoldYield(99);

            // Assert
            Assert.AreEqual(302, gold, "Level 99 monster should yield 302 gold (5 + 99*3)");
        }

        [TestMethod]
        public void AllMonsters_HaveGoldYield()
        {
            // Arrange & Act
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

            // Assert - Check all monsters have GoldYield > 0
            Assert.IsTrue(slime.GoldYield > 0, "Slime should have GoldYield");
            Assert.IsTrue(bat.GoldYield > 0, "Bat should have GoldYield");
            Assert.IsTrue(rat.GoldYield > 0, "Rat should have GoldYield");
            Assert.IsTrue(goblin.GoldYield > 0, "Goblin should have GoldYield");
            Assert.IsTrue(spider.GoldYield > 0, "Spider should have GoldYield");
            Assert.IsTrue(snake.GoldYield > 0, "Snake should have GoldYield");
            Assert.IsTrue(skeleton.GoldYield > 0, "Skeleton should have GoldYield");
            Assert.IsTrue(orc.GoldYield > 0, "Orc should have GoldYield");
            Assert.IsTrue(wraith.GoldYield > 0, "Wraith should have GoldYield");
            Assert.IsTrue(pitLord.GoldYield > 0, "Pit Lord should have GoldYield");
        }

        [TestMethod]
        public void AllMonsters_GoldYieldMatchesLevel()
        {
            // Arrange & Act
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

            // Assert - Check GoldYield matches expected formula
            Assert.AreEqual(BalanceConfig.CalculateMonsterGoldYield(slime.Level), slime.GoldYield);
            Assert.AreEqual(BalanceConfig.CalculateMonsterGoldYield(bat.Level), bat.GoldYield);
            Assert.AreEqual(BalanceConfig.CalculateMonsterGoldYield(rat.Level), rat.GoldYield);
            Assert.AreEqual(BalanceConfig.CalculateMonsterGoldYield(goblin.Level), goblin.GoldYield);
            Assert.AreEqual(BalanceConfig.CalculateMonsterGoldYield(spider.Level), spider.GoldYield);
            Assert.AreEqual(BalanceConfig.CalculateMonsterGoldYield(snake.Level), snake.GoldYield);
            Assert.AreEqual(BalanceConfig.CalculateMonsterGoldYield(skeleton.Level), skeleton.GoldYield);
            Assert.AreEqual(BalanceConfig.CalculateMonsterGoldYield(orc.Level), orc.GoldYield);
            Assert.AreEqual(BalanceConfig.CalculateMonsterGoldYield(wraith.Level), wraith.GoldYield);
            Assert.AreEqual(BalanceConfig.CalculateMonsterGoldYield(pitLord.Level), pitLord.GoldYield);
        }

        [TestMethod]
        public void CalculateMonsterGoldYield_BelowMinLevel_ClampsTo1()
        {
            // Act
            var gold = BalanceConfig.CalculateMonsterGoldYield(0);

            // Assert
            Assert.AreEqual(8, gold, "Level 0 should clamp to 1, yielding 8 gold");
        }

        [TestMethod]
        public void CalculateMonsterGoldYield_AboveMaxLevel_ClampsTo99()
        {
            // Act
            var gold = BalanceConfig.CalculateMonsterGoldYield(100);

            // Assert
            Assert.AreEqual(302, gold, "Level 100 should clamp to 99, yielding 302 gold");
        }
    }
}
