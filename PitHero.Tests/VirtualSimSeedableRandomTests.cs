using System.IO;
using PitHero.Services.Replay;
using PitHero.VirtualGame;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// The live game installs a <see cref="SeedableRandom"/> as Nez.Random.RNG. This proves the whole
    /// headless simulation (pit generation, exploration, battles, loot, hiring) is reproducible when
    /// that generator drives the global stream, mirroring the System.Random-based contract in
    /// VirtualBalanceTraversalTests.
    /// </summary>
    [TestClass]
    public class VirtualSimSeedableRandomTests
    {
        private static string RunWithSeedableRandom(int seed)
        {
            var sim = new VirtualGameSimulation(seed);
            // Replace the System.Random the ctor installed with the replay generator
            Nez.Random.RNG = new SeedableRandom(seed);

            var crystal = new HeroCrystal("RefCrystal", new Knight(), 5, new StatBlock(10, 8, 10, 4));
            crystal.EarnJP(1_000_000);
            sim.ConfigureHero(new Knight(), 5, new StatBlock(10, 8, 10, 4), crystal);
            var hero = sim.Hero.LinkedHero;
            for (int i = 0; i < hero.Job.Skills.Count; i++)
                hero.TryPurchaseSkill(hero.Job.Skills[i]);
            for (int i = 0; i < 5; i++)
                sim.Bag.TryAdd(PotionItems.HPPotion());

            var rows = sim.RunLevelRange(1, 6);
            var sb = new StringWriter();
            VirtualRunMetrics.WriteCsvHeader(sb);
            for (int i = 0; i < rows.Count; i++)
                rows[i].WriteRow(sb);
            return sb.ToString();
        }

        /// <summary>Same seed through SeedableRandom yields a byte-identical metrics CSV.</summary>
        [TestMethod]
        public void VirtualSimulation_SeedableRandomGlobalStream_SameSeedIsReproducible()
        {
            string first = RunWithSeedableRandom(4711);
            string second = RunWithSeedableRandom(4711);
            Assert.AreEqual(first, second, "Same SeedableRandom seed must reproduce the metrics CSV exactly");

            // restore an ordinary global stream for other tests
            Nez.Random.SetSeed(System.Environment.TickCount);
        }
    }
}
