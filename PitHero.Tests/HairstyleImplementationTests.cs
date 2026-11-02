using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.ECS.Components;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the hairstyle implementation to verify that:
    /// 1. GameConfig has a HeroHairstyleCount constant
    /// 2. HeroHairAnimationComponent accepts hairstyle index
    /// 3. Animation names are correctly formatted based on hairstyle index
    /// </summary>
    [TestClass]
    public class HairstyleImplementationTests
    {
        [TestMethod]
        public void GameConfig_ShouldHaveHeroHairstyleCount()
        {
            // Verify that GameConfig has HeroHairstyleCount constant
            var hairstyleCount = GameConfig.HeroHairstyleCount;
            Assert.IsTrue(hairstyleCount >= 1, "HeroHairstyleCount should be at least 1");
            Assert.AreEqual(4, hairstyleCount, "HeroHairstyleCount should be 4");
        }

        [TestMethod]
        public void HeroHairAnimationComponent_DefaultHairstyle_ShouldHave1Suffix()
        {
            // Create component with default hairstyle (1)
            var component = new HeroHairAnimationComponent(Color.White, hairstyleIndex: 1);
            Assert.IsNotNull(component);

            // Use reflection to get DefaultAnimation property
            var defaultAnimProperty = typeof(HeroHairAnimationComponent)
                .GetProperty("DefaultAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(defaultAnimProperty);

            var defaultAnim = defaultAnimProperty!.GetValue(component) as string;
            Assert.AreEqual("MaleHeroHair1MoveDown", defaultAnim, 
                "Hairstyle 1 should use 'MaleHeroHair1MoveDown'");
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
            Assert.AreEqual("MaleHeroHair2MoveDown", defaultAnim, 
                "Hairstyle 2 should use 'MaleHeroHair2MoveDown'");
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
            Assert.AreEqual("MaleHeroHair3MoveDown", defaultAnim, 
                "Hairstyle 3 should use 'MaleHeroHair3MoveDown'");
        }

        [TestMethod]
        public void HeroHairAnimationComponent_AllDirections_ShouldHaveCorrectSuffix()
        {
            // Test hairstyle 2 for all animation directions
            var component = new HeroHairAnimationComponent(Color.White, hairstyleIndex: 2);
            Assert.IsNotNull(component);

            var animProperties = new[]
            {
                ("AnimDown", "MaleHeroHair2MoveDown"),
                ("AnimUp", "MaleHeroHair2MoveUp"),
                ("AnimRight", "MaleHeroHair2MoveRight"),
                ("AnimLeft", "MaleHeroHair2MoveRight"), // Left uses Right (flipped)
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
            Assert.AreEqual("MaleHeroHair1MoveDown", defaultAnim, 
                "Default constructor should use hairstyle 1");
        }

        [TestMethod]
        public void HeroHairAnimationComponent_AllHairstyles_ShouldBeValid()
        {
            // Test that all hairstyles from 1 to HeroHairstyleCount can be created
            for (int i = 1; i <= GameConfig.HeroHairstyleCount; i++)
            {
                var component = new HeroHairAnimationComponent(Color.White, hairstyleIndex: i);
                Assert.IsNotNull(component, $"Should be able to create component with hairstyle {i}");

                var defaultAnimProperty = typeof(HeroHairAnimationComponent)
                    .GetProperty("DefaultAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(defaultAnimProperty, "DefaultAnimation property should exist");
                var defaultAnim = defaultAnimProperty!.GetValue(component) as string;

                string expectedAnimation = $"MaleHeroHair{i}MoveDown";
                Assert.AreEqual(expectedAnimation, defaultAnim,
                    $"Hairstyle {i} should use animation '{expectedAnimation}'");
            }
        }
    }
}
