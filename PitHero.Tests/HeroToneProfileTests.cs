using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.ECS.Components;
using RolePlayingFramework;
using System;

namespace PitHero.Tests
{
    /// <summary>Tone tints made from the chosen color (issue #462).</summary>
    [TestClass]
    public class HeroToneProfileTests
    {
        private static void AssertNear(Color expected, Color actual, int tolerance, string what)
        {
            Assert.IsTrue(Math.Abs(expected.R - actual.R) <= tolerance && Math.Abs(expected.G - actual.G) <= tolerance
                && Math.Abs(expected.B - actual.B) <= tolerance, $"{what}: expected {expected}, got {actual}");
        }

        private static Color Hex(uint rgb) => new Color((int)(rgb >> 16) & 255, (int)(rgb >> 8) & 255, (int)rgb & 255);

        [TestMethod]
        public void Midtone_ShouldBeTheChosenColor()
        {
            var chosen = new Color(20, 80, 201);
            Assert.AreEqual(chosen, HeroToneProfiles.Tint(Gender.Male, HeroLayer.Hair2, HeroTone.Midtone, chosen));
            Assert.AreEqual(chosen, HeroToneProfiles.Tint(Gender.Female, HeroLayer.Eyes, HeroTone.Midtone, chosen));
        }

        [TestMethod]
        public void ArtMidtone_ShouldReproduceThePaintedKeyColors()
        {
            // Clothing still uses the painted offsets: its own Midtone key gives back the artist's Shadow key
            AssertNear(Hex(0x5b6ee1), HeroToneProfiles.Tint(Gender.Male, HeroLayer.Clothing, HeroTone.Shadow, Hex(0x639bff)), 1, "Shirt shadow");
            AssertNear(Hex(0x663931), HeroToneProfiles.Tint(Gender.Female, HeroLayer.Clothing, HeroTone.Shadow, Hex(0x8f563b)), 1, "Skirt shadow");
        }

        [TestMethod]
        public void LargeHueShift_ShouldBeCapped()
        {
            // A tone painted 64 degrees from its midtone keeps at most the cap on other colors
            var chosen = new Color(20, 80, 201);
            HeroToneProfile.RgbToHsv(chosen, out float h0, out _, out _);
            HeroToneProfile.RgbToHsv(new HeroToneProfile(-64.45f, 0.8791f, 0.7095f).Apply(chosen), out float h1, out _, out _);
            float shift = (h1 - h0 + 540f) % 360f - 180f;
            Assert.AreEqual(-HeroToneProfile.MaxHueShift, shift, 1f);
        }

        [TestMethod]
        public void Light_ShouldIgnoreTheChosenColor()
        {
            Assert.AreEqual(Color.White, HeroToneProfiles.Tint(Gender.Male, HeroLayer.Hair1, HeroTone.Light, Color.Red));
        }

        [TestMethod]
        public void GrayChosenColor_ShouldStayGray()
        {
            var tint = HeroToneProfiles.Tint(Gender.Female, HeroLayer.Hair2, HeroTone.Shadow, new Color(50, 50, 50));
            Assert.AreEqual(tint.R, tint.G);
            Assert.AreEqual(tint.G, tint.B);
            Assert.IsTrue(tint.R < 50, "A shadow should be darker than its midtone");
        }

        [TestMethod]
        public void HighlightPastFullBrightness_ShouldBeWhite()
        {
            // Too light for a full highlight step: the highlight is pure white
            var chosen = new Color(255, 238, 88);
            var tint = HeroToneProfiles.Tint(Gender.Female, HeroLayer.Hair4, HeroTone.Highlight, chosen);
            Assert.AreEqual(Color.White, tint);
        }

        [TestMethod]
        public void HairPresets_ShouldAllGetVisibleContrast()
        {
            // Every hair preset and hairstyle: the shadow is clearly darker and the highlight lighter or paler
            var layers = new[] { HeroLayer.Hair1, HeroLayer.Hair2, HeroLayer.Hair3, HeroLayer.Hair4 };
            for (int i = 0; i < GameConfig.HairColors.Count; i++)
            {
                var mid = GameConfig.HairColors[i];
                HairToneRamp.ToOklab(mid, out double lMid, out double aMid, out double bMid);
                for (int g = 0; g < 2; g++)
                {
                    var gender = g == 0 ? Gender.Male : Gender.Female;
                    for (int j = 0; j < layers.Length; j++)
                    {
                        HairToneRamp.ToOklab(HeroToneProfiles.Tint(gender, layers[j], HeroTone.Shadow, mid), out double lS, out _, out _);
                        HairToneRamp.ToOklab(HeroToneProfiles.Tint(gender, layers[j], HeroTone.Highlight, mid), out double lH, out double aH, out double bH);
                        Assert.IsTrue(lMid - lS >= 0.1, $"Preset {i} {layers[j]}: shadow too close ({lMid - lS:F3})");
                        double chromaDrop = Math.Sqrt(aMid * aMid + bMid * bMid) - Math.Sqrt(aH * aH + bH * bH);
                        Assert.IsTrue(lH - lMid >= 0.12 || (lH - lMid >= 0.03 && chromaDrop >= 0.05),
                            $"Preset {i} {layers[j]}: highlight too close (dL {lH - lMid:F3}, dC {chromaDrop:F3})");
                    }
                }
            }
        }

