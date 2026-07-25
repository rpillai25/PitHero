namespace PitHero.Util
{
    /// <summary>Pure, camera-free math for horizontal distance-based sound attenuation and panning.</summary>
    public static class PositionalAudio
    {
        /// <summary>Returns a volume scale in [0,1]: 1 when sourceX is within [cameraLeft, cameraRight],
        /// linearly falling to 0 at maxAudibleDistancePx beyond the nearest horizontal edge.</summary>
        public static float CalculateVolumeScale(float sourceX, float cameraLeft, float cameraRight, float maxAudibleDistancePx)
        {
            if (sourceX >= cameraLeft && sourceX <= cameraRight)
                return 1f;

            float distance = sourceX < cameraLeft ? cameraLeft - sourceX : sourceX - cameraRight;
            if (maxAudibleDistancePx <= 0f || distance >= maxAudibleDistancePx)
                return 0f;

            return 1f - distance / maxAudibleDistancePx;
        }

        /// <summary>Returns a stereo pan in [-1,1]: 0 when sourceX is within [cameraLeft, cameraRight],
        /// growing toward -1 (left of camera) or +1 (right of camera) proportional to distance past the edge.</summary>
        public static float CalculatePan(float sourceX, float cameraLeft, float cameraRight, float maxAudibleDistancePx)
        {
            if (sourceX >= cameraLeft && sourceX <= cameraRight)
                return 0f;

            if (maxAudibleDistancePx <= 0f)
                return sourceX < cameraLeft ? -1f : 1f;

            if (sourceX < cameraLeft)
            {
                float pan = -(cameraLeft - sourceX) / maxAudibleDistancePx;
                return pan < -1f ? -1f : pan;
            }
            else
            {
                float pan = (sourceX - cameraRight) / maxAudibleDistancePx;
                return pan > 1f ? 1f : pan;
            }
        }
    }
}
