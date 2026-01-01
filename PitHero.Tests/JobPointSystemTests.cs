using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class JobPointSystemTests
    {
        [TestMethod]
        public void HeroCrystal_Starts_With_Zero_JP()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            
            Assert.AreEqual(0, crystal.TotalJP, "TotalJP should start at 0");
            Assert.AreEqual(0, crystal.CurrentJP, "CurrentJP should start at 0");
        }

        [TestMethod]
        public void EarnJP_Increases_Total_And_Current_JP()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            
            crystal.EarnJP(100);
            
            Assert.AreEqual(100, crystal.TotalJP, "TotalJP should be 100");
            Assert.AreEqual(100, crystal.CurrentJP, "CurrentJP should be 100");
        }

        [TestMethod]
        public void EarnJP_Multiple_Times_Accumulates()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            
            crystal.EarnJP(50);
            crystal.EarnJP(75);
            crystal.EarnJP(25);
            
            Assert.AreEqual(150, crystal.TotalJP, "TotalJP should accumulate to 150");
            Assert.AreEqual(150, crystal.CurrentJP, "CurrentJP should accumulate to 150");
        }

        [TestMethod]
        public void EarnJP_Negative_Amount_Is_Ignored()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            
            crystal.EarnJP(100);
            crystal.EarnJP(-50);
            
            Assert.AreEqual(100, crystal.TotalJP, "Negative JP should be ignored");
            Assert.AreEqual(100, crystal.CurrentJP, "Negative JP should be ignored");
        }

        [TestMethod]
        public void TryPurchaseSkill_Success_Reduces_Current_JP()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            crystal.EarnJP(100);
            
            var knight = new Knight();
            var lightArmorSkill = knight.Skills[0]; // Light Armor, JP cost 50, level 1
            
            bool success = crystal.TryPurchaseSkill(lightArmorSkill);
            
            Assert.IsTrue(success, "Purchase should succeed");
            Assert.AreEqual(100, crystal.TotalJP, "TotalJP should remain 100");
            Assert.AreEqual(50, crystal.CurrentJP, "CurrentJP should be reduced to 50");
            Assert.IsTrue(crystal.HasSkill(lightArmorSkill.Id), "Skill should be learned");
        }

        [TestMethod]
        public void TryPurchaseSkill_Fails_If_Insufficient_JP()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            crystal.EarnJP(30);
            
            var knight = new Knight();
            var lightArmorSkill = knight.Skills[0]; // Light Armor, JP cost 50, level 1
            
            bool success = crystal.TryPurchaseSkill(lightArmorSkill);
            
            Assert.IsFalse(success, "Purchase should fail due to insufficient JP");
            Assert.AreEqual(30, crystal.CurrentJP, "CurrentJP should remain unchanged");
            Assert.IsFalse(crystal.HasSkill(lightArmorSkill.Id), "Skill should not be learned");
        }

        [TestMethod]
        public void TryPurchaseSkill_Fails_If_Already_Learned()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            crystal.EarnJP(200);
            
            var knight = new Knight();
            var lightArmorSkill = knight.Skills[0]; // Light Armor, JP cost 50, level 1
            
            crystal.TryPurchaseSkill(lightArmorSkill);
            bool secondAttempt = crystal.TryPurchaseSkill(lightArmorSkill);
            
            Assert.IsFalse(secondAttempt, "Cannot purchase the same skill twice");
            Assert.AreEqual(150, crystal.CurrentJP, "CurrentJP should only be deducted once");
        }

        [TestMethod]
        public void JobLevel_Starts_At_0()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            
            Assert.AreEqual(0, crystal.JobLevel, "JobLevel should start at 0");
        }

        [TestMethod]
        public void JobLevel_Increases_With_Purchased_Skills()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 3, new StatBlock(4, 2, 4, 1));
            crystal.EarnJP(500);
            
            var knight = new Knight();
            
            // Purchase level 1 skill
            crystal.TryPurchaseSkill(knight.Skills[0]); // Light Armor
            Assert.AreEqual(1, crystal.JobLevel, "JobLevel should be 1 after level 1 skill");
            
            // Purchase level 2 skill
            crystal.TryPurchaseSkill(knight.Skills[1]); // Heavy Armor
            Assert.AreEqual(2, crystal.JobLevel, "JobLevel should be 2 after level 2 skill");
            
            // Purchase level 3 skill
            crystal.TryPurchaseSkill(knight.Skills[3]); // Heavy Strike
            Assert.AreEqual(3, crystal.JobLevel, "JobLevel should be 3 after level 3 skill");
        }

        [TestMethod]
        public void IsJobMastered_False_Initially()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 3, new StatBlock(4, 2, 4, 1));
            
            Assert.IsFalse(crystal.IsJobMastered(), "Job should not be mastered initially");
        }

        [TestMethod]
        public void IsJobMastered_True_When_All_Skills_Learned()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 3, new StatBlock(4, 2, 4, 1));
            crystal.EarnJP(500);
            
            var knight = new Knight();
            
            // Purchase all skills
            foreach (var skill in knight.Skills)
            {
                crystal.TryPurchaseSkill(skill);
            }
            
            Assert.IsTrue(crystal.IsJobMastered(), "Job should be mastered when all skills are learned");
        }

        [TestMethod]
        public void Knight_Skills_Have_Correct_JP_Costs()
        {
            var knight = new Knight();
            
            Assert.AreEqual(50, knight.Skills[0].JPCost, "Light Armor should cost 50 JP");
            Assert.AreEqual(100, knight.Skills[1].JPCost, "Heavy Armor should cost 100 JP");
            Assert.AreEqual(120, knight.Skills[2].JPCost, "Spin Slash should cost 120 JP");
            Assert.AreEqual(180, knight.Skills[3].JPCost, "Heavy Strike should cost 180 JP");
        }

        [TestMethod]
        public void Mage_Skills_Have_Correct_JP_Costs()
        {
            var mage = new Mage();
            
            Assert.AreEqual(60, mage.Skills[0].JPCost, "Heart of Fire should cost 60 JP");
            Assert.AreEqual(80, mage.Skills[1].JPCost, "Economist should cost 80 JP");
            Assert.AreEqual(120, mage.Skills[2].JPCost, "Fire should cost 120 JP");
            Assert.AreEqual(200, mage.Skills[3].JPCost, "Firestorm should cost 200 JP");
        }

        [TestMethod]
        public void Priest_Skills_Have_Correct_JP_Costs()
        {
            var priest = new Priest();
            
            Assert.AreEqual(50, priest.Skills[0].JPCost, "Calm Spirit should cost 50 JP");
            Assert.AreEqual(80, priest.Skills[1].JPCost, "Mender should cost 80 JP");
            Assert.AreEqual(100, priest.Skills[2].JPCost, "Heal should cost 100 JP");
            Assert.AreEqual(160, priest.Skills[3].JPCost, "Defense Up should cost 160 JP");
        }

        [TestMethod]
        public void Monk_Skills_Have_Correct_JP_Costs()
        {
            var monk = new Monk();
            
            Assert.AreEqual(70, monk.Skills[0].JPCost, "Counter should cost 70 JP");
            Assert.AreEqual(90, monk.Skills[1].JPCost, "Deflect should cost 90 JP");
            Assert.AreEqual(120, monk.Skills[2].JPCost, "Roundhouse should cost 120 JP");
            Assert.AreEqual(170, monk.Skills[3].JPCost, "Flaming Fist should cost 170 JP");
        }

        [TestMethod]
        public void Thief_Skills_Have_Correct_JP_Costs()
        {
            var thief = new Thief();
            
            Assert.AreEqual(70, thief.Skills[0].JPCost, "Shadowstep should cost 70 JP");
            Assert.AreEqual(90, thief.Skills[1].JPCost, "Trap Sense should cost 90 JP");
            Assert.AreEqual(130, thief.Skills[2].JPCost, "Sneak Attack should cost 130 JP");
            Assert.AreEqual(180, thief.Skills[3].JPCost, "Vanish should cost 180 JP");
        }

        [TestMethod]
        public void Bowman_Skills_Have_Correct_JP_Costs()
        {
            var bowman = new Bowman();
            
            Assert.AreEqual(70, bowman.Skills[0].JPCost, "Eagle Eye should cost 70 JP");
            Assert.AreEqual(100, bowman.Skills[1].JPCost, "Quickdraw should cost 100 JP");
            Assert.AreEqual(130, bowman.Skills[2].JPCost, "Power Shot should cost 130 JP");
            Assert.AreEqual(200, bowman.Skills[3].JPCost, "Volley should cost 200 JP");
        }

        [TestMethod]
        public void Combine_Crystals_Adds_JP()
        {
            var crystal1 = new HeroCrystal("Crystal1", new Knight(), 2, new StatBlock(4, 2, 4, 1));
            crystal1.EarnJP(150);
            
            var crystal2 = new HeroCrystal("Crystal2", new Mage(), 2, new StatBlock(1, 2, 2, 5));
            crystal2.EarnJP(200);
            
            var combined = HeroCrystal.Combine("Combined", crystal1, crystal2);
            
            Assert.AreEqual(350, combined.TotalJP, "Combined TotalJP should be sum of both crystals");
            Assert.AreEqual(350, combined.CurrentJP, "Combined CurrentJP should be sum of both crystals");
        }
    }
}
