using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.AlliedMonsters;
using PitHero;

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
            var monster = new AlliedMonster("Bob", MonsterTextKey.Monster_Slime, 5, 5, 5);

            Assert.AreEqual("Bob", monster.Name, "Name should be stored as provided");
            Assert.AreEqual(MonsterTextKey.Monster_Slime, monster.MonsterTypeName, "MonsterTypeName should be stored as provided");
        }

        /// <summary>Tests that proficiencies are stored correctly within valid range.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonster_Constructor_StoresProficienciesCorrectly()
        {
            var monster = new AlliedMonster("Alice", MonsterTextKey.Monster_Rat, 3, 7, 9);

            Assert.AreEqual(3, monster.FishingProficiency, "Fishing proficiency should be 3");
            Assert.AreEqual(7, monster.CookingProficiency, "Cooking proficiency should be 7");
            Assert.AreEqual(9, monster.FarmingProficiency, "Farming proficiency should be 9");
        }

        /// <summary>Tests that proficiency values below 1 are clamped to 1.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonster_Proficiency_ClampsToMinimum()
        {
            var monster = new AlliedMonster("Test", MonsterTextKey.Monster_Goblin, 0, -5, -100);

            Assert.AreEqual(1, monster.FishingProficiency, "Fishing proficiency below 1 should clamp to 1");
            Assert.AreEqual(1, monster.CookingProficiency, "Cooking proficiency below 1 should clamp to 1");
            Assert.AreEqual(1, monster.FarmingProficiency, "Farming proficiency below 1 should clamp to 1");
        }

        /// <summary>Tests that proficiency values above 9 are clamped to 9.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonster_Proficiency_ClampsToMaximum()
        {
            var monster = new AlliedMonster("Test", MonsterTextKey.Monster_Orc, 10, 50, 999);

            Assert.AreEqual(9, monster.FishingProficiency, "Fishing proficiency above 9 should clamp to 9");
            Assert.AreEqual(9, monster.CookingProficiency, "Cooking proficiency above 9 should clamp to 9");
            Assert.AreEqual(9, monster.FarmingProficiency, "Farming proficiency above 9 should clamp to 9");
        }

        /// <summary>Tests boundary values of exactly 1 and 9.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonster_Proficiency_AcceptsBoundaryValues()
        {
            var monster = new AlliedMonster("Test", MonsterTextKey.Monster_Bat, 1, 9, 5);

            Assert.AreEqual(1, monster.FishingProficiency, "Minimum proficiency of 1 should be accepted");
            Assert.AreEqual(9, monster.CookingProficiency, "Maximum proficiency of 9 should be accepted");
        }

        /// <summary>Ten kitchen tasks at level 1 raise cooking to level 2 and reset progress (issue #413).</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonster_RecordTask_KitchenLevelsUpAfterLevelTimesTen()
        {
            var monster = new AlliedMonster("Test", MonsterTextKey.Monster_Slime, 1, 1, 1);

            for (int i = 0; i < 9; i++)
                Assert.IsFalse(monster.RecordTask(MonsterJob.Cooking), "Nine tasks are not enough");
            Assert.AreEqual(9, monster.CookingTasks);
            Assert.AreEqual(10, monster.GetTasksRequired(MonsterJob.Cooking));

            Assert.IsTrue(monster.RecordTask(MonsterJob.Cooking), "The tenth task levels up");
            Assert.AreEqual(2, monster.CookingProficiency);
            Assert.AreEqual(0, monster.CookingTasks, "Progress restarts at the new level");
            Assert.AreEqual(20, monster.GetTasksRequired(MonsterJob.Cooking), "Level 2 needs 20 tasks");
            Assert.AreEqual(1, monster.FarmingProficiency, "Other jobs are untouched");
            Assert.AreEqual(0, monster.FarmingTasks);
        }

        /// <summary>Farming tasks are frequent, so farming needs level × 20 tasks per level.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonster_RecordTask_FarmingLevelsUpAfterLevelTimesTwenty()
        {
            var monster = new AlliedMonster("Test", MonsterTextKey.Monster_Slime, 1, 1, 1);
            Assert.AreEqual(20, monster.GetTasksRequired(MonsterJob.Farming));

            for (int i = 0; i < 19; i++)
                Assert.IsFalse(monster.RecordTask(MonsterJob.Farming), "Nineteen tasks are not enough");
            Assert.IsTrue(monster.RecordTask(MonsterJob.Farming), "The twentieth task levels up");
            Assert.AreEqual(2, monster.FarmingProficiency);
            Assert.AreEqual(0, monster.FarmingTasks);
            Assert.AreEqual(40, monster.GetTasksRequired(MonsterJob.Farming), "Level 2 farming needs 40 tasks");
            Assert.AreEqual(GameConfig.MonsterJobFarmingTasksPerLevel, AlliedMonster.TasksPerLevel(MonsterJob.Farming));
            Assert.AreEqual(GameConfig.MonsterJobTasksPerLevel, AlliedMonster.TasksPerLevel(MonsterJob.Fishing));
        }

        /// <summary>Progress stops at the maximum level and None never records anything.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonster_RecordTask_StopsAtMaxLevel()
        {
            var monster = new AlliedMonster("Test", MonsterTextKey.Monster_Slime, 9, 1, 1);

            Assert.IsTrue(monster.IsMaxLevel(MonsterJob.Fishing));
            Assert.IsFalse(monster.RecordTask(MonsterJob.Fishing));
            Assert.AreEqual(0, monster.FishingTasks, "No progress accumulates at max level");
            Assert.AreEqual(9, monster.FishingProficiency);

            Assert.IsFalse(monster.RecordTask(MonsterJob.None));
            Assert.AreEqual(0, monster.GetLevel(MonsterJob.None));
        }

        /// <summary>Restored progress is honoured and clamped to non-negative.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonster_SetTaskProgress_RestoresAndClamps()
        {
            var monster = new AlliedMonster("Test", MonsterTextKey.Monster_Slime, 1, 2, 3);
            monster.SetTaskProgress(4, -1, 7);

            Assert.AreEqual(4, monster.GetTasks(MonsterJob.Fishing));
            Assert.AreEqual(0, monster.GetTasks(MonsterJob.Cooking));
            Assert.AreEqual(7, monster.GetTasks(MonsterJob.Farming));
        }
    }
}
