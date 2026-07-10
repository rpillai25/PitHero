using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for healing-skill Magic scaling: (base + MAG × factor) × (1 + heal power bonus).
    /// </summary>
    [TestClass]
    public class SkillHealScalingTests
    {
        [TestMethod]
        public void CalculateSkillHealAmount_MatchesFormula()
        {
            // base 25, MAG 11, no bonus → 25 + 22 = 47 (≈ the old flat 50 at early-game MAG)
            Assert.AreEqual(47, BalanceConfig.CalculateSkillHealAmount(25, 11, 0f));

            // base 25, MAG 18, no bonus → 25 + 36 = 61 (keeps pace with pit 17-18 monster hits)
            Assert.AreEqual(61, BalanceConfig.CalculateSkillHealAmount(25, 18, 0f));

            // base 25, MAG 18, Mender +25% → 61 × 1.25 = 76
            Assert.AreEqual(76, BalanceConfig.CalculateSkillHealAmount(25, 18, 0.25f));

            // base 25, MAG 99 cap, no bonus → 25 + 198 = 223
            Assert.AreEqual(223, BalanceConfig.CalculateSkillHealAmount(25, 99, 0f));
        }

        [TestMethod]
        public void CalculateSkillHealAmount_ScalesWithMagic()
        {
            var low = BalanceConfig.CalculateSkillHealAmount(25, 11, 0f);
            var high = BalanceConfig.CalculateSkillHealAmount(25, 18, 0f);
            Assert.IsTrue(high > low, "Heal amount must grow with the caster's Magic stat");
        }

        [TestMethod]
        public void SkillHealCalculator_HeroCaster_UsesTotalMagicAndHealPowerBonus()
        {
            var hero = new Hero("TestPriest", new Priest(), 5, new StatBlock(10, 10, 10, 15));
            var skill = new HealSkill();

            var expected = BalanceConfig.CalculateSkillHealAmount(
                skill.HPRestoreAmount, hero.GetTotalStats().Magic, hero.HealPowerBonus);
            Assert.AreEqual(expected, SkillHealCalculator.GetAmount(skill, hero));

            hero.HealPowerBonus += 0.25f;
            var boosted = SkillHealCalculator.GetAmount(skill, hero);
            Assert.IsTrue(boosted > expected, "Heal power bonus must increase the heal amount");
        }

        [TestMethod]
        public void SkillHealCalculator_MercenaryCaster_UsesTotalMagicAndHealPowerBonus()
        {
            var merc = new Mercenary("TestPriest", new Priest(), 5, new StatBlock(10, 10, 10, 15));
            var skill = new HealSkill();

            var expected = BalanceConfig.CalculateSkillHealAmount(
                skill.HPRestoreAmount, merc.GetTotalStats().Magic, merc.HealPowerBonus);
            Assert.AreEqual(expected, SkillHealCalculator.GetAmount(skill, merc));
        }

        [TestMethod]
        public void SkillHealCalculator_ObjectDispatch_HandlesHeroMercAndUnknown()
        {
            var skill = new HealSkill();
            var hero = new Hero("TestHero", new Priest(), 5, new StatBlock(10, 10, 10, 15));
            var merc = new Mercenary("TestMerc", new Priest(), 5, new StatBlock(10, 10, 10, 15));

            Assert.AreEqual(SkillHealCalculator.GetAmount(skill, hero), SkillHealCalculator.GetAmount(skill, (object)hero));
            Assert.AreEqual(SkillHealCalculator.GetAmount(skill, merc), SkillHealCalculator.GetAmount(skill, (object)merc));

            // Unknown caster type falls back to the unscaled base amount
            Assert.AreEqual(skill.HPRestoreAmount, SkillHealCalculator.GetAmount(skill, new object()));
        }

        [TestMethod]
        public void HealSkill_BaseAmount_Is25()
        {
            // The skill's base was lowered from 50 to 25 when Magic scaling was added;
            // early-game effective healing stays ≈50 (25 + MAG×2 at MAG ~11-12)
            Assert.AreEqual(25, new HealSkill().HPRestoreAmount);
        }
    }
}
