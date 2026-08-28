using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// Unit tests for AutoLearnSkillsService — pure static helpers and TryLearnPass are
    /// fully headless (Core.Instance is null in the test host).
    /// </summary>
    [TestClass]
    public class AutoLearnSkillsServiceTests
    {
        // ─── helpers ────────────────────────────────────────────────────────────────

        private static Hero MakeHero(string name, RolePlayingFramework.Jobs.IJob job, int jp = 0)
        {
            var stats   = new StatBlock(4, 3, 5, 1);
            var crystal = new HeroCrystal(name, job, 1, stats);
            if (jp > 0) crystal.EarnJP(jp);
            var hero = new Hero(name, job, 1, stats, crystal);
            return hero;
        }

        // ─── Test 1: Smart mode selects job signature active skill first ─────────

        [TestMethod]
        public void Smart_SelectsSignatureSkill_Priest()
        {
            var hero = MakeHero("Reza", new Priest());
            var skill = AutoLearnSkillsService.SelectNextSkill(hero, AutoLearnMode.Smart);
            Assert.IsNotNull(skill, "Expected a skill; job has unlearned skills");
            Assert.AreEqual("priest.heal", skill.Id, "Priest Smart rank-0 should be Heal");
        }

        [TestMethod]
        public void Smart_SelectsSignatureSkill_Mage()
        {
            var hero = MakeHero("Lyra", new Mage());
            var skill = AutoLearnSkillsService.SelectNextSkill(hero, AutoLearnMode.Smart);
            Assert.IsNotNull(skill);
            Assert.AreEqual("mage.fire", skill.Id, "Mage Smart rank-0 should be Fire");
        }

        [TestMethod]
        public void Smart_SelectsSignatureSkill_Knight()
        {
            var hero = MakeHero("Arn", new Knight());
            var skill = AutoLearnSkillsService.SelectNextSkill(hero, AutoLearnMode.Smart);
            Assert.IsNotNull(skill);
            Assert.AreEqual("knight.spin_slash", skill.Id, "Knight Smart rank-0 should be Spin Slash");
        }

        // ─── Test 2: Smart full 4-skill order per job ───────────────────────────

        [TestMethod]
        public void Smart_FullOrder_Knight()
        {
            // Smart ranks: spin_slash(0,120), provoke(1,50), heavy_armor(2,100), heavy_strike(3,180)
            string[] expected = { "knight.spin_slash", "knight.provoke", "knight.heavy_armor", "knight.heavy_strike" };
            VerifySmartOrder(new Knight(), expected);
        }

        [TestMethod]
        public void Smart_FullOrder_Mage()
        {
            // Smart ranks: fire(0,120), heart_fire(1,60), economist(2,80), firestorm(3,200)
            string[] expected = { "mage.fire", "mage.heart_fire", "mage.economist", "mage.firestorm" };
            VerifySmartOrder(new Mage(), expected);
        }

        [TestMethod]
        public void Smart_FullOrder_Priest()
        {
            // Smart ranks: heal(0,100), calm_spirit(1,50), mender(2,80), defup(3,160)
            string[] expected = { "priest.heal", "priest.calm_spirit", "priest.mender", "priest.defup" };
            VerifySmartOrder(new Priest(), expected);
        }

        [TestMethod]
        public void Smart_FullOrder_Monk()
        {
            // Smart ranks: roundhouse(0,120), counter(1,70), deflect(2,90), flaming_fist(3,170)
            string[] expected = { "monk.roundhouse", "monk.counter", "monk.deflect", "monk.flaming_fist" };
            VerifySmartOrder(new Monk(), expected);
        }

        [TestMethod]
        public void Smart_FullOrder_Thief()
        {
            // Smart ranks: sneak_attack(0,130), shadowstep(1,70), trap_sense(2,90), vanish(3,180)
            string[] expected = { "thief.sneak_attack", "thief.shadowstep", "thief.trap_sense", "thief.vanish" };
            VerifySmartOrder(new Thief(), expected);
        }

        [TestMethod]
        public void Smart_FullOrder_Archer()
        {
            // Smart ranks: power_shot(0,130), eagle_eye(1,70), quickdraw(2,100), volley(3,200)
            string[] expected = { "archer.power_shot", "archer.eagle_eye", "archer.quickdraw", "archer.volley" };
            VerifySmartOrder(new Archer(), expected);
        }

        private static void VerifySmartOrder(RolePlayingFramework.Jobs.IJob job, string[] expectedIds)
        {
            // Build a hero with no JP (skills will be unaffordable, but SelectNextSkill is JP-agnostic)
            var hero = MakeHero("Test", job);
            for (int i = 0; i < expectedIds.Length; i++)
            {
                var skill = AutoLearnSkillsService.SelectNextSkill(hero, AutoLearnMode.Smart);
                Assert.IsNotNull(skill, $"Expected skill at position {i} but got null");
                Assert.AreEqual(expectedIds[i], skill.Id,
                    $"Position {i}: expected {expectedIds[i]} but got {skill.Id}");

                // Earn enough JP and purchase the skill to advance state
                hero.EarnJP(skill.JPCost);
                bool purchased = hero.TryPurchaseSkill(skill);
                Assert.IsTrue(purchased, $"Could not purchase {skill.Id} for test setup");
            }
            // After all skills learned, SelectNextSkill should return null
            var none = AutoLearnSkillsService.SelectNextSkill(hero, AutoLearnMode.Smart);
            Assert.IsNull(none, "Expected null when all skills are learned");
        }

        // ─── Test 3: Smart strict-order — Priest with 60 JP learns nothing ──────

        [TestMethod]
        public void Smart_StrictOrder_PriestWith60JP_LearnsNothing()
        {
            // Heal (rank 0) costs 100 JP; CalmSpirit costs 50 but is rank 1.
            // With 60 JP strict-order stops at Heal (unaffordable) and returns 0.
            var hero = MakeHero("Reza", new Priest(), jp: 60);
            var svc  = new AutoLearnSkillsService { Enabled = true, Mode = AutoLearnMode.Smart };

            int learned = svc.TryLearnPass(hero);
            Assert.AreEqual(0, learned, "Strict order should not skip Heal to buy CalmSpirit");
        }

        // ─── Test 4: Active mode Knight order ───────────────────────────────────

        [TestMethod]
        public void Active_Mode_Knight_Order()
        {
            // Active → actives cheapest-first, then passives cheapest-first
            // Knight actives: Provoke(50,idx 0), SpinSlash(120,idx 2), HeavyStrike(180,idx 3)
            // Knight passives: HeavyArmor(100,idx 1)
            // Expected: provoke → spin_slash → heavy_strike → heavy_armor
            string[] expected = { "knight.provoke", "knight.spin_slash", "knight.heavy_strike", "knight.heavy_armor" };
            VerifyModeOrder(new Knight(), AutoLearnMode.Active, expected);
        }

        // ─── Test 5: Passive mode Knight mirror ─────────────────────────────────

        [TestMethod]
        public void Passive_Mode_Knight_Order()
        {
            // Passive → passives cheapest-first, then actives cheapest-first
            // Knight passives: HeavyArmor(100,idx 1)
            // Knight actives: Provoke(50,idx 0), SpinSlash(120,idx 2), HeavyStrike(180,idx 3)
            // Expected: heavy_armor → provoke → spin_slash → heavy_strike
            string[] expected = { "knight.heavy_armor", "knight.provoke", "knight.spin_slash", "knight.heavy_strike" };
            VerifyModeOrder(new Knight(), AutoLearnMode.Passive, expected);
        }

        private static void VerifyModeOrder(RolePlayingFramework.Jobs.IJob job, AutoLearnMode mode, string[] expectedIds)
        {
            var hero = MakeHero("Test", job);
            for (int i = 0; i < expectedIds.Length; i++)
            {
                var skill = AutoLearnSkillsService.SelectNextSkill(hero, mode);
                Assert.IsNotNull(skill, $"Expected skill at position {i} but got null");
                Assert.AreEqual(expectedIds[i], skill.Id,
                    $"Position {i}: expected {expectedIds[i]} but got {skill.Id}");

                hero.EarnJP(skill.JPCost);
                bool purchased = hero.TryPurchaseSkill(skill);
                Assert.IsTrue(purchased, $"Could not purchase {skill.Id} for test setup");
            }
            var none = AutoLearnSkillsService.SelectNextSkill(hero, mode);
            Assert.IsNull(none, "Expected null when all skills are learned");
        }

        // ─── Test 6: Composite job tie-breaks ───────────────────────────────────

        [TestMethod]
        public void Smart_CompositeTieBreak_KnightMage_SpinSlashBeforeFire()
        {
            // Knight+Mage: SpinSlash (rank 0, cost 120, index 2) vs Fire (rank 0, cost 120, index 6)
            // Tie on rank and cost → lower index wins → spin_slash
            var compositeJob = new CompositeJob(new Knight(), new Mage());
            var hero         = MakeHero("Arn", compositeJob);
            var skill        = AutoLearnSkillsService.SelectNextSkill(hero, AutoLearnMode.Smart);
            Assert.IsNotNull(skill);
            Assert.AreEqual("knight.spin_slash", skill.Id,
                "Index tie-break: spin_slash (idx 2) should beat fire (idx 6)");
        }

        [TestMethod]
        public void Smart_CompositeTieBreak_PriestMage_HealBeforeFire()
        {
            // Priest+Mage: Heal (rank 0, cost 100, index 2) vs Fire (rank 0, cost 120, index 6)
            // Heal wins on cost alone
            var compositeJob = new CompositeJob(new Priest(), new Mage());
            var hero         = MakeHero("Reza", compositeJob);
            var skill        = AutoLearnSkillsService.SelectNextSkill(hero, AutoLearnMode.Smart);
            Assert.IsNotNull(skill);
            Assert.AreEqual("priest.heal", skill.Id,
                "Cost tie-break: heal (cost 100) should beat fire (cost 120)");
        }

        // ─── Test 7: Multi-learn, disabled guard, null guard, SanitizeMode ──────

        [TestMethod]
        public void TryLearnPass_MultiLearn_OnePass()
        {
            // Knight Smart order: spin_slash(120), provoke(50), heavy_armor(100), heavy_strike(180)
            // With 400 JP: 120+50+100=270 spent → 130 left; heavy_strike costs 180 → stops.
            // Expects 3 skills learned.
            var hero = MakeHero("Arn", new Knight(), jp: 400);
            var svc  = new AutoLearnSkillsService { Enabled = true, Mode = AutoLearnMode.Smart };

            int learned = svc.TryLearnPass(hero);
            Assert.AreEqual(3, learned, "Should learn spin_slash + provoke + heavy_armor before running short");
        }

        [TestMethod]
        public void TryLearnNow_Headless_ReturnsZero()
        {
            // In headless tests Core.Instance is null; TryLearnNow guards this.
            var svc = new AutoLearnSkillsService { Enabled = true };
            int result = svc.TryLearnNow();
            Assert.AreEqual(0, result, "TryLearnNow should return 0 when Core.Instance is null");
        }

        [TestMethod]
        public void TryLearnPass_NullHero_ReturnsZero()
        {
            var svc = new AutoLearnSkillsService { Enabled = true };
            int result = svc.TryLearnPass(null);
            Assert.AreEqual(0, result, "TryLearnPass(null) should return 0");
        }

        [TestMethod]
        public void SelectNextSkill_NullHero_ReturnsNull()
        {
            var skill = AutoLearnSkillsService.SelectNextSkill(null, AutoLearnMode.Smart);
            Assert.IsNull(skill, "SelectNextSkill(null hero) should return null");
        }

        [TestMethod]
        public void SanitizeMode_OutOfRange_ReturnsSmart()
        {
            Assert.AreEqual(AutoLearnMode.Smart, AutoLearnSkillsService.SanitizeMode(-1),  "Negative → Smart");
            Assert.AreEqual(AutoLearnMode.Smart, AutoLearnSkillsService.SanitizeMode(3),   "3 → Smart");
            Assert.AreEqual(AutoLearnMode.Smart, AutoLearnSkillsService.SanitizeMode(99),  "99 → Smart");
        }

        [TestMethod]
        public void SanitizeMode_ValidValues_RoundTrip()
        {
            Assert.AreEqual(AutoLearnMode.Smart,   AutoLearnSkillsService.SanitizeMode(0));
            Assert.AreEqual(AutoLearnMode.Active,  AutoLearnSkillsService.SanitizeMode(1));
            Assert.AreEqual(AutoLearnMode.Passive, AutoLearnSkillsService.SanitizeMode(2));
        }
    }
}
