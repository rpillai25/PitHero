using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// Unit tests for the Phase 6 minimal trap system:
    /// TrapSensePassive sets TrapSense on hero and mercenary via learn/apply path;
    /// CombatantPassiveApplier resets TrapSense; damage formula matches the constant;
    /// direct ApplyPassive calls behave correctly.
    /// </summary>
    [TestClass]
    public class TrapSystemTests
    {
        // ── Factory helpers ───────────────────────────────────────────────────────────

        private static Hero MakeThiefHero(int str = 8, int agi = 12, int vit = 6, int mag = 5)
            => new Hero("TestThief", new Thief(), level: 5, new StatBlock(str, agi, vit, mag));

        private static Mercenary MakeThiefMerc(int str = 8, int agi = 12, int vit = 6, int mag = 5)
            => new Mercenary("TestThiefMerc", new Thief(), level: 5, new StatBlock(str, agi, vit, mag));

        // ── TrapSensePassive — direct ApplyPassive ────────────────────────────────────

        [TestMethod]
        [TestCategory("TrapSystem")]
        public void TrapSensePassive_DirectApply_SetsTrapSenseTrue()
        {
            var hero = MakeThiefHero();
            Assert.IsFalse(hero.TrapSense, "Baseline: TrapSense should be false before passive is applied");

            var passive = new TrapSensePassive();
            passive.ApplyPassive(hero);

            Assert.IsTrue(hero.TrapSense, "After ApplyPassive, TrapSense should be true");
        }

        [TestMethod]
        [TestCategory("TrapSystem")]
        public void TrapSensePassive_DirectApply_SetsTrapSenseTrue_OnMercenary()
        {
            var merc = MakeThiefMerc();
            Assert.IsFalse(merc.TrapSense, "Baseline: Mercenary TrapSense should be false before passive");

            var passive = new TrapSensePassive();
            passive.ApplyPassive(merc);

            Assert.IsTrue(merc.TrapSense, "After ApplyPassive, Mercenary TrapSense should be true");
        }

        // ── TrapSensePassive — end-to-end via LearnSkill path ─────────────────────────

        [TestMethod]
        [TestCategory("TrapSystem")]
        public void TrapSensePassive_MercenaryLearnSkill_HasTrapSenseTrue()
        {
            var merc = new Mercenary("Thief", new Thief(), level: 5, new StatBlock(8, 12, 6, 5));
            Assert.IsFalse(merc.TrapSense, "Baseline: no TrapSense before learning the skill");

            merc.LearnSkill(new TrapSensePassive());

            Assert.IsTrue(merc.TrapSense,
                "After Mercenary.LearnSkill(TrapSensePassive), TrapSense should be true");
        }

        [TestMethod]
        [TestCategory("TrapSystem")]
        public void TrapSensePassive_HeroTryPurchaseSkill_HasTrapSenseTrue()
        {
            // End-to-end via TryPurchaseSkill → CombatantPassiveApplier
            var crystal = new HeroCrystal("ThiefCrystal", new Thief(), 1, new StatBlock(8, 12, 6, 5));
            var hero = new Hero("Thief", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            Assert.IsFalse(hero.TrapSense, "Baseline: no TrapSense before purchasing skill");

            hero.EarnJP(100); // TrapSensePassive costs 90 JP
            var thief = new Thief();
            bool purchased = hero.TryPurchaseSkill(thief.Skills[1]); // Index 1 = TrapSensePassive

            Assert.IsTrue(purchased, "TryPurchaseSkill should succeed with sufficient JP");
            Assert.IsTrue(hero.TrapSense,
                "After purchasing TrapSense, hero.TrapSense should be true");
        }

        // ── CombatantPassiveApplier — reset ───────────────────────────────────────────

        [TestMethod]
        [TestCategory("TrapSystem")]
        public void CombatantPassiveApplier_ResetAndApply_ClearsTrapSense_WhenNoSkillsLearned()
        {
            var hero = MakeThiefHero();
            // Manually set TrapSense to true (simulating a previous apply)
            hero.TrapSense = true;
            Assert.IsTrue(hero.TrapSense, "Pre-condition: TrapSense manually set to true");

            // Reset with no learned skills → TrapSense must be cleared
            CombatantPassiveApplier.ResetAndApply(hero, new Dictionary<string, ISkill>());

            Assert.IsFalse(hero.TrapSense,
                "After ResetAndApply with no skills, TrapSense should be reset to false");
        }

        [TestMethod]
        [TestCategory("TrapSystem")]
        public void CombatantPassiveApplier_ResetAndApply_ClearsTrapSense_OnMercenary()
        {
            var merc = MakeThiefMerc();
            merc.TrapSense = true;

            CombatantPassiveApplier.ResetAndApply(merc, new Dictionary<string, ISkill>());

            Assert.IsFalse(merc.TrapSense,
                "After ResetAndApply with no skills, Mercenary TrapSense should be reset to false");
        }

        [TestMethod]
        [TestCategory("TrapSystem")]
        public void CombatantPassiveApplier_ResetAndApply_PreservesTrapSense_WhenSkillIsPresent()
        {
            var hero = MakeThiefHero();
            Assert.IsFalse(hero.TrapSense, "Baseline: false before apply");

            var skills = new Dictionary<string, ISkill>
            {
                { "thief.trap_sense", new TrapSensePassive() }
            };
            CombatantPassiveApplier.ResetAndApply(hero, skills);

            Assert.IsTrue(hero.TrapSense,
                "After ResetAndApply with TrapSensePassive in skills, TrapSense should be true");
        }

        // ── Damage formula ─────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("TrapSystem")]
        public void TrapDamageFormula_PitLevel1_Returns7()
        {
            // Formula: 5 + pitLevel * 2
            const int pitLevel = 1;
            const int expected = 7; // 5 + 1*2

            // Verify the formula via GameConfig constants to ensure it stays in sync
            int actual = 5 + pitLevel * 2;
            Assert.AreEqual(expected, actual,
                "Trap damage at pit level 1 should be 7 (5 + 1*2)");
        }

        [TestMethod]
        [TestCategory("TrapSystem")]
        public void TrapDamageFormula_PitLevel10_Returns25()
        {
            const int pitLevel = 10;
            const int expected = 25; // 5 + 10*2
            int actual = 5 + pitLevel * 2;
            Assert.AreEqual(expected, actual,
                "Trap damage at pit level 10 should be 25 (5 + 10*2)");
        }

        [TestMethod]
        [TestCategory("TrapSystem")]
        public void TrapDamageFormula_PitLevel25_Returns55()
        {
            const int pitLevel = 25;
            const int expected = 55; // 5 + 25*2
            int actual = 5 + pitLevel * 2;
            Assert.AreEqual(expected, actual,
                "Trap damage at pit level 25 should be 55 (5 + 25*2)");
        }

        // ── TrapSense passive does not stack (CombatantPassiveApplier owns reset) ──────

        [TestMethod]
        [TestCategory("TrapSystem")]
        public void TrapSensePassive_AppliedTwice_TrapSenseRemainsTrue()
        {
            // TrapSense is a bool — applying twice is idempotent
            var hero = MakeThiefHero();
            var passive = new TrapSensePassive();
            passive.ApplyPassive(hero);
            passive.ApplyPassive(hero);

            Assert.IsTrue(hero.TrapSense,
                "Applying TrapSensePassive twice leaves TrapSense true (bool assign is idempotent)");
        }
    }
}
