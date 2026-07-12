using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.VirtualGame;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;
using System.IO;
using System.Text;

namespace PitHero.Tests
{
    /// <summary>
    /// Balance data-gathering probes for issue #291 (pit tier system).
    /// These runs print CSVs consumed by the balance report; they assert only
    /// structural properties, not balance verdicts (the report interprets the data).
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class BalanceProbeTests
    {
        /// <summary>Standard fully-equipped hero setup mirroring VirtualBalanceTraversalTests.</summary>
        private static VirtualGameSimulation CreateSim(int seed, int heroLevel, int startingGold = -1)
        {
            var sim = new VirtualGameSimulation(seed);
            var crystal = new HeroCrystal("ProbeCrystal", new Knight(), heroLevel, new StatBlock(10, 8, 10, 4));
            crystal.EarnJP(1_000_000);
            sim.ConfigureHero(new Knight(), heroLevel, new StatBlock(10, 8, 10, 4), crystal);

            var hero = sim.Hero.LinkedHero;
            var jobSkills = hero.Job.Skills;
            for (int i = 0; i < jobSkills.Count; i++)
                hero.TryPurchaseSkill(jobSkills[i]);

            for (int i = 0; i < 5; i++)
                sim.Bag.TryAdd(PotionItems.HPPotion());

            if (startingGold >= 0)
                sim.ConfigureStartingGold(startingGold);

            return sim;
        }

        private static string ToCsv(string title, System.Collections.Generic.List<VirtualRunMetrics> rows)
        {
            var sb = new StringBuilder(1024);
            using (var writer = new StringWriter(sb))
            {
                writer.WriteLine(title);
                VirtualRunMetrics.WriteCsvHeader(writer);
                for (int i = 0; i < rows.Count; i++)
                    rows[i].WriteRow(writer);
            }
            return sb.ToString();
        }

        /// <summary>
        /// A level-appropriate Knight (curve level for pit 20) with gold to hire mercs
        /// runs depths 20→30, crossing the 25→1(2) tier boundary. Prints the CSV so the
        /// balance report can evaluate boundary smoothness and crossing survivability.
        /// </summary>
        [TestMethod]
        [TestCategory("BalanceProbe")]
        public void Probe_TierCrossing_LevelAppropriateParty_Depths20To30()
        {
            int heroLevel = RolePlayingFramework.Balance.BalanceConfig.EstimatePlayerLevelForPitLevel(20);
            var sim = CreateSim(12345, heroLevel, startingGold: 5000);
            var rows = sim.RunLevelRange(20, 30);
            System.Console.WriteLine(ToCsv($"# crossing probe: Knight L{heroLevel} start, depths 20-30, 5000g", rows));
            Assert.IsTrue(rows.Count > 0, "Crossing probe must traverse at least one depth");
        }

        /// <summary>
        /// Natural-pacing persistent runs (level-1 Knight, default economy) from depth 1
        /// to 50 across three seeds — characterizes where natural leveling pace falls
        /// behind the monster curve (wipe depth distribution).
        /// </summary>
        [TestMethod]
        [TestCategory("BalanceProbe")]
        public void Probe_PersistentNaturalPacing_ThreeSeeds()
        {
            int[] seeds = { 12345, 777, 424242 };
            for (int s = 0; s < seeds.Length; s++)
            {
                var sim = CreateSim(seeds[s], heroLevel: 1);
                var rows = sim.RunLevelRange(1, 50);
                System.Console.WriteLine(ToCsv($"# natural pacing probe: seed {seeds[s]}, level-1 Knight, depths 1-50", rows));
                Assert.IsTrue(rows.Count > 0, $"Seed {seeds[s]}: must traverse at least one depth");
            }
        }
    }
}
