using Microsoft.Xna.Framework;
using RolePlayingFramework;
using System;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// The tone layers a tintable paperdoll part is split into (issue #462). Each tone is its own gray atlas
    /// sprite tinted with its own color, so shadows and highlights keep the hue shifts of the original art
    /// instead of all taking the one chosen color. Parts that are not split draw as a single Midtone.
    /// </summary>
    public enum HeroTone
    {
        Shadow = 0,
        Midtone = 1,
        Highlight = 2,
        /// <summary>Light hitting the part (white glints, hair ties): drawn in its painted colors, never tinted.</summary>
        Light = 3
    }

    /// <summary>
    /// How one tone's tint is made from the player's chosen color: the chosen color plays the Midtone, and
    /// each other tone moves it by the HSV offset the artist used between that tone and the Midtone.
    /// </summary>
    public readonly struct HeroToneProfile
    {
        /// <summary>Degrees added to the hue.</summary>
        public readonly float HueShift;
        /// <summary>Multiplier on saturation.</summary>
        public readonly float SaturationScale;
        /// <summary>Multiplier on value (HSV brightness).</summary>
        public readonly float ValueScale;
        /// <summary>When true the tone ignores the chosen color and draws in its painted colors.</summary>
        public readonly bool Untinted;
        /// <summary>When not None, the tone is made by <see cref="HairToneRamp"/> or <see cref="SkinToneRamp"/> and the HSV fields are unused.</summary>
        public readonly ToneRamp Ramp;

        public HeroToneProfile(float hueShift, float saturationScale, float valueScale, bool untinted = false)
        {
            HueShift = hueShift;
            SaturationScale = saturationScale;
            ValueScale = valueScale;
            Untinted = untinted;
            Ramp = ToneRamp.None;
        }

        private HeroToneProfile(ToneRamp ramp)
        {
            HueShift = 0f;
            SaturationScale = 1f;
            ValueScale = 1f;
            Untinted = false;
            Ramp = ramp;
        }

        /// <summary>Hair shadows: a fixed lightness step down from the chosen color (see <see cref="HairToneRamp"/>).</summary>
        public static readonly HeroToneProfile HairShadow = new HeroToneProfile(ToneRamp.HairShadow);

        /// <summary>Hair highlights: a fixed lightness step up from the chosen color (see <see cref="HairToneRamp"/>).</summary>
        public static readonly HeroToneProfile HairHighlight = new HeroToneProfile(ToneRamp.HairHighlight);

        /// <summary>Skin shadows: a gentle fixed lightness step down from the chosen color (see <see cref="SkinToneRamp"/>).</summary>
        public static readonly HeroToneProfile SkinShadow = new HeroToneProfile(ToneRamp.SkinShadow);

        /// <summary>
        /// Largest hue shift a tone keeps, in degrees. The artist's shift only suits the color it was painted for:
        /// green hair's 64° shift toward yellow-green turns blue hair's outline green, so larger shifts are capped.
        /// </summary>
        public const float MaxHueShift = 15f;

        /// <summary>The Midtone (and every unsplit part): the chosen color unchanged.</summary>
        public static readonly HeroToneProfile Identity = new HeroToneProfile(0f, 1f, 1f);

        /// <summary>The Light tone: painted colors, only faded with the chosen color's alpha.</summary>
        public static readonly HeroToneProfile Light = new HeroToneProfile(0f, 1f, 1f, untinted: true);

        /// <summary>
        /// The tint for this tone given the chosen color. A value pushed past full brightness is spent as
        /// less saturation instead, so a highlight on an already bright color still reads lighter.
        /// Alpha is kept; colors are premultiplied, so an untinted tone fades to a gray of the alpha.
        /// </summary>
        public Color Apply(Color chosen)
        {
            if (Untinted)
                return new Color(chosen.A, chosen.A, chosen.A, chosen.A);
            if (Ramp == ToneRamp.SkinShadow)
                return SkinToneRamp.Shadow(chosen);
            if (Ramp != ToneRamp.None)
                return HairToneRamp.Apply(Ramp, chosen);
            if (HueShift == 0f && SaturationScale == 1f && ValueScale == 1f)
                return chosen;

            RgbToHsv(chosen, out float h, out float s, out float v);
            h += Math.Clamp(HueShift, -MaxHueShift, MaxHueShift);
            s *= SaturationScale;
            v *= ValueScale;
            if (v > 1f)
            {
                s /= v;
                v = 1f;
            }
            s = Math.Clamp(s, 0f, 1f);
            return HsvToRgb(h, s, v, chosen.A);
        }

        /// <summary>Hue in degrees [0, 360), saturation and value in [0, 1].</summary>
        public static void RgbToHsv(Color c, out float h, out float s, out float v)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float d = max - min;
            h = 0f;
            if (d > 0f)
            {
                if (max == r) h = 60f * (((g - b) / d) % 6f);
                else if (max == g) h = 60f * ((b - r) / d + 2f);
                else h = 60f * ((r - g) / d + 4f);
                if (h < 0f) h += 360f;
            }
            s = max > 0f ? d / max : 0f;
            v = max;
        }

        /// <summary>Converts HSV (hue in degrees, any range) back to a color with the given alpha.</summary>
        public static Color HsvToRgb(float h, float s, float v, byte alpha)
        {
            h %= 360f;
            if (h < 0f) h += 360f;
            float c = v * s;
            float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
            float m = v - c;
            float r, g, b;
            if (h < 60f) { r = c; g = x; b = 0f; }
            else if (h < 120f) { r = x; g = c; b = 0f; }
            else if (h < 180f) { r = 0f; g = c; b = x; }
            else if (h < 240f) { r = 0f; g = x; b = c; }
            else if (h < 300f) { r = x; g = 0f; b = c; }
            else { r = c; g = 0f; b = x; }
            return new Color(ToByte(r + m), ToByte(g + m), ToByte(b + m), alpha);
        }

        private static int ToByte(float f) => (int)Math.Round(Math.Clamp(f, 0f, 1f) * 255f);
    }

    /// <summary>Which OKLab ramp tone a <see cref="HeroToneProfile"/> makes, if any.</summary>
    public enum ToneRamp : byte
    {
        None = 0,
        HairShadow = 1,
        HairHighlight = 2,
        SkinShadow = 3
    }

    /// <summary>
    /// Skin shadows made in OKLab: a gentle fixed lightness step down, a little more chroma and the hue turned
    /// slightly toward warm orange. The art's measured offsets doubled saturation, which turned pale blue skin's
    /// shadow into a dark royal-blue mask and left already saturated skins with almost no contrast.
    /// </summary>
    public static class SkinToneRamp
    {
        /// <summary>OKLab lightness the shadow sits below the midtone.</summary>
        public const double ShadowStep = 0.12;
        /// <summary>Multiplier on the shadow's chroma.</summary>
        public const double ShadowChroma = 1.15;
        /// <summary>Hue (OKLCH degrees) the shadow turns toward: warm orange.</summary>
        public const double ShadowHueTarget = 40.0;
        /// <summary>Most the shadow's hue turns, in degrees.</summary>
        public const double ShadowHueTurn = 8.0;
        /// <summary>Darkest the shadow goes (or 70% of a darker midtone's lightness).</summary>
        public const double ShadowFloor = 0.25;
        /// <summary>
        /// Extra shadow step for strongly colored skins (green, blue, red), reached at <see cref="SaturatedChromaFull"/>.
        /// Their presets are lighter, so the shadow lands where it was and the contrast grows; natural skins get none.
        /// </summary>
        public const double SaturatedExtraStep = 0.05;
        /// <summary>OKLab chroma where the extra step starts (natural skins sit below it).</summary>
        public const double SaturatedChromaStart = 0.10;
        /// <summary>OKLab chroma where the extra step is full.</summary>
        public const double SaturatedChromaFull = 0.125;

        /// <summary>The skin Shadow tint for the chosen (Midtone) skin color. Alpha is kept.</summary>
        public static Color Shadow(Color chosen)
        {
            HairToneRamp.ToOklab(chosen, out double L, out double a, out double b);
            double chroma = Math.Sqrt(a * a + b * b);
            double hue = Math.Atan2(b, a);
            double saturated = Math.Clamp((chroma - SaturatedChromaStart) / (SaturatedChromaFull - SaturatedChromaStart), 0.0, 1.0);
            double lightness = Math.Max(Math.Min(ShadowFloor, L * 0.7), L - ShadowStep - saturated * SaturatedExtraStep);
            if (chosen.R == chosen.G && chosen.G == chosen.B)
                chroma = 0.0;
            return HairToneRamp.FromOklch(lightness, chroma * ShadowChroma, HairToneRamp.Toward(hue, ShadowHueTarget, ShadowHueTurn), chosen.A);
        }
    }

    /// <summary>
    /// Hair tones made in OKLab, so every hair color gets the same visible contrast. The art's measured HSV offsets
    /// left some presets flat: a highlight at value ×0.99 is no lighter than the midtone, and a dark color's
    /// shadow barely darkens. Here the shadow is a fixed perceptual lightness step down with its hue turned toward
    /// purple, and the highlight a step up with its hue turned toward yellow and a little less chroma. Hair does
    /// not stay inside DB32. A color too light for a full highlight gets a pure white highlight and a deeper shadow;
    /// one too dark for a full shadow keeps a floor and gets a brighter highlight.
    /// </summary>
    public static class HairToneRamp
    {
        /// <summary>OKLab lightness the shadow sits below the midtone.</summary>
        public const double ShadowStep = 0.21;
        /// <summary>OKLab lightness the highlight sits above the midtone.</summary>
        public const double HighlightStep = 0.13;
        /// <summary>Darkest a shadow goes, so it stays apart from the outline.</summary>
        public const double ShadowFloor = 0.22;
        /// <summary>Lightest a highlight goes.</summary>
        public const double HighlightCeiling = 0.97;
        /// <summary>Hue (OKLCH degrees) shadows turn toward: purple.</summary>
        public const double ShadowHueTarget = 300.0;
        /// <summary>Hue (OKLCH degrees) highlights turn toward: yellow.</summary>
        public const double HighlightHueTarget = 100.0;
        /// <summary>Most a shadow's hue turns, in degrees.</summary>
        public const double ShadowHueTurn = 12.0;
        /// <summary>Most a highlight's hue turns, in degrees.</summary>
        public const double HighlightHueTurn = 8.0;
        /// <summary>Multiplier on the highlight's chroma.</summary>
        public const double HighlightChroma = 0.9;

        /// <summary>The tint for a hair Shadow or Highlight made from the chosen (Midtone) color. Alpha is kept.</summary>
        public static Color Apply(ToneRamp tone, Color chosen)
        {
            ToOklab(chosen, out double L, out double a, out double b);
            double chroma = Math.Sqrt(a * a + b * b);
            double hue = Math.Atan2(b, a);
            bool gray = chosen.R == chosen.G && chosen.G == chosen.B;

            // A shadow that would fall below the floor gives the missing contrast to the highlight
            double deficit = Math.Max(0.0, ShadowFloor - (L - ShadowStep));
            double highlightStep = Math.Min(HighlightStep + deficit * 0.7, Math.Max(0.0, HighlightCeiling - L));
            // A highlight cut short by the ceiling gives the missing contrast to the shadow and to paleness
            double shortfall = Math.Max(0.0, HighlightStep - highlightStep);

            double lightness;
            if (tone == ToneRamp.HairShadow)
            {
                lightness = Math.Max(Math.Min(ShadowFloor, L * 0.6), L - ShadowStep - shortfall * 0.6);
                hue = Toward(hue, ShadowHueTarget, ShadowHueTurn);
            }
            else
            {
                // Too light for a full step (yellow): a pale tint read as flat, so the highlight is pure white
                if (shortfall > 0.0)
                    return new Color(chosen.A, chosen.A, chosen.A, chosen.A);
                lightness = L + highlightStep;
                chroma *= HighlightChroma;
                hue = Toward(hue, HighlightHueTarget, HighlightHueTurn);
            }
            if (gray)
                chroma = 0.0;
            return FromOklch(lightness, chroma, hue, chosen.A);
        }

        /// <summary>Turns a hue (radians) toward a target (degrees) the short way round, by at most the given degrees.</summary>
        internal static double Toward(double hue, double targetDegrees, double maxDegrees)
        {
            double target = targetDegrees * Math.PI / 180.0;
            double d = (target - hue + 3.0 * Math.PI) % (2.0 * Math.PI) - Math.PI;
            double step = maxDegrees * Math.PI / 180.0;
            if (Math.Abs(d) < step)
                return target;
            return hue + (d > 0.0 ? step : -step);
        }

        /// <summary>sRGB to OKLab (Björn Ottosson's matrices).</summary>
        public static void ToOklab(Color c, out double L, out double a, out double b)
        {
            double r = ToLinear(c.R), g = ToLinear(c.G), bl = ToLinear(c.B);
            double l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * bl);
            double m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * bl);
            double s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * bl);
            L = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s;
            a = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
            b = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;
        }

        /// <summary>OKLCH to a color, lowering chroma until the color fits in sRGB.</summary>
        internal static Color FromOklch(double L, double chroma, double hue, byte alpha)
        {
            double cos = Math.Cos(hue), sin = Math.Sin(hue);
            if (!InGamut(L, chroma * cos, chroma * sin, out double r, out double g, out double b))
            {
                double lo = 0.0, hi = chroma;
                for (int i = 0; i < 24; i++)
                {
                    double mid = (lo + hi) * 0.5;
                    if (InGamut(L, mid * cos, mid * sin, out _, out _, out _)) lo = mid;
                    else hi = mid;
                }
                InGamut(L, lo * cos, lo * sin, out r, out g, out b);
            }
            return new Color(ToByte(r), ToByte(g), ToByte(b), alpha);
        }

        private static bool InGamut(double L, double a, double b, out double r, out double g, out double bl)
        {
            double l = L + 0.3963377774 * a + 0.2158037573 * b;
            double m = L - 0.1055613458 * a - 0.0638541728 * b;
            double s = L - 0.0894841775 * a - 1.2914855480 * b;
            l = l * l * l;
            m = m * m * m;
            s = s * s * s;
            r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
            g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
            bl = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
            const double e = 1e-4;
            return r >= -e && r <= 1 + e && g >= -e && g <= 1 + e && bl >= -e && bl <= 1 + e;
        }

        private static double ToLinear(byte channel)
        {
            double u = channel / 255.0;
            return u <= 0.04045 ? u / 12.92 : Math.Pow((u + 0.055) / 1.055, 2.4);
        }

        private static int ToByte(double linear)
        {
            linear = Math.Clamp(linear, 0.0, 1.0);
            double u = linear <= 0.0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1.0 / 2.4) - 0.055;
            return (int)Math.Floor(Math.Clamp(u, 0.0, 1.0) * 255.0 + 0.5);
        }
    }

    /// <summary>
    /// Which tones each paperdoll part has in the Actors atlas, and the HSV offsets measured from the source
    /// art (Male2/Female2.aseprite). The numbers come from the `profile` lines printed by the aseprite-pixel-art
    /// skill's prep_for_game.lua: each tone's key color (its lightest common color) against the Midtone's.
    /// Picking a part's own Midtone key as the color reproduces the painted key colors, except where the
    /// hue shift is past <see cref="HeroToneProfile.MaxHueShift"/>. Hair uses <see cref="HairToneRamp"/> instead.
    /// </summary>
    public static class HeroToneProfiles
    {
        private static readonly HeroTone[] Single = { HeroTone.Midtone };
        private static readonly HeroTone[] ShadowMid = { HeroTone.Shadow, HeroTone.Midtone };
        private static readonly HeroTone[] Hair = { HeroTone.Shadow, HeroTone.Midtone, HeroTone.Highlight };
        private static readonly HeroTone[] HairWithLight = { HeroTone.Shadow, HeroTone.Midtone, HeroTone.Highlight, HeroTone.Light };

        /// <summary>The tones a part draws, back to front. Unsplit parts have just Midtone.</summary>
        public static HeroTone[] TonesOf(Gender gender, HeroLayer layer)
        {
            switch (layer)
            {
                case HeroLayer.Body: return gender == Gender.Female ? ShadowMid : Single;
                case HeroLayer.Clothing:
                case HeroLayer.Head: return ShadowMid;
                case HeroLayer.Hair1: return HairWithLight;
                case HeroLayer.Hair2:
                case HeroLayer.Hair3:
                case HeroLayer.Hair4: return Hair;
                default: return Single;
            }
        }

        /// <summary>The profile that turns the chosen color into this tone's tint.</summary>
        public static HeroToneProfile Get(Gender gender, HeroLayer layer, HeroTone tone)
        {
            switch (tone)
            {
                case HeroTone.Midtone: return HeroToneProfile.Identity;
                case HeroTone.Light: return HeroToneProfile.Light;
            }
            bool shadow = tone == HeroTone.Shadow;
            switch (layer)
            {
                case HeroLayer.Hair1:
                case HeroLayer.Hair2:
                case HeroLayer.Hair3:
                case HeroLayer.Hair4:
                    // Every hairstyle shares one contrast-guaranteed ramp; the art's own offsets left some colors flat
                    return shadow ? HeroToneProfile.HairShadow : HeroToneProfile.HairHighlight;
                case HeroLayer.Head:
                case HeroLayer.Body:
                    // Skin parts have just Shadow and Midtone; one gentle ramp suits every skin color
                    if (shadow) return HeroToneProfile.SkinShadow;
                    break;
            }
            if (gender == Gender.Female)
            {
                switch (layer)
                {
                    case HeroLayer.Clothing: return P(-10.23f, 0.8846f, 0.7133f);              // 663931 | 8f563b
                }
            }
            else
            {
                switch (layer)
                {
                    case HeroLayer.Clothing: return P(13.03f, 0.9735f, 0.8824f);               // 5b6ee1 | 639bff
                }
            }
            return HeroToneProfile.Identity;
        }

        /// <summary>The tint of one tone of a part for the chosen color.</summary>
        public static Color Tint(Gender gender, HeroLayer layer, HeroTone tone, Color chosen)
        {
            return Get(gender, layer, tone).Apply(chosen);
        }

        private static HeroToneProfile P(float hue, float sat, float val) => new HeroToneProfile(hue, sat, val);
    }
}
