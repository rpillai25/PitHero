using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Jobs.Secondary;
using RolePlayingFramework.Jobs.Tertiary;
using RolePlayingFramework.Stats;
using System.Linq;

namespace PitHero.Tests
{
    [TestClass]
    public class SecondaryJobSystemTests
    {
        #region Secondary Job Instantiation Tests
        
        [TestMethod]
        public void Paladin_Job_Can_Be_Created()
        {
            var job = new Paladin();
            Assert.AreEqual("Paladin", job.Name);
            Assert.AreEqual(4, job.BaseBonus.Strength);
            Assert.AreEqual(1, job.BaseBonus.Agility);
            Assert.AreEqual(3, job.BaseBonus.Vitality);
            Assert.AreEqual(2, job.BaseBonus.Magic);
            Assert.AreEqual(4, job.Skills.Count);
        }

        [TestMethod]
        public void WarMage_Job_Can_Be_Created()
        {
            var job = new WarMage();
            Assert.AreEqual("War Mage", job.Name);
            Assert.AreEqual(3, job.BaseBonus.Strength);
            Assert.AreEqual(1, job.BaseBonus.Agility);
            Assert.AreEqual(2, job.BaseBonus.Vitality);
            Assert.AreEqual(3, job.BaseBonus.Magic);
            Assert.AreEqual(4, job.Skills.Count);
        }

        [TestMethod]
        public void Samurai_Job_Can_Be_Created()
        {
            var job = new Samurai();
            Assert.AreEqual("Samurai", job.Name);
            Assert.AreEqual(4, job.BaseBonus.Strength);
            Assert.AreEqual(2, job.BaseBonus.Agility);
            Assert.AreEqual(3, job.BaseBonus.Vitality);
            Assert.AreEqual(1, job.BaseBonus.Magic);
            Assert.AreEqual(4, job.Skills.Count);
        }

        [TestMethod]
        public void Ninja_Job_Can_Be_Created()
        {
            var job = new Ninja();
            Assert.AreEqual("Ninja", job.Name);
            Assert.AreEqual(3, job.BaseBonus.Strength);
            Assert.AreEqual(3, job.BaseBonus.Agility);
            Assert.AreEqual(2, job.BaseBonus.Vitality);
            Assert.AreEqual(1, job.BaseBonus.Magic);
            Assert.AreEqual(4, job.Skills.Count);
        }

        [TestMethod]
        public void Marksman_Job_Can_Be_Created()
        {
            var job = new Marksman();
            Assert.AreEqual("Marksman", job.Name);
            Assert.AreEqual(3, job.BaseBonus.Strength);
            Assert.AreEqual(2, job.BaseBonus.Agility);
            Assert.AreEqual(2, job.BaseBonus.Vitality);
            Assert.AreEqual(2, job.BaseBonus.Magic);
            Assert.AreEqual(4, job.Skills.Count);
        }

        [TestMethod]
        public void Wizard_Job_Can_Be_Created()
        {
            var job = new Wizard();
            Assert.AreEqual("Wizard", job.Name);
            Assert.AreEqual(1, job.BaseBonus.Strength);
            Assert.AreEqual(1, job.BaseBonus.Agility);
            Assert.AreEqual(1, job.BaseBonus.Vitality);
            Assert.AreEqual(6, job.BaseBonus.Magic);
            Assert.AreEqual(4, job.Skills.Count);
        }

        [TestMethod]
        public void DragonFist_Job_Can_Be_Created()
        {
            var job = new DragonFist();
            Assert.AreEqual("Dragon Fist", job.Name);
            Assert.AreEqual(3, job.BaseBonus.Strength);
            Assert.AreEqual(2, job.BaseBonus.Agility);
            Assert.AreEqual(2, job.BaseBonus.Vitality);
            Assert.AreEqual(3, job.BaseBonus.Magic);
            Assert.AreEqual(4, job.Skills.Count);
        }

        [TestMethod]
        public void Spellcloak_Job_Can_Be_Created()
        {
            var job = new Spellcloak();
            Assert.AreEqual("Spellcloak", job.Name);
            Assert.AreEqual(2, job.BaseBonus.Strength);
            Assert.AreEqual(3, job.BaseBonus.Agility);
            Assert.AreEqual(1, job.BaseBonus.Vitality);
            Assert.AreEqual(3, job.BaseBonus.Magic);
            Assert.AreEqual(4, job.Skills.Count);
        }

