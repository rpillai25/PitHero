using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class MercenaryExperienceTests
    {
        [TestMethod]
        public void AddExperience_GainsXP_ExperienceIncreases()
        {
            var merc = new Mercenary("TestMerc", new Knight(), 1, new StatBlock(4, 3, 5, 1));
            merc.AddExperience(50);
            Assert.AreEqual(50, merc.Experience, "Mercenary should accumulate experience");
        }

        [TestMethod]
        public void AddExperience_EnoughXP_LevelsUp()
        {
            var merc = new Mercenary("TestMerc", new Knight(), 1, new StatBlock(4, 3, 5, 1));
            bool leveled = merc.AddExperience(100);
            Assert.IsTrue(leveled, "Mercenary should level up with enough XP");
            Assert.AreEqual(2, merc.Level, "Mercenary should be level 2 after leveling up");
            Assert.AreEqual(0, merc.Experience, "Remaining XP should be 0 after exact level up");
        }

        [TestMethod]
        public void AddExperience_NotEnoughXP_DoesNotLevelUp()
        {
            var merc = new Mercenary("TestMerc", new Knight(), 1, new StatBlock(4, 3, 5, 1));
            bool leveled = merc.AddExperience(99);
            Assert.IsFalse(leveled, "Mercenary should not level up with insufficient XP");
            Assert.AreEqual(1, merc.Level, "Mercenary should remain level 1");
            Assert.AreEqual(99, merc.Experience, "Experience should be 99");
        }

        [TestMethod]
        public void AddExperience_StatsGrowOnLevelUp()
        {
            var merc = new Mercenary("TestMerc", new Knight(), 1, new StatBlock(4, 3, 5, 1));
            merc.AddExperience(100); // Level 1 -> 2
            Assert.AreEqual(5, merc.BaseStats.Strength, "Strength should increase by 1");
            Assert.AreEqual(4, merc.BaseStats.Agility, "Agility should increase by 1");
            Assert.AreEqual(6, merc.BaseStats.Vitality, "Vitality should increase by 1");
            Assert.AreEqual(2, merc.BaseStats.Magic, "Magic should increase by 1");
        }

        [TestMethod]
        public void AddExperience_MultipleLevelUps()
        {
            var merc = new Mercenary("TestMerc", new Knight(), 1, new StatBlock(4, 3, 5, 1));
            // Level 1->2 needs 100, level 2->3 needs 200, total 300
            merc.AddExperience(300);
            Assert.AreEqual(3, merc.Level, "Mercenary should reach level 3");
            Assert.AreEqual(0, merc.Experience, "Remaining XP should be 0");
            Assert.AreEqual(6, merc.BaseStats.Strength, "Strength should increase by 2");
        }

        [TestMethod]
        public void AddExperience_PartialXPCarriesOver()
        {
            var merc = new Mercenary("TestMerc", new Knight(), 1, new StatBlock(4, 3, 5, 1));
            merc.AddExperience(150); // 100 to level, 50 leftover
            Assert.AreEqual(2, merc.Level, "Should be level 2");
            Assert.AreEqual(50, merc.Experience, "Should have 50 XP remaining");
        }

        [TestMethod]
        public void AddExperience_MaxLevelCap()
        {
            var merc = new Mercenary("TestMerc", new Knight(), StatConstants.MaxLevel, new StatBlock(50, 50, 50, 50));
            bool leveled = merc.AddExperience(99999);
            Assert.IsFalse(leveled, "Should not level past max level");
            Assert.AreEqual(StatConstants.MaxLevel, merc.Level, "Should remain at max level");
        }

        [TestMethod]
        public void AddExperience_ZeroOrNegative_ReturnsFalse()
        {
            var merc = new Mercenary("TestMerc", new Knight(), 1, new StatBlock(4, 3, 5, 1));
            Assert.IsFalse(merc.AddExperience(0), "Should return false for 0 XP");
            Assert.IsFalse(merc.AddExperience(-10), "Should return false for negative XP");
            Assert.AreEqual(0, merc.Experience, "Experience should remain 0");
        }

        [TestMethod]
        public void RequiredExpForNextLevel_MatchesHeroFormula()
        {
            var merc = new Mercenary("TestMerc", new Knight(), 5, new StatBlock(4, 3, 5, 1));
            Assert.AreEqual(500, merc.RequiredExpForNextLevel(), "Level 5 should require 500 XP (5 * 100)");
        }

        [TestMethod]
        public void AddExperience_HPMPRecalculated()
        {
            var merc = new Mercenary("TestMerc", new Knight(), 1, new StatBlock(4, 3, 5, 1));
            int hpBefore = merc.MaxHP;
            int mpBefore = merc.MaxMP;
            merc.AddExperience(100); // Level up
            Assert.IsTrue(merc.MaxHP >= hpBefore, "MaxHP should not decrease after level up");
            Assert.IsTrue(merc.MaxMP >= mpBefore, "MaxMP should not decrease after level up");
        }

        [TestMethod]
        public void AddExperience_StatsCappedAt99()
        {
            var merc = new Mercenary("TestMerc", new Knight(), 1, new StatBlock(98, 98, 98, 98));
            merc.AddExperience(100); // Level up: stats go 98+1=99
            Assert.AreEqual(99, merc.BaseStats.Strength, "Strength should cap at 99");
            merc.AddExperience(200); // Level up again: stats should stay 99
            Assert.AreEqual(99, merc.BaseStats.Strength, "Strength should remain capped at 99");
        }

        [TestMethod]
        public void NewMercenary_ExperienceStartsAtZero()
        {
            var merc = new Mercenary("TestMerc", new Knight(), 1, new StatBlock(4, 3, 5, 1));
            Assert.AreEqual(0, merc.Experience, "New mercenary should start with 0 experience");
        }
    }
}
