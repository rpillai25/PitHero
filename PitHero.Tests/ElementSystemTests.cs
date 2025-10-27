using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;

namespace PitHero.Tests
{
    [TestClass]
    public class ElementSystemTests
    {
        #region Enemy Element Tests

        [TestMethod]
        public void Slime_ShouldHaveWaterElement()
        {
            var slime = new Slime();
            Assert.AreEqual(ElementType.Water, slime.Element);
        }

        [TestMethod]
        public void Goblin_ShouldHaveEarthElement()
        {
            var goblin = new Goblin();
            Assert.AreEqual(ElementType.Earth, goblin.Element);
        }

        [TestMethod]
        public void Bat_ShouldHaveWindElement()
        {
            var bat = new Bat();
            Assert.AreEqual(ElementType.Wind, bat.Element);
        }

        [TestMethod]
        public void Rat_ShouldHaveNeutralElement()
        {
            var rat = new Rat();
            Assert.AreEqual(ElementType.Neutral, rat.Element);
        }

        [TestMethod]
        public void Spider_ShouldHaveEarthElement()
        {
            var spider = new Spider();
            Assert.AreEqual(ElementType.Earth, spider.Element);
        }

        [TestMethod]
        public void Snake_ShouldHaveEarthElement()
        {
            var snake = new Snake();
            Assert.AreEqual(ElementType.Earth, snake.Element);
        }

        [TestMethod]
        public void Skeleton_ShouldHaveDarkElement()
        {
            var skeleton = new Skeleton();
            Assert.AreEqual(ElementType.Dark, skeleton.Element);
        }

        [TestMethod]
        public void Orc_ShouldHaveFireElement()
        {
            var orc = new Orc();
            Assert.AreEqual(ElementType.Fire, orc.Element);
        }

        [TestMethod]
        public void Wraith_ShouldHaveDarkElement()
        {
            var wraith = new Wraith();
            Assert.AreEqual(ElementType.Dark, wraith.Element);
        }

        [TestMethod]
        public void PitLord_ShouldHaveFireElement()
        {
            var pitLord = new PitLord();
            Assert.AreEqual(ElementType.Fire, pitLord.Element);
        }

        #endregion

        #region Elemental Opposing Tests

        [TestMethod]
        public void GetOpposingElement_Fire_ReturnsWater()
        {
            var opposing = ElementalProperties.GetOpposingElement(ElementType.Fire);
            Assert.AreEqual(ElementType.Water, opposing);
        }

        [TestMethod]
        public void GetOpposingElement_Water_ReturnsFire()
        {
            var opposing = ElementalProperties.GetOpposingElement(ElementType.Water);
            Assert.AreEqual(ElementType.Fire, opposing);
        }

        [TestMethod]
        public void GetOpposingElement_Earth_ReturnsWind()
        {
            var opposing = ElementalProperties.GetOpposingElement(ElementType.Earth);
            Assert.AreEqual(ElementType.Wind, opposing);
        }

        [TestMethod]
        public void GetOpposingElement_Wind_ReturnsEarth()
        {
            var opposing = ElementalProperties.GetOpposingElement(ElementType.Wind);
            Assert.AreEqual(ElementType.Earth, opposing);
        }

        [TestMethod]
        public void GetOpposingElement_Light_ReturnsDark()
        {
            var opposing = ElementalProperties.GetOpposingElement(ElementType.Light);
            Assert.AreEqual(ElementType.Dark, opposing);
        }

        [TestMethod]
        public void GetOpposingElement_Dark_ReturnsLight()
        {
            var opposing = ElementalProperties.GetOpposingElement(ElementType.Dark);
            Assert.AreEqual(ElementType.Light, opposing);
        }

        [TestMethod]
        public void GetOpposingElement_Neutral_ReturnsNeutral()
        {
            var opposing = ElementalProperties.GetOpposingElement(ElementType.Neutral);
            Assert.AreEqual(ElementType.Neutral, opposing);
        }

        #endregion

        #region Elemental Multiplier Tests - Advantage (2x)