        [TestMethod]
        public void ArcaneArcher_Job_Can_Be_Created()
        {
            var job = new ArcaneArcher();
            Assert.AreEqual("Arcane Archer", job.Name);
            Assert.AreEqual(2, job.BaseBonus.Strength);
            Assert.AreEqual(2, job.BaseBonus.Agility);
            Assert.AreEqual(2, job.BaseBonus.Vitality);
            Assert.AreEqual(4, job.BaseBonus.Magic);
            Assert.AreEqual(4, job.Skills.Count);
        }

        [TestMethod]
        public void DivineFist_Job_Can_Be_Created()
        {
            var job = new DivineFist();
            Assert.AreEqual("Divine Fist", job.Name);
            Assert.AreEqual(2, job.BaseBonus.Strength);
            Assert.AreEqual(2, job.BaseBonus.Agility);
            Assert.AreEqual(2, job.BaseBonus.Vitality);
            Assert.AreEqual(3, job.BaseBonus.Magic);
            Assert.AreEqual(4, job.Skills.Count);
        }

        [TestMethod]
        public void Shadowmender_Job_Can_Be_Created()
        {
            var job = new Shadowmender();
            Assert.AreEqual("Shadowmender", job.Name);
            Assert.AreEqual(1, job.BaseBonus.Strength);
            Assert.AreEqual(3, job.BaseBonus.Agility);
            Assert.AreEqual(1, job.BaseBonus.Vitality);
            Assert.AreEqual(3, job.BaseBonus.Magic);
            Assert.AreEqual(4, job.Skills.Count);
        }

        [TestMethod]
        public void HolyArcher_Job_Can_Be_Created()
        {
            var job = new HolyArcher();
            Assert.AreEqual("Holy Archer", job.Name);
            Assert.AreEqual(1, job.BaseBonus.Strength);
            Assert.AreEqual(2, job.BaseBonus.Agility);
            Assert.AreEqual(2, job.BaseBonus.Vitality);
            Assert.AreEqual(3, job.BaseBonus.Magic);
            Assert.AreEqual(4, job.Skills.Count);
        }

        [TestMethod]
        public void ShadowFist_Job_Can_Be_Created()
        {
            var job = new ShadowFist();
            Assert.AreEqual("Shadow Fist", job.Name);
            Assert.AreEqual(2, job.BaseBonus.Strength);
            Assert.AreEqual(4, job.BaseBonus.Agility);
            Assert.AreEqual(2, job.BaseBonus.Vitality);
            Assert.AreEqual(1, job.BaseBonus.Magic);
            Assert.AreEqual(4, job.Skills.Count);
        }

        [TestMethod]
        public void KiShot_Job_Can_Be_Created()
        {
            var job = new KiShot();
            Assert.AreEqual("Ki Shot", job.Name);
            Assert.AreEqual(2, job.BaseBonus.Strength);
            Assert.AreEqual(3, job.BaseBonus.Agility);
            Assert.AreEqual(2, job.BaseBonus.Vitality);
            Assert.AreEqual(2, job.BaseBonus.Magic);
            Assert.AreEqual(4, job.Skills.Count);
        }

        [TestMethod]
        public void Stalker_Job_Can_Be_Created()
        {
            var job = new Stalker();
            Assert.AreEqual("Stalker", job.Name);
            Assert.AreEqual(2, job.BaseBonus.Strength);
            Assert.AreEqual(3, job.BaseBonus.Agility);
            Assert.AreEqual(1, job.BaseBonus.Vitality);
            Assert.AreEqual(2, job.BaseBonus.Magic);
            Assert.AreEqual(4, job.Skills.Count);
        }

        #endregion

        #region JP Cost Verification Tests

        [TestMethod]
        public void Paladin_Skills_Have_Correct_JP_Costs()
        {
            var job = new Paladin();
            Assert.AreEqual(120, job.Skills[0].JPCost); // Knight's Honor
            Assert.AreEqual(160, job.Skills[1].JPCost); // Divine Shield
            Assert.AreEqual(200, job.Skills[2].JPCost); // Holy Strike
            Assert.AreEqual(220, job.Skills[3].JPCost); // Aura Heal
        }

