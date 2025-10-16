using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class TertiaryJobSystemTests
    {
        #region Tertiary Job Instantiation Tests

        [TestMethod]
        public void Templar_Job_Can_Be_Created()
        {
            var job = new Templar();
            Assert.AreEqual("Templar", job.Name);
            Assert.AreEqual(5, job.BaseBonus.Strength);
            Assert.AreEqual(2, job.BaseBonus.Agility);
            Assert.AreEqual(4, job.BaseBonus.Vitality);
            Assert.AreEqual(3, job.BaseBonus.Magic);
            Assert.AreEqual(4, job.Skills.Count, "Templar should have 4 skills");
        }

        [TestMethod]
        public void All_Implemented_Tertiary_Jobs_Can_Be_Instantiated()
        {
            var jobs = new IJob[]
            {
                new Templar(), new ShinobiMaster(), new SpellSniper(),
                new SoulGuardian(), new Trickshot(), new SeraphHunter(),
                new MysticAvenger(), new ShadowPaladin(), new ArcaneSamurai(),
                new MarksmanWizard(), new DragonMarksman(),
                new DivineCloak(), new StalkerMonk(), new HolyShadow(),
                new KiNinja(), new ArcaneStalker(), new ShadowAvenger(),
                new DivineArcher(), new MysticMarksman(), new SilentHunter(),
                new DivineSamurai(), new MysticStalker()
            };

            foreach (var job in jobs)
            {
                Assert.IsNotNull(job, $"{job.Name} should be instantiable");
                Assert.AreEqual(4, job.Skills.Count, $"{job.Name} should have 4 skills");
            }
        }

        #endregion

        #region JP Cost Tests

        [TestMethod]
        public void Tertiary_Job_Skills_Have_Higher_JP_Costs()
        {
            var templar = new Templar();
            
            // Tertiary jobs should have JP costs in range 180-250
            foreach (var skill in templar.Skills)
            {
                Assert.IsTrue(skill.JPCost >= 180 && skill.JPCost <= 250,
                    $"{skill.Name} should have JP cost between 180 and 250, got {skill.JPCost}");
            }
        }

        [TestMethod]
        public void All_Tertiary_Jobs_Follow_Learn_Level_Pattern()
        {
            var jobs = new IJob[]
            {
                new Templar(), new ShinobiMaster(), new SpellSniper(),
                new SoulGuardian(), new Trickshot(), new SeraphHunter(),
                new MysticAvenger(), new ShadowPaladin(), new ArcaneSamurai(),
                new MarksmanWizard(), new DragonMarksman(),
                new DivineCloak(), new StalkerMonk(), new HolyShadow(),
                new KiNinja(), new ArcaneStalker(), new ShadowAvenger(),
                new DivineArcher(), new MysticMarksman(), new SilentHunter(),
                new DivineSamurai(), new MysticStalker()
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

        #region Crystal and Hero Integration Tests

        [TestMethod]
        public void Tertiary_Job_Crystal_Can_Be_Created()
        {
            var crystal = new HeroCrystal("TemplarCore", new Templar(), 3, new StatBlock(5, 2, 4, 3));
            
            Assert.IsNotNull(crystal);
            Assert.AreEqual("Templar", crystal.Job.Name);
            Assert.AreEqual(3, crystal.Level);
        }

        [TestMethod]
        public void Hero_Can_Use_Tertiary_Job()
        {
            var crystal = new HeroCrystal("TemplarCore", new Templar(), 3, new StatBlock(5, 2, 4, 3));
            var hero = new Hero("TestTemplar", crystal.Job, crystal.Level, crystal.BaseStats, crystal);

            Assert.IsNotNull(hero);
            Assert.AreEqual("Templar", hero.Job.Name);
        }

        [TestMethod]
        public void Tertiary_Job_Skills_Can_Be_Purchased()
        {
            var crystal = new HeroCrystal("TemplarCore", new Templar(), 3, new StatBlock(5, 2, 4, 3));
            crystal.EarnJP(200);

            var job = new Templar();
            var skill = job.Skills[0]; // Battle Meditation - 180 JP

            var result = crystal.TryPurchaseSkill(skill);

            Assert.IsTrue(result, "Should be able to purchase Battle Meditation");
            Assert.AreEqual(20, crystal.CurrentJP, "Current JP should be reduced (200-180=20)");
        }

        #endregion
    }
}
