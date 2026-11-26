using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez.Persistence.Binary;
using PitHero.ECS.Components;
using PitHero.Services;
using RolePlayingFramework;
using RolePlayingFramework.Heroes;

namespace PitHero.Tests
{
    /// <summary>Player-picked Shadow / Highlight tones for skin, hair and clothing (issue #467).</summary>
    [TestClass]
    public class HeroCustomTonesTests
    {
        private static readonly Color Mid = new Color(40, 160, 90);
        private static readonly Color PickedShadow = new Color(10, 20, 200);
        private static readonly Color PickedHighlight = new Color(250, 240, 10);
        private static readonly HeroCustomTones Picked = new HeroCustomTones(PickedShadow, PickedHighlight);

        [TestMethod]
        public void Tint_CustomShadowAndHighlight_ReplaceTheRamp()
        {
            Assert.AreEqual(PickedShadow, HeroToneProfiles.Tint(Gender.Male, HeroLayer.Hair2, HeroTone.Shadow, Mid, in Picked));
            Assert.AreEqual(PickedHighlight, HeroToneProfiles.Tint(Gender.Female, HeroLayer.Hair3, HeroTone.Highlight, Mid, in Picked));
            Assert.AreEqual(PickedShadow, HeroToneProfiles.Tint(Gender.Female, HeroLayer.Head, HeroTone.Shadow, Mid, in Picked));
            Assert.AreEqual(PickedShadow, HeroToneProfiles.Tint(Gender.Male, HeroLayer.Clothing, HeroTone.Shadow, Mid, in Picked));
        }

        [TestMethod]
        public void Tint_CustomTones_LeaveMidtoneAndLightAlone()
        {
            Assert.AreEqual(Mid, HeroToneProfiles.Tint(Gender.Male, HeroLayer.Hair1, HeroTone.Midtone, Mid, in Picked));
            Assert.AreEqual(HeroToneProfiles.Tint(Gender.Male, HeroLayer.Hair1, HeroTone.Light, Mid),
                HeroToneProfiles.Tint(Gender.Male, HeroLayer.Hair1, HeroTone.Light, Mid, in Picked));
        }

        [TestMethod]
        public void Tint_UnsetCustomTones_FollowTheRamp()
        {
            var none = HeroCustomTones.None;
            Assert.AreEqual(HeroToneProfiles.Tint(Gender.Male, HeroLayer.Hair2, HeroTone.Shadow, Mid),
                HeroToneProfiles.Tint(Gender.Male, HeroLayer.Hair2, HeroTone.Shadow, Mid, in none));
            Assert.AreEqual(HeroToneProfiles.Tint(Gender.Female, HeroLayer.Clothing, HeroTone.Shadow, Mid),
                HeroToneProfiles.Tint(Gender.Female, HeroLayer.Clothing, HeroTone.Shadow, Mid, in none));
        }

        [TestMethod]
        public void Tint_CustomTones_KeepTheChosenAlpha()
        {
            // Death and battle fades lower only the layer color's alpha
            var faded = new Color(Mid.R, Mid.G, Mid.B, (byte)90);
            var tint = HeroToneProfiles.Tint(Gender.Male, HeroLayer.Hair2, HeroTone.Shadow, faded, in Picked);
            Assert.AreEqual(new Color(PickedShadow.R, PickedShadow.G, PickedShadow.B, (byte)90), tint);
        }

        [TestMethod]
        public void HairComponent_UsesItsCustomTones()
        {
            var hair = new HeroHairAnimationComponent(Mid, 2, Gender.Male);
            hair.CustomTones = Picked;
            for (int i = 0; i < hair.PartCount; i++)
            {
                var expected = hair.GetTone(i) == HeroTone.Shadow ? PickedShadow
                    : hair.GetTone(i) == HeroTone.Highlight ? PickedHighlight
                    : HeroToneProfiles.Tint(Gender.Male, HeroLayer.Hair2, hair.GetTone(i), Mid);
                Assert.AreEqual(expected, hair.ToneTint(i, Mid), $"tone {hair.GetTone(i)}");
            }
        }

        [TestMethod]
        public void SaveData_V46_CustomTones_RoundTrip()
        {
            var original = new SaveData { HeroName = "Painted", HeroId = 77 };
            original.HairTones = Picked;
            original.SkinTones = new HeroCustomTones(new Color(1, 2, 3), new Color(4, 5, 6));
            original.ShirtTones = HeroCustomTones.None;

            var loaded = RoundTrip(original, patchVersion: 0);

            Assert.IsTrue(loaded.HairTones.IsSet);
            Assert.AreEqual(PickedShadow, loaded.HairTones.Shadow);
            Assert.AreEqual(PickedHighlight, loaded.HairTones.Highlight);
            Assert.IsTrue(loaded.SkinTones.IsSet);
            Assert.AreEqual(new Color(1, 2, 3), loaded.SkinTones.Shadow);
            Assert.IsFalse(loaded.ShirtTones.IsSet);
            Assert.AreEqual(77, loaded.HeroId);
        }

        [TestMethod]
        public void SaveData_V45_File_ReadsWithoutCustomTones()
        {
            var original = new SaveData { HeroName = "Preset", HeroId = 78 };
            original.HairTones = Picked;

            var loaded = RoundTrip(original, patchVersion: 45);

            Assert.IsFalse(loaded.HairTones.IsSet, "a v45 file predates custom tones");
            Assert.IsFalse(loaded.SkinTones.IsSet);
            Assert.IsFalse(loaded.ShirtTones.IsSet);
            Assert.AreEqual("Preset", loaded.HeroName);
            Assert.AreEqual(78, loaded.HeroId);
        }

        [TestMethod]
        public void HeroDesign_CarriesCustomTones()
        {
            var design = new HeroDesign("Ann", Color.Tan, Mid, 1, Color.Blue, "Knight", Gender.Female, null,
                HeroCustomTones.None, Picked, HeroCustomTones.None);
            Assert.IsTrue(design.HairTones.Equals(Picked));
            Assert.IsFalse(design.SkinTones.IsSet);
            Assert.IsFalse(new HeroDesign("Bob", Color.Tan, Mid, 1, Color.Blue).HairTones.IsSet, "presets by default");
        }

        // Writes the save, optionally patches the header version down, and reads it back
        private static SaveData RoundTrip(SaveData original, int patchVersion)
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
                writer.Write(original);
            byte[] bytes = ms.ToArray();
            if (patchVersion > 0)
            {
                bytes[0] = (byte)patchVersion;
                bytes[1] = 0;
                bytes[2] = 0;
                bytes[3] = 0;
            }
            var loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(bytes)))
                rdr.ReadPersistableInto(loaded);
            return loaded;
        }
    }
}
