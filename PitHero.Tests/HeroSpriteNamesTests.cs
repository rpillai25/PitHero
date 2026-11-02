using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.ECS.Components;
using RolePlayingFramework;

namespace PitHero.Tests
{
    [TestClass]
    public class HeroSpriteNamesTests
    {
        private static readonly HeroLayer[] Layers =
        {
            HeroLayer.Body, HeroLayer.Clothing, HeroLayer.Head, HeroLayer.EyesBase, HeroLayer.Eyes,
            HeroLayer.Hair1, HeroLayer.Hair2, HeroLayer.Hair3, HeroLayer.Hair4, HeroLayer.Armor
        };
        private static readonly Gender[] Genders = { Gender.Male, Gender.Female };

        private static string FindAtlasPath()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null)
            {
                var path = Path.Combine(dir.FullName, "PitHero", "Content", "Atlases", "Actors.atlas");
                if (File.Exists(path))
                    return path;
                dir = dir.Parent;
            }
            Assert.Fail("Actors.atlas not found above the test output folder");
            return null;
        }

        private static HashSet<string> AtlasNames()
        {
            var names = new HashSet<string>();
            var lines = File.ReadAllLines(FindAtlasPath());
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length > 0 && lines[i][0] != '\t')
                    names.Add(lines[i]);
            }
            return names;
        }

        [TestMethod]
        public void EveryName_ShouldExistInActorsAtlas()
        {
            var atlas = AtlasNames();
            for (int g = 0; g < Genders.Length; g++)
            {
                for (int l = 0; l < Layers.Length; l++)
                {
                    if (Layers[l] == HeroLayer.Armor && !HeroSpriteNames.HasArmorLayer(Genders[g]))
                        continue;
                    var names = HeroSpriteNames.Get(Genders[g], Layers[l]);
                    var all = new[] { names.Down, names.Right, names.Up, names.Jump, names.SleepDown, names.SleepRight, names.PreviewSprite };
                    for (int n = 0; n < all.Length; n++)
                        Assert.IsTrue(atlas.Contains(all[n]), $"{all[n]} should be in Actors.atlas");
                }
            }
        }

        [TestMethod]
        public void Clothing_ShouldBeShirtForMaleAndSkirtForFemale()
        {
            Assert.AreEqual("MaleHeroShirtMoveDown", HeroSpriteNames.Get(Gender.Male, HeroLayer.Clothing).Down);
            Assert.AreEqual("FemaleHeroSkirtMoveDown", HeroSpriteNames.Get(Gender.Female, HeroLayer.Clothing).Down);
        }

        [TestMethod]
        public void SleepNames_ShouldOnlyDifferForEyeLayers()
        {
            Assert.AreEqual("MaleHeroEyesBaseSleepDown", HeroSpriteNames.Get(Gender.Male, HeroLayer.EyesBase).SleepDown);
            Assert.AreEqual("FemaleHeroEyesSleepRight", HeroSpriteNames.Get(Gender.Female, HeroLayer.Eyes).SleepRight);
            Assert.AreEqual("MaleHeroBodyMoveDown", HeroSpriteNames.Get(Gender.Male, HeroLayer.Body).SleepDown);
        }

        [TestMethod]
        public void BodyTint_ShouldBeSkinForFemaleAndWhiteForMale()
        {
            Assert.AreEqual(Color.Tan, HeroSpriteNames.BodyTint(Gender.Female, Color.Tan));
            Assert.AreEqual(Color.White, HeroSpriteNames.BodyTint(Gender.Male, Color.Tan));
        }

        [TestMethod]
        public void Armor_ShouldOnlyExistForFemale()
        {
            var atlas = AtlasNames();
            Assert.IsTrue(HeroSpriteNames.HasArmorLayer(Gender.Female));
            Assert.IsFalse(HeroSpriteNames.HasArmorLayer(Gender.Male));
            Assert.IsTrue(atlas.Contains("FemaleHeroArmorMoveDown"));
            Assert.IsFalse(atlas.Contains("MaleHeroArmorMoveDown"));
        }

        [TestMethod]
        public void HairLayer_ShouldClampHairstylesThatNoLongerExist()
        {
            Assert.AreEqual(HeroLayer.Hair4, HeroSpriteNames.HairLayer(5));
            Assert.AreEqual(HeroLayer.Hair1, HeroSpriteNames.HairLayer(0));
            Assert.AreEqual(4, GameConfig.ClampHairstyleIndex(5));
            Assert.AreEqual(3, GameConfig.ClampHairstyleIndex(3));
        }

        [TestMethod]
        public void HairComponent_ShouldUseFemaleFrames()
        {
            var component = new HeroHairAnimationComponent(Color.White, 2, Gender.Female);
            var property = typeof(HeroHairAnimationComponent)
                .GetProperty("DefaultAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.AreEqual("FemaleHeroHair2MoveDown", property.GetValue(component) as string);
        }
    }
}
