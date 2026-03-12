using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Tests
{
    [TestClass]
    public class AlliedMonsterTests
    {
        /// <summary>Tests that AlliedMonster stores name and type correctly.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonster_Constructor_StoresNameAndType()
        {
            var monster = new AlliedMonster("Bob", "Slime", 5, 5, 5);

            Assert.AreEqual("Bob", monster.Name, "Name should be stored as provided");
            Assert.AreEqual("Slime", monster.MonsterTypeName, "MonsterTypeName should be stored as provided");
        }

        /// <summary>Tests that proficiencies are stored correctly within valid range.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonster_Constructor_StoresProficienciesCorrectly()
        {
            var monster = new AlliedMonster("Alice", "Rat", 3, 7, 9);

            Assert.AreEqual(3, monster.FishingProficiency, "Fishing proficiency should be 3");
            Assert.AreEqual(7, monster.CookingProficiency, "Cooking proficiency should be 7");
            Assert.AreEqual(9, monster.FarmingProficiency, "Farming proficiency should be 9");
        }

        /// <summary>Tests that proficiency values below 1 are clamped to 1.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonster_Proficiency_ClampsToMinimum()
        {
            var monster = new AlliedMonster("Test", "Goblin", 0, -5, -100);

            Assert.AreEqual(1, monster.FishingProficiency, "Fishing proficiency below 1 should clamp to 1");
            Assert.AreEqual(1, monster.CookingProficiency, "Cooking proficiency below 1 should clamp to 1");
            Assert.AreEqual(1, monster.FarmingProficiency, "Farming proficiency below 1 should clamp to 1");
        }

        /// <summary>Tests that proficiency values above 9 are clamped to 9.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonster_Proficiency_ClampsToMaximum()
        {
            var monster = new AlliedMonster("Test", "Orc", 10, 50, 999);

            Assert.AreEqual(9, monster.FishingProficiency, "Fishing proficiency above 9 should clamp to 9");
            Assert.AreEqual(9, monster.CookingProficiency, "Cooking proficiency above 9 should clamp to 9");
            Assert.AreEqual(9, monster.FarmingProficiency, "Farming proficiency above 9 should clamp to 9");
        }

        /// <summary>Tests boundary values of exactly 1 and 9.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonster_Proficiency_AcceptsBoundaryValues()
        {
            var monster = new AlliedMonster("Test", "Bat", 1, 9, 5);

            Assert.AreEqual(1, monster.FishingProficiency, "Minimum proficiency of 1 should be accepted");
            Assert.AreEqual(9, monster.CookingProficiency, "Maximum proficiency of 9 should be accepted");
        }
    }
}
