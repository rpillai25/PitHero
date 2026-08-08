using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class HeroCrystalBuyBackPriceTests
    {
        private static HeroCrystal CreateCrystal(IJob job, int level)
        {
            var baseStats = new StatBlock(strength: 5, agility: 3, vitality: 5, magic: 1);
            return new HeroCrystal("Test Crystal", job, level, baseStats);
        }

        [TestMethod]
        public void BuyBackPrice_NoSkills_IsFlatBasePricePerLevel()
        {
            var crystal = CreateCrystal(new Knight(), 1);

            Assert.AreEqual(GameConfig.CrystalBuyBackBasePrice, crystal.CalculateBuyBackPrice());
        }

        [TestMethod]
        public void BuyBackPrice_NoSkills_ScalesWithLevel()
        {
            var crystal = CreateCrystal(new Mage(), 10);

            Assert.AreEqual(10 * GameConfig.CrystalBuyBackBasePrice, crystal.CalculateBuyBackPrice());
        }

        [TestMethod]
        public void BuyBackPrice_LearnedJobSkill_AddsJPAnchoredPremium()
        {
            var knight = new Knight();
            // High level so the cap does not engage
            var crystal = CreateCrystal(knight, 50);
            var skill = knight.Skills[0];
            crystal.AddLearnedSkill(skill.Id);

            int basePrice = 50 * GameConfig.CrystalBuyBackBasePrice;
            int expectedPremium = (int)(skill.JPCost * GameConfig.CrystalJPToGoldRate);
            Assert.AreEqual(basePrice + expectedPremium, crystal.CalculateBuyBackPrice());
        }

        [TestMethod]
        public void BuyBackPrice_LearnedSynergySkill_AddsFlatFee()
        {
            var crystal = CreateCrystal(new Priest(), 50);
            crystal.LearnSynergySkill("synergy.test_skill");

            int basePrice = 50 * GameConfig.CrystalBuyBackBasePrice;
            Assert.AreEqual(basePrice + GameConfig.CrystalSynergySkillFee, crystal.CalculateBuyBackPrice());
        }

        [TestMethod]
        public void BuyBackPrice_PremiumIsCappedAtBasePrice()
        {
            var knight = new Knight();
            // Level 1: base 100g, so a mastered crystal must cost exactly double the base
            var crystal = CreateCrystal(knight, 1);
            for (int i = 0; i < knight.Skills.Count; i++)
                crystal.AddLearnedSkill(knight.Skills[i].Id);
            crystal.LearnSynergySkill("synergy.test_skill");

            int totalJP = 0;
            for (int i = 0; i < knight.Skills.Count; i++)
                totalJP += knight.Skills[i].JPCost;
            int uncappedPremium = (int)(totalJP * GameConfig.CrystalJPToGoldRate) + GameConfig.CrystalSynergySkillFee;
            Assert.IsTrue(uncappedPremium > GameConfig.CrystalBuyBackBasePrice,
                "Test requires the uncapped premium to exceed the level-1 base price");

            Assert.AreEqual(2 * GameConfig.CrystalBuyBackBasePrice, crystal.CalculateBuyBackPrice());
        }

        [TestMethod]
        public void BuyBackPrice_SkillIdNotInJob_AddsNoPremium()
        {
            var crystal = CreateCrystal(new Knight(), 5);
            crystal.AddLearnedSkill("mage.fire");

            Assert.AreEqual(5 * GameConfig.CrystalBuyBackBasePrice, crystal.CalculateBuyBackPrice());
        }

        [TestMethod]
        public void BuyBackPrice_CompositeJob_CountsSkillsFromBothJobs()
        {
            var knight = new Knight();
            var mage = new Mage();
            var composite = new CompositeJob(knight, mage);
            var crystal = CreateCrystal(composite, 50);
            var knightSkill = knight.Skills[0];
            var mageSkill = mage.Skills[0];
            crystal.AddLearnedSkill(knightSkill.Id);
            crystal.AddLearnedSkill(mageSkill.Id);

            int basePrice = 50 * GameConfig.CrystalBuyBackBasePrice;
            int expectedPremium = (int)((knightSkill.JPCost + mageSkill.JPCost) * GameConfig.CrystalJPToGoldRate);
            Assert.AreEqual(basePrice + expectedPremium, crystal.CalculateBuyBackPrice());
        }
    }
}
