using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services.AutoJob;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the pure JobAssignmentSolver: sticky/min/desired fill order, proficiency selection
    /// with deterministic tie-breaks, surplus demotion, and the strict-double-improvement swap pass.
    /// </summary>
    [TestClass]
    public class JobAssignmentSolverTests
    {
        private readonly List<MonsterJob> _result = new List<MonsterJob>();

        private static MonsterJobSnapshot Monster(int index, MonsterJob current,
            int farming, int cooking, int fishing = 1)
        {
            return new MonsterJobSnapshot
            {
                RosterIndex = index,
                CurrentJob = current,
                FarmingProficiency = farming,
                CookingProficiency = cooking,
                FishingProficiency = fishing,
            };
        }

        private static JobDemandEntry Demand(MonsterJob job, int min, int desired, bool sticky)
        {
            return new JobDemandEntry { Job = job, MinWorkers = min, DesiredWorkers = desired, Sticky = sticky };
        }

        [TestMethod]
        public void Solve_ZeroDemand_AllMonstersGetNone()
        {
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.Farming, 9, 9),
                Monster(1, MonsterJob.None, 5, 5),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Cooking, 0, 0, sticky: true),
                Demand(MonsterJob.Farming, 0, 0, sticky: false),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.None, _result[0], "Non-sticky farmer should be sent home with zero demand");
            Assert.AreEqual(MonsterJob.None, _result[1], "Unassigned monster should stay None with zero demand");
        }

        [TestMethod]
        public void Solve_FarmingDemand_FilledByBestFarmingProficiency()
        {
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.None, 3, 1),
                Monster(1, MonsterJob.None, 9, 1),
                Monster(2, MonsterJob.None, 6, 1),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Farming, 1, 2, sticky: false),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.None, _result[0], "Lowest farming skill should stay home");
            Assert.AreEqual(MonsterJob.Farming, _result[1], "Best farmer should be assigned");
            Assert.AreEqual(MonsterJob.Farming, _result[2], "Second-best farmer should be assigned");
        }

        [TestMethod]
        public void Solve_ProficiencyTie_BrokenByLowestRosterIndex()
        {
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.None, 5, 1),
                Monster(1, MonsterJob.None, 5, 1),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Farming, 1, 1, sticky: false),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.Farming, _result[0], "Tie should go to the lowest roster index");
            Assert.AreEqual(MonsterJob.None, _result[1]);
        }

        [TestMethod]
        public void Solve_KitchenMin_FilledByCookingSkillBeforeFarmingDesired()
        {
            // One monster, kitchen min 1 listed first: kitchen wins even though farming wants 1 too.
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.None, 9, 2),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Cooking, 1, 1, sticky: true),
                Demand(MonsterJob.Farming, 1, 1, sticky: false),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.Cooking, _result[0], "Kitchen minimum outranks farming (demand-list order)");
        }

        [TestMethod]
        public void Solve_MinsFillBeforeAnyDesired()
        {
            // Two monsters. Cooking min 1 / desired 2, Farming min 1 / desired 1.
            // Both minimums must fill before cooking's second desired slot takes the last monster.
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.None, 1, 9),
                Monster(1, MonsterJob.None, 9, 1),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Cooking, 1, 2, sticky: true),
                Demand(MonsterJob.Farming, 1, 1, sticky: false),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.Cooking, _result[0], "Best cook fills the kitchen minimum");
            Assert.AreEqual(MonsterJob.Farming, _result[1], "Farming minimum fills before kitchen's desired extras");
        }

        [TestMethod]
        public void Solve_StickyCookingWorkers_KeptEvenAboveDesired()
        {
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.Cooking, 1, 4),
                Monster(1, MonsterJob.Cooking, 1, 6),
                Monster(2, MonsterJob.Cooking, 1, 8),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Cooking, 0, 1, sticky: true),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.Cooking, _result[0], "Sticky worker is never demoted");
            Assert.AreEqual(MonsterJob.Cooking, _result[1], "Sticky worker is never demoted");
            Assert.AreEqual(MonsterJob.Cooking, _result[2], "Sticky worker is never demoted");
        }

        [TestMethod]
        public void Solve_SurplusFarmers_MoveToKitchenOrHome()
        {
            // Three former farmers; farming now wants 1, kitchen wants 1.
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.Farming, 7, 2),
                Monster(1, MonsterJob.Farming, 5, 9),
                Monster(2, MonsterJob.Farming, 3, 1),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Cooking, 1, 1, sticky: true),
                Demand(MonsterJob.Farming, 1, 1, sticky: false),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.Farming, _result[0], "Best farmer keeps farming");
            Assert.AreEqual(MonsterJob.Cooking, _result[1], "Best cook moves to the kitchen");
            Assert.AreEqual(MonsterJob.None, _result[2], "Surplus worker with no demanded job goes home");
        }

        [TestMethod]
        public void Solve_FishingWorkerWithNoFishingDemand_IsReassigned()
        {
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.Fishing, 8, 1, fishing: 9),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Farming, 1, 1, sticky: false),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.Farming, _result[0],
                "A job with no demand evaluator is non-sticky; its workers are pooled and reassigned");
        }

        [TestMethod]
        public void Solve_SwapPass_FiresOnStrictDoubleImprovement()
        {
            // Sticky cook (cooking 2, farming 9) and assigned farmer (cooking 9, farming 2):
            // both jobs strictly improve, so they swap.
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.Cooking, 9, 2),
                Monster(1, MonsterJob.None, 2, 9),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Cooking, 0, 1, sticky: true),
                Demand(MonsterJob.Farming, 1, 1, sticky: false),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.Farming, _result[0], "Great farmer swaps out of the kitchen");
            Assert.AreEqual(MonsterJob.Cooking, _result[1], "Great cook swaps into the kitchen");
        }

        [TestMethod]
        public void Solve_SwapPass_NoSwapWhenOnlyOneSideImproves()
        {
            // Farmer would improve the kitchen (cooking 9 > 5) but the cook is a worse farmer (3 < 6):
            // farming would lose, so no swap.
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.Cooking, 3, 5),
                Monster(1, MonsterJob.None, 6, 9),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Cooking, 0, 1, sticky: true),
                Demand(MonsterJob.Farming, 1, 1, sticky: false),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.Cooking, _result[0], "No swap when the other job would lose skill");
            Assert.AreEqual(MonsterJob.Farming, _result[1]);
        }

        [TestMethod]
        public void Solve_RosterSmallerThanDemand_MinsFillInDemandListOrder()
        {
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.None, 5, 5),
                Monster(1, MonsterJob.None, 5, 5),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Cooking, 3, 3, sticky: true),
                Demand(MonsterJob.Farming, 2, 2, sticky: false),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.Cooking, _result[0], "First-listed demand takes the whole short roster");
            Assert.AreEqual(MonsterJob.Cooking, _result[1], "First-listed demand takes the whole short roster");
        }

        [TestMethod]
        public void Solve_IsDeterministicAcrossRepeatedSolves()
        {
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.Farming, 4, 7),
                Monster(1, MonsterJob.Cooking, 7, 4),
                Monster(2, MonsterJob.None, 5, 5),
                Monster(3, MonsterJob.None, 5, 5),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Cooking, 1, 2, sticky: true),
                Demand(MonsterJob.Farming, 1, 2, sticky: false),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);
            var first = new List<MonsterJob>(_result);
            JobAssignmentSolver.Solve(monsters, demands, _result);

            CollectionAssert.AreEqual(first, _result, "Same inputs must always produce the same assignments");
        }
    }
}
