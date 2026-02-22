using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.ECS.Components;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the hairstyle implementation to verify that:
    /// 1. GameConfig has a MaleHeroHairstyleCount constant
    /// 2. HeroHairAnimationComponent accepts hairstyle index
    /// 3. Animation names are correctly formatted based on hairstyle index
    /// </summary>
    [TestClass]
    public class HairstyleImplementationTests
    {
        [TestMethod]
        public void GameConfig_ShouldHaveMaleHeroHairstyleCount()
        {
            // Verify that GameConfig has MaleHeroHairstyleCount constant
            var hairstyleCount = GameConfig.MaleHeroHairstyleCount;
            Assert.IsTrue(hairstyleCount >= 1, "MaleHeroHairstyleCount should be at least 1");
            Assert.AreEqual(5, hairstyleCount, "MaleHeroHairstyleCount should be 5");
        }

        [TestMethod]
        public void HeroHairAnimationComponent_DefaultHairstyle_ShouldHaveNoSuffix()
        {
            // Create component with default hairstyle (1)
            var component = new HeroHairAnimationComponent(Color.White, hairstyleIndex: 1);
            Assert.IsNotNull(component);

            // Use reflection to get DefaultAnimation property
            var defaultAnimProperty = typeof(HeroHairAnimationComponent)
                .GetProperty("DefaultAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(defaultAnimProperty);

            var defaultAnim = defaultAnimProperty!.GetValue(component) as string;
            Assert.AreEqual("MaleHeroHairWalkDown", defaultAnim, 
                "Hairstyle 1 should use 'MaleHeroHairWalkDown' (no number suffix)");
        }

        [TestMethod]
        public void HeroHairAnimationComponent_Hairstyle2_ShouldHave2Suffix()
        {
            // Create component with hairstyle 2
            var component = new HeroHairAnimationComponent(Color.White, hairstyleIndex: 2);
            Assert.IsNotNull(component);

            // Use reflection to get DefaultAnimation property
            var defaultAnimProperty = typeof(HeroHairAnimationComponent)
                .GetProperty("DefaultAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(defaultAnimProperty);

            var defaultAnim = defaultAnimProperty!.GetValue(component) as string;
            Assert.AreEqual("MaleHeroHair2WalkDown", defaultAnim, 
                "Hairstyle 2 should use 'MaleHeroHair2WalkDown'");
        }

        [TestMethod]
        public void HeroHairAnimationComponent_Hairstyle3_ShouldHave3Suffix()
        {
            // Create component with hairstyle 3
            var component = new HeroHairAnimationComponent(Color.White, hairstyleIndex: 3);
            Assert.IsNotNull(component);

            // Use reflection to get DefaultAnimation property
            var defaultAnimProperty = typeof(HeroHairAnimationComponent)
                .GetProperty("DefaultAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(defaultAnimProperty);

            var defaultAnim = defaultAnimProperty!.GetValue(component) as string;
            Assert.AreEqual("MaleHeroHair3WalkDown", defaultAnim, 
                "Hairstyle 3 should use 'MaleHeroHair3WalkDown'");
        }

        [TestMethod]
        public void HeroHairAnimationComponent_AllDirections_ShouldHaveCorrectSuffix()
        {
            // Test hairstyle 2 for all animation directions
            var component = new HeroHairAnimationComponent(Color.White, hairstyleIndex: 2);
            Assert.IsNotNull(component);

            var animProperties = new[]
            {
                ("AnimDown", "MaleHeroHair2WalkDown"),
                ("AnimUp", "MaleHeroHair2WalkUp"),
                ("AnimRight", "MaleHeroHair2WalkRight"),
                ("AnimLeft", "MaleHeroHair2WalkRight"), // Left uses Right (flipped)
                ("JumpAnimDown", "MaleHeroHair2JumpRight"),
                ("JumpAnimLeft", "MaleHeroHair2JumpRight"),
                ("JumpAnimRight", "MaleHeroHair2JumpRight"),
                ("JumpAnimUp", "MaleHeroHair2JumpRight")
            };

            foreach (var (propertyName, expectedValue) in animProperties)
            {
                var property = typeof(HeroHairAnimationComponent)
                    .GetProperty(propertyName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(property, $"Property {propertyName} should exist");

                var value = property.GetValue(component) as string;
                Assert.AreEqual(expectedValue, value, 
                    $"Hairstyle 2 {propertyName} should be '{expectedValue}'");
            }
        }

        [TestMethod]
        public void HeroHairAnimationComponent_CanBeConstructedWithoutHairstyleParameter()
        {
            // Test backward compatibility - should default to hairstyle 1
            var component = new HeroHairAnimationComponent(Color.White);
            Assert.IsNotNull(component);

            var defaultAnimProperty = typeof(HeroHairAnimationComponent)
                .GetProperty("DefaultAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(defaultAnimProperty);

            var defaultAnim = defaultAnimProperty!.GetValue(component) as string;
            Assert.AreEqual("MaleHeroHairWalkDown", defaultAnim, 
                "Default constructor should use hairstyle 1");
        }

        [TestMethod]
        public void HeroHairAnimationComponent_AllHairstyles_ShouldBeValid()
        {
            // Test that all hairstyles from 1 to MaleHeroHairstyleCount can be created
            for (int i = 1; i <= GameConfig.MaleHeroHairstyleCount; i++)
            {
                var component = new HeroHairAnimationComponent(Color.White, hairstyleIndex: i);
                Assert.IsNotNull(component, $"Should be able to create component with hairstyle {i}");

                var defaultAnimProperty = typeof(HeroHairAnimationComponent)
                    .GetProperty("DefaultAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(defaultAnimProperty, "DefaultAnimation property should exist");
                var defaultAnim = defaultAnimProperty!.GetValue(component) as string;

                string expectedSuffix = i == 1 ? "" : i.ToString();
                string expectedAnimation = $"MaleHeroHair{expectedSuffix}WalkDown";
                Assert.AreEqual(expectedAnimation, defaultAnim,
                    $"Hairstyle {i} should use animation '{expectedAnimation}'");
            }
        }
    }
}
