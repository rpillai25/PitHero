#if CRYSTAL_UI_FEATURE
// This file compiles only when CRYSTAL_UI_FEATURE is defined.
// The Principal Engineer should define this constant (in PitHero.Tests.csproj
// DefineConstants) once CrystalColorUtil is implemented.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using RolePlayingFramework.Heroes;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the CrystalColorUtil static helper class.
    /// Expected API:
    ///   CrystalColorUtil.CombineColors(Color a, Color b) : Color
    ///   CrystalColorUtil.ToHSV(Color rgb, out float h, out float s, out float v)
    ///   CrystalColorUtil.FromHSV(float h, float s, float v) : Color
    ///
    /// These tests verify Scenario 3 (forge combo crystal color) from the
    /// Hero Crystal UI feature specification.
    /// </summary>
    [TestClass]
    public class CrystalColorUtilTests
    {
        // ── CombineColors ─────────────────────────────────────────────────────

        [TestMethod]
        public void CrystalColorUtil_CombineColors_SameColor_ReturnsSameColor()
        {
            // Combining a color with itself should return approximately the same color
            var red = new Color(255, 0, 0);
            var result = CrystalColorUtil.CombineColors(red, red);
            Assert.AreEqual(red.R, result.R, 5, "R channel should be preserved");
            Assert.AreEqual(red.G, result.G, 5, "G channel should be preserved");
            Assert.AreEqual(red.B, result.B, 5, "B channel should be preserved");
        }

        [TestMethod]
        public void CrystalColorUtil_CombineColors_RedAndBlue_ProducesMidtonePurple()
        {
            // Knight = Green (example), Mage = Red (example) per HERO_CRYSTAL_TAB_UI_MOCKUP
            // Here we test generic HSV blend
            var red = new Color(255, 0, 0);
            var blue = new Color(0, 0, 255);
            var result = CrystalColorUtil.CombineColors(red, blue);

            // The combined color should have some red and blue, resulting in purple range
            Assert.IsTrue(result.R > 0, "Combined color should have red component");
            Assert.IsTrue(result.B > 0, "Combined color should have blue component");
        }

        [TestMethod]
        public void CrystalColorUtil_CombineColors_IsCommutative()
        {
            var green = new Color(0, 255, 0);
            var red = new Color(255, 0, 0);

            var ab = CrystalColorUtil.CombineColors(green, red);
            var ba = CrystalColorUtil.CombineColors(red, green);

            // HSV blend should be commutative (average hue)
            Assert.AreEqual(ab.R, ba.R, 5, "CombineColors should be commutative on R");
            Assert.AreEqual(ab.G, ba.G, 5, "CombineColors should be commutative on G");
            Assert.AreEqual(ab.B, ba.B, 5, "CombineColors should be commutative on B");
        }

        [TestMethod]
        public void CrystalColorUtil_CombineColors_AlphaIsPreserved()
        {
            var a = new Color(255, 0, 0, 255);
            var b = new Color(0, 255, 0, 255);
            var result = CrystalColorUtil.CombineColors(a, b);
            Assert.AreEqual(255, result.A, "Alpha channel should remain fully opaque");
        }

        // ── ToHSV / FromHSV roundtrip ─────────────────────────────────────────

        [TestMethod]
        public void CrystalColorUtil_ToHSV_PureRed_ReturnsHue0()
        {
            var red = new Color(255, 0, 0);
            CrystalColorUtil.ToHSV(red, out float h, out float s, out float v);
            Assert.AreEqual(0f, h, 1f, "Pure red should have hue ~0 degrees");
            Assert.AreEqual(1f, s, 0.05f, "Pure red should have full saturation");
            Assert.AreEqual(1f, v, 0.05f, "Pure red should have full value");
        }

        [TestMethod]
        public void CrystalColorUtil_ToHSV_PureGreen_ReturnsHue120()
        {
            var green = new Color(0, 255, 0);
            CrystalColorUtil.ToHSV(green, out float h, out float s, out float v);
            Assert.AreEqual(120f, h, 2f, "Pure green should have hue ~120 degrees");
        }

        [TestMethod]
        public void CrystalColorUtil_ToHSV_PureBlue_ReturnsHue240()
        {
            var blue = new Color(0, 0, 255);
            CrystalColorUtil.ToHSV(blue, out float h, out float s, out float v);
            Assert.AreEqual(240f, h, 2f, "Pure blue should have hue ~240 degrees");
        }

        [TestMethod]
        public void CrystalColorUtil_FromHSV_RoundTrip_PreservesColor()
        {
            var original = new Color(200, 100, 50);
            CrystalColorUtil.ToHSV(original, out float h, out float s, out float v);
            var restored = CrystalColorUtil.FromHSV(h, s, v);

            Assert.AreEqual(original.R, restored.R, 3, "R channel roundtrip within tolerance");
            Assert.AreEqual(original.G, restored.G, 3, "G channel roundtrip within tolerance");
            Assert.AreEqual(original.B, restored.B, 3, "B channel roundtrip within tolerance");
        }

        [TestMethod]
        public void CrystalColorUtil_FromHSV_White_IsWhite()
        {
            // H doesn't matter for achromatic colors; S=0 V=1 => white
            var white = CrystalColorUtil.FromHSV(0f, 0f, 1f);
            Assert.AreEqual(255, white.R, 3);
            Assert.AreEqual(255, white.G, 3);
            Assert.AreEqual(255, white.B, 3);
        }

        [TestMethod]
        public void CrystalColorUtil_FromHSV_Black_IsBlack()
        {
            var black = CrystalColorUtil.FromHSV(0f, 0f, 0f);
            Assert.AreEqual(0, black.R, 3);
            Assert.AreEqual(0, black.G, 3);
            Assert.AreEqual(0, black.B, 3);
        }
    }
}
#endif
