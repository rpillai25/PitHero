using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>Fee math for crystal creation and forging (issue #379).</summary>
    [TestClass]
    public class CrystalFeeCalculatorTests
    {
        private static HeroCrystal CreateCrystal(IJob job, int level)
        {
            var baseStats = new StatBlock(strength: 5, agility: 3, vitality: 5, magic: 1);
            return new HeroCrystal("Test Crystal", job, level, baseStats);
        }

        [TestMethod]
        public void CreationFee_Is100Gold()
        {
            Assert.AreEqual(100, GameConfig.CrystalCreationFee);
        }

        [TestMethod]
        public void ForgeFee_IsDoubleUncappedBuyBackPrice_LevelOnePair()
        {
            var a = CreateCrystal(new Knight(), 1);
            var b = CreateCrystal(new Mage(), 1);

            var expected = HeroCrystal.Combine("Combo Crystal", a, b).CalculateBuyBackPrice(capPremium: false) * 2;
            Assert.AreEqual(expected, CrystalFeeCalculator.GetForgeFee(a, b));
            Assert.IsTrue(expected > 0, "Forge fee for a valid pair must be positive");
        }

        [TestMethod]
        public void ForgeFee_IsDoubleUncappedBuyBackPrice_MasteredWithSkills()
        {
            var knight = new Knight();
            var mage = new Mage();
            var a = CreateCrystal(knight, 10);
            var b = CreateCrystal(mage, 20);
            for (int i = 0; i < knight.Skills.Count; i++)
                a.AddLearnedSkill(knight.Skills[i].Id);
            for (int i = 0; i < mage.Skills.Count; i++)
                b.AddLearnedSkill(mage.Skills[i].Id);
            b.LearnSynergySkill("synergy.test_skill");

            var expected = HeroCrystal.Combine("Combo Crystal", a, b).CalculateBuyBackPrice(capPremium: false) * 2;
            Assert.AreEqual(expected, CrystalFeeCalculator.GetForgeFee(a, b));
        }

        [TestMethod]
        public void ForgeFee_SkillPremiumIsUncapped_ScalesBeyondShopCap()
        {
            // Regression for the 100G Legend forge: at level 1 the shop's premium cap makes a
            // mastered 12+-skill combo price identical to a barely-skilled one. The forge fee
            // must ignore the cap so skill count always raises the price.
            var knight = new Knight();
            var mage = new Mage();
            var a = CreateCrystal(knight, 1);
            var b = CreateCrystal(mage, 1);
            for (int i = 0; i < knight.Skills.Count; i++)
                a.AddLearnedSkill(knight.Skills[i].Id);
            for (int i = 0; i < mage.Skills.Count; i++)
                b.AddLearnedSkill(mage.Skills[i].Id);

            int cappedShopPrice = HeroCrystal.Combine("Combo Crystal", a, b).CalculateBuyBackPrice();
            int fee = CrystalFeeCalculator.GetForgeFee(a, b);

            Assert.IsTrue(fee > cappedShopPrice * 2,
                "A fully mastered level-1 combo must forge for more than double the capped shop price");

            // And more skills must always mean a higher fee than fewer skills at the same level
            var c = CreateCrystal(new Knight(), 1);
            var d = CreateCrystal(new Mage(), 1);
            c.AddLearnedSkill(knight.Skills[0].Id);

            Assert.IsTrue(fee > CrystalFeeCalculator.GetForgeFee(c, d),
                "Skill count must raise the forge fee at any crystal level");
        }

        [TestMethod]
        public void ForgeFee_DoesNotMutateSourceCrystals()
        {
            var a = CreateCrystal(new Knight(), 5);
            var b = CreateCrystal(new Mage(), 7);
            int priceABefore = a.CalculateBuyBackPrice();
            int priceBBefore = b.CalculateBuyBackPrice();

            CrystalFeeCalculator.GetForgeFee(a, b);

            Assert.AreEqual(priceABefore, a.CalculateBuyBackPrice());
            Assert.AreEqual(priceBBefore, b.CalculateBuyBackPrice());
            Assert.AreEqual(5, a.Level);
            Assert.AreEqual(7, b.Level);
        }

        [TestMethod]
        public void ForgeFee_NullSource_ReturnsZero()
        {
            var a = CreateCrystal(new Knight(), 1);
            Assert.AreEqual(0, CrystalFeeCalculator.GetForgeFee(a, null));
            Assert.AreEqual(0, CrystalFeeCalculator.GetForgeFee(null, a));
            Assert.AreEqual(0, CrystalFeeCalculator.GetForgeFee(null, null));
        }
    }
}
