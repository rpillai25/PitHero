using Microsoft.Xna.Framework;

namespace PitHero.Util
{
    public class ColorUtil
    {
        public static void SubtractAlpha(ref Color color, byte val)
        {
            if (color.A - val >= 0)
                color.A -= val;
            else
                color.A = 0;
        }
        public static void SubtractRed(ref Color color, byte val)
        {
            if (color.R - val >= 0)
                color.R -= val;
            else
                color.R = 0;
        }
        public static void SubtractGreen(ref Color color, byte val)
        {
            if (color.G - val >= 0)
                color.G -= val;
            else
                color.G = 0;
        }
        public static void SubtractBlue(ref Color color, byte val)
        {
            if (color.B - val >= 0)
                color.B -= val;
            else
                color.B = 0;
        }
    }
}
