using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services.AutoJob;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the pure JobAssignmentSolver: sticky/min fill order, the round-robin desired
    /// fill, proficiency selection with deterministic tie-breaks, the one-per-solve sticky trim
    /// (issue #375 slow-drain scale-down), the starvation release pass, and the
    /// strict-double-improvement swap pass.
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
        public void Solve_FirstListedDemandMin_OutranksLaterListedDemands()
        {
            // One monster, two competing minimums: demand-list order is priority, so the
            // first-listed job wins the lone worker.
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
        public void Solve_StickyExcess_TrimsOneWorstWorkerPerSolve()
        {
            // Issue #375 scale-down: three sticky cooks but the kitchen only wants one. Exactly
            // ONE worker (the worst cook) is released per solve — a slow drain, never a layoff.
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

            Assert.AreEqual(MonsterJob.None, _result[0], "Worst cook is released first");
            Assert.AreEqual(MonsterJob.Cooking, _result[1], "Only one worker is trimmed per solve");
            Assert.AreEqual(MonsterJob.Cooking, _result[2], "Best cook is never the one trimmed");
        }

        [TestMethod]
        public void Solve_StickyAtDesired_NothingTrimmed()
        {
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.Cooking, 1, 4),
                Monster(1, MonsterJob.Cooking, 1, 6),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Cooking, 0, 2, sticky: true),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.Cooking, _result[0], "Held workers within desired are untouched");
            Assert.AreEqual(MonsterJob.Cooking, _result[1], "Held workers within desired are untouched");
        }

        [TestMethod]
        public void Solve_TrimmedWorker_ReassignedToNeedyJobSameSolve()
        {
            // The trim runs before the fill passes, so the released cook lands on the farm in the
            // SAME solve instead of walking home first.
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.Cooking, 7, 3),
                Monster(1, MonsterJob.Cooking, 2, 9),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Farming, 1, 1, sticky: false),
                Demand(MonsterJob.Cooking, 0, 1, sticky: true),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.Farming, _result[0], "Trimmed worst cook is picked up by farming immediately");
            Assert.AreEqual(MonsterJob.Cooking, _result[1], "Best cook keeps the kitchen");
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

        // ── Starvation release: the one exception to kitchen stickiness ──────

        [TestMethod]
        public void Solve_StarvedFarming_PullsStickyKitchenWorkersStepwise()
        {
            // Every monster is sticky-locked in a kitchen that wants nobody while farming demands
            // workers. The drain is stepwise: each solve trims one cook for the farm, so farming
            // reaches its desired count over successive solves rather than in one layoff.
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.Cooking, 5, 5),
                Monster(1, MonsterJob.Cooking, 5, 5),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Farming, 1, 2, sticky: false),
                Demand(MonsterJob.Cooking, 0, 0, sticky: true),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);
            Assert.AreEqual(MonsterJob.Farming, _result[0], "First solve releases one cook to the farm");
            Assert.AreEqual(MonsterJob.Cooking, _result[1], "The second cook drains on a later solve, not this one");

            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                m.CurrentJob = _result[i];
                monsters[i] = m;
            }
            JobAssignmentSolver.Solve(monsters, demands, _result);
            Assert.AreEqual(MonsterJob.Farming, _result[0]);
            Assert.AreEqual(MonsterJob.Farming, _result[1], "Second solve drains the remaining cook");
        }

        [TestMethod]
        public void Solve_StarvedFarming_RespectsRaidedJobMinimum()
        {
            // Four sticky cooks; farming wants 3 but the kitchen's own minimum of 3 is a floor
            // the release pass never raids below — exactly one worker is pulled.
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.Cooking, 5, 5),
                Monster(1, MonsterJob.Cooking, 5, 5),
                Monster(2, MonsterJob.Cooking, 5, 5),
                Monster(3, MonsterJob.Cooking, 5, 5),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Farming, 1, 3, sticky: false),
                Demand(MonsterJob.Cooking, 3, 3, sticky: true),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            int farmers = 0, cooks = 0;
            for (int i = 0; i < _result.Count; i++)
            {
                if (_result[i] == MonsterJob.Farming) farmers++;
                if (_result[i] == MonsterJob.Cooking) cooks++;
            }
            Assert.AreEqual(1, farmers, "Only one worker can be pulled before the kitchen hits its minimum");
            Assert.AreEqual(3, cooks, "Kitchen never drops below its own MinWorkers");
        }

        [TestMethod]
        public void Solve_StarvedFarming_PicksBestFarmerFromKitchen()
        {
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.Cooking, 3, 5),
                Monster(1, MonsterJob.Cooking, 9, 5),
                Monster(2, MonsterJob.Cooking, 9, 5),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Farming, 1, 1, sticky: false),
                Demand(MonsterJob.Cooking, 0, 0, sticky: true),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.Cooking, _result[0], "Weakest farmer stays in the kitchen");
            Assert.AreEqual(MonsterJob.Farming, _result[1], "Best farmer is pulled; proficiency tie breaks to lowest index");
            Assert.AreEqual(MonsterJob.Cooking, _result[2]);
        }

        [TestMethod]
        public void Solve_FarmingHasAnyWorker_KitchenStickinessHolds()
        {
            // The exception fires only at ZERO farm workers: with one free monster available for
            // farming, understaffed farming never raids the sticky kitchen.
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.None, 5, 5),
                Monster(1, MonsterJob.Cooking, 5, 5),
                Monster(2, MonsterJob.Cooking, 5, 5),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Farming, 1, 3, sticky: false),
                Demand(MonsterJob.Cooking, 0, 3, sticky: true),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.Farming, _result[0], "Free monster staffs the farm");
            Assert.AreEqual(MonsterJob.Cooking, _result[1], "Sticky cook stays — farming has a worker");
            Assert.AreEqual(MonsterJob.Cooking, _result[2], "Sticky cook stays — farming has a worker");
        }

        [TestMethod]
        public void Solve_StarvedDemand_NeverRaidsHigherPriorityStickyJob()
        {
            // Kitchen still wants both its workers (desired 2, so no trim), and starved farming
            // sits BELOW it in the demand list: the release pass only raids lower-priority jobs.
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.Cooking, 5, 5),
                Monster(1, MonsterJob.Cooking, 5, 5),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Cooking, 0, 2, sticky: true),
                Demand(MonsterJob.Farming, 1, 2, sticky: false),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.Cooking, _result[0], "A starved demand only raids lower-priority jobs");
            Assert.AreEqual(MonsterJob.Cooking, _result[1], "A starved demand only raids lower-priority jobs");
        }

        // ── Round-robin desired fill (issue #375) ────────────────────────────

        [TestMethod]
        public void Solve_DesiredExtras_SplitRoundRobinAcrossDemands()
        {
            // The original bug: farming's desired extras consumed every spare monster before the
            // kitchen's desired pass ran, freezing the kitchen at its minimum. Desired slots now
            // fill one per demand per cycle, so both jobs grow together.
            var monsters = new List<MonsterJobSnapshot>();
            for (int i = 0; i < 10; i++)
                monsters.Add(Monster(i, MonsterJob.None, 5, 5));
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Farming, 1, 8, sticky: false),
                Demand(MonsterJob.Cooking, 3, 6, sticky: true),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            int farmers = 0, cooks = 0;
            for (int i = 0; i < _result.Count; i++)
            {
                if (_result[i] == MonsterJob.Farming) farmers++;
                if (_result[i] == MonsterJob.Cooking) cooks++;
            }
            // Mins take 1 + 3; the 6 spares alternate farming/cooking (farming first per cycle)
            // until the kitchen hits its desired 6, leaving farming the rest.
            Assert.AreEqual(6, cooks, "Kitchen reaches its full desired count despite farming being listed first");
            Assert.AreEqual(4, farmers, "Farming grows with the remaining spares instead of monopolizing all six");
        }

        [TestMethod]
        public void Solve_DesiredRoundRobin_ListOrderBreaksTheTie()
        {
            // One spare after mins: the first-listed demand wins each cycle's first slot.
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.None, 5, 5),
                Monster(1, MonsterJob.None, 5, 5),
                Monster(2, MonsterJob.None, 5, 5),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Farming, 1, 3, sticky: false),
                Demand(MonsterJob.Cooking, 1, 3, sticky: true),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            int farmers = 0, cooks = 0;
            for (int i = 0; i < _result.Count; i++)
            {
                if (_result[i] == MonsterJob.Farming) farmers++;
                if (_result[i] == MonsterJob.Cooking) cooks++;
            }
            Assert.AreEqual(2, farmers, "First-listed farming takes the lone contested spare");
            Assert.AreEqual(1, cooks);
        }

        [TestMethod]
        public void Solve_ZeroFarmingDemand_NoStickyRelease()
        {
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.Cooking, 9, 5),
                Monster(1, MonsterJob.Cooking, 9, 5),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Farming, 0, 0, sticky: false),
                Demand(MonsterJob.Cooking, 0, 3, sticky: true),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);

            Assert.AreEqual(MonsterJob.Cooking, _result[0], "No farm workload — no exception, cooks stay");
            Assert.AreEqual(MonsterJob.Cooking, _result[1], "No farm workload — no exception, cooks stay");
        }

        [TestMethod]
        public void Solve_AfterStickyRelease_NextSolveIsStable()
        {
            var monsters = new List<MonsterJobSnapshot>
            {
                Monster(0, MonsterJob.Cooking, 5, 5),
                Monster(1, MonsterJob.Cooking, 5, 5),
                Monster(2, MonsterJob.Cooking, 5, 5),
                Monster(3, MonsterJob.Cooking, 5, 5),
            };
            var demands = new List<JobDemandEntry>
            {
                Demand(MonsterJob.Farming, 1, 3, sticky: false),
                Demand(MonsterJob.Cooking, 3, 3, sticky: true),
            };

            JobAssignmentSolver.Solve(monsters, demands, _result);
            var first = new List<MonsterJob>(_result);

            // Apply the results as the new current jobs and solve again with identical demands.
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                m.CurrentJob = first[i];
                monsters[i] = m;
            }
            JobAssignmentSolver.Solve(monsters, demands, _result);

            CollectionAssert.AreEqual(first, _result,
                "Release must not oscillate: once a farmer exists the exception never re-fires");
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
