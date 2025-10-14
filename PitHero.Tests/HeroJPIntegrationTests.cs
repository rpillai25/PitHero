using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class HeroJPIntegrationTests
    {
        [TestMethod]
        public void Hero_Can_Earn_JP_Through_Crystal()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            var hero = new Hero("Arthur", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            
            hero.EarnJP(100);
            
            Assert.AreEqual(100, hero.GetCurrentJP(), "Hero should have 100 JP");
            Assert.AreEqual(100, hero.GetTotalJP(), "Hero should have 100 total JP");
        }

        [TestMethod]
        public void Hero_Can_Purchase_Skills_With_JP()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            var hero = new Hero("Arthur", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            
            hero.EarnJP(100);
            
            var knight = new Knight();
            var lightArmorSkill = knight.Skills[0]; // Light Armor, JP cost 50, level 1
            
            bool success = hero.TryPurchaseSkill(lightArmorSkill);
            
            Assert.IsTrue(success, "Skill purchase should succeed");
            Assert.AreEqual(50, hero.GetCurrentJP(), "Hero should have 50 JP remaining");
            Assert.IsTrue(hero.LearnedSkills.ContainsKey(lightArmorSkill.Id), "Skill should be learned by hero");
        }

        [TestMethod]
        public void Hero_Without_Crystal_Cannot_Use_JP_System()
        {
            var knight = new Knight();
            var hero = new Hero("Arthur", knight, 1, new StatBlock(4, 2, 4, 1), null);
            
            hero.EarnJP(100); // Should do nothing
            
            Assert.AreEqual(0, hero.GetCurrentJP(), "Hero without crystal should have 0 JP");
            
            var lightArmorSkill = knight.Skills[0];
            bool success = hero.TryPurchaseSkill(lightArmorSkill);
            
            Assert.IsFalse(success, "Hero without crystal cannot purchase skills");
        }

        [TestMethod]
        public void Hero_GetJobLevel_Returns_Crystal_JobLevel()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 3, new StatBlock(4, 2, 4, 1));
            var hero = new Hero("Arthur", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            
            Assert.AreEqual(1, hero.GetJobLevel(), "Initial job level should be 1");
            
            hero.EarnJP(500);
            var knight = new Knight();
            
            hero.TryPurchaseSkill(knight.Skills[0]); // Level 1 skill
            Assert.AreEqual(1, hero.GetJobLevel(), "Job level should be 1 after level 1 skill");
            
            hero.TryPurchaseSkill(knight.Skills[1]); // Level 2 skill
            Assert.AreEqual(2, hero.GetJobLevel(), "Job level should be 2 after level 2 skill");
            
            hero.TryPurchaseSkill(knight.Skills[3]); // Level 3 skill
            Assert.AreEqual(3, hero.GetJobLevel(), "Job level should be 3 after level 3 skill");
        }

        [TestMethod]
        public void Hero_IsJobMastered_Reflects_Crystal_Status()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 3, new StatBlock(4, 2, 4, 1));
            var hero = new Hero("Arthur", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            
            Assert.IsFalse(hero.IsJobMastered(), "Job should not be mastered initially");
            
            hero.EarnJP(500);
            var knight = new Knight();
            
            foreach (var skill in knight.Skills)
            {
                hero.TryPurchaseSkill(skill);
            }
            
            Assert.IsTrue(hero.IsJobMastered(), "Job should be mastered when all skills are learned");
        }

        [TestMethod]
        public void Passive_Skills_Are_Applied_After_Purchase()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 2, new StatBlock(4, 2, 4, 1));
            var hero = new Hero("Arthur", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            
            Assert.AreEqual(0, hero.PassiveDefenseBonus, "Initial defense bonus should be 0");
            
            hero.EarnJP(200);
            var knight = new Knight();
            var heavyArmorSkill = knight.Skills[1]; // Heavy Armor passive, +2 defense
            
            hero.TryPurchaseSkill(heavyArmorSkill);
            
            Assert.AreEqual(2, hero.PassiveDefenseBonus, "Defense bonus should be 2 after purchasing Heavy Armor");
        }

        [TestMethod]
        public void Multiple_Heroes_Can_Share_Same_Crystal()
        {
            var crystal = new HeroCrystal("SharedCrystal", new Knight(), 2, new StatBlock(4, 2, 4, 1));
            
            var hero1 = new Hero("Arthur", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            hero1.EarnJP(100);
            
            var hero2 = new Hero("Lancelot", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            
            // Hero2 should see the JP earned by hero1
            Assert.AreEqual(100, hero2.GetCurrentJP(), "Hero2 should see JP from shared crystal");
        }

        [TestMethod]
        public void Skills_Learned_By_One_Hero_Available_To_Another_With_Same_Crystal()
        {
            var crystal = new HeroCrystal("SharedCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            
            var hero1 = new Hero("Arthur", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            hero1.EarnJP(100);
            
            var knight = new Knight();
            var lightArmorSkill = knight.Skills[0];
            hero1.TryPurchaseSkill(lightArmorSkill);
            
            // Create a new hero with the same crystal
            var hero2 = new Hero("Lancelot", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            
            Assert.IsTrue(hero2.LearnedSkills.ContainsKey(lightArmorSkill.Id), "Hero2 should have the skill learned by hero1");
        }

        [TestMethod]
        public void Thief_Hero_Can_Use_JP_System()
        {
            var crystal = new HeroCrystal("ThiefCrystal", new Thief(), 2, new StatBlock(2, 3, 1, 0));
            var hero = new Hero("Locke", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            
            hero.EarnJP(200);
            
            var thief = new Thief();
            var shadowstepSkill = thief.Skills[0]; // Shadowstep
            
            bool success = hero.TryPurchaseSkill(shadowstepSkill);
            
            Assert.IsTrue(success, "Thief should be able to purchase skills");
            Assert.AreEqual(130, hero.GetCurrentJP(), "Should have 130 JP after purchasing 70 JP skill");
        }

        [TestMethod]
        public void Bowman_Hero_Can_Use_JP_System()
        {
            var crystal = new HeroCrystal("BowmanCrystal", new Bowman(), 2, new StatBlock(2, 2, 2, 1));
            var hero = new Hero("Rosa", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            
            hero.EarnJP(200);
            
            var bowman = new Bowman();
            var eagleEyeSkill = bowman.Skills[0]; // Eagle Eye
            
            bool success = hero.TryPurchaseSkill(eagleEyeSkill);
            
            Assert.IsTrue(success, "Bowman should be able to purchase skills");
            Assert.AreEqual(130, hero.GetCurrentJP(), "Should have 130 JP after purchasing 70 JP skill");
        }

        [TestMethod]
        public void Purchasing_Skill_Updates_Both_Hero_And_Crystal()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            var hero = new Hero("Arthur", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            
            hero.EarnJP(100);
            
            var knight = new Knight();
            var lightArmorSkill = knight.Skills[0];
            
            hero.TryPurchaseSkill(lightArmorSkill);
            
            // Check both hero and crystal have the skill
            Assert.IsTrue(hero.LearnedSkills.ContainsKey(lightArmorSkill.Id), "Hero should have the skill");
            Assert.IsTrue(crystal.HasSkill(lightArmorSkill.Id), "Crystal should have the skill");
            
            // Check JP is deducted from both
            Assert.AreEqual(50, hero.GetCurrentJP(), "Hero should show 50 JP");
            Assert.AreEqual(50, crystal.CurrentJP, "Crystal should have 50 JP");
        }
    }
}