        [TestMethod]
        public void HairPresets_ShouldMatchTheApprovedRamps()
        {
            // The ramps shown in the hair contrast sheets (shadow | chosen | highlight)
            AssertNear(Hex(0x190075), HeroToneProfiles.Tint(Gender.Male, HeroLayer.Hair1, HeroTone.Shadow, Hex(0x1450c9)), 1, "blue shadow");
            AssertNear(Hex(0x2082e9), HeroToneProfiles.Tint(Gender.Male, HeroLayer.Hair1, HeroTone.Highlight, Hex(0x1450c9)), 1, "blue highlight");
            AssertNear(Hex(0xb49000), HeroToneProfiles.Tint(Gender.Female, HeroLayer.Hair3, HeroTone.Shadow, Hex(0xffee58)), 1, "yellow shadow");
            AssertNear(Hex(0xffffff), HeroToneProfiles.Tint(Gender.Female, HeroLayer.Hair3, HeroTone.Highlight, Hex(0xffee58)), 1, "yellow highlight");
            AssertNear(Hex(0x320b0e), HeroToneProfiles.Tint(Gender.Male, HeroLayer.Hair4, HeroTone.Shadow, Hex(0x5e332b)), 1, "brown shadow");
            AssertNear(Hex(0x8d6456), HeroToneProfiles.Tint(Gender.Male, HeroLayer.Hair4, HeroTone.Highlight, Hex(0x5e332b)), 1, "brown highlight");
            AssertNear(Hex(0x141414), HeroToneProfiles.Tint(Gender.Female, HeroLayer.Hair2, HeroTone.Shadow, Hex(0x323232)), 1, "black shadow");
            AssertNear(Hex(0x6b6b6b), HeroToneProfiles.Tint(Gender.Female, HeroLayer.Hair2, HeroTone.Highlight, Hex(0x323232)), 1, "black highlight");
        }

        [TestMethod]
        public void SkinPresets_ShouldMatchTheApprovedShadows()
        {
            // The skin sheets the owner approved (chosen -> shadow); Head and female Body share the ramp
            var expected = new[] { 0xd99f8bu, 0x8f4d2cu, 0x2ea95au, 0x7a7fd6u, 0xc12d0cu };
            Assert.AreEqual(expected.Length, GameConfig.SkinColors.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                var skin = GameConfig.SkinColors[i];
                AssertNear(Hex(expected[i]), HeroToneProfiles.Tint(Gender.Male, HeroLayer.Head, HeroTone.Shadow, skin), 1, $"skin {i + 1} male head");
                AssertNear(Hex(expected[i]), HeroToneProfiles.Tint(Gender.Female, HeroLayer.Head, HeroTone.Shadow, skin), 1, $"skin {i + 1} female head");
                AssertNear(Hex(expected[i]), HeroToneProfiles.Tint(Gender.Female, HeroLayer.Body, HeroTone.Shadow, skin), 1, $"skin {i + 1} female body");
            }
        }

        [TestMethod]
        public void SkinShadow_ShouldBeGentlerThanHair()
        {
            // Every skin preset: visibly darker, but a smaller step than hair so faces don't read as masks
            for (int i = 0; i < GameConfig.SkinColors.Count; i++)
            {
                var skin = GameConfig.SkinColors[i];
                HairToneRamp.ToOklab(skin, out double lMid, out _, out _);
                HairToneRamp.ToOklab(HeroToneProfiles.Tint(Gender.Female, HeroLayer.Head, HeroTone.Shadow, skin), out double lS, out _, out _);
                double step = lMid - lS;
                Assert.IsTrue(step >= 0.08 && step < HairToneRamp.ShadowStep, $"skin {i + 1}: shadow step {step:F3}");
            }
        }

        [TestMethod]
        public void HsvRoundTrip_ShouldKeepTheColor()
        {
            var colors = new[] { Hex(0xac3232), Hex(0x37946e), Hex(0x5fcde4), Hex(0xfbf236), Color.Black, Color.White };
            for (int i = 0; i < colors.Length; i++)
            {
                HeroToneProfile.RgbToHsv(colors[i], out float h, out float s, out float v);
                Assert.AreEqual(colors[i], HeroToneProfile.HsvToRgb(h, s, v, 255));
            }
        }
    }
}