        [TestMethod]
        public void WarMage_Skills_Have_Correct_JP_Costs()
        {
            var job = new WarMage();
            Assert.AreEqual(100, job.Skills[0].JPCost); // Focused Mind
            Assert.AreEqual(140, job.Skills[1].JPCost); // Arcane Defense
            Assert.AreEqual(180, job.Skills[2].JPCost); // Spellblade
            Assert.AreEqual(220, job.Skills[3].JPCost); // Blitz
        }

        [TestMethod]
        public void Wizard_Skills_Have_Correct_JP_Costs()
        {
            var job = new Wizard();
            Assert.AreEqual(120, job.Skills[0].JPCost); // Mana Spring
            Assert.AreEqual(160, job.Skills[1].JPCost); // Blessing
            Assert.AreEqual(200, job.Skills[2].JPCost); // Meteor
            Assert.AreEqual(220, job.Skills[3].JPCost); // Purify
        }

        [TestMethod]
        public void All_Secondary_Jobs_Have_Four_Skills()
        {
            var jobs = new IJob[]
            {
                new Paladin(), new WarMage(), new Samurai(), new Ninja(), new Marksman(),
                new Wizard(), new DragonFist(), new Spellcloak(), new ArcaneArcher(),
                new DivineFist(), new Shadowmender(), new HolyArcher(), new ShadowFist(),
                new KiShot(), new Stalker()
            };

            foreach (var job in jobs)
            {
                Assert.AreEqual(4, job.Skills.Count, $"{job.Name} should have exactly 4 skills");
            }
        }

        #endregion

        #region Crystal Combination Tests

        [TestMethod]
        public void Knight_And_Priest_Can_Be_Combined_In_Crystal()
        {
            var knightCrystal = new HeroCrystal("KnightCore", new Knight(), 5, new StatBlock(4, 2, 4, 1));
            var priestCrystal = new HeroCrystal("PriestCore", new Priest(), 5, new StatBlock(2, 2, 3, 4));

            var combined = HeroCrystal.Combine("PaladinCore", knightCrystal, priestCrystal);

            Assert.IsNotNull(combined);
            Assert.AreEqual("PaladinCore", combined.Name);
            // CompositeJob will have skills from both parent jobs
            Assert.IsTrue(combined.Job.Skills.Count >= 4, "Combined job should have skills from both parents");
        }

        [TestMethod]
        public void Mage_And_Priest_Can_Be_Combined_In_Crystal()
        {
            var mageCrystal = new HeroCrystal("MageCore", new Mage(), 5, new StatBlock(1, 2, 2, 5));
            var priestCrystal = new HeroCrystal("PriestCore", new Priest(), 5, new StatBlock(2, 2, 3, 4));

            var combined = HeroCrystal.Combine("WizardCore", mageCrystal, priestCrystal);

            Assert.IsNotNull(combined);
            Assert.AreEqual("WizardCore", combined.Name);
            Assert.IsTrue(combined.Job.Skills.Count >= 4, "Combined job should have skills from both parents");
        }

        [TestMethod]
        public void Secondary_Job_Crystal_Can_Be_Used_With_Hero()
        {
            var crystal = new HeroCrystal("PaladinCore", new Paladin(), 3, new StatBlock(4, 1, 3, 2));
            var hero = new Hero("TestPaladin", crystal.Job, crystal.Level, crystal.BaseStats, crystal);

            Assert.IsNotNull(hero);
            Assert.AreEqual("Paladin", hero.Job.Name);
        }

        [TestMethod]
        public void Hero_Can_Earn_JP_With_Secondary_Job()
        {
            var crystal = new HeroCrystal("PaladinCore", new Paladin(), 1, new StatBlock(4, 1, 3, 2));
            var hero = new Hero("TestPaladin", crystal.Job, crystal.Level, crystal.BaseStats, crystal);

            hero.EarnJP(100);

            Assert.AreEqual(100, hero.GetTotalJP());
            Assert.AreEqual(100, hero.GetCurrentJP());
        }

        [TestMethod]
        public void Hero_Can_Purchase_Secondary_Job_Skills()
        {
            // Test skill purchase via crystal (not hero) to avoid auto-learning
            var crystal = new HeroCrystal("PaladinCore", new Paladin(), 3, new StatBlock(4, 1, 3, 2));
            crystal.EarnJP(250);
            
            var job = new Paladin();
            var skill = job.Skills[3]; // Aura Heal - 220 JP, Level 3

            var result = crystal.TryPurchaseSkill(skill);

            Assert.IsTrue(result, "Should be able to purchase Aura Heal");
            Assert.AreEqual(30, crystal.CurrentJP, "Current JP should be reduced by skill cost (250-220=30)");
        }

