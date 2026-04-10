using Microsoft.Xna.Framework;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using System;

namespace RolePlayingFramework.Heroes
{
    /// <summary>Static utility for crystal colors based on job types.</summary>
    public static class CrystalColorUtil
    {
        /// <summary>Returns the canonical color for a given job type.</summary>
        public static Color GetJobColor(IJob job)
        {
            if (job is CompositeJob)
            {
                return Color.White;
            }

            if (job is Knight) return new Color(0, 255, 0);      // Green
            if (job is Mage) return new Color(255, 0, 0);        // Red
            if (job is Priest) return new Color(0, 0, 255);      // Blue
            if (job is Thief) return new Color(255, 255, 0);     // Yellow
            if (job is Monk) return new Color(0, 255, 255);      // Cyan
            if (job is Archer) return new Color(255, 0, 255);    // Magenta

            return Color.White;
        }

        /// <summary>Blends two colors using HSV: lerp hue, max saturation, max value.</summary>
        public static Color CombineColors(Color a, Color b)
        {
            var (hA, sA, vA) = RgbToHsv(a);
            var (hB, sB, vB) = RgbToHsv(b);

            var blendedH = LerpHue(hA, hB, 0.5f);
            var blendedS = Math.Max(sA, sB);
            var blendedV = Math.Max(vA, vB);

            return HsvToRgb(blendedH, blendedS, blendedV);
        }

        /// <summary>Converts RGB color to HSV color space.</summary>
        /// <summary>Converts RGB color to HSV components via out parameters.</summary>
        public static void ToHSV(Color c, out float h, out float s, out float v)
        {
            var result = RgbToHsv(c);
            h = result.h;
            s = result.s;
            v = result.v;
        }

        private static (float h, float s, float v) RgbToHsv(Color c)
        {
            var r = c.R / 255f;
            var g = c.G / 255f;
            var b = c.B / 255f;

            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var delta = max - min;

            var h = 0f;
            if (delta != 0f)
            {
                if (max == r)
                    h = 60f * (((g - b) / delta) % 6f);
                else if (max == g)
                    h = 60f * (((b - r) / delta) + 2f);
                else
                    h = 60f * (((r - g) / delta) + 4f);
            }

            if (h < 0f) h += 360f;

            var s = max == 0f ? 0f : delta / max;
            var v = max;

            return (h, s, v);
        }

        /// <summary>Converts HSV components to a Color.</summary>
        public static Color FromHSV(float h, float s, float v) => HsvToRgb(h, s, v);

        /// <summary>Converts HSV color to RGB color.</summary>
        private static Color HsvToRgb(float h, float s, float v)
        {
            var c = v * s;
            var x = c * (1f - Math.Abs(((h / 60f) % 2f) - 1f));
            var m = v - c;

            float r = 0f, g = 0f, b = 0f;

            if (h < 60f)
            {
                r = c; g = x; b = 0f;
            }
            else if (h < 120f)
            {
                r = x; g = c; b = 0f;
            }
            else if (h < 180f)
            {
                r = 0f; g = c; b = x;
            }
            else if (h < 240f)
            {
                r = 0f; g = x; b = c;
            }
            else if (h < 300f)
            {
                r = x; g = 0f; b = c;
            }
            else
            {
                r = c; g = 0f; b = x;
            }

            var red = (byte)((r + m) * 255f);
            var green = (byte)((g + m) * 255f);
            var blue = (byte)((b + m) * 255f);

            return new Color(red, green, blue, 255);
        }

        /// <summary>Performs circular interpolation on hue values.</summary>
        private static float LerpHue(float hA, float hB, float t)
        {
            var diff = Math.Abs(hA - hB);
            if (diff > 180f)
            {
                if (hA < hB)
                    hA += 360f;
                else
                    hB += 360f;
            }

            var result = hA + (hB - hA) * t;
            if (result >= 360f) result -= 360f;
            if (result < 0f) result += 360f;

            return result;
        }
    }
}
