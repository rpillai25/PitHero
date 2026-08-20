using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.UI;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Tests.UI
{
    /// <summary>
    /// Covers the workforce counts behind the Monster Info card (issue #394): roster totals split by
    /// shift, and the farm/kitchen/idle headcount of whichever shift is awake.
    /// </summary>
    [TestClass]
    public class MonsterInfoStatsTests
    {
        // "Slime" is a daytime type, "Orc" is nocturnal (MonsterScheduleConfig).
        private static AlliedMonster Day(MonsterJob job) => Make("Slime", job);
        private static AlliedMonster Night(MonsterJob job) => Make("Orc", job);

        private static AlliedMonster Make(string typeName, MonsterJob job)
        {
            var monster = new AlliedMonster("Test", typeName, 5, 5, 5);
            monster.Job = job;
            return monster;
        }

        [TestMethod]
        public void Compute_NullRoster_ReturnsZeros()
        {
            var stats = MonsterInfoStats.Compute(null, false);

            Assert.AreEqual(0, stats.TotalDaytime);
            Assert.AreEqual(0, stats.TotalNighttime);
            Assert.AreEqual(0, stats.FarmWorkers);
            Assert.AreEqual(0, stats.KitchenWorkers);
            Assert.AreEqual(0, stats.IdleWorkers);
        }

        [TestMethod]
        public void Compute_EmptyRoster_ReturnsZeros()
        {
            var stats = MonsterInfoStats.Compute(new List<AlliedMonster>(), true);

            Assert.AreEqual(0, stats.TotalDaytime);
            Assert.AreEqual(0, stats.TotalNighttime);
            Assert.AreEqual(0, stats.IdleWorkers);
        }

        [TestMethod]
        public void Compute_TotalsCountWholeRoster_RegardlessOfShift()
        {
            var roster = new List<AlliedMonster>
            {
                Day(MonsterJob.Farming), Day(MonsterJob.None), Night(MonsterJob.Cooking)
            };

            var day = MonsterInfoStats.Compute(roster, false);
            var night = MonsterInfoStats.Compute(roster, true);

            Assert.AreEqual(2, day.TotalDaytime);
            Assert.AreEqual(1, day.TotalNighttime);
            Assert.AreEqual(2, night.TotalDaytime, "totals must not depend on the current shift");
            Assert.AreEqual(1, night.TotalNighttime);
        }

        [TestMethod]
        public void Compute_TotalsAcceptLocalizedKeyForm()
        {
            var roster = new List<AlliedMonster> { Make("Monster_Orc", MonsterJob.None) };

            var stats = MonsterInfoStats.Compute(roster, true);

            Assert.AreEqual(0, stats.TotalDaytime);
            Assert.AreEqual(1, stats.TotalNighttime);
            Assert.AreEqual(1, stats.IdleWorkers, "the Monster_ prefix must not hide the night shift");
        }

        [TestMethod]
        public void Compute_DayShift_CountsOnlyDaytimeMonstersAsWorkers()
        {
            var roster = new List<AlliedMonster>
            {
                Day(MonsterJob.Farming), Day(MonsterJob.Farming),
                Day(MonsterJob.Cooking),
                Day(MonsterJob.None),
                Night(MonsterJob.Farming), Night(MonsterJob.None)
            };

            var stats = MonsterInfoStats.Compute(roster, false);

            Assert.AreEqual(2, stats.FarmWorkers);
            Assert.AreEqual(1, stats.KitchenWorkers);
            Assert.AreEqual(1, stats.IdleWorkers);
        }

        [TestMethod]
        public void Compute_NightShift_CountsOnlyNocturnalMonstersAsWorkers()
        {
            var roster = new List<AlliedMonster>
            {
                Day(MonsterJob.Farming), Day(MonsterJob.None),
                Night(MonsterJob.Farming),
                Night(MonsterJob.Cooking), Night(MonsterJob.Cooking),
                Night(MonsterJob.None), Night(MonsterJob.None)
            };

            var stats = MonsterInfoStats.Compute(roster, true);

            Assert.AreEqual(1, stats.FarmWorkers);
            Assert.AreEqual(2, stats.KitchenWorkers);
            Assert.AreEqual(2, stats.IdleWorkers);
        }

        [TestMethod]
        public void Compute_AwakeFishingMonster_CountsInNoWorkerLine()
        {
            var roster = new List<AlliedMonster> { Day(MonsterJob.Fishing) };

            var stats = MonsterInfoStats.Compute(roster, false);

            Assert.AreEqual(1, stats.TotalDaytime);
            Assert.AreEqual(0, stats.FarmWorkers);
            Assert.AreEqual(0, stats.KitchenWorkers);
            Assert.AreEqual(0, stats.IdleWorkers, "Fishing has no work loop, but it is still a job");
        }
    }
}