        [TestMethod]
        public void Secondary_Job_Progression_Complete_Workflow()
        {
            // Test complete workflow: create crystal, earn JP, purchase all skills, verify mastery
            var crystal = new HeroCrystal("PaladinCore", new Paladin(), 3, new StatBlock(4, 1, 3, 2));
            
            // Earn enough JP to purchase all skills: 120+160+200+220 = 700
            crystal.EarnJP(700);

            var job = new Paladin();
            
            // Purchase all skills via crystal
            foreach (var skill in job.Skills)
            {
                var result = crystal.TryPurchaseSkill(skill);
                Assert.IsTrue(result, $"Should be able to purchase {skill.Name}");
            }

            // Verify job is mastered
            Assert.IsTrue(crystal.IsJobMastered(), "Paladin job should be mastered after learning all skills");
            
            // Verify we can create a hero with all skills
            var hero = new Hero("TestPaladin", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            Assert.AreEqual(4, hero.LearnedSkills.Count, "Hero should have all 4 Paladin skills");
        }

        #endregion

        #region Skill Learn Level Tests

        [TestMethod]
        public void Secondary_Job_Skills_Have_Correct_Learn_Levels()
        {
            var job = new Paladin();
            
            Assert.AreEqual(1, job.Skills[0].LearnLevel); // Passive 1
            Assert.AreEqual(2, job.Skills[1].LearnLevel); // Passive 2
            Assert.AreEqual(2, job.Skills[2].LearnLevel); // Active 1
            Assert.AreEqual(3, job.Skills[3].LearnLevel); // Active 2
        }

        [TestMethod]
        public void All_Secondary_Jobs_Follow_Learn_Level_Pattern()
        {
            var jobs = new IJob[]
            {
                new Paladin(), new WarMage(), new Samurai(), new Ninja(), new Marksman(),
                new Wizard(), new DragonFist(), new Spellcloak(), new ArcaneArcher(),
                new DivineFist(), new Shadowmender(), new HolyArcher(), new ShadowFist(),
                new KiShot(), new Stalker()
            };

            foreach (var job in jobs)
            {
                Assert.AreEqual(1, job.Skills[0].LearnLevel, $"{job.Name} first skill should be level 1");
                Assert.AreEqual(2, job.Skills[1].LearnLevel, $"{job.Name} second skill should be level 2");
                Assert.AreEqual(2, job.Skills[2].LearnLevel, $"{job.Name} third skill should be level 2");
                Assert.AreEqual(3, job.Skills[3].LearnLevel, $"{job.Name} fourth skill should be level 3");
            }
        }

        #endregion

        #region Stat Growth Tests

        [TestMethod]
        public void Paladin_Has_Correct_Stat_Growth()
        {
            var job = new Paladin();
            Assert.AreEqual(2, job.GrowthPerLevel.Strength);
            Assert.AreEqual(1, job.GrowthPerLevel.Agility);
            Assert.AreEqual(2, job.GrowthPerLevel.Vitality);
            Assert.AreEqual(1, job.GrowthPerLevel.Magic);
        }

        [TestMethod]
        public void Wizard_Has_Highest_Magic_Growth()
        {
            var wizard = new Wizard();
            var jobs = new IJob[]
            {
                new Paladin(), new WarMage(), new Samurai(), new Ninja(), new Marksman(),
                wizard, new DragonFist(), new Spellcloak(), new ArcaneArcher(),
                new DivineFist(), new Shadowmender(), new HolyArcher(), new ShadowFist(),
                new KiShot(), new Stalker()
            };

            foreach (var job in jobs)
            {
                if (job != wizard)
                {
                    Assert.IsTrue(wizard.GrowthPerLevel.Magic >= job.GrowthPerLevel.Magic, 
                        $"Wizard should have highest magic growth, but {job.Name} has {job.GrowthPerLevel.Magic}");
                }
            }
        }

        [TestMethod]
        public void ShadowFist_Has_Highest_Agility_Base()
        {
            var shadowFist = new ShadowFist();
            Assert.AreEqual(4, shadowFist.BaseBonus.Agility, "Shadow Fist should have 4 base agility");
        }

        #endregion
    }
}
