using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.VirtualGame;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;
using System.IO;
using System.Text;

namespace PitHero.Tests
{
    /// <summary>
    /// Phase C balance-traversal tests for issue #296.
    ///
    /// Runs seeded, fully headless pit-level traversals with real combat via
    /// <see cref="VirtualGameSimulation.RunPitLevel"/> and prints the per-level
    /// metrics table the pit-balance-test skill consumes.  The reproducibility
    /// test pins the deterministic-seeding contract: same seed, same CSV.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class VirtualBalanceTraversalTests
    {
        private const int Seed = 12345;

        /// <summary>Cave-biome sample levels, including every boss floor (5/10/15/20/25).</summary>
        private static readonly int[] SampleLevels = { 1, 5, 10, 15, 20, 25 };

        /// <summary>
        /// Runs one pit level with a fresh seeded simulation and a reference Knight
        /// whose level tracks the expected player level for that pit level.
        /// Stats scale with level automatically via GrowthCurveCalculator.
        /// </summary>
        private static VirtualRunMetrics RunLevel(int seed, int pitLevel)
        {
            var sim = new VirtualGameSimulation(seed);
            int heroLevel = BalanceConfig.EstimatePlayerLevelForPitLevel(pitLevel);
            sim.ConfigureHero(new Knight(), heroLevel, new StatBlock(10, 8, 10, 4));
            return sim.RunPitLevel(pitLevel);
        }

        /// <summary>Runs all sample levels and returns the combined CSV report.</summary>
        private static string RunTraversalCsv(int seed)
        {
            var sb = new StringBuilder(512);
            using (var writer = new StringWriter(sb))
            {
                VirtualRunMetrics.WriteCsvHeader(writer);
                for (int i = 0; i < SampleLevels.Length; i++)
                {
                    var metrics = RunLevel(seed, SampleLevels[i]);
                    metrics.WriteRow(writer);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Traverses the sampled Cave levels with a level-appropriate Knight and prints
        /// the per-level metrics table (pitLevel, battles, rounds, dmgDealt, dmgTaken,
        /// hpLossPct, healing, deaths, wiped).  Sanity: every sampled level must produce
        /// battles (monsters are real now) and pit level 1 must be beatable.
        /// </summary>
        [TestMethod]
        [TestCategory("BalanceTraversal")]
        public void BalanceTraversal_SampledCaveLevels_ProducesPerLevelMetrics()
        {
            var sb = new StringBuilder(512);
            using var writer = new StringWriter(sb);
            VirtualRunMetrics.WriteCsvHeader(writer);

            for (int i = 0; i < SampleLevels.Length; i++)
            {
                int pitLevel = SampleLevels[i];
                var metrics = RunLevel(Seed, pitLevel);
                metrics.WriteRow(writer);

                Assert.AreEqual(Seed, metrics.RngSeed,
                    $"Pit {pitLevel}: metrics must record the run seed");
                Assert.IsTrue(metrics.BattleCount > 0,
                    $"Pit {pitLevel}: traversal must fight at least one battle (real monsters spawn now)");
                Assert.IsTrue(metrics.DamageDealt > 0,
                    $"Pit {pitLevel}: allies must deal damage in real combat");
            }

            // The balance-report table — visible in test output for the pit-balance-test skill
            System.Console.WriteLine(sb.ToString());

            // Acceptance floor: a level-appropriate hero must clear pit level 1
            var level1 = RunLevel(Seed, 1);
            Assert.IsFalse(level1.Wiped, "A level-appropriate Knight must clear pit level 1");
        }

        /// <summary>
        /// Deterministic-seeding contract: two traversals with the same seed produce a
        /// byte-identical CSV report (combat rolls flow through seeded Nez.Random; pit
        /// layout is already deterministic per level).
        /// </summary>
        [TestMethod]
        [TestCategory("BalanceTraversal")]
        public void BalanceTraversal_SameSeed_ProducesIdenticalCsv()
        {
            string first  = RunTraversalCsv(Seed);
            string second = RunTraversalCsv(Seed);
            Assert.AreEqual(first, second,
                "Same seed must reproduce the exact same balance metrics CSV");
        }

        /// <summary>
        /// Different seeds should be recorded in metrics independently (seed capture works
        /// for both the seeded constructor and ambient-seed capture in the default one).
        /// </summary>
        [TestMethod]
        [TestCategory("BalanceTraversal")]
        public void SeededConstructor_RecordsSeedInMetrics()
        {
            var metrics = RunLevel(98765, 1);
            Assert.AreEqual(98765, metrics.RngSeed);

            Nez.Random.SetSeed(555);
            var ambientSim = new VirtualGameSimulation();
            ambientSim.ConfigureHero(new Knight(), 3, new StatBlock(10, 8, 10, 4));
            var ambientMetrics = ambientSim.RunPitLevel(1);
            Assert.AreEqual(555, ambientMetrics.RngSeed,
                "Default constructor must capture the ambient Nez.Random seed");
        }
    }
}
