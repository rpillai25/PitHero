using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class MercenarySkillTests
    {
        #region LearnAllJobSkills Tests

        [TestMethod]
        public void LearnAllJobSkills_Knight_LearnsAllFourSkills()
        {
            var merc = new Mercenary("Test", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(4, merc.LearnedSkills.Count, "Knight should have 4 skills");
        }

        [TestMethod]
        public void LearnAllJobSkills_Thief_LearnsAllFourSkills()
        {
            var merc = new Mercenary("Test", new Thief(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(4, merc.LearnedSkills.Count, "Thief should have 4 skills");
        }

        [TestMethod]
        public void LearnAllJobSkills_Mage_LearnsAllFourSkills()
        {
            var merc = new Mercenary("Test", new Mage(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(4, merc.LearnedSkills.Count, "Mage should have 4 skills");
        }

        [TestMethod]
        public void LearnAllJobSkills_Priest_LearnsAllFourSkills()
        {
            var merc = new Mercenary("Test", new Priest(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(4, merc.LearnedSkills.Count, "Priest should have 4 skills");
        }

        [TestMethod]
        public void LearnAllJobSkills_Monk_LearnsAllFourSkills()
        {
            var merc = new Mercenary("Test", new Monk(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(4, merc.LearnedSkills.Count, "Monk should have 4 skills");
        }

        [TestMethod]
        public void LearnAllJobSkills_Archer_LearnsAllFourSkills()
        {
            var merc = new Mercenary("Test", new Archer(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(4, merc.LearnedSkills.Count, "Archer should have 4 skills");
        }

        [TestMethod]
        public void LearnAllJobSkills_CalledTwice_DoesNotDuplicate()
        {
            var merc = new Mercenary("Test", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            merc.LearnAllJobSkills();
            Assert.AreEqual(4, merc.LearnedSkills.Count, "Calling LearnAllJobSkills twice should not duplicate skills");
        }

        #endregion

        #region Active Skill Tests

        [TestMethod]
        public void LearnAllJobSkills_Knight_HasActiveAttackSkills()
        {
            var merc = new Mercenary("Test", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();

            bool hasActive = false;
            var enumerator = merc.LearnedSkills.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Value.Kind == SkillKind.Active)
                {
                    hasActive = true;
                    break;
                }
            }
            Assert.IsTrue(hasActive, "Knight should have at least one active skill for battle");
        }

        [TestMethod]
        public void LearnAllJobSkills_Mage_HasActiveAttackSkills()
        {
            var merc = new Mercenary("Test", new Mage(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();

            bool hasActive = false;
            var enumerator = merc.LearnedSkills.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Value.Kind == SkillKind.Active)
                {
                    hasActive = true;
                    break;
                }
            }
            Assert.IsTrue(hasActive, "Mage should have at least one active skill for battle");
        }

        #endregion

        #region Passive Skill Application Tests

        [TestMethod]
        public void LearnAllJobSkills_Knight_AppliesPassiveDefenseBonus()
        {
            var merc = new Mercenary("Test", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(2, merc.PassiveDefenseBonus, "Knight passive should give +2 defense bonus");
        }

        [TestMethod]
        public void LearnAllJobSkills_Mage_AppliesFireDamageBonus()
        {
            var merc = new Mercenary("Test", new Mage(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(0.25f, merc.FireDamageBonus, 0.001f, "Mage passive should give +25% fire damage");
        }

        [TestMethod]
        public void LearnAllJobSkills_Mage_AppliesMPCostReduction()
        {
            var merc = new Mercenary("Test", new Mage(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(0.15f, merc.MPCostReduction, 0.001f, "Mage passive should give -15% MP cost");
        }

        [TestMethod]
        public void LearnAllJobSkills_Priest_AppliesMPTickRegen()
        {
            var merc = new Mercenary("Test", new Priest(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(1, merc.MPTickRegen, "Priest passive should give +1 MP tick regen");
        }

        [TestMethod]
        public void LearnAllJobSkills_Priest_AppliesHealPowerBonus()
        {
            var merc = new Mercenary("Test", new Priest(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(0.25f, merc.HealPowerBonus, 0.001f, "Priest passive should give +25% heal power");
        }

        [TestMethod]
        public void LearnAllJobSkills_Monk_AppliesEnableCounter()
        {
            var merc = new Mercenary("Test", new Monk(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.IsTrue(merc.EnableCounter, "Monk passive should enable counter");
        }

        [TestMethod]
        public void LearnAllJobSkills_Monk_AppliesDeflectChance()
        {
            var merc = new Mercenary("Test", new Monk(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(0.15f, merc.DeflectChance, 0.001f, "Monk passive should give 15% deflect chance");
        }

        [TestMethod]
        public void LearnAllJobSkills_NoSkills_PassivesAreZero()
        {
            var merc = new Mercenary("Test", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            Assert.AreEqual(0, merc.PassiveDefenseBonus, "Before learning skills, passive defense should be 0");
            Assert.AreEqual(0f, merc.DeflectChance, "Before learning skills, deflect chance should be 0");
            Assert.IsFalse(merc.EnableCounter, "Before learning skills, counter should be disabled");
        }

        #endregion
    }
}
