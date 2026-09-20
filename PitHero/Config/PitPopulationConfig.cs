namespace PitHero.Config
{
    /// <summary>
    /// How many monsters a pit floor holds (issue #422). Keyed on the DISPLAYED biome level (1-25),
    /// which is how the floors are numbered and how the hero re-progresses: a respawn drops the party
    /// at the tier base level facing displayed floor 1, so density has to ramp back up with them.
    /// Shared by the live PitGenerator and the headless VirtualPitGenerator so the two layers cannot
    /// drift apart again (they previously disagreed — the live one was fed the displayed level while
    /// the virtual one was fed cumulative depth).
    /// </summary>
    public static class PitPopulationConfig
    {
        /// <summary>
        /// Inclusive monster count range for the given displayed pit level. Ramps linearly from the
        /// depth-1 endpoints up to the biome's last floor, where the requested "at least 10" lands.
        /// </summary>
        public static void GetMonsterRange(int displayedPitLevel, out int min, out int max)
        {
            float t = Progress(displayedPitLevel, GameConfig.PitMonsterRampStartLevel, GameConfig.PitMonsterRampEndLevel);
            min = Round(GameConfig.PitMonsterMinAtFirstFloor, GameConfig.PitMonsterMinAtLastFloor, t);
            max = Round(GameConfig.PitMonsterMaxAtFirstFloor, GameConfig.PitMonsterMaxAtLastFloor, t);
            if (max < min)
                max = min;
        }

        // Fraction of the way from fromLevel to toLevel, clamped to [0,1].
        private static float Progress(int level, int fromLevel, int toLevel)
        {
            int span = toLevel - fromLevel;
            if (span <= 0)
                return 1f;
            float t = (level - fromLevel) / (float)span;
            if (t < 0f) return 0f;
            if (t > 1f) return 1f;
            return t;
        }

        private static int Round(int fromValue, int toValue, float t)
        {
            return (int)System.Math.Round(fromValue + (toValue - fromValue) * t, System.MidpointRounding.AwayFromZero);
        }
    }
}
