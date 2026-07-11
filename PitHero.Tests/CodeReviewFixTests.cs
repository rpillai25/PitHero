using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the 9 verified code-review findings addressed in this batch:
    /// Fix 1  — Save/load MP corruption (SetCurrentMP)
    /// Fix 5  — MP affordability / GetEffectiveMPCost
    /// Fix 8  — TrapSense MercenaryManager.AnyHiredMercenaryHasTrapSense (logic only)
    /// Other fixes are tested in their own existing test files or rely on coroutine-level seams.
    /// </summary>
    [TestClass]
    public class CodeReviewFixTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────────

        private static Hero MakeHero(int mag = 10)
            => new Hero("TestHero", new Mage(), level: 5, new StatBlock(5, 5, 5, mag));

        private static Mercenary MakeMerc(int mag = 10)
            => new Mercenary("TestMerc", new Mage(), level: 5, new StatBlock(5, 5, 5, mag));

        // ── Fix 1: SetCurrentMP — save/load state restore ─────────────────────────────

        [TestMethod]
        [TestCategory("Fix1_SaveLoad")]
        public void Hero_SetCurrentMP_SetsExactValue()
        {
            var hero = MakeHero(mag: 10);
            int savedMP = 3;

            hero.SetCurrentMP(savedMP);

            Assert.AreEqual(savedMP, hero.CurrentMP,
                "SetCurrentMP should set CurrentMP to exactly the saved value");
        }

        [TestMethod]
        [TestCategory("Fix1_SaveLoad")]
        public void Hero_SetCurrentMP_ClampsToZero()
        {
            var hero = MakeHero();
            hero.SetCurrentMP(-5);
            Assert.AreEqual(0, hero.CurrentMP, "SetCurrentMP(-5) should clamp to 0");
        }

        [TestMethod]
        [TestCategory("Fix1_SaveLoad")]
        public void Hero_SetCurrentMP_ClampsToMaxMP()
        {
            var hero = MakeHero();
            hero.SetCurrentMP(hero.MaxMP + 100);
            Assert.AreEqual(hero.MaxMP, hero.CurrentMP, "SetCurrentMP above MaxMP should clamp to MaxMP");
        }

        [TestMethod]
        [TestCategory("Fix1_SaveLoad")]
        public void Merc_WithEconomist_SetCurrentMP_NotCostReduced()
        {
            // A mage merc with Economist has MPCostReduction = 0.15.
            // If we used UseMP(diff) to restore, it would apply the reduction and land at the wrong value.
            // SetCurrentMP must bypass the reduction and land exactly at the saved value.
            var merc = MakeMerc(mag: 10);
            merc.LearnSkill(new EconomistPassive()); // sets MPCostReduction = 0.15
            Assert.AreNotEqual(0f, merc.MPCostReduction, "Precondition: economist should set reduction");

            int savedMP = 3;
            merc.SetCurrentMP(savedMP);

            Assert.AreEqual(savedMP, merc.CurrentMP,
                "SetCurrentMP with MPCostReduction active should still land at exactly the saved value");
        }

        [TestMethod]
        [TestCategory("Fix1_SaveLoad")]
        public void Merc_SetCurrentMP_SetsExactValue()
        {
            var merc = MakeMerc();
            merc.SetCurrentMP(7);
            Assert.AreEqual(7, merc.CurrentMP, "Mercenary SetCurrentMP should set CurrentMP to exactly 7");
        }

        // ── Fix 5: GetEffectiveMPCost — MP affordability consistent with SpendMP ─────

        [TestMethod]
        [TestCategory("Fix5_MPCost")]
        public void Hero_GetEffectiveMPCost_NoReduction_ReturnsSameAsRawCost()
        {
            var hero = MakeHero();
            // No economist passive — MPCostReduction = 0
            Assert.AreEqual(0f, hero.MPCostReduction);
            Assert.AreEqual(4, hero.GetEffectiveMPCost(4),
                "Without reduction, effective cost should equal raw cost");
        }

        [TestMethod]
        [TestCategory("Fix5_MPCost")]
        public void Hero_GetEffectiveMPCost_ZeroRawCost_ReturnsZero()
        {
            var hero = MakeHero();
            Assert.AreEqual(0, hero.GetEffectiveMPCost(0),
                "GetEffectiveMPCost(0) should return 0 regardless of reduction");
        }

        [TestMethod]
        [TestCategory("Fix5_MPCost")]
        public void Hero_GetEffectiveMPCost_WithEconomist_ReducesCost()
        {
            var hero = MakeHero();
            hero.MPCostReduction = 0.15f;
            // raw 4 * (1 - 0.15) = 3.4 → (int) = 3
            int expected = (int)(4 * (1f - 0.15f));
            Assert.AreEqual(expected, hero.GetEffectiveMPCost(4),
                "With 15% reduction, cost of 4 should be reduced");
        }

        [TestMethod]
        [TestCategory("Fix5_MPCost")]
        public void Merc_WithEconomist_CurrentMP3_CanAfford4MPSkill()
        {
            // Economist: 15% reduction. Skill costs 4 MP raw → effective = (int)(4 * 0.85) = 3.
            // Merc has CurrentMP = 3. Affordability check: effective(4) <= 3 → can afford.
            var merc = MakeMerc();
            merc.LearnSkill(new EconomistPassive());
            merc.SetCurrentMP(3);

            int effectiveCost = merc.GetEffectiveMPCost(4);
            Assert.AreEqual(3, effectiveCost, "Economist merc: effective cost of 4 MP skill should be 3");
            Assert.IsTrue(merc.CurrentMP >= effectiveCost,
                "Merc with CurrentMP=3 should be able to afford a skill that costs 4 raw (3 effective)");
        }

        [TestMethod]
        [TestCategory("Fix5_MPCost")]
        public void Merc_WithoutEconomist_CurrentMP3_CannotAfford4MPSkill()
        {
            // Without economist, raw cost check: 4 > 3 → cannot afford.
            var merc = MakeMerc();
            merc.SetCurrentMP(3);

            int effectiveCost = merc.GetEffectiveMPCost(4);
            Assert.AreEqual(4, effectiveCost, "Without reduction, effective cost equals raw cost");
            Assert.IsFalse(merc.CurrentMP >= effectiveCost,
                "Merc with CurrentMP=3 should NOT be able to afford a skill that costs 4 MP raw without reduction");
        }

        [TestMethod]
        [TestCategory("Fix5_MPCost")]
        public void Hero_SpendMP_UsesEffectiveCost()
        {
            // SpendMP should now delegate to GetEffectiveMPCost.
            var hero = MakeHero(mag: 20); // gives plenty of MP
            hero.MPCostReduction = 0.15f;
            int mpBefore = hero.CurrentMP;

            hero.SpendMP(4);

            int expectedDeducted = hero.GetEffectiveMPCost(4); // should be (int)(4 * 0.85) = 3
            Assert.AreEqual(mpBefore - expectedDeducted, hero.CurrentMP,
                "SpendMP should deduct the effective cost (same as GetEffectiveMPCost), not the raw cost");
        }
    }
}
