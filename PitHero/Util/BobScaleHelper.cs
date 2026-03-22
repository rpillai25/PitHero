using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.Util
{
    /// <summary>
    /// Helper class for creating a "bobbing" effect or scaling effect, over time
    /// </summary>
    public static class BobScaleHelper
    {
        private static short[] _bobScaleOffsets = new short[] { 1, 2, 3, 4, 5, 4, 3, 2, 1, 0, -1, -2, -3, -4, -5, -4, -3, -2, -1, 0 };

        public static Vector2 GetPositionOffset(int bobSpeed = 4, int axis = 1, int frameOffset = 0)
        {
            //return new Vector2(0, _bobScaleOffsets[Time.FrameCount / bobSpeed % 20]);
            return GetFramePositionOffset(bobSpeed, Time.FrameCount, frameOffset, axis);
        }

        public static Vector2 GetFramePositionOffset(int bobSpeed = 4, uint frameCount = 0, int frameOffset = 0, int axis = 1)
        {
            if(axis == 0)
            {
                return new Vector2(_bobScaleOffsets[(frameCount + frameOffset) / bobSpeed % 20], 0);
            }
            return new Vector2(0, _bobScaleOffsets[(frameCount + frameOffset) / bobSpeed % 20]);
        }

        public static Vector2 GetScaleOffset(int scaleSpeed = 4, float scaleFactor = 16, float minClamp = 1, float maxClamp = 999, int frameOffset = 0)
        {
            return new Vector2(Mathf.Clamp(Mathf.Radians(_bobScaleOffsets[(Time.FrameCount + frameOffset) / scaleSpeed % 10] * scaleFactor), minClamp, maxClamp),
                Mathf.Clamp(Mathf.Radians(_bobScaleOffsets[(Time.FrameCount + frameOffset) / scaleSpeed % 10] * scaleFactor), minClamp, maxClamp));
        }
    }
}
