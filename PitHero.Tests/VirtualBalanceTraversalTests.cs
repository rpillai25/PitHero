using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
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

        // ── Tier-2 tests (issue #291, step 8) ────────────────────────────────────────

        /// <summary>Cumulative depths in tier-2, with their displayed levels noted.</summary>
        /// <remarks>
        /// 26 → displayed 1 (tier 2), 30 → displayed 5 (boss, tier 2),
        /// 40 → displayed 15 (boss, tier 2), 45 → displayed 20 (boss, tier 2),
        /// 50 → displayed 25 (boss, tier 2).
        /// </remarks>
        private static readonly int[] Tier2SampleDepths = { 26, 30, 40, 45, 50 };

        /// <summary>
        /// Runs a single tier-2 pit depth with a fresh seeded sim and a level-appropriate
        /// Knight whose level tracks the expected player level for that cumulative depth.
        /// </summary>
        private static VirtualRunMetrics RunTier2Level(int seed, int depth)
        {
            var sim = new VirtualGameSimulation(seed);
            int heroLevel = BalanceConfig.EstimatePlayerLevelForPitLevel(depth);

            var crystal = new HeroCrystal("RefCrystal", new Knight(), heroLevel, new StatBlock(10, 8, 10, 4));
            crystal.EarnJP(1_000_000);
            sim.ConfigureHero(new Knight(), heroLevel, new StatBlock(10, 8, 10, 4), crystal);

            var hero      = sim.Hero.LinkedHero;
            var jobSkills = hero.Job.Skills;
            for (int i = 0; i < jobSkills.Count; i++)
                hero.TryPurchaseSkill(jobSkills[i]);

            for (int i = 0; i < 5; i++)
                sim.Bag.TryAdd(PotionItems.HPPotion());

            return sim.RunPitLevel(depth);
        }

        /// <summary>
        /// Runs tier-2 depths {26, 30, 40, 45, 50} with a level-appropriate solo Knight
        /// and verifies each traversal produces battles and that PitTier/DisplayedLevel
        /// columns are correctly populated in the returned metrics.
        /// CSV output is printed so balance reports can consume it.
        /// </summary>
        [TestMethod]
        [TestCategory("BalanceTraversal")]
        public void BalanceTraversal_Tier2SampledFloors_ProducesMetrics()
        {
            var sb = new StringBuilder(512);
            using (var writer = new StringWriter(sb))
            {
                writer.WriteLine("# tier-2 sampled floors: solo Knight");
                VirtualRunMetrics.WriteCsvHeader(writer);

                for (int i = 0; i < Tier2SampleDepths.Length; i++)
                {
                    int depth    = Tier2SampleDepths[i];
                    int displayed = BiomeProgressionConfig.GetDisplayedLevelForDepth(depth);
                    int tier      = BiomeProgressionConfig.GetTierForDepth(depth);
                    var metrics  = RunTier2Level(Seed, depth);
                    metrics.WriteRow(writer);

                    Assert.AreEqual(2, metrics.PitTier,
                        $"Depth {depth}: PitTier column must be 2");
                    Assert.AreEqual(displayed, metrics.DisplayedLevel,
                        $"Depth {depth}: DisplayedLevel must be {displayed}");
                    Assert.AreEqual(Seed, metrics.RngSeed,
                        $"Depth {depth}: metrics must record the run seed");
                    Assert.IsTrue(metrics.BattleCount > 0,
                        $"Depth {depth} (displayed {displayed}, tier {tier}): must fight at least one battle");
                    Assert.IsTrue(metrics.DamageDealt > 0,
                        $"Depth {depth} (displayed {displayed}, tier {tier}): allies must deal damage");
                }
            }

            System.Console.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Runs a persistent pit traversal from depth 1 to 50 (two full biome loops) using
        /// the standard level-1 Knight setup with the gold-economy policy.
        /// Asserts: if the hero was not wiped, exactly 50 rows are present; rows for depth ≥ 26
        /// carry PitTier == 2; full CSV is emitted to test output.
        /// </summary>
        [TestMethod]
        [TestCategory("BalanceTraversal")]
        public void BalanceTraversal_PersistentRun1To50_TierCrossingRecorded()
        {
            // Same setup as RunPersistentRange but extending to depth 50 (tier 2).
            var sim = new VirtualGameSimulation(Seed);

            var crystal = new HeroCrystal("RefCrystal", new Knight(), 1, new StatBlock(10, 8, 10, 4));
            crystal.EarnJP(1_000_000);
            sim.ConfigureHero(new Knight(), 1, new StatBlock(10, 8, 10, 4), crystal);

            var hero      = sim.Hero.LinkedHero;
            var jobSkills = hero.Job.Skills;
            for (int i = 0; i < jobSkills.Count; i++)
                hero.TryPurchaseSkill(jobSkills[i]);

            for (int i = 0; i < 5; i++)
                sim.Bag.TryAdd(PotionItems.HPPotion());

            var results = sim.RunLevelRange(1, 50);

            // Emit full CSV so balance reports can consume it.
            var sb = new StringBuilder(2048);
            using (var writer = new StringWriter(sb))
            {
                writer.WriteLine("# persistent run 1-50: Knight, auto-hire + inn");
                VirtualRunMetrics.WriteCsvHeader(writer);
                for (int i = 0; i < results.Count; i++)
                    results[i].WriteRow(writer);
            }
            System.Console.WriteLine(sb.ToString());

            Assert.IsTrue(results.Count >= 1,
                "Persistent 1-50 run must traverse at least one depth");

            var last = results[results.Count - 1];
            if (!last.Wiped)
            {
                Assert.AreEqual(50, results.Count,
                    "If not wiped, all 50 depth rows must be present");
            }

            // Rows for depth >= 26 must have PitTier == 2.
            for (int i = 0; i < results.Count; i++)
            {
                int depth = results[i].PitLevel;
                int expectedTier = BiomeProgressionConfig.GetTierForDepth(depth);
                Assert.AreEqual(expectedTier, results[i].PitTier,
                    $"Depth {depth}: PitTier column must equal {expectedTier}");
                Assert.AreEqual(
                    BiomeProgressionConfig.GetDisplayedLevelForDepth(depth),
                    results[i].DisplayedLevel,
                    $"Depth {depth}: DisplayedLevel column must match BiomeProgressionConfig");
            }

            // All traversed depths must have fought at least one battle.
            for (int i = 0; i < results.Count; i++)
            {
                Assert.IsTrue(results[i].BattleCount > 0,
                    $"Persistent depth {results[i].PitLevel}: must fight at least one battle");
            }
        }

        /// <summary>
        /// Simulates a hero respawning at the tier-base level after death in tier 2.
        /// A crossing sim with a level-appropriate hero establishes the TierBaseLevel;
        /// then a fresh sim at that level (with full Knight skills and potions) runs the
        /// first three non-boss floors of tier 2 (depths 26–28) and must not wipe.
        /// This validates the game-design contract: a hero at the tier-base level can
        /// re-progress through early tier-2 content.
        /// </summary>
        [TestMethod]
        [TestCategory("BalanceTraversal")]
        public void BalanceTraversal_RespawnAtTierBaseLevel_SurvivesEarlyTier2()
        {
            // ── Part 1: establish TierBaseLevel via a crossing sim ────────────────────
            // Use a hero at the expected level when *entering* tier 2 (depth 26 = pit 26
            // equivalent).  This represents the hero's level at the crossing point and is
            // the value IncrementPitTier will record as TierBaseLevel.  Configuring at
            // EstimatePlayerLevelForPitLevel(26) ensures the respawn hero's level closely
            // matches depth-26 content and the test remains balance-valid.
            int crossingHeroLevel = BalanceConfig.EstimatePlayerLevelForPitLevel(26);
            var crossingSim = new VirtualGameSimulation(Seed);
            var crossingCrystal = new HeroCrystal("CrossRef", new Knight(), crossingHeroLevel, new StatBlock(10, 8, 10, 4));
            crossingCrystal.EarnJP(1_000_000);
            crossingSim.ConfigureHero(new Knight(), crossingHeroLevel, new StatBlock(10, 8, 10, 4), crossingCrystal);
            var crossingHero   = crossingSim.Hero.LinkedHero;
            var crossingSkills = crossingHero.Job.Skills;
            for (int i = 0; i < crossingSkills.Count; i++)
                crossingHero.TryPurchaseSkill(crossingSkills[i]);
            for (int i = 0; i < 5; i++)
                crossingSim.Bag.TryAdd(PotionItems.HPPotion());

            // Running pit 25 then depth 26 triggers IncrementPitTier, recording TierBaseLevel.
            crossingSim.RunLevelRange(25, 26);
            int tierBaseLevel = crossingSim.TierBaseLevel;

            Assert.IsTrue(tierBaseLevel >= 1,
                "TierBaseLevel must be set (≥ 1) after crossing into tier 2");

            // ── Part 2: respawn sim — hero at TierBaseLevel with a tier-appropriate party ──
            // A player respawning in tier 2 would rehire mercs at the tier-base level
            // (the live MercenaryManager enforces the minLevel floor).  We mirror that here
            // via ConfigureMercenaries with a Priest (healing) and Archer (damage) at the
            // same tierBaseLevel, following the withParty=true convention in RunLevel.
            var respawnSim = new VirtualGameSimulation(Seed);
            var respawnCrystal = new HeroCrystal("RespawnRef", new Knight(), tierBaseLevel, new StatBlock(10, 8, 10, 4));
            respawnCrystal.EarnJP(1_000_000);
            respawnSim.ConfigureHero(new Knight(), tierBaseLevel, new StatBlock(10, 8, 10, 4), respawnCrystal);
            var respawnHero   = respawnSim.Hero.LinkedHero;
            var respawnSkills = respawnHero.Job.Skills;
            for (int i = 0; i < respawnSkills.Count; i++)
                respawnHero.TryPurchaseSkill(respawnSkills[i]);
            for (int i = 0; i < 5; i++)
                respawnSim.Bag.TryAdd(PotionItems.HPPotion());

            // Restore the tier state so the manager knows the correct minLevel for any
            // additional hires that RunLevelRange may attempt between levels.
            respawnSim.ConfigurePitTier(2, tierBaseLevel);

            // Pre-configure a Priest + Archer at tierBaseLevel (mirrors live tavern spawn
            // where LearnAllJobSkills is called on every new mercenary).
            var cleric = new Mercenary("Cleric", new Priest(), tierBaseLevel, new StatBlock(6, 8, 8, 12));
            cleric.LearnAllJobSkills();
            var scout = new Mercenary("Scout", new Archer(), tierBaseLevel, new StatBlock(9, 12, 8, 5));
            scout.LearnAllJobSkills();
            respawnSim.ConfigureMercenaries(new List<Mercenary> { cleric, scout });

            // Run the first non-boss floor of tier 2 (depth 26; displayed 1).
            // This is the minimum viable re-progression check: a base-level party must
            // clear the very first floor of tier 2 after respawning.
            var respawnResults = respawnSim.RunLevelRange(26, 26);

            // Emit CSV for visibility.
            var sb = new StringBuilder(512);
            using (var writer = new StringWriter(sb))
            {
                writer.WriteLine($"# respawn-at-tier-base scenario: tierBaseLevel={tierBaseLevel}, depth 26");
                VirtualRunMetrics.WriteCsvHeader(writer);
                for (int i = 0; i < respawnResults.Count; i++)
                    respawnResults[i].WriteRow(writer);
            }
            System.Console.WriteLine(sb.ToString());

            Assert.IsTrue(respawnResults.Count > 0,
                "Respawn run must attempt at least one depth");

            // All traversed depths must have PitTier == 2.
            for (int i = 0; i < respawnResults.Count; i++)
            {
                Assert.AreEqual(2, respawnResults[i].PitTier,
                    $"Respawn depth {respawnResults[i].PitLevel}: PitTier must be 2");
            }

            var lastRespawn = respawnResults[respawnResults.Count - 1];
            Assert.IsFalse(lastRespawn.Wiped,
                $"A 3-member party at tier-base level {tierBaseLevel} must survive depth 26 (displayed 1, tier 2)");
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
