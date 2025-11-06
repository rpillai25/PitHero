using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace PitHero.Tests
{
    [TestClass]
    public class CompleteJPWorkflowTests
    {
        /// <summary>Simulates a complete JP workflow: create hero, earn JP from battles, purchase skills.</summary>
        [TestMethod]
        public void Complete_Workflow_Knight_Earns_JP_And_Masters_Job()
        {
            // Create a crystal for a Knight
            var crystal = new HeroCrystal("KnightCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            var hero = new Hero("Arthur", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            
            // Initial state
            Assert.AreEqual(1, hero.GetJobLevel(), "Initial job level should be 1");
            Assert.IsFalse(hero.IsJobMastered(), "Job should not be mastered");
            
            // Earn JP from battles (simulate 5 battles at 100 JP each)
            for (int i = 0; i < 5; i++)
            {
                hero.EarnJP(100);
            }
            Assert.AreEqual(500, hero.GetCurrentJP(), "Should have 500 JP");
            
            // Purchase all Knight skills in order
            var knight = new Knight();
            
            // Purchase Light Armor (50 JP, Level 1)
            bool success1 = hero.TryPurchaseSkill(knight.Skills[0]);
            Assert.IsTrue(success1, "Should purchase Light Armor");
            Assert.AreEqual(450, hero.GetCurrentJP(), "Should have 450 JP after first purchase");
            Assert.AreEqual(1, hero.GetJobLevel(), "Job level should still be 1");
            
            // Purchase Heavy Armor (100 JP, Level 2) - requires hero to be level 2
            // First level up the hero
            hero.AddExperience(100);
            
            bool success2 = hero.TryPurchaseSkill(knight.Skills[1]);
            Assert.IsTrue(success2, "Should purchase Heavy Armor");
            Assert.AreEqual(350, hero.GetCurrentJP(), "Should have 350 JP");
            Assert.AreEqual(2, hero.GetJobLevel(), "Job level should be 2");
            
            // Check passive is applied
            Assert.AreEqual(2, hero.PassiveDefenseBonus, "Heavy Armor passive should be active");
            
            // Purchase Spin Slash (120 JP, Level 2)
            bool success3 = hero.TryPurchaseSkill(knight.Skills[2]);
            Assert.IsTrue(success3, "Should purchase Spin Slash");
            Assert.AreEqual(230, hero.GetCurrentJP(), "Should have 230 JP");
            Assert.AreEqual(2, hero.GetJobLevel(), "Job level should still be 2");
            
            // Level up to 3
            hero.AddExperience(100);
            
            // Purchase Heavy Strike (180 JP, Level 3)
            bool success4 = hero.TryPurchaseSkill(knight.Skills[3]);
            Assert.IsTrue(success4, "Should purchase Heavy Strike");
            Assert.AreEqual(50, hero.GetCurrentJP(), "Should have 50 JP remaining");
            Assert.AreEqual(3, hero.GetJobLevel(), "Job level should be 3");
            
            // Job should now be mastered
            Assert.IsTrue(hero.IsJobMastered(), "Knight job should be mastered");
            
            // Verify all skills are learned
            Assert.AreEqual(4, hero.LearnedSkills.Count, "Should have 4 skills learned");
        }

        /// <summary>Tests that heroes can progress through multiple jobs using combined crystals.</summary>
        [TestMethod]
        public void Combined_Crystals_Support_Multi_Job_Progression()
        {
            // Create a Knight crystal and master it
            var knightCrystal = new HeroCrystal("KnightCore", new Knight(), 3, new StatBlock(4, 2, 4, 1));
            var knight = new Hero("Arthur", knightCrystal.Job, knightCrystal.Level, knightCrystal.BaseStats, knightCrystal);
            knight.EarnJP(500);
            
            var knightJob = new Knight();
            foreach (var skill in knightJob.Skills)
            {
                knight.TryPurchaseSkill(skill);
            }
            
            Assert.IsTrue(knight.IsJobMastered(), "Knight should be mastered");
            
            // Create a Mage crystal and master it
            var mageCrystal = new HeroCrystal("MageCore", new Mage(), 3, new StatBlock(1, 2, 2, 5));
            var mage = new Hero("Vivi", mageCrystal.Job, mageCrystal.Level, mageCrystal.BaseStats, mageCrystal);
            mage.EarnJP(500);
            
            var mageJob = new Mage();
            foreach (var skill in mageJob.Skills)
            {
                mage.TryPurchaseSkill(skill);
            }
            
            Assert.IsTrue(mage.IsJobMastered(), "Mage should be mastered");
            
            // Combine the two crystals
            var combinedCrystal = HeroCrystal.Combine("PaladinCore", knightCrystal, mageCrystal);
            
            // Create a hero from the combined crystal
            var paladin = new Hero("Cecil", combinedCrystal.Job, combinedCrystal.Level, combinedCrystal.BaseStats, combinedCrystal);
            
            // Should have skills from both jobs
            Assert.IsTrue(paladin.LearnedSkills.ContainsKey("knight.light_armor"), "Should have Knight skills");
            Assert.IsTrue(paladin.LearnedSkills.ContainsKey("mage.fire"), "Should have Mage skills");
            
            // Should have combined JP
            int expectedJP = (500 - 50 - 100 - 120 - 180) + (500 - 60 - 80 - 120 - 200); // remaining JP from both
            Assert.AreEqual(expectedJP, paladin.GetCurrentJP(), "Should have combined remaining JP");
        }

        /// <summary>Tests the new Thief job progression.</summary>
        [TestMethod]
        public void Thief_Job_Complete_Progression()
        {
            var crystal = new HeroCrystal("ThiefCore", new Thief(), 3, new StatBlock(2, 3, 1, 0));
            var hero = new Hero("Locke", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            
            // Earn sufficient JP
            hero.EarnJP(500);
            
            var thief = new Thief();
            
            // Verify skill order and costs
            Assert.AreEqual("thief.shadowstep", thief.Skills[0].Id);
            Assert.AreEqual(70, thief.Skills[0].JPCost);
            
            Assert.AreEqual("thief.trap_sense", thief.Skills[1].Id);
            Assert.AreEqual(90, thief.Skills[1].JPCost);
            
            Assert.AreEqual("thief.sneak_attack", thief.Skills[2].Id);
            Assert.AreEqual(130, thief.Skills[2].JPCost);
            
            Assert.AreEqual("thief.vanish", thief.Skills[3].Id);
            Assert.AreEqual(180, thief.Skills[3].JPCost);
            
            // Purchase all skills
            foreach (var skill in thief.Skills)
            {
                bool success = hero.TryPurchaseSkill(skill);
                Assert.IsTrue(success, $"Should purchase {skill.Name}");
            }
            
            Assert.IsTrue(hero.IsJobMastered(), "Thief should be mastered");
            Assert.AreEqual(30, hero.GetCurrentJP(), "Should have 30 JP remaining (500 - 470)");
        }

        /// <summary>Tests the new Bowman job progression.</summary>
        [TestMethod]
        public void Bowman_Job_Complete_Progression()
        {
            var crystal = new HeroCrystal("BowmanCore", new Bowman(), 3, new StatBlock(2, 2, 2, 1));
            var hero = new Hero("Rosa", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            
            // Earn sufficient JP
            hero.EarnJP(550);
            
            var bowman = new Bowman();
            
            // Verify skill order and costs
            Assert.AreEqual("bowman.eagle_eye", bowman.Skills[0].Id);
            Assert.AreEqual(70, bowman.Skills[0].JPCost);
            
            Assert.AreEqual("bowman.quickdraw", bowman.Skills[1].Id);
            Assert.AreEqual(100, bowman.Skills[1].JPCost);
            
            Assert.AreEqual("bowman.power_shot", bowman.Skills[2].Id);
            Assert.AreEqual(130, bowman.Skills[2].JPCost);
            
            Assert.AreEqual("bowman.volley", bowman.Skills[3].Id);
            Assert.AreEqual(200, bowman.Skills[3].JPCost);
            
            // Purchase all skills
            foreach (var skill in bowman.Skills)
            {
                bool success = hero.TryPurchaseSkill(skill);
                Assert.IsTrue(success, $"Should purchase {skill.Name}");
            }
            
            Assert.IsTrue(hero.IsJobMastered(), "Bowman should be mastered");
            Assert.AreEqual(50, hero.GetCurrentJP(), "Should have 50 JP remaining (550 - 500)");
        }

        /// <summary>Verifies that all 6 primary jobs exist and have 4 skills each.</summary>
        [TestMethod]
        public void All_Six_Primary_Jobs_Exist_With_Four_Skills()
        {
            var jobs = new IJob[]
            {
                new Knight(),
                new Mage(),
                new Priest(),
                new Monk(),
                new Thief(),
                new Bowman()
            };
            
            foreach (var job in jobs)
            {
                Assert.AreEqual(4, job.Skills.Count, $"{job.Name} should have exactly 4 skills");
                
                // Verify each skill has a JP cost
                foreach (var skill in job.Skills)
                {
                    Assert.IsTrue(skill.JPCost > 0, $"{skill.Name} should have a JP cost > 0");
                }
            }
        }
    }
}
