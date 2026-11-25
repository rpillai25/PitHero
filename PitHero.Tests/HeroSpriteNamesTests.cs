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
                    var tones = HeroSpriteNames.Get(Genders[g], Layers[l]).Tones;
                    for (int t = 0; t < tones.Length; t++)
                    {
                        var names = tones[t];
                        var all = new[] { names.Down, names.Right, names.Up, names.Jump, names.SleepDown, names.SleepRight, names.PreviewSprite };
                        for (int n = 0; n < all.Length; n++)
                            Assert.IsTrue(atlas.Contains(all[n]), $"{all[n]} should be in Actors.atlas");
                    }
                }
            }
        }

        [TestMethod]
        public void Clothing_ShouldBeShirtForMaleAndSkirtForFemale()
        {
            Assert.AreEqual("MaleHeroShirtMidtoneMoveDown", HeroSpriteNames.Get(Gender.Male, HeroLayer.Clothing).Down);
            Assert.AreEqual("FemaleHeroSkirtMidtoneMoveDown", HeroSpriteNames.Get(Gender.Female, HeroLayer.Clothing).Down);
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
            Assert.AreEqual("FemaleHeroHair2MidtoneMoveDown", property.GetValue(component) as string);
        }

        [TestMethod]
        public void Tones_ShouldMatchTheSplitArt()
        {
            CollectionAssert.AreEqual(new[] { HeroTone.Shadow, HeroTone.Midtone, HeroTone.Highlight, HeroTone.Light },
                ToneList(Gender.Male, HeroLayer.Hair1));
            CollectionAssert.AreEqual(new[] { HeroTone.Shadow, HeroTone.Midtone, HeroTone.Highlight },
                ToneList(Gender.Female, HeroLayer.Hair3));
            CollectionAssert.AreEqual(new[] { HeroTone.Shadow, HeroTone.Midtone }, ToneList(Gender.Female, HeroLayer.Body));
            CollectionAssert.AreEqual(new[] { HeroTone.Midtone }, ToneList(Gender.Male, HeroLayer.Body));
            CollectionAssert.AreEqual(new[] { HeroTone.Midtone }, ToneList(Gender.Female, HeroLayer.Armor));
            Assert.AreEqual("MaleHeroEyesBaseMoveDown", HeroSpriteNames.Get(Gender.Male, HeroLayer.EyesBase).Down);
            Assert.AreEqual("male_hero_hair1_shadow_move_down_0", HeroSpriteNames.Get(Gender.Male, HeroLayer.Hair1).Tones[0].PreviewSprite);
        }

        [TestMethod]
        public void SplitPart_ShouldAnswerWithItsMidtoneNames()
        {
            var names = HeroSpriteNames.Get(Gender.Female, HeroLayer.Head);
            Assert.AreEqual(HeroTone.Midtone, names.Tone);
            Assert.AreEqual("FemaleHeroHeadMidtoneMoveDown", names.Down);
            Assert.IsTrue(System.Array.IndexOf(names.Tones, names) >= 0, "The part's own names should be one of its tones");
        }

        [TestMethod]
        public void HeroComponent_ShouldExposeOnePartPerTone()
        {
            var hair = new HeroHairAnimationComponent(Color.Red, 1, Gender.Male);
            Assert.AreEqual(4, hair.PartCount);
            Assert.AreEqual(HeroTone.Light, hair.GetTone(3));
            var eyesBase = new HeroEyesBaseAnimationComponent(Gender.Male);
            Assert.AreEqual(1, eyesBase.PartCount);
        }

        [TestMethod]
        public void MultiSpriteAnimator_ShouldListEveryTonePart()
        {
            var animator = new MultiSpriteAnimator(
                new HeroBodyAnimationComponent(Color.Tan, Gender.Female),
                new HeroArmorAnimationComponent(),
                new HeroHairAnimationComponent(Color.Red, 1, Gender.Female));
            Assert.AreEqual(2 + 1 + 4, animator.LayerCount);
        }

        private static HeroTone[] ToneList(Gender gender, HeroLayer layer)
        {
            var tones = HeroSpriteNames.Get(gender, layer).Tones;
            var list = new HeroTone[tones.Length];
            for (int i = 0; i < tones.Length; i++)
                list[i] = tones[i].Tone;
            return list;
        }
    }
}
