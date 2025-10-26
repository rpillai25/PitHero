using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Skills;
using System.Collections.Generic;

namespace PitHero.Tests
{
    [TestClass]
    public class SkillElementTests
    {
        /// <summary>Test skill for verifying element functionality</summary>
        private sealed class TestFireSkill : BaseSkill
        {
            public TestFireSkill() 
                : base("test.fire", "Fire Blast", "A test fire attack", 
                      SkillKind.Active, SkillTargetType.SingleEnemy, 5, 100, ElementType.Fire) 
            { }

            public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
            {
                return "FireBlast";
            }
        }

        /// <summary>Test skill with default neutral element</summary>
        private sealed class TestNeutralSkill : BaseSkill
        {
            public TestNeutralSkill() 
                : base("test.neutral", "Neutral Attack", "A test neutral attack", 
                      SkillKind.Active, SkillTargetType.SingleEnemy, 3, 80) 
            { }
        }

        /// <summary>Test skill with water element</summary>
        private sealed class TestWaterSkill : BaseSkill
        {
            public TestWaterSkill() 
                : base("test.water", "Water Wave", "A test water attack", 
                      SkillKind.Active, SkillTargetType.SurroundingEnemies, 6, 120, ElementType.Water) 
            { }
        }

        /// <summary>Test skill with earth element</summary>
        private sealed class TestEarthSkill : BaseSkill
        {
            public TestEarthSkill() 
                : base("test.earth", "Earth Spike", SkillKind.Active, SkillTargetType.SingleEnemy, 4, 90, ElementType.Earth) 
            { }
        }

        /// <summary>Test skill with wind element</summary>
        private sealed class TestWindSkill : BaseSkill
        {
            public TestWindSkill() 
                : base("test.wind", "Wind Slash", "A test wind attack", 
                      SkillKind.Active, SkillTargetType.SingleEnemy, 4, 95, ElementType.Wind) 
            { }
        }

        /// <summary>Test skill with light element</summary>
        private sealed class TestLightSkill : BaseSkill
        {
            public TestLightSkill() 
                : base("test.light", "Holy Light", "A test light attack", 
                      SkillKind.Active, SkillTargetType.SingleEnemy, 7, 150, ElementType.Light) 
            { }
        }

        /// <summary>Test skill with dark element</summary>
        private sealed class TestDarkSkill : BaseSkill
        {
            public TestDarkSkill() 
                : base("test.dark", "Shadow Strike", "A test dark attack", 
                      SkillKind.Active, SkillTargetType.SingleEnemy, 7, 150, ElementType.Dark) 
            { }
        }

        /// <summary>Test passive skill with element</summary>
        private sealed class TestPassiveFireSkill : BaseSkill
        {
            public TestPassiveFireSkill() 
                : base("test.passive_fire", "Fire Aura", "A test fire passive", 
                      SkillKind.Passive, SkillTargetType.Self, 0, 200, ElementType.Fire) 
            { }

            public override void ApplyPassive(Hero hero)
            {
                // Passive implementation
            }
        }

        #region BaseSkill Element Tests

        [TestMethod]
        public void BaseSkill_WithFireElement_ShouldHaveFireElement()
        {
            var skill = new TestFireSkill();
            Assert.AreEqual(ElementType.Fire, skill.Element);
        }

        [TestMethod]
        public void BaseSkill_WithoutElement_ShouldDefaultToNeutral()
        {
            var skill = new TestNeutralSkill();
            Assert.AreEqual(ElementType.Neutral, skill.Element);
        }

        [TestMethod]
        public void BaseSkill_WithWaterElement_ShouldHaveWaterElement()
        {
            var skill = new TestWaterSkill();
            Assert.AreEqual(ElementType.Water, skill.Element);
        }

        [TestMethod]
        public void BaseSkill_WithEarthElement_ShouldHaveEarthElement()
        {
            var skill = new TestEarthSkill();
            Assert.AreEqual(ElementType.Earth, skill.Element);
        }

        [TestMethod]
        public void BaseSkill_WithWindElement_ShouldHaveWindElement()
        {
            var skill = new TestWindSkill();
            Assert.AreEqual(ElementType.Wind, skill.Element);
        }

        [TestMethod]
        public void BaseSkill_WithLightElement_ShouldHaveLightElement()
        {
            var skill = new TestLightSkill();
            Assert.AreEqual(ElementType.Light, skill.Element);
        }

        [TestMethod]
        public void BaseSkill_WithDarkElement_ShouldHaveDarkElement()
        {
            var skill = new TestDarkSkill();
            Assert.AreEqual(ElementType.Dark, skill.Element);
        }

        [TestMethod]
        public void BaseSkill_PassiveWithElement_ShouldHaveElement()
        {
            var skill = new TestPassiveFireSkill();
            Assert.AreEqual(ElementType.Fire, skill.Element);
            Assert.AreEqual(SkillKind.Passive, skill.Kind);
        }

        #endregion

        #region ISkill Interface Tests

        [TestMethod]
        public void ISkill_Interface_ShouldExposeElementProperty()
        {
            ISkill skill = new TestFireSkill();
            Assert.AreEqual(ElementType.Fire, skill.Element);
        }

        [TestMethod]
        public void ISkill_WithNeutralElement_ShouldExposeNeutral()
        {
            ISkill skill = new TestNeutralSkill();
            Assert.AreEqual(ElementType.Neutral, skill.Element);
        }

        #endregion

        #region Skill Property Preservation Tests

        [TestMethod]
        public void Skill_WithElement_ShouldPreserveAllProperties()
        {
            var skill = new TestFireSkill();

            Assert.AreEqual("test.fire", skill.Id);
            Assert.AreEqual("Fire Blast", skill.Name);
            Assert.AreEqual("A test fire attack", skill.Description);
            Assert.AreEqual(SkillKind.Active, skill.Kind);
            Assert.AreEqual(SkillTargetType.SingleEnemy, skill.TargetType);
            Assert.AreEqual(5, skill.MPCost);
            Assert.AreEqual(100, skill.JPCost);
            Assert.AreEqual(ElementType.Fire, skill.Element);
        }

        [TestMethod]
        public void Skill_ShortConstructor_ShouldSetDefaultNeutral()
        {
            var skill = new TestEarthSkill();

            Assert.AreEqual("test.earth", skill.Id);
            Assert.AreEqual("Earth Spike", skill.Name);
            Assert.AreEqual("", skill.Description); // Short constructor uses empty description
            Assert.AreEqual(SkillKind.Active, skill.Kind);
            Assert.AreEqual(SkillTargetType.SingleEnemy, skill.TargetType);
            Assert.AreEqual(4, skill.MPCost);
            Assert.AreEqual(90, skill.JPCost);
            Assert.AreEqual(ElementType.Earth, skill.Element);
        }

        #endregion

        #region Multiple Skills Element Tests

        [TestMethod]
        public void MultipleSkills_ShouldHaveIndependentElements()
        {
            var fireSkill = new TestFireSkill();
            var waterSkill = new TestWaterSkill();
            var neutralSkill = new TestNeutralSkill();

            Assert.AreEqual(ElementType.Fire, fireSkill.Element);
            Assert.AreEqual(ElementType.Water, waterSkill.Element);
            Assert.AreEqual(ElementType.Neutral, neutralSkill.Element);
        }

        #endregion
    }
}