        [TestMethod]
        public void GetElementalMultiplier_FireVsWater_Returns2x()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Fire, ElementType.Water);
            Assert.AreEqual(2.0f, multiplier);
        }

        [TestMethod]
        public void GetElementalMultiplier_WaterVsFire_Returns2x()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Water, ElementType.Fire);
            Assert.AreEqual(2.0f, multiplier);
        }

        [TestMethod]
        public void GetElementalMultiplier_EarthVsWind_Returns2x()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Earth, ElementType.Wind);
            Assert.AreEqual(2.0f, multiplier);
        }

        [TestMethod]
        public void GetElementalMultiplier_WindVsEarth_Returns2x()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Wind, ElementType.Earth);
            Assert.AreEqual(2.0f, multiplier);
        }

        [TestMethod]
        public void GetElementalMultiplier_LightVsDark_Returns2x()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Light, ElementType.Dark);
            Assert.AreEqual(2.0f, multiplier);
        }

        [TestMethod]
        public void GetElementalMultiplier_DarkVsLight_Returns2x()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Dark, ElementType.Light);
            Assert.AreEqual(2.0f, multiplier);
        }

        #endregion

        #region Elemental Multiplier Tests - Disadvantage (0.5x)

        [TestMethod]
        public void GetElementalMultiplier_FireVsFire_ReturnsHalf()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Fire, ElementType.Fire);
            Assert.AreEqual(0.5f, multiplier);
        }

        [TestMethod]
        public void GetElementalMultiplier_WaterVsWater_ReturnsHalf()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Water, ElementType.Water);
            Assert.AreEqual(0.5f, multiplier);
        }

        [TestMethod]
        public void GetElementalMultiplier_EarthVsEarth_ReturnsHalf()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Earth, ElementType.Earth);
            Assert.AreEqual(0.5f, multiplier);
        }

        [TestMethod]
        public void GetElementalMultiplier_WindVsWind_ReturnsHalf()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Wind, ElementType.Wind);
            Assert.AreEqual(0.5f, multiplier);
        }

        [TestMethod]
        public void GetElementalMultiplier_LightVsLight_ReturnsHalf()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Light, ElementType.Light);
            Assert.AreEqual(0.5f, multiplier);
        }

        [TestMethod]
        public void GetElementalMultiplier_DarkVsDark_ReturnsHalf()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Dark, ElementType.Dark);
            Assert.AreEqual(0.5f, multiplier);
        }

        #endregion

        #region Elemental Multiplier Tests - Neutral (1x)

        [TestMethod]
        public void GetElementalMultiplier_NeutralAttack_ReturnsNeutral()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Neutral, ElementType.Fire);
            Assert.AreEqual(1.0f, multiplier);
        }

        [TestMethod]
        public void GetElementalMultiplier_NeutralTarget_ReturnsNeutral()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Fire, ElementType.Neutral);
            Assert.AreEqual(1.0f, multiplier);
        }

        [TestMethod]
        public void GetElementalMultiplier_FireVsEarth_ReturnsNeutral()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Fire, ElementType.Earth);
            Assert.AreEqual(1.0f, multiplier);
        }

        [TestMethod]
        public void GetElementalMultiplier_WaterVsWind_ReturnsNeutral()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Water, ElementType.Wind);
            Assert.AreEqual(1.0f, multiplier);
        }

        [TestMethod]
        public void GetElementalMultiplier_LightVsEarth_ReturnsNeutral()
        {
            var multiplier = ElementalProperties.GetElementalMultiplier(ElementType.Light, ElementType.Earth);
            Assert.AreEqual(1.0f, multiplier);
        }

        #endregion

        #region ElementalProperties Class Tests

        [TestMethod]
        public void ElementalProperties_Constructor_SetsElement()
        {
            var props = new ElementalProperties(ElementType.Fire);
            Assert.AreEqual(ElementType.Fire, props.Element);
        }

        [TestMethod]
        public void ElementalProperties_Constructor_InitializesEmptyResistances()
        {
            var props = new ElementalProperties(ElementType.Fire);
            Assert.IsNotNull(props.Resistances);
            Assert.AreEqual(0, props.Resistances.Count);
        }

        [TestMethod]
        public void ElementalProperties_ConstructorWithResistances_SetsValues()
        {
            var resistances = new System.Collections.Generic.Dictionary<ElementType, float>
            {
                { ElementType.Water, 0.5f },
                { ElementType.Fire, -0.5f }
            };
            
            var props = new ElementalProperties(ElementType.Fire, resistances);
            
            Assert.AreEqual(ElementType.Fire, props.Element);
            Assert.AreEqual(2, props.Resistances.Count);
            Assert.AreEqual(0.5f, props.Resistances[ElementType.Water]);
            Assert.AreEqual(-0.5f, props.Resistances[ElementType.Fire]);
        }

        #endregion
    }
}
