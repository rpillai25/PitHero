using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.VirtualGame;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Stats;
using System.Collections.Generic;
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
        /// With <paramref name="withParty"/>, two level-appropriate mercenaries join:
        /// a Priest (exercises the heal path) and an Archer (extra damage).
        /// Fresh mercenaries are created per level so each level is independent
        /// (no inn-rest simulation exists between levels yet).
        /// </summary>
        private static VirtualRunMetrics RunLevel(int seed, int pitLevel, bool withParty = false)
        {
            var sim = new VirtualGameSimulation(seed);
            int heroLevel = BalanceConfig.EstimatePlayerLevelForPitLevel(pitLevel);

            // Live heroes know their purchased job skills (JP via bound crystal) — mirror
            // that with a JP-loaded crystal and buy the full Knight kit.
            var crystal = new HeroCrystal("RefCrystal", new Knight(), heroLevel, new StatBlock(10, 8, 10, 4));
            crystal.EarnJP(1_000_000);
            sim.ConfigureHero(new Knight(), heroLevel, new StatBlock(10, 8, 10, 4), crystal);

            var hero = sim.Hero.LinkedHero;
            var jobSkills = hero.Job.Skills;
            for (int i = 0; i < jobSkills.Count; i++)
                hero.TryPurchaseSkill(jobSkills[i]);

            // A live new game starts with HP Potions — stock the shared bag
            for (int i = 0; i < 5; i++)
                sim.Bag.TryAdd(PotionItems.HPPotion());

            if (withParty)
            {
                // Live tavern mercs always know their full job kit (MercenaryManager
                // calls LearnAllJobSkills on spawn and on save-restore) — mirror that.
                var cleric = new Mercenary("Cleric", new Priest(), heroLevel, new StatBlock(6, 8, 8, 12));
                cleric.LearnAllJobSkills();
                var scout = new Mercenary("Scout", new Archer(), heroLevel, new StatBlock(9, 12, 8, 5));
                scout.LearnAllJobSkills();
                sim.ConfigureMercenaries(new List<Mercenary> { cleric, scout });
            }

            return sim.RunPitLevel(pitLevel);
        }

        /// <summary>
        /// Runs all sample levels for both configurations (solo Knight, then
        /// Knight + Priest + Archer party) plus ONE persistent-run simulation
        /// covering pit levels 1–25 with the gold-economy policy (auto-hire + inn),
        /// and returns the combined CSV report.
        /// The persistent block is seeded identically to the others, making all three
        /// blocks reproducible under the same-seed contract.
        /// </summary>
        private static string RunTraversalCsv(int seed)
        {
            var sb = new StringBuilder(1024);
            using (var writer = new StringWriter(sb))
            {
                writer.WriteLine("# solo Knight");
                VirtualRunMetrics.WriteCsvHeader(writer);
                for (int i = 0; i < SampleLevels.Length; i++)
                    RunLevel(seed, SampleLevels[i]).WriteRow(writer);

                writer.WriteLine("# party: Knight + Priest + Archer");
                VirtualRunMetrics.WriteCsvHeader(writer);
                for (int i = 0; i < SampleLevels.Length; i++)
                    RunLevel(seed, SampleLevels[i], withParty: true).WriteRow(writer);

                writer.WriteLine("# persistent run: Knight, auto-hire + inn");
                VirtualRunMetrics.WriteCsvHeader(writer);
                var persistent = RunPersistentRange(seed);
                for (int i = 0; i < persistent.Count; i++)
                    persistent[i].WriteRow(writer);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Runs a persistent pit-1-to-25 traversal with a fresh level-1 crystal-equipped
        /// Knight using the live starting gold and gold-economy policy (auto-hire + inn).
        /// Level-ups accumulate naturally from battle XP exactly as in live play.
        /// </summary>
        private static List<VirtualRunMetrics> RunPersistentRange(int seed)
        {
            var sim = new VirtualGameSimulation(seed);

            // Mirror live new-game setup: crystal with all JP, full Knight skill kit
            var crystal = new HeroCrystal("RefCrystal", new Knight(), 1, new StatBlock(10, 8, 10, 4));
            crystal.EarnJP(1_000_000);
            sim.ConfigureHero(new Knight(), 1, new StatBlock(10, 8, 10, 4), crystal);

            var hero     = sim.Hero.LinkedHero;
            var jobSkills = hero.Job.Skills;
            for (int i = 0; i < jobSkills.Count; i++)
                hero.TryPurchaseSkill(jobSkills[i]);

            // Live new game grants 5 HP Potions (GameConfig.NewGameStartingHPPotions)
            for (int i = 0; i < 5; i++)
                sim.Bag.TryAdd(PotionItems.HPPotion());

            // Gold wallet defaults to GameConfig.NewGameStartingGold (200) — no override needed.
            // No ConfigureMercenaries call: roster starts empty; RunLevelRange hires on demand.

            return sim.RunLevelRange(1, 25);
        }

        /// <summary>
        /// Traverses the sampled Cave levels with a level-appropriate Knight — solo and
        /// with a Priest + Archer party — and prints both per-level metrics tables
        /// (pitLevel, battles, rounds, dmgDealt, dmgTaken, hpLossPct, healing, deaths,
        /// wiped).  Sanity: every sampled level must produce battles in both
        /// configurations and pit level 1 must be beatable.
        /// </summary>
        [TestMethod]
        [TestCategory("BalanceTraversal")]
        public void BalanceTraversal_SampledCaveLevels_ProducesPerLevelMetrics()
        {
            var sb = new StringBuilder(1024);
            using var writer = new StringWriter(sb);

            writer.WriteLine("# solo Knight");
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

            writer.WriteLine("# party: Knight + Priest + Archer");
            VirtualRunMetrics.WriteCsvHeader(writer);
            for (int i = 0; i < SampleLevels.Length; i++)
            {
                int pitLevel = SampleLevels[i];
                var metrics = RunLevel(Seed, pitLevel, withParty: true);
                metrics.WriteRow(writer);

                Assert.IsTrue(metrics.BattleCount > 0,
                    $"Pit {pitLevel} (party): traversal must fight at least one battle");
                Assert.IsTrue(metrics.DamageDealt > 0,
                    $"Pit {pitLevel} (party): allies must deal damage in real combat");
            }

            // ── Persistent run block: level-1 Knight with gold economy, pit 1-25 ──
            writer.WriteLine("# persistent run: Knight, auto-hire + inn");
            VirtualRunMetrics.WriteCsvHeader(writer);
            var persistent = RunPersistentRange(Seed);
            for (int i = 0; i < persistent.Count; i++)
                persistent[i].WriteRow(writer);

            // The balance-report tables — visible in test output for the pit-balance-test skill
            System.Console.WriteLine(sb.ToString());

            // Acceptance floor: a level-appropriate hero must clear pit level 1 either way
            Assert.IsFalse(RunLevel(Seed, 1).Wiped,
                "A level-appropriate solo Knight must clear pit level 1");
            Assert.IsFalse(RunLevel(Seed, 1, withParty: true).Wiped,
                "A level-appropriate party must clear pit level 1");

            // Persistent-run sanity: at least 1 level traversed; if hero survived all 25, all rows present
            Assert.IsTrue(persistent.Count >= 1,
                "Persistent run must traverse at least 1 level");
            var lastPersistent = persistent[persistent.Count - 1];
            if (!lastPersistent.Wiped)
            {
                Assert.AreEqual(25, persistent.Count,
                    "If the persistent-run hero was not wiped, all 25 level rows must be present");
            }

            // Every traversed level must actually be played: battles fought on each level
            // (guards against per-level GOAP flags leaking across levels and skipping levels).
            for (int i = 0; i < persistent.Count; i++)
            {
                Assert.IsTrue(persistent[i].BattleCount > 0,
                    $"Persistent run pit {persistent[i].PitLevel}: must fight at least one battle");
            }
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
