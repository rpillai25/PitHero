using Microsoft.Xna.Framework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// The path a hooked catch follows during a reel, shared by the fishing line (which stays attached
    /// while the catch is lifted out of the water) and the flying catch sprite, so the two always meet.
    /// Progress p runs 0..1 over <see cref="GameConfig.FishingReelSeconds"/>: first the catch is pulled
    /// straight up out of the water at the bobber, then it arcs through the air to the fisher.
    /// </summary>
    public static class FishingCatchPath
    {
        /// <summary>Fraction of the reel spent pulling the catch up out of the water.</summary>
        public const float LiftFraction = 0.35f;

        /// <summary>World px the catch rises above the bobber before it flies.</summary>
        public const float LiftHeight = 20f;

        /// <summary>Extra world px of arc at the middle of the flight.</summary>
        public const float ArcHeight = 18f;

        /// <summary>True while the catch is still on the line (being lifted out of the water).</summary>
        public static bool IsLifting(float p) => p < LiftFraction;

        /// <summary>The point where the catch leaves the water: the splash goes here.</summary>
        public static Vector2 WaterExit(Vector2 bobber) => bobber;

        /// <summary>Where the catch is at reel progress p, from the bobber to the fisher's catch point.</summary>
        public static Vector2 Position(Vector2 bobber, Vector2 target, float p)
        {
            if (p <= 0f)
                return bobber;
            var lifted = new Vector2(bobber.X, bobber.Y - LiftHeight);
            if (p < LiftFraction)
            {
                float t = p / LiftFraction;
                float eased = 1f - (1f - t) * (1f - t);
                return new Vector2(bobber.X, bobber.Y - LiftHeight * eased);
            }
            float f = (p - LiftFraction) / (1f - LiftFraction);
            if (f > 1f)
                f = 1f;
            var straight = Vector2.Lerp(lifted, target, f);
            return new Vector2(straight.X, straight.Y - ArcHeight * 4f * f * (1f - f));
        }
    }
}
