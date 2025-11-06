using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class RolePlayingFrameworkHeroCrystalSkillTests
    {
        /// <summary>Leveling a hero persists learned skills into its bound crystal.</summary>
        [TestMethod]
        public void Crystal_Persists_Knight_Skills_On_LevelUp()
        {
            var crystal = new HeroCrystal("KnightCore", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            var hero = new Hero("Arthur", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            hero.AddExperience(300); // reach level 3 (learn level 1/2/3 skills)

            Assert.IsTrue(crystal.HasSkill("knight.light_armor"), "Light Armor passive should be stored in crystal.");
            Assert.IsTrue(crystal.HasSkill("knight.heavy_armor"), "Heavy Armor passive should be stored in crystal.");
            Assert.IsTrue(crystal.HasSkill("knight.spin_slash"), "Spin Slash should be stored in crystal.");
            Assert.IsTrue(crystal.HasSkill("knight.heavy_strike"), "Heavy Strike should be stored in crystal.");
        }

        /// <summary>Creating a hero from an existing crystal restores all learned skills.</summary>
        [TestMethod]
        public void Hero_Restores_Skills_From_Crystal()
        {
            var crystal = new HeroCrystal("KnightCore", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            var hero1 = new Hero("Arthur", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            hero1.AddExperience(300); // learn up to level 3

            // New hero from same crystal should preload skills without leveling again
            var hero2 = new Hero("Lancelot", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            Assert.IsTrue(hero2.LearnedSkills.ContainsKey("knight.light_armor"));
            Assert.IsTrue(hero2.LearnedSkills.ContainsKey("knight.heavy_armor"));
            Assert.IsTrue(hero2.LearnedSkills.ContainsKey("knight.spin_slash"));
            Assert.IsTrue(hero2.LearnedSkills.ContainsKey("knight.heavy_strike"));
        }

        /// <summary>Combining two crystals unions their learned skill sets.</summary>
        [TestMethod]
        public void Combine_Crystals_Unions_Skills()
        {
            // Mage crystal learns Fire (level 2)
            var mageCrystal = new HeroCrystal("MageCore", new Mage(), 1, new StatBlock(1, 2, 2, 5));
            var mageHero = new Hero("Vivi", mageCrystal.Job, mageCrystal.Level, mageCrystal.BaseStats, mageCrystal);
            mageHero.AddExperience(200); // reach level 2: fire + economist + heart_of_fire already at 1
            Assert.IsTrue(mageCrystal.HasSkill("mage.fire"), "Mage crystal should have Fire skill.");

            // Priest crystal learns Heal (level 2)
            var priestCrystal = new HeroCrystal("PriestCore", new Priest(), 1, new StatBlock(1, 2, 3, 4));
            var priestHero = new Hero("Yuna", priestCrystal.Job, priestCrystal.Level, priestCrystal.BaseStats, priestCrystal);
            priestHero.AddExperience(100); // reach level 2: heal + mender + calm spirit already at 1
            Assert.IsTrue(priestCrystal.HasSkill("priest.heal"), "Priest crystal should have Heal skill.");

            var combined = HeroCrystal.Combine("Archon", mageCrystal, priestCrystal);
            Assert.IsTrue(combined.HasSkill("mage.fire"), "Combined crystal should retain mage Fire skill.");
            Assert.IsTrue(combined.HasSkill("priest.heal"), "Combined crystal should retain priest Heal skill.");

            // Hero from combined crystal should preload both
            var archonHero = new Hero("Archon", combined.Job, combined.Level, combined.BaseStats, combined);
            Assert.IsTrue(archonHero.LearnedSkills.ContainsKey("mage.fire"));
            Assert.IsTrue(archonHero.LearnedSkills.ContainsKey("priest.heal"));
        }
    }
}
