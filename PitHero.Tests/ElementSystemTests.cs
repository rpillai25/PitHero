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

        #region Monster ElementalProperties Tests

        [TestMethod]
        public void Slime_ShouldHaveElementalPropsWithWaterResistance()
        {
            var slime = new Slime();
            Assert.IsNotNull(slime.ElementalProps);
            Assert.AreEqual(ElementType.Water, slime.ElementalProps.Element);
            Assert.AreEqual(0.3f, slime.ElementalProps.Resistances[ElementType.Water]);
            Assert.AreEqual(-0.3f, slime.ElementalProps.Resistances[ElementType.Fire]);
        }

        [TestMethod]
        public void Bat_ShouldHaveElementalPropsWithWindResistance()
        {
            var bat = new Bat();
            Assert.IsNotNull(bat.ElementalProps);
            Assert.AreEqual(ElementType.Wind, bat.ElementalProps.Element);
            Assert.AreEqual(0.3f, bat.ElementalProps.Resistances[ElementType.Wind]);
            Assert.AreEqual(-0.3f, bat.ElementalProps.Resistances[ElementType.Earth]);
        }

        [TestMethod]
        public void Rat_ShouldHaveElementalPropsWithNoResistances()
        {
            var rat = new Rat();
            Assert.IsNotNull(rat.ElementalProps);
            Assert.AreEqual(ElementType.Neutral, rat.ElementalProps.Element);
            Assert.AreEqual(0, rat.ElementalProps.Resistances.Count);
        }

        [TestMethod]
        public void Goblin_ShouldHaveElementalPropsWithEarthResistance()
        {
            var goblin = new Goblin();
            Assert.IsNotNull(goblin.ElementalProps);
            Assert.AreEqual(ElementType.Earth, goblin.ElementalProps.Element);
            Assert.AreEqual(0.3f, goblin.ElementalProps.Resistances[ElementType.Earth]);
            Assert.AreEqual(-0.3f, goblin.ElementalProps.Resistances[ElementType.Wind]);
        }

        [TestMethod]
        public void Spider_ShouldHaveElementalPropsWithEarthResistance()
        {
            var spider = new Spider();
            Assert.IsNotNull(spider.ElementalProps);
            Assert.AreEqual(ElementType.Earth, spider.ElementalProps.Element);
            Assert.AreEqual(0.3f, spider.ElementalProps.Resistances[ElementType.Earth]);
            Assert.AreEqual(-0.3f, spider.ElementalProps.Resistances[ElementType.Wind]);
        }

        [TestMethod]
        public void Snake_ShouldHaveElementalPropsWithEarthResistance()
        {
            var snake = new Snake();
            Assert.IsNotNull(snake.ElementalProps);
            Assert.AreEqual(ElementType.Earth, snake.ElementalProps.Element);
            Assert.AreEqual(0.3f, snake.ElementalProps.Resistances[ElementType.Earth]);
            Assert.AreEqual(-0.3f, snake.ElementalProps.Resistances[ElementType.Wind]);
        }

        [TestMethod]
        public void Skeleton_ShouldHaveElementalPropsWithDarkResistance()
        {
            var skeleton = new Skeleton();
            Assert.IsNotNull(skeleton.ElementalProps);
            Assert.AreEqual(ElementType.Dark, skeleton.ElementalProps.Element);
            Assert.AreEqual(0.3f, skeleton.ElementalProps.Resistances[ElementType.Dark]);
            Assert.AreEqual(-0.3f, skeleton.ElementalProps.Resistances[ElementType.Light]);
        }

        [TestMethod]
        public void Orc_ShouldHaveElementalPropsWithFireResistance()
        {
            var orc = new Orc();
            Assert.IsNotNull(orc.ElementalProps);
            Assert.AreEqual(ElementType.Fire, orc.ElementalProps.Element);
            Assert.AreEqual(0.3f, orc.ElementalProps.Resistances[ElementType.Fire]);
            Assert.AreEqual(-0.3f, orc.ElementalProps.Resistances[ElementType.Water]);
        }

        [TestMethod]
        public void Wraith_ShouldHaveElementalPropsWithDarkResistance()
        {
            var wraith = new Wraith();
            Assert.IsNotNull(wraith.ElementalProps);
            Assert.AreEqual(ElementType.Dark, wraith.ElementalProps.Element);
            Assert.AreEqual(0.3f, wraith.ElementalProps.Resistances[ElementType.Dark]);
            Assert.AreEqual(-0.3f, wraith.ElementalProps.Resistances[ElementType.Light]);
        }

        [TestMethod]
        public void PitLord_ShouldHaveElementalPropsWithFireResistance()
        {
            var pitLord = new PitLord();
            Assert.IsNotNull(pitLord.ElementalProps);
            Assert.AreEqual(ElementType.Fire, pitLord.ElementalProps.Element);
            Assert.AreEqual(0.3f, pitLord.ElementalProps.Resistances[ElementType.Fire]);
            Assert.AreEqual(-0.3f, pitLord.ElementalProps.Resistances[ElementType.Water]);
        }

        #endregion
    }
}
